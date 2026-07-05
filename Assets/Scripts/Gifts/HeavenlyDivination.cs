using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 天机术 —— 预知危险、发现秘境、窥探命运。
    /// 代价：每次窥探天机都会遭受反噬（损失HP/修为）。
    /// </summary>
    public class HeavenlyDivination : GiftBase
    {
        private int _divinationsUsed;
        private int _backlashReceived;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _divinationsUsed = 0; _backlashReceived = 0; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🔮 天机术开启。'窥探天机者，必遭天谴。但你...别无选择。'");
        }

        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 天机术精进 Lv.{Level}。反噬减轻{Level*10}%。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "divine_danger": DivineDanger(); break;
                case "divine_secret": DivineSecret(); break;
                case "divine_fate": DivineFate(); break;
                case "status": ShowStatus(); break;
            }
        }

        void DivineDanger()
        {
            _divinationsUsed++;
            int backlash = Mathf.Max(1, 15 - Level * 3);
            _backlashReceived += backlash;
            var stats = PlayerStats.Instance;
            if (stats != null) stats.TakeDamage(backlash);

            var enemies = Object.FindObjectsOfType<EarthOnline.Combat.EnemyAI>();
            int nearby = 0; var player = GameObject.FindGameObjectWithTag("Player");
            foreach (var e in enemies)
                if (!e.IsDead && player != null && Vector3.Distance(player.transform.position, e.transform.position) < 15f) nearby++;

            Debug.Log($"[{GiftName}] 🔮 感知危险！附近{nearby}个敌人。反噬:-{backlash}HP (累计:{_backlashReceived})");
            if (nearby >= 3 && StoryProgress == 0) AdvanceStory(1);
        }

        void DivineSecret()
        {
            _divinationsUsed++;
            int backlash = Mathf.Max(1, 20 - Level * 3);
            _backlashReceived += backlash;
            PlayerStats.Instance?.TakeDamage(backlash);

            var pickups = Object.FindObjectsOfType<WorldPickup>();
            var chests = Object.FindObjectsOfType<TreasureChest>();
            Debug.Log($"[{GiftName}] 🔮 洞察秘境！{pickups.Length}个灵物、{chests.Length}个宝箱在附近。反噬:-{backlash}HP");
            foreach (var p in pickups)
            {
                float d = Vector3.Distance(PlayerStats.Instance?.transform.position ?? Vector3.zero, p.transform.position);
                if (d < 20f) Debug.Log($"  [{p.itemRarity}] {p.itemName} @ {d:F1}m");
            }
        }

        void DivineFate()
        {
            if (Level < 3) { Debug.Log($"[{GiftName}] 需要Lv.3解锁命运窥探。"); return; }
            _divinationsUsed++;
            int backlash = 30 - Level * 5;
            _backlashReceived += backlash;
            PlayerStats.Instance?.TakeDamage(backlash);

            string[] fates = {
                "三天之内，你会遇到一个改变你命运的人。黑色衣服，不会说话。",
                "不要去北方的山。那里有你无法对抗的东西。至少现在不能。",
                "你体内的那个东西...它不是在帮你。它是在利用你。但它需要你活着。利用这一点。",
                "你曾经死过一次。在你穿越之前。地球意志复活了你。它付出了代价。你欠它一条命。"
            };
            Debug.Log($"[{GiftName}] 🔮 命运窥探：'{fates[Random.Range(0, fates.Length)]}' 反噬:-{backlash}HP");

            if (_divinationsUsed >= 10) AdvanceStory(1);
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 天机术 Lv.{Level} | 占卜{_divinationsUsed}次 | 反噬{_backlashReceived}HP");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"占卜{_divinationsUsed}次 反噬{_backlashReceived}HP\n感知危险(Lv.1) 洞察秘境(Lv.1) 命运窥探(Lv.3)",
            Abilities = new List<string> { "divine_danger", "divine_secret", "divine_fate(Lv.3)", "status" },
            StoryHint = _divinationsUsed >= 10 ? "你看到了一条血红色的命运线。它连接着你——和一个已经毁灭的世界。" : "窥探天机越多，反噬越大。但有些秘密...值得付出代价。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "你在命运线中看到了一个巨大的阴影。它在吞噬一个个世界——就像曾经吞噬了签到系统的制造文明。它在找你。因为你体内有它想要的东西。",
            _ => $"Milestone {i}"
        };
    }
}
