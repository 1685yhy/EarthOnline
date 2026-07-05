using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 物品数据库——给每个物品名字、故事、背景。不是"铁剑(攻击+5)"。
    /// </summary>
    public static class ItemDatabase
    {
        public static Dictionary<string, ItemStory> Stories = new()
        {
            ["item_iron_sword"] = new ItemStory {
                displayName = "断水", rarityName = "精铁剑",
                story = "王铁柱年轻时凭记忆锻出的第一把灵剑。剑身上有一道隐约的纹路——那是他见过一头妖兽后刻上去的。",
                origin = "王铁柱铸造"
            },
            ["item_leather_armor"] = new ItemStory {
                displayName = "韧皮甲", rarityName = "兽皮护甲",
                story = "用三张野狼皮缝制而成。内侧缝着一个小小的'李'字——李灵儿曾帮忙鞣制这批皮革。",
                origin = "李灵儿鞣制"
            },
            ["item_steel_sword"] = new ItemStory {
                displayName = "寒星", rarityName = "精钢灵剑",
                story = "王家铸剑术的巅峰之作。剑柄上刻着'赠吾弟'——这把剑曾属于王铁柱的弟弟，那个杀了天元宗长老后失踪的铸剑师。",
                origin = "王家铸剑术"
            },
            ["item_guard_ring"] = new ItemStory {
                displayName = "守心", rarityName = "守护之戒",
                story = "一位元婴修士为道侣炼制的戒指。道侣死后，戒指流落坊市，换了三次主人。每一位主人都死于非命——除了最后一位。她说戒指里有一个女修的执念，在守护佩戴者。",
                origin = "无名元婴修士"
            },
            ["item_dragon_scale_armor"] = new ItemStory {
                displayName = "龙鳞", rarityName = "龙鳞宝甲",
                story = "这不是炼制的——这是蜕下来的。一头真正的青龙在渡劫前蜕下了这层鳞甲。它是告别，也是遗物。因为那头龙知道：渡劫成功则飞升，失败则灰飞烟灭。无论哪种——都不再需要这层鳞甲了。",
                origin = "渡劫青龙遗蜕"
            },
            ["item_cultivation_pill"] = new ItemStory {
                displayName = "破障丹", rarityName = "筑基灵丹",
                story = "李灵儿父亲——前天元宗副宗主——留下的丹方。他废掉修为前炼的最后一炉丹。每一颗都蕴含着一个父亲对女儿的歉意。",
                origin = "李灵儿父亲炼制"
            },
            ["item_skill_scroll"] = new ItemStory {
                displayName = "残卷·剑意篇", rarityName = "上古剑诀残卷",
                story = "纸张已经泛黄发脆。第一页写着：'此卷传自剑仙李太白。然太白飞升后，世间无人能练至第七层。后学者慎之。'卷中夹着一片枯叶——可能是太白飞升前看最后一眼人间时落下的。",
                origin = "剑仙李太白遗物"
            },
            ["item_cultivation_elixir"] = new ItemStory {
                displayName = "月华露", rarityName = "修炼灵液",
                story = "只有在月圆之夜、灵气浓度最高的山顶才能采集到的露水。每一滴都经过月光淬炼。陈半仙说他曾在一个古墓里见过一池月华露——守池的是一具活着的骷髅。",
                origin = "月圆之夜·灵气山巅"
            },
        };

        /// <summary>获取物品的显示名称（带故事背景）</summary>
        public static string GetDisplayName(string itemId)
        {
            return Stories.ContainsKey(itemId) ? Stories[itemId].displayName : itemId;
        }
    }

    public class ItemStory
    {
        public string displayName;  // 独特名称（如"断水"）
        public string rarityName;   // 品质名称（如"精铁剑"）
        public string story;        // 物品故事
        public string origin;       // 来源
    }
}
