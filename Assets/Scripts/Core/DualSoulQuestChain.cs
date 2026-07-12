using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 双魂5主线任务链 —— 觉醒→试探→反击→决战→真相
    /// 仅 DualSoul 出身激活。与 DualSoulManager 深度联动（信任度/觉醒度）。
    /// 5个任务覆盖从"初次对话"到"揭穿师父二十年骗局"的完整弧线。
    /// </summary>
    public class DualSoulQuestChain : MonoBehaviour
    {
        public static DualSoulQuestChain Instance { get; private set; }

        [Header("任务链状态")]
        public bool chainStarted;
        public int currentQuestIndex = -1;  // -1 = 未开始
        public bool chainCompleted;

        [Header("关键NPC引用ID")]
        public string npcLittleSister = "npc_lin_meng";   // 林师妹——偷丹药的
        public string npcElderMing = "npc_ming_zhanglao"; // 明长老——陷害主角的
        public string npcSectMaster = "npc_tian_xuanzi";  // 天玄子——师父/PUA原主人
        public string npcSectWitness = "npc_witness_elder"; // 见证长老（中立）

        [Header("对话文本（策划可调）")]
        public string[] alertNeiXin = new[] {
            "那个声音又出现了……它说小师妹在偷东西。",
            "但我不能去看——她会生气的。",
            "可是……如果它说的是真的呢？",
            "我……我不知道该怎么办。"
        };

        private List<DualSoulQuest> _questChain = new();
        private QuestData _currentQuestData;
        private bool _questManagerActivated;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (!DualSoulManager.Instance.IsActive) return;
            InitializeDualSoulChain();
            EventBus.Subscribe("OnQuestCompleted", OnQuestCompleted);
            EventBus.Subscribe("OnDualSoulAwakened", OnDualSoulAwakened);

            // 自动激活第一个任务到QuestManager（等一帧确保QuestManager已就绪）
            StartCoroutine(ActivateFirstQuestInQuestManager());
        }

        System.Collections.IEnumerator ActivateFirstQuestInQuestManager()
        {
            yield return null; // 等一帧确保QuestManager.RegisterAllQuests()已执行
            var qm = QuestManager.Instance;
            if (qm != null && qm.AcceptQuest("ds_01"))
            {
                _questManagerActivated = true;
                Debug.Log("[双魂·任务链] 📌 双魂主线任务1已激活到QuestManager");
            }
            else
            {
                Debug.LogWarning("[双魂·任务链] ⚠ 激活任务1到QuestManager失败，将在Update中重试");
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnQuestCompleted", OnQuestCompleted);
            EventBus.Unsubscribe("OnDualSoulAwakened", OnDualSoulAwakened);
        }

        void Update()
        {
            if (!DualSoulManager.Instance.IsActive) return;
            if (chainCompleted) return;

            // 如果协程未能激活，在Update中兜底重试
            if (!_questManagerActivated)
            {
                var qm = QuestManager.Instance;
                if (qm != null && qm.AcceptQuest("ds_01"))
                {
                    _questManagerActivated = true;
                    Debug.Log("[双魂·任务链] 📌 双魂主线任务1已激活到QuestManager");
                }
            }

            // 自动检测任务进度——用于非标准完成条件
            CheckQuestProgress();
        }

        #region === 任务链构建 ===

        void InitializeDualSoulChain()
        {
            var dm = DualSoulManager.Instance;

            // 任务1：觉醒·第一声低语
            _questChain.Add(new DualSoulQuest
            {
                id = "ds_01",
                title = "觉醒·第一声低语",
                type = QuestType.Talk,
                chapter = "觉醒",
                description = "你灵魂中有一个声音在告诉你——有人在偷东西。你不敢看——但这次你得看看。",
                shortObjective = "目睹林师妹偷走丹药，并回应内心的声音",
                trigger = new QuestTrigger { type = TriggerType.Auto, autoDelay = 5f },
                completionCheck = (ctx) => ctx.littleSisterStolen && ctx.firstTalkDone,
                onAccept = (ctx) => {
                    Debug.Log("[双魂·任务] 📋 觉醒·第一声低语——开始。那个声音在脑海中回响……");
                    // 自动触发林师妹偷丹药事件
                    ctx.TriggerLittleSisterScene();
                },
                onComplete = (ctx) => {
                    var dmLocal = DualSoulManager.Instance;
                    dmLocal.AddTrust(8, "任务1·觉醒：念安第一次相信了你的话");
                    dmLocal.AddAwakening(5, "任务1·觉醒：发现小师妹偷丹药的真相");
                    ctx.littleSisterStolen = false;
                    ctx.firstTalkDone = false;
                    Debug.Log("[双魂·任务] ✅ 觉醒完成——念安开始动摇。信任+8 觉醒+5");
                },
                rewards = new QuestRewards { spiritStones = 50, cultivation = 20 },
                failure = new QuestFailure { conditions = "无——觉醒是故事起点，不可跳过" },
                nextQuestId = "ds_02"
            });

            // 任务2：试探·第一次出手
            _questChain.Add(new DualSoulQuest
            {
                id = "ds_02",
                title = "试探·第一次出手",
                type = QuestType.Combat,
                chapter = "试探",
                description = "宗门小比在即。明长老故意安排你和筑基期师兄对战——他想看你出丑。但这次——你有帮手。",
                shortObjective = "在宗门小比中击败明长老安排的对手",
                trigger = new QuestTrigger { type = TriggerType.NpcTalk, npcId = "npc_lin_meng" },
                completionCheck = (ctx) => ctx.tournamentWon,
                onAccept = (ctx) => {
                    Debug.Log("[双魂·任务] 📋 试探·第一次出手——'你决定出手了？好。听我指挥。'");
                    // 注册战斗监听——双魂参战标志
                    ctx.RegisterCombatListener("ds_02_boss");
                },
                onComplete = (ctx) => {
                    var dmLocal = DualSoulManager.Instance;
                    dmLocal.AddTrust(10, "任务2·试探：在你的指挥下赢得了宗门小比");
                    dmLocal.AddAwakening(8, "任务2·试探：第一次尝到'反抗'的甜头");
                    Debug.Log("[双魂·任务] ✅ 试探完成——念安第一次赢了。信任+10 觉醒+8");
                },
                rewards = new QuestRewards { spiritStones = 120, cultivation = 60, rewardItemId = "item_dualsoul_talisman_01" },
                failure = new QuestFailure {
                    conditions = "输掉小比 → 信任度-5，觉醒度-3（可重试）",
                    onFail = (ctx) => {
                        var dmLocal = DualSoulManager.Instance;
                        dmLocal.AddTrust(-5, "任务2失败：输掉小比，念安更加退缩");
                        dmLocal.AddAwakening(-3, "任务2失败：反抗失败加重自我怀疑");
                    }
                },
                nextQuestId = "ds_03"
            });

            // 任务3：反击·真相在眼前
            _questChain.Add(new DualSoulQuest
            {
                id = "ds_03",
                title = "反击·真相在眼前",
                type = QuestType.Talk,
                chapter = "反击",
                description = "明长老再次设局陷害你——这次是栽赃你偷了宗门秘典。但你早已准备好了证据。在所有人面前——揭穿他。",
                shortObjective = "收集3件证据并在宗门会审上揭发明长老",
                trigger = new QuestTrigger { type = TriggerType.Auto, requiredTrust = 20 },
                completionCheck = (ctx) => ctx.evidenceCollected >= 3 && ctx.elderExposed,
                onAccept = (ctx) => {
                    Debug.Log("[双魂·任务] 📋 反击·真相在眼前——'这次，我们不躲了。'");
                    ctx.RegisterCollectListener("item_evidence_01", "item_evidence_02", "item_evidence_03");
                },
                onComplete = (ctx) => {
                    var dmLocal = DualSoulManager.Instance;
                    dmLocal.AddTrust(12, "任务3·反击：在所有人面前揭穿了陷害者");
                    dmLocal.AddAwakening(15, "任务3·反击：宗门震惊——念安不再是任人宰割的懦夫");
                    // 更新复仇名单：明长老从灰变红
                    dmLocal.RecordRevenge("明长老", "多次诬陷、抢夺丹药、栽赃秘典");
                    Debug.Log("[双魂·任务] ✅ 反击完成——宗门炸锅。信任+12 觉醒+15");
                },
                rewards = new QuestRewards { spiritStones = 200, cultivation = 80, rewardItemId = "item_evidence_scroll" },
                failure = new QuestFailure {
                    conditions = "证据不足无法揭穿 → 觉醒度-5，明长老怀疑加深",
                    onFail = (ctx) => {
                        DualSoulManager.Instance.AddAwakening(-5, "任务3失败：证据不足，陷害成功");
                    }
                },
                nextQuestId = "ds_04"
            });

            // 任务4：决战·元婴之怒
            _questChain.Add(new DualSoulQuest
            {
                id = "ds_04",
                title = "决战·元婴之怒",
                type = QuestType.Boss,
                chapter = "决战",
                description = "天玄子（师父）察觉到了你的变化。他派出了大弟子——元婴中期修为——来'清理门户'。这一次，你不再隐藏真正的实力。",
                shortObjective = "以元婴全力击败天玄子派来的大弟子",
                trigger = new QuestTrigger { type = TriggerType.NpcTalk, npcId = "npc_tian_xuanzi" },
                completionCheck = (ctx) => ctx.bossDefeated,
                onAccept = (ctx) => {
                    Debug.Log("[双魂·任务] 📋 决战·元婴之怒——'他们要来看看你有多强。让他们看。'");
                    ctx.RegisterCombatListener("boss_senior_001");
                    // 通知战斗系统开启双魂全力模式
                    EventBus.Publish("OnDualSoulFullPower");
                },
                onComplete = (ctx) => {
                    var dmLocal = DualSoulManager.Instance;
                    dmLocal.AddTrust(15, "任务4·决战：念安全力出手的震撼");
                    dmLocal.AddAwakening(20, "任务4·决战：天地异变——元婴之力的真相");
                    // 天地异变效果
                    EventBus.Publish("OnWorldEvent", new Dictionary<string, object> {
                        {"eventId", "world_sky_change"}, {"intensity", "high"}
                    });
                    Debug.Log("[双魂·任务] ✅ 决战完成——天地变色！全宗门震动。信任+15 觉醒+20");
                },
                rewards = new QuestRewards {
                    spiritStones = 500, cultivation = 200,
                    rewardItemId = "item_dualsoul_awaken_core"
                },
                failure = new QuestFailure {
                    conditions = "战败 → 信任-10，觉醒-5，念安陷入自我怀疑（24小时后可重试）",
                    onFail = (ctx) => {
                        var dmLocal = DualSoulManager.Instance;
                        dmLocal.AddTrust(-10, "任务4失败：全力出手却战败");
                        dmLocal.AddAwakening(-5, "任务4失败：'我果然还是不够强……'");
                    }
                },
                nextQuestId = "ds_05"
            });

            // 任务5：真相·二十年骗局
            _questChain.Add(new DualSoulQuest
            {
                id = "ds_05",
                title = "真相·二十年骗局",
                type = QuestType.Talk,
                chapter = "真相",
                description = "天玄子召你进入他的闭关密室。二十年的疑惑即将揭晓——为什么师父养大你却从不真正教你？为什么所有人都在欺负你而他视而不见？因为你的身体——是万年难遇的'道胎'。他养你——只是为了炼你。",
                shortObjective = "进入密室，面对师父，做出最终选择",
                trigger = new QuestTrigger { type = TriggerType.Auto, requiredAwakening = 40, requiredTrust = 40 },
                completionCheck = (ctx) => ctx.truthRevealed && ctx.finalChoiceMade,
                onAccept = (ctx) => {
                    Debug.Log("[双魂·任务] 📋 真相·二十年骗局——'走吧。去问清楚。无论答案是什么。'");
                    // 锁定场景入口
                    EventBus.Publish("OnDualSoulFinalConfrontation");
                },
                onComplete = (ctx) => {
                    var dmLocal = DualSoulManager.Instance;
                    dmLocal.AddTrust(20, "任务5·真相：共同面对师父——灵魂真正的战友");
                    dmLocal.AddAwakening(30, "任务5·真相：二十年的骗局——全部揭穿");
                    chainCompleted = true;

                    // 全服公告
                    Debug.Log("[双魂] 🌟🌟🌟 剧情完成 — 双魂一体主线全通关！🌟🌟🌟");
                    Debug.Log("[双魂] 念安看着你：'二十年……原来我从来不是他弟子。我是他的丹药。'");
                    Debug.Log("[双魂] 他转向你：'但你——你是真的。从始至终——只有你是真的。'");

                    // 解锁终极能力/结局分支
                    EventBus.Publish("OnDualSoulFinale", new Dictionary<string, object> {
                        {"trust", dmLocal.trust}, {"awakening", dmLocal.awakening}
                    });
                },
                rewards = new QuestRewards {
                    spiritStones = 1000, cultivation = 500,
                    rewardItemId = "item_dualsoul_final_relic"
                },
                failure = new QuestFailure {
                    conditions = "师父发现双魂存在且出手剥离 → 触发Bad Ending。觉醒度>=60可抵抗。",
                    onFail = (ctx) => {
                        Debug.Log("[双魂] ❌ BAD ENDING——师父剥离了穿越者的灵魂。念安回到了孤独……");
                        EventBus.Publish("OnDualSoulBadEnding");
                    }
                },
                nextQuestId = ""
            });

            // 注册任务到QuestManager
            foreach (var dsq in _questChain)
            {
                RegisterToQuestManager(dsq);
            }

            // 启动第一个任务
            chainStarted = true;
            StartQuest("ds_01");
        }

        void RegisterToQuestManager(DualSoulQuest dsq)
        {
            // QuestData已在QuestManager.RegisterAllQuests()中预先注册，
            // 此处无需重复注册。保留方法桩以便后续扩展。
        }

        #endregion

        #region === 任务生命周期 ===

        void StartQuest(string questId)
        {
            var dsq = _questChain.FirstOrDefault(q => q.id == questId);
            if (dsq == null) return;

            currentQuestIndex = _questChain.IndexOf(dsq);
            dsq.onAccept?.Invoke(_questContext);

            Debug.Log($"[双魂·任务链] 📌 当前任务 [{currentQuestIndex + 1}/5]：{dsq.title}");
            EventBus.Publish("OnDualSoulQuestStarted", new Dictionary<string, object> {
                {"questId", questId}, {"title", dsq.title}, {"chapter", dsq.chapter}
            });
        }

        void CompleteQuest(string questId)
        {
            var dsq = _questChain.FirstOrDefault(q => q.id == questId);
            if (dsq == null) return;

            dsq.onComplete?.Invoke(_questContext);

            // 标准奖励（灵石/修为/道具）由QuestManager统一发放（调用CompleteQuestById）
            // 双魂专有奖励（信任度/觉醒度）已在onComplete中通过DualSoulManager处理
            Debug.Log($"[双魂·任务链] ✅ 任务完成 [{currentQuestIndex + 1}/5]：{dsq.title}");
            EventBus.Publish("OnDualSoulQuestCompleted", new Dictionary<string, object> {
                {"questId", questId}, {"chapter", dsq.chapter}
            });

            // 同步到QuestManager：标记完成 & 自动解锁下一个任务
            var qm = QuestManager.Instance;
            if (qm != null)
            {
                qm.CompleteQuestById(questId);
                if (!string.IsNullOrEmpty(dsq.nextQuestId))
                {
                    qm.AcceptQuest(dsq.nextQuestId);
                }
            }

            // 启动下一个任务
            if (!string.IsNullOrEmpty(dsq.nextQuestId))
            {
                StartQuest(dsq.nextQuestId);
            }
            else
            {
                chainCompleted = true;
                Debug.Log("[双魂·任务链] 🎉 全部5个任务完成！双魂主线通关。");
            }
        }

        void FailQuest(string questId)
        {
            var dsq = _questChain.FirstOrDefault(q => q.id == questId);
            if (dsq?.failure.onFail != null)
            {
                dsq.failure.onFail.Invoke(_questContext);
                Debug.Log($"[双魂·任务链] ❌ 任务失败 [{currentQuestIndex + 1}/5]：{dsq.title} — {dsq.failure.conditions}");
                EventBus.Publish("OnDualSoulQuestFailed", new Dictionary<string, object> {
                    {"questId", questId}, {"chapter", dsq.chapter}
                });
            }
        }

        #endregion

        #region === 进度检查 ===

        /// <summary>
        /// 任务上下文——追踪每个任务的运行时状态
        /// </summary>
        public class QuestContext
        {
            // 任务1
            public bool littleSisterStolen;
            public bool firstTalkDone;

            // 任务2
            public bool tournamentWon;

            // 任务3
            public int evidenceCollected;
            public bool elderExposed;
            public HashSet<string> collectedEvidence = new();

            // 任务4
            public bool bossDefeated;

            // 任务5
            public bool truthRevealed;
            public bool finalChoiceMade;

            // 运行时
            public HashSet<string> combatListeners = new();
            public HashSet<string> collectListeners = new();

            public void TriggerLittleSisterScene()
            {
                // 由外部NPC事件调用
                Debug.Log("[双魂·场景] 📜 林师妹进来了。她熟练地翻开你的储物袋，取走了三枚聚灵丹。");
                Debug.Log("[双魂·场景] 📜 '师兄不会发现的——他从来不敢说话。'她自言自语。");
                Debug.Log("[双魂·场景] 📜 但这次——你看到了。那个声音看到了。");
                littleSisterStolen = true;
            }

            public void RegisterCombatListener(string enemyId)
            {
                combatListeners.Add(enemyId);
                EventBus.Subscribe("OnEnemyKilled", (data) => {
                    var eId = data?.ContainsKey("enemyId") == true ? data["enemyId"]?.ToString() : "";
                    if (eId == enemyId)
                    {
                        if (enemyId == "ds_02_boss") { tournamentWon = true; }
                        if (enemyId == "boss_senior_001") { bossDefeated = true; }
                    }
                });
            }

            public void RegisterCollectListener(params string[] itemIds)
            {
                foreach (var id in itemIds) collectListeners.Add(id);
                EventBus.Subscribe("OnItemAdded", (data) => {
                    var iId = data?.ContainsKey("itemId") == true ? data["itemId"]?.ToString() : "";
                    if (collectListeners.Contains(iId) && !collectedEvidence.Contains(iId))
                    {
                        collectedEvidence.Add(iId);
                        evidenceCollected = collectedEvidence.Count;
                        Debug.Log($"[双魂·证据] 📄 收集证据 ({evidenceCollected}/3)");
                    }
                });
            }
        }

        private QuestContext _questContext = new QuestContext();

        void CheckQuestProgress()
        {
            if (currentQuestIndex < 0 || currentQuestIndex >= _questChain.Count) return;
            var currentQuest = _questChain[currentQuestIndex];

            // 检查触发条件
            if (!currentQuest.triggered && CheckTriggerCondition(currentQuest.trigger))
            {
                currentQuest.triggered = true;
                // 如果任务尚未激活，通知玩家
                Debug.Log($"[双魂·任务] 💡 新任务触发：{currentQuest.title}");
            }

            // 检查完成条件
            if (currentQuest.completionCheck != null && currentQuest.completionCheck(_questContext))
            {
                CompleteQuest(currentQuest.id);
            }

            // 检查失败条件（特定手动检查）
            CheckFailureConditions(currentQuest);
        }

        bool CheckTriggerCondition(QuestTrigger trigger)
        {
            var dm = DualSoulManager.Instance;
            switch (trigger.type)
            {
                case TriggerType.Auto:
                    if (trigger.requiredTrust > 0 && dm.trust < trigger.requiredTrust) return false;
                    if (trigger.requiredAwakening > 0 && dm.awakening < trigger.requiredAwakening) return false;
                    return true;
                case TriggerType.NpcTalk:
                    // 由外部OnNPCInteract事件触发
                    if (trigger.requiredTrust > 0 && dm.trust < trigger.requiredTrust) return false;
                    if (trigger.requiredAwakening > 0 && dm.awakening < trigger.requiredAwakening) return false;
                    return true;
                default:
                    return true;
            }
        }

        void CheckFailureConditions(DualSoulQuest quest)
        {
            // 特定任务的失败检查由各任务自己的onComplete/onFail控制
        }

        #endregion

        #region === 事件回调 ===

        void OnQuestCompleted(Dictionary<string, object> data)
        {
            if (!DualSoulManager.Instance.IsActive) return;
            string qId = data?.ContainsKey("questId") == true ? data["questId"]?.ToString() : "";
            // 处理从QuestManager传来的完成事件
        }

        void OnDualSoulAwakened(Dictionary<string, object> data)
        {
            // 觉醒度100时触发——双魂终极状态解锁
            Debug.Log("[双魂·任务链] 🔥 念安完全觉醒！双魂一体达到巅峰状态。");
            chainCompleted = true;
        }

        // 对外接口——NPC交互时调用
        public void OnNpcTalked(string npcId)
        {
            if (currentQuestIndex < 0 || currentQuestIndex >= _questChain.Count) return;
            var currentQuest = _questChain[currentQuestIndex];
            if (currentQuest.trigger.type == TriggerType.NpcTalk && currentQuest.trigger.npcId == npcId)
            {
                if (!currentQuest.triggered)
                {
                    currentQuest.triggered = true;
                    Debug.Log($"[双魂·任务] 💬 对话触发：{currentQuest.title}");
                }
            }

            // 任务5的特殊触发——进入师父密室
            if (currentQuest.id == "ds_05" && npcId == npcSectMaster)
            {
                _questContext.truthRevealed = true;
                Debug.Log("[双魂·场景] 🎭 天玄子转过身来。他看你的眼神——不像看弟子。像看一件工具。");
                Debug.Log("[双魂·场景] 🎭 '念安——或者说，你体内的那位。你终于来了。'");
                Debug.Log("[双魂·场景] 🎭 '你知道我等了多久吗？二十年。你的道胎……终于成熟了。'");
            }
        }

        // 对外接口——执行最终选择
        public void MakeFinalChoice(int choice)
        {
            // choice: 0=灵魂融合, 1=分离, 2=继续共生
            _questContext.finalChoiceMade = true;
            string[] choiceTexts = {
                "'我们合二为一吧。不分开——也不分彼此。'——灵魂融合路线",
                "'给我一具肉身。我们各自作为独立的人活下去。'——分离路线",
                "'就这样吧。你站在阳光下——我在你心里。永远。'——继续共生路线"
            };
            Debug.Log($"[双魂·结局] 🔮 最终选择：{choiceTexts[choice]}");
            Debug.Log("[双魂·结局] 🔮 念安看着你。他笑了。二十年——他第一次真正地笑。");
            EventBus.Publish("OnDualSoulEnding", new Dictionary<string, object> {
                {"endingChoice", choice}, {"trust", DualSoulManager.Instance.trust},
                {"awakening", DualSoulManager.Instance.awakening}
            });
        }

        #endregion

        #region === 对话文本 ===

        string GetQuestAcceptDialogue(string questId)
        {
            return questId switch
            {
                "ds_01" => "（内心低语）'你……你真的是在和我说话？'",
                "ds_02" => "（内心低语）'好。听你的。但我……我怕。'",
                "ds_03" => "（坚定）'够了。这次——我不躲了。'",
                "ds_04" => "（冷笑）'他们想看看我有多强？好。让他们看。'",
                "ds_05" => "（平静）'走吧。到该去的地方去。'",
                _ => ""
            };
        }

        string GetQuestCompleteDialogue(string questId)
        {
            return questId switch
            {
                "ds_01" => "'她……她真的拿了。我一直以为她只是……只是借。但是她没有还过。从来没有。'",
                "ds_02" => "'赢了？我赢了？！那个声音——是你的功劳。谢谢。'",
                "ds_03" => "'你看——所有人的表情。他们不敢相信。不敢相信我会反抗。'",
                "ds_04" => "'这就是……我的力量？不——这是我们的力量。'",
                "ds_05" => "'二十年——原来我从来不是他弟子。我是他的丹药。但你——你是真的。只有你是真的。'",
                _ => ""
            };
        }

        string GetQuestCompletionText(string questId)
        {
            return questId switch
            {
                "ds_01" => "念安开始动摇了。他的世界——第一次出现了裂痕。",
                "ds_02" => "胜利的滋味是甜的。念安第一次知道——反抗并不总是带来惩罚。",
                "ds_03" => "宗门炸开了锅。那个任人欺负的懦夫——居然在所有人面前揭穿了真相。",
                "ds_04" => "天象变色。元婴之威震动整个宗门。所有人——都在颤抖。",
                "ds_05" => "真相大白。道胎的秘密。二十年的骗局。以及——一个新世界的开始。",
                _ => ""
            };
        }

        #endregion

        #region === 调试命令 ===

        [ContextMenu("Force Start Quest Chain")]
        void DebugForceStart()
        {
            chainStarted = true;
            StartQuest("ds_01");
            Debug.Log("[双魂·调试] ⚡ 强制启动双魂任务链");
        }

        [ContextMenu("Force Complete Current Quest")]
        void DebugForceComplete()
        {
            if (currentQuestIndex >= 0 && currentQuestIndex < _questChain.Count)
            {
                CompleteQuest(_questChain[currentQuestIndex].id);
                Debug.Log($"[双魂·调试] ⚡ 强制完成任务 {_questChain[currentQuestIndex].id}");
            }
        }

        [ContextMenu("Print Chain Status")]
        void DebugPrintStatus()
        {
            var dm = DualSoulManager.Instance;
            Debug.Log($"=== 双魂任务链状态 ===");
            Debug.Log($"链进行中: {chainStarted}");
            Debug.Log($"当前任务索引: {currentQuestIndex + 1}/5");
            Debug.Log($"全链完成: {chainCompleted}");
            Debug.Log($"信任度: {dm.trust}/100 | 觉醒度: {dm.awakening}/100");
            Debug.Log($"同步率: {dm.syncRate}");
            Debug.Log($"复仇条目: {dm.revengeList.Count}条");
        }

        #endregion
    }

    #region === 数据结构 ===

    /// <summary>
    /// 双魂任务定义——扩展自QuestData，增加双魂专用字段
    /// </summary>
    [System.Serializable]
    public class DualSoulQuest
    {
        public string id;
        public string title;
        public QuestType type;
        public string chapter;        // 所属章节名（觉醒/试探/反击/决战/真相）
        public string description;
        public string shortObjective; // UI显示的简短目标

        public QuestTrigger trigger;
        public System.Func<DualSoulQuestChain.QuestContext, bool> completionCheck;
        public System.Action<DualSoulQuestChain.QuestContext> onAccept;
        public System.Action<DualSoulQuestChain.QuestContext> onComplete;

        public QuestRewards rewards;
        public QuestFailure failure;
        public string nextQuestId;

        [System.NonSerialized] public bool triggered;
    }

    /// <summary>
    /// 触发条件配置
    /// </summary>
    [System.Serializable]
    public class QuestTrigger
    {
        public TriggerType type = TriggerType.Auto;
        public string npcId = "";
        public string itemId = "";
        public float autoDelay = 0f;          // 自动触发的延迟秒数
        public int requiredTrust = 0;           // 最低信任度要求
        public int requiredAwakening = 0;       // 最低觉醒度要求
    }

    public enum TriggerType { Auto, NpcTalk, ItemPickup, Time }

    [System.Serializable]
    public class QuestRewards
    {
        public int spiritStones;
        public int cultivation;
        public string rewardItemId;
        // 信任度和觉醒度奖励直接在onComplete里调用DualSoulManager
    }

    [System.Serializable]
    public class QuestFailure
    {
        public string conditions;
        public System.Action<DualSoulQuestChain.QuestContext> onFail;
    }

    #endregion
}
