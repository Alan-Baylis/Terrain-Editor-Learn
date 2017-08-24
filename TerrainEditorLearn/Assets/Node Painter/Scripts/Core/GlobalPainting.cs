using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TerrainComposer2.NodePainter.Utilities;

namespace TerrainComposer2.NodePainter
{
	public static class GlobalPainting 
	{
		// Brush Visualization
		private static GameObject BrushProjPrefab;
		private static Projector BrushProjection;

		public static Color normalBrushColor = new Color (0.25f, 0.5f, 0.65f, 1f);
		public static Color invertedBrushColor = new Color (0.75f, 0.06f, 0.06f, 1f);

		public static Texture2D[] brushTextures;

		// Canvas Visualization
		private static GameObject CanvasProjPrefab;
		private static Projector CanvasProjection;

		public static Color canvasTerrainVizColor = new Color (0f, 1f, 0f, 1f);

		public static List<Color> colorPresets = new List<Color> { Color.black, Color.red, Color.green, Color.blue };
		public static List<Painting.Brush> brushPresets = new List<Painting.Brush> { 
			new Painting.Brush { mode=Painting.PaintMode.Add, 		type=0, size=0.25f, intensity=0.2f, falloff=0f, hardness=1f }, 
			new Painting.Brush { mode=Painting.PaintMode.Smoothen, 	type=0, size=1.00f, intensity=0.2f, falloff=0f, hardness=0.5f }, 
			new Painting.Brush { mode=Painting.PaintMode.Add, 		type=5, size=0.25f, intensity=0.1f } };

		static GlobalPainting () 
		{
			ReloadBrushTextures ();

			ReloadResources ();

			Settings.LoadBrushPresets ();
			Settings.LoadColorPresets ();
		}

		private static void ReloadResources () 
		{
			BrushProjPrefab = ResourceManager.LoadResource<GameObject> ("Visualization/BrushProjector.prefab");
			CanvasProjPrefab = ResourceManager.LoadResource<GameObject> ("Visualization/CanvasProjector.prefab");
		}

		#region Brush Textures

		/// <summary>
		/// Reloads the brush textures. Call when paths have changed.
		/// </summary>
		public static void ReloadBrushTextures ()
		{
		#if UNITY_EDITOR
			string texFolder = Application.dataPath.Substring (0, Application.dataPath.Length - 6) + Settings.brushStampsFolder + "/";
			if (!Directory.Exists (texFolder))
			{
				Debug.LogWarning ("BrushStamps-Folder does not exist! Please select a valid resource path in the settings!");
				brushTextures = new Texture2D[0];
			}
			else
			{
				string[] texPaths = Directory.GetFiles (texFolder);

				List<Texture2D> textures = new List<Texture2D> ();
				foreach (string texPath in texPaths)
				{
					Texture2D tex = ResourceManager.LoadResource<Texture2D> (texPath);
					if (tex != null) 
					{
						tex.wrapMode = TextureWrapMode.Clamp;
						textures.Add (tex);
					}
				}
				brushTextures = textures.ToArray ();
			}
		#else 
			brushTextures = Resources.LoadAll<Texture2D> ("BrushStamps/");
		#endif
			brushTextures = brushTextures.OrderBy ((Texture2D tex) => getSortOrder(tex.name)).ToArray ();
		}

		private static int getSortOrder (string name)
		{
			string nameChars = new string(name.TakeWhile(char.IsDigit).ToArray());
			int orderChar = nameChars.Length;
			int.TryParse (nameChars, out orderChar);
			return orderChar;
		}

		#endregion

		#region Projection Viz

		/// <summary>
		/// Calculates the brush position in the current GUI space and returns whether it is relevant for the canvas based on GUICanvasPosition
		/// </summary>
		public static bool CalcBrushGUIPos (Rect GUICanvasPos, float brushSize, out Vector2 brushPos)
		{
			brushPos = Vector2.zero;
			if (GUICanvasPos.size == Vector2.zero)
				return false; // No GUI texture visible
			Vector2 mousePos = Event.current.mousePosition;
			float expand = Settings.fixedCanvasPaintBorder + brushSize * Settings.relativeCanvasPaintBorder;
			Rect expandedRect = new Rect (GUICanvasPos.x-expand, GUICanvasPos.y-expand, GUICanvasPos.width+2*expand, GUICanvasPos.height+2*expand);
			if (expandedRect.Contains (mousePos))
			{ // Canvas is targetted. Calculate local brushPos
				brushPos = GUICanvasPos.center - mousePos;
				brushPos.x = 1 - brushPos.x / GUICanvasPos.width - 0.5f;
				brushPos.y = brushPos.y / GUICanvasPos.height + 0.5f;
				return true;
			}
			return false;
		}


		// BRUSH PROJECTION

