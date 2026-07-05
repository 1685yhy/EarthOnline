using System.Collections.Generic;
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

            RegisterAllGifts();
            AutoActivateStarterGift();

            EventBus.Subscribe("OnItemAdded", OnItemPickedUp);

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
        }

        void OnDestroy()
        {
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
