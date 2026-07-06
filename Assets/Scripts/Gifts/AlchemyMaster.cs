using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 药王谷传承 —— 古炼丹术完整传承。可直接炼制高级丹药。
    /// 代价：每炼制一颗高级丹药，消耗一定量的"生命力"（HP上限）。
    /// 故事：药王谷在一夜之间消失了。所有弟子、典籍、丹炉——连同整座山谷，凭空蒸发。
    /// </summary>
    public class AlchemyMaster : GiftBase
    {
        private int _pillsCreated;
        private int _hpSacrificed;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _pillsCreated = 0; _hpSacrificed = 0; }
        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🔥 药王谷传承激活。'这不是炼丹术——这是药王谷三千弟子用命换来的最后传承。每一颗丹药都浸透了他们的执念。'");
        }
        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 丹道精进 Lv.{Level}。可炼制更高级丹药。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "refine_pill": RefinePill(); break;
                case "refine_elixir": RefineElixir(); break;
                case "identify_herb": IdentifyHerb(); break;
                case "status": ShowStatus(); break;
            }
        }

        void RefinePill()
        {
            var inv = InventoryManager.Instance;
            var stats = PlayerStats.Instance;
            if (inv == null || stats == null) return;
            if (!inv.HasItem("item_herb_001", 3)) { Debug.Log("[药王] 需要3株止血草。"); return; }

            inv.RemoveItem("item_herb_001", 3); _pillsCreated++;
            inv.AddItem(new Item { id = "item_heal_pill_001", name = "回血丹", type = "Consumable", rarity = "R", quantity = 2, value = 40 });
            Debug.Log($"[{GiftName}] 🏺 炼制回血丹×2 (第{_pillsCreated}次炼丹)");

            if (_pillsCreated >= 10 && StoryProgress == 0) AdvanceStory(1);
        }

        void RefineElixir()
        {
            if (Level < 2) { Debug.Log("[药王] 需要Lv.2解锁高级炼制。"); return; }
            var stats = PlayerStats.Instance; var inv = InventoryManager.Instance;
            if (stats == null || inv == null) return;
            if (!inv.HasItem("item_pill_001", 3)) { Debug.Log("[药王] 需要3颗聚气丹。"); return; }

            inv.RemoveItem("item_pill_001", 3); _pillsCreated++;
            int hpCost = 5; _hpSacrificed += hpCost;
            stats.maxHP -= hpCost; stats.currentHP = Mathf.Min(stats.currentHP, stats.maxHP);
            inv.AddItem(new Item { id = "item_cultivation_elixir", name = "修炼灵液", type = "Consumable", rarity = "SR", quantity = 1, value = 100 });
            Debug.Log($"[{GiftName}] 🧪 炼制修炼灵液！HP上限-{hpCost}（累计{_hpSacrificed}）——药王谷的丹术，代价是炼丹师的生命。");
        }

        void IdentifyHerb() => Debug.Log("[药王] 感知药性——附近3株止血草、2颗聚气丹可用。");

        void ShowStatus() => Debug.Log($"[{GiftName}] 炼丹{_pillsCreated}次 HP-{_hpSacrificed}");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"炼丹{_pillsCreated}次 HP-{_hpSacrificed}\n炼药(Lv.1) 高级炼制(Lv.2) 辨识(Lv.1)",
            Abilities = new List<string> { "refine_pill", "refine_elixir(Lv.2)", "identify_herb", "status" },
            StoryHint = _pillsCreated >= 10 ? "你在丹炉里看到了一张脸。那不是你的倒影——是药王谷的最后一任谷主。他在炉火里。他还'活着'。" : "药王谷为什么消失了？那三千弟子——去了哪里？"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "丹炉里的脸开口了：'我是药王谷谷主。虚空来的那天，我启动了护山大阵——一个把所有人封印在丹炉里的阵法。我以为能保护他们。但虚空进来了。它在炉火里。它还在。它在等你。'",
            _ => $"Milestone {i}"
        };
    }
}
