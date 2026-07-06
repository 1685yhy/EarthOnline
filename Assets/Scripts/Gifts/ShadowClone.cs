using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 影分身 —— 可创造自己的影子分身。分身可独立行动。
    /// 代价：分身受伤=本体受伤。分身死亡=永久损失HP上限。
    /// </summary>
    public class ShadowClone : GiftBase
    {
        private int _clonesCreated;
        private int _clonesLost;
        private bool _hasActiveClone;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _clonesCreated = 0; _clonesLost = 0; }
        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 👥 影分身之术。'你的影子活了。它不是模仿你——它就是另一个你。小心点——你分出去的每一片灵魂都不会完整地回来。'");
        }
        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] Lv.{Level}——可创造{Level}个分身。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "create_clone": CreateClone(); break;
                case "dismiss_clone": DismissClone(); break;
                case "swap": SwapWithClone(); break;
                case "status": ShowStatus(); break;
            }
        }

        void CreateClone()
        {
            if (_hasActiveClone && Level < 3) { Debug.Log("[影分身] 当前只能维持1个分身。Lv.3解锁第二个。"); return; }
            var stats = PlayerStats.Instance;
            if (stats == null || stats.currentHP < 20) { Debug.Log("[影分身] HP不足20，无法分身。"); return; }
            stats.currentHP -= 10; _clonesCreated++; _hasActiveClone = true;
            Debug.Log($"[{GiftName}] 👤 分身创造！-10HP。分身可独立战斗。HP={stats.currentHP}");
            if (_clonesCreated >= 5 && StoryProgress == 0) AdvanceStory(1);
        }

        void DismissClone()
        {
            if (!_hasActiveClone) { Debug.Log("[影分身] 没有活动分身。"); return; }
            _hasActiveClone = false;
            Debug.Log("[影分身] 分身解除——灵魂碎片回归。");
        }

        void SwapWithClone()
        {
            if (Level < 2) { Debug.Log("[影分身] 需要Lv.2解锁位置交换。"); return; }
            if (!_hasActiveClone) { Debug.Log("[影分身] 没有活动分身可以交换。"); return; }
            Debug.Log("[影分身] 🔄 与分身交换位置！");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 分身 Lv.{Level} | 创造{_clonesCreated}次 | 失去{_clonesLost}个");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"分身{_clonesCreated}次 失去{_clonesLost}个\n创造(Lv.1) 交换(Lv.2) 多重(Lv.3)",
            Abilities = new List<string> { "create_clone", "dismiss_clone", "swap(Lv.2)", "status" },
            StoryHint = _clonesCreated >= 5 ? "你的分身在某次回来后...不太一样了。它看着你的眼神——像是在评估什么东西。" : "每一次分身，都分出去一片灵魂。那些碎片去了哪里——又带回了什么？"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "分身回来后沉默了很久。然后它说了一句话——不是你说的。'我去了一个地方。那里全都是我们。成千上万个你。每一层的空间都在重复你的脸。虚空...在复制你。'",
            _ => $"Milestone {i}"
        };
    }
}
