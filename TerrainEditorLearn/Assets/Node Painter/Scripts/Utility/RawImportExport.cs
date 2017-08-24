using UnityEngine;

namespace TerrainComposer2.NodePainter.Utilities
{
	public static class RawImportExport
	{
		public enum BitDepth { Bit8 = 1, Bit16 = 2, Bit32 = 4 }

		private static TextureFormat TexFormat (int bitDepth)
		{
			return bitDepth <= 1? TextureFormat.RGBA32 : (bitDepth == 2? TextureFormat.RGBAHalf : TextureFormat.RGBAFloat);
		}

		private static RenderTextureFormat RTFormat (int bitDepth)
		{
			return bitDepth == 1? RenderTextureFormat.ARGB32 : (bitDepth == 2? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat);
		}

		/// <summary>
		/// Loads a raw image with the specified specs into an RGBAFloat normal texture, assuming it to be either grayscale or color, depending on the current setting.
		/// </summary>
		public static Texture2D LoadRawImage (byte[] rawData, int format, int sizeX, int sizeY) 
		{
			if (format == 1)
				return LoadGrayscaleRaw (rawData, sizeX, sizeY);
			int bitDepth = (int)(rawData.Length/4/sizeX/sizeY);
			Texture2D rawImage = new Texture2D (sizeX, sizeY, TexFormat(bitDepth), false);
			rawImage.LoadRawTextureData (rawData);
			rawImage.Apply ();
			return rawImage;
		}

		/// <summary>
		/// Loads a square grayscale raw file with either 32Bit, 16Bit or 8Bit depth into an RGBAFloat normal texture
		/// </summary>
		public static Texture2D LoadGrayscaleRaw (byte[] rawData) 
		{
			int byteDepth, resolution;
			if (!ReadSquareRawSpecs (rawData.Length, out byteDepth, out resolution))
				throw new System.FormatException ("Could not load raw texture as it not square or does not have a bit depth of either 8, 16 or 32 Bit!");
			return LoadGrayscaleRaw (rawData, resolution, resolution);
		}

		/// <summary>
		/// Loads a grayscale raw file with specified size into an RGBAFloat normal texture
		/// </summary>
		public static Texture2D LoadGrayscaleRaw (byte[] rawData, int sizeX, int sizeY) 
		{
			int bitDepth = rawData.Length/(sizeX*sizeY);
			if (bitDepth == 3 || (bitDepth != 1 && bitDepth != 2 && bitDepth != 4))
				throw new System.FormatException ("Can't load 24Bit raw file, only 8-, 16- and 32Bit!");

			Color[] colors = new Color[sizeX*sizeY];
			for (int i = 0; i < colors.Length; i++)
			{
				float grayscale;
				if (bitDepth == 1)
					grayscale = (float)rawData[i*bitDepth];
				else if (bitDepth == 2)
					grayscale = ((float)System.BitConverter.ToUInt16 (rawData, i*bitDepth))/System.Int16.MaxValue/2;
				else
					grayscale = System.BitConverter.ToSingle (rawData, i*bitDepth);
				colors[i] = new Color (grayscale, grayscale, grayscale, grayscale);
			}
			Texture2D rawImage = new Texture2D (sizeX, sizeY, TexFormat(bitDepth), false);
			rawImage.SetPixels (colors);
			rawImage.Apply ();
			return rawImage;
		}

		/// <summary>
		/// Tries to read specs of a square raw file.
		/// </summary>
		public static bool ReadSquareRawSpecs (long byteLength, out int bitDepth, out int resolution)
		{
			return ReadSquareRawSpecs (byteLength, 1, out bitDepth, out resolution);
		}

		/// <summary>
		/// Tries to read specs of a square raw file.
		/// </summary>
		public static bool ReadSquareRawSpecs (long byteLength, int channelCount, out int bitDepth, out int resolution)
		{
			bool identified = true;
			float tempRes = 0;
			if ((tempRes = Mathf.Sqrt (byteLength/channelCount/4)) == Mathf.Floor (tempRes))
				bitDepth = 4;
			else if ((tempRes = Mathf.Sqrt (byteLength/channelCount/3)) == Mathf.Floor (tempRes))
				bitDepth = 3;
			else if ((tempRes = Mathf.Sqrt (byteLength/channelCount/2)) == Mathf.Floor (tempRes))
				bitDepth = 2;
			else if ((tempRes = Mathf.Sqrt (byteLength/channelCount/1)) == Mathf.Floor (tempRes))
				bitDepth = 1;
			else
			{ // Unidentified
				bitDepth = 0;
				identified = false;
			}
			resolution = (int)tempRes;
			//		Debug.Log (identified? ((bitDepth*8) + "Bit " + resolution + "!") : "Could not identify specs of raw file length " + byteLength + "!");
			return identified;
		}

		public static byte[] GetRawGrayscale (Texture2D tex, int bitDepth) 
		{
			if (tex == null)
				return new byte[0];

			if (bitDepth != 4 && bitDepth != 2 && bitDepth != 1)
				throw new System.FormatException ("Can only create 32Bit (4), 16Bit (2) or 8Bit (1) grayscale snapshots! Not " + bitDepth);

			Color[] colors = tex.GetPixels ();
			byte[] rawBytes = new byte[tex.width*tex.height*bitDepth];
			for (int i = 0; i < colors.Length; i++)
			{
				Color col = colors[i];
				float grayscale = Mathf.Max (col.r, Mathf.Max (col.g, Mathf.Max (col.b, col.a)));

				byte[] bytes;
				if (bitDepth == 1)
					bytes = new byte[] { (byte)grayscale };
				else if (bitDepth == 2)
					bytes = System.BitConverter.GetBytes ((ushort)(grayscale*System.Int16.MaxValue*2));
				else
					bytes = System.BitConverter.GetBytes (grayscale);
				System.Buffer.BlockCopy (bytes, 0, rawBytes, i*bitDepth, bitDepth);
			}
			return rawBytes;
		}
	}

}