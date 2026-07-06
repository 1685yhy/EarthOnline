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
            ["item_spirit_jade"] = new ItemStory {
                displayName = "凝魄", rarityName = "上品灵玉",
                story = "不是挖出来的——是'长'出来的。灵玉只能在灵气浓度极高的地方自然结晶。这一块的中心有一滴已经凝固的血——可能是某个修士在修炼时滴落的。",
                origin = "灵气结晶"
            },
            ["item_ginseng_1000yr"] = new ItemStory {
                displayName = "千年参王", rarityName = "千年灵芝",
                story = "已经能微弱地动弹——它差一点就能成精了。采它的人在旁边等了三天三夜，等到它最放松的那一刻。吃下它的人会获得它千年积累的灵力——但也会继承它对采药人的恨意。",
                origin = "深山老林·千年成精"
            },
            ["item_spirit_stone"] = new ItemStory {
                displayName = "碎灵", rarityName = "下品灵石",
                story = "最常见的修炼货币。但这一块有点不同——它的切面里封着一只已经石化的远古昆虫。虫子的姿势像是在逃跑。它在躲什么？",
                origin = "普通矿脉"
            },
            ["item_herb_001"] = new ItemStory {
                displayName = "血痕草", rarityName = "止血草",
                story = "叶片边缘天然带着红色的纹路——像血迹。古籍上记载：第一株止血草是上古一位女修用自己的血浇灌出来的。她是一个凡人，爱上了一个修士。修士受伤了，她没有灵药。她割开了自己的手腕。",
                origin = "上古传说"
            },
            ["item_pill_001"] = new ItemStory {
                displayName = "凝气散", rarityName = "聚气丹",
                story = "最基础的修炼丹药。炼丹师学徒的第一课。这枚丹药上有一道裂纹——是炼丹师在出炉时手抖了。他的师父说：'每一道裂纹都是教训。记住它。'",
                origin = "炼丹学徒"
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
