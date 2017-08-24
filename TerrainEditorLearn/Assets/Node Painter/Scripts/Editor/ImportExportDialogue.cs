using UnityEngine;
using UnityEditor;
using System.IO;

using TerrainComposer2.NodePainter.Utilities;

namespace TerrainComposer2.NodePainter
{
	public class ImportExportDialogue : EditorWindow 
	{
		public Painting Painter;

		public string path;
		public string importPath;
		public bool import;
		//	public bool isRAW;

		private Painting.Format format = Painting.Format.Value;
		private int textureCount = 1;
		private int channelCount = 1;
		private long byteLength;
		private RawImportExport.BitDepth bitDepth;
		private int sizeX, sizeY;

		public static void ImportRawDialogue (Painting painter, string path)
		{
			if (string.IsNullOrEmpty (path))
				return;
			ImportExportDialogue dialogue = EditorWindow.GetWindow<ImportExportDialogue> ("Import RAW");
			dialogue.maxSize = new Vector2 (300, 200);

			dialogue.Painter = painter;
			dialogue.path = path;
			dialogue.import = true;
//			dialogue.isRAW = true;
			dialogue.Init ();
		}

		public static void ExportRawDialogue (Painting painter)
		{
			ImportExportDialogue dialogue = EditorWindow.GetWindow<ImportExportDialogue> ("Export RAW");
			dialogue.maxSize = new Vector2 (300, 200);

			dialogue.Painter = painter;
			dialogue.import = false;
//			dialogue.isRAW = true;
			dialogue.Init ();
		}

		private void Init () 
		{
			if (import)
			{
				byteLength = new FileInfo (path).Length;
				if (Path.GetFileNameWithoutExtension (path).EndsWith (")"))
					format = Painting.Format.Multi;
				FetchSpecsForFormat ();
			}
			else 
			{
				format = Painter.canvasFormat;
				textureCount = Painter.canvasTextureCount;
				channelCount = Painter.canvasChannelCount;
				sizeX = Painter.sizeX;
				sizeY = Painter.sizeY;
				bitDepth = RawImportExport.BitDepth.Bit16;
			}
		}

		public void FetchSpecsForFormat () 
		{
			int bitDepthInt;
			RawImportExport.ReadSquareRawSpecs (byteLength, format == Painting.Format.Value? 1 : 4, out bitDepthInt, out sizeX);
			bitDepth = (RawImportExport.BitDepth)bitDepthInt;
			sizeY = sizeX;

			importPath = path;
			Painting.getMultiTextureInfo(ref importPath, out textureCount);
			channelCount = textureCount*4;
		}

		private void OnGUI () 
		{
			if (import)
			{
				Painting.Format newFormat = (Painting.Format)GUILayout.Toolbar ((int)format, new string[] { "Color", "Value", "Multi" });
				if (format != newFormat)
				{
					format = newFormat;
					FetchSpecsForFormat ();
				}

				if (format == Painting.Format.Multi)
				{
					GUILayout.Label ("Texture Count: " + textureCount);
					channelCount = EditorGUILayout.IntSlider ("Channel Count", channelCount, (textureCount-1)*4+1, textureCount*4);
				}

				bitDepth = (RawImportExport.BitDepth)EditorGUILayout.EnumPopup ("Depth", bitDepth);

				sizeX = EditorGUILayout.IntField ("Size X", sizeX);
				sizeY = EditorGUILayout.IntField ("Size Y", sizeY);

				if ((byteLength/sizeX/sizeY) != (int)bitDepth*(format != Painting.Format.Value? 4 : 1))
					EditorGUILayout.HelpBox ("Specs do not match the file size!", MessageType.Warning);
				else if (GUILayout.Button ("Import"))
				{
					if (Painter.ImportCanvas (importPath, format, channelCount, sizeX, sizeY))
						Close ();
					else
						ShowNotification (new GUIContent ("Failed to Import texture!"));
				}
			}
			else
			{
				GUILayout.Label (format + " Raw");

				bitDepth = (RawImportExport.BitDepth)EditorGUILayout.EnumPopup ("Depth", bitDepth);

				GUILayout.Label ("Width: " + sizeX);
				GUILayout.Label ("Height: " + sizeY);

				GUILayout.Label ("Texture Count: " + textureCount);
				GUILayout.Label ("Channel Count: " + channelCount);

				if (GUILayout.Button ("Export"))
				{
					path = EditorUtility.SaveFilePanel ("Export raw File", Painter.canvasName, "raw", "Choose a path to save the canvas image to.");
					if (!string.IsNullOrEmpty (path))
					{
						if (Painter.ExportCanvas (path))
						{
							AssetDatabase.Refresh ();
							Close ();
						}
						else
							ShowNotification (new GUIContent ("Failed to Export texture!"));
					}
					else
						Close();
				}
				if (bitDepth == RawImportExport.BitDepth.Bit32)
					EditorGUILayout.HelpBox ("Unity Terrain can't import 32-Bit raws, only 8- or 16-Bit! You can import it into Terrain Painter again though.", MessageType.Info);
			}
		}
	}

}