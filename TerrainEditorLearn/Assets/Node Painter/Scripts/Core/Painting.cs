using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

using TerrainComposer2.NodePainter.Utilities;

namespace TerrainComposer2.NodePainter
{
	[System.Serializable]
	public class Painting
	{
		#region System

		// Unique ID
		[SerializeField]
		private int uniqueID;
		public int ID { get { return uniqueID; } }

		// Paint Material
		private Material _paintMat;
		private Material PaintMat { get { if (_paintMat == null) _paintMat = new Material (Shader.Find ("Hidden/NodePainter_RTPaint")); return _paintMat; } }

		// Seperate pass
		public bool forceSeperatePaintPass = false;
		public bool supportsSeperatePass { get { return curBrush.mode != PaintMode.Smoothen && curBrush.mode != PaintMode.Contrast && curBrush.mode != PaintMode.Replace; } }
		public bool needsSeperatePass { get { return clampResultStroke || (canvasFormat == Format.Multi && normalizeMultiChannels) || forceSeperatePaintPass; } }
		public bool seperatePaintPass { get { return needsSeperatePass && supportsSeperatePass; } }

		// Visualization
		[SerializeField]
		private CanvasVizState _canvasVizState = CanvasVizState.All;
		public CanvasVizState canvasVizState { get { return _canvasVizState; } set { if (_canvasVizState != value) { _canvasVizState = value; PushContent(); } } }
	#if UNITY_5_4_OR_NEWER
		private Texture2DArray cachedMultiTexVizArray; // cached TextureArray containing all channels to visualize multi-canvases faster
	#endif
		private static string[] channelVizColorsHex = new string[16] { "#ff0000", "#00FF11", "#0011FF", "#eeff00", "#00ffaa", "#ffaa00", "#6f00a6", "#c8ff59", "#3a82a6", "#82a63a", "#00aaff", "#00a600", "#c859ff", "#ff5959", "#3aa682", "#a6823a" };
		[System.NonSerialized] public Color[] channelVizColors;

		// Paint state
		public bool isDrawing { get; private set; }
		public bool isOnCanvas { get; private set; }
		private string drawingSource; // Usually GUI or Terrain
		public Vector2 brushPos { get; private set; }
		private float lastPaintTime;
		private float lastGenerationTime;
		public float canvasRotation;
		[System.NonSerialized] public bool blockPainting;

		// Canvas
		public bool hasCanvas { get { return _canvasTextures != null && _canvasTextures.Count > 0 && !_canvasTextures.Any ((RenderTexture RT) => RT == null); } }
		private string _canvasName;
		public string canvasName { get { return hasCanvas? _canvasName : "None"; } set { if (_canvasName != value) { _canvasName = value; AssignCanvasNames (); } } }
		private Format _canvasFormat;
		public Format canvasFormat { get { return _canvasFormat; } set { _canvasFormat = value; } }
		public int sizeX { get; private set; }
		public int sizeY { get; private set; }

		// Default canvas formats
		public const RenderTextureFormat RTFormat = RenderTextureFormat.ARGBHalf; // RenderTextureFormat.ARGBHalf; // RenderTextureFormat.ARGBFloat
		public const TextureFormat TexFormat = TextureFormat.RGBAHalf; // TextureFormat.RGBAHalf; // TextureFormat.RGBAFloat
		public const RawImportExport.BitDepth bitDepth = RawImportExport.BitDepth.Bit16; // RawImportExport.BitDepth.Bit16; // RawImportExport.BitDepth.Bit32

		// Painting Callbacks
		public delegate void SetCanvasFunc (params RenderTexture[] textures);
		public SetCanvasFunc OnAssignCanvas;
		public System.Action OnPainting;
		public System.Action OnStartPainting;
		public System.Action OnEndPainting;
		public System.Action<float,float> OnScaleCanvas;

		// Temp brush state
		private int brushType = -1;
		public int brushFunc = -1;
		public Painting.PaintMode prevPaintMode = (Painting.PaintMode)(-1);

		// Undo
		private List<UndoRecord> canvasUndoList = new List<UndoRecord> ();
		private List<UndoRecord> canvasRedoList = new List<UndoRecord> ();

		// Cache Information
		public TextAsset cache_Asset = null;
		public bool cache_Canvas = true;
		public string cache_Path;
		public int cache_SizeX, cache_SizeY;
		public Format cache_Format;
		public int cache_ChannelCnt;

		// Constants
		private const int shaderManualTextureCount = 6; // How many textures the shader handles manually before switching to TextureArrays

		#endregion

		#region Internal Texture Definitions

		// Canvas containing intermediate, unsaved modifications used for visualization
		public RenderTexture vizCanvas;

		// Internal canvas (stack) with saved content
		public RenderTexture currentCanvasTex { get { return _canvasTextures != null && curTexIndex >= 0 && _canvasTextures.Count > curTexIndex? _canvasTextures[curTexIndex] : null; } }
		private List<RenderTexture> _canvasTextures = new List<RenderTexture> ();
		private List<RenderTexture> _tempCanvasTextures;

		public List<CanvasChannel> canvasChannelDefinitions = new List<CanvasChannel> ();

		[SerializeField]
		private int _canvasChannelCount = 1;
		public int canvasChannelCount { get { return hasCanvas? (canvasFormat == Format.Multi? _canvasChannelCount : 1) : 0; } set { if (_canvasChannelCount != value) { _canvasChannelCount = value; MatchCanvasChannelCount (canvasChannelCount); } }}
		public int canvasTextureCount { get { return hasCanvas? _canvasTextures.Count : 0; } }

		// Working passes - endlessly switching to provide canvases for many processing passes
		private RenderTexture tempPass1, tempPass2;
		private bool tempSwitch = false;
		private RenderTexture TempPassWrite { get { return tempSwitch? tempPass2 : tempPass1; } }
		public RenderTexture TempPassRead { get { return tempSwitch? tempPass1 : tempPass2; } }

		// Intermediate result used before modifications are applied to canvas when additional steps are required
		private RenderTexture tempResult;
		public RenderTexture CurPaintTexRaw { get; private set; }

		#endregion

		#region User Options

		// Modifications
		public Modifications mods = new Modifications (true);
		public bool applyingOngoingMods { get { return canvasFormat != Format.Multi && (mods.brightness != 0 || mods.contrast != 1 || mods.tintColor != Color.white || mods.advancedChannelMods); } }

		// Channel Selection
		private int _curChannelIndex = 0;
		public int curChannelIndex { get { return _curChannelIndex; } set { if (hasCanvas && _curChannelIndex != value) { _curChannelIndex = canvasFormat == Format.Multi? value : 0; CanvasChange (false); PushContent (); } } }
		public int curTexIndex { get { return Mathf.FloorToInt ((float)curChannelIndex/4); } }

		// Drawing Options
		public Color curColor = Color.white;
		public float colorIntensity = 1;
		public Brush curBrush = new Brush { mode=PaintMode.Add, type=0, size=0.05f, intensity=0.2f };
		public float brushRotation = 0;
		public float smoothenBias = 2;
		public float targetValue = 1;

		[System.NonSerialized]
		public bool invertPainting;

		// Clamp Options
		public bool clampResult01 = true;
		public bool clampResultStroke = false;

		// Misc Options
		public bool normalizeMultiChannels = false;

		// Visualization
		public bool vizCanvasExternally;

		#endregion

		#region Children

		[System.Serializable]
		public struct Brush 
		{
			public PaintMode mode;
			public int type;
			public float size;
			public float intensity;
			public float falloff;
			public float hardness;
		}

		[System.Serializable]
		private class UndoRecord 
		{
			public string name;
			public string message;
			// Canvas Format
			public int sizeX, sizeY;
			public Format format;
			public RenderTextureFormat texFormat;
			// Canvas content
			//public List<CanvasChannel> channels;
			public int channelCount;
			public List<Texture2D> tex;
			public int curIndex = 0, nextIndex = -1;
			// Canvas mods
			public bool writeMods;
			public Modifications mods;
		}

		[System.Serializable]
		public struct Modifications 
		{
			public float brightness;
			public float contrast;
			public Color tintColor;
			public bool advancedChannelMods;
			public ChannelMod chR, chG, chB, chA;

			public Modifications (bool initialize)
			{
				brightness = 0;
				contrast = 1;
				tintColor = Color.white;
				advancedChannelMods = false;
				chR = new ChannelMod (0);
				chG = new ChannelMod (1);
				chB = new ChannelMod (2); 
				chA = new ChannelMod (3);
			}
		}

		[System.Serializable]
		public struct ChannelMod 
		{
			public int channel;
			public float offset, scale;
			public int shuffle;
			public bool invert;

			public ChannelMod (int ch)
			{
				channel = ch;
				offset = 0;
				scale = 1;
				shuffle = ch;
				invert = false;
			}
		}

		[System.Serializable]
		public struct CanvasChannel
		{
			public int channelIndex;
			public Texture displayTexture;

			public CanvasChannel (int channelInd) 
			{
				channelIndex = channelInd;
				displayTexture = null;
			}
			public CanvasChannel (int channelInd, Texture displayTex) 
			{
				channelIndex = channelInd;
				displayTexture = displayTex;
			}
		}

		public enum Format { Color, Value, Multi }
		public enum CanvasVizState { None, Current, All } 

		public enum Channels { RGBA, RGB, R, G, B, A, Grayscale }
		public enum ChannelValue { R, G, B, A, black, white }

