using UnityEngine;
using System.IO;
using System.Collections.Generic;

using TerrainComposer2.NodePainter.Utilities;

#if UNITY_EDITOR
using SettingsPrefs = UnityEditor.EditorPrefs;
using BoolPrefs = UnityEditor.EditorPrefs;
#else
using SettingsPrefs = UnityEngine.PlayerPrefs;
using BoolPrefs = TerrainComposer2.NodePainter.Settings;
#endif

namespace TerrainComposer2.NodePainter
{
	public static class Settings 
	{
		#region Settings

		private static float _targetPaintInterval = 0.01f;
		private static string _targetPaintIntervalKey = "TP_targetPaintInterval";
		public static float targetPaintInterval { get { return _targetPaintInterval; } set { if (_targetPaintInterval != value) { _targetPaintInterval = value; SettingsPrefs.SetFloat (_targetPaintIntervalKey, _targetPaintInterval); } } }

		private static float _targetGenerationInterval = 0.1f;
		private static string _targetGenerationIntervalKey = "TP_targetGenerationInterval";
		public static float targetGenerationInterval { get { return _targetGenerationInterval; } set { if (_targetGenerationInterval != value) { _targetGenerationInterval = value; SettingsPrefs.SetFloat (_targetGenerationIntervalKey, _targetGenerationInterval); } } }


		private static bool _autoGenerate = true;
		private static string _autoGenerateKey = "TP_autoGenerate";
		public static bool autoGenerate { get { return _autoGenerate; } set { if (_autoGenerate != value) { _autoGenerate = value; BoolPrefs.SetBool (_autoGenerateKey, _autoGenerate); } } }

		private static bool _continuousPaint = true;
		private static string _continuousPaintKey = "TP_continuousPaint";
		public static bool continuousPaint { get { return _continuousPaint; } set { if (_continuousPaint != value) { _continuousPaint = value; BoolPrefs.SetBool (_continuousPaintKey, _continuousPaint); } } }


		private static bool _enableUndo = true;
		private static string _enableUndoKey = "TP_enableUndo";
		public static bool enableUndo { get { return _enableUndo; } set { if (_enableUndo != value) { _enableUndo = value; BoolPrefs.SetBool (_enableUndoKey, _enableUndo); } } }

		private static int _undoStackSize = 30;
		private static string _undoStackSizeKey = "TP_undoStackSize";
		public static int undoStackSize { get { return _undoStackSize; } set { if (_undoStackSize != value) { _undoStackSize = value; SettingsPrefs.SetInt (_undoStackSizeKey, _undoStackSize); } } }


		private static int _imgPreviewTexSize = 48;
		private static string _imgPreviewTexSizeKey = "TP_imgPreviewTexSize";
		public static int imgPreviewTexSize { get { return _imgPreviewTexSize; } set { if (_imgPreviewTexSize != value) { _imgPreviewTexSize = value; SettingsPrefs.SetInt (_imgPreviewTexSizeKey, _imgPreviewTexSize); } } }

		private static int _maxCanvasPreviewSize = 512;
		private static string _maxCanvasPreviewSizeKey = "TP_maxCanvasPreviewSize";
		public static int maxCanvasPreviewSize { get { return _maxCanvasPreviewSize; } set { if (_maxCanvasPreviewSize != value) { _maxCanvasPreviewSize = value; SettingsPrefs.SetInt (_maxCanvasPreviewSizeKey, _maxCanvasPreviewSize); } } }

		private static float _relativeCanvasPaintBorder = 0f;
		private static string _relativeCanvasPaintBorderKey = "TP_relativeCanvasPaintBorder";
		public static float relativeCanvasPaintBorder { get { return _relativeCanvasPaintBorder; } set { if (_relativeCanvasPaintBorder != value) { _relativeCanvasPaintBorder = value; SettingsPrefs.SetFloat (_relativeCanvasPaintBorderKey, _relativeCanvasPaintBorder); } } }
		
