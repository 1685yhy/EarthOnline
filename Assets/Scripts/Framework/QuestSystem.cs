using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 简易任务系统 —— 接受/完成/追踪任务。
    /// </summary>
    [System.Serializable]
    public class Quest
    {
        public string id;
        public string title;
        public string description;
        public string npcGiverId;
        public string npcName;
        public string type;          // Talk, Collect, Kill, Explore
        public string targetId;      // 目标物品/NPC ID
        public int targetCount;
        public int currentCount;
        public int rewardGold;
        public int rewardExp;
        public bool isCompleted;
        public bool isAccepted;
        public List<string> dialogueOnAccept;
        public List<string> dialogueOnComplete;
    }

    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        private List<Quest> _activeQuests = new List<Quest>();
        private List<string> _completedQuestIds = new List<string>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterQuests();
            EventBus.Subscribe("OnNPCInteract", OnNPCInteracted);
            EventBus.Subscribe("OnItemAdded", OnItemPickedUp);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
        }

        void RegisterQuests()
        {
            // 任务1：帮张老采药
            RegisterQuest(new Quest
            {
                id = "quest_herb_001", title = "张老的请求", type = "Collect",
                npcGiverId = "npc_zhang_001", npcName = "张老",
                description = "张老需要3株止血草来炼制丹药。在村庄周围可以找到发绿光的药草。",
                targetId = "item_herb_001", targetCount = 3,
                rewardGold = 50, rewardExp = 30,
                dialogueOnAccept = new List<string> {
                    "老夫最近身体不适，需要几株止血草炼药。你能帮我去找找吗？3株就够了。",
                    "止血草就在村子周围，发绿光的就是。"
                },
                dialogueOnComplete = new List<string> {
                    "好小子！效率不错。这几颗灵石你收着。……另外，老夫观察到一件事：村子西边的山里有异动，最近别往那边去。"
                }
            });

            // 任务2：帮铁匠送剑
            RegisterQuest(new Quest
            {
                id = "quest_sword_001", title = "铁匠的订单", type = "Talk",
                npcGiverId = "npc_wang_001", npcName = "王铁柱",
                description = "王铁柱打好了一把剑，需要你送到李灵儿那里。",
                targetId = "npc_li_001", targetCount = 1,
                rewardGold = 80, rewardExp = 50,
                dialogueOnAccept = new List<string> {
                    "嘿！你可算来了。我这儿有把剑是要送给李灵儿的，但我这儿走不开。你替我跑一趟？",
                    "她就在村子南边的药铺。"
                },
                dialogueOnComplete = new List<string> {
                    "送到了？好嘞！这是给你的跑腿费。对了，李灵儿有没有说最近山里采不到药的事？最近确实不太平啊..."
                }
            });

            // 任务3：消灭野狼
            RegisterQuest(new Quest
            {
                id = "quest_wolf_001", title = "清除狼患", type = "Kill",
                npcGiverId = "npc_li_001", npcName = "李灵儿",
                description = "村子周围的野狼越来越多了，李灵儿担心采药人会受伤。消灭2只野狼。",
                targetId = "wolf_001", targetCount = 2,
                rewardGold = 100, rewardExp = 80,
                dialogueOnAccept = new List<string> {
                    "村子外面的野狼越来越多了...我一个人不敢出去采药。你能帮我清理一下吗？两只就够了，剩下的我自己来。"
                },
                dialogueOnComplete = new List<string> {
                    "太好了！这下安全多了。这些丹药你拿着，是我自己炼的。对了，你有没有在村子北边看到一个黑色的漩涡？那里最近散发出很强的灵力波动..."
                }
            });

            // 任务4：探索地下城入口
            RegisterQuest(new Quest
            {
                id = "quest_dungeon_001", title = "虚空裂缝", type = "Explore",
                npcGiverId = "npc_zhang_001", npcName = "张老",
                description = "张老感知到村子北边有一个异常的空间裂缝。去调查一下那个紫色漩涡。",
                targetId = "DungeonEntrance", targetCount = 1,
                rewardGold = 200, rewardExp = 150,
                dialogueOnAccept = new List<string> {
                    "你也感觉到了吧？村子北边的异常灵力。那里有一个空间裂缝。老夫年纪大了，走不动了。你去看看是什么情况。注意安全——裂缝附近有强大的守护者。"
                },
                dialogueOnComplete = new List<string> {
                    "果然...虚空行者。那是从裂缝中跑出来的怪物。看来这个世界的屏障比我想象的更脆弱。'天陨丹方'...可能和这个裂缝有关。"
                }
            });
        }

        void RegisterQuest(Quest q)
        {
            _activeQuests.Add(q);
            Debug.Log($"[QuestManager] Quest registered: {q.title}");
        }

        void OnNPCInteracted(Dictionary<string, object> data)
        {
            string npcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            // Check if any quest from this NPC
            foreach (var q in _activeQuests)
            {
                if (q.isAccepted && !q.isCompleted && q.type == "Talk" && q.targetId == npcId)
                {
                    CompleteQuest(q);
                    return;
                }
            }
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            string enemyId = data.ContainsKey("enemyId") ? data["enemyId"].ToString() : "";
            foreach (var q in _activeQuests)
            {
                if (q.isAccepted && !q.isCompleted && q.type == "Kill"
                    && (q.targetId == enemyId || q.targetId.StartsWith("wolf_") && enemyId.StartsWith("wolf_")))
                {
                    q.currentCount++;
                    Debug.Log($"[Quest] {q.title}: {q.currentCount}/{q.targetCount}");
                    if (q.currentCount >= q.targetCount)
                        CompleteQuest(q);
                }
            }
        }

        void Update()
        {
            // Explore quests: check if player is near target
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            foreach (var q in _activeQuests)
            {
                if (q.isAccepted && !q.isCompleted && q.type == "Explore")
                {
                    var target = GameObject.Find(q.targetId);
                    if (target != null)
                    {
                        float dist = Vector3.Distance(player.transform.position, target.transform.position);
                        if (dist <= 5f)
                            CompleteQuest(q);
                    }
                }
            }
        }

        void OnItemPickedUp(Dictionary<string, object> data)
        {
            string itemId = data.ContainsKey("itemId") ? data["itemId"].ToString() : "";
            int qty = data.ContainsKey("quantity") ? (int)data["quantity"] : 1;

            foreach (var q in _activeQuests)
            {
                if (q.isAccepted && !q.isCompleted && q.type == "Collect" && q.targetId == itemId)
                {
                    q.currentCount += qty;
                    Debug.Log($"[Quest] {q.title}: {q.currentCount}/{q.targetCount}");
                    if (q.currentCount >= q.targetCount)
                        CompleteQuest(q);
                }
            }
        }

        public Quest GetQuestFromNPC(string npcId)
        {
            return _activeQuests.Find(q => q.npcGiverId == npcId && !q.isCompleted);
        }

        public void AcceptQuest(Quest q)
        {
            if (q.isAccepted) return;
            q.isAccepted = true;
            Debug.Log($"[Quest] 接受任务: {q.title}");
            if (q.dialogueOnAccept != null)
                foreach (var line in q.dialogueOnAccept)
                    Debug.Log($"[Quest:{q.title}] NPC: {line}");

            EventBus.Publish("OnQuestAccepted", new Dictionary<string, object> {
                {"questId", q.id}, {"title", q.title}
            });
        }

        void CompleteQuest(Quest q)
        {
            q.isCompleted = true;
            _completedQuestIds.Add(q.id);

            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.AddSpiritStone(q.rewardGold);
                stats.AddCultivation(q.rewardExp);
            }

            Debug.Log($"[Quest] ✅ 完成: {q.title}! +{q.rewardGold}灵石 +{q.rewardExp}修为");
            if (q.dialogueOnComplete != null)
                foreach (var line in q.dialogueOnComplete)
                    Debug.Log($"[Quest:{q.title}] NPC: {line}");

            EventBus.Publish("OnQuestCompleted", new Dictionary<string, object> {
                {"questId", q.id}, {"title", q.title},
                {"rewardGold", q.rewardGold}, {"rewardExp", q.rewardExp}
            });
        }

        public List<Quest> GetActiveQuests()
        {
            return _activeQuests.FindAll(q => q.isAccepted && !q.isCompleted);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnNPCInteracted);
            EventBus.Unsubscribe("OnItemAdded", OnItemPickedUp);
            EventBus.Unsubscribe("OnEnemyKilled", OnEnemyKilled);
        }
    }
}
