using UnityEngine;
using UnityEditor;
using System.IO;

namespace TerrainComposer2.NodePainter
{
	public class SettingsWindow : EditorWindow 
	{
		Vector2 scrollPos = Vector2.zero;

		#region GUI Content

		private static GUIContent ContentPaintInterval = new GUIContent ("Paint Interval", "Target interval to paint with. Set 0 to use every frame available. A variable timeStep corrcts irregular intervals.");
		private static GUIContent ContentGenerationInterval = new GUIContent ("Generation Interval", "Interval at which TC2 generates when enabled.");
		private static GUIContent ContentAutoGenerate = new GUIContent ("Auto Generate", "Issue TC2 update in a regular specified interval.");
//		private static GUIContent ContentPartialGeneration = new GUIContent ("Enable Partial Generation", "Using partial generation will only re-generate the terrain under the brush while painting and generally will result in a great speed up.");
		private static GUIContent ContentContinuousPainting = new GUIContent ("Continuous Painting", "Also paint when not moving the mouse.");

		private static GUIContent ContentEnableUndo = new GUIContent ("Enable Undo", "Enable Undo system. Disable if you got memory problems or get hickups after painting.");
		private static GUIContent ContentUndoStackSize = new GUIContent ("Undo Stack size", "Maximum Undo/Redo records remembered. Higher numbers will have a severe impact on RAM used! When exceding this limit, records will be garbage collected!");

		private static GUIContent ContentBrushPreviewSize = new GUIContent ("Brush Preview Size", "Brush Preview Size");
		private static GUIContent ContentMaxCanvasSize = new GUIContent ("Max Canvas Size", "Max Canvas Size");
		//		private static GUIContent ContentRelativeCanvasBorder = new GUIContent ("Relative Canvas Border", "Border around canvas relative to the brush size in order to paint the edges easier.");
		private static GUIContent ContentFixedCanvasBorder = new GUIContent ("Fixed Canvas Border", " Fixed border around the canvas in order to paint the edges easier.");

		private static GUIContent ContentCanvasOverlayStrength = new GUIContent ("Terrain Canvas Opacity", "Opacity of the canvas projection on the terrain.");

		private static GUIContent ContentResourceFolder = new GUIContent ("Resource Folder", "Folder containing all resources used for visualization.");
		private static GUIContent ContentBrushStampsFolder = new GUIContent ("Brush Stamp Folder", "Folder containing the Brush Stamps. Can be moved to an external path if desired");
		private static GUIContent ContentCacheFolder = new GUIContent ("Cache Folder", "Folder containing the cached canvases to load up next session. Can be moved to an external path if desired");

//		private static GUIContent ContentEnableDebug = new GUIContent ("Enable Debug", "Advanced Settings and Debugging (in TerrainPainter component)");
//		private static GUIContent ContentUniformBranching = new GUIContent ("Enable Uniform Branching On GPU", "Using Uniform branching on the GPU instead of calculating all branches and discarding other pathes may give performance improvements on certain GPUs");

		#endregion

		[MenuItem ("Window/TC2 Node Painter Settings")]
		public static void Open () 
		{
			SettingsWindow window = GetWindow<SettingsWindow> ("NP Settings");
			window.Focus ();
			window.minSize = new Vector2 (200, 60);
		}

