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
                stats.AddCurrency(q.rewardGold);
                stats.AddCultivation(q.rewardExp);
            }

            Debug.Log($"[Quest] ✅ 完成: {q.title}! +{q.rewardGold}金币 +{q.rewardExp}修为");
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
        }
    }
}
