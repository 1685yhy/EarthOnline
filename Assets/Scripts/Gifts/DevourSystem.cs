using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 吞噬系统 —— 击杀敌人可吞噬其精华转化为自身修为和HP。
    /// 代价：每吞噬一个生命，你的"人性"就少一分。人性归零时...你不再是人类。
    /// </summary>
    public class DevourSystem : GiftBase
    {
        private int _devoured;
        private int _humanity = 100;
        public float DevourMultiplier => 1f + Level * 0.3f;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _devoured = 0; _humanity = 100; }

        public override void Activate()
        {
            IsActive = true;
            EventBus.Subscribe("OnEnemyKilled", OnKill);
            Debug.Log($"[{GiftName}] 🖤 吞噬系统激活。人性:{_humanity}%。");
            Debug.Log($"[{GiftName}] '警告：吞噬生命将侵蚀使用者的人性。人性归零后，使用者将成为新的虚空生物。'");
        }

        public override void Deactivate()
        {
            IsActive = false;
            EventBus.Unsubscribe("OnEnemyKilled", OnKill);
        }

        void OnKill(Dictionary<string, object> data)
        {
            if (!IsActive) return;
            _devoured++;

            int expGain = Mathf.RoundToInt(20 * DevourMultiplier);
            int hpGain = Mathf.RoundToInt(10 * DevourMultiplier);
            _humanity = Mathf.Max(0, _humanity - 1);

            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.AddCultivation(expGain);
                stats.Heal(hpGain);
            }

            string humanityWarning = _humanity switch
            {
                <= 10 => "⚠️ 你的影子开始自己移动了。",
                <= 30 => "⚡ 你的眼睛变成了纯黑色。NPC开始回避你。",
                <= 50 => "🌑 你感到饥饿——不是对食物的饥饿，是对生命的饥饿。",
                _ => ""
            };

            Debug.Log($"[{GiftName}] 🖤 吞噬！+{expGain}修为 +{hpGain}HP (第{_devoured}次) 人性:{_humanity}% {humanityWarning}");

            if (_devoured >= 20 && StoryProgress == 0) AdvanceStory(1);
            if (_humanity <= 10 && StoryProgress == 1) AdvanceStory(2);
        }

        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 吞噬进化 Lv.{Level}！倍率×{DevourMultiplier:F1}。但人性消耗不变。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            if (abilityName == "restore_humanity") RestoreHumanity();
            else if (abilityName == "status") ShowStatus();
        }

        void RestoreHumanity()
        {
            if (_humanity >= 100) { Debug.Log("[吞噬] 你的人性完好。"); return; }
            int restore = 5;
            var stats = PlayerStats.Instance;
            if (stats != null && stats.spiritStones >= 200)
            {
                stats.spiritStones -= 200;
                _humanity = Mathf.Min(100, _humanity + restore);
                Debug.Log($"[{GiftName}] 💫 花费200灵石净化心灵。人性恢复+{restore}%→{_humanity}%");
            }
            else Debug.Log("[吞噬] 需要200灵石来净化心灵。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 吞噬 Lv.{Level} | {_devoured}次吞噬 | 人性:{_humanity}% | 倍率×{DevourMultiplier:F1}");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"吞噬{_devoured}次 人性{_humanity}%\n击杀自动吞噬 | 恢复人性(200灵石)",
            Abilities = new List<string> { "auto_devour(passive)", "restore_humanity", "status" },
            StoryHint = _humanity <= 30 ? "你开始听到虚空中的低语。它们在叫你...同类。" : "吞噬得越多，你越像那些你吞噬的东西。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "第20次吞噬。你看到了——每一只被你吞噬的妖兽的记忆碎片。它们不是野兽。它们是上一个被虚空吞噬的世界的幸存者。它们变成了妖兽。你正在和它们走上同一条路。",
            2 => "人性降到10%。虚空开始承认你为'同类'。签到系统的制造者——那个追杀老爷爷的人——他曾经也是一个吞噬者。他选择了完全放弃人性。他成了虚空的一部分。你会选择什么？",
            _ => $"Milestone {i}"
        };
    }
}