		public static void ShowTerrainBrush (Painting paint, Vector3 brushPos, float brushSize)
		{ // Show brush at position
			UpdateTerrainBrush ();

			if (BrushProjection != null && BrushProjection.material != null)
			{
				BrushProjection.transform.position = brushPos + new Vector3 (0, BrushProjection.farClipPlane/2, 0);
				BrushProjection.orthographicSize = brushSize / 2;

				// General
				BrushProjection.material.SetInt ("sizeX", paint.sizeX);
				BrushProjection.material.SetInt ("sizeY", paint.sizeY);

				// Set Brush Texture
				if (brushTextures == null)
					ReloadBrushTextures ();
				paint.curBrush.type = Mathf.Clamp (paint.curBrush.type, 0, GlobalPainting.brushTextures.Length-1);
				Texture2D brushTex = brushTextures.Length > 0? GlobalPainting.brushTextures[paint.curBrush.type] : null;
				BrushProjection.material.SetInt ("_brushType", paint.brushFunc);
				BrushProjection.material.SetTexture ("_brushTex", brushTex);

				// Set Brush Parameters
				BrushProjection.material.SetFloat ("_intensity", paint.curBrush.intensity);
				BrushProjection.material.SetFloat ("_size", paint.curBrush.size);
				BrushProjection.material.SetFloat ("_falloff", paint.curBrush.falloff);
				BrushProjection.material.SetFloat ("_hardness", paint.curBrush.hardness);
				BrushProjection.material.SetFloat ("_targetValue", paint.targetValue);

				// Apply brush rotation matrix
				Matrix4x4 rotMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, paint.brushRotation * 180), Vector3.one);
				BrushProjection.material.SetMatrix("_rotationMatrix", rotMatrix);

				BrushProjection.material.SetColor ("_Color", paint.invertPainting? invertedBrushColor : normalBrushColor);

				BrushProjection.enabled = true;
			}
		}

		public static void UpdateTerrainBrush () 
		{ // Update and hide brush
			if (BrushProjection == null || BrushProjection.material == null)
			{
				GameObject brushProjObj = GameObject.Find ("NodePainter_BrushProjector");
				DestroyObj (brushProjObj);

				if (BrushProjPrefab == null)
					ReloadResources ();
				brushProjObj = BrushProjPrefab != null? Object.Instantiate (BrushProjPrefab) : null;

				if (brushProjObj != null)
				{
					brushProjObj.name = "NodePainter_BrushProjector";
					brushProjObj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

					BrushProjection = brushProjObj.GetComponentInChildren<Projector> ();
					if (BrushProjection == null)
						DestroyObj (brushProjObj);
					else
						BrushProjection.material = new Material (BrushProjection.material);
				}
			}
			
			if (BrushProjection != null)
				BrushProjection.enabled = false;
		}


		// CANVAS PROJECTION

		private static void CheckCanvasProjection () 
		{
			if (CanvasProjection == null || CanvasProjection.material == null)
			{
				GameObject canvasProjObj = GameObject.Find ("NodePainter_CanvasProjector");
				DestroyObj (canvasProjObj);

				if (CanvasProjPrefab == null)
					ReloadResources ();
				canvasProjObj = CanvasProjPrefab != null? Object.Instantiate (CanvasProjPrefab) : null;

				if (canvasProjObj != null)
				{
					canvasProjObj.name = "NodePainter_CanvasProjector";
					canvasProjObj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

					CanvasProjection = canvasProjObj.GetComponentInChildren<Projector> ();
					if (CanvasProjection == null)
						DestroyObj (canvasProjObj);
					else
						CanvasProjection.material = new Material (CanvasProjection.material);
				}

				if (CanvasProjection != null)
					CanvasProjection.enabled = false;
			}
		}

		public static void ShowCanvasProjection (Vector3 pos, Quaternion rot, Vector2 size, Texture canvasTex, bool isValue = false)
		{ // Show canvas projection
			CheckCanvasProjection ();
			if (CanvasProjection != null && CanvasProjection.material != null)
			{
				CanvasProjection.transform.position = pos;
				CanvasProjection.transform.rotation = Quaternion.Euler (90, rot.eulerAngles.y, 0);
				CanvasProjection.transform.localScale = Vector3.one;

				CanvasProjection.orthographicSize = size.y/2;
				CanvasProjection.aspectRatio = size.x/size.y;

				if (canvasTex != null)
					CanvasProjection.material.SetTexture ("_CanvasTex", canvasTex);
				CanvasProjection.material.SetColor ("_TintColor", (isValue? canvasTerrainVizColor : new Color (1,1,1,0.5f)));
				CanvasProjection.material.SetFloat ("_Strength", Settings.terrainCanvasOverlayStrength);

				CanvasProjection.enabled = true;
			}
		}

		public static void UpdateCanvasProjection ()
		{
			CheckCanvasProjection ();
			if (CanvasProjection != null)
				CanvasProjection.material.SetFloat ("_Strength", Settings.terrainCanvasOverlayStrength);
		}

		public static void HideCanvasProjection () 
		{
			if (CanvasProjection == null)
				DestroyObj (GameObject.Find ("NodePainter_CanvasProjector"));
			if (CanvasProjection != null)
				CanvasProjection.enabled = false;
		}

		#endregion

		#region Presets

		public static void DeleteColorPreset (object data)
		{
			int index = (int)data;
			if (index >= 0 && index < colorPresets.Count)
				colorPresets.RemoveAt (index);
			Settings.SaveColorPresets ();
		}

		public static void DeleteBrushPreset (object data)
		{
			int index = (int)data;
			if (index >= 0 && index < brushPresets.Count)
				brushPresets.RemoveAt (index);
			Settings.SaveBrushPresets ();
		}

		#endregion

		public static void DestroyObj (Object obj)
		{
			if (obj != null)
			{
			#if UNITY_EDITOR
				Object.DestroyImmediate (obj, false);
			#else
				Object.Destroy (obj);
			#endif
			}
		}
	}

}