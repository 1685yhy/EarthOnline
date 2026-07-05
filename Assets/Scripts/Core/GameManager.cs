using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.Gifts;

namespace EarthOnline
{
    /// <summary>
    /// GameManager —— 游戏总控。
    /// V0.1: 初始化框架、注册金手指、管理游戏状态。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("玩家设置")]
        public string playerName = "穿越者";
        public int startingCurrency = 100;

        private GameState _state = GameState.Init;
        public GameState CurrentState => _state;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _state = GameState.Init;
        }

        void Start()
        {
            EnsureComponent<GiftManager>();
            EnsureComponent<SaveManager>();
            EnsureComponent<InventoryManager>();
            EnsureComponent<TimeManager>();
            EnsureComponent<PlayerStats>();
            EnsureComponent<QuestManager>();
            EnsureComponent<EarthOnline.Combat.CombatSystem>();
            EnsureComponent<CraftingManager>();
            EnsureComponent<EnemyRespawner>();

            RegisterAllGifts();
            AutoActivateStarterGift();

            EventBus.Subscribe("OnItemAdded", OnItemPickedUp);
            EventBus.Subscribe("OnPlayerDeath", OnPlayerDied);
            EventBus.Subscribe("OnDayPassed", OnDayPassed_Save);

            _state = GameState.Playing;
            Debug.Log("========== [GameManager] EarthOnline V0.3 Ready ==========");
        }

        void OnItemPickedUp(Dictionary<string, object> data)
        {
            string itemId = data.ContainsKey("itemId") ? data["itemId"].ToString() : "";
            if (itemId == "item_ring_dark")
            {
                var giftMgr = GiftManager.Instance;
                if (giftMgr != null)
                {
                    var om = giftMgr.ActivateGift("gift_old_master_001");
                    if (om != null)
                        Debug.Log($"[GameManager] 『{om.GiftName}』已觉醒！一股古老的气息从黑铁戒指中涌出...");
                }
            }
            else if (itemId == "item_chaos_fragment")
            {
                var giftMgr = GiftManager.Instance;
                if (giftMgr != null)
                {
                    var db = giftMgr.ActivateGift("gift_divine_body_001");
                    if (db != null)
                    {
                        Debug.Log($"[GameManager] 混沌碎片融入体内...『{db.GiftName}』觉醒！");
                        Debug.Log($"[GameManager] ⚠️ 你感觉到虚空中有什么东西注意到了你...");
                    }
                }
            }
        }

        void EnsureComponent<T>() where T : Component
        {
            if (GetComponent<T>() == null)
                gameObject.AddComponent<T>();
        }

        void RegisterAllGifts()
        {
            var gm = GiftManager.Instance;
            if (gm == null) return;

            // 注册签到系统
            var signIn = new SignInSystem();
            signIn.Initialize(new Dictionary<string, object>
            {
                {"id", "gift_sign_in_001"},
                {"name", "签到系统"},
                {"type", "System"},
                {"rarity", "R"},
                {"storyOrigin", "来自未知文明的新手引导系统，为所有穿越者提供基本生存保障。但请注意：它的前7任宿主全部在第一个月死亡。"},
                {"storyMystery", "制造这个系统的文明发生了什么？为什么系统日志中有大量被删除的数据？第30天签到将揭示真相..."}
            });
            gm.RegisterTemplate(signIn);

            // 注册老爷爷
            var oldMaster = new OldMaster();
            oldMaster.Initialize(new Dictionary<string, object>
            {
                {"id", "gift_old_master_001"},
                {"name", "戒指老爷爷"},
                {"type", "Mentor"},
                {"rarity", "SR"},
                {"storyOrigin", "上古天元宗首席炼丹宗师，渡劫失败后残魂封印于黑铁戒指中。已等待有缘人千年。"},
                {"storyMystery", "追杀他的人是谁？天陨丹方究竟是什么？为什么这个丹方的最后一个字，在你的血液里？"}
            });
            gm.RegisterTemplate(oldMaster);

            // 注册混沌圣体
            var divineBody = new DivineBody();
            divineBody.Initialize(new Dictionary<string, object>
            {
                {"id", "gift_divine_body_001"},
                {"name", "混沌圣体"},
                {"type", "Body"},
                {"rarity", "SSR"},
                {"storyOrigin", "穿越时灵魂撕裂空间裂缝，混沌之力灌入体内。这是最罕见的天赋——但也是最危险的天赋。"},
                {"storyMystery", "混沌之力的源头是什么？为什么那个'影子'和你的面容一模一样？当威胁度达到10，会发生什么？"}
            });
            gm.RegisterTemplate(divineBody);

            Debug.Log("[GameManager] All gift templates registered (3).");
        }

        void AutoActivateStarterGift()
        {
            var gm = GiftManager.Instance;
            if (gm != null)
            {
                var gift = gm.ActivateGift("gift_sign_in_001");
                if (gift != null)
                {
                    Debug.Log($"[GameManager] Starter gift activated: {gift.GiftName}");
                }
            }
        }

        void Update()
        {
            if (_state != GameState.Playing) return;

            // ESC 释放鼠标
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }

            // 测试：按T键触发签到
            if (Input.GetKeyDown(KeyCode.T))
            {
                var gm = GiftManager.Instance;
                if (gm != null)
                {
                    var gifts = gm.GetActiveGifts();
                    foreach (var g in gifts)
                    {
                        g.UseAbility("sign_in");
                    }
                }
            }

            // 测试：按I键查看状态
            if (Input.GetKeyDown(KeyCode.I))
            {
                var gm = GiftManager.Instance;
                if (gm != null)
                {
                    foreach (var g in gm.GetActiveGifts())
                        g.UseAbility("get_status");
                }
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    var items = inv.GetAllItems();
                    Debug.Log($"[Inventory] 背包 {inv.Count}/{inv.maxSlots}: " +
                        string.Join(", ", items.ConvertAll(i => $"{i.name}x{i.quantity}")));
                }
            }

            // 按O键与老爷爷交谈
            if (Input.GetKeyDown(KeyCode.O))
            {
                var gm = GiftManager.Instance;
                if (gm != null && gm.HasGiftOfType("Mentor"))
                {
                    var gifts = gm.GetActiveGifts();
                    foreach (var g in gifts)
                    {
                        if (g.GiftType == "Mentor")
                            g.UseAbility("ask_advice");
                    }
                }
                else
                {
                    Debug.Log("[GameManager] 你还没有遇到任何导师。或许可以在村子里找找线索...");
                }
            }

            // 按P键查看背包
            if (Input.GetKeyDown(KeyCode.P))
            {
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    var items = inv.GetAllItems();
                    if (items.Count == 0)
                        Debug.Log("[Inventory] 背包空空如也。去探索世界捡些东西吧！");
                    else
                        Debug.Log($"[Inventory] 背包 ({inv.Count}/{inv.maxSlots}):\n" +
                            string.Join("\n", items.ConvertAll(i =>
                                $"  [{i.rarity}] {i.name} x{i.quantity} — {i.description}")));
                }
            }

            // 按C键制作
            if (Input.GetKeyDown(KeyCode.C))
            {
                var cm = CraftingManager.Instance;
                if (cm != null)
                {
                    var available = cm.GetAvailableRecipes();
                    var all = cm.GetAllRecipes();
                    Debug.Log($"[Craft] === 制作台 === 可用:{available.Count}/{all.Count}");
                    foreach (var r in all)
                    {
                        bool canCraft = available.Contains(r);
                        var ings = string.Join(", ", System.Linq.Enumerable.Select(
                            r.ingredients, kvp => $"{kvp.Key}:{kvp.Value}"));
                        Debug.Log($"  {(canCraft ? "✅" : "❌")} {r.resultItemName}[{r.resultRarity}] ← {ings}");
                    }
                    Debug.Log("[Craft] 按K键尝试制作第一个可用配方");
                }
            }

            // 按K键执行第一个可用制作
            if (Input.GetKeyDown(KeyCode.K))
            {
                var cm = CraftingManager.Instance;
                var recipes = cm?.GetAvailableRecipes();
                if (recipes != null && recipes.Count > 0)
                    cm.Craft(recipes[0].id);
                else
                    Debug.Log("[Craft] 没有可制作的配方。需要材料！");
            }

            // 按H使用回血物品
            if (Input.GetKeyDown(KeyCode.H))
            {
                var inv = InventoryManager.Instance;
                var stats = PlayerStats.Instance;
                if (inv != null && stats != null)
                {
                    if (inv.HasItem("item_heal_pill_001"))
                    {
                        inv.RemoveItem("item_heal_pill_001", 1);
                        stats.Heal(30);
                        Debug.Log("[Item] 使用回血丹！+30HP");
                        EarthOnline.Combat.FloatingDamage.Spawn(
                            GameObject.FindGameObjectWithTag("Player").transform.position,
                            "+30", Color.green, 1.2f);
                    }
                    else if (inv.HasItem("item_herb_001"))
                    {
                        inv.RemoveItem("item_herb_001", 1);
                        stats.Heal(10);
                        Debug.Log("[Item] 使用止血草！+10HP");
                    }
                    else
                        Debug.Log("[Item] 没有回复物品。找草药或制作回血丹。");
                }
            }

            // 数字键 1-4：快捷技能
            if (Input.GetKeyDown(KeyCode.Alpha1)) UseGiftAbility("sign_in");
            if (Input.GetKeyDown(KeyCode.Alpha2)) UseGiftAbility("ask_advice");
            if (Input.GetKeyDown(KeyCode.Alpha3)) UseGiftAbility("heal");
            if (Input.GetKeyDown(KeyCode.Alpha4)) UseGiftAbility("cultivate");
        }

        void OnPlayerDied(Dictionary<string, object> data)
        {
            Debug.Log("[GameManager] 💀 玩家死亡。3秒后在地图中央重生...");
            StartCoroutine(RespawnPlayer());
        }

        System.Collections.IEnumerator RespawnPlayer()
        {
            yield return new WaitForSeconds(3f);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(0, 1.5f, 0);
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = new Vector3(0, 1.5f, 0);
                if (cc != null) cc.enabled = true;
            }
            var stats = PlayerStats.Instance;
            if (stats != null) stats.Heal(stats.maxHP);
            Debug.Log("[GameManager] 🏥 已重生！HP全恢复。");
        }

        void OnDayPassed_Save(Dictionary<string, object> data)
        {
            // 每天自动存档
            var sm = SaveManager.Instance;
            if (sm != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                var stats = PlayerStats.Instance;
                var time = TimeManager.Instance;
                var saveData = new EarthOnline.Framework.SaveData
                {
                    playerName = playerName,
                    playerPosX = player != null ? player.transform.position.x : 0,
                    playerPosY = player != null ? player.transform.position.y : 0,
                    playerPosZ = player != null ? player.transform.position.z : 0,
                    playerLevel = stats != null ? stats.playerLevel : 1,
                    playerCurrency = stats != null ? stats.currency : 0,
                    gameDay = time != null ? time.GameDay : 1,
                    currentSceneName = "EarthOnline_Main"
                };
                sm.Save(saveData);
                Debug.Log($"[GameManager] 💾 自动存档 — 第{data["day"]}天");
            }
        }

        void UseGiftAbility(string abilityName)
        {
            var gm = GiftManager.Instance;
            if (gm == null) return;
            foreach (var g in gm.GetActiveGifts())
                g.UseAbility(abilityName);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnItemAdded", OnItemPickedUp);
            EventBus.Unsubscribe("OnPlayerDeath", OnPlayerDied);
            EventBus.Unsubscribe("OnDayPassed", OnDayPassed_Save);
            EventBus.Clear();
        }
    }

    public enum GameState
    {
        Init,
        MainMenu,
        Playing,
        Paused,
        Dialog,
        Loading
    }
}
