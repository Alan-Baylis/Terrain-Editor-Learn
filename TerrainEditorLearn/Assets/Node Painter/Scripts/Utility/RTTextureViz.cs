using UnityEngine;

namespace TerrainComposer2.NodePainter.Utilities
{
	public static class RTTextureViz 
	{
		private static Material texVizMat;

		/// <summary>
		/// Draws the texture.
		/// </summary>
		public static void DrawTexture (Texture texture, int texSize, GUIStyle style, params GUILayoutOption[] options) 
		{
			DrawTexture (texture, texSize, style, 1, 2, 3, 4, Color.white, options);
		}

		/// <summary>
		/// Draws the texture with tint applied.
		/// </summary>
		public static void DrawTexture (Texture texture, int texSize, GUIStyle style, Color tint, params GUILayoutOption[] options) 
		{
			DrawTexture (texture, texSize, style, 1, 2, 3, 4, tint, options);
		}


		/// <summary>
		/// Draws the texture.
		/// </summary>
		public static void DrawTexture (Texture texture, Rect rect) 
		{
			DrawTexture (texture, rect, 1, 2, 3, 4, Color.white);
		}

		/// <summary>
		/// Draws the texture with tint applied.
		/// </summary>
		public static void DrawTexture (Texture texture, Rect rect, Color tint) 
		{
			DrawTexture (texture, rect, 1, 2, 3, 4, tint);
		}

		/// <summary>
		/// Draws the texture with shuffled channels.
		/// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
		/// </summary>
		public static void DrawTexture (Texture texture, int texSize, GUIStyle style, int shuffleRed, int shuffleGreen, int shuffleBlue, int shuffleAlpha, params GUILayoutOption[] options) 
		{
			DrawTexture (texture, texSize, style, shuffleRed, shuffleGreen, shuffleBlue, shuffleAlpha, Color.white, options);
		}

		/// <summary>
		/// Draws the texture with shuffled channels.
		/// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
		/// </summary>
		public static void DrawTexture (Texture texture, Rect rect, int shuffleRed, int shuffleGreen, int shuffleBlue, int shuffleAlpha) 
		{
			DrawTexture (texture, rect, shuffleRed, shuffleGreen, shuffleBlue, shuffleAlpha, Color.white);
		}

		/// <summary>
		/// Draws the texture with shuffled channels and tint applied.
		/// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
		/// </summary>
		public static void DrawTexture (Texture texture, int texSize, GUIStyle style, int shuffleRed, int shuffleGreen, int shuffleBlue, int shuffleAlpha, Color tint, params GUILayoutOption[] options) 
		{
			if (options == null || options.Length == 0)
				options = new GUILayoutOption[] { GUILayout.ExpandWidth (false) };
			Rect rect = style == null? GUILayoutUtility.GetRect (texSize, texSize * texture.height/texture.width, options) : GUILayoutUtility.GetRect (texSize, texSize * texture.height/texture.width, style, options);
			DrawTexture (texture, rect, shuffleRed, shuffleGreen, shuffleBlue, shuffleAlpha, tint);
		}

		/// <summary>
		/// Draws the texture with shuffled channels and tint applied.
		/// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
		/// </summary>
		public static void DrawTexture (Texture texture, Rect rect, int shuffleRed, int shuffleGreen, int shuffleBlue, int shuffleAlpha, Color tint) 
		{
			if (texVizMat == null)
				texVizMat = new Material (Shader.Find ("Hidden/GUITextureClip_ChannelControl"));
			texVizMat.SetColor ("tintColor", tint);

			texVizMat.SetInt ("shuffleR", shuffleRed);
			texVizMat.SetInt ("shuffleG", shuffleGreen);
			texVizMat.SetInt ("shuffleB", shuffleBlue);
			texVizMat.SetInt ("shuffleA", shuffleAlpha);

			if (Event.current.type == EventType.Repaint)
				Graphics.DrawTexture (rect, texture, texVizMat);
		}

		/// <summary>
		/// Draws the texture with TC2-style grayscale (max channel value)
		/// </summary>
		public static void DrawTexture (Texture texture, int texSize, GUIStyle style, float grayscale, bool alpha = false, params GUILayoutOption[] options) 
		{
			if (options == null || options.Length == 0)
				options = new GUILayoutOption[] { GUILayout.ExpandWidth (false) };
			Rect rect = style == null? GUILayoutUtility.GetRect (texSize, texSize * texture.height/texture.width, options) : GUILayoutUtility.GetRect (texSize, texSize * texture.height/texture.width, style, options);
			DrawTexture (texture, rect, grayscale, alpha);
		}

		/// <summary>
		/// Draws the texture with TC2-style grayscale (max channel value)
		/// </summary>
		public static void DrawTexture (Texture texture, Rect rect, float grayscale, bool alpha = false) 
		{
			if (texVizMat == null)
				texVizMat = new Material (Shader.Find ("Hidden/GUITextureClip_ChannelControl"));
			texVizMat.EnableKeyword ("GRAYSCALE");
			texVizMat.SetFloat ("_grayscale", grayscale);
			texVizMat.SetInt ("_alpha", alpha? 1 : 0);

			if (Event.current.type == EventType.Repaint)
				Graphics.DrawTexture (rect, texture, texVizMat);
			texVizMat.DisableKeyword ("GRAYSCALE");
		}
	}
}