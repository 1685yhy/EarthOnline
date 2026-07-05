using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 神豪系统 —— 灵石获取翻倍，商业直觉，可投资/收购。
    /// 故事：来自一个"金钱至上"的平行世界。那个世界已经毁灭了——因为贫富差距引发了世界大战。
    /// </summary>
    public class WealthSystem : GiftBase
    {
        private int _totalExtraEarned;
        public float WealthMultiplier => 1f + Level * 0.5f; // Lv1=1.5x, Lv2=2x

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _totalExtraEarned = 0; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 💰 神豪系统激活！所有灵石收入×{WealthMultiplier:F1}倍。");
            Debug.Log($"[{GiftName}] '警告：本系统来自已毁灭的026号平行世界。该世界因资源分配失衡而自我毁灭。'");
        }

        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            string unlocks = Level switch
            {
                2 => "解锁：拍卖行特权——可以看到隐藏竞拍物品。",
                3 => "解锁：投资——可以向商号投资获取被动收入。",
                4 => "解锁：收购——可以收购小型商号和坊市摊位。",
                _ => ""
            };
            Debug.Log($"[{GiftName}] 升级 Lv.{Level}！灵石倍率×{WealthMultiplier:F1}。{unlocks}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "earn": EarnBonus(context); break;
                case "invest": Invest(); break;
                case "status": ShowWealth(); break;
            }
        }

        void EarnBonus(Dictionary<string, object> context)
        {
            int baseAmount = context != null && context.ContainsKey("amount") ? (int)context["amount"] : 0;
            int bonus = Mathf.RoundToInt(baseAmount * (WealthMultiplier - 1f));
            _totalExtraEarned += bonus;
            var stats = PlayerStats.Instance;
            if (stats != null && bonus > 0) stats.spiritStones += bonus;
            Debug.Log($"[{GiftName}] 神豪加成：+{bonus}灵石 (总计额外获得:{_totalExtraEarned})");
        }

        void Invest()
        {
            if (Level < 3) { Debug.Log($"[{GiftName}] 需要Lv.3解锁投资功能。"); return; }
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 500) { Debug.Log("[神豪] 投资需要至少500灵石。"); return; }
            stats.spiritStones -= 500;
            Debug.Log($"[{GiftName}] 投资500灵石到陈半仙的商队。每天可获得被动收入。");

            if (_totalExtraEarned >= 5000 && StoryProgress == 0) AdvanceStory(1);
        }

        void ShowWealth()
        {
            Debug.Log($"[{GiftName}] 神豪系统 Lv.{Level} | 倍率:{WealthMultiplier}x | 额外获得:{_totalExtraEarned}灵石");
            Debug.Log($"  能力: earn(Lv.1) | invest(Lv.3) | 收购(Lv.4)");
        }

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"灵石收入×{WealthMultiplier:F1} | 额外获得{_totalExtraEarned}灵石\nLv.3=投资 Lv.4=收购",
            Abilities = new List<string> { "earn", "invest(Lv.3)", "status" },
            StoryHint = _totalExtraEarned >= 5000 ? "系统日志中出现了一条来自'026号世界'的求救信号..." : "金钱至上的世界为什么会毁灭？"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "系统日志：'026号世界，末日倒计时：最后72小时。贫富差距系数：99.7%。社会已崩溃。如果收到这条信息……请记住：金钱不是目的，是手段。'",
            _ => $"Milestone {i}"
        };
    }
}
