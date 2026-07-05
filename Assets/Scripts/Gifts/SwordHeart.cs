using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 剑心通明 —— 天生剑道奇才。修炼剑法事半功倍，能感知剑意。
    /// 代价：越接近剑道极致，越远离人性。剑是冷的。
    /// </summary>
    public class SwordHeart : GiftBase
    {
        private int _swordInsight;
        public float SwordBonus => 1f + Level * 0.25f;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _swordInsight = 0; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] ⚔️ 剑心通明。'你握着剑的时候，能听到它在跟你说话。它不是武器——它是你的另一半灵魂。'");
        }

        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            string insight = Level switch
            {
                2 => "感知剑气——可以看到敌人出招前的灵力流动。",
                3 => "人剑合一——灵击消耗减半。",
                4 => "剑域——创造一个剑气领域，持续伤害敌人。",
                _ => ""
            };
            Debug.Log($"[{GiftName}] 剑道精进 Lv.{Level}。{insight}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "sword_slash": SwordSlash(); break;
                case "sword_sense": SwordSense(); break;
                case "sword_domain": SwordDomain(); break;
                case "status": ShowStatus(); break;
            }
        }

        void SwordSlash()
        {
            _swordInsight++;
            int dmg = Mathf.RoundToInt(25 * SwordBonus);
            Debug.Log($"[{GiftName}] ⚔️ 剑斩！{dmg}伤害。剑意+1→{_swordInsight}");
            if (_swordInsight >= 15 && StoryProgress == 0) AdvanceStory(1);
        }

        void SwordSense()
        {
            if (Level < 2) { Debug.Log($"[{GiftName}] 需要Lv.2"); return; }
            var enemies = Object.FindObjectsOfType<EarthOnline.Combat.EnemyAI>();
            Debug.Log($"[{GiftName}] 剑意感知——{enemies.Length}个敌人:");
            var player = GameObject.FindGameObjectWithTag("Player");
            foreach (var e in enemies)
                if (!e.IsDead && player != null)
                    Debug.Log($"  {e.enemyName} @ {Vector3.Distance(player.transform.position, e.transform.position):F1}m HP:{e.currentHP}/{e.maxHP}");
        }

        void SwordDomain()
        {
            if (Level < 4) { Debug.Log($"[{GiftName}] 需要Lv.4解锁剑域。"); return; }
            Debug.Log($"[{GiftName}] 🗡️ 剑域展开！周围所有敌人持续受到剑气伤害。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 剑心通明 Lv.{Level} | 剑意:{_swordInsight} | 剑术×{SwordBonus:F1}");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"剑意{_swordInsight} 剑术×{SwordBonus:F1}\nLv.1剑斩 Lv.2感知剑气 Lv.3人剑合一 Lv.4剑域",
            Abilities = new List<string> { "sword_slash", "sword_sense(Lv.2)", "sword_domain(Lv.4)", "status" },
            StoryHint = _swordInsight >= 15 ? "你看到了一把剑——不是真实存在的，而是刻在天道中的。那把剑在等你。" : "剑道的尽头是什么？"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "你在剑意中看到了一把贯穿天地的巨剑。那不是武器——那是'道'本身。上一个看到这把剑的人，在一千年后成了剑仙。但他留下了警告：'剑道的终极——是孤独。我失去了所有我在乎的人。'",
            _ => $"Milestone {i}"
        };
    }
}
