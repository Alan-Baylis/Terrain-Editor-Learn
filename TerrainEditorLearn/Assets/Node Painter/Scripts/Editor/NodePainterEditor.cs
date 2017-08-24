using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

using TerrainComposer2.NodePainter.Utilities;

namespace TerrainComposer2.NodePainter
{
	[CustomEditor(typeof(NodePainter))]
	public class NodePainterEditor : Editor 
	{
		public NodePainter painter;
		public PaintingEditor editor;

//		private int curNodeTargetList = 0;

		private static bool debugGenerateRect = false;
		private static bool debugNodeRect = false;

		public bool vizCanvasOnTerrain = false;

		public bool expandTargets = false;

		private string SceneViewID { get { return "Scene-" + SceneView.lastActiveSceneView.GetInstanceID (); } }
		private string GUIWindowID { get { return "GUI-" + this.GetInstanceID (); } }

//		private static string[] syncChannelOptions = new string[]{ "None", "Splat", "Node Targets", "Node Children" };

		public void OnEnable () 
		{
			painter = (NodePainter)target;
			if  (painter.painter == null)
				throw new Exception ("Painter not initialized!");
			editor = new PaintingEditor (painter.painter);
			editor.MultiChannelSyncGUI = DrawMultiChannelSyncGUI;

			//GetPreviewTextures ();

			painter.UpdateTerrains ();
			GlobalPainting.UpdateTerrainBrush ();
			GlobalPainting.HideCanvasProjection ();

			if (!System.IO.Directory.Exists (Settings.paintingResourcesFolder))
			{
				Debug.LogWarning ("Resource folder does not exist! Please select a valid path in the settings!");
				return;
			}
		}

		public void OnDisable () 
		{
			editor.Close ();
			GlobalPainting.UpdateTerrainBrush ();
			GlobalPainting.HideCanvasProjection ();
		}

		public void OnSceneGUI () 
		{
			if (!painter.painter.hasCanvas)
				return;

			if (!editor.isMouseInWindow || Event.current.modifiers == EventModifiers.Alt)
			{
				GlobalPainting.UpdateTerrainBrush ();
				return;
			}
			
			if (vizCanvasOnTerrain)
			{
				GlobalPainting.ShowCanvasProjection (painter.transform.position + Vector3.up*painter.canvasSize.y, painter.transform.rotation, 
													new Vector2 (painter.transform.lossyScale.x*painter.canvasSize.x, painter.transform.lossyScale.z*painter.canvasSize.z),
													painter.painter.vizCanvas, painter.painter.canvasFormat == Painting.Format.Value);
			}

			if (debugGenerateRect)
			{
				Vector2 totalAreaSize = new Vector2 (painter.areaSize.x*painter.areaTiles.x, painter.areaSize.z*painter.areaTiles.y);
				Rect rect = new Rect (Vector2.Scale (painter.generateRect.position, totalAreaSize)-totalAreaSize/2, Vector2.Scale (painter.generateRect.size, totalAreaSize));
				rect.position += new Vector2 (painter.areaPos.x, painter.areaPos.z);
				Handles.DrawSolidRectangleWithOutline (new Vector3[] { new Vector3 (rect.xMin, 0, rect.yMin), new Vector3 (rect.xMax, 0, rect.yMin), new Vector3 (rect.xMax, 0, rect.yMax), new Vector3 (rect.xMin, 0, rect.yMax) }, new Color (1,1,1,0), Color.green);
			}

			if (debugNodeRect)
			{
				Rect rect = painter.nodeRect;
				Handles.DrawSolidRectangleWithOutline (new Vector3[] { new Vector3 (rect.xMin, 0, rect.yMin), new Vector3 (rect.xMax, 0, rect.yMin), new Vector3 (rect.xMax, 0, rect.yMax), new Vector3 (rect.xMin, 0, rect.yMax) }, new Color (1,1,1,0), Color.red);
			}

			painter.painter.blockPainting = Tools.current != Tool.None;

			if ((Event.current.type == EventType.MouseUp && Event.current.button == 0) || Event.current.button != 0)
				painter.painter.StopPainting (SceneViewID);

			editor.controlID = GUIUtility.GetControlID (FocusType.Passive);
			HandleUtility.AddDefaultControl (editor.controlID);
			if (Event.current.GetTypeForControl (editor.controlID) == EventType.MouseDown)
			{ // Block left-click in scene view
				GUIUtility.hotControl = editor.controlID;
//				Event.current.Use ();
			}

			Vector2 brushPos;
			Vector3 worldPos;
			if (painter.CalcBrushWorldPos (out brushPos, out worldPos))
			{
				if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
					Undo.RecordObject (painter, "Node Painter Shortcut");
				string undoRecord;
				bool blockPainting = editor.HandleShortcuts (out undoRecord) || painter.painter.blockPainting;
				if (!string.IsNullOrEmpty (undoRecord))
					Undo.FlushUndoRecordObjects ();
				
				if (Event.current.type == EventType.Layout)
				{
					if (blockPainting)
						GlobalPainting.UpdateTerrainBrush ();
					else if (painter.TCNodeTarget != null)
						GlobalPainting.ShowTerrainBrush (painter.painter, worldPos, painter.painter.curBrush.size * painter.TCNodeTarget.size.x * painter.transform.lossyScale.x);
					else
						GlobalPainting.ShowTerrainBrush (painter.painter, worldPos, painter.painter.curBrush.size * TC_Area2D.current.bounds.size.x * painter.transform.lossyScale.x);
				}

				float expand = painter.painter.curBrush.size;
				bool isInCanvas = (brushPos.x >= -expand && brushPos.x <= 1+expand) && (brushPos.y >= -expand && brushPos.y <= 1+expand);

				if (painter.painter.PaintUpdate (SceneViewID, editor.controlID, isInCanvas, blockPainting, brushPos))
					Repaint ();
			}
			else
			{
				if (GUIUtility.hotControl == editor.controlID)
					GUIUtility.hotControl = 0;
				GlobalPainting.UpdateTerrainBrush ();
			}

			if (editor.isMouseInWindow)
				HandleUtility.Repaint ();
		}

