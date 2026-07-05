using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 血脉觉醒 —— 上古青龙血脉。越觉醒越不像人。
    /// 代价：龙化进度——太高了会被修士猎杀，但能力极强。
    /// </summary>
    public class BloodlineAwakening : GiftBase
    {
        public int DragonProgress { get; private set; } // 0-100，龙化程度
        public float PowerBonus => 1f + DragonProgress * 0.02f; // 每1%龙化+2%战力

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); DragonProgress = 5; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🐉 青龙血脉觉醒。龙化:{DragonProgress}%。");
            Debug.Log($"[{GiftName}] '你体内流淌着远古神兽的血。但人族的修士——会把你当成炼丹材料。'");
        }

        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            DragonProgress = Mathf.Min(DragonProgress + 15, 100);
            Debug.Log($"[{GiftName}] 血脉进化！龙化:{DragonProgress}% 战力+{PowerBonus:F1}x");

            if (DragonProgress >= 50 && StoryProgress == 0) AdvanceStory(1);
            if (DragonProgress >= 80) AdvanceStory(2);
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "dragon_roar": DragonRoar(); break;
                case "dragon_scales": DragonScales(); break;
                case "partial_transform": PartialTransform(); break;
                case "status": ShowStatus(); break;
            }
        }

        void DragonRoar()
        {
            DragonProgress = Mathf.Min(DragonProgress + 2, 100);
            Debug.Log($"[{GiftName}] 🐉 龙吟！震慑周围所有敌人3秒。龙化+2%→{DragonProgress}%");
            if (DragonProgress >= 50) Debug.Log($"[{GiftName}] ⚠️ 你的眼睛变成了竖瞳。有修士开始用异样的眼光看你...");
        }

        void DragonScales()
        {
            DragonProgress = Mathf.Min(DragonProgress + 1, 100);
            Debug.Log($"[{GiftName}] 🛡️ 龙鳞护体！防御大幅提升30秒。龙化+1%。");
        }

        void PartialTransform()
        {
            if (Level < 3) { Debug.Log($"[{GiftName}] 需要Lv.3"); return; }
            DragonProgress = Mathf.Min(DragonProgress + 5, 100);
            Debug.Log($"[{GiftName}] 🐲 部分龙化！战力飙升300%，持续10秒。但龙化+5%。");
            Debug.Log($"[{GiftName}] '你的手臂覆盖了青色的鳞片。这不是属于人类的力量。'");
        }

        void ShowStatus()
        {
            string warning = DragonProgress switch { >= 80 => "⚠️ 修士已在猎杀你！", >= 50 => "⚡ 有人开始注意到你...", _ => "" };
            Debug.Log($"[{GiftName}] 青龙血脉 Lv.{Level} | 龙化:{DragonProgress}% | 战力×{PowerBonus:F1} {warning}");
        }

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"龙化{DragonProgress}% | 战力×{PowerBonus:F1}\n龙吟(Lv.1) | 龙鳞(Lv.1) | 部分龙化(Lv.3)",
            Abilities = new List<string> { "dragon_roar", "dragon_scales", "partial_transform(Lv.3)", "status" },
            StoryHint = DragonProgress >= 50 ? "天元宗的长老看你的眼神变了。那不是好奇——那是贪婪。" : "你的血液里，有什么东西在苏醒..."
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "天元宗的外门弟子开始在远处偷偷观察你。有一个黑衣人在你练功的地方留下了标记。他们不是来看你的——是来踩点的。",
            2 => "你发现你不是唯一一个血脉觉醒者。这片大陆上，有人在猎杀所有血脉觉醒者，用他们的血液炼制'升仙丹'。猎杀者的总部——就在天元宗。",
            _ => $"Milestone {i}"
        };
    }
}
