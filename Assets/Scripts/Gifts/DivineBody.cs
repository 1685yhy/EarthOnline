using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 混沌圣体 —— 异能体质金手指。
    /// 故事：穿越时灵魂撕裂了空间裂缝，混沌之力灌入体内。
    /// 能力：自愈(Lv.1) → 感知(Lv.2) → 吞噬(Lv.3) → 领域(Lv.4)。
    /// 代价：每次使用高级能力都会吸引"虚空生物"的注意...
    /// </summary>
    public class DivineBody : GiftBase
    {
        private int _healCount;
        private int _threatLevel; // 0-10, higher = more danger

        public override void Initialize(Dictionary<string, object> config)
        {
            base.Initialize(config);
            _healCount = 0; _threatLevel = 0;
        }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 混沌圣体觉醒！你的身体开始自主吸收周围的灵气...");
            Debug.Log($"[{GiftName}] ⚠️ 警告：圣体每次使用高级能力都会提升'虚空威胁度'，{_threatLevel}/10。");
            EventBus.Publish("OnDivineBodyAwake", new Dictionary<string, object> {
                {"giftId", GiftId}, {"threatLevel", _threatLevel}
            });
        }

        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 圣体进化 Lv.{Level}!");
            switch (Level)
            {
                case 2: Debug.Log($"[{GiftName}] 新能力解锁：虚空感知 —— 探测周围10m内的灵物和危险。"); break;
                case 3: Debug.Log($"[{GiftName}] 新能力解锁：吞噬 —— 吸收敌人精华转化为自身修为。"); break;
                case 4: Debug.Log($"[{GiftName}] 新能力解锁：混沌领域 —— 3秒内无敌，但威胁度+3。"); break;
            }
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "heal": SelfHeal(); break;
                case "sense": Sense(); break;
                case "devour": Devour(); break;
                case "domain": Domain(); break;
                case "status": ShowStatus(); break;
                default: Debug.LogWarning($"[{GiftName}] Unknown: {abilityName}"); break;
            }
        }

        void SelfHeal()
        {
            _healCount++;
            int healAmount = 20 * Level;
            var stats = PlayerStats.Instance;
            if (stats != null) stats.Heal(healAmount);
            Debug.Log($"[{GiftName}] 自愈发动！+{healAmount}HP (第{_healCount}次)");

            if (_healCount >= 10 && StoryProgress == 0) AdvanceStory(1);
        }

        void Sense()
        {
            if (Level < 2) { Debug.Log($"[{GiftName}] 需要Lv.2解锁"); return; }
            _threatLevel = Mathf.Min(_threatLevel + 1, 10);

            var pickups = Object.FindObjectsOfType<WorldPickup>();
            Debug.Log($"[{GiftName}] 虚空感知 —— 探测到 {pickups.Length} 个灵物在附近。");
            foreach (var p in pickups)
            {
                float dist = Vector3.Distance(
                    PlayerStats.Instance != null ? PlayerStats.Instance.transform.position : Vector3.zero,
                    p.transform.position);
                Debug.Log($"  [{p.itemRarity}] {p.itemName} @ {dist:F1}m");
            }
            Debug.Log($"[{GiftName}] 威胁度: {_threatLevel}/10");

            if (_threatLevel >= 5) AdvanceStory(1);
        }

        void Devour()
        {
            if (Level < 3) { Debug.Log($"[{GiftName}] 需要Lv.3解锁"); return; }
            _threatLevel = Mathf.Min(_threatLevel + 2, 10);

            int expGain = 50 * Level;
            var stats = PlayerStats.Instance;
            if (stats != null) stats.AddCultivation(expGain);
            Debug.Log($"[{GiftName}] 吞噬发动！吸收周围灵气转化为{expGain}修为。威胁度:{_threatLevel}/10");

            if (_threatLevel >= 8) AdvanceStory(2);
        }

        void Domain()
        {
            if (Level < 4) { Debug.Log($"[{GiftName}] 需要Lv.4解锁"); return; }
            _threatLevel = Mathf.Min(_threatLevel + 3, 10);
            Debug.Log($"[{GiftName}] 混沌领域展开！！3秒无敌！威胁度暴增至{_threatLevel}/10！");
            Debug.Log($"[{GiftName}] 远处传来一声低沉的咆哮...有什么东西被惊醒了。");
            AdvanceStory(3);
        }

        void ShowStatus()
        {
            Debug.Log($"[{GiftName}] 混沌圣体 Lv.{Level} | 威胁度:{_threatLevel}/10 | 自愈{_healCount}次");
            Debug.Log($"  能力: heal(Lv.1) {(_threatLevel>=5?"| sense(Lv.2)":"")} {(_threatLevel>=10?"| devour(Lv.3)":"")}");
        }

        public override GiftDisplayInfo GetDisplayInfo()
        {
            return new GiftDisplayInfo
            {
                Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
                Description = $"混沌圣体 Lv.{Level} | 威胁度{_threatLevel}/10 | {_healCount}次自愈\n" +
                    "Lv.1自愈 | Lv.2感知 | Lv.3吞噬 | Lv.4领域",
                Abilities = new List<string> { "heal", "sense(Lv.2)", "devour(Lv.3)", "domain(Lv.4)", "status" },
                StoryHint = _threatLevel >= 5
                    ? "虚空中的东西已经注意到你了。它正在靠近..."
                    : "混沌之力在体内流淌。每次使用感知和吞噬都会增加'威胁度'..."
            };
        }

        public override string GetStoryMilestoneDescription(int milestoneIndex)
        {
            return milestoneIndex switch
            {
                1 => "【威胁度≥5】你在感知时发现了一个可怕的事实：那个虚空中的存在，一直在跟踪你。它的气息...和你体内的混沌之力完全同源。你们之间，有某种联系。",
                2 => "【威胁度≥8】混沌圣体的力量开始失控。你的影子...在动。那里站着一个和你长得一模一样的人，但他的眼睛是纯黑的。'终于...找到你了。'他微笑着说。",
                3 => "【领域展开】混沌领域全开的瞬间，你短暂地看到了真相：你和你体内的混沌之力，是一个被拆散的存在的两半。你们的融合，将复活一个古老到连天道都惧怕的东西。",
                _ => $"Milestone {milestoneIndex}"
            };
        }
    }
}
