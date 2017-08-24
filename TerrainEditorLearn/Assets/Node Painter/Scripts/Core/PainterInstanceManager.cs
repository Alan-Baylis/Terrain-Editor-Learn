using System.Linq;
using UnityEngine;

namespace TerrainComposer2.NodePainter
{
	/// <summary>
	/// Responsible for saving the cache of all NodePainter in the scene when the scene is saved.
	/// For 5.5+, new callbacks are used by the NodePainter itself and this manager is not used.
	/// </summary>
	[AddComponentMenu("")]
	[ExecuteInEditMode]
	public class PainterInstanceManager : MonoBehaviour, ISerializationCallbackReceiver 
	{
		private int serializeCounter = 0;

		public void OnEnable()
		{
			hideFlags = HideFlags.HideInHierarchy;
#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SaveAllNodePainters;
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += SaveAllNodePainters;
#endif
		}

		public void OnDisable()
		{
#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SaveAllNodePainters;
#endif
		}

#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
		public static void SaveAllNodePainters(UnityEngine.SceneManagement.Scene scene, string path)
		{ // Save node painter caches
			NodePainter[] nodePainters = FindObjectsOfType<NodePainter>().Where(painter => painter.gameObject.scene == scene).ToArray();
			foreach (NodePainter painter in nodePainters)
				painter.painter.SaveCurrentSession(true);
		}
#endif

		public void OnBeforeSerialize () 
		{
			if (serializeCounter == 1)
			{
#if !(UNITY_EDITOR && UNITY_5_6_OR_NEWER)
				// Save node painter caches
				NodePainter[] nodePainters = FindObjectsOfType<NodePainter>();
				foreach (NodePainter painter in nodePainters)
					painter.painter.SaveCurrentSession(false);
#endif
			}
			serializeCounter++;
			if (serializeCounter > 2)
				serializeCounter = 0;
		}

		public void OnAfterDeserialize () 
		{
			serializeCounter = 0;
		}
	}
}
