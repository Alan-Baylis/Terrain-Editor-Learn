using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace TerrainComposer2.NodePainter
{
	[ExecuteInEditMode]
	public class NodePainter : MonoBehaviour
	{
		#region Variables

		// Painter
		[SerializeField]
		public Painting painter;

		// TerrainComposer 2
		public TC_Node TCNode;
		private TC_Node TCNode_Rep;
		public TC_Node TCNodeTarget { get { return TCNode != null? TCNode : (TCNode_Rep != null? TCNode_Rep : (TCNode_Rep = channelTargets.SelectMany ((t) => t.targets).FirstOrDefault ((n) => n != null))); } }
		public List<ChannelNodeTargets> channelTargets = new List<ChannelNodeTargets> ();
		public TC_Area2D repTCArea;
		public TC_Area2D TCArea { get { return repTCArea == null? TC_Area2D.current : repTCArea; } set { repTCArea = TC_Area2D.current != value? value : null; } }
		public TC_TerrainArea TCTerrainArea { get { return TCArea.currentTerrainArea; } }
		public bool TC2Ready { get { return TC_Generate.instance != null && TCArea != null; } }

		// Terrain Setup
		private TerrainCollider[] terrainColliders;

		// Related sizes, transformations and rects
		public Bounds unitBounds { get { return TCNodeTarget != null? TCNodeTarget.bounds : TCArea.bounds; } }
		public Vector3 canvasSize { get { return TCNodeTarget != null? TCNodeTarget.size : TCArea.bounds.size; } }
		public Vector3 areaSize { get { return TCArea.bounds.size; } }
		public Int2 areaTiles { get { return TCTerrainArea != null? TCTerrainArea.tiles : new Int2 (1, 1); } }
		public Vector3 areaPos { get { return TCTerrainArea != null? TCTerrainArea.transform.position : Vector3.zero; } }
		public Vector2 terrainNodeRatio { get { return new Vector2 (areaTiles.x*(areaSize.x/canvasSize.x), areaTiles.y*(areaSize.z/canvasSize.z)); } }

		public Rect localNodeRect { get { return new Rect (transform.position.x/canvasSize.x-transform.lossyScale.x/2, transform.position.z/canvasSize.z-transform.lossyScale.z/2, transform.lossyScale.x, transform.lossyScale.z); } }
		public Rect nodeRect { get { return new Rect (localNodeRect.x*canvasSize.x, localNodeRect.y*canvasSize.z, localNodeRect.width*canvasSize.x, localNodeRect.height*canvasSize.z); } }
		public Rect areaNodeRect { get { return new Rect (localNodeRect.x/terrainNodeRatio.x+0.5f, localNodeRect.y/terrainNodeRatio.y+0.5f, localNodeRect.width/terrainNodeRatio.x, localNodeRect.height/terrainNodeRatio.y); } }

		// Temporary paint record
		private Rect paintArea = new Rect ();
		private float paintBrushSize;

		public Rect generateRect { get { return new Rect (paintArea.x - paintBrushSize/2/terrainNodeRatio.x*transform.lossyScale.x, paintArea.y - paintBrushSize/2/terrainNodeRatio.y*transform.lossyScale.z,
														paintArea.width + paintBrushSize/terrainNodeRatio.x*transform.lossyScale.x, paintArea.height + paintBrushSize/terrainNodeRatio.y*transform.lossyScale.z); } }

		// Options
		public enum SyncChannelOptions { None, Splat, NodeTargets, NodeChildren }
		public SyncChannelOptions syncChannelOptions;

		[System.Serializable]
		public class ChannelNodeTargets 
		{
			public List<TC_Node> targets;
			public ChannelNodeTargets() 
			{ targets = new List<TC_Node> (); }
		}

		#endregion

		#region General Methods

		private void ValidateID () 
		{
			if (painter.ID == 0 || GameObject.FindObjectsOfType<NodePainter> ().Any ((NodePainter np) => np != this && np.painter.ID == painter.ID))
				painter.RecreateID ();
		}
		
		private void CheckInstanceManager ()
		{
#if !(UNITY_EDITOR && UNITY_5_6_OR_NEWER)
			PainterInstanceManager instanceManager = GameObject.FindObjectOfType<PainterInstanceManager>();
			if (instanceManager == null)
				instanceManager = new GameObject().AddComponent<PainterInstanceManager>();
			hideFlags = HideFlags.HideInHierarchy;
#endif
		}

		private void OnEnable ()
		{
			TCNode = GetComponent<TC_Node> ();

			if (painter == null)
				painter = new Painting ();
			painter.OnAssignCanvas -= AssignCanvas;
			painter.OnAssignCanvas += AssignCanvas;
			painter.OnPainting -= UpdatePaintArea;
			painter.OnPainting += UpdatePaintArea;
			painter.OnScaleCanvas -= ScaleRelative;
			painter.OnScaleCanvas += ScaleRelative;

			painter.Open();

			UpdateTerrains ();

#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SaveCacheWithScene;
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += SaveCacheWithScene;
#endif
		}

		private void OnDisable () 
		{
			painter.OnAssignCanvas -= AssignCanvas;
			painter.OnPainting -= UpdatePaintArea;
			painter.OnScaleCanvas -= ScaleRelative;
			painter.Hide ();

#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SaveCacheWithScene;
#endif
		}

		private void Start()
		{
			CheckInstanceManager();
			ValidateID();
		}

		private void Update()
		{
			painter.CheckCacheAsset();
			if (transform.hasChanged)
			{
				transform.hasChanged = false;
				UpdateNodeTargets();
				TC.AutoGenerate();
			}
		}

		private void ScaleRelative (float x, float y)
		{
			transform.localScale = new Vector3 (transform.localScale.x * x, transform.localScale.y, transform.localScale.z * y);
		}

#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
		private void SaveCacheWithScene(UnityEngine.SceneManagement.Scene scene, string path)
		{ // Save node painter caches
			if (gameObject.scene == scene)
				painter.SaveCurrentSession(true);
		}
#endif

		#endregion

		#region Canvas Assignment

		public void AssignCanvas (params RenderTexture[] textures) 
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty (gameObject.scene);
#else
				UnityEditor.EditorApplication.MarkSceneDirty ();
#endif
#endif

			if (textures == null || textures.Length == 0 || textures.Any ((RT) => RT == null))
			{
				if (TCNode != null)
					AssignToNode (Texture2D.blackTexture, TCNode);
				for (int ch = 0; ch < channelTargets.Count; ch++)
				{
					ChannelNodeTargets channelTarget = channelTargets[ch];
					for (int i = 0; i < channelTarget.targets.Count; i++)
						AssignToNode (Texture2D.blackTexture, channelTarget.targets[i]);
				}
			}
			else
			{
				if (TCNode != null)
					AssignToNode(textures[0], TCNode);
				for (int ch = 0; ch < painter.canvasChannelCount && ch < channelTargets.Count; ch++)
				{
					ChannelNodeTargets channelTarget = channelTargets[ch];
					int texIndex = Mathf.Clamp(Mathf.FloorToInt((float)ch / 4), 0, textures.Length - 1);
					Painting.Channels texChannels = painter.canvasFormat == Painting.Format.Multi ? (Painting.Channels)(ch % 4 + 2) : Painting.Channels.RGBA;
					for (int i = 0; i < channelTarget.targets.Count; i++)
						AssignToNode (textures[texIndex], texChannels, channelTarget.targets[i]);
				}
			}

			painter.canvasRotation = transform.localRotation.eulerAngles.y/180;
			if (!painter.isDrawing || Settings.autoGenerate)
			{
				if (TC_Generate.instance != null)
					TC.AutoGenerate (FinalizeGenerateRect ());
			}
		}

		public void UpdateNodeTargets () 
		{
			for (int ch = 0; ch < painter.canvasChannelCount && ch < channelTargets.Count; ch++)
			{
				ChannelNodeTargets channelTarget = channelTargets[ch];
				for (int i = 0; i < channelTarget.targets.Count; i++)
					UpdateNodeTarget (channelTarget.targets[i]);
			}
		}

		public void UpdateNodeTarget (TC_Node node)
		{
			if (node == null)
				return;
			if (node.inputKind != InputKind.File || node.inputFile != InputFile.Image)
			{ // Make sure it's an image node
				node.inputKind = InputKind.File;
				node.inputFile = InputFile.Image;
				node.Init();
				TC.RefreshOutputReferences(node.outputId, true);
			}

			if (TCNode != null && TCNode != node)
				node.size = TCNode.size;

			if (node.transform != transform)
			{
				node.transform.position = transform.position;
				node.transform.localScale = Vector3.one;
				node.transform.localScale = new Vector3 (transform.lossyScale.x/node.transform.lossyScale.x, transform.lossyScale.y/node.transform.lossyScale.y, transform.lossyScale.z/node.transform.lossyScale.z);
				node.transform.rotation = transform.rotation;
			}
		}

		public void AssignToNode (Texture tex, TC_Node node)
		{
			AssignToNode (tex, Painting.Channels.RGBA, node);
		}

		public void AssignToNode (Texture tex, Painting.Channels channels, TC_Node node)
		{
			if (node == null)
				return;
			
			UpdateNodeTarget (node);

			node.stampTex = tex;
//			node.size.y = 1000;
			if (channels != Painting.Channels.RGBA)
			{
				if (node.imageSettings == null)
					node.imageSettings = new ImageSettings ();
				int active = (int)channels - 2;
				for (int i = 0; i < node.imageSettings.colChannels.Length; i++)
					node.imageSettings.colChannels[i].active = i == active;
			}
			node.active = node.enabled = node.stampTex != null;
		}

		/// <summary>
		/// Updates the paint area with the brush position in world space
		/// </summary>
		public void UpdatePaintArea ()
		{
			if (!Settings.enablePartialGeneration)
				return;

			// Transform to global brush pos
			Vector3 wBrusPos = new Vector3 (painter.brushPos.x-0.5f, 0, painter.brushPos.y-0.5f);
			wBrusPos = (Vector3)(transform.localToWorldMatrix * wBrusPos);
			wBrusPos += new Vector3 ((transform.position.x-areaPos.x)/areaSize.x, 0, (transform.position.z-areaPos.z)/areaSize.z);
			Vector2 brushPos = new Vector2 (wBrusPos.x/terrainNodeRatio.x+0.5f, wBrusPos.z/terrainNodeRatio.y+0.5f);

			if (!painter.isDrawing)
				paintArea = new Rect (0, 0, 1, 1);
			else if (paintArea.position == Vector2.zero && paintArea.size == Vector2.zero)
				paintArea = new Rect (brushPos, Vector2.zero);
			else
			{
				paintArea.xMin = Mathf.Min (paintArea.xMin, brushPos.x);
				paintArea.yMin = Mathf.Min (paintArea.yMin, brushPos.y);
				paintArea.xMax = Mathf.Max (paintArea.xMax, brushPos.x);
				paintArea.yMax = Mathf.Max (paintArea.yMax, brushPos.y);
			}
			paintBrushSize = Mathf.Max (paintBrushSize, painter.curBrush.size);
		}

		private Rect FinalizeGenerateRect ()
		{
			if (!TC2Ready)
				return new Rect(0, 0, 1, 1);
			if (!Settings.enablePartialGeneration || !painter.isDrawing)
				return areaNodeRect;
			Rect rect = generateRect;
			paintArea = new Rect ();
			paintBrushSize = 0;
			return rect;
		}

#endregion

#region Terrains

		/// <summary>
		/// Updates all registered terrains by fetching them from the current or first terrain area, if existant, else searches all terrains
		/// </summary>
		public void UpdateTerrains () 
		{
			if (TCArea != null && TCArea.currentTerrainArea != null && TCArea.currentTerrainArea.terrains != null)
				terrainColliders = TCArea.currentTerrainArea.terrains.Where ((TCUnityTerrain TCT) => TCT .terrain != null).Select ((TCUnityTerrain TCT) => TCT.terrain.GetComponent<TerrainCollider> ()).ToArray ();
			else if (TCArea != null && TCArea.terrainAreas.Length > 0 && TCArea.terrainAreas[0] != null && TCArea.terrainAreas[0].terrains != null)
				terrainColliders = TCArea.terrainAreas[0].terrains.Where ((TCUnityTerrain TCT) => TCT .terrain != null).Select ((TCUnityTerrain TCT) => TCT.terrain.GetComponent<TerrainCollider> ()).ToArray ();
			else
				terrainColliders = FindObjectsOfType<TerrainCollider> ();
		}

		/// <summary>
		/// Calculates brush position of the mouse on the worldspace terrains according in the node space and returns whether it has hit a terrain at all
		/// </summary>
		public bool CalcBrushWorldPos (out Vector2 brushPos, out Vector3 worldPos)
		{
			brushPos = Vector2.zero;
			worldPos = Vector3.zero;
			if (Camera.current == null)
				return false; // Not in the scene GUI

			if (terrainColliders == null || terrainColliders.Length == 0)
				UpdateTerrains ();

			Vector2 mousePos = Event.current.mousePosition;
			mousePos.y = (Screen.height - mousePos.y) - 37.5f;
			Ray mouseRay = Camera.current.ScreenPointToRay (mousePos);
			RaycastHit hit;

			foreach (TerrainCollider terrainCol in terrainColliders)
			{
				if (terrainCol.Raycast (mouseRay, out hit, float.PositiveInfinity)) 
				{
					worldPos = hit.point;
					Vector3 localPos = transform.worldToLocalMatrix * (worldPos - transform.position);
					brushPos.x = localPos.x / canvasSize.x + 0.5f;
					brushPos.y = localPos.z / canvasSize.z + 0.5f;
					return true;
				}
			}
			return false;
		}

#endregion
	}
}