		public override void OnInspectorGUI () 
		{
			if (editor == null)
				return;

			if ((Event.current.type == EventType.MouseUp && Event.current.button == 0) || Event.current.button != 0)
				painter.painter.StopPainting (GUIWindowID);

			editor.GUIWindowID = GUIWindowID;

			if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
				Undo.RecordObject (painter, "Node Painter Settings");
			string undoRecord;
			editor.DoPainterGUI (out undoRecord);
			if (!string.IsNullOrEmpty (undoRecord))
			{
				Undo.FlushUndoRecordObjects ();
				//Debug.Log ("Recording " + undoRecord + " over " + Undo.GetCurrentGroupName () + "!");
			}

			if (painter.painter.hasCanvas)
			{
				expandTargets = EditorGUILayout.Foldout (expandTargets, "Node Targets", PaintingEditor.headerFoldout);
				if (expandTargets)
				{
					painter.TCArea = (TC_Area2D)EditorGUILayout.ObjectField ("TC2 Area 2D", painter.TCArea, typeof(TC_Area2D), true);
					if (painter.painter.canvasFormat != Painting.Format.Multi && painter.TCNode != null)
						GUILayout.Label ("Current node: " + painter.TCNode.name);
					while (painter.channelTargets.Count < painter.painter.canvasChannelCount || painter.channelTargets.Count < 1)
						painter.channelTargets.Add (new NodePainter.ChannelNodeTargets ());
//					painter.painter.curChannelIndex = Mathf.Clamp (painter.painter.curChannelIndex, 0, painter.painter.canvasChannelCount-1);
					DrawNodeTargetList (painter.channelTargets[painter.painter.curChannelIndex].targets);

					if (painter.channelTargets.Count == 0)
						GUILayout.Label ("No node targets specified!");

				}
			}

			AdditionalGUIUtility.Seperator ();

			if (GUILayout.Button ("Show Settings", EditorStyles.toolbarButton))
				SettingsWindow.Open ();

//			GUILayout.Label ("Painter's control ID: " + editor.controlID);
//			GUILayout.Label ("Active control ID: " + GUIUtility.hotControl);

			EditorGUILayout.Space ();

			if (editor.isMouseInWindow)
				Repaint ();
			
			if (vizCanvasOnTerrain != painter.painter.vizCanvasExternally)
			{
				vizCanvasOnTerrain = painter.painter.vizCanvasExternally;
				if (vizCanvasOnTerrain)
				{
					GlobalPainting.ShowCanvasProjection (painter.transform.position + Vector3.up*TC_Area2D.current.bounds.size.y, painter.transform.rotation, 
					                                     new Vector2 (painter.transform.localScale.x*painter.TCNodeTarget.size.x, painter.transform.localScale.z*painter.TCNodeTarget.size.z),
					                                     painter.painter.vizCanvas, painter.painter.canvasFormat == Painting.Format.Value);
				}
				else 
					GlobalPainting.HideCanvasProjection ();
				HandleUtility.Repaint ();
				SceneView.RepaintAll ();
			}
		}

