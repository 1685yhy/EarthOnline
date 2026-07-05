using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 老爷爷金手指 —— 戒指中的古老灵魂。
    /// 故事：一位上古炼丹宗师，渡劫失败后将残魂封印于一枚黑铁戒指。
    /// 能力：炼丹指导(Lv.1) → 战斗预判(Lv.2) → 灵魂附体(Lv.3)。
    /// 暗线：有人在追杀所有知道"天陨丹方"的人...
    /// </summary>
    public class OldMaster : GiftBase
    {
        public enum MasterState { Sleeping, Awake, Teaching, Warning, Empowered }
        public MasterState State { get; private set; } = MasterState.Sleeping;

        private int _cultivationBoost;
        private int _adviceGiven;
        private int _emergencySaves;

        public override void Initialize(Dictionary<string, object> config)
        {
            base.Initialize(config);
            _cultivationBoost = 0;
            _adviceGiven = 0;
            _emergencySaves = 0;
        }

        public override void Activate()
        {
            IsActive = true;
            State = MasterState.Awake;

            string[] awakenLines = {
                "...这是哪儿？",
                "老夫沉睡了多久？...小子，现在是何年月？",
                "哼，又一个捡到戒指的幸运儿。也罢，既然你能唤醒老夫，便是缘分。",
            };
            Debug.Log($"[{GiftName}] {awakenLines[Random.Range(0, awakenLines.Length)]}");

            EventBus.Publish("OnOldMasterAwake", new Dictionary<string, object>
            {
                {"giftId", GiftId}, {"name", GiftName}
            });
        }

        public override void Deactivate()
        {
            IsActive = false;
            State = MasterState.Sleeping;
        }

        public override void Upgrade()
        {
            Level++;
            switch (Level)
            {
                case 2: State = MasterState.Teaching; break;
                case 3: State = MasterState.Warning; break;
                case 4: State = MasterState.Empowered; break;
            }
            Debug.Log($"[{GiftName}] 升级到 Lv.{Level} —— 状态:{State}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "ask_advice": GiveAdvice(); break;
                case "cultivate": BoostCultivation(); break;
                case "emergency": EmergencySave(); break;
                case "talk": Talk(); break;
                default: Debug.LogWarning($"[{GiftName}] Unknown ability: {abilityName}"); break;
            }
        }

        void GiveAdvice()
        {
            _adviceGiven++;
            string[] advices = {
                $"小子，这个世界的灵气流动不对。{(_adviceGiven >= 3 ? "你还记得三日前山崖上的异象吗？" : "多观察，少莽撞。")}",
                "炼丹之道，火候第一，材料第二。你身上那些草药，留着别乱用。",
                "有人在跟踪你。不是这个世界的人...和老夫一样，来自'那边'。",
                "你手上的签到系统...老夫感觉到一丝熟悉的气息。制造它的文明，老夫曾去过。",
            };
            string advice = advices[Random.Range(0, advices.Length)];
            Debug.Log($"[{GiftName}] 『{advice}』");

            EventBus.Publish("OnOldMasterAdvice", new Dictionary<string, object>
            {
                {"advice", advice}, {"count", _adviceGiven}
            });

            if (_adviceGiven >= 5 && StoryProgress == 0) AdvanceStory(1);
        }

        void BoostCultivation()
        {
            _cultivationBoost++;
            int boostAmount = 10 * Level;
            Debug.Log($"[{GiftName}] 为你灌注了一丝灵力，修为+{boostAmount} (累计{_cultivationBoost}次)");

            EventBus.Publish("OnCultivationBoost", new Dictionary<string, object>
            {
                {"amount", boostAmount}, {"total_boosts", _cultivationBoost}
            });
        }

        void EmergencySave()
        {
            _emergencySaves++;
            Debug.Log($"[{GiftName}] 『小子，退后！！』—— 一道金色屏障挡在你面前！");
            Debug.Log($"[{GiftName}] 老夫残魂之力有限，还能救你 {3 - _emergencySaves} 次...");

            EventBus.Publish("OnEmergencySave", new Dictionary<string, object>
            {
                {"remaining", 3 - _emergencySaves}
            });
        }

        void Talk()
        {
            string[] talks = {
                $"老夫生前乃天元宗首席炼丹师。这枚戒指...是老夫一生最后的作品。",
                $"你很好奇为什么一个残魂能活这么久？因为{(_adviceGiven >= 5 ? "天陨丹方" : "...算了，现在告诉你还太早")}。",
            };
            Debug.Log($"[{GiftName}] {talks[Random.Range(0, talks.Length)]}");
        }

        public override GiftDisplayInfo GetDisplayInfo()
        {
            return new GiftDisplayInfo
            {
                Name = GiftName,
                Type = GiftType,
                Rarity = Rarity,
                Level = Level,
                Description = $"状态:{State} | 指导{_adviceGiven}次 | 修炼{_cultivationBoost}次\n" +
                    $"Lv.1 炼丹指导 | Lv.2 战斗预判 | Lv.3 灵魂附体",
                Abilities = new List<string> { "ask_advice", "cultivate", "emergency", "talk" },
                StoryHint = _adviceGiven >= 5
                    ? "天陨丹方 —— 这个禁忌的名字，究竟意味着什么？"
                    : "多向老爷爷请教，他会慢慢讲述自己的故事..."
            };
        }

        public override string GetStoryMilestoneDescription(int milestoneIndex)
        {
            return milestoneIndex switch
            {
                1 => "【第5次请教】老爷爷沉默了很久，终于说出了那个名字：'天陨丹方'。那是他渡劫失败的原因，也是他被追杀至今的秘密。那些人...还在找它。",
                2 => "【第15次请教】你发现了一个可怕的事实：追杀老爷爷的组织，和制造'签到系统'的文明，是同一个人建立的。而这个人，似乎认识你...",
                3 => "【第30次请教】老爷爷燃烧最后的残魂之力，为你打开了通往'真相'的门。'小子...记住...天陨丹方的最后一个字，在你的血液里...'",
                _ => $"Milestone {milestoneIndex}"
            };
        }
    }
}