		public enum BlendMode { Add, Substract, Multiply, Divide, Lerp, Overlay, Replace, Smoothen, Contrast }
		public enum PaintMode { Add = 0, Lerp = 4, Replace = 6, Smoothen = 7, Contrast = 8 }

		#endregion


		#region General Methods

		public Painting ()
		{
			if (uniqueID == 0) // Creation
				RecreateID ();
		}

		public void ReadColorsIn () 
		{
			channelVizColors = new Color[channelVizColorsHex.Length];
			for (int hCnt = 0; hCnt < channelVizColorsHex.Length; hCnt++)
			{
				Color hexCol;
			#if UNITY_5_2 || UNITY_5_3 || UNITY_5_3_OR_NEWER
				if (ColorUtility.TryParseHtmlString (channelVizColorsHex[hCnt], out hexCol))
			#else
				if (Color.TryParseHexString (channelVizColorsHex[hCnt], out hexCol))
			#endif
					channelVizColors[hCnt] = hexCol;
				else
					Debug.LogError (channelVizColorsHex[hCnt] + " could not be parsed to a color!");
			}
		}

		public void RecreateID () 
		{
			uniqueID = System.Math.Abs (GetHashCode ());
		}

		public void Open () 
		{
			if (canvasUndoList == null || canvasUndoList.Count <= 0)
				RegisterCanvasUndo ("INITIAL");
			if (!hasCanvas && !canPerformUndo)
				LoadLastSession ();
		}

		public void Hide () 
		{
			if (isDrawing)
				EndPainting ();
		}

		public void Close () 
		{
			SaveCurrentSession ();
		}

		#endregion


		#region Internal RenderTexture Handling

		/// <summary>
		/// Validates the current canvas
		/// </summary>
		private bool AssureRTs () 
		{
			if (!hasCanvas)
			{ // No internal canvas -> nothing assigned
				ReleaseRTs (true, true);
				return false;
			}
			if (sizeX <= 0 || sizeY <= 0)
				sizeX = sizeY = 1024;
			// Check Multi format
			if (canvasFormat == Format.Multi && Mathf.CeilToInt ((float)canvasChannelCount/4) != _canvasTextures.Count)
				MatchCanvasChannelCount ();
			curChannelIndex = Mathf.Clamp (curChannelIndex, 0, canvasChannelCount-1);
			// Check for Temp RTs
			if (vizCanvas == null || tempPass1 == null || tempPass2 == null || tempResult == null)
				CreateTempRTs ();
			return true;
		}

