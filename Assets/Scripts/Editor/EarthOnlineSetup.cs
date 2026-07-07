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
                "NPC_Wang", "NPC_Li", "NPC_Chen", "NPC_Zhao", "VillageHouse"
            };
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == null) continue;
                string n = go.name;
                if (cleanNames.Contains(n) || n.StartsWith("Tree_") || n.StartsWith("Rock_")
                    || n.StartsWith("Pickup_") || n.StartsWith("House_") || n.StartsWith("Fence_")
                    || n.StartsWith("Enemy_") || n.StartsWith("Chest_") || n == "NPC_Chen" || n == "NPC_Zhao"
                    || n.StartsWith("Dungeon") || n.StartsWith("Flower_") || n.StartsWith("Travel_")
                    || n.StartsWith("Discovery_") || n.StartsWith("灵脉_") || n.StartsWith("Echo_"))
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

            CreateNPC("NPC_Li", new Vector3(3, 1.2f, -5), "npc_li_001", "李灵儿", "药铺掌柜", "最近采到的灵药越来越少了...", new Color(0.3f, 0.7f, 0.4f), false);
            CreateEnemy("Enemy_Phoenix", new Vector3(0, 5f, 25), "phoenix_001", "不死鸟", maxHP: 300, attack: 40, speed: 6f, detect: 30f, patrol: 20f, dropId: "item_phoenix_feather", dropName: "凤羽", dropQty: 2, color: new Color(1f, 0.5f, 0.1f));
            CreateEnemy("Enemy_Wyvern", new Vector3(-18, 3f, -20), "wyvern_001", "双足飞龙", maxHP: 180, attack: 30, speed: 4f, detect: 20f, patrol: 15f, dropId: "item_spirit_core_001", dropName: "灵气核心", dropQty: 5, color: new Color(0.4f, 0.1f, 0.4f));
            CreateEnemy("Enemy_Treant", new Vector3(15, 4f, 20), "treant_001", "古树精", maxHP: 250, attack: 35, speed: 0.5f, detect: 5f, patrol: 0f, dropId: "item_ancient_rune", dropName: "远古符文", dropQty: 1, color: new Color(0.2f, 0.5f, 0.1f));
            CreateEnemy("Enemy_Basilisk", new Vector3(18, 1.2f, 15), "basilisk_001", "蛇怪", 110, 24, 2.5f, 12f, 8f, "item_spirit_core_001", "灵气核心", 3, new Color(0.1f, 0.5f, 0.1f));
            CreateEnemy("Enemy_Lich", new Vector3(0, 1.5f, 22), "lich_001", "巫妖", 120, 28, 1f, 20f, 3f, "item_void_crystal", "虚空结晶", 2, new Color(0.05f, 0.05f, 0.1f));
            CreateEnemy("Enemy_Gargoyle", new Vector3(5, 2f, -20), "gargoyle_001", "石像鬼", 90, 20, 1.5f, 6f, 0f, "item_spirit_core_001", "灵气核心", 3, new Color(0.3f, 0.3f, 0.3f));
            CreateEnemy("Enemy_Elemental", new Vector3(22, 1.5f, -10), "elemental_001", "火元素", 80, 22, 2f, 8f, 4f, "item_spirit_core_001", "灵气核心", 3, new Color(1f, 0.4f, 0.1f));
            CreateEnemy("Enemy_IceWraith", new Vector3(-22, 1.5f, 10), "ice_001", "冰霜之灵", 65, 16, 2.5f, 10f, 8f, "item_spirit_jade", "灵玉", 1, new Color(0.1f, 0.6f, 1f));
            CreateEnemy("Enemy_Drake", new Vector3(20, 2f, 0), "drake_001", "幼龙", 200, 25, 3f, 15f, 12f, "item_spirit_core_001", "灵气核心", 4, new Color(0.8f, 0.3f, 0.05f));
            CreateEnemy("Enemy_Cultist", new Vector3(-20, 1f, -18), "cultist_001", "虚空信徒", 70, 18, 2f, 12f, 6f, "item_void_crystal", "虚空结晶", 1, new Color(0.3f, 0.05f, 0.3f));
            CreateEnemy("Enemy_Scorpion", new Vector3(16, 1f, -12), "scorp_001", "巨蝎", 55, 14, 2f, 5f, 4f, "item_pill_001", "聚气丹", 2, new Color(0.6f, 0.2f, 0.05f));
            CreateEnemy("Enemy_Ghoul", new Vector3(-16, 1f, 16), "ghoul_001", "食尸鬼", 40, 12, 3f, 10f, 8f, "item_herb_001", "止血草", 2, new Color(0.1f, 0.3f, 0.1f));
            CreateEnemy("Enemy_Shadow", new Vector3(2, 1f, 20), "shadow_001", "魅影", 25, 8, 6f, 8f, 15f, "item_spirit_stone", "灵石碎片", 2, new Color(0.05f, 0.05f, 0.05f));
            CreateEnemy("Enemy_Wraith", new Vector3(0, 1.5f, 18), "wraith_001", "虚空游魂", 60, 15, 2f, 20f, 10f, "item_spirit_core_001", "灵气核心", 2, new Color(0.1f, 0.1f, 0.3f));
            CreateNPC("NPC_Mystic", new Vector3(-10, 1.2f, 18), "npc_mystic_001", "云游道人", "神秘道人", "贫道云游四方——见过虚空吞噬的世界不止这一个。你身上有地球意志的印记。它选中了你——你知道代价是什么吗？", new Color(0.6f, 0.6f, 0.9f), true);
            CreateNPC("NPC_Guard", new Vector3(0, 1.2f, -22), "npc_guard_001", "守卫队长", "守卫", "没人能越过这条线。虚空行者之外的东西——不是你能对抗的。等你足够强了再来。", new Color(0.3f, 0.3f, 0.5f), true);

            CreateNPC("NPC_Liu", new Vector3(-8, 1.2f, -12), "npc_liu_001", "刘猎户", "猎人", "北山的野兽越来越多了——不像是自然迁徙，像是被什么东西赶出来的。你要去北边的话小心点。", new Color(0.5f, 0.4f, 0.2f), true);
            CreateNPC("NPC_Miner", new Vector3(-15, 1.2f, -8), "npc_miner_001", "老矿工", "矿工", "我挖了二十年矿——最近挖出来的灵石都是黑的。像被什么东西污染了。工头说是自然现象。狗屁。", new Color(0.4f, 0.3f, 0.3f), true);
            CreateNPC("NPC_Apprentice", new Vector3(3, 1.2f, 12), "npc_apprentice_001", "小药童", "药童", "李师父教我认药已经三年了！我马上就能自己开炉炼丹了——不过上次差点把药铺烧了...", new Color(0.3f, 0.8f, 0.3f), false);

            CreateEnemy("Enemy_Slime", new Vector3(10, 1f, 10), "slime_001", "灵气史莱姆", 10, 2, 1f, 3f, 3f, "item_herb_001", "止血草", 2, new Color(0.2f, 0.8f, 0.3f));
            CreateEnemy("Enemy_Harpy", new Vector3(-10, 2f, -16), "harpy_001", "鹰身女妖", 35, 10, 5f, 15f, 20f, "item_spirit_stone", "灵石碎片", 3, new Color(0.7f, 0.7f, 0.2f));
            CreateEnemy("Enemy_Imp", new Vector3(-5, 1f, 15), "imp_001", "小恶魔", 15, 3, 5f, 4f, 8f, "item_spirit_stone", "灵石碎片", 1, new Color(0.8f, 0.1f, 0.1f));
            CreateEnemy("Enemy_Spider", new Vector3(12, 1, -15), "spider_001", "毒蛛", 20, 5, 3f, 6f, 6f, "item_herb_001", "止血草", 1, new Color(0.2f, 0.1f, 0.2f));
            CreateEnemy("Enemy_Golem", new Vector3(-18, 2, -2), "golem_001", "石魔像", 150, 20, 1f, 4f, 2f, "item_spirit_core_001", "灵气核心", 2, new Color(0.4f, 0.35f, 0.3f));

            CreateNPC("NPC_Chen", new Vector3(-3, 1.2f, -8),
                "npc_chen_001", "陈半仙", "流浪商人",
                "走过路过不要错过！从东土大唐到西域荒漠，我陈半仙什么好东西没见过？",
                new Color(0.9f, 0.7f, 0.1f), true);

            CreateNPC("NPC_Zhao", new Vector3(-2, 1.2f, -2),
                "npc_zhao_001", "赵掌柜", "云来客栈老板",
                "客官里边请！住宿50灵石一晚，免费送早餐。想打听消息？那得看你请我喝什么酒了。",
                new Color(0.7f, 0.3f, 0.1f), true);

            // ====== VILLAGE BUILDINGS ======
            CreateBuilding("House_Blacksmith", new Vector3(-6, 0, 7), new Vector3(3, 2, 3), new Color(0.4f, 0.3f, 0.2f));
            CreateBuilding("House_Herb", new Vector3(5, 0, -7), new Vector3(3, 1.5f, 3), new Color(0.3f, 0.5f, 0.3f));
            CreateBuilding("House_Elder", new Vector3(8, 0, 5), new Vector3(2, 1.8f, 2), new Color(0.5f, 0.4f, 0.3f));
            CreateBuilding("House_Inn", new Vector3(-2, 0, -1), new Vector3(4, 2.5f, 4), new Color(0.7f, 0.5f, 0.2f));

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
            CreatePickup("Pickup_Sword", new Vector3(6, 0.5f, -3),
                "item_iron_sword", "铁剑", "Weapon", "R", 1, 80);
            CreatePickup("Pickup_Armor", new Vector3(-6, 0.5f, 2),
                "item_leather_armor", "皮甲", "Armor", "R", 1, 60);
            CreatePickup("Pickup_Elixir", new Vector3(-12, 0.5f, 8),
                "item_cultivation_elixir", "修炼灵液", "Consumable", "SR", 1, 100);
            CreatePickup("Pickup_Scroll", new Vector3(10, 0.5f, -12),
                "item_skill_scroll", "残缺功法", "Skill", "SR", 1, 150);
            CreatePickup("Pickup_Jade", new Vector3(15, 0.5f, -2),
                "item_spirit_jade", "灵玉", "Material", "SR", 1, 200);
            CreatePickup("Pickup_Ginseng", new Vector3(-15, 0.5f, -4),
                "item_ginseng_1000yr", "千年灵芝", "Consumable", "SSR", 1, 500);
            CreatePickup("Pickup_Scripture", new Vector3(0, 0.5f, 16),
                "item_ancient_scripture", "上古残卷", "Skill", "SSR", 1, 800);

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

            // ====== TREASURE CHESTS ======
            var chest1 = new GameObject("Chest_Forest"); chest1.transform.position = new Vector3(-10, 0.5f, -5);
            chest1.AddComponent<EarthOnline.TreasureChest>();
            var chest2 = new GameObject("Chest_Ruins"); chest2.transform.position = new Vector3(10, 0.5f, 7);
            chest2.AddComponent<EarthOnline.TreasureChest>();
            var chest3 = new GameObject("Chest_Cave"); chest3.transform.position = new Vector3(0, 0.5f, -12);
            chest3.AddComponent<EarthOnline.TreasureChest>();

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

            // New V2.0 enemies
            CreateEnemy("Enemy_Serpent", new Vector3(14, 1, 5), "serpent_001", "灵蛇",
                maxHP: 30, attack: 8, speed: 4f, detect: 8f, patrol: 10f,
                dropId: "item_herb_001", dropName: "止血草", dropQty: 3,
                color: new Color(0.1f, 0.6f, 0.3f));

            CreateEnemy("Enemy_Ghost", new Vector3(-14, 1.5f, -10), "ghost_001", "怨灵",
                maxHP: 25, attack: 12, speed: 3f, detect: 12f, patrol: 5f,
                dropId: "item_spirit_stone", dropName: "灵石碎片", dropQty: 2,
                color: new Color(0.4f, 0.4f, 0.7f));

            CreateEnemy("Enemy_Bandit", new Vector3(8, 1.2f, -14), "bandit_001", "山贼",
                maxHP: 45, attack: 10, speed: 2.5f, detect: 10f, patrol: 12f,
                dropId: "item_herb_001", dropName: "止血草", dropQty: 2,
                color: new Color(0.3f, 0.2f, 0.1f));

            // Boss enemy
            CreateEnemy("Enemy_Boss", new Vector3(0, 2f, -15), "boss_001", "虚空行者",
                maxHP: 300, attack: 30, speed: 3f, detect: 12f, patrol: 3f,
                dropId: "item_spirit_core_001", dropName: "灵气核心", dropQty: 3,
                color: new Color(0.6f, 0.1f, 0.5f));

            // Dungeon entrance
            // Decorative village items
            for (int i = -4; i <= 4; i++)
            {
            CreatePickup("Pickup_Core", new Vector3(0, 0.5f, -25), "item_void_heart", "虚空之心", "Quest", "SSR", 1, 3000);
                var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            CreatePickup("Pickup_Rune", new Vector3(-23, 0.5f, -15), "item_ancient_rune", "远古符文", "Material", "SSR", 1, 1500);

            CreatePickup("Pickup_Amber", new Vector3(-12, 0.5f, 14), "item_amber_fossil", "琥珀化石", "Material", "SR", 1, 200);
                flower.name = $"Flower_{i}"; flower.transform.position = new Vector3(i * 1.5f, 0.05f, 0.5f);
            CreatePickup("Pickup_Orb", new Vector3(-20, 0.5f, 20), "item_mana_orb", "灵能宝珠", "Material", "SSR", 1, 1200);
            CreatePickup("Pickup_Seed", new Vector3(24, 0.5f, -8), "item_world_seed", "世界树种", "Quest", "SSR", 1, 2000);

                flower.transform.localScale = Vector3.one * 0.15f;
                var fr = flower.GetComponent<Renderer>();
                if (fr != null) { var fm = new Material(Shader.Find("Standard")); fm.color = Random.value > 0.5f ? new Color(1,0.2f,0.2f) : new Color(1,1,0.2f); fr.material = fm; }
            CreatePickup("Pickup_Feather", new Vector3(22, 0.5f, 3), "item_phoenix_feather", "凤羽", "Material", "SSR", 1, 800);

                flower.GetComponent<Collider>().isTrigger = true;
            }

            // Fast travel points
            var tp1 = new GameObject("Travel_Village"); tp1.transform.position = new Vector3(0, 0.5f, 0);
            var ft1 = tp1.AddComponent<EarthOnline.FastTravel>(); ft1.pointName = "村庄中心"; ft1.pointId = "village_center";
            var tp2 = new GameObject("Travel_Dungeon"); tp2.transform.position = new Vector3(0, 0.5f, -18);
            var forestSign = new GameObject("Zone_Forest"); forestSign.transform.position = new Vector3(25, 0.5f, 0); forestSign.AddComponent<EarthOnline.HiddenDiscovery>().discoveryName="妖兽森林"; forestSign.GetComponent<EarthOnline.HiddenDiscovery>().discoveryText="你踏入了妖兽森林。树木遮天蔽日——空气中弥漫着野兽的气味。这里的灵脉更浓——但危险也更大。";
            var mineSign = new GameObject("Zone_Mine"); mineSign.transform.position = new Vector3(-25, 0.5f, -5); mineSign.AddComponent<EarthOnline.HiddenDiscovery>().discoveryName="北部矿脉"; mineSign.GetComponent<EarthOnline.HiddenDiscovery>().discoveryText="北部矿脉——天元宗和青云门的争夺之地。地面上散落着碎裂的灵石和干涸的血迹。不是所有的血都是妖兽的。";

            var ft2 = tp2.AddComponent<EarthOnline.FastTravel>(); ft2.pointName = "虚空裂缝入口"; ft2.pointId = "dungeon_entrance";
            var tp3 = new GameObject("Travel_Forest"); tp3.transform.position = new Vector3(15, 0.5f, 0);
            var ft3 = tp3.AddComponent<EarthOnline.FastTravel>(); ft3.pointName = "东边森林"; ft3.pointId = "east_forest";
            CreatePickup("Pickup_Crystal", new Vector3(20, 0.5f, -5), "item_void_crystal", "虚空结晶", "Material", "SSR", 1, 1000);


            var dungeon = new GameObject("DungeonEntrance");
            dungeon.transform.position = new Vector3(0, 0, -15);
            dungeon.AddComponent<EarthOnline.DungeonEntrance>();
            var dc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); dc.name = "DungeonVisual";
            dc.transform.SetParent(dungeon.transform);
            dc.transform.localPosition = Vector3.zero;
            CreatePickup("Pickup_Pearl", new Vector3(18, 0.5f, 8), "item_spirit_pearl", "灵珠", "Material", "SR", 1, 300);
            CreatePickup("Pickup_Talisman", new Vector3(-18, 0.5f, -2), "item_talisman_protection", "护身玉符", "Accessory", "SR", 1, 250);
            CreatePickup("Pickup_Map", new Vector3(7, 0.5f, 14), "item_treasure_map", "藏宝图碎片", "Quest", "SR", 1, 100);

            dc.transform.localScale = new Vector3(3, 0.3f, 3);
            var dr = dc.GetComponent<Renderer>();
            if (dr != null) { var dm = new Material(Shader.Find("Standard")); dm.color = new Color(0.1f, 0.05f, 0.1f); dr.material = dm; }
            var dc2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder); dc2.name = "DungeonPortal";
            dc2.transform.SetParent(dungeon.transform);
            dc2.transform.localPosition = Vector3.up * 0.5f;
            dc2.transform.localScale = new Vector3(2, 0.2f, 2);
            var dr2 = dc2.GetComponent<Renderer>();
            if (dr2 != null) { var dm2 = new Material(Shader.Find("Standard")); dm2.color = new Color(0.5f, 0f, 0.8f); dm2.EnableKeyword("_EMISSION"); dm2.SetColor("_EmissionColor", new Color(0.5f, 0f, 0.8f) * 0.5f); dr2.material = dm2; }

            // ====== WORLD ECHOES ======
            CreateEcho("Echo_Civilization", new Vector3(-12, 0.5f, -12),
                "echo_civ_001", "文明终结之日",
                "你看到一个人——他坐在一块石头上，面前漂浮着一个由光构成的球体。那是签到系统。他输入了最后一行代码-然后虚空从地面裂缝涌出，吞没了他。三百年前。这个人来自一个你不认识的文明——但你知道他的恐惧。",
                "gift_sign_in_001", "npc_zhang_001");

            var fish1 = new GameObject("Fishing_River"); fish1.transform.position = new Vector3(8, 0.5f, -16); fish1.AddComponent<EarthOnline.FishingSpot>();
            var fish2 = new GameObject("Fishing_Lake"); fish2.transform.position = new Vector3(-14, 0.5f, 10); fish2.AddComponent<EarthOnline.FishingSpot>();
            CreateEcho("Echo_Alchemist", new Vector3(8, 0.5f, 12),
                "echo_alchemy_001", "药王谷·最后的炉火",
                "一个白发老人在丹炉前跪着...",
                "gift_alchemy_001", "npc_li_001");

            CreateEcho("Echo_Ring", new Vector3(-3, 0.5f, -6),
                "echo_ring_001", "黑铁戒指·铸造之日",
                "一枚黑铁戒指躺在锻造台上。一只苍老的手把它捡起来——那是年轻时的张老。他在戒指上刻了一个名字。名字被虚空腐蚀了，看不清。但这枚戒指是他为一个人铸的。一个他等了三十年、还在等的人。",
                "gift_old_master_001", "npc_zhang_001");

            CreateEcho("Echo_Bloodline", new Vector3(14, 0.5f, 2),
                "echo_blood_001", "猎杀者·升仙丹",
                "你看到一个黑衣人蹲在一具尸体旁。尸体的脖子上有两个小孔——血被抽干了。黑衣人把血装进一个玉瓶里。瓶子上刻着天元宗的标记。这不是谋杀——这是'收割'。每月一次。已经持续了五十年。",
                "gift_bloodline_001", "npc_li_001");

            CreateEcho("Echo_Sword", new Vector3(-14, 0.5f, 12),
                "echo_sword_001", "剑仙·最后一剑",
                "一把剑插在地上。周围是数百把断裂的剑。一个白衣人站在剑前——他的手在发抖。'剑道的极致...是斩断一切。包括自己在意的东西。我不想走了。'他拔出剑——不是向着敌人，而是插入了自己的胸口。他把自己变成了剑——永远守在这里。",
                "gift_sword_001", null);

            CreateEcho("Echo_Formation", new Vector3(-16, 0.5f, -8),
                "echo_formation_001", "阵法师·不死之人",
                "一个黑衣人蹲在地上用手指画着阵法。他没有用灵石——他用的是自己的血。他的手指已经磨到见骨了，但他还在画。'再来一次...这次一定能成功...'他画了多久了？地上的阵法痕迹层层叠叠——最下面的一层已经是三百年前的了。",
                "gift_formation_001", null);

            CreateEcho("Echo_Time", new Vector3(10, 0.5f, 14),
                "echo_time_001", "时光·被困住的灵魂",
                "你看到一个人站在同一天的日落里。他的嘴唇在动——但声音来自很远的地方。'第109500天。还是同一天。她死在第109499天的日落——我只要再往前回溯一天。就一天。但他不让。'谁不让？你看到一个影子站在他身后——和他长得一样。那个影子在微笑。",
                "gift_time_001", null);

            CreateEcho("Echo_Dream", new Vector3(-2, 0.5f, -16),
                "echo_dream_001", "梦境·全大陆的噩梦",
                "所有人的梦都汇聚在这里。你看到成千上万张脸——都是灵气大陆的居民。他们在做同一个梦：虚空裂缝在他们面前裂开，一只手从里面伸出来。那只手——是你的手。不是比喻——就是你的手。你在虚空里。你在往外爬。",
                "gift_dream_001", null);

            CreateEcho("Echo_Divination", new Vector3(16, 0.5f, -8),
                "echo_divination_001", "命运·血红色丝线",
                "你看到无数条命运丝线——每一条都连接着一个穿越者。大多数是灰色的——他们已经死了。有几条是金色的——还在发光。有一条特别亮——那是你的。但你的丝线上缠着一根黑色的线。那根线来自虚空。它一直在。从你穿越的第一天起——就在你的命运里。",
                "gift_divination_001", null);

            CreateEcho("Echo_Devour", new Vector3(-10, 0.5f, -14),
                "echo_devour_001", "吞噬·最后的人性",
                "你看到一个人跪在地上。他的眼睛已经变成了纯黑色——但他在哭。黑色的眼泪。'我不想...我不想变成那样...'他身边是一只妖兽的尸体——已经被吞噬了一半。他的手不受控制地伸向尸体。他还在哭——但他的手在笑。",
                "gift_devour_001", null);

            CreateEcho("Echo_Wealth", new Vector3(4, 0.5f, -14),
                "echo_wealth_001", "财富·026号世界末日",
                "你看到了一座城市——和灵气大陆完全不同的城市。摩天大楼、霓虹灯、飞行汽车。然后一切都碎了。不是战争——是人们停止了交易。货币变成了废纸。食物变成了货币。邻居变成了敌人。最后一个活着的人是一个孩子——他抱着一个存钱罐。'妈妈说...存钱就能活下去...'",
                "gift_wealth_001", null);

            // ====== SPIRIT VEINS ======
            CreateSpiritVein("灵脉_村庄", new Vector3(0, 0.1f, 2), "小型灵脉", 1.5f, 3f);
            CreateSpiritVein("灵脉_森林", new Vector3(-10, 0.1f, -3), "森林灵脉", 1.8f, 4f);
            CreateSpiritVein("灵脉_裂缝", new Vector3(0, 0.1f, -20), "虚空边缘灵脉", 2.5f, 5f);
            CreateSpiritVein("灵脉_南", new Vector3(8, 0.1f, -10), "南部灵脉", 1.3f, 2f);
            CreateSpiritVein("灵脉_西", new Vector3(-15, 0.1f, 0), "西部灵脉", 1.6f, 3f);
            CreateSpiritVein("灵脉_东", new Vector3(18, 0.1f, 3), "东部灵脉", 1.4f, 2f);
            CreateSpiritVein("灵脉_山洞", new Vector3(-12, 0.1f, 8), "山洞灵脉", 2.0f, 4f);

            // ====== HIDDEN DISCOVERIES ======
            CreateDiscovery("Discovery_Cave", new Vector3(-15, 0.5f, -15),
                "disc_cave_001", "隐士洞府", "一个被藤蔓遮蔽的洞府。里面有一具坐化的骷髅——手边放着一卷未写完的功法。最后一页的字迹越来越潦草，最后一行是：'他们来了...如果有人在看这个...快走...'",
                "", "", 0, 80);

            CreateDiscovery("Discovery_Tree", new Vector3(15, 1, 15),
                "disc_tree_001", "万年古树", "这棵树的树干上刻满了名字。最早的已经模糊不清——那是三千年前的文字。最近的名字还在：'李太白到此一游'。旁边有人用小字补了一句：'太白已飞升，留剑影于此树。有缘者可见。'",
                "", "", 0, 50);

            CreateDiscovery("Discovery_Stone", new Vector3(-5, 0.5f, -12),
                "disc_stone_001", "界碑残片", "一块断裂的石碑。上面刻着'灵气大陆·北域·天元——'后面的字被毁掉了。碑座的另一面刻着不同的文字——不是人族的语言。",
                "item_spirit_stone", "灵石碎片", 5, 30);

            CreateDiscovery("Discovery_Altar", new Vector3(12, 0.5f, -10),
                "disc_altar_001", "古老祭坛", "一个被藤蔓覆盖的石制祭坛。上面有已经干涸的血迹——黑色的，不像是人血。祭坛的中心有一个凹槽——恰好能放下一枚黑铁戒指。",
                "item_spirit_core_001", "灵气核心", 1, 100);

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
            go.AddComponent<EarthOnline.NPC.NPCActivity>();
            go.AddComponent<EarthOnline.NPC.NPCMemory>();
            go.AddComponent<EarthOnline.NPC.NPCNaturalSchedule>();
            go.AddComponent<EarthOnline.NPC.NPCNetwork>();
            go.AddComponent<EarthOnline.NPC.DialogueTree>();
            go.AddComponent<EarthOnline.NPC.NPCBond>();
            // Set work lines based on NPC
            var act = go.GetComponent<EarthOnline.NPC.NPCActivity>();
            act.workLines = displayName switch {
                "张老" => new[]{"这炉丹药还差三味...", "年轻人应该多出去走走。", "虚空...不会放过任何人的。"},
                "王铁柱" => new[]{"这把剑的钢口还差一锤...", "好铁！好铁！", "当年我在炼器阁...算了不说也罢。"},
                "李灵儿" => new[]{"这株药草的药性不对...", "凡是药三分毒。", "爹...我今天又梦到你了。"},
                "陈半仙" => new[]{"走过路过不要错过！", "这件东西...说实话我也不确定是什么。", "古墓快出现了...得准备准备了。"},
                "赵掌柜" => new[]{"客官里边请！", "我在这开了三十年店——什么人没见过。", "你是第47个。小心点。"},
                _ => new[]{"..."}
            };
            AddNPCSecrets(go, displayName);

            // Add schedule based on NPC role
            var schedule = go.AddComponent<EarthOnline.NPC.NPCSchedule>();
            if (title.Contains("商人"))
            {
                schedule.schedule = new EarthOnline.NPC.NPCSchedule.TimeSlot[] {
                    new() { startHour=6, endHour=18, position=pos, activity="在摊位卖货" },
                    new() { startHour=18, endHour=6, position=pos+new Vector3(2,0,2), activity="在篝火旁休息" },
                };
            }

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

        static void AddNPCSecrets(GameObject go, string name)
        {
            var sec = go.GetComponent<EarthOnline.NPC.NPCSecret>();
            if (sec == null) return;

            var secrets = new System.Collections.Generic.List<EarthOnline.NPC.NPCSecret.Secret>();

            switch (name)
            {
                case "张老":
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 3, hint = "张老说话时总是下意识地摸左手...那里以前应该戴过戒指。", revelation = "你问他手上的印记。张老沉默了很久：'那是我妻子留下的。她死在了一个虚空裂缝里...三十年了，我每天都在想怎么去那里找她。'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 8, hint = "张老提到'天元宗'时的眼神不对——那不是敬仰，是仇恨。", revelation = "'老夫曾是天元宗内门弟子。三十年前，宗门发现了虚空裂缝的秘密——可以从中提取力量。我妻子反对，第二天她就'意外'掉进裂缝了。'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 15, hint = "张老的书房里藏着一卷泛黄的手稿，上面的字迹不是他的。", revelation = "'这是虚空裂缝的地图。我花了三十年画的。每一条裂缝都在变化——但它们都指向同一个地方。虚空里...有人。或者说，有什么东西。我妻子没有死。她被关在里面。'" });
                    break;

                case "王铁柱":
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 3, hint = "王铁柱打铁的手法不像是普通铁匠——每一锤都有灵力波动。", revelation = "'你发现了？我以前不是打铁的。我是...铸剑师。给修士铸剑的。后来因为一把剑，我被赶出了炼器阁。'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 8, hint = "铁匠铺后院有一把用布包着的剑，从来不给任何人看。", revelation = "'这把剑...(他掀开布)是我铸的最后一把灵剑。它杀过人。不是我杀的。但用剑的人——是我弟弟。他拿着这把剑杀了天元宗的一个长老。他们现在还在找他。也在找铸剑的人。'" });
                    break;

                case "李灵儿":
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 3, hint = "李灵儿开的药方...有几味药的用法完全不对。除非——不是在治人。", revelation = "'你懂药理？那我不瞒你了。我不是在治病——我是在研制一种毒。专门针对...修真者的毒。'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 8, hint = "李灵儿提到她父亲时用的是'被天元宗的人害死的'——但她说的是'人'，不是'弟子'或'长老'。", revelation = "'我爹是天元宗的副宗主。他发现了宗主在用人血炼丹。第二天他就'走火入魔'了。一身的修为，一夜之间全废了。他们没杀他——他们让他活着，变成一个废人，让所有人都看到：这就是反对宗主的下场。'" });
                    break;

                case "赵掌柜":
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 3, hint = "赵掌柜记性特别好——他能记住每一个来过客栈的人。", revelation = "'我在这里开了三十年客栈。见过的人比天元宗长老见过的还多。你想知道什么？'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 8, hint = "赵掌柜有一个从不打开的账本——封面上写着一个'虚'字。", revelation = "'这个账本...记的不是灵石流水。是每一个从虚空裂缝里出来的人。三十年了——你是第47个。前46个...后来都消失了。有的被宗门带走了。有的被杀了。有的一觉醒来就不在了。你小心点。'" });
                    break;

                case "陈半仙":
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 3, hint = "陈半仙自称'走过半个大陆'——但他卖的永远只有那几样东西。他的货不是走商收的，是...", revelation = "'你以为我是流浪商人？哈！我是倒斗的——专门挖古修士的墓。这些东西都是从墓里挖出来的。'" });
                    secrets.Add(new EarthOnline.NPC.NPCSecret.Secret { revealThreshold = 8, hint = "陈半仙有时候会盯着某个方向发呆很久，嘴里念叨着'快了快了'。", revelation = "'我在找一个墓。不是普通的墓——是一座活着的墓。它每隔一百年出现一次，每次在不同的地方。下次出现是三个月后。里面的东西...随便拿出一件就能让整个大陆的修士疯狂。你想不想一起去？'" });
                    break;
            }

            sec.secrets = secrets.ToArray();
        }

        static Font GetChineseFont()
        {
            var f = Font.CreateDynamicFontFromOSFont("SimHei", 14);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        static void CreateEcho(string name, Vector3 pos, string id, string title, string text,
            string giftId, string npcId)
        {
            var go = new GameObject(name); go.transform.position = pos;
            var echo = go.AddComponent<EarthOnline.WorldEcho>();
            echo.echoId = id; echo.echoTitle = title; echo.echoText = text;
            echo.connectedGiftId = giftId; echo.connectedNpcId = npcId;
        }

        static void CreateSpiritVein(string name, Vector3 pos, string veinName, float mult, float regen)
        {
            var go = new GameObject(name); go.transform.position = pos;
            var sv = go.AddComponent<EarthOnline.SpiritVein>();
            sv.veinName = veinName; sv.cultivationMultiplier = mult; sv.spiritRegenBonus = regen;
            var challenge = go.AddComponent<EarthOnline.SpiritVeinChallenge>();
            challenge.dailyCost = Mathf.RoundToInt(mult * 15); // 倍率越高持有成本越高
        }

        static void CreateDiscovery(string name, Vector3 pos, string id, string title, string text,
            string itemId, string itemName, int qty, int cultivation)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var d = go.AddComponent<EarthOnline.HiddenDiscovery>();
            d.discoveryId = id; d.discoveryName = title; d.discoveryText = text;
            d.rewardItemId = itemId; d.rewardItemName = itemName;
            d.rewardQuantity = qty; d.rewardCultivation = cultivation;
            d.triggerRange = 3f;
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
            hintText.font = GetChineseFont();
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
            dlgText.font = GetChineseFont();
            dlgText.fontSize = 20; dlgText.alignment = TextAnchor.MiddleCenter; dlgText.color = Color.white;
            var dtr = dlgTextGo.GetComponent<RectTransform>();
            dtr.anchorMin = Vector2.zero; dtr.anchorMax = Vector2.one; dtr.sizeDelta = Vector2.zero;

            // Status
            var statusGo = new GameObject("StatusText"); statusGo.transform.SetParent(hudGo.transform);
            var statusText = statusGo.AddComponent<UnityEngine.UI.Text>();
            statusText.font = GetChineseFont();
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