		public void OnGUI ()
		{
			scrollPos = EditorGUILayout.BeginScrollView (scrollPos);

			#region Settings GUI

			GUILayout.Label ("Paint Behaviour", EditorStyles.boldLabel);

			Settings.targetPaintInterval = EditorGUILayout.Slider (ContentPaintInterval, Settings.targetPaintInterval, 0, 1);
			Settings.targetGenerationInterval = EditorGUILayout.Slider (ContentGenerationInterval, Settings.targetGenerationInterval, 0, 1);
			Settings.autoGenerate = EditorGUILayout.Toggle (ContentAutoGenerate, Settings.autoGenerate);
//			Settings.enablePartialGeneration = EditorGUILayout.Toggle (ContentPartialGeneration, Settings.enablePartialGeneration);
			Settings.continuousPaint = EditorGUILayout.Toggle (ContentContinuousPainting, Settings.continuousPaint);


			GUILayout.Label ("Undo", EditorStyles.boldLabel);

			Settings.enableUndo = EditorGUILayout.Toggle (ContentEnableUndo, Settings.enableUndo);
			Settings.undoStackSize = Mathf.Clamp (EditorGUILayout.IntField (ContentUndoStackSize, Settings.undoStackSize), 5, 50);


			GUILayout.Label ("GUI", EditorStyles.boldLabel);

			Settings.imgPreviewTexSize = Mathf.Clamp (EditorGUILayout.IntField (ContentBrushPreviewSize, Settings.imgPreviewTexSize), 16, 128);
			Settings.maxCanvasPreviewSize = Mathf.Clamp (EditorGUILayout.IntField (ContentMaxCanvasSize, Settings.maxCanvasPreviewSize), 128, 2048);

//			Settings.relativeCanvasPaintBorder = EditorGUILayout.Slider (ContentRelativeCanvasBorder, Settings.relativeCanvasPaintBorder, 0, 1);
			Settings.fixedCanvasPaintBorder = EditorGUILayout.IntSlider (ContentFixedCanvasBorder, Settings.fixedCanvasPaintBorder, 0, 100);

			EditorGUI.BeginChangeCheck ();
			Settings.terrainCanvasOverlayStrength = EditorGUILayout.Slider (ContentCanvasOverlayStrength, Settings.terrainCanvasOverlayStrength, 0, 1);
			if (EditorGUI.EndChangeCheck ())
			{
				GlobalPainting.UpdateCanvasProjection ();
				Repaint ();
			}

			GUILayout.Label ("Folder Locations", EditorStyles.boldLabel);

			GUILayout.BeginHorizontal ();
			Settings.paintingResourcesFolder = EditorGUILayout.TextField (ContentResourceFolder, Settings.paintingResourcesFolder);
			if (GUILayout.Button ("Select", GUILayout.ExpandWidth (false)))
			{
				string newPath = EditorUtility.OpenFolderPanel ("Select Folder containing all resources.", Settings.paintingResourcesFolder, "");
				if (!string.IsNullOrEmpty (newPath))
				{
					if (!newPath.StartsWith (Application.dataPath) || !Directory.Exists (newPath))
						ShowNotification (new GUIContent ("Selected resource folder does not exist or is not located in the Asset folder! Please re-select!"));
					else 
						Settings.paintingResourcesFolder = newPath;
				}
			}
			GUILayout.EndHorizontal ();

			EditorGUI.BeginChangeCheck ();
			GUILayout.BeginHorizontal ();
			Settings.brushStampsFolder = EditorGUILayout.TextField (ContentBrushStampsFolder, Settings.brushStampsFolder);
			if (GUILayout.Button ("Select", GUILayout.ExpandWidth (false)))
			{
				string newPath = EditorUtility.OpenFolderPanel ("Select Folder containing all brush stamps.", Settings.brushStampsFolder, "");
				if (!string.IsNullOrEmpty (newPath))
				{
					if (!Directory.Exists (newPath))
						ShowNotification (new GUIContent ("Selected brush stamps folder does not exist! Please re-select!"));
					else 
						Settings.brushStampsFolder = newPath;
				}
			}
			GUILayout.EndHorizontal ();
			if (EditorGUI.EndChangeCheck ())
				GlobalPainting.ReloadBrushTextures ();

			GUILayout.BeginHorizontal ();
			Settings.lastSessionCacheFolder = EditorGUILayout.TextField (ContentCacheFolder, Settings.lastSessionCacheFolder);
			if (GUILayout.Button ("Select", GUILayout.ExpandWidth (false)))
			{
				string newPath = EditorUtility.OpenFolderPanel ("Select Folder to store the last session cache in.", Settings.lastSessionCacheFolder, "");
				if (!string.IsNullOrEmpty (newPath))
				{
					if (!Directory.Exists (newPath))
						ShowNotification (new GUIContent ("Selected cache folder does not exist! Please re-select!"));
					else 
						Settings.lastSessionCacheFolder = newPath;
				}
			}
			GUILayout.EndHorizontal ();


//			GUILayout.Label ("Internal Settings", EditorStyles.boldLabel);
//
//			Settings.enableDebug = EditorGUILayout.ToggleLeft (ContentEnableDebug, Settings.enableDebug);
//			Settings.enableGPUUniformBranching = EditorGUILayout.ToggleLeft (ContentUniformBranching, Settings.enableGPUUniformBranching);


			#endregion

			EditorGUILayout.EndScrollView ();

			EditorGUILayout.Space ();

			if (GUILayout.Button ("Reset All to Default Values"))
				Settings.ResetAll ();
		}
	}
}

