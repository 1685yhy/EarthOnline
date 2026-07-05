using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EarthOnline.Editor
{
    /// <summary>
    /// 一键设置 V0.1 完整场景。
    /// </summary>
    public static class EarthOnlineSetup
    {
        [MenuItem("EarthOnline/Setup V0.1 Scene")]
        public static void SetupScene()
        {
            // Clean up ALL dynamic objects from previous runs
            var allObjects = Object.FindObjectsOfType<GameObject>();
            var toDestroy = new System.Collections.Generic.List<string> {
                "Ground", "Player", "OldMan_Zhang", "GameManager",
                "Canvas", "FrameworkManagers", "NPC_Elder", "CameraRig", "GameHUD"
            };
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                string name = go.name;
                bool shouldDestroy = toDestroy.Contains(name)
                    || name.StartsWith("Tree_")
                    || name.StartsWith("Rock_");
                if (shouldDestroy)
                    Object.DestroyImmediate(go);
            }

            // ==================== GROUND ====================
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5, 1, 5);
            var groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.3f, 0.5f, 0.2f);
            ground.GetComponent<Renderer>().material = groundMat;

            // ==================== PLAYER ====================
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0, 1.5f, 0);
            Object.DestroyImmediate(player.GetComponent<Rigidbody>());
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0);
            cc.height = 2f;
            cc.radius = 0.5f;
            // Attach PlayerController via AddComponent by type name
            var pcType = System.Type.GetType("EarthOnline.Player.PlayerController, Assembly-CSharp");
            if (pcType != null && player.GetComponent(pcType) == null)
                player.AddComponent(pcType);

            // ==================== CAMERA ====================
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var cfType = System.Type.GetType("EarthOnline.CameraFollow, Assembly-CSharp");
                if (cfType != null && mainCam.GetComponent(cfType) == null)
                {
                    var cf = mainCam.gameObject.AddComponent(cfType);
                    // Set default values via reflection
                    cfType.GetField("target")?.SetValue(cf, player.transform);
                    cfType.GetField("offset")?.SetValue(cf, new Vector3(0, 3, -6));
                    cfType.GetField("smoothSpeed")?.SetValue(cf, 5f);
                }
                mainCam.transform.position = player.transform.position + new Vector3(0, 3, -6);
            }

            // ==================== LIGHT ====================
            var light = GameObject.Find("Directional Light");
            if (light == null)
            {
                light = new GameObject("Directional Light");
                var dl = light.AddComponent<Light>();
                dl.type = LightType.Directional;
                dl.intensity = 1.2f;
                dl.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // ==================== GAME MANAGER ====================
            var gm = new GameObject("GameManager");
            var gmType = System.Type.GetType("EarthOnline.GameManager, Assembly-CSharp");
            if (gmType != null) gm.AddComponent(gmType);

            // ==================== EVENT SYSTEM ====================
            var es = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ==================== NPC ====================
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "OldMan_Zhang";
            npc.transform.position = new Vector3(5, 1.2f, 3);
            npc.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Object.DestroyImmediate(npc.GetComponent<Rigidbody>());
            var npcType = System.Type.GetType("EarthOnline.NPC.NPCBase, Assembly-CSharp");
            if (npcType != null)
            {
                var n = npc.AddComponent(npcType);
                npcType.GetField("npcId")?.SetValue(n, "npc_zhang_001");
                npcType.GetField("npcName")?.SetValue(n, "张老");
                npcType.GetField("npcTitle")?.SetValue(n, "神秘老者");
                npcType.GetField("greetingText")?.SetValue(n, "年轻人，你也穿越了？...看来这个世界的'玩家'越来越多了。");
                npcType.GetField("interactionRange")?.SetValue(n, 5f);
            }
            var npcRenderer = npc.GetComponent<Renderer>();
            if (npcRenderer != null)
            {
                var npcMat = new Material(Shader.Find("Standard"));
                npcMat.color = new Color(0.8f, 0.6f, 0.3f);
                npcRenderer.material = npcMat;
            }

            // ==================== ENVIRONMENT ====================
            CreateEnvironmentObjects();

            // ==================== HUD CANVAS ====================
            CreateHUD();

            // ==================== SAVE ====================
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("============================================================");
            Debug.Log("  EarthOnline V0.1 Scene Setup Complete!");
            Debug.Log("  WASD=移动 | Mouse=视角 | Scroll=缩放 | Shift=加速");
            Debug.Log("  E=与NPC对话 | T=签到 | I=查看状态 | ESC=释放鼠标");
            Debug.Log("  Ground ✓ | Player ✓ | Camera ✓ | Light ✓");
            Debug.Log("  NPC ✓ | GameManager ✓ | EventSystem ✓");
            Debug.Log("============================================================");
        }

        static void CreateEnvironmentObjects()
        {
            var positions = new Vector3[]
            {
                new Vector3(-6, 0, -6), new Vector3(6, 0, -6),
                new Vector3(-6, 0, 6), new Vector3(6, 0, 6),
                new Vector3(0, 0, 8), new Vector3(-8, 0, 0),
            };

            foreach (var pos in positions)
            {
                string name = Random.value > 0.5f ? "Tree" : "Rock";
                PrimitiveType pt = name == "Tree" ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                var go = GameObject.CreatePrimitive(pt);
                go.name = $"{name}_{pos.x}_{pos.z}";
                go.transform.position = pos;

                if (name == "Tree")
                {
                    go.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
                    go.transform.position += Vector3.up * 1f;
                    var r = go.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var mat = new Material(Shader.Find("Standard"));
                        mat.color = new Color(0.2f, 0.4f, 0.1f);
                        r.material = mat;
                    }
                }
                else
                {
                    go.transform.localScale = new Vector3(1.5f, 0.6f, 1.5f);
                    var r = go.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var mat = new Material(Shader.Find("Standard"));
                        mat.color = new Color(0.4f, 0.35f, 0.3f);
                        r.material = mat;
                    }
                }
            }
        }

        static void CreateHUD()
        {
            var hudGo = GameObject.Find("GameHUD");
            if (hudGo != null) Object.DestroyImmediate(hudGo);

            hudGo = new GameObject("GameHUD");
            var hudType = System.Type.GetType("EarthOnline.UI.GameHUD, Assembly-CSharp");
            if (hudType != null) hudGo.AddComponent(hudType);

            // Add Canvas
            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = hudGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            hudGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Interaction Hint (bottom center)
            var hintGo = new GameObject("InteractionHint");
            hintGo.transform.SetParent(hudGo.transform);
            var hintText = hintGo.AddComponent<UnityEngine.UI.Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 18;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0.1f);
            hintRect.anchorMax = new Vector2(0.5f, 0.1f);
            hintRect.sizeDelta = new Vector2(400, 40);
            hintRect.anchoredPosition = Vector2.zero;

            // Dialogue Bubble (top center)
            var dlgGo = new GameObject("DialogueBubble");
            dlgGo.transform.SetParent(hudGo.transform);
            var dlgBg = dlgGo.AddComponent<UnityEngine.UI.Image>();
            dlgBg.color = new Color(0, 0, 0, 0.7f);
            var dlgRect = dlgGo.GetComponent<RectTransform>();
            dlgRect.anchorMin = new Vector2(0.5f, 0.7f);
            dlgRect.anchorMax = new Vector2(0.5f, 0.7f);
            dlgRect.sizeDelta = new Vector2(500, 100);
            dlgRect.anchoredPosition = Vector2.zero;

            var dlgTextGo = new GameObject("DialogueText");
            dlgTextGo.transform.SetParent(dlgGo.transform);
            var dlgText = dlgTextGo.AddComponent<UnityEngine.UI.Text>();
            dlgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dlgText.fontSize = 20;
            dlgText.alignment = TextAnchor.MiddleCenter;
            dlgText.color = Color.white;
            var dlgTextRect = dlgTextGo.GetComponent<RectTransform>();
            dlgTextRect.anchorMin = Vector2.zero;
            dlgTextRect.anchorMax = Vector2.one;
            dlgTextRect.sizeDelta = Vector2.zero;

            // Wire up GameHUD references via reflection
            if (hudType != null)
            {
                var hudComp = hudGo.GetComponent(hudType);
                hudType.GetField("interactionHint")?.SetValue(hudComp, hintGo);
                hudType.GetField("interactionText")?.SetValue(hudComp, hintText);
                hudType.GetField("dialogueBubble")?.SetValue(hudComp, dlgGo);
                hudType.GetField("dialogueText")?.SetValue(hudComp, dlgText);
            }

            // Status text (top right)
            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(hudGo.transform);
            var statusText = statusGo.AddComponent<UnityEngine.UI.Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 16;
            statusText.alignment = TextAnchor.UpperRight;
            statusText.color = Color.white;
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(1, 1);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(1, 1);
            statusRect.sizeDelta = new Vector2(300, 30);
            statusRect.anchoredPosition = new Vector2(-20, -20);

            if (hudType != null)
            {
                var hudComp = hudGo.GetComponent(hudType);
                hudType.GetField("statusText")?.SetValue(hudComp, statusText);
            }

            Debug.Log("[Setup] HUD created with Canvas + interaction + dialogue + status.");
        }
    }
}
