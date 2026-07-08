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
        public int startingSpiritStone = 100;

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
            EnsureComponent<TutorialManager>();
            EnsureComponent<EquipmentManager>();
            EnsureComponent<EarthOnline.UI.PauseMenu>();
            EnsureComponent<ShopManager>();
            EnsureComponent<WeatherSystem>();
            EnsureComponent<AchievementManager>();
            EnsureComponent<RandomEvents>();
            EnsureComponent<OpeningSequence>();
            EnsureComponent<EarthOnline.Combat.BuffManager>();
            EnsureComponent<EarthOnline.Combat.CombatFeedback>();
            EnsureComponent<EarthOnline.Combat.SkillComboSystem>();
            EnsureComponent<TitleManager>();

            RegisterAllGifts();
            AutoActivateStarterGift();

            EventBus.Subscribe("OnItemAdded", OnItemPickedUp);
            EventBus.Subscribe("OnPlayerDeath", OnPlayerDied);
            EventBus.Subscribe("OnDayPassed", OnDayPassed_Save);

            // V2.0: 随机出身
            EnsureComponent<OriginManager>();
            EnsureComponent<CultivationManager>();
            EnsureComponent<RumorSystem>();
            EnsureComponent<CrimeSystem>();
            EnsureComponent<ReputationSystem>();
            EnsureComponent<AntagonistSystem>();
            EnsureComponent<VillainStoryline>();
            EnsureComponent<FactionSystem>();
            EnsureComponent<GossipSystem>();
            EnsureComponent<MarketSystem>();
            EnsureComponent<TerritorySystem>();
            EnsureComponent<WitnessSystem>();
            EnsureComponent<AudioManager>();
            EnsureComponent<EarthOnline.UI.UIManager>();
            var (origin, cfg) = OriginManager.RollOrigin();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) OriginManager.ApplyOrigin(origin, player);

            _state = GameState.Playing;
            Debug.Log($"========== [GameManager] 🌍 地球Online V2.0 | {cfg.name} | {cfg.startRealm} ==========");
        }

        void OnItemPickedUp(Dictionary<string, object> data)
        {
            string itemId = data.ContainsKey("itemId") ? data["itemId"].ToString() : "";
            string itemType = data.ContainsKey("itemType") ? data["itemType"].ToString() : "";

            if (itemId == "item_ring_dark")
            {
                var giftMgr = GiftManager.Instance;
                if (giftMgr != null)
                {
                    var om = giftMgr.ActivateGift("gift_old_master_001");
                    if (om != null)
                        Debug.Log($"[GameManager] 『{om.GiftName}』已觉醒！");
                }
            }
            else if (itemId == "item_chaos_fragment")
            {
                var giftMgr = GiftManager.Instance;
                if (giftMgr != null)
                {
                    var db = giftMgr.ActivateGift("gift_divine_body_001");
                    if (db != null)
                        Debug.Log($"[GameManager] 『{db.GiftName}』觉醒！虚空中有东西注意到了你...");
                }
            }

            // 自动装备武器/防具
            if (itemType == "Weapon" || itemType == "Armor" || itemType == "Accessory")
            {
                var eq = EquipmentManager.Instance;
                var inv = InventoryManager.Instance;
                if (eq != null && inv != null)
                {
                    var item = inv.GetItem(itemId);
                    if (item != null)
                    {
                        inv.RemoveItem(itemId, 1);
                        eq.Equip(item);
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

            // 神豪系统
            var wealth = new WealthSystem();
            wealth.Initialize(new Dictionary<string, object> {
                {"id", "gift_wealth_001"}, {"name", "神豪系统"}, {"type", "System"}, {"rarity", "SR"},
                {"storyOrigin", "来自026号平行世界——那个世界因为贫富差距引发世界大战而自我毁灭。本系统是那个世界最后的遗物。"},
                {"storyMystery", "026号世界毁灭的真正原因是什么？系统里隐藏的'求救信号'——是谁发的？他/她还活着吗？"}
            });
            gm.RegisterTemplate(wealth);

            // 青龙血脉
            var bloodline = new BloodlineAwakening();
            bloodline.Initialize(new Dictionary<string, object> {
                {"id", "gift_bloodline_001"}, {"name", "青龙血脉"}, {"type", "Bloodline"}, {"rarity", "SSR"},
                {"storyOrigin", "上古青龙的后裔。血脉越觉醒越不像人——但力量也越强。这片大陆上有人在猎杀血脉觉醒者，用他们的血炼制'升仙丹'。"},
                {"storyMystery", "猎杀者是谁？猎杀者总部为什么在天元宗——这个'正道第一宗门'？宗主的真实身份是什么？"}
            });
            gm.RegisterTemplate(bloodline);

            // 天机术
            var divination = new HeavenlyDivination();
            divination.Initialize(new Dictionary<string, object> {
                {"id", "gift_divination_001"}, {"name", "天机术"}, {"type", "Knowledge"}, {"rarity", "R"},
                {"storyOrigin", "窥探命运者必遭天谴。但穿越者本就是逆天而行——你早已在天道的黑名单上。"},
                {"storyMystery", "你在命运线中看到的那个吞噬世界的阴影——和签到系统制造者看到的虚影是同一个吗？"}
            });
            gm.RegisterTemplate(divination);

            // 吞噬系统
            var devour = new DevourSystem();
            devour.Initialize(new Dictionary<string, object> {
                {"id", "gift_devour_001"}, {"name", "吞噬系统"}, {"type", "System"}, {"rarity", "SSR"},
                {"storyOrigin", "吞噬生命的禁术。上一个使用者已经完全转化为了虚空生物——就是那个在追杀老爷爷的人。"},
                {"storyMystery", "妖兽不是天生的。它们是上一个被虚空吞噬的世界的幸存者。吞噬它们——你也在变成它们。你还能回头吗？"}
            });
            gm.RegisterTemplate(devour);

            // 剑心通明
            var swordHeart = new SwordHeart();
            swordHeart.Initialize(new Dictionary<string, object> {
                {"id", "gift_sword_001"}, {"name", "剑心通明"}, {"type", "Talent"}, {"rarity", "SR"},
                {"storyOrigin", "天生剑道奇才。不是学了剑法——是剑选择了你。上一个被选中的人成了剑仙。他失去了一切。"},
                {"storyMystery", "天道中刻着一把剑。那是'剑道'的本体。为什么它会出现在你的剑意中？它在等你做什么？"}
            });
            gm.RegisterTemplate(swordHeart);

            // 阵法大师
            var formation = new FormationMaster();
            formation.Initialize(new Dictionary<string, object> {
                {"id", "gift_formation_001"}, {"name", "阵法大师"}, {"type", "Knowledge"}, {"rarity", "SR"},
                {"storyOrigin", "阵法是灵气大陆最古老的技艺。比宗门更古老。比文字更古老。第一个学会布阵的人——没有人知道他的名字。但每一个学阵法的人都会在梦中看到同一张脸。"},
                {"storyMystery", "那个黑衣人——他尝了你的灵力，说'可以教你'。他不是修士。他没有灵力。那他怎么知道阵法？他活了多久了？"}
            });
            gm.RegisterTemplate(formation);

            // 时光回溯
            var timeRegression = new TimeRegression();
            timeRegression.Initialize(new Dictionary<string, object> {
                {"id", "gift_time_001"}, {"name", "时光回溯"}, {"type", "Mystery"}, {"rarity", "SSR"},
                {"storyOrigin", "这不是金手指。这是一个被困在同一天3000年的灵魂和他的交易。每一次使用，你离他更近一步。"},
                {"storyMystery", "那个灵魂说他是3000年前的你。上一个轮回的失败品。为什么你会轮回？你失败了多少次？这一次有什么不同？"}
            });
            gm.RegisterTemplate(timeRegression);

            // 御兽宗师
            var beastTamer = new BeastTamer();
            beastTamer.Initialize(new Dictionary<string, object> {
                {"id", "gift_beast_001"}, {"name", "御兽宗师"}, {"type", "Talent"}, {"rarity", "SR"},
                {"storyOrigin", "御兽师曾是一个被尊崇的职业——直到天元宗五十年前将其定为'妖修'，全部处决。最后一位御兽师躲进了妖兽森林。"},
                {"storyMystery", "天元宗为什么突然将御兽师定为妖修？五十年前发生了什么——让一个正道宗门对御兽师赶尽杀绝？"}
            });
            gm.RegisterTemplate(beastTamer);

            // 药王谷传承
            var alchemy = new AlchemyMaster();
            alchemy.Initialize(new Dictionary<string, object> {
                {"id", "gift_alchemy_001"}, {"name", "药王谷传承"}, {"type", "Knowledge"}, {"rarity", "SR"},
                {"storyOrigin", "药王谷——天下炼丹师的圣地。五十年前一夜之间凭空消失。三千弟子、所有典籍、整座山谷——连同谷主——人间蒸发。只有一枚传承玉简留了下来。"},
                {"storyMystery", "谷主为什么要启动那个阵法？他说'虚空进来了'——虚空是什么？为什么它在炉火里？它为什么在等你？"}
            });
            gm.RegisterTemplate(alchemy);

            // 影分身
            var shadowClone = new ShadowClone();
            shadowClone.Initialize(new Dictionary<string, object> {
                {"id", "gift_shadow_001"}, {"name", "影分身"}, {"type", "Ability"}, {"rarity", "SR"},
                {"storyOrigin", "分出去的每一片灵魂都不会完整地回来。它们会去一个地方——一个充满'你'的地方。虚空在用你的灵魂碎片做实验。"},
                {"storyMystery", "虚空为什么在复制你？成千上万个你的副本——它们被用来做什么？"}
            });
            gm.RegisterTemplate(shadowClone);

            // 鉴宝灵瞳
            var merchantEye = new MerchantEye();
            merchantEye.Initialize(new Dictionary<string, object> {
                {"id", "gift_eye_001"}, {"name", "鉴宝灵瞳"}, {"type", "Talent"}, {"rarity", "R"},
                {"storyOrigin", "能看到物品承载的记忆和执念。看得越多，越发现这个世界被'使用'过——被修改过。有人在操控一切。"},
                {"storyMystery", "张老身上的虚空痕迹最多——因为他妻子在虚空里。但有一个人，痕迹不是黑色的，是金色的。他是谁？他为什么不同？"}
            });
            gm.RegisterTemplate(merchantEye);

            // 梦境行者
            var dreamWalker = new DreamWalker();
            dreamWalker.Initialize(new Dictionary<string, object> {
                {"id", "gift_dream_001"}, {"name", "梦境行者"}, {"type", "Mystery"}, {"rarity", "SSR"},
                {"storyOrigin", "每个人的梦都是一扇门。所有的门都通向同一片虚空。有一个穿越者在用梦境向全大陆发送警告——他的梦里，虚空已经吞噬了半个世界。"},
                {"storyMystery", "那个穿越者是谁？他看到了未来还是过去？如果他看到的是未来——还来得及改变吗？"}
            });
            gm.RegisterTemplate(dreamWalker);

            Debug.Log("[GameManager] All gift templates registered (15). 🎉");
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
                var eq = EquipmentManager.Instance;
                if (eq != null) Debug.Log($"[Equip] {eq.GetSummary()}");
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    var items = inv.GetAllItems();
                    if (items.Count > 0)
                        Debug.Log($"[Inventory] 背包 {inv.Count}/{inv.maxSlots}:\n" +
                            string.Join("\n", items.ConvertAll(i =>
                                $"  [{i.rarity}] {i.name} x{i.quantity} — {i.description}")));
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

            // 按J键查看成就
            if (Input.GetKeyDown(KeyCode.J))
            {
                var am = AchievementManager.Instance;
                if (am != null)
                {
                    var all = am.GetAll();
                    int unlocked = all.FindAll(a => a.unlocked).Count;
                    Debug.Log($"🏆 成就 ({unlocked}/{all.Count}):");
                    foreach (var a in all)
                        Debug.Log($"  {(a.unlocked ? "✅" : "🔒")} {a.title} — {a.description} (+{a.reward}💰)");
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

            // 按K键执行第一个可用制作(带预览)
            if (Input.GetKeyDown(KeyCode.K))
            {
                var cm = CraftingManager.Instance;
                var recipes = cm?.GetAvailableRecipes();
                if (recipes != null && recipes.Count > 0)
                {
                    var r = recipes[0];
                    var inv = InventoryManager.Instance;
                    var ings = string.Join(", ", System.Linq.Enumerable.Select(
                        r.ingredients, kvp => $"{kvp.Key}:{kvp.Value}"));
                    Debug.Log($"[Craft] 将制作 [{r.resultRarity}]{r.resultItemName} x{r.resultQuantity}，消耗: {ings}");
                    Debug.Log("[Craft] 按Y确认制作，其他键取消。");
                    StartCoroutine(ConfirmCraft(r.id));
                }
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

            // F5快速存档
            if (Input.GetKeyDown(KeyCode.F5)) QuickSave();

            // Shift+N出售(防误触)
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.N)) ConfirmSellItem();

            // Tab切换攻击目标
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var cs = EarthOnline.Combat.CombatSystem.Instance;
                if (cs != null) cs.CycleTarget();
            }

            // L键查看通缉状态
            if (Input.GetKeyDown(KeyCode.L))
            {
                var crime = CrimeSystem.Instance;
                if (crime != null) Debug.Log($"[通缉] {crime.GetStatusText()}\n  犯罪记录:{(crime.crimeRecord.Count > 0 ? string.Join(", ", crime.crimeRecord) : "无")}");
            }

            // G键缴纳罚款
            if (Input.GetKeyDown(KeyCode.G))
            {
                CrimeSystem.Instance?.PayBounty();
            }

            // M键查看市场行情
            if (Input.GetKeyDown(KeyCode.M))
            {
                MarketSystem.Instance?.ShowMarketReport();
            }

            // 商店已改为NPC对话中按Y打开 (V2.0沉浸式交互)
            // (N键改为Shift+N防误触，见上方)
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
                    playerSpiritStones = stats != null ? stats.spiritStones : 0,
                    gameDay = time != null ? time.GameDay : 1,
                    currentSceneName = "EarthOnline_Main"
                };
                sm.Save(saveData);
                Debug.Log($"[GameManager] 💾 自动存档 — 第{data["day"]}天");
            }
        }

        void QuickSave()
        {
            var sm = SaveManager.Instance; var stats = PlayerStats.Instance;
            var time = TimeManager.Instance; var player = GameObject.FindGameObjectWithTag("Player");
            if (sm != null)
            {
                sm.Save(new EarthOnline.Framework.SaveData
                {
                    playerName = playerName,
                    playerPosX = player != null ? player.transform.position.x : 0,
                    playerPosY = player != null ? player.transform.position.y : 0,
                    playerPosZ = player != null ? player.transform.position.z : 0,
                    playerLevel = stats != null ? stats.playerLevel : 1,
                    playerSpiritStones = stats != null ? stats.spiritStones : 0,
                    gameDay = time != null ? time.GameDay : 1,
                    currentSceneName = "EarthOnline_Main"
                });
                Debug.Log("[GameManager] 💾 快速存档完成！(F5)");
            }
        }

        void ConfirmSellItem()
        {
            var shop = ShopManager.Instance;
            var inv = InventoryManager.Instance;
            if (shop == null || inv == null) return;
            var items = inv.GetAllItems();
            if (items.Count == 0) { Debug.Log("[Shop] 背包空空。"); return; }
            var item = items[0];
            Debug.Log($"[Shop] 按Y确认出售 [{item.rarity}]{item.name}(+{item.value/2}💰)，其他键取消。");
            StartCoroutine(WaitForSellConfirm(item.id));
        }

        System.Collections.IEnumerator WaitForSellConfirm(string itemId)
        {
            float timeout = Time.time + 3f;
            while (Time.time < timeout)
            {
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    ShopManager.Instance?.Sell(itemId);
                    yield break;
                }
                if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Y))
                {
                    Debug.Log("[Shop] 取消出售。");
                    yield break;
                }
                yield return null;
            }
            Debug.Log("[Shop] 超时，取消出售。");
        }

        void TryOpenShop()
        {
            var shop = ShopManager.Instance; var player = GameObject.FindGameObjectWithTag("Player");
            if (shop == null || player == null) return;
            var npcs = Object.FindObjectsOfType<EarthOnline.NPC.NPCBase>();
            EarthOnline.NPC.NPCBase closest = null; float bestDist = 6f;
            foreach (var n in npcs)
            {
                float d = Vector3.Distance(player.transform.position, n.transform.position);
                if (d < bestDist) { bestDist = d; closest = n; }
            }
            if (closest != null)
                shop.ShowShop(closest.npcId);
            else
                Debug.Log("[Shop] 附近没有商人。村子里找陈半仙(金色NPC)或李灵儿(绿色NPC)按B购物。");
        }

        System.Collections.IEnumerator ConfirmCraft(string recipeId)
        {
            float deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                if (Input.GetKeyDown(KeyCode.Y)) { CraftingManager.Instance?.Craft(recipeId); yield break; }
                if (Input.anyKeyDown) { Debug.Log("[Craft] 取消制作。"); yield break; }
                yield return null;
            }
        }

        void UseGiftAbility(string abilityName)
        {
            var gm = GiftManager.Instance;
            if (gm == null) return;
            foreach (var g in gm.GetActiveGifts())
            {
                g.UseAbility(abilityName);
                // Buff bindings
                if (abilityName == "sign_in")
                    EarthOnline.Combat.BuffManager.Instance?.Apply(
                        EarthOnline.Combat.BuffType.AttackUp, 0.2f, 300f, "签到祝福");
                if (abilityName == "heal")
                    EarthOnline.Combat.BuffManager.Instance?.Apply(
                        EarthOnline.Combat.BuffType.DefenseUp, 0.15f, 180f, "圣体护盾");
                if (abilityName == "cultivate")
                    EarthOnline.Combat.BuffManager.Instance?.Apply(
                        EarthOnline.Combat.BuffType.SpeedUp, 0.1f, 180f, "灵气灌注");
            }
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