		private void DrawNodeTargetList (List<TC_Node> nodeList) 
		{
			for (int i = 0; i < nodeList.Count; i++)
			{
				GUILayout.BeginHorizontal ();
				nodeList[i] = EditorGUILayout.ObjectField (nodeList[i], typeof(TC_Node), true) as TC_Node;
				if (GUILayout.Button ("X", GUILayout.Width (20)))
				{
					nodeList.RemoveAt (i);
					i--;
				}
				GUILayout.EndHorizontal ();
			}
			if (painter.channelTargets.Count == 0)
				GUILayout.Label ("No node targets specified!");
			if (GUILayout.Button ("Add Additional Node Target"))
				nodeList.Add (null);
		}

		public void DrawMultiChannelSyncGUI () 
		{
			if (GUILayout.Button ("Fetch from Nodes"))
			{
				GetPreviewTextures (true);
			}

//			painter.syncChannelOptions = (NodePainter.SyncChannelOptions)EditorGUILayout.EnumPopup ("Sync Channel Setup", painter.syncChannelOptions);

			//GUILayout.Label ("Synchronise Channel Setup");
//			painter.syncChannelOptions = (NodePainter.SyncChannelOptions)GUILayout.Toolbar ((int)painter.syncChannelOptions, syncChannelOptions);
//			if (painter.syncChannelOptions != NodePainter.SyncChannelOptions.None)
//			{
//				GUILayout.BeginVertical (GUI.skin.box);
//				if (painter.syncChannelOptions == NodePainter.SyncChannelOptions.Splat)
//				{
//					GUILayout.Label ("Splat!");
//				}
//				else if (painter.syncChannelOptions == NodePainter.SyncChannelOptions.NodeTargets)
//				{
//					GUILayout.Label ("Targets!");
//				}
//				else if (painter.syncChannelOptions == NodePainter.SyncChannelOptions.NodeChildren)
//				{
//					GUILayout.Label ("Children!");
//				}
//				GUILayout.EndVertical ();
//			}
		}

		/// <summary>
		/// Scans the group the node targets are in and fetches their preview
		/// </summary>
		private void GetPreviewTextures (bool overwriteExisting = false) 
		{
			for (int ch = 0; ch < painter.channelTargets.Count && ch < painter.painter.canvasChannelCount; ch++)
			{
				if (painter.painter.canvasChannelDefinitions[ch].displayTexture != null && !overwriteExisting)
					continue;
				List<TC_Node> nodes = painter.channelTargets[ch].targets;
				TC_Node node = nodes == null? null : nodes.FirstOrDefault (n => n != null); // TODO: Filter by outputID - make sure each preview is of the same output if possible
				if (node != null)
				{
					// Search layer the node is in
					TC_ItemBehaviour parent = node;
					while (parent.parentItem != null)
					{
						parent = parent.parentItem;
						if (parent.GetType () == typeof(TC_Layer) || parent.GetType () == typeof(TC_LayerGroup))
							break;
					}

					TC_Layer layer = parent as TC_Layer;
					TC_LayerGroup layerGroup = parent as TC_LayerGroup;
					TC_ItemBehaviour previewItem = null;

					if (layer != null)
						previewItem = (TC_ItemBehaviour)layer.selectItemGroup ?? (TC_ItemBehaviour)layer.selectNodeGroup;
					else if (layerGroup != null)
						previewItem = layerGroup.groupResult;

					if (previewItem != null)
					{ // Get preview texture of the given item
						Texture texPreview = null;
						if (previewItem.outputId == TC.colorOutput && previewItem.preview.tex != null) 
							texPreview = previewItem.preview.tex;
						else
						{
							if (previewItem.rtDisplay != null) texPreview = previewItem.rtDisplay;
							else if (previewItem.rtPreview != null) texPreview = previewItem.rtPreview;
							else if (previewItem.preview.tex != null) texPreview = previewItem.preview.tex;
						}

						if (texPreview != null)
						{ // Assign preview texture
							Painting.CanvasChannel channel = painter.painter.canvasChannelDefinitions[ch];
							channel.displayTexture = texPreview;
							painter.painter.canvasChannelDefinitions[ch] = channel;
						}
					}
				}
			}
		}
	}
}