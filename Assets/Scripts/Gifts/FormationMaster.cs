using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 阵法大师 —— 可布设阵法控制区域。攻阵/守阵/困阵/幻阵。
    /// 代价：布阵消耗灵石和精力。阵被破会反噬。
    /// </summary>
    public class FormationMaster : GiftBase
    {
        private int _formationsDeployed;
        private int _formationsBroken;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _formationsDeployed = 0; _formationsBroken = 0; }

        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🔷 阵法感知开启。'天地为盘，万物为子。你看到的不是风景——是阵眼。'");
        }
        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            string unlock = Level switch
            {
                2 => "解锁：困阵——束缚敌人，减速50%。",
                3 => "解锁：杀阵——阵法范围内持续伤害。",
                4 => "解锁：幻阵——迷惑敌人自相残杀。",
                _ => ""
            };
            Debug.Log($"[{GiftName}] 阵法精进 Lv.{Level}。{unlock}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "deploy_defense": DeployDefense(); break;
                case "deploy_trap": DeployTrap(); break;
                case "deploy_killzone": DeployKillZone(); break;
                case "status": ShowStatus(); break;
            }
        }

        void DeployDefense()
        {
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 50) { Debug.Log("[阵法] 布阵需要50灵石。"); return; }
            stats.spiritStones -= 50; _formationsDeployed++;
            Debug.Log($"[{GiftName}] 🛡️ 守阵部署！10秒内受到伤害-30%。消耗50灵石。(第{_formationsDeployed}次布阵)");
            if (_formationsDeployed >= 10 && StoryProgress == 0) AdvanceStory(1);
        }

        void DeployTrap()
        {
            if (Level < 2) { Debug.Log("[阵法] 需要Lv.2解锁困阵。"); return; }
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 30) { Debug.Log("[阵法] 需要30灵石。"); return; }
            stats.spiritStones -= 30; _formationsDeployed++;
            Debug.Log($"[{GiftName}] 🕸️ 困阵部署！周围敌人减速50%，持续15秒。");
        }

        void DeployKillZone()
        {
            if (Level < 3) { Debug.Log("[阵法] 需要Lv.3解锁杀阵。"); return; }
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 100) { Debug.Log("[阵法] 需要100灵石。"); return; }
            stats.spiritStones -= 100; _formationsDeployed++;
            Debug.Log($"[{GiftName}] ⚡ 杀阵启动！范围内敌人每秒受到10点伤害，持续20秒。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 阵法 Lv.{Level} | 布阵{_formationsDeployed}次 | 被破{_formationsBroken}次");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"布阵{_formationsDeployed}次\nLv.1守阵 Lv.2困阵 Lv.3杀阵 Lv.4幻阵",
            Abilities = new List<string> { "deploy_defense", "deploy_trap(Lv.2)", "deploy_killzone(Lv.3)", "status" },
            StoryHint = _formationsDeployed >= 10 ? "你布的每一个阵都在地下留下痕迹。有人——在收集你的痕迹。他是谁？" : "阵法是这片大陆最古老的技艺。它的源头比宗门更古老。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "你在一次布阵后留在原地观察。几个时辰后，一个黑衣人出现了。他蹲在你布阵的位置，用手指沾了沾地上的残余灵力——放在嘴里尝了尝。他抬起头，对着你隐藏的方向笑了笑。'不错的阵。但还有瑕疵。我可以教你。'他没有恶意。但他是谁？为什么对你的阵法这么感兴趣？",
            _ => $"Milestone {i}"
        };
    }
}
