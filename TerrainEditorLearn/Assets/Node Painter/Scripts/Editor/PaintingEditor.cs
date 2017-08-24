using System;

using UnityEngine;
using UnityEditor;

using TerrainComposer2.NodePainter.Utilities;

namespace TerrainComposer2.NodePainter
{
	public class PaintingEditor
	{
		public Painting painter;
		public int controlID;

		public bool isMouseInWindow { get { return new Rect (0, 0, Screen.width, Screen.height).Contains (Event.current.mousePosition); } }
		public string GUIWindowID = "GUI";

		#region GUI

		private static string[] formatOptions = new string[]{ "Color", "Value", "Multi" };
		private static string[] channelOptions = new string[]{ "RGBA", "RGB", "R", "G", "B", "A", "Max" };
		private static string[] multiDisplayOptions = new string[]{ "Current", "Mix" };

		private static Color selectCol = new Color (0.242f, 0.488f, 0.902f, 1f);

		private static Texture2D warningIcon;
		private static Texture2D eyeOpenIcon;
		private static Texture2D eyeClosedIcon;

		private static GUIStyle brushSelectorButton;
		private static GUIStyle channelSelectorButton;
		private static GUIStyle colorButton;
		internal static GUIStyle headerFoldout;

		private static Texture2D imgBGTex;
		private static Texture2D imgBorderTex;

		#endregion

		#region Temp Variables

		// Expand
		private bool expandIO = true, expandCanvasPreview = true, expandColor = true, expandBrush = true, expandMods = false, expandResize = false, expandDebug = false;

		// Create Canvas
		public string canvasName = "New Canvas";
		public int resX = 1024, resY = 1024;
		private Painting.Format format = Painting.Format.Value;
		public int channelCount = 1;

		// Visualization
		private Material brushGUIMaterial;

		// Canvas Preview
		private bool editName;
		private Painting.Channels workChannels = Painting.Channels.RGB; // 0:RGBA 1:RGB 2:R 3:G 4:B 5:A
		private Painting.CanvasVizState visualizationState = Painting.CanvasVizState.All;

		// Fill
		private Color fillColor = new Color (0, 0, 0, 0);
		private float fillValue = 0;

		// Debug
		public int testIterations = 1000;

		// Shortcuts
		private bool pressedSpace = false;

		// Misc
		private int lastBrushPreset = -1;
		private Tool lastTool;

		// Channel Texture Picker
#if UNITY_5_2 || UNITY_5_3_OR_NEWER
		private int pickerControlID;
		private int pickerChannelIndex;
#endif

		#endregion

		public Action MultiChannelSyncGUI;

		#region General Methods

		public PaintingEditor (Painting paint) 
		{
			painter = paint;

			GlobalPainting.ReloadBrushTextures ();
			GlobalPainting.UpdateTerrainBrush ();

			lastTool = Tools.current;
			Tools.current = Tool.None;
		}

		public void CheckGUIStyles () 
		{
			
			if (imgBGTex == null)
				imgBGTex = AssetDatabase.LoadAssetAtPath<Texture2D> (Settings.paintingResourcesFolder + "/GUI/GUI_SelectBG.png");
			if (imgBorderTex == null)
				imgBorderTex = AssetDatabase.LoadAssetAtPath<Texture2D> (Settings.paintingResourcesFolder + "/GUI/GUI_SelectBorder.png");

			if (brushSelectorButton == null)
				brushSelectorButton = new GUIStyle ();
			brushSelectorButton.onNormal.background = imgBGTex;

			if (channelSelectorButton == null)
			{ // Setup GUIStyle
				channelSelectorButton = new GUIStyle ();
				channelSelectorButton.border = new RectOffset (1, 1, 1, 1);
				channelSelectorButton.margin = new RectOffset (5, 5, 5, 5);
				channelSelectorButton.padding = new RectOffset (5, 5, 5, 5);
			}
			channelSelectorButton.normal.background = imgBorderTex;

			if (warningIcon == null)
				warningIcon = (Texture2D)EditorGUIUtility.Load ("icons/console.warnicon.sml.png");
			if (eyeOpenIcon == null)
				eyeOpenIcon  = (Texture2D)EditorGUIUtility.Load ("icons/animationvisibilitytoggleon.png");
			if (eyeClosedIcon  == null)
				eyeClosedIcon = (Texture2D)EditorGUIUtility.Load ("icons/animationvisibilitytoggleoff.png");

			if (colorButton == null)
			{
				colorButton = new GUIStyle (GUI.skin.box);
				colorButton.fixedHeight = colorButton.fixedWidth = 15;
				colorButton.margin = new RectOffset (0, 0, 2, 0);
				colorButton.contentOffset = new Vector2 (0, -1);
				colorButton.alignment = TextAnchor.MiddleCenter;
			}

			if (headerFoldout == null)
			{
				headerFoldout = new GUIStyle (EditorStyles.foldout);
				headerFoldout.font = EditorStyles.boldFont;
				headerFoldout.margin.top += 2;
			}
		}

		public void Close () 
		{
			Tools.current = lastTool == Tool.None? Tool.Move : lastTool;
		}

		#endregion

