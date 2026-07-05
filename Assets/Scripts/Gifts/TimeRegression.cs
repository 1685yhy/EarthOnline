using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 时光回溯 —— 可倒流时间10秒。最稀有也最危险的金手指。
    /// 代价：每次使用消耗寿命（HP上限永久降低）。
    /// 故事：这不是金手指——这是一个诅咒。上一个拥有者活了3000年，被困在同一天里。
    /// </summary>
    public class TimeRegression : GiftBase
    {
        private int _timesUsed;
        private int _hpLost;
        public int TotalHPLost => _hpLost;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _timesUsed = 0; _hpLost = 0; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] ⏳ 时光回溯激活。'你摸到了时间的纹理。它是冷的。像死人皮肤。'");
            Debug.Log($"[{GiftName}] ⚠️ 每次使用永久降低5点HP上限。这不是金手指——这是你和一个被困在同一天3000年的灵魂的交易。");
        }
        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 回溯精进 Lv.{Level}。回溯时间+5秒/级。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "rewind": RewindTime(); break;
                case "slow_time": SlowTime(); break;
                case "status": ShowStatus(); break;
            }
        }

        void RewindTime()
        {
            _timesUsed++;
            int hpCost = 5;
            _hpLost += hpCost;
            var stats = PlayerStats.Instance;
            if (stats != null) { stats.maxHP -= hpCost; stats.currentHP = Mathf.Min(stats.currentHP, stats.maxHP); }

            Debug.Log($"[{GiftName}] ⏰ 时光倒流！回到10秒前的状态。HP上限永久-{hpCost}（累计:{_hpLost}）");
            Debug.Log($"[{GiftName}] 耳边传来低语：'第{_timesUsed}次...你会后悔的...像我一样...'");

            if (_timesUsed >= 10 && StoryProgress == 0) AdvanceStory(1);
        }

        void SlowTime()
        {
            if (Level < 2) { Debug.Log($"[{GiftName}] 需要Lv.2"); return; }
            Debug.Log($"[{GiftName}] 🐌 时间缓速！周围时间流速减半，持续5秒。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 时光回溯 Lv.{Level} | 使用{_timesUsed}次 | HP损失{_hpLost}");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"使用{_timesUsed}次 HP-{_hpLost}\n回溯(Lv.1) 缓速(Lv.2)",
            Abilities = new List<string> { "rewind", "slow_time(Lv.2)", "status" },
            StoryHint = _timesUsed >= 10 ? "那个被困在同一天的灵魂——他在你的影子里。他在等。等你的意志崩溃。然后他会取代你。" : "时间是单行道。倒着走的人——会看到不该看的东西。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "第10次回溯。你看到了他——被困在同一天的那个灵魂。他就是3000年前的你。上一个轮回的失败品。'你也会失败的'他说，'但这一次...我想帮你。我不想再孤独了。'",
            _ => $"Milestone {i}"
        };
    }
}
