using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 反派系统 —— 有名字、有动机、有行为逻辑的对手。
    /// 不是"虚空"这种抽象概念——是会主动来找你麻烦的人。
    /// </summary>
    [System.Serializable]
    public class Antagonist
    {
        public string id, name, title;
        public string motivation;           // 为什么和你过不去
        public string background;           // 背景故事
        public int power;                   // 实力(影响攻击力)
        public int progress;                // 对抗进度(0-100，满了触发决战)
        public bool defeated;               // 是否已被击败
        public float nextActionTime;        // 下次行动时间
        public string currentScheme;        // 当前在谋划什么

        public void AdvanceProgress(int amount)
        {
            progress = Mathf.Min(progress + amount, 100);
        }
    }

    public class AntagonistSystem : MonoBehaviour
    {
        public static AntagonistSystem Instance { get; private set; }

        public List<Antagonist> antagonists = new();
        private float _schemeInterval = 180f; // 每3分钟反派行动一次

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            CreateAntagonists();
            EventBus.Subscribe("OnPlayerLevelUp", OnPlayerGrew);
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
        }

        void CreateAntagonists()
        {
            antagonists = new List<Antagonist>
            {
                new Antagonist {
                    id="ant_liu", name="刘总管", title="天元宗外务总管",
                    motivation="你在调查天元宗用人血炼丹的秘密。刘总管奉命让你'闭嘴'——用任何必要的手段。",
                    background="天元宗外务总管。三十年前——就是他亲手废掉了李灵儿父亲的修为。他享受那个过程。",
                    power=30, currentScheme="派眼线监视你的行动"
                },
                new Antagonist {
                    id="ant_hunter", name="猎血人", title="血脉猎人",
                    motivation="你是青龙血脉的觉醒者。你的血——是炼制'升仙丹'的极品材料。",
                    background="没有人知道他的真面目。只知道他猎杀血脉觉醒者——然后把他们的尸体卖给天元宗。他是这片大陆上最危险的不是修士的'修士'。",
                    power=50, currentScheme="在森林中设置陷阱"
                },
                new Antagonist {
                    id="ant_rival", name="莫问", title="穿越者·第46号",
                    motivation="在你之前的那个穿越者。他没有死——他在虚空里待了十年。他出来了。他恨每一个穿越者——因为没有人回来找他。",
                    background="第46号穿越者。曾经和你一样——被地球意志选中，投放到灵气大陆。他也抽到了金手指。他也想拯救世界。但虚空改变了他。现在他只想让所有穿越者体验他经历过的痛苦。",
                    power=80, currentScheme="在虚空中观察你...等待时机"
                },
            };
        }

        void Update()
        {
            foreach (var ant in antagonists)
            {
                if (ant.defeated) continue;
                if (Time.time >= ant.nextActionTime)
                {
                    ant.nextActionTime = Time.time + Random.Range(_schemeInterval * 0.5f, _schemeInterval * 1.5f);
                    AntagonistAct(ant);
                }
            }
        }

        void AntagonistAct(Antagonist ant)
        {
            switch (ant.currentScheme)
            {
                case "派眼线监视你的行动":
                    Debug.Log($"[反派] 👁️ {ant.name}的眼线在跟踪你...");
                    ant.AdvanceProgress(5);
                    break;

                case "在森林中设置陷阱":
                    var stats = PlayerStats.Instance;
                    if (Random.value < 0.3f && stats != null)
                    {
                        int dmg = ant.power / 3;
                        stats.TakeDamage(dmg);
                        Debug.Log($"[反派] 🪤 {ant.name}的陷阱！你受了{dmg}点伤害。");
                    }
                    ant.AdvanceProgress(8);
                    break;

                case "在虚空中观察你...等待时机":
                    Debug.Log($"[反派] 🌑 你感到一双眼睛在虚空中注视着你。{ant.name}在等你犯错。");
                    ant.AdvanceProgress(3);
                    break;
            }

            // 进度满了→决战事件
            if (ant.progress >= 100 && !ant.defeated)
            {
                TriggerShowdown(ant);
            }

            // 反派也会说一些话（通过流言系统）
            if (Random.value < 0.3f)
            {
                Debug.Log($"[反派] '{ant.name}'的传闻在坊间流传：{ant.background.Substring(0, Mathf.Min(50, ant.background.Length))}...");
            }
        }

        void TriggerShowdown(Antagonist ant)
        {
            Debug.Log($"⚔️ ═══════════════════════════════");
            Debug.Log($"⚔️  【决战】{ant.title}·{ant.name}");
            Debug.Log($"⚔️  {ant.motivation}");
            Debug.Log($"⚔️  实力:{ant.power} | 对抗进度:100%");
            Debug.Log($"⚔️  他来找你了。这一战无法避免。");
            Debug.Log($"⚔️ ═══════════════════════════════");

            EventBus.Publish("OnShowdownTriggered", new Dictionary<string, object> {
                {"antagonistId", ant.id}, {"name", ant.name}, {"power", ant.power}
            });
        }

        void OnPlayerGrew(Dictionary<string, object> data)
        {
            int lv = (int)data["level"];
            // 玩家越强→反派越紧张→行动加速
            foreach (var ant in antagonists)
            {
                if (!ant.defeated && lv >= 5)
                    ant.AdvanceProgress(10);
            }
        }

        void OnDayPassed(Dictionary<string, object> data)
        {
            foreach (var ant in antagonists)
            {
                if (!ant.defeated) ant.AdvanceProgress(2);
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnPlayerLevelUp", OnPlayerGrew);
            EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
        }
    }
}
