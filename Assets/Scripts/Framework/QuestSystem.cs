using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    public enum QuestType { Guidance, Combat, Explore, Collect, Talk, Boss }
    public enum QuestStatus { Available, Accepted, InProgress, Completed, Failed }

    [System.Serializable]
    public class QuestData
    {
        public string id, title, description;
        public QuestType type;
        public QuestStatus status;
        public string giverNpcId, giverName;
        public string targetId;
        public int targetCount, currentCount;
        public int rewardSpiritStones, rewardCultivation;
        public string rewardItemId;
        public string nextQuestId;
        public string completionText;
        public List<string> dialogueOnAccept = new();
        public List<string> dialogueOnComplete = new();
    }

    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        private Dictionary<string, QuestData> _allQuests = new();
        private List<QuestData> _activeQuests = new();
        public int questsCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterAllQuests();
            EventBus.Subscribe("OnNPCInteract", OnNpcInteract);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
            EventBus.Subscribe("OnItemAdded", OnItemCollected);
        }

        void RegisterAllQuests()
        {
            // 引导任务
            AddQuest(new QuestData {
                id="q_guide_01", title="初临灵气大陆", type=QuestType.Guidance, status=QuestStatus.Available,
                giverNpcId="npc_zhang_001", giverName="张老",
                description="你刚刚穿越到灵气大陆。张老似乎知道很多事情——去和他聊聊。",
                targetId="npc_zhang_001", targetCount=1,
                rewardSpiritStones=30, rewardCultivation=20,
                nextQuestId="q_guide_02",
                completionText="张老看着你，眼神里有一丝欣慰：'第47个了。希望你能比前面的人走得更远。'",
                dialogueOnAccept=new(){"终于又有人穿过来了。这个世界比你想象的大得多——也危险得多。先去村子里转转吧。"},
                dialogueOnComplete=new(){"你比我想的要聪明。拿着，这是我年轻时的一些修炼心得。"}
            });
            AddQuest(new QuestData {
                id="q_guide_02", title="认识村子", type=QuestType.Talk, status=QuestStatus.Available,
                giverNpcId="npc_zhang_001", giverName="张老",
                description="张老建议你先去认识村子里的人。和王铁柱聊聊，再去看看李灵儿的药铺。",
                targetId="npc_wang_001", targetCount=1,
                rewardSpiritStones=20, rewardCultivation=15,
                nextQuestId="q_guide_03",
                completionText="你对这个村子有了基本了解。每个人都有自己的故事——也有自己的秘密。",
            });
            AddQuest(new QuestData {
                id="q_guide_03", title="野外初探", type=QuestType.Combat, status=QuestStatus.Available,
                giverNpcId="npc_wang_001", giverName="王铁柱",
                description="王铁柱说野外有野狼出没。新手练手的好机会——击败1只野狼。",
                targetId="wolf_001", targetCount=1,
                rewardSpiritStones=50, rewardCultivation=30, rewardItemId="item_iron_sword",
                completionText="王铁柱点点头：'不错！这把铁剑送你了——虽然是旧的，但比空手强。'",
            });

            // 战斗任务
            AddQuest(new QuestData {
                id="q_combat_01", title="清除狼患", type=QuestType.Combat, status=QuestStatus.Available,
                giverNpcId="npc_li_001", giverName="李灵儿",
                description="野狼让采药人不敢进山。击败3只野狼让李灵儿能安全采药。",
                targetId="wolf_001", targetCount=3,
                rewardSpiritStones=80, rewardCultivation=40,
                completionText="李灵儿松了一口气：'终于能进山采药了。这个村子要是没有你——不知道该怎么办。'",
            });

            // 探索任务
            AddQuest(new QuestData {
                id="q_explore_01", title="虚空裂缝调查", type=QuestType.Explore, status=QuestStatus.Available,
                giverNpcId="npc_zhang_001", giverName="张老",
                description="北边的虚空裂缝越来越大。去看看那里发生了什么。注意安全——裂缝附近有强大的守护者。",
                targetId="DungeonEntrance", targetCount=1,
                rewardSpiritStones=150, rewardCultivation=80,
                completionText="张老面色凝重：'果然...虚空裂缝在扩大。我们需要做好准备。'",
            });

            // Boss任务
            AddQuest(new QuestData {
                id="q_boss_01", title="挑战虚空行者", type=QuestType.Boss, status=QuestStatus.Available,
                giverNpcId="npc_zhao_001", giverName="赵掌柜",
                description="赵掌柜说虚空行者守护着裂缝。击败它——你就能进入虚空边缘。建议Lv.5+。",
                targetId="boss_001", targetCount=1,
                rewardSpiritStones=300, rewardCultivation=150, rewardItemId="item_spirit_core_001",
                completionText="赵掌柜瞪大了眼睛：'你做到了...第47个穿越者——第一个击败虚空行者的人。'",
            });

            // ========== 双魂5主线任务链 ==========
            // 由DualSoulQuestChain驱动实际触发与CompletionCheck，QuestManager仅跟踪状态/奖励
            AddQuest(new QuestData {
                id="ds_01", title="觉醒·第一声低语", type=QuestType.Talk, status=QuestStatus.Available,
                giverNpcId="", giverName="命运",
                description="你灵魂中有一个声音在告诉你——有人在偷东西。你不敢看——但这次你得看看。",
                targetId="", targetCount=1,
                rewardSpiritStones=50, rewardCultivation=20,
                nextQuestId="ds_02",
                completionText="念安开始动摇了。他的世界——第一次出现了裂痕。",
                dialogueOnAccept=new List<string>{ "（内心低语）'你……你真的是在和我说话？'" },
                dialogueOnComplete=new List<string>{ "'她……她真的拿了。我一直以为她只是……只是借。但是她没有还过。从来没有。'" }
            });
            AddQuest(new QuestData {
                id="ds_02", title="试探·第一次出手", type=QuestType.Combat, status=QuestStatus.Available,
                giverNpcId="npc_lin_meng", giverName="林师妹",
                description="宗门小比在即。明长老故意安排你和筑基期师兄对战——他想看你出丑。但这次——你有帮手。",
                targetId="ds_02_boss", targetCount=1,
                rewardSpiritStones=120, rewardCultivation=60, rewardItemId="item_dualsoul_talisman_01",
                nextQuestId="ds_03",
                completionText="胜利的滋味是甜的。念安第一次知道——反抗并不总是带来惩罚。",
                dialogueOnAccept=new List<string>{ "（内心低语）'好。听你的。但我……我怕。'" },
                dialogueOnComplete=new List<string>{ "'赢了？我赢了？！那个声音——是你的功劳。谢谢。'" }
            });
            AddQuest(new QuestData {
                id="ds_03", title="反击·真相在眼前", type=QuestType.Talk, status=QuestStatus.Available,
                giverNpcId="", giverName="命运",
                description="明长老再次设局陷害你——这次是栽赃你偷了宗门秘典。但你早已准备好了证据。在所有人面前——揭穿他。",
                targetId="", targetCount=3,
                rewardSpiritStones=200, rewardCultivation=80, rewardItemId="item_evidence_scroll",
                nextQuestId="ds_04",
                completionText="宗门炸开了锅。那个任人欺负的懦夫——居然在所有人面前揭穿了真相。",
                dialogueOnAccept=new List<string>{ "（坚定）'够了。这次——我不躲了。'" },
                dialogueOnComplete=new List<string>{ "'你看——所有人的表情。他们不敢相信。不敢相信我会反抗。'" }
            });
            AddQuest(new QuestData {
                id="ds_04", title="决战·元婴之怒", type=QuestType.Boss, status=QuestStatus.Available,
                giverNpcId="npc_tian_xuanzi", giverName="天玄子",
                description="天玄子（师父）察觉到了你的变化。他派出了大弟子——元婴中期修为——来'清理门户'。这一次，你不再隐藏真正的实力。",
                targetId="boss_senior_001", targetCount=1,
                rewardSpiritStones=500, rewardCultivation=200, rewardItemId="item_dualsoul_awaken_core",
                nextQuestId="ds_05",
                completionText="天象变色。元婴之威震动整个宗门。所有人——都在颤抖。",
                dialogueOnAccept=new List<string>{ "（冷笑）'他们想看看我有多强？好。让他们看。'" },
                dialogueOnComplete=new List<string>{ "'这就是……我的力量？不——这是我们的力量。'" }
            });
            AddQuest(new QuestData {
                id="ds_05", title="真相·二十年骗局", type=QuestType.Talk, status=QuestStatus.Available,
                giverNpcId="npc_tian_xuanzi", giverName="天玄子",
                description="天玄子召你进入他的闭关密室。二十年的疑惑即将揭晓——为什么师父养大你却从不真正教你？为什么所有人都在欺负你而他视而不见？因为你的身体——是万年难遇的'道胎'。他养你——只是为了炼你。",
                targetId="", targetCount=1,
                rewardSpiritStones=1000, rewardCultivation=500, rewardItemId="item_dualsoul_final_relic",
                nextQuestId="",
                completionText="真相大白。道胎的秘密。二十年的骗局。以及——一个新世界的开始。",
                dialogueOnAccept=new List<string>{ "（平静）'走吧。到该去的地方去。'" },
                dialogueOnComplete=new List<string>{ "'二十年——原来我从来不是他弟子。我是他的丹药。但你——你是真的。只有你是真的。'" }
            });
        }

        public void AddQuest(QuestData q) { _allQuests[q.id] = q; }

        void Update() { CheckExploreQuests(); }

        public bool AcceptQuest(string questId)
        {
            if (!_allQuests.ContainsKey(questId)) return false;
            var q = _allQuests[questId];
            if (q.status != QuestStatus.Available) return false;
            q.status = QuestStatus.Accepted;
            _activeQuests.Add(q);
            if (q.dialogueOnAccept.Count > 0) Debug.Log($"[任务] 📋 {q.giverName}：'{q.dialogueOnAccept[0]}'");
            Debug.Log($"[任务] 📋 接受：{q.title}");
            EventBus.Publish("OnQuestAccepted", new Dictionary<string, object> {{"questId", q.id}, {"title", q.title}});
            return true;
        }

        /// <summary>
        /// 由DualSoulQuestChain等外部系统调用，完成并同步任务状态到QuestManager
        /// </summary>
        public void CompleteQuestById(string questId)
        {
            if (_allQuests.TryGetValue(questId, out var q))
            {
                CompleteQuest(q);
            }
        }

        void CompleteQuest(QuestData q)
        {
            if (q.status == QuestStatus.Completed) return;
            q.status = QuestStatus.Completed; questsCompleted++;
            _activeQuests.Remove(q);

            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                if (q.rewardSpiritStones > 0) stats.AddSpiritStone(q.rewardSpiritStones);
                if (q.rewardCultivation > 0) stats.AddCultivation(q.rewardCultivation);
            }
            if (!string.IsNullOrEmpty(q.rewardItemId)) InventoryManager.Instance?.AddItem(new Item { id=q.rewardItemId, name=q.rewardItemId, quantity=1, value=50 });

            Debug.Log($"[任务] ✅ 完成：{q.title}！+{q.rewardSpiritStones}灵石 +{q.rewardCultivation}修为");
            if (!string.IsNullOrEmpty(q.completionText)) Debug.Log($"[任务] {q.completionText}");

            EventBus.Publish("OnQuestCompleted", new Dictionary<string, object> {{"questId", q.id}, {"title", q.title}});

            // 解锁下一个任务
            if (!string.IsNullOrEmpty(q.nextQuestId) && _allQuests.ContainsKey(q.nextQuestId))
                _allQuests[q.nextQuestId].status = QuestStatus.Available;
        }

        void OnNpcInteract(Dictionary<string, object> data)
        {
            string npcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            foreach (var q in _activeQuests)
            {
                if (q.type == QuestType.Talk && q.targetId == npcId && q.status == QuestStatus.Accepted)
                { q.currentCount++; if (q.currentCount >= q.targetCount) CompleteQuest(q); }
            }
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            string eId = data.ContainsKey("enemyId") ? data["enemyId"]?.ToString() ?? "" : "";
            foreach (var q in _activeQuests)
            {
                if ((q.type == QuestType.Combat || q.type == QuestType.Boss) && q.status == QuestStatus.Accepted)
                {
                    if (eId.StartsWith("wolf_") && q.targetId.StartsWith("wolf_")) q.currentCount++;
                    else if (eId == q.targetId) q.currentCount++;
                    if (q.currentCount >= q.targetCount) CompleteQuest(q);
                }
            }
        }

        void OnItemCollected(Dictionary<string, object> data)
        {
            string itemId = data.ContainsKey("itemId") ? data["itemId"]?.ToString() ?? "" : "";
            int qty = data.ContainsKey("quantity") ? (int)data["quantity"] : 1;
            foreach (var q in _activeQuests)
            {
                if (q.type == QuestType.Collect && q.targetId == itemId && q.status == QuestStatus.Accepted)
                { q.currentCount += qty; if (q.currentCount >= q.targetCount) CompleteQuest(q); }
            }
        }

        public QuestData GetQuestFromNPC(string npcId) { return _allQuests.Values.FirstOrDefault(q => q.giverNpcId == npcId && q.status == QuestStatus.Available); }

        void CheckExploreQuests()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            foreach (var q in _activeQuests)
            {
                if (q.type == QuestType.Explore && q.status == QuestStatus.Accepted)
                {
                    var target = GameObject.Find(q.targetId);
                    if (target != null && Vector3.Distance(player.transform.position, target.transform.position) < 5f)
                        CompleteQuest(q);
                }
            }
        }

        public List<QuestData> GetActiveQuests() => _activeQuests.FindAll(q => q.status == QuestStatus.Accepted);
        public List<QuestData> GetAvailableQuests() => new(_allQuests.Values.Where(q => q.status == QuestStatus.Available));
    }
}
