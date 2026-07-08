using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// M3 主线任务链 —— 地球意志→虚空危机→终极选择。
    /// 12个任务，分5章。引导玩家从初临到决战。
    /// </summary>
    public class MainQuestChain : MonoBehaviour
    {
        public static MainQuestChain Instance { get; private set; }

        public enum Chapter { Prologue, VoidShadow, SectDarkness, GatheringPower, FinalBattle }
        public Chapter currentChapter = Chapter.Prologue;
        public int mainQuestProgress;

        private List<QuestData> _mainQuests = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            BuildMainQuestChain();
        }

        void BuildMainQuestChain()
        {
            // Chapter 1: Prologue (3 quests) - 初临异界
            AddMainQuest("mq_01", "初临灵气大陆", "和村子里的张老交谈——他知道你为什么来这里。",
                "npc_zhang_001", QuestType.Talk, "npc_zhang_001", 1, 30, 20);
            AddMainQuest("mq_02", "认识世界", "和至少3个村民交谈，了解这个世界的基本情况。",
                "npc_zhang_001", QuestType.Talk, "", 3, 20, 15);
            AddMainQuest("mq_03", "第一次战斗", "击败1只野狼——证明你有在这个世界生存的能力。",
                "npc_wang_001", QuestType.Combat, "wolf_001", 1, 50, 30);

            // Chapter 2: Void Shadow (3 quests) - 发现虚空威胁
            AddMainQuest("mq_04", "虚空裂缝", "去北方调查虚空裂缝——那里有不该出现的东西。",
                "npc_zhang_001", QuestType.Explore, "DungeonEntrance", 1, 80, 50);
            AddMainQuest("mq_05", "张老的过去", "和张老深入对话——了解他和虚空的关系。",
                "npc_zhang_001", QuestType.Talk, "npc_zhang_001", 3, 40, 30);
            AddMainQuest("mq_06", "收集情报", "从赵掌柜和李灵儿那里收集关于虚空的情报。",
                "npc_zhao_001", QuestType.Talk, "", 2, 60, 40);

            // Chapter 3: Sect Darkness (3 quests) - 揭露天元宗的秘密
            AddMainQuest("mq_07", "天元宗的秘密", "李灵儿告诉了你天元宗用人血炼丹的真相。收集证据。",
                "npc_li_001", QuestType.Collect, "item_void_crystal", 2, 100, 60);
            AddMainQuest("mq_08", "背叛者", "王铁柱的弟弟——那个杀了天元宗长老的铸剑师——有消息了。找到他。",
                "npc_wang_001", QuestType.Talk, "npc_wang_001", 5, 80, 50);
            AddMainQuest("mq_09", "与虎谋皮", "反派刘总管提出交易——他帮你对抗虚空，你停止调查天元宗。你的选择？",
                "npc_zhang_001", QuestType.Talk, "", 1, 0, 0);

            // Chapter 4: Gathering Power (2 quests)
            AddMainQuest("mq_10", "收集力量", "击败虚空裂缝的守护者——虚空行者。证明你有资格面对更大的威胁。",
                "npc_zhao_001", QuestType.Boss, "boss_001", 1, 200, 100);
            AddMainQuest("mq_11", "远古遗产", "在世界回响中寻找远古文明留下的武器——他们也在对抗虚空。",
                "npc_zhang_001", QuestType.Explore, "", 3, 150, 80);

            // Chapter 5: Final Battle (1 quest)
            AddMainQuest("mq_12", "决战虚空", "虚空已经知道你。它来了。去虚空裂缝——面对它。你的选择将决定这个世界的命运。",
                "npc_zhang_001", QuestType.Boss, "boss_001", 1, 500, 300);
        }

        void AddMainQuest(string id, string title, string desc, string giver, QuestType type, string target, int count, int stones, int cult)
        {
            _mainQuests.Add(new QuestData
            {
                id = id, title = title, description = desc,
                giverNpcId = giver, giverName = giver,
                type = type, targetId = target, targetCount = count,
                rewardSpiritStones = stones, rewardCultivation = cult,
                status = QuestStatus.Available,
                completionText = $"主线任务完成：{title}"
            });
        }
    }
}
