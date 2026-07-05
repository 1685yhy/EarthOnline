using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EarthOnline.Editor
{
    public class SceneSetup : EditorWindow
    {
        [MenuItem("EarthOnline/Setup Test Scene")]
        public static void SetupScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateGround();
                CreateLighting();
                CreatePlayer();
                CreateNPC();
                CreateGameHUD();
                CreateFrameworkManagers();
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[SceneSetup] Test scene setup complete!");
            }
        }

        static void CreateGround()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.position = Vector3.zero;
            g.transform.localScale = new Vector3(10, 1, 10);
            var m = CreateMaterial("GroundMat", new Color(0.3f, 0.6f, 0.3f));
            g.GetComponent<Renderer>().material = m;
        }

        static void CreateLighting()
        {
            if (Object.FindObjectOfType<Light>() == null)
            {
                var l = new GameObject("Directional Light");
                var lt = l.AddComponent<Light>();
                lt.type = LightType.Directional;
                lt.intensity = 1.2f;
                l.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        static void CreatePlayer()
        {
            var e = GameObject.Find("Player");
            if (e != null) Object.DestroyImmediate(e);
            var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            p.name = "Player";
            p.transform.position = new Vector3(0, 1, 0);
            var cc = p.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0);
            cc.radius = 0.3f;
            cc.height = 2f;
            p.AddComponent<EarthOnline.Player.PlayerController>();
            var h = new GameObject("Head");
            h.transform.SetParent(p.transform);
            h.transform.localPosition = new Vector3(0, 1.8f, 0);
            var m = CreateMaterial("PlayerMat", new Color(0.3f, 0.5f, 0.8f));
            p.GetComponent<Renderer>().material = m;
            TryAddTag("Player");
            p.tag = "Player";
        }

        static void CreateNPC()
        {
            var e = GameObject.Find("NPC_Elder");
            if (e != null) Object.DestroyImmediate(e);
            var n = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            n.name = "NPC_Elder";
            n.transform.position = new Vector3(5, 1, 3);
            var nb = n.AddComponent<EarthOnline.NPC.NPCBase>();
            nb.npcId = "npc_elder_01";
            nb.npcName = "无名老者";
            nb.npcTitle = "山间散人";
            nb.greetingText = "年轻人...你来这里，是为了找什么东西吗？";
            var m = CreateMaterial("NPC_Mat", new Color(0.5f, 0.5f, 0.5f));
            n.GetComponent<Renderer>().material = m;
        }

        static void CreateGameHUD()
        {
            if (Object.FindObjectOfType<Canvas>() != null) return;
            var c = new GameObject("Canvas");
            var cv = c.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            c.AddComponent<EarthOnline.UI.GameHUD>();
        }

        static void CreateFrameworkManagers()
        {
            var m = GameObject.Find("FrameworkManagers");
            if (m == null)
            {
                m = new GameObject("FrameworkManagers");
                m.AddComponent<EarthOnline.Framework.SaveManager>();
                m.AddComponent<EarthOnline.Framework.GiftManager>();
            }
        }

        static Material CreateMaterial(string name, Color color)
        {
            string path = "Assets/Art/Materials/" + name + ".mat";
            var e = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (e != null) return e;
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Art/Materials");
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void TryAddTag(string tag)
        {
            var a = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (a == null || a.Length == 0) return;
            var so = new SerializedObject(a[0]);
            var tp = so.FindProperty("tags");
            for (int i = 0; i < tp.arraySize; i++)
                if (tp.GetArrayElementAtIndex(i).stringValue == tag) return;
            tp.InsertArrayElementAtIndex(tp.arraySize);
            tp.GetArrayElementAtIndex(tp.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }
    }
}