		private static int _fixedCanvasPaintBorder = 0;
		private static string _fixedCanvasPaintBorderKey = "TP_fixedCanvasPaintBorder";
		public static int fixedCanvasPaintBorder { get { return _fixedCanvasPaintBorder; } set { if (_fixedCanvasPaintBorder != value) { _fixedCanvasPaintBorder = value; SettingsPrefs.SetInt (_fixedCanvasPaintBorderKey, _fixedCanvasPaintBorder); } } }


		private static float _terrainCanvasOverlayStrength = 0.6f;
		private static string _terrainCanvasOverlayStrengthKey = "TP_terrainCanvasOverlayStrength";
		public static float terrainCanvasOverlayStrength { get { return _terrainCanvasOverlayStrength; } set { if (_terrainCanvasOverlayStrength != value) { _terrainCanvasOverlayStrength = value; SettingsPrefs.SetFloat (_terrainCanvasOverlayStrengthKey, _terrainCanvasOverlayStrength); } } }


		private static string _paintingResourcesFolder = "Assets/Node Painter/EditorResources";
		public static string paintingResourcesFolder { get { return _paintingResourcesFolder; } set { if (_paintingResourcesFolder != value) { _paintingResourcesFolder = value.Replace (Application.dataPath, "Assets").Replace (@"\", "/"); SaveProjectPaths (); } } }

		private static string _brushStampsFolder = "Assets/Node Painter/EditorResources/BrushStamps";
		public static string brushStampsFolder { get { return _brushStampsFolder; } set { if (_brushStampsFolder != value) { _brushStampsFolder = value.Replace (Application.dataPath, "Assets").Replace (@"\", "/"); SaveProjectPaths (); } } }

		private static string _lastSessionCacheFolder = "Assets/Node Painter/Cache";
		public static string lastSessionCacheFolder { get { return _lastSessionCacheFolder; } set { if (_lastSessionCacheFolder != value) { _lastSessionCacheFolder = value.Replace (Application.dataPath, "Assets").Replace (@"\", "/"); SaveProjectPaths (); } } }


		private static bool _enableDebug = false;
		private static string _enableDebugKey = "TP_enableDebug";
		public static bool enableDebug { get { return _enableDebug; } set { if (_enableDebug != value) { _enableDebug = value; BoolPrefs.SetBool (_enableDebugKey, _enableDebug); } } }

		private static bool _enableGPUUniformBranching = true;
		private static string _enableGPUUniformBranchingKey = "TP_enableGPUUniformBranching";
		public static bool enableGPUUniformBranching { get { return _enableGPUUniformBranching; } set { if (_enableGPUUniformBranching != value) { _enableGPUUniformBranching = value; BoolPrefs.SetBool (_enableGPUUniformBranchingKey, _enableGPUUniformBranching); } } }

		private static bool _enablePartialGeneration = true;
		private static string _enablePartialGenerationKey = "TP_enablePartialGeneration";
		public static bool enablePartialGeneration { get { return _enablePartialGeneration; } set { if (_enablePartialGeneration != value) { _enablePartialGeneration = value; BoolPrefs.SetBool (_enablePartialGenerationKey, _enablePartialGeneration); } } }

		#endregion

		#region BoolPrefs

		public static void SetBool(string name, bool booleanValue) 
		{
			PlayerPrefs.SetInt(name, booleanValue ? 1 : 0);
		}

		public static bool GetBool(string name)  
		{
			return PlayerPrefs.GetInt(name) == 1 ? true : false;
		}

		public static bool GetBool(string name, bool defaultValue)
		{
			if(PlayerPrefs.HasKey(name))
				return GetBool(name);
			return defaultValue;
		}


		#endregion

		static Settings () 
		{
			#region Fetch Settings

			if (SettingsPrefs.HasKey (_targetPaintIntervalKey))
				_targetPaintInterval = SettingsPrefs.GetFloat (_targetPaintIntervalKey);
			if (SettingsPrefs.HasKey (_targetGenerationIntervalKey))
				_targetGenerationInterval = SettingsPrefs.GetFloat (_targetGenerationIntervalKey);


			if (SettingsPrefs.HasKey (_autoGenerateKey))
				_autoGenerate = BoolPrefs.GetBool (_autoGenerateKey);
			if (SettingsPrefs.HasKey (_continuousPaintKey))
				_continuousPaint = BoolPrefs.GetBool (_continuousPaintKey);
			

			if (SettingsPrefs.HasKey (_enableUndoKey))
				_enableUndo = BoolPrefs.GetBool (_enableUndoKey);
			if (SettingsPrefs.HasKey (_undoStackSizeKey))
				_undoStackSize = SettingsPrefs.GetInt (_undoStackSizeKey);


			if (SettingsPrefs.HasKey (_imgPreviewTexSizeKey))
				_imgPreviewTexSize = SettingsPrefs.GetInt (_imgPreviewTexSizeKey);
			if (SettingsPrefs.HasKey (_maxCanvasPreviewSizeKey))
				_maxCanvasPreviewSize = SettingsPrefs.GetInt (_maxCanvasPreviewSizeKey);
			if (SettingsPrefs.HasKey (_relativeCanvasPaintBorderKey))
				_relativeCanvasPaintBorder = SettingsPrefs.GetFloat (_relativeCanvasPaintBorderKey);
			if (SettingsPrefs.HasKey (_fixedCanvasPaintBorderKey))
				_fixedCanvasPaintBorder = SettingsPrefs.GetInt (_fixedCanvasPaintBorderKey);


			if (SettingsPrefs.HasKey (_terrainCanvasOverlayStrengthKey))
				_terrainCanvasOverlayStrength = SettingsPrefs.GetFloat (_terrainCanvasOverlayStrengthKey);


			LoadProjectPaths ();


//			if (SettingsPrefs.HasKey (_enableDebugKey))
//				_enableDebug = BoolPrefs.GetBool (_enableDebugKey);

//			if (SettingsPrefs.HasKey (_enableGPUUniformBranchingKey))
//				_enableGPUUniformBranching = BoolPrefs.GetBool (_enableGPUUniformBranchingKey);
			
//			if (SettingsPrefs.HasKey (_enablePartialGenerationKey))
//				_enablePartialGeneration = BoolPrefs.GetBool (_enablePartialGenerationKey);

			#endregion
		}

		public static void ResetAll () 
		{
			#region Reset Settings

			targetPaintInterval = 0.01f;
			targetGenerationInterval = 0.1f;

			autoGenerate = true;
			continuousPaint = true;

			enableUndo = true;
			undoStackSize = 30;

			imgPreviewTexSize = 48;
			maxCanvasPreviewSize = 512;

			relativeCanvasPaintBorder = 0f;
			fixedCanvasPaintBorder = 0;

			terrainCanvasOverlayStrength = 0.6f;

			enableDebug = false;
			enableGPUUniformBranching = true;
			enablePartialGeneration = true;

			paintingResourcesFolder = "Assets/Node Painter/EditorResources";
			brushStampsFolder = "Assets/Node Painter/EditorResources/BrushStamps";
			lastSessionCacheFolder = "Assets/Node Painter/Cache";

			#endregion
		}

		#region Paths & Presets

		private static void SaveProjectPaths () 
		{
			string pathContents = "";
			pathContents += paintingResourcesFolder + ";";
			pathContents += brushStampsFolder + ";";
			pathContents += lastSessionCacheFolder;

			string path = Application.dataPath.Substring (0, Application.dataPath.Length-"Assets".Length) + "ProjectSettings/NodePainterSettings.txt";
			File.WriteAllText (path, pathContents);

			ResourceManager.SetDefaultResourcePath (Settings.paintingResourcesFolder + "/");
		}

		private static void LoadProjectPaths () 
		{
			string path = Application.dataPath.Substring (0, Application.dataPath.Length-"Assets".Length) + "ProjectSettings/NodePainterSettings.txt";
			if (File.Exists (path))
			{
				string pathContents = File.ReadAllText (path);
				if (pathContents != null)
				{
					string[] keys = pathContents.Split (';');
					if (keys.Length == 3)
					{ // Read paths out of file
						paintingResourcesFolder = keys[0].Replace (@"\", "/");
						brushStampsFolder = keys[1].Replace (@"\", "/");
						lastSessionCacheFolder = keys[2].Replace (@"\", "/");
					}
				}
			}
		}


		public static void SaveBrushPresets () 
		{
			List<Painting.Brush> brushes = GlobalPainting.brushPresets;
			string saveData = "";
			for (int i = 0; i < brushes.Count; i++)
			{
				Painting.Brush b = brushes[i];
				saveData += "{" + (int)(b.mode) + ";" + b.type + ";" + b.size + ";" + b.intensity + ";" + b.falloff + ";" + b.hardness + "}";
			}
			SettingsPrefs.SetString ("TP_BrushPresets", saveData);
		}

		public static void LoadBrushPresets () 
		{
			if (!SettingsPrefs.HasKey ("TP_BrushPresets"))
			{
				SaveBrushPresets ();
				return;
			}
			string saveData = SettingsPrefs.GetString ("TP_BrushPresets");
			string[] presets = saveData.Split (new char[] { '{', '}' }, System.StringSplitOptions.RemoveEmptyEntries);
			List<Painting.Brush> brushes = new List<Painting.Brush> (presets.Length);
			for (int i = 0; i < presets.Length; i++)
			{
				string[] brushValueData = presets[i].Split (';');
				if (brushValueData.Length < 4)
					continue;
				float[] values = new float[6] { (float)Painting.BlendMode.Add, 0f, 0.05f, 0.2f, 0f, 1f };
				for (int iV = 0; iV < brushValueData.Length && iV < values.Length; iV++)
				{
					float value = 0;
					if (float.TryParse (brushValueData[iV], out value))
						values[iV] = value;
				}
				brushes.Add (new Painting.Brush { mode=(Painting.PaintMode)values[0], type=(int)values[1], size=values[2], intensity=values[3], falloff=values[4], hardness=values[5] });
			}
			GlobalPainting.brushPresets = brushes;
		}

		public static void SaveColorPresets () 
		{
			List<Color> colors = GlobalPainting.colorPresets;
			string saveData = "";
			for (int i = 0; i < colors.Count; i++)
			{
				Color col = colors[i];
				saveData += "{" + col.r + ";" + col.g + ";" + col.b + ";" + col.a + "}";
			}
			SettingsPrefs.SetString ("TP_ColorPresets", saveData);
		}

		public static void LoadColorPresets () 
		{
			if (!SettingsPrefs.HasKey ("TP_ColorPresets"))
			{
				SaveColorPresets ();
				return;
			}
			string saveData = SettingsPrefs.GetString ("TP_ColorPresets");
			string[] presets = saveData.Split (new char[] { '{', '}' }, System.StringSplitOptions.RemoveEmptyEntries);
			List<Color> colors = new List<Color> (presets.Length);
			for (int i = 0; i < presets.Length; i++)
			{
				string[] colorValueData = presets[i].Split (';');
				if (colorValueData.Length < 4)
					continue;
				float[] values = new float[4];
				for (int iV = 0; iV < 4; iV++)
				{
					float value = 0;
					if (float.TryParse (colorValueData[iV], out value))
						values[iV] = value;
				}
				colors.Add (new Color (values[0], values[1], values[2], values[3]));
			}
			GlobalPainting.colorPresets = colors;
		}

		#endregion
	}
}