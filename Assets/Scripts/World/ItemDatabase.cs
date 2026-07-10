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
            ["item_ancient_scripture"] = new ItemStory {
                displayName = "虚空经·残页", rarityName = "上古残卷",
                story = "纸张的材料不是这个世界的东西。上面的文字会自动变化——每次打开都是不同的内容。最后一页只有一句话：'写下这些的人还活着。他在虚空里。他在写。不要让他的手稿被读完——读完的那一天，他会从最后一页爬出来。'",
                origin = "虚空中的手稿"
            },
            ["item_spirit_core_001"] = new ItemStory {
                displayName = "灵核", rarityName = "灵气核心",
                story = "妖兽体内凝结的灵力精华。每一颗都是妖兽一生的修为结晶。握着它的时候——你能短暂地感受到那只妖兽的记忆碎片：森林、月光、还有把它杀死的那个修士的脸。",
                origin = "妖兽体内"
            },
            ["item_heal_pill_001"] = new ItemStory { displayName = "续命散", rarityName = "回血丹", story = "李灵儿炼的第一炉丹药。那一年她十岁，父亲刚被废掉修为。她把丹药塞进父亲嘴里——'爹，吃下去就会好的。'父亲笑了——那是他最后一次笑。", origin = "李灵儿·第一炉" },
            ["item_cultivation_pill"] = new ItemStory { displayName = "破障丹", rarityName = "筑基灵丹", story = "李灵儿父亲留下的丹方。他废掉修为前炼的最后一炉丹。每一颗都蕴含着一个父亲对女儿的歉意——对不起，没能保护你。", origin = "天元宗前副宗主·遗物" },
            ["item_steel_sword"] = new ItemStory { displayName = "寒星", rarityName = "精钢灵剑", story = "王家铸剑术的巅峰之作。剑柄刻着赠吾弟——这把剑曾属于王铁柱的弟弟，那个杀了天元宗长老后失踪的铸剑师。每一道剑痕都是一个没有说完的故事。", origin = "王家铸剑术·兄弟之剑" },
            ["item_void_crystal"] = new ItemStory { displayName = "虚空碎片", rarityName = "虚空结晶", story = "虚空也会哭。吸收了足够多穿越者的记忆和遗憾后——就会凝结成这种结晶。握在手里能感受到47个穿越者的一生——他们的欢笑、痛苦、希望。", origin = "虚空·穿越者记忆" },
            ["item_dragon_fang"] = new ItemStory { displayName = "龙牙遗书", rarityName = "龙牙", story = "不是被拔的——是龙自己吐的。龙族灭族前——每条龙留下了一颗牙。当你们找到这些牙时——我们已经不在了。但龙族的意志——永远不会消失。", origin = "龙族·灭绝遗书" },
            ["item_phoenix_feather"] = new ItemStory { displayName = "涅槃之羽", rarityName = "凤羽", story = "凤凰涅槃时脱落的羽毛。一根羽就是一个轮回。集齐三根可炼制不死药——但上一个尝试的人在丹炉前老死了。他等了凤凰三千年——凤凰回来了——但他已经不在了。", origin = "凤凰·轮回之证" },
            ["item_mana_orb"] = new ItemStory { displayName = "魂之容器", rarityName = "灵能宝珠", story = "一个用来封印灵魂的宝珠。里面封着三个灵魂——一个是修士，一个是妖兽，一个是凡人。他们被困在这里三千年了——互相陪伴。他们不恨封印他们的人——他们只是寂寞。", origin = "远古·灵魂监狱" },
            ["item_iron_sword"] = new ItemStory { displayName = "断水", rarityName = "精铁剑", story = "王铁柱年轻时凭记忆锻出的第一把灵剑。剑身上有一道隐约的纹路——那是他见过一头妖兽后刻上去的。这把剑不是最好的——但是是王铁柱最珍惜的。", origin = "王铁柱铸造" },
            ["item_phoenix_feather"] = new ItemStory { displayName = "不死之证", rarityName = "凤羽", story = "凤凰涅槃时脱落的羽毛。每一根都蕴含着一丝不死之力。传说集齐三根可以炼制不死药——但上一个尝试的人在丹炉前老死了。他等了凤凰三千年——凤凰没有回来。", origin = "凤凰·涅槃遗物" },
            ["item_amber_fossil"] = new ItemStory { displayName = "时光琥珀", rarityName = "琥珀化石", story = "一只远古蝴蝶被封在琥珀里。不是死了——是时间停止了。它的翅膀还在微微发光。三千年了——它在等有人把它放出来。", origin = "远古·时间囚笼" },
            ["item_spirit_lotus"] = new ItemStory { displayName = "轮回之莲", rarityName = "千年灵莲", story = "一千年开花——一千年凋谢。每一片花瓣都蕴含着一次轮回的记忆。吃下它——你会记起你的前世。你是第47个穿越者——但你不是第一次来这个世界。", origin = "轮回·前世记忆" },
            ["item_dragon_egg"] = new ItemStory { displayName = "龙之遗孤", rarityName = "龙蛋", story = "一颗还活着的龙蛋。蛋壳上刻着龙族的最后一句话：我们把最后的孩子留给你们。不要让虚空找到它。龙族灭绝了——但这颗蛋还在等待。", origin = "龙族·最后的遗孤" },
            ["item_star_dust"] = new ItemStory { displayName = "星之泪", rarityName = "星尘", story = "不是灰尘——是星星的尸体。每一颗星星死去的时候——会撒下这样的粉末。收集足够多——你就可以点燃一颗新的星星。", origin = "星辰·遗骸" },
            ["item_amber_fossil"] = new ItemStory { displayName = "时光琥珀", rarityName = "琥珀化石", story = "一只远古蝴蝶被封在琥珀里。不是死了——是时间停止了。它的翅膀还在微微发光。三千年了——它在等有人把它放出来。", origin = "远古·时间囚笼" },
            ["item_ancient_rune"] = new ItemStory { displayName = "天道碎片", rarityName = "远古符文", story = "天道的法则被刻在这些符文上。不是人刻的——是天道自己在崩溃时脱落下来的。每收集一枚符文——你就离理解这个世界的真相更近一步。也离天道崩溃的真正原因更近一步。", origin = "天道·崩溃碎片" },

            ["item_world_seed"] = new ItemStory { displayName = "创世之种", rarityName = "世界树种", story = "这不是这个世界的东西。它是地球意志投放的——每一颗种子——都是一个新的世界的可能性。这颗种子是给你的。不是让你种的。是让你理解的——你可以创造世界。", origin = "地球意志·创世之种" },


            ["item_phoenix_feather"] = new ItemStory { displayName = "不死之证", rarityName = "凤羽", story = "凤凰涅槃时脱落的羽毛。每一根都蕴含着一丝不死之力。传说集齐三根可以炼制不死药——但上一个尝试的人——在丹炉前老死了。他等了凤凰三千年——凤凰没有回来。", origin = "凤凰·涅槃遗物" },


            ["item_spirit_pearl"] = new ItemStory { displayName = "鲛人泪", rarityName = "灵珠", story = "传说鲛人哭泣时会落下珍珠。但这颗不是泪——是鲛人的眼珠。有人在海底发现了它——发现者第二天就失踪了。他的遗物里只有这颗珠子——和一句话：海里有人在叫我。", origin = "深海·鲛人之眼" },
            ["item_spirit_amulet"] = new ItemStory { displayName = "往生扣", rarityName = "灵蕴护符", story = "一位元婴修士在飞升前为凡间妻子炼制的最后一件东西。他说：戴上它——下辈子我会找到你。妻子戴了一辈子。她说：我不要下辈子——我要这辈子。", origin = "元婴修士·最后遗物" },

            ["item_treasure_map"] = new ItemStory { displayName = "故人归途", rarityName = "藏宝图碎片", story = "一共七片——指向上一个穿越者的遗书。他把自己的金手指埋在了那里。他希望有人能找到——在他死后。", origin = "第46号穿越者·遗物" },
            ["item_talisman_protection"] = new ItemStory { displayName = "平安扣", rarityName = "护身玉符", story = "一个母亲给即将远行的儿子的护身符。母亲是凡人——她不知道这块玉符能不能在修真者的世界里保护儿子。她只是把它放进儿子手里，说：'娘在家里等你。'儿子再也没有回来。玉符自己回来了——上面多了一道裂纹。", origin = "凡人母亲的祈愿" },
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
