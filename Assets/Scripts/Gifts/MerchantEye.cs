using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 鉴宝灵瞳 —— 可鉴定物品真实价值、发现隐藏属性、识别赝品。
    /// 代价：长时间使用会"看到不该看的东西"——物品上附着的执念、怨气、记忆。
    /// </summary>
    public class MerchantEye : GiftBase
    {
        private int _itemsAppraised;
        private int _visionsSeen;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _itemsAppraised = 0; _visionsSeen = 0; }
        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 👁️ 鉴宝灵瞳开启。'你看到的不再是物品——是它们承载的记忆。有些记忆...不该被看到。'");
        }
        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 灵瞳进化 Lv.{Level}。可看到更深的因果。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "appraise": Appraise(); break;
                case "see_hidden": SeeHidden(); break;
                case "see_cursed": SeeCursed(); break;
                case "status": ShowStatus(); break;
            }
        }

        void Appraise()
        {
            _itemsAppraised++;
            var inv = InventoryManager.Instance;
            var items = inv?.GetAllItems();
            if (items == null || items.Count == 0) { Debug.Log("[鉴宝] 背包空空。"); return; }
            var item = items[Random.Range(0, items.Count)];
            string story = ItemDatabase.Stories.ContainsKey(item.id) ? ItemDatabase.Stories[item.id].story : "这件物品上没有特殊的故事。";
            Debug.Log($"[{GiftName}] 🔍 鉴定 [{item.rarity}]{item.name}：{story}");
            if (_itemsAppraised >= 10 && StoryProgress == 0) AdvanceStory(1);
        }

        void SeeHidden()
        {
            if (Level < 2) { Debug.Log("[鉴宝] 需要Lv.2"); return; }
            _visionsSeen++;
            Debug.Log($"[{GiftName}] 🌌 看到隐藏灵气节点——附近可能有秘境入口或隐藏宝藏。");
            if (_visionsSeen >= 5) Debug.Log("[鉴宝] ⚠️ 你看到的东西开始不限于物品了...NPC身上也有'痕迹'。");
        }

        void SeeCursed()
        {
            if (Level < 3) { Debug.Log("[鉴宝] 需要Lv.3"); return; }
            _visionsSeen++;
            Debug.Log($"[{GiftName}] 💀 洞察诅咒——某些物品上缠绕着黑色的丝线。那不是灵力——是执念。有人死在这件物品上。不止一个。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 鉴定{_itemsAppraised}次 灵视{_visionsSeen}次");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"鉴定{_itemsAppraised}次 灵视{_visionsSeen}次\n鉴定(Lv.1) 灵气视觉(Lv.2) 诅咒洞察(Lv.3)",
            Abilities = new List<string> { "appraise", "see_hidden(Lv.2)", "see_cursed(Lv.3)", "status" },
            StoryHint = _visionsSeen >= 5 ? "张老身上的黑色丝线比其他任何人都多。那是虚空留下的痕迹——不是碰过虚空的人，是在里面待过的人。" : "你看得越多，越发现——这个世界被'使用'过。被人刻意修改过。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "你看到了真相。每一个从虚空裂缝出来的人——身上都缠绕着那种黑色丝线。张老身上的最多——因为他妻子在虚空里，他离虚空最近。但还有一个人——他的丝线不是黑的，是金色的。是谁？",
            _ => $"Milestone {i}"
        };
    }
}