		public bool DoPainterGUI (out string undo) 
		{
			bool repaint = false;
			undo = null;

			CheckGUIStyles ();

			#region Canvas IO

			expandIO = EditorGUILayout.Foldout (expandIO, "Canvas IO", headerFoldout);
			if (expandIO)
			{
				EditorGUI.BeginDisabledGroup (!painter.hasCanvas);
				if (GUILayout.Button ("Delete Canvas"))
					painter.DeleteCanvas ();
				EditorGUI.EndDisabledGroup ();

				// NATIVE COMBINED BYTES

				GUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent ("Native", "Native combined byte format that perfectly stores the canvas in one file with extension .bytes"), GUILayout.Width(60));
				if (GUILayout.Button("Load"))
				{
					string path = EditorUtility.OpenFilePanel("Load canvas data", Application.dataPath, "bytes");
					if (!string.IsNullOrEmpty(path))
						painter.ImportCanvas(path);
				}
				EditorGUI.BeginDisabledGroup(!painter.hasCanvas);
				if (GUILayout.Button("Save"))
				{
					string path = EditorUtility.SaveFilePanel("Save Canvas", Application.dataPath, painter.canvasName, "bytes");
					if (!string.IsNullOrEmpty(path))
					{
						painter.ExportCanvas(path);
						AssetDatabase.Refresh();
					}
				}
				EditorGUI.EndDisabledGroup();
				GUILayout.EndHorizontal();

				// TEXTURE

				GUILayout.BeginHorizontal ();
				GUILayout.Label(new GUIContent ("Texture", "LOSSY 8-Bit PNG Texture format for exporting to all image editors that do not support RAW. " + 
					"Stores most canvas meta information except canvas channel count. " +
					"For multi-canvas, save files are split in name(n) files for each 4 channels."), GUILayout.Width(60));
				if (GUILayout.Button("Import"))
				{
					string path = EditorUtility.OpenFilePanel("Import texture", Application.dataPath, "png");
					if (!painter.ImportCanvas(path))
						Debug.LogError("Failed importing texture into canvas!");
				}
				EditorGUI.BeginDisabledGroup(!painter.hasCanvas);
				if (GUILayout.Button("Export"))
				{
					string path = EditorUtility.SaveFilePanel("Export texture", Application.dataPath, painter.canvasName, "png");
					if (!string.IsNullOrEmpty(path))
					{
						painter.ExportCanvas(path);
						AssetDatabase.Refresh();
					}
				}
				EditorGUI.EndDisabledGroup();
				GUILayout.EndHorizontal ();

				// RAW FILE

				GUILayout.BeginHorizontal ();
				GUILayout.Label(new GUIContent("Raw", "16-Bit RAW File export format for directly exporting to terrains and few image editors. " + 
					"Does not store ANY canvas meta information, needs to be re-entered upon import. " + 
					"For multi-canvas, save files are split in name(n) files for each 4 channels."), GUILayout.Width(60));
				if (GUILayout.Button ("Import"))
				{
					string path = EditorUtility.OpenFilePanel ("Import raw file", Application.dataPath, "raw");
					if (!string.IsNullOrEmpty(path))
						ImportExportDialogue.ImportRawDialogue(painter, path);
				}
				EditorGUI.BeginDisabledGroup(!painter.hasCanvas);
				if (GUILayout.Button("Export"))
				{
					string path = EditorUtility.SaveFilePanel("Export raw file", Application.dataPath, painter.canvasName, "raw");
					if (!string.IsNullOrEmpty(path))
					{
						painter.ExportCanvas(path);
						AssetDatabase.Refresh();
					}
				}
				EditorGUI.EndDisabledGroup();
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent ("Cache", "Cache file found in the cache folder for quickly creating backup-points. " + 
					"Cannot restore overridden cache file!"), GUILayout.Width(60));
				if (GUILayout.Button("Load", GUI.skin.button))
					painter.LoadLastSession(true);
				if (GUILayout.Button("Save", GUI.skin.button))
					painter.SaveCurrentSession();
				GUILayout.EndHorizontal();
			}

			#endregion

			#region Canvas Creation

			if (!painter.hasCanvas) 
			{ // Canvas creation wizard
				SubSectionSeperator ();

				EditorGUI.BeginChangeCheck ();

				GUILayout.BeginVertical(GUI.skin.box);
				GUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent ("Load Cache", "Specify an already existing cache file to sync painters between scenes."));
				painter.cache_Asset = (TextAsset)EditorGUILayout.ObjectField(painter.cache_Asset, typeof(TextAsset), false);
				if (GUILayout.Button("Load", GUI.skin.button))
					painter.LoadLastSession(true);
				GUILayout.EndHorizontal();

				GUILayout.Label ("Create new canvas:", EditorStyles.boldLabel);
				canvasName = EditorGUILayout.TextField ("Name", canvasName);

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Size X/Y");
				resX = Mathf.Clamp (EditorGUILayout.IntField (resX), 32, 4096);
				resY = Mathf.Clamp (EditorGUILayout.IntField (resY), 32, 4096);
				GUILayout.EndHorizontal ();

				format = (Painting.Format)GUILayout.Toolbar ((int)format, formatOptions);

				EditorGUI.BeginDisabledGroup (format != Painting.Format.Multi);
				channelCount = Mathf.Clamp (EditorGUILayout.IntField ("Channel Count", channelCount), 1, 32);
				EditorGUI.EndDisabledGroup ();

				if (EditorGUI.EndChangeCheck ())
					undo = "Creation Settings";

