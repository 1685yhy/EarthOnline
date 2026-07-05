using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EarthOnline.Editor
{
    /// <summary>
    /// V0.2 完整场景搭建 —— 双金手指 + NPC巡逻 + 物品拾取 + 村庄环境。
    /// </summary>
    public static class EarthOnlineSetup
    {
        [MenuItem("EarthOnline/Setup V0.2 Scene")]
        public static void SetupScene()
        {
            // ====== CLEAN UP ======
            var cleanNames = new System.Collections.Generic.List<string> {
                "Ground", "Player", "OldMan_Zhang", "GameManager",
                "Canvas", "FrameworkManagers", "NPC_Elder", "CameraRig", "GameHUD",
                "NPC_Wang", "NPC_Li", "VillageHouse"
            };
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == null) continue;
                string n = go.name;
                if (cleanNames.Contains(n) || n.StartsWith("Tree_") || n.StartsWith("Rock_")
                    || n.StartsWith("Pickup_") || n.StartsWith("House_") || n.StartsWith("Fence_")
                    || n.StartsWith("Enemy_"))
                    Object.DestroyImmediate(go);
            }

            // ====== GROUND ======
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(6, 1, 6);
            var gm = new Material(Shader.Find("Standard"));
            gm.color = new Color(0.35f, 0.55f, 0.25f);
            ground.GetComponent<Renderer>().material = gm;

            // ====== PLAYER ======
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0, 1.5f, 0);
            Object.DestroyImmediate(player.GetComponent<Rigidbody>());
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0); cc.height = 2f; cc.radius = 0.5f;
            var pcType = System.Type.GetType("EarthOnline.Player.PlayerController, Assembly-CSharp");
            if (pcType != null && player.GetComponent(pcType) == null)
                player.AddComponent(pcType);
            var pr = player.GetComponent<Renderer>();
            if (pr != null) { var pm = new Material(Shader.Find("Standard")); pm.color = new Color(0.2f, 0.4f, 0.8f); pr.material = pm; }

            // ====== CAMERA ======
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var cfType = System.Type.GetType("EarthOnline.CameraFollow, Assembly-CSharp");
                if (cfType != null && mainCam.GetComponent(cfType) == null)
                {
                    var cf = mainCam.gameObject.AddComponent(cfType);
                    cfType.GetField("target")?.SetValue(cf, player.transform);
                    cfType.GetField("offset")?.SetValue(cf, new Vector3(0, 3, -6));
                    cfType.GetField("smoothSpeed")?.SetValue(cf, 5f);
                }
                mainCam.transform.position = player.transform.position + new Vector3(0, 3, -6);
            }

            // ====== LIGHT ======
            var dl = GameObject.Find("Directional Light");
            if (dl == null)
            {
                dl = new GameObject("Directional Light");
                var l = dl.AddComponent<Light>(); l.type = LightType.Directional;
                l.intensity = 1.2f; l.shadows = LightShadows.Soft;
                dl.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // ====== GAME MANAGER ======
            var gameMgr = new GameObject("GameManager");
            var gmType = System.Type.GetType("EarthOnline.GameManager, Assembly-CSharp");
            if (gmType != null) gameMgr.AddComponent(gmType);
            // InventoryManager on GameManager
            gameMgr.AddComponent<EarthOnline.Framework.InventoryManager>();

            // ====== EVENT SYSTEM ======
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ====== NPCs ======
            CreateNPC("OldMan_Zhang", new Vector3(5, 1.2f, 3),
                "npc_zhang_001", "张老", "神秘老者",
                "年轻人，你也穿越了？...看来这个世界的'玩家'越来越多了。",
                new Color(0.8f, 0.6f, 0.3f), true);

            CreateNPC("NPC_Wang", new Vector3(-4, 1.2f, 5),
                "npc_wang_001", "王铁柱", "铁匠",
                "嘿！要打铁吗？我这儿可是整个新手村最好的铁匠铺！虽然新手村就我一家铁匠铺...",
                new Color(0.6f, 0.4f, 0.2f), true);

            CreateNPC("NPC_Li", new Vector3(3, 1.2f, -5),
                "npc_li_001", "李灵儿", "药铺掌柜",
                "最近采到的灵药越来越少了...山里好像有什么东西在驱赶采药人。",
                new Color(0.3f, 0.7f, 0.4f), false);

            // ====== VILLAGE BUILDINGS ======
            CreateBuilding("House_Blacksmith", new Vector3(-6, 0, 7), new Vector3(3, 2, 3), new Color(0.4f, 0.3f, 0.2f));
            CreateBuilding("House_Herb", new Vector3(5, 0, -7), new Vector3(3, 1.5f, 3), new Color(0.3f, 0.5f, 0.3f));
            CreateBuilding("House_Elder", new Vector3(8, 0, 5), new Vector3(2, 1.8f, 2), new Color(0.5f, 0.4f, 0.3f));

            // ====== WORLD PICKUPS ======
            CreatePickup("Pickup_Herb", new Vector3(8, 0.5f, 2),
                "item_herb_001", "止血草", "Consumable", "N", 3, 15);
            CreatePickup("Pickup_Stone", new Vector3(-8, 0.5f, 3),
                "item_spirit_stone", "灵石碎片", "Material", "R", 1, 50);
            CreatePickup("Pickup_Pill", new Vector3(2, 0.5f, 8),
                "item_pill_001", "聚气丹", "Consumable", "R", 2, 30);
            CreatePickup("Pickup_Ring", new Vector3(-3, 0.5f, -6),
                "item_ring_dark", "黑铁戒指", "Quest", "SR", 1, 200);
            CreatePickup("Pickup_Chaos", new Vector3(-9, 0.5f, -8),
                "item_chaos_fragment", "混沌碎片", "Quest", "SSR", 1, 500);

            // ====== TREES & ROCKS ======
            var envPositions = new Vector3[] {
                new(-9,0,-9), new(9,0,-9), new(-9,0,9), new(9,0,9),
                new(0,0,10), new(-10,0,0), new(10,0,0), new(0,0,-10),
            };
            foreach (var pos in envPositions)
            {
                string nm = Random.value > 0.5f ? "Tree" : "Rock";
                var go = (nm == "Tree")
                    ? GameObject.CreatePrimitive(PrimitiveType.Cylinder)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{nm}_{pos.x}_{pos.z}";
                go.transform.position = pos;
                if (nm == "Tree")
                {
                    go.transform.localScale = new Vector3(0.3f, 2.5f, 0.3f);
                    go.transform.position += Vector3.up * 1.25f;
                    var r = go.GetComponent<Renderer>();
                    if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.15f, 0.35f, 0.1f); r.material = m; }
                }
                else
                {
                    go.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
                    var r = go.GetComponent<Renderer>();
                    if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.4f, 0.35f, 0.3f); r.material = m; }
                }
            }

            // ====== ENEMIES ======
            CreateEnemy("Enemy_Wolf1", new Vector3(-12, 1, -3), "wolf_001", "野狼",
                maxHP: 40, attack: 6, speed: 2.5f, detect: 10f, patrol: 8f,
                dropId: "item_spirit_stone", dropName: "灵石碎片", dropQty: 2,
                color: new Color(0.4f, 0.3f, 0.2f));

            CreateEnemy("Enemy_Wolf2", new Vector3(10, 1, -8), "wolf_002", "灰狼",
                maxHP: 40, attack: 6, speed: 2.5f, detect: 10f, patrol: 8f,
                dropId: "item_spirit_stone", dropName: "灵石碎片", dropQty: 2,
                color: new Color(0.5f, 0.35f, 0.25f));

            CreateEnemy("Enemy_Bear", new Vector3(-8, 1.5f, 10), "bear_001", "狂暴熊",
                maxHP: 100, attack: 15, speed: 2f, detect: 6f, patrol: 4f,
                dropId: "item_pill_001", dropName: "聚气丹", dropQty: 3,
                color: new Color(0.5f, 0.25f, 0.1f));

            // ====== HUD ======
            CreateHUD();

            // ====== SAVE ======
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("============================================================");
            Debug.Log("  EarthOnline V0.2 Scene Setup Complete!");
            Debug.Log("  WASD=移动 | Mouse=视角 | Scroll=缩放 | Shift=加速 | Space=跳");
            Debug.Log("  E=对话 | T=签到 | I=状态 | O=老爷爷 | P=打开背包");
            Debug.Log("  3 NPCs + 4 Pickups + 3 Buildings + 双金手指");
            Debug.Log("============================================================");
        }

        static void CreateNPC(string name, Vector3 pos, string id, string displayName, string title,
            string greeting, Color color, bool isMale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Object.DestroyImmediate(go.GetComponent<Rigidbody>());

            var npcType = System.Type.GetType("EarthOnline.NPC.NPCBase, Assembly-CSharp");
            if (npcType != null)
            {
                var comp = go.AddComponent(npcType);
                npcType.GetField("npcId")?.SetValue(comp, id);
                npcType.GetField("npcName")?.SetValue(comp, displayName);
                npcType.GetField("npcTitle")?.SetValue(comp, title);
                npcType.GetField("greetingText")?.SetValue(comp, greeting);
                npcType.GetField("interactionRange")?.SetValue(comp, 6f);
            }
            go.AddComponent<EarthOnline.NPC.NPCWander>();
            go.AddComponent<EarthOnline.NPC.NPCRelationship>();

            var r = go.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = color; r.material = m; }
        }

        static void CreateBuilding(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos + Vector3.up * scale.y / 2f;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = color; r.material = m; }

            // 屋顶 (三角/斜面)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roof.name = $"{name}_Roof";
            roof.transform.SetParent(go.transform);
            roof.transform.localPosition = Vector3.up * 0.55f;
            roof.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
            var rr = roof.GetComponent<Renderer>();
            if (rr != null) { var rm = new Material(Shader.Find("Standard")); rm.color = new Color(0.6f, 0.2f, 0.1f); rr.material = rm; }
        }

        static void CreatePickup(string name, Vector3 pos, string itemId, string itemName,
            string itemType, string rarity, int qty, int value)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var pickup = go.AddComponent<EarthOnline.WorldPickup>();
            pickup.itemId = itemId;
            pickup.itemName = itemName;
            pickup.itemType = itemType;
            pickup.itemRarity = rarity;
            pickup.quantity = qty;
            pickup.value = value;
            pickup.pickupRange = 2.5f;
        }

        static void CreateEnemy(string name, Vector3 pos, string id, string displayName,
            int maxHP, int attack, float speed, float detect, float patrol,
            string dropId, string dropName, int dropQty, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            Object.DestroyImmediate(go.GetComponent<Rigidbody>());

            var enemyType = System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");
            if (enemyType != null)
            {
                var comp = go.AddComponent(enemyType);
                enemyType.GetField("enemyId")?.SetValue(comp, id);
                enemyType.GetField("enemyName")?.SetValue(comp, displayName);
                enemyType.GetField("maxHP")?.SetValue(comp, maxHP);
                enemyType.GetField("attackPower")?.SetValue(comp, attack);
                enemyType.GetField("moveSpeed")?.SetValue(comp, speed);
                enemyType.GetField("detectRange")?.SetValue(comp, detect);
                enemyType.GetField("patrolRadius")?.SetValue(comp, patrol);
                enemyType.GetField("dropItemId")?.SetValue(comp, dropId);
                enemyType.GetField("dropItemName")?.SetValue(comp, dropName);
                enemyType.GetField("dropQuantity")?.SetValue(comp, dropQty);
            }

            var r = go.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = color; r.material = m; }
        }

        static void CreateHUD()
        {
            var hudGo = new GameObject("GameHUD");
            var hudType = System.Type.GetType("EarthOnline.UI.GameHUD, Assembly-CSharp");
            if (hudType != null) hudGo.AddComponent(hudType);

            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hudGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            hudGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Interaction hint
            var hintGo = new GameObject("InteractionHint"); hintGo.transform.SetParent(hudGo.transform);
            var hintText = hintGo.AddComponent<UnityEngine.UI.Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 18; hintText.alignment = TextAnchor.MiddleCenter; hintText.color = Color.white;
            var hr = hintGo.GetComponent<RectTransform>();
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.08f);
            hr.sizeDelta = new Vector2(400, 40); hr.anchoredPosition = Vector2.zero;

            // Dialogue bubble
            var dlgGo = new GameObject("DialogueBubble"); dlgGo.transform.SetParent(hudGo.transform);
            dlgGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.7f);
            var dr = dlgGo.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.75f);
            dr.sizeDelta = new Vector2(500, 100); dr.anchoredPosition = Vector2.zero;
            var dlgTextGo = new GameObject("DialogueText"); dlgTextGo.transform.SetParent(dlgGo.transform);
            var dlgText = dlgTextGo.AddComponent<UnityEngine.UI.Text>();
            dlgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dlgText.fontSize = 20; dlgText.alignment = TextAnchor.MiddleCenter; dlgText.color = Color.white;
            var dtr = dlgTextGo.GetComponent<RectTransform>();
            dtr.anchorMin = Vector2.zero; dtr.anchorMax = Vector2.one; dtr.sizeDelta = Vector2.zero;

            // Status
            var statusGo = new GameObject("StatusText"); statusGo.transform.SetParent(hudGo.transform);
            var statusText = statusGo.AddComponent<UnityEngine.UI.Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 16; statusText.alignment = TextAnchor.UpperRight; statusText.color = Color.white;
            var sr = statusGo.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = Vector2.one; sr.pivot = Vector2.one;
            sr.sizeDelta = new Vector2(300, 30); sr.anchoredPosition = new Vector2(-20, -20);

            if (hudType != null)
            {
                var hc = hudGo.GetComponent(hudType);
                hudType.GetField("interactionHint")?.SetValue(hc, hintGo);
                hudType.GetField("interactionText")?.SetValue(hc, hintText);
                hudType.GetField("dialogueBubble")?.SetValue(hc, dlgGo);
                hudType.GetField("dialogueText")?.SetValue(hc, dlgText);
                hudType.GetField("statusText")?.SetValue(hc, statusText);
            }
        }
    }
}
