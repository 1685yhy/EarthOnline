using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace EarthOnline.Editor
{
    /// <summary>
    /// 编辑器工具：一键生成V0.1测试场景。
    /// 使用方法：顶部菜单 EarthOnline → Setup Test Scene
    /// </summary>
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

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[SceneSetup] Test scene setup complete! Press Play to test.");
            }
        }

        static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10, 1, 10);
            Material mat = CreateOrGetMaterial("GroundMat", new Color(0.3f, 0.6f, 0.3f));
            ground.GetComponent<Renderer>().material = mat;
        }

        static void CreateLighting()
        {
            if (FindObjectOfType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        static void CreatePlayer()
        {
            var existing = GameObject.Find("Player");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, 0);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0);
            cc.radius = 0.3f;
            cc.height = 2f;

            player.AddComponent<EarthOnline.Player.PlayerController>();

            GameObject head = new GameObject("Head");
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0, 1.8f, 0);

            Material playerMat = CreateOrGetMaterial("PlayerMat", new Color(0.3f, 0.5f, 0.8f));
            player.GetComponent<Renderer>().material = playerMat;

            TryAddTag("Player");
            player.tag = "Player";
        }

        static void CreateNPC()
        {
            var existing = GameObject.Find("NPC_Elder");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "NPC_Elder";
            npc.transform.position = new Vector3(5, 1, 3);

            EarthOnline.NPC.NPCBase npcBase = npc.AddComponent<EarthOnline.NPC.NPCBase>();
            npcBase.npcId = "npc_elder_01";
            npcBase.npcName = "无名老者";
            npcBase.npcTitle = "山间散人";
            npcBase.greetingText = "年轻人...你来这里，是为了找什么东西吗？";

            Material npcMat = CreateOrGetMaterial("NPC_Mat", new Color(0.5f, 0.5f, 0.5f));
            npc.GetComponent<Renderer>().material = npcMat;
        }

        /// <summary>安全创建材质，已存在则复用</summary>
        static Material CreateOrGetMaterial(string name, Color color)
        {
            string path = $"Assets/Art/Materials/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureDirectory("Assets/Art/Materials");
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>确保目录存在</summary>
        static void EnsureDirectory(string path)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
            if (!System.IO.Directory.Exists(fullPath))
                System.IO.Directory.CreateDirectory(fullPath);
        }

        static void CreateGameHUD()
        {
            var existingCanvas = FindObjectOfType<Canvas>();
            if (existingCanvas != null) return;

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            canvasObj.AddComponent<EarthOnline.UI.GameHUD>();
        }

        static void CreateFrameworkManagers()
        {
            GameObject managers = GameObject.Find("FrameworkManagers");
            if (managers == null)
            {
                managers = new GameObject("FrameworkManagers");
                managers.AddComponent<EarthOnline.Framework.SaveManager>();
                managers.AddComponent<EarthOnline.Framework.GiftManager>();
            }
        }

        static void TryAddTag(string tag)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[SceneSetup] TagManager.asset not found, skipping tag setup.");
                return;
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue.Equals(tag)) { found = true; break; }
            }

            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                tagManager.ApplyModifiedProperties();
            }
        }
    }
}