				if (GUILayout.Button ("Create!"))
					painter.NewCanvas (resX, resY, format, canvasName, format == Painting.Format.Multi? channelCount : 1);
				GUILayout.EndVertical ();
			}

			#endregion

			if (!painter.hasCanvas)
			{ // Can't display canvas related GUI
				SubSectionSeperator();
				ShowUndoButtons();
				return repaint;
			}

			SectionSeperator ();

			#region Canvas Preview

			GUILayout.BeginHorizontal ();
			if (editName)
			{
				painter.canvasName = GUILayout.TextField(painter.canvasName, GUILayout.ExpandWidth(true));
#if UNITY_EDITOR
				GUILayout.Space(5);
				GUILayout.Label(new GUIContent("Cache", "Cache-File for this painter. Used to sync painters in different scenes by using the same cache file."), GUILayout.ExpandWidth(false));
				TextAsset cacheAsset = (TextAsset)EditorGUILayout.ObjectField(painter.cache_Asset, typeof(TextAsset), false, GUILayout.ExpandWidth(false), GUILayout.MaxWidth (100));
				if (cacheAsset != painter.cache_Asset)
				{ // New cache file to use has been assigned, load it
					painter.cache_Asset = cacheAsset;
					painter.LoadLastSession();
				}
#endif
			}
			else
			{
				expandCanvasPreview = EditorGUILayout.Foldout (expandCanvasPreview, painter.canvasFormat.ToString() + "-Format " + painter.canvasName + " Preview", headerFoldout);
			}
			if (GUILayout.Button ("E", GUILayout.Width (20)))
				editName = !editName;
			if (GUILayout.Button (painter.vizCanvasExternally? eyeOpenIcon : eyeClosedIcon, GUILayout.Width (20)))
				painter.vizCanvasExternally = !painter.vizCanvasExternally;
			GUILayout.EndHorizontal ();

			// Render Texture

			if (!expandCanvasPreview)
				painter.canvasVizState = Painting.CanvasVizState.None;
			if (expandCanvasPreview)
			{
//				float textureSize = Mathf.Max (Mathf.Min (Mathf.Min (painter.sizeX, EditorGUIUtility.currentViewWidth), Settings.maxCanvasPreviewSize) - 46, 256);
				float textureSize = Mathf.Max (Mathf.Min (EditorGUIUtility.currentViewWidth, Settings.maxCanvasPreviewSize) - 46, 256);
				float border = Settings.fixedCanvasPaintBorder + textureSize * painter.curBrush.size * Settings.relativeCanvasPaintBorder;
				Rect textureRect = GUILayoutUtility.GetRect (textureSize, textureSize * painter.sizeY/painter.sizeX, GUILayout.ExpandWidth (false));
				textureRect = new Rect (textureRect.x+border, textureRect.y+border, textureRect.width-2*border, textureRect.height-2*border);

				if (painter.canvasFormat == Painting.Format.Value)
				{ // Grayscale
					RTTextureViz.DrawTexture (painter.vizCanvas, textureRect, 1f, false);
				}
				else if (painter.canvasFormat == Painting.Format.Multi)
				{ // Multi Channel
					int localChannel = painter.curChannelIndex%4;
					if (painter.canvasVizState == Painting.CanvasVizState.All)
						RTTextureViz.DrawTexture (painter.vizCanvas, textureRect, 1, 2, 3, 5);
					else
						RTTextureViz.DrawTexture (painter.vizCanvas, textureRect, localChannel+1, localChannel+1, localChannel+1, 5);
				}
				else
				{
					DrawCanvasPreview (textureRect, painter.vizCanvas, workChannels);
				}

				if (painter.canvasFormat == Painting.Format.Color)
				{ // Channel Toolbar
					GUILayout.BeginHorizontal ();
					workChannels = (Painting.Channels)GUILayout.Toolbar ((int)workChannels, channelOptions, EditorStyles.toolbarButton, GUILayout.Width (textureSize));
					painter.canvasVizState = Painting.CanvasVizState.All;
					GUILayout.Space (12);
					GUILayout.EndHorizontal ();
				}
				else if (painter.canvasFormat == Painting.Format.Multi)
				{ // Visualization State
					GUILayout.BeginHorizontal ();
					painter.canvasVizState = visualizationState = (Painting.CanvasVizState)(GUILayout.Toolbar ((int)visualizationState - 1, multiDisplayOptions, EditorStyles.toolbarButton, GUILayout.Width (textureSize))+1);
					GUILayout.Space (12);
					GUILayout.EndHorizontal ();
				}
				else
					painter.canvasVizState = Painting.CanvasVizState.All;

				// GUI Painting

//				repaint = true;
				string undoRecord = null;
				bool blockPainting = HandleShortcuts (out undoRecord);
				if (!string.IsNullOrEmpty (undoRecord))
					undo = undoRecord;
				
				if (Event.current.type != EventType.Layout)
				{
					Vector2 brushPos;
					if (GlobalPainting.CalcBrushGUIPos (textureRect, textureSize * painter.curBrush.size, out brushPos))
					{
						int controlID = GUIUtility.GetControlID (FocusType.Passive);
						if (Event.current.isMouse && painter.PaintUpdate ("GUI", controlID, true, blockPainting, brushPos))
							repaint = true;
						if (Event.current.type == EventType.Repaint)
						{ // Draw brush in GUI
							if (imgBGTex != null)
							{
								GUI.BeginClip (textureRect);
								setupBrushVizMat (textureRect);
								Rect brushRect = new Rect (Vector2.zero, textureRect.size * painter.curBrush.size);
								brushRect.center = Event.current.mousePosition;
								if (Event.current.type == EventType.Repaint)
									Graphics.DrawTexture (brushRect, imgBGTex, brushGUIMaterial);
								GUI.EndClip ();
							}
						}
					}
					else if (!isMouseInWindow)
						painter.StopPainting (GUIWindowID);
				}
			}

			#endregion

			SubSectionSeperator();

			ShowUndoButtons();

			painter.blockPainting = Tools.current != Tool.None;
			if (GUILayout.Button (painter.blockPainting? "Start Painting" : "Stop Painting"))
			{
				if (painter.blockPainting)
				{
					lastTool = Tools.current;
					Tools.current = Tool.None;
					painter.blockPainting = false;
				}
				else
				{
					Tools.current = lastTool == Tool.None? Tool.Move : lastTool;
					painter.blockPainting = true;
				}
			}

			SectionSeperator ();

			#region Colors

			if (painter.canvasFormat == Painting.Format.Color)
			{
				// Foldout with Color Presets
				GUILayout.BeginHorizontal ();
				expandColor = EditorGUILayout.Foldout (expandColor, "Color", headerFoldout);
				GUILayout.Space (5);
				for (int colI = 0; colI < GlobalPainting.colorPresets.Count; colI++)
				{
					if (PresetButton (" ", GlobalPainting.colorPresets[colI], GlobalPainting.DeleteColorPreset, colI))
					{
						painter.curColor = GlobalPainting.colorPresets[colI];
						undo = "Color Preset";
					}
				}
				GUILayout.Space (5);
				if (PresetButton ("+", painter.curColor, null, 0))
				{
					GlobalPainting.colorPresets.Add (painter.curColor);
					Settings.SaveColorPresets ();
					undo = "Color Preset";
				}
				GUILayout.FlexibleSpace ();
				GUILayout.EndHorizontal ();

				if (expandColor)
				{ // Modify color parameters
					EditorGUI.BeginChangeCheck ();
					painter.curColor = EditorGUILayout.ColorField ("Color", painter.curColor);
					painter.colorIntensity = EditorGUILayout.Slider ("Intensity", painter.colorIntensity, 0f, 10f);
					if (EditorGUI.EndChangeCheck ())
						undo = "Color Settings";
				}

				SectionSeperator ();
			}
			else
			{
				painter.colorIntensity = 1;
				painter.curColor = Color.white;
			}

			#endregion

			#region Multi Channel

			if (painter.canvasFormat == Painting.Format.Multi)
			{
				// Canvas channel selection
				string newUndoRecord = null;
				painter.curChannelIndex = Mathf.Clamp (painter.curChannelIndex, 0, painter.canvasChannelCount-1);
				if (painter.channelVizColors == null)
					painter.ReadColorsIn ();
				Rect channelsRect = DrawResponsiveGrid ((int)EditorGUIUtility.currentViewWidth-36-20, Settings.imgPreviewTexSize, painter.canvasChannelCount, (int chInd, Rect rect) => {

					Painting.CanvasChannel channel = painter.canvasChannelDefinitions[chInd];
					Rect colorRect = rect;
					if (channel.displayTexture != null)
					{
						RTTextureViz.DrawTexture (channel.displayTexture, rect, 1, 2, 3, 5);
						colorRect.height = Settings.imgPreviewTexSize/8;
						colorRect.y += rect.height-colorRect.height;
					}
					if (painter.channelVizColors.Length > chInd)
						RTTextureViz.DrawTexture (Texture2D.whiteTexture, colorRect, painter.channelVizColors[chInd]);
					
					if (Event.current.type == EventType.MouseUp && Event.current.button == 1 && rect.Contains (Event.current.mousePosition))
					{
						GenericMenu context = new GenericMenu ();

//						context.AddItem (new GUIContent ("Delete Channel"), false, () => { throw new NotImplementedException (); });
//						context.AddItem (new GUIContent ("Insert new Channel"), false, () => { throw new NotImplementedException (); });
//						context.AddItem (new GUIContent ("Move Channel Up"), false, () => { throw new NotImplementedException (); });
//						context.AddItem (new GUIContent ("Move Channel Down"), false, () => { throw new NotImplementedException (); });

						#if UNITY_5_2 || UNITY_5_3_OR_NEWER
						context.AddItem (new GUIContent ("Select Display Texture"), false, () => {
							pickerControlID = EditorGUIUtility.GetControlID (FocusType.Passive);
							pickerChannelIndex = chInd;
							EditorGUIUtility.ShowObjectPicker<Texture> (channel.displayTexture, true, null, pickerControlID);
						});
						#endif

						context.ShowAsContext ();
						Event.current.Use ();
					}
					painter.canvasChannelDefinitions[chInd] = channel;

					if (GUI.Toggle (rect, painter.curChannelIndex == chInd, GUIContent.none, channelSelectorButton))
					{
						painter.curChannelIndex = chInd;
						newUndoRecord = "Changed channels";
					}

					if (painter.curChannelIndex == chInd && imgBGTex != null)
						RTTextureViz.DrawTexture (imgBGTex, rect, new Color (1, 1, 1, 0.5f));
					if (painter.curChannelIndex == chInd)
					{
						GUI.color = new Color (0, 0, 0, 1f);
						GUI.Box (rect, GUIContent.none, channelSelectorButton);
						GUI.color = Color.white;
					}
					//RTTextureViz.DrawTexture (imgBorderTex, rect, new Color (0, 0, 0, 0.8f));
				});
				if (!string.IsNullOrEmpty (newUndoRecord))
					undo = newUndoRecord;
				#if UNITY_5_2 || UNITY_5_3_OR_NEWER
				if (Event.current.type == EventType.ExecuteCommand && Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID () == pickerControlID)
				{
					undo = "Channel Texture";
					Painting.CanvasChannel channel = painter.canvasChannelDefinitions[pickerChannelIndex];
					channel.displayTexture = (Texture)EditorGUIUtility.GetObjectPickerObject ();
					painter.canvasChannelDefinitions[pickerChannelIndex] = channel;
				}
				#endif

				Rect addButtonRect = new Rect (EditorGUIUtility.currentViewWidth-20-20, channelsRect.y, 20, Settings.imgPreviewTexSize/2);
				Rect delButtonRect = new Rect (EditorGUIUtility.currentViewWidth-20-20, channelsRect.y+Settings.imgPreviewTexSize/2, 20, Settings.imgPreviewTexSize/2);
				if (GUI.Button (addButtonRect, "+"))
				{
					painter.AddNewCanvasChannel ();
				}
				if (GUI.Button (delButtonRect, "-"))
				{
					painter.DeleteLastCanvasChannel ();
				}
					
			#if !UNITY_5_2 && !UNITY_5_3_OR_NEWER
				EditorGUI.BeginChangeCheck ();
				GUILayout.BeginHorizontal ();
				
				Painting.CanvasChannel curChannel = painter.canvasChannelDefinitions[painter.curChannelIndex];
				GUILayout.Label ("Display Texture");
				curChannel.displayTexture = EditorGUILayout.ObjectField (curChannel.displayTexture, typeof(Texture), false) as Texture;
				painter.canvasChannelDefinitions[painter.curChannelIndex] = curChannel;

//				if (MultiChannelSyncGUI != null)
//					MultiChannelSyncGUI.Invoke ();

				GUILayout.EndHorizontal ();
				if (EditorGUI.EndChangeCheck ())
					undo = "Channel Settings";
			#endif

				SectionSeperator ();
			}

			#endregion

			#region Brush

			// Foldout with brush presets
			GUILayout.BeginHorizontal ();
			expandBrush = EditorGUILayout.Foldout (expandBrush, "Brush", headerFoldout);
			GUILayout.Space (5);
			for (int brushI = 0; brushI < GlobalPainting.brushPresets.Count; brushI++)
			{
				if (PresetButton ((brushI+1).ToString (), lastBrushPreset == brushI? selectCol : Color.white, GlobalPainting.DeleteBrushPreset, brushI))
				{
					painter.prevPaintMode = painter.curBrush.mode;
					painter.curBrush = GlobalPainting.brushPresets[brushI];
					painter.UpdateBrushType ();
					lastBrushPreset = brushI;
					undo = "Brush Preset";
				}
			}
			GUILayout.Space (5);
			if (PresetButton ("+", Color.white, null, 0))
			{
				GlobalPainting.brushPresets.Add (painter.curBrush);
				Settings.SaveBrushPresets ();
				undo = "Brush Preset";
			}
			GUILayout.FlexibleSpace ();
			GUILayout.EndHorizontal ();


			if (expandBrush)
			{
				EditorGUILayout.Space ();

				// Brush type selection
				string newUndoRecord = null;
				DrawResponsiveGrid ((int)EditorGUIUtility.currentViewWidth-36, Settings.imgPreviewTexSize, GlobalPainting.brushTextures.Length, (int texInd, Rect rect) => {
					if (GUI.Toggle (rect, painter.curBrush.type == texInd, GUIContent.none, brushSelectorButton))
					{
						if (painter.curBrush.type != texInd)
						{
							painter.curBrush.type = texInd;
							painter.UpdateBrushType ();
							newUndoRecord = "Brush Shape";
							lastBrushPreset = -1;
						}
					}
					RTTextureViz.DrawTexture (GlobalPainting.brushTextures[texInd], rect, 0, 0, 0, 4);
				});
				if (!string.IsNullOrEmpty (newUndoRecord))
					undo = newUndoRecord;

				EditorGUI.BeginChangeCheck ();

				painter.brushRotation = EditorGUILayout.Slider (new GUIContent ("Rotation", "Shortcut: Space + Scroll"), painter.brushRotation, -1f, 1f);

				// Brush settings
				Painting.PaintMode newMode = (Painting.PaintMode)EditorGUILayout.EnumPopup (new GUIContent ("Mode", "Switch to last: Tab; Invert: Shift"), painter.curBrush.mode);
				if (newMode != painter.curBrush.mode)
				{
					painter.prevPaintMode = painter.curBrush.mode;
					painter.curBrush.mode = newMode;
				}
				painter.curBrush.size = EditorGUILayout.Slider (new GUIContent ("Size", "Shortcut: Control + Scroll"), painter.curBrush.size, 0.005f, 1f);
				if (painter.brushFunc > 0)
				{
					painter.curBrush.falloff = EditorGUILayout.Slider ("Falloff", painter.curBrush.falloff, 0f, 1f);
					painter.curBrush.hardness = EditorGUILayout.Slider ("Hardness", painter.curBrush.hardness, 1f, 4f);
				}
				painter.curBrush.intensity = EditorGUILayout.Slider (new GUIContent ("Intensity", "Shortcut: Shift + Scroll"), painter.curBrush.intensity, 0.005f, 1);

				// Extra brush values
				if (painter.curBrush.mode == Painting.PaintMode.Smoothen || painter.curBrush.mode == Painting.PaintMode.Contrast)
					painter.smoothenBias = EditorGUILayout.Slider (new GUIContent ("Bias", "Shortcut: Control + Shift + Scroll"), painter.smoothenBias, 1, 4);
				if (painter.curBrush.mode == Painting.PaintMode.Replace || painter.curBrush.mode == Painting.PaintMode.Lerp)
					painter.targetValue = EditorGUILayout.Slider (new GUIContent ("Target", "Shortcut: Control + Shift + Scroll; Pick Value: Control + LeftClick"), painter.targetValue, 0f, 1f);

				SubSectionSeperator ();

				//painter.clampResultStroke = EditorGUILayout.Toggle ("Clamp Stroke", painter.clampResultStroke);
				painter.clampResult01 = EditorGUILayout.Toggle ("Clamp 0-1", painter.clampResult01);

				if (painter.canvasFormat == Painting.Format.Multi)
				{
					painter.normalizeMultiChannels = EditorGUILayout.Toggle ("Normalize Channels", painter.normalizeMultiChannels);
					if (painter.normalizeMultiChannels && !painter.supportsSeperatePass)
						EditorGUILayout.HelpBox ("Currently selected mode does not work well with channel normalization - please select a different like Lerp or Add/Substract!", MessageType.Warning);
				}

				if (EditorGUI.EndChangeCheck ())
				{
					lastBrushPreset = -1;
					undo = "Brush Settings";
				}
			}

			#endregion

			SectionSeperator ();

			#region Modifications

			if (painter.canvasFormat != Painting.Format.Multi)
			{
				// Modification foldout
				GUILayout.BeginHorizontal ();
				expandMods = EditorGUILayout.Foldout (expandMods, "Modifications", headerFoldout);
				GUILayout.Space (40);
				if (painter.applyingOngoingMods && warningIcon != null)
					GUILayout.Label (new GUIContent (warningIcon, "Modifications are being generated"), GUILayout.ExpandWidth (false));
				GUILayout.FlexibleSpace ();
				GUILayout.EndHorizontal ();

				if (expandMods)
				{
					EditorGUI.BeginChangeCheck ();
					painter.mods.contrast = EditorGUILayout.Slider ("Contrast", painter.mods.contrast, 0, 2);
					painter.mods.brightness = EditorGUILayout.Slider ("Brightness", painter.mods.brightness, -1, 1);
					if (painter.canvasFormat == Painting.Format.Color)
					{
						painter.mods.tintColor = EditorGUILayout.ColorField ("Tint", painter.mods.tintColor);
						painter.mods.advancedChannelMods = EditorGUILayout.ToggleLeft ("Enable Advanced Channel Mods", painter.mods.advancedChannelMods);
						if (painter.mods.advancedChannelMods)
							ShowChannelMods (ref painter.mods.chR, ref painter.mods.chG, ref painter.mods.chB, ref painter.mods.chA);
					}
					else
						painter.mods.advancedChannelMods = false;
					if (EditorGUI.EndChangeCheck ())
					{
						painter.UpdateModifications ();
						undo = "Modifications";
					}

					GUILayout.BeginHorizontal ();
					if (GUILayout.Button ("Reset Modifications"))
					{
						painter.mods = new Painting.Modifications (true);
						painter.UpdateModifications ();
						undo = "Reset";
					}
					if (GUILayout.Button ("Apply Modifications"))
						painter.ApplyModifications ();
					GUILayout.EndHorizontal ();

					SubSectionSeperator ();

					// Fill
					if (painter.canvasFormat == Painting.Format.Color)
					{
						GUILayout.BeginHorizontal ();
						fillColor = EditorGUILayout.ColorField ("Color", fillColor);
						if (GUILayout.Button ("Fill"))
							painter.Fill (fillColor);
						GUILayout.EndHorizontal ();
					}
					else
					{
						GUILayout.BeginHorizontal ();
						fillValue = EditorGUILayout.Slider ("Height", fillValue, 0f, 1f);
						if (GUILayout.Button ("Set"))
							painter.Fill (new Color (fillValue, fillValue, fillValue, fillValue));
						GUILayout.EndHorizontal ();
					}
				}

				SectionSeperator ();
			}

			#endregion

			#region Resize

			if (painter.hasCanvas)
			{
				//GUILayout.Label ("Resize (" + painter.sizeX + ", " + painter.sizeY + ")", EditorStyles.boldLabel);
				expandResize = EditorGUILayout.Foldout (expandResize, "Resize (" + painter.sizeX + ", " + painter.sizeY + ")", headerFoldout);

				if (expandResize)
				{
					EditorGUI.BeginChangeCheck ();
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("New X/Y");
					resX = EditorGUILayout.IntField (resX);
					resY = EditorGUILayout.IntField (resY);
					GUILayout.EndHorizontal ();
					if (EditorGUI.EndChangeCheck ())
						undo = "Resize Settings";

					GUILayout.BeginHorizontal ();
					if (GUILayout.Button ("Resize"))
						painter.Resize (resX, resY);
					if (GUILayout.Button ("Expand"))
						painter.Expand (resX, resY);
					GUILayout.EndHorizontal ();
				}
			}

			#endregion

			SectionSeperator ();

			#region Debug
			
			if (Settings.enableDebug)
			{
				expandDebug = EditorGUILayout.Foldout (expandDebug, "Debug", EditorStyles.boldLabel);

				painter.forceSeperatePaintPass = GUILayout.Toggle (painter.forceSeperatePaintPass, "Force Seperate Paint Pass");

				GUILayout.Label ("Seperate paint pass is " + (painter.seperatePaintPass? "enabled" : "disabled") + "!");
				if (painter.needsSeperatePass && !painter.supportsSeperatePass)
					GUILayout.Label ("SPP is not supported by the current mode!");

				GUILayout.BeginHorizontal ();

				testIterations = System.Math.Min (10000, System.Math.Max (1, EditorGUILayout.IntField ("Iterations", testIterations)));

				if (GUILayout.Button ("Test"))
				{
					System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch ();
					watch.Start ();

					for (int i = 0; i < testIterations; i++)
						painter.Paint (new Vector2 (0.5f, 0.5f));

					watch.Stop ();

					Debug.Log ("Test time with " + testIterations + " iterations: " + watch.ElapsedMilliseconds);
				}

				GUILayout.EndHorizontal ();

				GUILayout.Label ("Temp Pass Read:", EditorStyles.boldLabel);

				float textureSize = Mathf.Max (Mathf.Min (Mathf.Min (painter.sizeX, EditorGUIUtility.currentViewWidth), Settings.maxCanvasPreviewSize) - 46, 256);
				Rect textureRect = GUILayoutUtility.GetRect (textureSize, textureSize * painter.sizeY/painter.sizeX, GUILayout.ExpandWidth (false));
				if (painter.TempPassRead != null)
					DrawCanvasPreview (textureRect, painter.TempPassRead, workChannels);

				SectionSeperator ();
			}
			else
				painter.forceSeperatePaintPass = false;

			#endregion

			return repaint;
		}

		#region Shortcuts

		public bool HandleShortcuts (out string undo) 
		{
			bool blockPainting = false;
			undo = null;


			// SHIFT MODIFIER

			// Invert Brush mode while holding shift
			painter.invertPainting = Event.current.modifiers == EventModifiers.Shift;

			// Scale brush intensity by holding shift while scrolling
			if (Event.current.modifiers == EventModifiers.Shift && Event.current.type == EventType.ScrollWheel)
			{
				Event.current.Use ();
				painter.curBrush.intensity = Mathf.Clamp (painter.curBrush.intensity * (1-(0.05f*Event.current.delta.y)), 0.005f, 1f);
				undo = "Brush Intensity";
			}


			// CONTROL MODIFIER

			// Resize brush size by holding control while scrolling
			if (Event.current.modifiers == EventModifiers.Control && Event.current.type == EventType.ScrollWheel)
			{
				Event.current.Use ();
				painter.curBrush.size = Mathf.Clamp (painter.curBrush.size * (1-(0.05f*Event.current.delta.y)), 0.005f, 1f);
				undo = "Brush Size";
			}

			// Set current color to white and intensity to the height under the mouse when leftclicking
			if (Event.current.modifiers == EventModifiers.Control && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && Event.current.button == 0)
			{
				blockPainting = true;
				Event.current.Use ();
				if (new Rect (0,0,1,1).Contains (painter.brushPos) && (painter.curBrush.mode == Painting.PaintMode.Lerp || painter.curBrush.mode == Painting.PaintMode.Replace))
				{
					Color sample = painter.Sample (painter.brushPos);
					if (painter.canvasFormat == Painting.Format.Color)
					{
						painter.targetValue = 1;
						painter.colorIntensity = 1;
						painter.curColor = sample;
					}
					else
					{
						painter.targetValue = Mathf.Max (sample.r, sample.g, sample.b, sample.a);
						painter.colorIntensity = 1;
						painter.curColor = Color.white;
					}
					undo = "Target Value";
				}
			}


			// Detect space key press for usage in non-key events (scroll)
			if (Event.current.isKey)
			{
				if (Event.current.type != EventType.KeyUp && Event.current.keyCode == KeyCode.Space)
					pressedSpace = true;
				else if (Event.current.type != EventType.KeyDown)
					pressedSpace = false;
			}

			// Adjust brush rotation while holding space and scrolling
			if (Event.current.type == EventType.ScrollWheel && pressedSpace)
			{
				Event.current.Use ();
				painter.brushRotation = painter.brushRotation - 0.05f*(int)(Event.current.delta.y/3);
				if (painter.brushRotation > 1f || painter.brushRotation < -1f)
					painter.brushRotation = Mathf.Clamp (-painter.brushRotation, -1, 1);
				undo = "Brush Rotation";
			}

			// Adjust brush target/smoothen bias by holding control+shift while scrolling
			if (Event.current.modifiers == (EventModifiers.Control | EventModifiers.Shift) && Event.current.type == EventType.ScrollWheel)
			{
				Event.current.Use ();
				if (painter.curBrush.mode == Painting.PaintMode.Lerp || painter.curBrush.mode == Painting.PaintMode.Replace)
					painter.targetValue = Mathf.Clamp (painter.targetValue - 0.05f*(int)(Event.current.delta.y/3), 0f, 1f);
				else if (painter.curBrush.mode == Painting.PaintMode.Smoothen || painter.curBrush.mode == Painting.PaintMode.Contrast)
					painter.smoothenBias = Mathf.Clamp (painter.smoothenBias - 0.2f*(int)(Event.current.delta.y/3), 1f, 4f);
				undo = "Brush Value";
			}


			// TAB MODIFIER

			// Switch to last brush mode used when hitting tab
			if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Tab)
			{
				Event.current.Use ();
				GUIUtility.hotControl = controlID;

				Painting.PaintMode curMode = painter.curBrush.mode;
				if ((int)painter.prevPaintMode >= 0)
					painter.curBrush.mode = painter.prevPaintMode;
				painter.prevPaintMode = curMode;

				undo = "Brush Mode";
			}
			else if (GUIUtility.hotControl == controlID)
				GUIUtility.hotControl = 0;
			


			return blockPainting;
		}

		#endregion

		#region GUI Functions

		private void DrawCanvasPreview (Rect textureRect, Texture texture, Painting.Channels channels) 
		{
			switch ((int)channels)
			{
			case 0:
				RTTextureViz.DrawTexture (texture, textureRect, 1, 2, 3, 4);
				break;
			case 1:
				RTTextureViz.DrawTexture (texture, textureRect, 1, 2, 3, 5);
				break;
			case 2:
				RTTextureViz.DrawTexture (texture, textureRect, 1, 1, 1, 5);
				break;
			case 3:
				RTTextureViz.DrawTexture (texture, textureRect, 2, 2, 2, 5);
				break;
			case 4:
				RTTextureViz.DrawTexture (texture, textureRect, 3, 3, 3, 5);
				break;
			case 5:
				RTTextureViz.DrawTexture (texture, textureRect, 4, 4, 4, 5);
				break;
			case 6:
				RTTextureViz.DrawTexture (texture, textureRect, 1);
				break;
			default:
				RTTextureViz.DrawTexture (texture, textureRect);
				break;
			}
		}

		private void setupBrushVizMat (Rect displayRect) 
		{
			if (brushGUIMaterial == null)
				brushGUIMaterial = new Material (Shader.Find ("Hidden/BrushGUIViz"));
			if (GlobalPainting.brushTextures == null)
				GlobalPainting.ReloadBrushTextures ();
			if (GlobalPainting.brushTextures == null || GlobalPainting.brushTextures.Length <= 0)
				return;

			// General
			brushGUIMaterial.SetInt ("sizeX", (int)displayRect.width);
			brushGUIMaterial.SetInt ("sizeY", (int)displayRect.height);

			painter.curBrush.type = Mathf.Clamp (painter.curBrush.type, 0, GlobalPainting.brushTextures.Length-1);
			Texture2D brushTex = GlobalPainting.brushTextures[painter.curBrush.type];
			brushGUIMaterial.SetInt ("_brushType", painter.brushFunc);
			brushGUIMaterial.SetTexture ("_brushTex", brushTex);

			// Set Brush Parameters
			brushGUIMaterial.SetFloat ("_intensity", painter.curBrush.intensity);
			brushGUIMaterial.SetFloat ("_size", painter.curBrush.size);
			brushGUIMaterial.SetFloat ("_falloff", painter.curBrush.falloff);
			brushGUIMaterial.SetFloat ("_hardness", painter.curBrush.hardness);
			brushGUIMaterial.SetFloat ("_targetValue", painter.targetValue);

			// Apply brush rotation matrix
			Matrix4x4 rotMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, painter.brushRotation * 180), Vector3.one);
			brushGUIMaterial.SetMatrix("_rotationMatrix", rotMatrix);

			brushGUIMaterial.SetColor ("_Color", painter.invertPainting? GlobalPainting.invertedBrushColor : GlobalPainting.normalBrushColor);
		}

		private void ShowUndoButtons () 
		{
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("< " + painter.getNextUndoName, GUI.skin.button, GUILayout.MinWidth (EditorGUIUtility.currentViewWidth/2-25)))
			{
				painter.PerformUndo ();
				GUI.changed = false;
			}
			if (GUILayout.Button ("" + painter.getNextRedoName + " >", GUI.skin.button, GUILayout.MinWidth (EditorGUIUtility.currentViewWidth/2-25)))
			{
				painter.PerformRedo ();
				GUI.changed = false;
			}
			GUILayout.EndHorizontal ();
		}

		private static void SubSectionSeperator () 
		{
			//GUIUtility.Seperator ();
			EditorGUILayout.Space ();
		}

		private static void SectionSeperator () 
		{
			AdditionalGUIUtility.Seperator ();
			//EditorGUILayout.Space ();
		}

		private static void ShowChannelMods (ref Painting.ChannelMod R, ref Painting.ChannelMod G, ref Painting.ChannelMod B, ref Painting.ChannelMod A)
		{

			GUILayout.BeginHorizontal ();

			GUILayout.BeginVertical (GUILayout.MaxWidth (80));
			GUILayout.Label ("Shuffle");
			R.shuffle = (int)(Painting.ChannelValue)ShortEnumPopup ("R ->", (Painting.ChannelValue)R.shuffle);
			G.shuffle = (int)(Painting.ChannelValue)ShortEnumPopup ("G ->", (Painting.ChannelValue)G.shuffle);
			B.shuffle = (int)(Painting.ChannelValue)ShortEnumPopup ("B ->", (Painting.ChannelValue)B.shuffle);
			A.shuffle = (int)(Painting.ChannelValue)ShortEnumPopup ("A ->", (Painting.ChannelValue)A.shuffle);
			GUILayout.EndVertical ();
			EditorGUILayout.Space ();

			GUILayout.BeginVertical (GUILayout.ExpandWidth (true));
			GUILayout.Label ("Offset");
			R.offset = ShortSlider (R.offset, -1, 1);
			G.offset = ShortSlider (G.offset, -1, 1);
			B.offset = ShortSlider (B.offset, -1, 1);
			A.offset = ShortSlider (A.offset, -1, 1);
			GUILayout.EndVertical ();
			EditorGUILayout.Space ();

			GUILayout.BeginVertical (GUILayout.ExpandWidth (true));
			GUILayout.Label ("Scale");
			R.scale = ShortSlider (R.scale, 0, 2);
			G.scale = ShortSlider (G.scale, 0, 2);
			B.scale = ShortSlider (B.scale, 0, 2);
			A.scale = ShortSlider (A.scale, 0, 2);
			GUILayout.EndVertical ();
			EditorGUILayout.Space ();

			GUILayout.BeginVertical (GUILayout.Width (40));
			GUILayout.Label ("Invert");
			R.invert = EditorGUILayout.Toggle (R.invert);
			G.invert = EditorGUILayout.Toggle (G.invert);
			B.invert = EditorGUILayout.Toggle (B.invert);
			A.invert = EditorGUILayout.Toggle (A.invert);
			GUILayout.EndVertical ();

			GUILayout.EndHorizontal ();
		}

		private static Enum ShortEnumPopup (string label, Enum selected) 
		{
			GUILayout.BeginHorizontal ();
			GUILayout.Label (label, GUILayout.ExpandWidth (true));
			selected = EditorGUILayout.EnumPopup (selected, GUILayout.ExpandWidth (false));
			GUILayout.EndHorizontal ();
			return selected;
		}

		private static float ShortSlider (float value, float min, float max) 
		{
			GUILayout.BeginHorizontal ();
			value = GUILayout.HorizontalSlider (value, min, max, new GUILayoutOption [] { GUILayout.MinWidth (40), GUILayout.MaxHeight (16) } );
			value = Mathf.Clamp (EditorGUILayout.FloatField (value, GUILayout.MaxWidth (50)), min, max);
			GUILayout.EndHorizontal ();
			return value;
		}

		private static bool PresetButton (string label, Color color, GenericMenu.MenuFunction2 deletePreset, int index)
		{
			Rect rect = GUILayoutUtility.GetRect (new GUIContent (label), colorButton);//, new GUILayoutOption[] { GUILayout.Width (colorButton.fixedWidth), GUILayout.Height (colorButton.fixedHeight) });
			if (deletePreset != null && Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains (Event.current.mousePosition))
			{
				GenericMenu editMenu = new GenericMenu ();
				editMenu.AddItem (new GUIContent ("Delete"), false, deletePreset, index as object);
				editMenu.DropDown (rect);
				Event.current.Use ();
			}
			GUI.color = new Color (color.r, color.g, color.b, 1);
			bool clicked = GUI.Button (rect, label, colorButton);
			GUI.color = Color.white;
			return clicked;
		}

		private static Rect DrawResponsiveGrid (int gridSize, int cellSize, int cellCount, Action<int, Rect> cellFunc) 
		{
			if (cellFunc == null || gridSize <= 0 || cellSize <= 0 || cellCount <= 0)
				return new Rect ();
			int cellsPerRow = Mathf.FloorToInt (gridSize/cellSize);
			int rows = Mathf.CeilToInt ((float)cellCount/cellsPerRow);
			int cellInd = 0;
			Rect completeRect = new Rect ();
			for (int row = 0; row < rows; row++)
			{
				GUILayout.BeginHorizontal ();
				for (int col = 0; col < cellsPerRow && cellInd < cellCount; col++)
				{
					Rect cellRect = GUILayoutUtility.GetRect (cellSize, cellSize);
					if (cellInd == 0) completeRect = cellRect;
					cellFunc.Invoke (cellInd, cellRect);
					cellInd++;
				}
				GUILayout.FlexibleSpace ();
				GUILayout.EndHorizontal ();
			}
			completeRect.size = new Vector2 (cellsPerRow*cellSize, rows*cellSize);
			return completeRect;
		}

		#endregion
	}
}