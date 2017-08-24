using UnityEngine;

namespace TerrainComposer2.NodePainter.Utilities
{
	public static class AdditionalGUIUtility
	{
		#region Seperator

		/// <summary>
		/// Efficient space like EditorGUILayout.Space
		/// </summary>
		public static void Space ()
		{
			Space (6);
		}
		/// <summary>
		/// Space like GUILayout.Space but more efficient
		/// </summary>
		public static void Space (float pixels)
		{
			GUILayoutUtility.GetRect (pixels, pixels);
		}


		/// <summary>
		/// A GUI Function which simulates the default seperator
		/// </summary>
		public static void Seperator () 
		{
			setupSeperator ();
			GUILayout.Box (GUIContent.none, seperator, new GUILayoutOption[] { GUILayout.Height (1) });
		}

		/// <summary>
		/// A GUI Function which simulates the default seperator
		/// </summary>
		public static void Seperator (Rect rect) 
		{
			setupSeperator ();
			GUI.Box (new Rect (rect.x, rect.y, rect.width, 1), GUIContent.none, seperator);
		}

		private static GUIStyle seperator;
		private static void setupSeperator () 
		{
			if (seperator == null || seperator.normal.background == null) 
			{
				seperator = new GUIStyle();
				seperator.normal.background = ColorToTex (1, new Color (0.6f, 0.6f, 0.6f));
				seperator.stretchWidth = true;
				seperator.margin = new RectOffset(0, 0, 7, 7);
			}
		}

		#endregion

		/// <summary>
		/// Create a 1x1 tex with color col
		/// </summary>
		public static Texture2D ColorToTex (int pxSize, Color col) 
		{
			Color[] texCol = new Color[pxSize*pxSize];
			for (int c = 0; c < texCol.Length; c++)
				texCol[c] = col;
			Texture2D tex = new Texture2D (pxSize, pxSize);
			tex.SetPixels (texCol);
			tex.Apply ();
			return tex;
		}
	}

}