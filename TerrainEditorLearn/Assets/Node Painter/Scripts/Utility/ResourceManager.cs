using UnityEngine;
using System.Linq;
using System.IO;

namespace TerrainComposer2.NodePainter.Utilities
{
	/// <summary>
	/// Provides methods for loading resources both at runtime and in the editor; 
	/// Though, to load at runtime, they have to be in a resources folder
	/// </summary>
	public static class ResourceManager 
	{
		private static string _ResourcePath = "";
		public static void SetDefaultResourcePath (string defaultResourcePath) 
		{
			_ResourcePath = defaultResourcePath;
		}

		static ResourceManager () 
		{
			if (!System.IO.Directory.Exists (Application.dataPath + Settings.paintingResourcesFolder.Substring ("Assets".Length))) 
			{
				Debug.LogWarning ("Resource folder does not exist! Please select a valid path in the settings!");
				return;
			}
			SetDefaultResourcePath (Settings.paintingResourcesFolder + "/");
		}

		/// <summary>
		/// Trims the path to be retlative to the last folder with the specified name
		/// </summary>
		public static string TrimPath (string path, string folder) 
		{
			if (path.StartsWith (Application.dataPath))
				path = path.Substring (Application.dataPath.Length - "Assets".Length);
			path = path.Replace (@"\", "/");
			folder = "/"+folder+"/";
			if (path.Contains (folder))
				path = path.Substring (path.LastIndexOf (folder) + folder.Length);
			return path;
		}

		/// <summary>
		/// Prepares the path; At Runtime, it will return path relative to Resources, in editor, it will return the assets relative to Assets. Takes any path.
		/// </summary>
		public static string PreparePath (string path) 
		{
			path = path.Replace (Application.dataPath, "Assets");
			#if UNITY_EDITOR
			if (!path.StartsWith ("Assets/"))
				path = _ResourcePath + path;
			return path;
			#else
			if (path.Contains ("Resources"))
			path = path.Substring (path.LastIndexOf ("Resources") + 10);
			return path.Substring (0, path.LastIndexOf ('.'));
			#endif
		}

		/// <summary>
		/// Makes the path absolute (supports absolute, asset and resource paths)
		/// </summary>
		public static string MakePathAbsolute (string path) 
		{
			if (!Path.IsPathRooted (path))
			{
				if (!path.StartsWith ("Assets"))
					path = _ResourcePath + path;
				path = path.Replace ("Assets", Application.dataPath);
			}
			return path;
		}

		/// <summary>
		/// Loads a resource in the resources folder in both the editor and at runtime. 
		/// Path can be global, relative to the assets folder or, if used at runtime only, any subfolder, but has to be in a Resource folder to be loaded at runtime
		/// </summary>
		public static T[] LoadResources<T> (string path) where T : UnityEngine.Object
		{
			path = PreparePath (path);
		#if UNITY_EDITOR // In the editor
			return UnityEditor.AssetDatabase.LoadAllAssetsAtPath (path).OfType<T> ().ToArray ();
		#else
			return UnityEngine.Resources.LoadAll<T> (path);
		#endif
		}

		/// <summary>
		/// Loads a resource in the resources folder in both the editor and at runtime
		/// Path can be global, relative to the assets folder or, if used at runtime only, any subfolder, but has to be in a Resource folder to be loaded at runtime
		/// </summary>
		public static T LoadResource<T> (string path) where T : UnityEngine.Object
		{
			path = PreparePath (path);
		#if UNITY_EDITOR // In the editor
			return UnityEditor.AssetDatabase.LoadAssetAtPath<T> (path);
		#else
			return UnityEngine.Resources.Load<T> (path);
		#endif
		}
	}
}