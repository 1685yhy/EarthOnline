using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// M3 反派故事线 —— 三个反派各有完整的故事弧线。
    /// 不是"打败Boss"——是理解他们为什么变成Boss。
    /// </summary>
    public class VillainStoryline : MonoBehaviour
    {
        public static VillainStoryline Instance { get; private set; }

        public enum VillainPhase { Dormant, Emerging, Active, Confrontation, Defeated, Redeemed }

        [System.Serializable]
        public class VillainState
        {
            public string id, name;
            public VillainPhase phase;
            public int encounterCount;
            public List<string> storyBeats = new(); // 已触发故事节点
            public string currentDialogue;
        }

        public List<VillainState> villains = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupVillains();
            EventBus.Subscribe("OnPlayerLevelUp", OnPlayerProgress);
            EventBus.Subscribe("OnQuestCompleted", OnQuestProgress);
        }

        void SetupVillains()
        {
            villains = new List<VillainState>
            {
                new VillainState {
                    id="liu", name="刘总管", phase=VillainPhase.Emerging,
                    currentDialogue="天元宗的秘密不是你能碰的。收手吧——我可以当没看见。"
                },
                new VillainState {
                    id="hunter", name="猎血人", phase=VillainPhase.Dormant,
                    currentDialogue="你的血脉——很特别。我在远处就能闻到。我们还会再见的。"
                },
                new VillainState {
                    id="rival", name="莫问(#46)", phase=VillainPhase.Dormant,
                    currentDialogue="...你身上有和我一样的印记。地球意志选了你——就像当年选了我。小心——它不会保护你的。"
                },
            };
        }

        public void AdvanceVillain(string id)
        {
            var v = villains.Find(x => x.id == id);
            if (v == null) return;
            v.encounterCount++;

            v.phase = v.encounterCount switch
            {
                1 => VillainPhase.Emerging,
                3 => VillainPhase.Active,
                5 => VillainPhase.Confrontation,
                _ => v.phase
            };

            // 每个阶段反派说不同的话
            v.currentDialogue = v.id switch
            {
                "liu" => v.phase switch
                {
                    VillainPhase.Emerging => "最后一次警告——离开天元宗的事。否则——李灵儿就是下场。",
                    VillainPhase.Active => "你以为你在做好事？天元宗用人血炼丹——是为了对抗虚空！没有我们的丹药——这片大陆早被虚空吞了！",
                    VillainPhase.Confrontation => "来吧——让我看看你的正义能走多远。但记住——杀了我，虚空就会赢。选择吧。",
                    VillainPhase.Redeemed => "我...为天元宗做了那些事。我毁了多少人的一生。但虚空面前——我别无选择。拿着这个——这是天元宗虚空研究的全部资料。用它——代替我。",
                    _ => v.currentDialogue
                },
                "hunter" => v.phase switch
                {
                    VillainPhase.Emerging => "你的血——太完美了。升仙丹的最后一位材料。我会来找你的——但不是今天。",
                    VillainPhase.Active => "你以为我在猎杀血脉觉醒者？我在保护他们！天元宗——已经抓了三个——用他们的血在炼制升仙丹——不是让人飞升——是让人变成虚空的傀儡！我杀了他们——免得他们变成怪物！",
                    VillainPhase.Confrontation => "最后一个血脉觉醒者——就是你。你的血能炼成对抗虚空的药——或者毁灭世界的毒。选择权在你。",
                    _ => v.currentDialogue
                },
                "rival" => v.phase switch
                {
                    VillainPhase.Emerging => "你在做我当年做的事——相信地球意志，相信金手指，相信你能拯救世界。我花了十年才知道——这些都是谎言。",
                    VillainPhase.Active => "虚空不是敌人——虚空是牢笼。地球意志把我们投放到这里——不是让我们拯救世界——是让我们加固牢笼。每一个穿越者——都是一把锁。",
                    VillainPhase.Confrontation => "第47号和46号——我们本可以是战友。但我不会让你走我走过的路。虚空里的十年——我知道了一切。现在——你要听吗？",
                    VillainPhase.Redeemed => "我恨了你很久——因为你是新的穿越者而我不是。但现在——我只是累了。让我帮你——把真正的地球意志叫出来——让它回答我们的问题。",
                    _ => v.currentDialogue
                },
                _ => v.currentDialogue
            };

            Debug.Log($"[反派] {v.name}({v.phase}): '{v.currentDialogue}'");
        }

        void OnPlayerProgress(Dictionary<string, object> data)
        {
            int lv = (int)data["level"];
            if (lv >= 5) AdvanceVillain("liu");
            if (lv >= 8) AdvanceVillain("hunter");
            if (lv >= 12) AdvanceVillain("rival");
        }

        void OnQuestProgress(Dictionary<string, object> data)
        {
            string qId = data["questId"]?.ToString();
            if (qId == "mq_06") AdvanceVillain("liu");
            if (qId == "mq_09") AdvanceVillain("rival");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnPlayerLevelUp", OnPlayerProgress);
            EventBus.Unsubscribe("OnQuestCompleted", OnQuestProgress);
        }
    }
}