		/// <summary>
		/// Recreates all temporal RTs when the internal canvas is set
		/// </summary>
		private void CreateTempRTs () 
		{
			if (!hasCanvas)
				return;

			// Make sure previous ones are released
			ReleaseRTs (true, false);

			// Create RTs
			tempPass1 = new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear);
			tempPass2 = new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear);
			tempResult = new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear);
			vizCanvas = new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear);

			tempPass1.name = canvasName + ":Pass1";
			tempPass2.name = canvasName + ":Pass2";
			tempResult.name = canvasName + ":TempResult";
			vizCanvas.name = canvasName + ":Canvas";

			// Setup working RT mipmapping
			tempPass1.useMipMap = true;
			tempPass1.filterMode = FilterMode.Trilinear;
			tempPass2.useMipMap = true;
			tempPass2.filterMode = FilterMode.Trilinear;
		}

		/// <summary>
		/// Releases the RenderTextures, the main internal one only if desired
		/// </summary>
		private void ReleaseRTs (bool resetToNull, bool internalRT) 
		{
			if (internalRT && _canvasTextures != null)
			{
				for (int RTi = 0; RTi < _canvasTextures.Count; RTi++)
					ReleaseRT (_canvasTextures[RTi]);
				if (resetToNull)
				{
					if (_canvasTextures != null)
						_canvasTextures.Clear ();
					_canvasTextures = null;
				}
			}

			ReleaseRT (tempPass1);
			ReleaseRT (tempPass2);
			ReleaseRT (tempResult);
			ReleaseRT (vizCanvas);

			if (_tempCanvasTextures != null)
			{
				for (int RTi = 0; RTi < _tempCanvasTextures.Count; RTi++)
					ReleaseRT (_tempCanvasTextures[RTi]);
			}
			CanvasChange (true);

			if (resetToNull)
				vizCanvas = tempPass1 = tempPass2 = tempResult = null;
		}

		/// <summary>
		/// Releases the RenderTexture
		/// </summary>
		private void ReleaseRT (RenderTexture RT) 
		{
			if (RT != null) 
			{
				#if UNITY_EDITOR
				if (UnityEditor.AssetDatabase.Contains (RT))
					return;
				#endif
				if (RenderTexture.active == RT)
					RenderTexture.active = null;
				RT.Release (); 
				GlobalPainting.DestroyObj (RT); 
			}
		}

		private void CanvasChange (bool updatedCanvasTextures = false)
		{
		#if UNITY_5_4_OR_NEWER
			cachedMultiTexVizArray = null;
		#endif
			if (updatedCanvasTextures)
				_tempCanvasTextures = null;
		}

		/// <summary>
		/// Assignes the current, unmodified canvas
		/// </summary>
		private void AssignCanvas () 
		{
			AssignAllCanvas (_canvasTextures.ToArray ());
		}
		/// <summary>
		/// Assigns the current canvas with the specified, current modified texture
		/// </summary>
		private void AssignModCanvas (RenderTexture modTex) 
		{
			AssignModCanvas (_canvasTextures, modTex);
		}
		/// <summary>
		/// Assigns the current canvas with the specified, current modified texture
		/// </summary>
		private void AssignModCanvas (List<RenderTexture> RTs, RenderTexture modTex) 
		{
			if (canvasFormat == Format.Multi)
			{
				RenderTexture[] textures = RTs.ToArray ();
				textures[curTexIndex] = modTex;
				AssignAllCanvas (textures);
			}
			else
				AssignAllCanvas (modTex);
		}
		/// <summary>
		/// Assigns an arbitrary set of textures with the same length as the internal texture set
		/// </summary>
		private void AssignAllCanvas (params RenderTexture[] tex) 
		{
			if (OnAssignCanvas != null && (!hasCanvas || tex.Length == _canvasTextures.Count))
				OnAssignCanvas (tex);
		}

		private void AssignCanvasNames () 
		{
			AssureRTs ();
			if (hasCanvas)
			{
				if (_canvasTextures.Count > 1)
				{
					for (int i = 0; i < _canvasTextures.Count; i++)
						_canvasTextures[i].name = canvasName + "("+i+")";
				}
				else
					_canvasTextures[0].name = canvasName;

				tempPass1.name = canvasName + ":Pass1";
				tempPass2.name = canvasName + ":Pass2";
				tempResult.name = canvasName + ":TempResult";
				vizCanvas.name = canvasName + ":Canvas";
			}
		}

		private void MatchCanvasChannelCount ()
		{
			if (_canvasTextures == null || _canvasTextures.Count <= 0)
				return;
			if (_canvasTextures == null)
				MatchCanvasChannelCount (0);
			else 
			{
				if (canvasFormat != Format.Multi)
					MatchCanvasChannelCount (1);
				else if (_canvasTextures == null)
					MatchCanvasChannelCount (canvasChannelCount);
				else
					MatchCanvasChannelCount (Mathf.Max (canvasChannelCount, (_canvasTextures.Count-1)*4));
			}
		}

		private void MatchCanvasChannelCount (int chCount)
		{
			if (canvasFormat != Format.Multi)
				chCount = 1;
			if (chCount > 32)
				chCount = 32;
			if (chCount < 0)
				chCount = 0;
			
			// Match RTs
			int RTCount = Mathf.CeilToInt ((float)chCount/4);
			bool changedRTs = MatchListLength (ref _canvasTextures, RTCount, () => new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear), (RT) => ReleaseRT (RT));
			// Match Channels
			_canvasChannelCount = chCount;
			MatchListLength (ref canvasChannelDefinitions, chCount, () => new CanvasChannel (canvasChannelDefinitions.Count), null, false);
			// Reassign names if necessary
			if (changedRTs)
				AssignCanvasNames ();
		}

		private bool MatchListLength<T> (ref List<T> list, int count, System.Func<T> CreateElement, System.Action<T> RemoveElement, bool removeElements = true)
		{
			if (list == null)
				list = new List<T> (count);
			int diff = count-list.Count;
			for (int i = 0; i < diff; i++)
				list.Add (CreateElement != null? CreateElement () : default(T));
			if (removeElements)
			{
				if (RemoveElement != null)
					for (int i = count; i < list.Count; i++)
						RemoveElement (list[i]);
				list.RemoveRange (count, Mathf.Max (0, -diff));
			}
			return diff != 0;
		}

		/// <summary>
		/// Fills the specified RT with the new color
		/// </summary>
		private void Fill (RenderTexture RT, Color fillColor) 
		{
			RenderTexture prevRT = RenderTexture.active;
			RenderTexture.active = RT;
			GL.Clear (true, true, fillColor);
			RenderTexture.active = prevRT;
		}

		#endregion

		#region Public Canvas Utility

		/// <summary>
		/// Deletes (Unloads) the current canvas
		/// </summary>
		public void DeleteCanvas ()
		{
			ReleaseRTs (true, true);
			string prevName = canvasName;
			canvasName = "None";
			RegisterCanvasUndo ("Deleted " + prevName);
		}

		/// <summary>
		/// Creates a new canvas with specified resolution in either color or grayscale
		/// </summary>
		public void NewCanvas (int resX, int resY, Format format, string name)
		{
			NewCanvas (resX, resY, format, name, 1);
		}

		/// <summary>
		/// Creates a new canvas with specified resolution in either color or grayscale
		/// </summary>
		public void NewCanvas (int resX, int resY, Format format, string name, int channelCount)
		{
			if (resX <= 8 || resY <= 8 || resX > 8192 || resY > 8192 || channelCount <= 0)
				throw new System.ArgumentException ("Could not create canvas: Invalid canvas data specified!");
			if (format != Format.Multi && channelCount != 1)
				throw new System.ArgumentException ("Cannot create multiple channels in a canvas not of format " + Format.Multi + "!");
			if (channelCount <= 0)
				throw new System.ArgumentException ("Cannot create canvas with no channels!");

			sizeX = resX;
			sizeY = resY;
			canvasFormat = format;
			canvasName = name;

			ReleaseRTs (true, true);
			MatchCanvasChannelCount (channelCount);
			AssignCanvasNames ();
			CreateTempRTs ();
			PushContent ();
			RegisterCanvasUndo ("Created " + canvasName);

			//if (string.IsNullOrEmpty(cache_Path))
			//	SaveCurrentSession();
		}

		/// <summary>
		/// Adds a canvas channel to the Mutli-Mask canvas
		/// </summary>
		public void AddNewCanvasChannel ()
		{
			if (canvasFormat != Format.Multi)
				throw new System.NotSupportedException ("Cannot add channels to canvases of format color or value!");
			if (canvasChannelCount >= 32)
				throw new System.NotSupportedException ("Currently no more than 32 channels are supported!");
			
			MatchCanvasChannelCount (canvasChannelCount+1);
			PushContent ();
			RegisterCanvasUndo ("New Channel");
		}

		/// <summary>
		/// Adds a canvas channel to the Mutli-Mask canvas
		/// </summary>
		public void DeleteLastCanvasChannel ()
		{
			if (canvasFormat != Format.Multi)
				throw new System.NotSupportedException ("Cannot add channels to canvases of format color or value!");
			if (canvasChannelCount <= 1)
			{
				MatchCanvasChannelCount (1);
				Fill (Color.clear);
				return;
			}

			MatchCanvasChannelCount (canvasChannelCount-1);
			PushContent ();
			RegisterCanvasUndo ("Removed Channel");
		}

		/// <summary>
		/// Imports the specified texture into the canvas, either overriding it's contents but keeping format, 
		/// or recreating the current canvas with the format of the texture if adaptFormat is true
		/// </summary>
		public void ImportTexture (Texture2D tex, bool adaptSpecs)
		{
			ImportTexture (new List<Texture2D> () { tex }, 1, adaptSpecs, canvasFormat);
		}
		/// <summary>
		/// Imports the specified textures into the canvas
		/// </summary>
		public void ImportTexture (List<Texture2D> tex, int channelCount, bool adaptSpecs)
		{
			if (canvasFormat != Format.Multi && tex.Count > 1)
				throw new System.ArgumentException ("Can't import multiple textures into non-Multi format!");
			ImportTexture (tex, channelCount, adaptSpecs, canvasFormat);
		}
		/// <summary>
		/// Imports the specified textures into the canvas
		/// </summary>
		public void ImportTexture (Texture2D tex, bool adaptSpecs, Format format)
		{
			if (format == Format.Multi)
				throw new System.ArgumentException ("Can't import single texture with Multi format!");
			ImportTexture (new List<Texture2D> () { tex }, 1, adaptSpecs, format);
		}
		/// <summary>
		/// Imports the specified textures into the canvas
		/// </summary>
		public void ImportTexture (List<Texture2D> tex, int channelCount, bool adaptSpecs, Format format)
		{	
			if (format != Format.Multi && tex.Count > 1)
				throw new System.ArgumentException ("Can't import multiple textures with non-Multi format!");
			if (tex == null || tex.Count <= 0 || tex.Any ((Texture2D t) => t == null))
				throw new System.ArgumentException ("Trying to import null canvas!");
			
			// Validate tex and channel count
			if (format != Format.Multi)
				channelCount = 1;
			else if (Mathf.CeilToInt ((float)channelCount/4) != tex.Count)
			{
				Debug.LogWarning ("Trying to import " + tex.Count + " textures with " + channelCount + " channels! Correcting!");
				channelCount = tex.Count * 4;
			}
			if (adaptSpecs)
			{ // New textures with source specs
				ReleaseRTs (true, true);
				sizeX = tex[0].width;
				sizeY = tex[0].height;
			}
			// Prepare textures and copy content
			canvasFormat = format;
			canvasName = tex[0].name;
			MatchCanvasChannelCount (channelCount);
			for (int i = 0; i < tex.Count; i++)
				Graphics.Blit (tex[i], _canvasTextures[i]);
			// Apply changes
			if (adaptSpecs)
				CreateTempRTs ();
			PushContent ();
			// Register Undo
			RegisterCanvasUndo ((adaptSpecs? "Loaded " : "Imported ") + canvasName + "");

			//if (string.IsNullOrEmpty(cache_Path))
			//	SaveCurrentSession();
		}

		/// <summary>
		/// Resize the canvas and it's contents to the new size
		/// </summary>
		public void Resize (int newWidth, int newHeight)
		{
			if (!hasCanvas || (newWidth == sizeX && newHeight == sizeY))
				return;
			sizeX = newWidth;
			sizeY = newHeight;
			List<RenderTexture> renderTextures = new List<RenderTexture> (_canvasTextures.Count);
			for (int i = 0; i < _canvasTextures.Count; i++)
			{
				RenderTexture newRT = new RenderTexture (sizeX, sizeY, 0, RTFormat);
				Graphics.Blit (_canvasTextures[i], newRT);
				renderTextures.Add (newRT);
			}
			ReleaseRTs (true, true);
			_canvasTextures = renderTextures;
			AssignCanvasNames ();
			CreateTempRTs ();
			PushContent ();
			RegisterCanvasUndo ("Resized: (" + sizeX + ", " + sizeY + ")");
		}

		/// <summary>
		/// Expands the canvas while keeping the canvas content the same
		/// When new size is larger, it creates empty space around the canvas, else it cuts the content
		/// </summary>
		public void Expand (int newWidth, int newHeight)
		{
			if (!hasCanvas || (newWidth == sizeX && newHeight == sizeY))
				return;

			PaintMat.SetVector ("sourceRect", new Vector4 (0,0,1,1));
			float ratioX = (float)sizeX/newWidth, ratioY = (float)sizeY/newHeight;
			PaintMat.SetVector ("targetRect", new Vector4 (0.5f-ratioX/2, 0.5f-ratioY/2, ratioX, ratioY));

			sizeX = newWidth;
			sizeY = newHeight;

			List<RenderTexture> renderTextures = new List<RenderTexture> (_canvasTextures.Count);
			for (int i = 0; i < _canvasTextures.Count; i++)
			{
				RenderTexture newRT = new RenderTexture (sizeX, sizeY, 0, RTFormat);
				PaintMat.SetTexture ("_RTCanvas", _canvasTextures[i]);
				RenderCurrentSetup (6, newRT);
				renderTextures.Add (newRT);
			}
			ReleaseRTs (true, true);
			_canvasTextures = renderTextures;

			// Call to scale canvas parent
			OnScaleCanvas (1/ratioX, 1/ratioY);
			//TODO: Scale Undo when expanding canvas

			AssignCanvasNames ();
			CreateTempRTs ();
			PushContent ();
			RegisterCanvasUndo ("Expanded: (" + sizeX + ", " + sizeY + ")");
		}

		/// <summary>
		/// Fills the canvas with the new color
		/// </summary>
		public void Fill (Color fillColor) 
		{
			if (!hasCanvas)
				return;
			for (int i = 0; i < _canvasTextures.Count; i++)
				Fill (_canvasTextures[i], fillColor);
			CanvasChange (false);
			PushContent ();
			RegisterCanvasUndo ("Filled canvas");
		}

		#endregion

		#region Public Canvas Sampling

		/// <summary>
		/// Samples the canvas at the specified position in local 0-1 space and returns the color at that position.
		/// Don't call every frame or GPU will be stalled!!
		/// </summary>
		public Color Sample (Vector2 pos)
		{
			if (!AssureRTs ())
				return Color.clear;

			pos.x = Mathf.Clamp01 (pos.x) * sizeX;
			pos.y = (1-Mathf.Clamp01 (pos.y)) * sizeY;

			RenderTexture prevRT = RenderTexture.active;
			RenderTexture.active = vizCanvas;
			Texture2D tex = new Texture2D (1, 1, TexFormat, false);
			tex.ReadPixels (new Rect (pos, Vector2.one), 0, 0, false);
			RenderTexture.active = prevRT;

			Color col = tex.GetPixel (0, 0);
			GlobalPainting.DestroyObj (tex);
			return col;
		}

		/// <summary>
		/// Samples the canvas in the specified rectangle in local 0-1 space and returns the colors in this rectangle.
		/// Don't call every frame or GPU will be stalled!!
		/// </summary>
		public Color[] Sample (Rect rect)
		{
			if (!AssureRTs ())
				return new Color[] { Color.clear };

			rect.xMin = Mathf.Clamp01 (rect.xMin) * sizeX;
			rect.xMax = Mathf.Clamp01 (rect.xMax) * sizeX;
			rect.yMin = (1-Mathf.Clamp01 (rect.yMin)) * sizeY;
			rect.yMax = (1-Mathf.Clamp01 (rect.yMax)) * sizeY;

			if (rect.width <= 0 || rect.height <= 0)
				return new Color[] { Color.clear };

			RenderTexture prevRT = RenderTexture.active;
			RenderTexture.active = vizCanvas;
			Texture2D tex = new Texture2D ((int)Mathf.Max(1, rect.width), (int)Mathf.Max(1, rect.height), TexFormat, false);
			tex.ReadPixels (rect, 0, 0, false);
			RenderTexture.active = prevRT;
			return tex.GetPixels ();
		}

		// SNAPSHOT

		/// <summary>
		/// Gets a snapshot snapshot from the canvas
		/// </summary>
		public Texture2D getSnapshot () 
		{
			return getSnapshot (curTexIndex);			
		}

		/// <summary>
		/// Gets a snapshot snapshot from the canvas
		/// </summary>
		public Texture2D getSnapshot (int texIndex) 
		{
			if (!AssureRTs ())
				throw new System.NullReferenceException ("No canvas existing!");
			if (texIndex < 0 || (canvasFormat != Format.Multi && texIndex != 0) || (canvasFormat == Format.Multi && canvasChannelCount <= texIndex))
				throw new System.IndexOutOfRangeException ("Canvas does not contain a channel with index " + texIndex);

			Texture2D canvasTex = new Texture2D (sizeX, sizeY, TexFormat, false, true);
			canvasTex.name = canvasName + (canvasFormat == Format.Multi? ("("+texIndex+")") : "");
//			if ((SystemInfo.copyTextureSupport | UnityEngine.Rendering.CopyTextureSupport.RTToTexture) != 0)
//			{
//				Graphics.CopyTexture (_canvasTextures[texIndex], canvasTex);
//			}
//			else
//			{
				RenderTexture prevRT = RenderTexture.active;
				RenderTexture.active = _canvasTextures[texIndex];
				canvasTex.ReadPixels (new Rect (0, 0, sizeX, sizeY), 0, 0);
				canvasTex.Apply ();
				RenderTexture.active = prevRT;
//			}

			//RenderTexture RT = _canvasTextures[texIndex];
			//Debug.Log ("RT of size " + RT.width + "x" + RT.height + " in format " + RT.format + " takes " + UnityEngine.Profiling.Profiler.GetRuntimeMemorySize (RT)/1000 + "KB of memory!"); 
			//Debug.Log ("Snapshot of size " + canvasTex.width + "x" + canvasTex.height + " in format " + canvasTex.format + " takes " + UnityEngine.Profiling.Profiler.GetRuntimeMemorySize (canvasTex)/1000 + "KB of memory!");
			//canvasTex.Compress (true);
			//UnityEditor.EditorUtility.CompressTexture (canvasTex, TextureFormat.ASTC_RGBA_4x4, TextureCompressionQuality.Fast);
			//Debug.Log ("Compressed Snapshot of size " + canvasTex.width + "x" + canvasTex.height + " in format " + canvasTex.format + " takes " + UnityEngine.Profiling.Profiler.GetRuntimeMemorySize (canvasTex)/1000 + "KB of memory!");


			return canvasTex;
		}

		/// <summary>
		/// Gets a raw snapshot in the current format (Color or Value) of the current canvas
		/// BitDepth specifies the bitDepth in grayscake format only
		/// </summary>
		public byte[] getRawSnapshot (int bitDepth)
		{
			return getRawSnapshot (bitDepth, curTexIndex);
		}

		/// <summary>
		/// Gets a raw snapshot in the current format (Color or Value) of the canvas texture at index
		/// BitDepth specifies the bitDepth in grayscake format only
		/// </summary>
		public byte[] getRawSnapshot (int bitDepth, int texIndex)
		{
			Texture2D snapshot = getSnapshot (texIndex);
			byte[] rawTex = canvasFormat == Format.Value? RawImportExport.GetRawGrayscale (snapshot, bitDepth) : snapshot.GetRawTextureData ();
			GlobalPainting.DestroyObj (snapshot);
			return rawTex;
		}

		#endregion

		#region Canvas Session Cache

		public void LoadLastSession (bool forceLoad = false, bool canRefreshAssetDatabase = true)
		{
			if (!cache_Canvas && !forceLoad)
				return;

#if UNITY_EDITOR // Get cache path from selected cache asset
			if (cache_Asset != null && UnityEditor.AssetDatabase.Contains (cache_Asset))
				cache_Path = UnityEditor.AssetDatabase.GetAssetPath(cache_Asset);
#endif

			if (string.IsNullOrEmpty(cache_Path))
			{ // Try to fetch from default path
				string path = Settings.lastSessionCacheFolder + "/" + uniqueID + "_" + canvasName + ".bytes";
				if (File.Exists(path))
					cache_Path = path;
			}

			// Check if cache is existant
			cache_Canvas = !string.IsNullOrEmpty (cache_Path);
			if (!cache_Canvas)
				return;

			// Import cached canvas
			if (!ImportCanvas (cache_Path, cache_Format, cache_ChannelCnt, cache_SizeX, cache_SizeY))
				Debug.LogWarning ("Failed to load cache from '" + cache_Path + "'!");
			// Try to update cache asset if possible
			CheckCacheAsset(canRefreshAssetDatabase);
		}

		public void SaveCurrentSession (bool canRefreshAssetDatabase = true) 
		{
			if (!hasCanvas)
			{ // Just ignore cache if canvas was deleted
				cache_Canvas = false;
				return;
			}

			int cache_TexCnt = Mathf.CeilToInt (((float)cache_ChannelCnt)/4);
			if (canvasUndoList.Count == 0 && canvasRedoList.Count == 0 && Settings.enableUndo && cache_Canvas && ExistsSession(cache_Path, cache_TexCnt))
				return; // If there's nothing to save, don't save anything

			if (!Directory.Exists (Settings.lastSessionCacheFolder))
				Directory.CreateDirectory(Settings.lastSessionCacheFolder);

			// Generate and check new cache path
			string curSessionPath = Settings.lastSessionCacheFolder + "/" + uniqueID + "_" + canvasName + ".bytes";
#if UNITY_EDITOR // Write in existing cache asset if existant
			if (cache_Asset != null && UnityEditor.AssetDatabase.Contains(cache_Asset))
				curSessionPath = UnityEditor.AssetDatabase.GetAssetPath(cache_Asset);
#endif

			// Export canvas to cache path
			if (!ExportCanvas(curSessionPath))
				Debug.LogError("Failed to save cache to '" + curSessionPath + "'!");

			// Save information about the cache
			cache_Canvas = true;
			cache_Path = curSessionPath;
			cache_SizeX = sizeX;
			cache_SizeY = sizeY;
			cache_Format = canvasFormat;
			cache_ChannelCnt = canvasChannelCount;
			// Try to update cache asset if possible
			CheckCacheAsset(canRefreshAssetDatabase);
		}

		public void CheckCacheAsset(bool refreshDatabase = true)
		{
#if UNITY_EDITOR
			if (cache_Asset == null)
			{ // Load cache asset file
				if (refreshDatabase)  // Have to refresh database
					UnityEditor.AssetDatabase.Refresh();
				cache_Asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(cache_Path);
				if (cache_Asset != null)
					UnityEditor.EditorGUIUtility.PingObject(cache_Asset);
			}
#endif
		}

		public static void DeleteSession (string path, int texCnt) 
		{
			if (File.Exists(path))
				File.Delete (path);
			else if (texCnt > 1)
			{
				int endNameIndex = path.LastIndexOf ('.');
				for (int i = 0; i < texCnt; i++)
					File.Delete (path.Insert (endNameIndex, "("+i+")"));
			}
		}

		private bool ExistsSession (string path, int texCnt) 
		{
			if (texCnt <= 0)
				return false;
			if (File.Exists (path))
				return true;
			if (texCnt > 1)
			{
				int endNameIndex = path.LastIndexOf ('.');
				for (int i = 0; i < texCnt; i++)
				{
					if (!File.Exists(path.Insert (endNameIndex, "("+i+")")))
						return false;
				}
			}
			return true;
		}

		#endregion

		#region Import/Export

		/// <summary>
		/// Exports the canvas textures to the path with extension .png, .raw or .bytes.
		/// For all types but the combined format .bytes, '(n)' is appended to the name for multiple canvas textures in the multi format.
		/// </summary>
		public bool ExportCanvas(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;
			bool rawEncoding = path.EndsWith(".raw") || path.EndsWith(".bytes");
			int expectedByteLength = sizeX * sizeY * (int)bitDepth * (canvasFormat == Format.Value ? 1 : 4);

			// Encode all textures
			List<byte[]> saveData = new List<byte[]>();
			for (int i = 0; i < canvasTextureCount; i++)
				saveData.Add(rawEncoding ? getRawSnapshot((int)bitDepth, i) : getSnapshot(i).EncodeToPNG());

			// Make sure there are no failures
			if (rawEncoding)
			{
				if (saveData.Any(o => o == null || o.Length != expectedByteLength))
				{ // Found invalid raw data
					Debug.LogWarning("Unexpected save data size! " + 
						"Expected " + sizeX + "*" + sizeY + "*" + (int)bitDepth + "*" + (canvasFormat == Format.Value ? 1 : 4) + "=" + expectedByteLength + " Bytes!" + 
						" Received " + saveData[0].Length + "Bytes!");
					return false;
				}
			}

			// Save encoded texture bytes
			if (path.EndsWith(".bytes"))
			{ // Combined file format
				int texOffset = 16, texCount = saveData.Count, texSize = expectedByteLength;
				byte[] combinedSave = new byte[texOffset + texCount * texSize];
				Array.Copy(BitConverter.GetBytes((short)texOffset), 0, combinedSave, 0, 2);
				Array.Copy(BitConverter.GetBytes((short)texCount), 0, combinedSave, 2, 2);
				Array.Copy(BitConverter.GetBytes((int)texSize), 0, combinedSave, 4, 4);
				Array.Copy(BitConverter.GetBytes((short)sizeX), 0, combinedSave, 8, 2);
				Array.Copy(BitConverter.GetBytes((short)sizeY), 0, combinedSave, 10, 2);
				Array.Copy(BitConverter.GetBytes((short)canvasFormat), 0, combinedSave, 12, 2);
				Array.Copy(BitConverter.GetBytes((short)canvasChannelCount), 0, combinedSave, 14, 2);
				// Write in tex byte blocks
				for (int i = 0; i < saveData.Count; i++)
					Array.Copy(saveData[i], 0, combinedSave, texOffset + i * texSize, texSize);
				// Write file
				File.WriteAllBytes(path, combinedSave);
			}
			else
			{ // Potentially split files
				if (canvasFormat == Format.Multi)
				{ // Multi format -> save as subfiles name(n)
					int itInd = Path.GetFileNameWithoutExtension(path).LastIndexOf('(');
					if (itInd > 0) // Clear name from existing (n) postfix
						path = Path.GetDirectoryName(path) + Path.AltDirectorySeparatorChar + Path.GetFileNameWithoutExtension(path).Substring(0, itInd) + Path.GetExtension(path);
					int endNameIndex = path.LastIndexOf('.');
					for (int i = 0; i < saveData.Count; i++)
						File.WriteAllBytes(path.Insert(endNameIndex, "(" + i + ")"), saveData[i]);
				}
				else if (saveData.Count > 0) // Save file
					File.WriteAllBytes(path, saveData[0]);
			}

			return true;
		}

		/// <summary>
		/// Imports the data at the path with extension .png, .raw or .bytes into the canvas.
		/// Don't call for .raw paths, use the overload specifying the canvas information for the raw file instead.
		/// </summary>
		public bool ImportCanvas(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;
			if (path.EndsWith(".raw"))
				Debug.LogWarning("No canvas information specified for raw import! Using default canvas data!");
			int texCount;
			if (getMultiTextureInfo(ref path, out texCount))
				return ImportCanvas(path, Format.Multi, texCount * 4, sizeX, sizeY);
			else
				return ImportCanvas(path, canvasFormat != Format.Multi ? canvasFormat : Format.Color, 1, sizeX, sizeY);
		}

		/// <summary>
		/// Imports the data at the path with extension .png, .raw or .bytes into the canvas.
		/// For all extensions but .raw, additional information does not need to be specified and is read out of the file.
		/// </summary>
		public bool ImportCanvas(string path, Format format, int chCnt, int sX, int sY)
		{
			if (string.IsNullOrEmpty(path))
				return false;
			path = ResourceManager.MakePathAbsolute(path);
			bool rawEncoding = path.EndsWith(".raw") || path.EndsWith(".bytes");

			// Load encoded texture bytes
			List<byte[]> saveData = new List<byte[]>();
			if (path.EndsWith(".bytes"))
			{ // Combined file format
				if (!File.Exists(path))
					return false;

				// Fetch save data and information
				byte[] combinedSave = File.ReadAllBytes(path);
				int texOffset = BitConverter.ToInt16(combinedSave, 0);
				int texCount = BitConverter.ToInt16(combinedSave, 2);
				int texSize = BitConverter.ToInt32(combinedSave, 4);
				sX = BitConverter.ToInt16(combinedSave, 8);
				sY = BitConverter.ToInt16(combinedSave, 10);
				format = (Format)BitConverter.ToInt16(combinedSave, 12);
				chCnt = BitConverter.ToInt16(combinedSave, 14);
				if (combinedSave.Length != (texOffset + texCount * texSize))
					return false;

				for (int i = 0; i < texCount; i++)
				{ // Read out tex byte blocks
					byte[] texBytes = new byte[texSize];
					Array.Copy(combinedSave, texOffset + i * texSize, texBytes, 0, texSize);
					saveData.Add(texBytes);
				}
			}
			else
			{ // Potentially Split files
				if (format == Format.Multi)
				{ // Multi format -> load all subfiles name(n)
					int texCount = Mathf.CeilToInt(((float)chCnt) / 4);
					int itInd = Path.GetFileNameWithoutExtension(path).LastIndexOf('(');
					if (itInd > 0) // Clear name from existing (n) postfix
						path = Path.GetDirectoryName(path) + Path.AltDirectorySeparatorChar + Path.GetFileNameWithoutExtension(path).Substring(0, itInd) + Path.GetExtension(path);
					int endNameIndex = path.LastIndexOf('.');
					for (int i = 0; i < texCount; i++)
					{ // Read bytes from subfile
						string texPath = path.Insert(endNameIndex, "(" + i + ")");
						if (File.Exists(texPath))
							saveData.Add(File.ReadAllBytes(texPath));
						else
							return false;
					}
				}
				else if (File.Exists(path)) // Read bytes
					saveData.Add(File.ReadAllBytes(path));
				else
					return false;
			}

			// Make sure there are no failures
			if (rawEncoding)
			{
				int expectedByteLength = sX * sY * (int)bitDepth * (format == Format.Value ? 1 : 4);
				if (saveData.Any (o => o == null || o.Length != expectedByteLength))
				{ // Found invalid raw data
					Debug.LogWarning("Unexpected save data size!");
					return false;
				}
			}
			
			// Decode all textures
			List<Texture2D> sessionTextures = new List<Texture2D>(saveData.Count);
			for (int i = 0; i < saveData.Count; i++)
			{ // Decode and rename appropriately
				Texture2D tex = null;
				if (rawEncoding)
					tex = RawImportExport.LoadRawImage(saveData[i], (int)format, sX, sY);
				else
				{
					tex = new Texture2D(sX, sY, TexFormat, false);
					tex.LoadImage(saveData[i]);
				}
				tex.name = Path.GetFileNameWithoutExtension(path);
				if (Regex.IsMatch (tex.name, @"\d+_"))
					tex.name = tex.name.Substring(tex.name.IndexOf("_") + 1);
				sessionTextures.Add(tex);
			}
			// Import all loaded textures
			ImportTexture(sessionTextures, chCnt, true, format);

			return true;
		}

		/// <summary>
		/// Returns how many textures are saved for the multi-canvas at path.
		/// Checks for the format 'name(n)' with n between 0 and 8, path following the same convention
		/// </summary>
		public static bool getMultiTextureInfo (ref string path, out int count) 
		{
			// Find iterator as (n) after the name
			int itInd = Path.GetFileNameWithoutExtension (path).LastIndexOf ('(');
			if (itInd > 0)
			{
				path = Path.GetDirectoryName (path) + Path.AltDirectorySeparatorChar + Path.GetFileNameWithoutExtension (path).Substring (0, itInd) + Path.GetExtension (path);
				// Try to find as much textures with this naming convention as possible
				int endNameIndex = path.LastIndexOf ('.');
				for (count = 0; count < 9; count++)
				{
					if (!File.Exists(path.Insert(endNameIndex, "(" + count + ")")))
						break;
				}
				return true;
			}
			count = 1;
			return false;
		}

		#endregion

		#region Undo

		public bool canPerformUndo { get { return Settings.enableUndo && canvasUndoList != null && canvasUndoList.Count > 1; } }
		public bool canPerformRedo { get { return Settings.enableUndo && canvasRedoList != null && canvasRedoList.Count > 0; } }

		public string getNextUndoName { get { return canvasUndoList.Count > 1? canvasUndoList[canvasUndoList.Count-1].message : "None"; } }
		public string getNextRedoName { get { return canvasRedoList.Count > 0? canvasRedoList[canvasRedoList.Count-1].message : "None"; } }

		private void RegisterCanvasUndo (string message) 
		{
			RegisterCanvasUndo (message, false);
		}

		private void RegisterCanvasUndo (string message, bool writeMods) 
		{
			if (canvasUndoList == null)
				canvasUndoList = new List<UndoRecord> ();
			if (canvasRedoList == null)
				canvasRedoList = new List<UndoRecord> ();
			
			if (!Settings.enableUndo)
			{
				ClearRecordsUntil (canvasRedoList, 0);
				ClearRecordsUntil (canvasUndoList, 0);
				return;
			}
			MatchCanvasChannelCount ();

			// Build undo record
			UndoRecord undoRecord = new UndoRecord ();
			undoRecord.name = canvasName;
			undoRecord.message = message;
			if (hasCanvas)
			{
				// Set canvas information
				undoRecord.sizeX = sizeX;
				undoRecord.sizeY = sizeY;
				undoRecord.format = canvasFormat;
				undoRecord.texFormat = RTFormat;
				// Save snapshot
				undoRecord.tex = new List<Texture2D> ();
				for (int i = 0; i < _canvasTextures.Count; i++)
				{
					Texture2D snapshot = getSnapshot (i);
					if (snapshot != null)
						snapshot.name = "TPUNDO:" + ID + ":" + message + "("+i+")";
					undoRecord.tex.Add (snapshot);
				}
				SetModifiedTexture (curTexIndex);
				undoRecord.curIndex = curTexIndex;
				//undoRecord.channels = canvasChannelDefinitions;
				undoRecord.channelCount = canvasChannelCount;
				// Save mods and whether they are important
				undoRecord.writeMods = writeMods;
				undoRecord.mods = mods;
			}

			// Add undo record and clear redo list
			canvasUndoList.Add (undoRecord);
			ClearRecordsUntil (canvasUndoList, Settings.undoStackSize);
			ClearRecordsUntil (canvasRedoList, 0);
		}

		// If set, all textures not modified will get deleted from the record to save memory
		private void SetModifiedTexture (int index) 
		{
			if (canvasUndoList.Count > 0)
			{	
				UndoRecord prevRec = canvasUndoList[canvasUndoList.Count-1];
				prevRec.nextIndex = index;
			}
		}

		public void PerformUndo () 
		{
			if (!canPerformUndo)
				return;
			// Take the last operation record from undo stack
			UndoRecord canvasRecord = canvasUndoList[canvasUndoList.Count-1];
			canvasUndoList.RemoveAt (canvasUndoList.Count-1);
			// And put it on the redo stack
			canvasRedoList.Add (canvasRecord);
			ClearRecordsUntil (canvasRedoList, Settings.undoStackSize);
			// Undo it by restoring the previous record
			canvasRecord = canvasUndoList[canvasUndoList.Count-1];
			RestoreUndoRecord (canvasRecord);
		}

		public void PerformRedo () 
		{
			if (!canPerformRedo)
				return;
			// Take the last undone operation from the redo stack
			UndoRecord canvasRecord = canvasRedoList[canvasRedoList.Count-1];
			canvasRedoList.RemoveAt (canvasRedoList.Count-1);
			// And put it back on the undo stack
			canvasUndoList.Add (canvasRecord);
			ClearRecordsUntil (canvasUndoList, Settings.undoStackSize);
			// Then restore it to redo the operation it represents
			RestoreUndoRecord (canvasRecord);
		}

		private void ClearRecordsUntil (List<UndoRecord> recordList, int targetSize) 
		{
			while (recordList.Count > targetSize)
			{
				UndoRecord record = recordList[0];
				if (record.tex != null)
				{
					for (int i = 0; i < record.tex.Count; i++)
						GlobalPainting.DestroyObj (record.tex[i]);
				}
				recordList.RemoveAt (0);
			}
		}

		/// <summary>
		/// Restores the state of the specified canvasRecord
		/// </summary>
		private void RestoreUndoRecord (UndoRecord rec) 
		{
			if (rec.tex != null && rec.tex.Count > 0 && !rec.tex.Any ((Texture2D tex) => tex == null))
			{
				bool specsChanged = !hasCanvas || canvasFormat != rec.format || sizeX != rec.sizeX || sizeY != rec.sizeY || RTFormat != rec.texFormat;
				if (specsChanged)
				{ // Clear canvases and write specs
					ReleaseRTs (true, true);
					canvasFormat = rec.format;
					sizeX = rec.sizeX;
					sizeY = rec.sizeY;
					CanvasChange (true);
				}
				if (rec.writeMods)
					mods = rec.mods;
				canvasName = rec.name;

				// Restore canvas content
				//canvasChannelDefinitions = rec.channels;
				MatchCanvasChannelCount (canvasFormat == Format.Multi? rec.channelCount : 1);
				for (int i = 0; i < _canvasTextures.Count; i++)
					Graphics.Blit (rec.tex[i], _canvasTextures[i]);
				
				// Udpate temp RTs
				if (specsChanged)
					CreateTempRTs ();
			}
			else
			{ // Canvas was null
				ReleaseRTs (true, true);
			}
			PushContent ();
		}

		#endregion


		#region Painting

		/// <summary>
		/// Paint state is updated
		/// </summary>
		public bool PaintUpdate (string DrawingSource, int controlID, bool IsOnCanvas, bool BlockPainting, Vector2 BrushPos) 
		{
			if (!AssureRTs ())
				return false;

			if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
				return false;

			BlockPainting = BlockPainting || blockPainting;

			if (controlID == 0)
				controlID = GUIUtility.GetControlID (FocusType.Passive);

			if (isDrawing && DrawingSource != drawingSource)
				return false;

			if (BlockPainting && !isDrawing)
				return false;

			bool mouseMove = Event.current.GetTypeForControl (controlID) == EventType.MouseMove;
			bool mouseDrag = Event.current.GetTypeForControl (controlID) == EventType.MouseDrag && Event.current.button == 0;
			bool mouseDown = Event.current.GetTypeForControl (controlID) == EventType.MouseDown && Event.current.button == 0;
			bool mouseUp = Event.current.GetTypeForControl (controlID) == EventType.MouseUp && Event.current.button == 0;

			isOnCanvas = IsOnCanvas;
			brushPos = BrushPos;

			bool painted = false;

			if (!BlockPainting && (/*mouseDrag || */mouseDown))
			{ // Start Painting
				if (isDrawing)
					EndPainting ();

				GUIUtility.hotControl = controlID;
				if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
					Event.current.Use ();
				
				StartPainting (DrawingSource);
				if (IsOnCanvas)
				{ // So you can only click once to paint
					painted = true;
					Paint (brushPos);
				}
			}

			if (isDrawing)
			{
				if (mouseUp || mouseMove || Event.current.button != 0)
				{ // End Painting
					GUIUtility.hotControl = 0;
					if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
						Event.current.Use ();
					EndPainting ();
				}
				else if (Event.current.type != EventType.Repaint /*&& Event.current.type != EventType.Layout*/ && !BlockPainting && (mouseDown || mouseDrag || Settings.continuousPaint))
				{ // Painting
					GUIUtility.hotControl = controlID;
					if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
						Event.current.Use ();
					
					if (IsOnCanvas)
					{
						painted = true;
						Paint (brushPos);
					}
				}
			}

			return painted;
		}

		public void StopPainting (string DrawingSource) 
		{
			if (isDrawing && (drawingSource == DrawingSource || (Event.current.type == EventType.MouseUp && Event.current.button == 0) || Event.current.button != 0))
				EndPainting ();
		}

		private void StartPainting (string DrawingSource)  
		{ // Set TempPassRead, which is the base to draw on, correctly
			if (!AssureRTs ())
				throw new System.NullReferenceException ("StartPainting: Canvas is null!");

			isDrawing = true;
			drawingSource = DrawingSource;

			RenderTexture.active = null;
			if (seperatePaintPass) // Current stroke is seperate so result has to be blended afterwards
				Fill (TempPassRead, Color.clear);
			else // Blit the current canvas into temp pass, directly working on that
				Graphics.Blit (currentCanvasTex, TempPassRead);
			ResetTimer (ref lastPaintTime, Settings.targetPaintInterval);
			ResetTimer (ref lastGenerationTime, Settings.targetGenerationInterval);
			CanvasChange (false);
			if (OnStartPainting != null)
				OnStartPainting.Invoke ();
		}

		private void EndPainting () 
		{ // Save strokes
			if (Event.current.type == EventType.Repaint)
				return;
			isDrawing = false;
			if (!AssureRTs ())
				throw new System.NullReferenceException ("EndPainting: Canvas is null!");
			
			ResetTimer (ref lastPaintTime, Settings.targetPaintInterval);
			ResetTimer (ref lastGenerationTime, Settings.targetGenerationInterval);

			RenderTexture.active = null;

			if (seperatePaintPass)
			{
				if (canvasFormat == Format.Multi && normalizeMultiChannels)
				{ // Passes have already been blended with the seperate pass in _tempCanvasTextures
					if (_tempCanvasTextures == null)
						throw new System.NullReferenceException ("Temp Canvas Textures are null on EndPainting with MultiNormalization!");
					List<RenderTexture> tTex = _canvasTextures;
					_canvasTextures = _tempCanvasTextures;
					_tempCanvasTextures = tTex;
				}
				else
				{ // Merge passes
					MatSetupBase ();
					MatSetupBlend (currentCanvasTex, TempPassRead, (int)curBrush.mode, 1f);
					RenderCurrentSetup (1, tempResult);
					Graphics.Blit (tempResult, currentCanvasTex);
				}
			}
			else
				Graphics.Blit (TempPassRead, currentCanvasTex);
			
			CanvasChange (false);
			PushContent ();
			RegisterCanvasUndo ("" + curBrush.mode.ToString () + "");
			if (OnEndPainting != null)
				OnEndPainting.Invoke ();
		}

		/// <summary>
		/// Applies a paint stroke at the current position
		/// </summary>
		public void Paint (Vector2 pos) 
		{
			if (Event.current.type == EventType.Repaint)
				return;
			
			float timeStep = 1;
			if (isDrawing && !CheckTimer (ref lastPaintTime, Settings.targetPaintInterval, out timeStep))
				return; // Make sure to draw according to the target framerate

			if (Mathf.Abs (pos.x-0.5f)-curBrush.size < 0.5f && Mathf.Abs (pos.y-0.5f)-curBrush.size < 0.5f)
			{ // Brush position is in the bounds of the canvas
				
				RenderTexture.active = null;

				if (OnPainting != null)
					OnPainting.Invoke ();

				// Setup Material
				PaintMat.SetTexture ("_RTCanvas", TempPassRead);
				PaintMat.SetFloat ("_timeStep", Settings.continuousPaint? timeStep*20 : 1);
				MatSetupBase ();
				MatSetupBrush ();

				// Render stroke to temp pass texture
				RenderCurrentSetup (0, TempPassWrite);
				tempSwitch = !tempSwitch;

				CurPaintTexRaw = TempPassRead;
				if (seperatePaintPass) 
					PushContent (currentCanvasTex, TempPassRead);
				else
					PushContent (TempPassRead);
			}
		}

		public RenderTexture getBrushPreview (int resolution, bool applySettings) 
		{
			RenderTexture target = new RenderTexture (resolution, resolution, 0, RenderTextureFormat.ARGB32);

			// Setup Material
			PaintMat.SetTexture ("_RTCanvas", target);
			PaintMat.SetFloat ("_timeStep", 20);
			PaintMat.SetVector ("_channelMask", Vector4.one);

			PaintMat.SetInt ("sizeX", resolution);
			PaintMat.SetInt ("sizeY", resolution);

			PaintMat.SetVector ("_brushPos", new Vector4 (0.5f, 0.5f, 0, 0));

			PaintMat.SetColor ("_color", Color.white * (applySettings? colorIntensity*targetValue : 1));
			PaintMat.SetFloat ("_intensity", applySettings? curBrush.intensity : 1);
			PaintMat.SetInt ("_brushMode", applySettings? (int)curBrush.mode : (int)PaintMode.Add);

			PaintMat.SetFloat ("_size", curBrush.size);
			PaintMat.SetFloat ("_falloff", curBrush.falloff);
			PaintMat.SetFloat ("_hardness", curBrush.hardness);

			// Set Brush Texture
			Texture2D brushTex = UpdateBrushType ();
			PaintMat.SetInt ("_brushType", brushFunc);
			PaintMat.SetTexture ("_brushTex", brushTex);

			RenderCurrentSetup (0, target);
			return target;
		}

		public Texture2D TextureFromRT (RenderTexture rt) 
		{
			Texture2D tex = new Texture2D (rt.width, rt.height, TextureFormat.ARGB32, true);
			RenderTexture.active = rt;
			tex.ReadPixels (new Rect (0, 0, rt.width, rt.height), 0, 0, true);
			RenderTexture.active = null;
			tex.Apply ();
			return tex;
		}

		#endregion

		#region Post-Modification

		/// <summary>
		/// Pushes the internal canvas content to the modified visualization state
		/// </summary>
		private void PushContent ()
		{
			PushContent (null, null);
		}

		/// <summary>
		/// Pushes the internal canvas content with modified current texture according to the canvasFormat to the modified visualization state
		/// </summary>
		private void PushContent (RenderTexture curTexFrame)
		{
			PushContent (curTexFrame, null);
		}

		/// <summary>
		/// Pushes the internal canvas content with one modified current texture according to the canvasFormat to the modified visualization state and blended with the specified texture
		/// </summary>
		private void PushContent (RenderTexture curTexFrame, RenderTexture blendTex)
		{
			if (!hasCanvas)
			{
				AssignAllCanvas (null);
				return;
			}
			if (isDrawing && !CheckTimer (ref lastGenerationTime, Settings.targetGenerationInterval))
				return;
			if (canvasFormat != Format.Multi && _canvasTextures.Count != 1)
				throw new System.ArgumentException ("Invalid texture count: Cannot have multiple textures without Multi Format!");

			RenderTexture.active = null;

			if (curTexFrame == null)
				curTexFrame = currentCanvasTex;

			bool needsSingleBlend = blendTex != null, 
				needsMultiBlend = canvasFormat == Format.Multi && canvasVizState == CanvasVizState.All, 
				needsMods = applyingOngoingMods;

			if (!needsSingleBlend && !needsMultiBlend && !needsMods)
			{ // No need to blend any textures or to modify the canvas
				Graphics.Blit (curTexFrame, vizCanvas);
				AssignModCanvas (vizCanvas);
				return;
			}

			MatSetupBase ();
			int pass = -1;
			List<RenderTexture> curCanvasRTs = _canvasTextures;

			if (needsSingleBlend)
			{
				if (canvasFormat == Format.Multi)
				{ // Need to blend ALL textures with the single individually - cannot be merged with other passes, so it's done beforehand
					PaintMat.DisableKeyword ("BLEND_SINGLE");
					// Prepare blend targets and material
					MatchListLength (ref _tempCanvasTextures, _canvasTextures.Count, () => new RenderTexture (sizeX, sizeY, 0, RTFormat, RenderTextureReadWrite.Linear), (tex) => ReleaseRT (tex));
					MatSetupIndividualBlend (blendTex, curTexFrame, (int)curBrush.mode, curBrush.intensity);
					// Render normalized result in temp RT list
					for (int i = 0; i < _canvasTextures.Count; i++)
					{
						PaintMat.SetTexture ("_RTCanvas", curCanvasRTs[i]);
						if (normalizeMultiChannels)
							PaintMat.SetVector ("_channelMask", i == curTexIndex? getChannelMask (curChannelIndex%4) : Vector4.zero);
						RenderCurrentSetup (normalizeMultiChannels? 5 : 1, _tempCanvasTextures[i]);
					}

					// Set to use result
					CanvasChange (false);
					curCanvasRTs = _tempCanvasTextures;
					curTexFrame = curCanvasRTs[curTexIndex];
				}
				else
				{ // Single blend for seperate pass
					pass = 1;
					PaintMat.EnableKeyword ("BLEND_SINGLE");
					MatSetupBlend (curTexFrame, blendTex, (int)curBrush.mode, 1f);
				}
			}
			else
				PaintMat.DisableKeyword ("BLEND_SINGLE");

			// SETUP MAT
			if (canvasFormat == Format.Multi && canvasVizState == CanvasVizState.All)
			{ // Set all textures to combine
				pass = 4;
				MatSetupMultiBlend (curCanvasRTs);
			}
			else if (applyingOngoingMods)
			{ // Apply modifications
				pass = needsSingleBlend? 3 : 2;
				MatSetupMods (canvasFormat == Format.Color && mods.advancedChannelMods);
			}

			PaintMat.SetTexture ("_RTCanvas", curTexFrame);

			// RENDER
			if (pass != -1)
				RenderCurrentSetup (pass, vizCanvas);
			else
				Graphics.Blit (curTexFrame, vizCanvas);

			// ASSIGN
			if (needsMultiBlend)
				AssignModCanvas (curCanvasRTs, curTexFrame); // TODO: Cannot assign blended tex, so need to send unmodified stack
			else
				AssignModCanvas (curCanvasRTs, vizCanvas);
		}

		/// <summary>
		/// Applies all current modifications to the canvas
		/// </summary>
		public void ApplyModifications ()
		{
			if (!applyingOngoingMods)
				return;
			if (canvasFormat == Format.Multi)
				throw new System.NotImplementedException ("Cannot apply modifications on multi canvas!");
			// Save changes on modification values so you can go back without resetting the values
			RegisterCanvasUndo ("Modification", true);

			// Apply modifications
			MatSetupBase ();
			PaintMat.SetTexture ("_RTCanvas", currentCanvasTex);
			MatSetupMods (canvasFormat == Format.Color && mods.advancedChannelMods);
			RenderCurrentSetup (2, vizCanvas);

			// Reset modification values
			mods = new Modifications (true);
			CanvasChange (true);

			// Save change
			Graphics.Blit (vizCanvas, currentCanvasTex);
			RegisterCanvasUndo ("Applied", true);
		}

		/// <summary>
		/// Updates the visualized canvas by re-applying the ongoing modifications on the internal canvas
		/// </summary>
		public void UpdateModifications () 
		{
			PushContent ();
		}

		#endregion

		#region PaintMat Preparation

		private void MatSetupBase () 
		{
			// Setup Canvas
			PaintMat.SetInt ("sizeX", sizeX);
			PaintMat.SetInt ("sizeY", sizeY);

			// Set work channels
			if (canvasFormat == Format.Multi)
				PaintMat.SetVector ("_channelMask", getChannelMask (curChannelIndex%4));
			else
				PaintMat.SetVector ("_channelMask", Vector4.one);
			
			// Shader Features
			if (Settings.enableGPUUniformBranching) // Whether uniform if clauses should be calculated or branched
				PaintMat.DisableKeyword ("CALC_BRANCHES");
			else
				PaintMat.EnableKeyword ("CALC_BRANCHES");
		}

		private void MatSetupBrush () 
		{
			// Set Brush Texture
			Texture2D brushTex = UpdateBrushType ();
			PaintMat.SetInt ("_brushType", brushFunc);
			PaintMat.SetTexture ("_brushTex", brushTex);

			// Set Brush Parameters
			PaintMat.SetVector ("_brushPos", new Vector4 (brushPos.x, brushPos.y, 0, 0));

			PaintMat.SetColor ("_color", curColor * (invertPainting && curBrush.mode == PaintMode.Add? -colorIntensity : colorIntensity));
			PaintMat.SetFloat("_intensity", curBrush.intensity);
			PaintMat.SetFloat ("_size", curBrush.size);
			PaintMat.SetFloat ("_falloff", curBrush.falloff);
			PaintMat.SetFloat ("_hardness", curBrush.hardness);
			PaintMat.SetInt ("_brushMode", seperatePaintPass? -1 : (int)(invertPainting? InvertPaintMode (curBrush.mode) : curBrush.mode));

			PaintMat.SetFloat ("_smoothenBias", smoothenBias);
			PaintMat.SetFloat ("_targetValue", invertPainting? 1-targetValue : targetValue);

			// Apply brush rotation matrix
			Matrix4x4 rotMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, (brushRotation+canvasRotation) * 180), Vector3.one);
			PaintMat.SetMatrix("_rotationMatrix", rotMatrix);

			//PaintMat.SetInt ("_clampMode", (int)clampMode);
			PaintMat.SetInt ("_clamp01", clampResult01 && !seperatePaintPass? 1 : 0);
		}

		private void MatSetupMods (bool channelMods) 
		{
			// Setup modification settings
			//PaintMat.SetInt ("_clamp01", clampResult? 1 : 0);
			PaintMat.SetFloat ("_brightness", mods.brightness);
			PaintMat.SetFloat ("_contrast", mods.contrast);
			PaintMat.SetColor ("_tintColor", mods.tintColor);

			if (channelMods)
			{
				PaintMat.EnableKeyword ("MOD_CHANNEL");
				PaintMat.SetInt ("shuffleR", mods.chR.shuffle);
				PaintMat.SetInt ("shuffleG", mods.chG.shuffle);
				PaintMat.SetInt ("shuffleB", mods.chB.shuffle);
				PaintMat.SetInt ("shuffleA", mods.chA.shuffle);

				PaintMat.SetVector ("_channelOffset", new Vector4 (
					mods.chR.invert? 1+mods.chR.offset : mods.chR.offset, 
					mods.chG.invert? 1+mods.chG.offset : mods.chG.offset, 
					mods.chB.invert? 1+mods.chB.offset : mods.chB.offset, 
					mods.chA.invert? 1+mods.chA.offset : mods.chA.offset));
				PaintMat.SetVector ("_channelScale", new Vector4 (
					mods.chR.invert? -mods.chR.scale : mods.chR.scale, 
					mods.chG.invert? -mods.chG.scale : mods.chG.scale, 
					mods.chB.invert? -mods.chB.scale : mods.chB.scale, 
					mods.chA.invert? -mods.chA.scale : mods.chA.scale));
			}
			else
				PaintMat.DisableKeyword ("MOD_CHANNEL");
		}

		private void MatSetupBlend (Texture baseTex, Texture blendTex, int mode, float intensity) 
		{
			PaintMat.SetTexture ("_RTCanvas", baseTex);
			PaintMat.SetTexture ("_blendTex", blendTex);
			PaintMat.SetInt ("_blendMode", mode);
			PaintMat.SetFloat ("_blendAmount", intensity);
			PaintMat.SetInt ("_clamp01", clampResult01? 1 : 0);
		}

		private void MatSetupIndividualBlend (Texture blendTex, Texture curTex, int mode, float intensity)  
		{
			PaintMat.SetTexture ("_blendTex", blendTex);
			if (normalizeMultiChannels)
			{
				PaintMat.SetTexture ("_curChannelTex", curTex);
				PaintMat.SetInt ("_curChannelIndex", curChannelIndex%4);
			}

			PaintMat.SetInt ("_blendMode", mode);
			PaintMat.SetFloat ("_blendAmount", intensity);
			PaintMat.SetInt ("_clamp01", clampResult01? 1 : 0);

			PaintMat.SetVector ("_channelMask", Vector4.one);

			if (normalizeMultiChannels)
				PaintMat.EnableKeyword ("NORMALIZE_CHANNELS");
			else
				PaintMat.DisableKeyword ("NORMALIZE_CHANNELS");
		}

		private void MatSetupMultiBlend (List<RenderTexture> RTs) 
		{
		#if UNITY_5_4_OR_NEWER
			bool supportsTexArrays = SystemInfo.supports2DArrayTextures && (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture) != 0;
			if (RTs.Count > 2 && !supportsTexArrays)
				throw new System.NotSupportedException ("System either does not support Texture Arrays or Graphics.CopyTexture RTToTexture and thus cannot use more than 8 Channels!");
		#endif

			PaintMat.SetInt ("_canvasTexCount", RTs.Count);
			PaintMat.SetInt ("_curTexIndex", curTexIndex);

			if (channelVizColors == null) 
				ReadColorsIn ();
		#if UNITY_5_4_OR_NEWER
			PaintMat.SetColorArray ("_channelColors", channelVizColors);
		#else
			for (int colCnt = 0; colCnt < channelVizColors.Length; colCnt++)
				PaintMat.SetColor ("_channelColors" + colCnt, channelVizColors[colCnt]);
		#endif
		
		#if UNITY_5_4_OR_NEWER
			if (RTs.Count > shaderManualTextureCount)
			{ // Set three or more textures using a texture array
				if (cachedMultiTexVizArray == null)
				{ // Create new cached texture array
					cachedMultiTexVizArray = new Texture2DArray (sizeX, sizeY, RTs.Count, TexFormat, false, true);
					for (int i = 0; i < RTs.Count; i++)
						Graphics.CopyTexture (RTs[i], 0, 0, cachedMultiTexVizArray, i, 0);
				}

				PaintMat.EnableKeyword ("ENABLE_TEXTURE_ARRAYS");
				PaintMat.SetTexture ("_canvasTextures", cachedMultiTexVizArray);
			}	
			else
		#endif
			{ // Set two or less textures as normal variables
				PaintMat.DisableKeyword ("ENABLE_TEXTURE_ARRAYS");
				for (int i = 0; i < RTs.Count; i++)
					PaintMat.SetTexture ("_canvasTex"+(i+1), RTs[i]);
			}

			int chInd = canvasChannelCount%4; // 1 -> 0111
			if (chInd > 0)
				PaintMat.SetVector ("lastTexInvMask", new Vector4 (chInd<=0? 1 : 0, chInd<=1? 1 : 0, chInd<=2? 1 : 0, chInd<=3? 1 : 0));
			else
				PaintMat.SetVector ("lastTexInvMask", Vector4.zero);
		}

		/// <summary>
		/// Updates the brush type used by the shader specified by the curBrush.type in brushFunc and returns the texture
		/// </summary>
		public Texture2D UpdateBrushType ()
		{
			if (GlobalPainting.brushTextures.Length <= 0)
				return null;
			curBrush.type = Mathf.Clamp (curBrush.type, 0, GlobalPainting.brushTextures.Length-1);
			Texture2D brushTex = GlobalPainting.brushTextures[curBrush.type];
			if (brushType != curBrush.type)
			{
				brushType = curBrush.type;
				if (!brushTex.name.Contains ("_func"))
					brushFunc = 0;
				else
				{
					string funcNumStr = brushTex.name.Substring (brushTex.name.IndexOf ("_func")+5);
					int funcNum;
					if (int.TryParse (funcNumStr, out funcNum))
						brushFunc = funcNum;
					else // If texture is labeled as a function but it does not exist, still use that texture
						brushFunc = 0;
				}
			}
			return brushTex;
		}

		private PaintMode InvertPaintMode (PaintMode mode)
		{
			if (mode == PaintMode.Smoothen)
				return PaintMode.Contrast;
			if (mode == PaintMode.Contrast)
				return PaintMode.Smoothen;
			return mode;
		}

		#endregion

		#region Utility

		/// <summary>
		/// Resets the timer
		/// </summary>
		public void ResetTimer (ref float lastTimePoint, float timerInterval)
		{
			lastTimePoint = Time.realtimeSinceStartup - 2*timerInterval;
		}

		/// <summary>
		/// Returns if the timer has reached it's interval yet and updates it.
		/// </summary>
		public bool CheckTimer (ref float lastTimePoint, float timerInterval)
		{
			bool timer = Time.realtimeSinceStartup-lastTimePoint >= timerInterval;
			lastTimePoint = Time.realtimeSinceStartup - (timerInterval == 0? 0 : ((Time.realtimeSinceStartup-lastTimePoint)%timerInterval));
			return timer;
		}

		/// <summary>
		/// Returns if the timer has reached it's interval yet and updates it. Also outputs timeStep.
		/// </summary>
		public bool CheckTimer (ref float lastTimePoint, float timerInterval, out float timeStep)
		{
			timeStep = Time.realtimeSinceStartup-lastTimePoint;
			bool timer = timeStep >= timerInterval;
			//timeStep = timeStep%timerInterval + timerInterval;
			lastTimePoint = Time.realtimeSinceStartup - (timerInterval == 0? 0 : ((Time.realtimeSinceStartup-lastTimePoint)%timerInterval));
			return timer;
		}

		/// <summary>
		/// Render a quad used for rendering from a material set with SetPass to the target RT
		/// </summary>
		private void RenderQuad () 
		{
			RenderQuad (new Rect (0, 0, 1, 1));
		}

		/// <summary>
		/// Render a quad used for rendering from a material set with SetPass to the target RT
		/// Also lets you specify the rect to execute only
		/// </summary>
		private void RenderQuad (Rect rect) 
		{
			GL.Begin (GL.QUADS);
			GL.TexCoord2 (rect.xMin, rect.yMin); GL.Vertex3 (rect.xMin, rect.yMin, 0.1f);
			GL.TexCoord2 (rect.xMax, rect.yMin); GL.Vertex3 (rect.xMax, rect.yMin, 0.1f);
			GL.TexCoord2 (rect.xMax, rect.yMax); GL.Vertex3 (rect.xMax, rect.yMax, 0.1f);
			GL.TexCoord2 (rect.xMin, rect.yMax); GL.Vertex3 (rect.xMin, rect.yMax, 0.1f);
			GL.End ();
		}

		private void RenderCurrentSetup (int pass, RenderTexture target) 
		{
			RenderTexture prevRT = RenderTexture.active;
			RenderTexture.active = null;
			GL.PushMatrix ();
			GL.LoadOrtho ();

			Graphics.SetRenderTarget (target);
			PaintMat.SetPass (pass);
			RenderQuad ();

			GL.PopMatrix ();
			RenderTexture.active = prevRT;
		}

		private Vector4 getChannelMask (int channel) 
		{
			return new Vector4 (channel==0? 1 : 0, channel==1? 1 : 0, channel==2? 1 : 0, channel==3? 1 : 0);
		}

		#endregion
	}
}
