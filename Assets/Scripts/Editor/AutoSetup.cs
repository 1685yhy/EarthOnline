using UnityEditor;
using UnityEngine;

namespace EarthOnline.Editor
{
    [InitializeOnLoad]
    public class AutoSetup
    {
        private const string KEY = "EO_SetupDone";

        static AutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(KEY, false)) return;
                EditorPrefs.SetBool(KEY, true);
                Debug.Log("[AutoSetup] Running scene setup...");
                SceneSetup.SetupScene();
            };
        }
    }
}
