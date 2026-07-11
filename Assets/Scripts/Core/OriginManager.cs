using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 出身管理器 —— 7种出身各有独特开场场景。
    /// 不是"随机降落"，而是"你在这个世界有自己的位置"。
    /// </summary>
    public enum PlayerOrigin
    {
        SectDisciple,    // 宗门嫡传
        NobleScion,      // 世家子弟
        RogueCultivator, // 散修
        Commoner,        // 平民
        Beggar,          // 乞丐
        MerchantChild,   // 商户之子
        ExiledOfficial,  // 被贬官宦
        DualSoul  // 双魂一体——穿越者与原主人共享身体，智勇互补
    }

    public class OriginManager : MonoBehaviour
    {
        public static OriginManager Instance { get; private set; }
        public static PlayerOrigin ChosenOrigin { get; private set; }

        private static readonly System.Random _rng = new System.Random();

        // 出身配置
        private static readonly Dictionary<PlayerOrigin, OriginConfig> _origins = new()
        {
            [PlayerOrigin.SectDisciple] = new OriginConfig {
                name = "宗门嫡传", description = "天元宗内门弟子，修炼天赋出众，有宗门庇护。但你的一举一动都在长老的注视之下。",
                startPos = new Vector3(12, 2, 8), startScene = "EarthOnline_Main",
                startRealm = "练气期", startLayer = 3, startSpiritStones = 500,
                startItems = new[] { ("item_pill_001", 3), ("item_iron_sword", 1) },
                openingText = "天元宗的晨钟响起。你从打坐中睁开眼睛。\n今天的功课是去后山采集聚灵花——这是每个内门弟子的日常。\n但你知道，你不只是一个普通的内门弟子。\n那个东西……在你体内。"
            },
            [PlayerOrigin.NobleScion] = new OriginConfig {
                name = "世家子弟", description = "王家三公子，锦衣玉食，人脉广阔。但家族内斗从未停止——你的两个哥哥不希望你活着继承家业。",
                startPos = new Vector3(-8, 1.5f, 5), startScene = "EarthOnline_Main",
                startRealm = "练气期", startLayer = 2, startSpiritStones = 2000,
                startItems = new[] { ("item_leather_armor", 1), ("item_heal_pill_001", 5) },
                openingText = "王府的马车停在了村口。你下车，看着这个偏僻的小镇。\n父亲的遗言还在耳边：'去找一个叫张老的人……他知道我们家族的秘密。'\n但你的两个哥哥也在找这个人。而且他们不在乎用什么手段。"
            },
            [PlayerOrigin.RogueCultivator] = new OriginConfig {
                name = "散修", description = "没有宗门、没有家族、没有靠山。在这片大陆上，散修是最自由的，也是最危险的。",
                startPos = new Vector3(0, 1.5f, 0), startScene = "EarthOnline_Main",
                startRealm = "练气期", startLayer = 1, startSpiritStones = 200,
                startItems = new[] { ("item_herb_001", 5) },
                openingText = "你在这片荒野已经走了三天。食物快吃完了，身上的灵石也只够再撑两天。\n前方有个小镇。也许可以在那里找到一些活计。\n自由是有代价的。这个代价，你每天都在付。"
            },
            [PlayerOrigin.Commoner] = new OriginConfig {
                name = "平民", description = "大周王朝的普通百姓。没有修炼天赋？不，只是还没有机会发现而已。",
                startPos = new Vector3(0, 1.5f, -3), startScene = "EarthOnline_Main",
                startRealm = "凡人", startLayer = 0, startSpiritStones = 100,
                startItems = new[] { ("item_herb_001", 2) },
                openingText = "你本来只是大周王朝一个普通的农民。\n直到三天前——你在地里挖出了一块发光的石头。\n那块石头碎了，你晕了过去。醒来时，你能看到空气中流动的光点。\n村里的人说，那是灵气。你不再是凡人了。"
            },
            [PlayerOrigin.Beggar] = new OriginConfig {
                name = "乞丐", description = "身无分文，衣衫褴褛。没有人会注意一个乞丐——这就是你最大的优势。",
                startPos = new Vector3(5, 1.5f, 8), startScene = "EarthOnline_Main",
                startRealm = "凡人", startLayer = 0, startSpiritStones = 10,
                startItems = new (string, int)[] { },
                openingText = "你在垃圾堆里翻到了一枚戒指。黑铁做的，看起来不值钱。\n但当你把它戴上的时候——一个老人的声音在你脑海中响起。\n'终于……有人戴上它了。小子，你想改变命运吗？'\n穷人没有选择。但这枚戒指给了你一个。"
            },
            [PlayerOrigin.MerchantChild] = new OriginConfig {
                name = "商户之子", description = "陈家商号的少东家。灵石不缺，但修炼资源需要自己打点。钱能解决很多问题——但不是全部。",
                startPos = new Vector3(-4, 1.5f, -6), startScene = "EarthOnline_Main",
                startRealm = "凡人", startLayer = 0, startSpiritStones = 1000,
                startItems = new[] { ("item_heal_pill_001", 3), ("item_pill_001", 2) },
                openingText = "你爹说：'修炼？那是烧灵石的无底洞。咱家是做生意的，踏踏实实赚钱不好吗？'\n但你知道，在这个世界上——没有修为，再多的灵石也保不住。\n你偷偷存了一笔钱。今天，你要去见一个人。一个能教你修炼的人。"
            },
            [PlayerOrigin.DualSoul] = new OriginConfig {
                name = "双魂一体", description = "你没有取代任何人。你和原主人的灵魂共存于同一具身体。他是这片区域最强的修士——但懦弱到被人欺负不敢还手。你帮他看穿谎言——他帮你横扫敌人。",
                startPos = new Vector3(-12, 2, -15), startScene = "EarthOnline_Main",
                startRealm = "元婴期", startLayer = 1, startSpiritStones = 5000,
                startItems = new[] { ("item_steel_sword", 1), ("item_dragon_scale_armor", 1), ("item_heal_pill_001", 10), ("item_cultivation_elixir", 3) },
                openingText = "你睁开眼睛——但这不是你的手。\n一只修长白净的手——元婴期修士的手——正微微发抖。\n'你是谁？'——一个声音在你脑海里响起，充满恐惧。\n那是原主人。他还活着。\n'我...我不知道你怎么进来的...但这是我的身体...'\n他的声音越来越小。他习惯了退让——对所有人都退让。\n小师妹推门进来了：'师兄！你又偷懒！长老要罚你！'\n她一边骂——一边顺手拿走了桌上的丹药。\n她在翻你的东西。她以为你看不见。\n但你看得清清楚楚。\n'她...她只是...'——原主人想辩解。\n你打断了他：'不是。她在偷你东西。睁大眼睛看着。'"
            },
            [PlayerOrigin.ExiledOfficial] = new OriginConfig {
                name = "被贬官宦", description = "曾经的朝中重臣，因得罪权贵被贬至边疆。但你在朝中的人脉和情报网依然存在。",
                startPos = new Vector3(3, 1.5f, -8), startScene = "EarthOnline_Main",
                startRealm = "练气期", startLayer = 1, startSpiritStones = 300,
                startItems = new[] { ("item_spirit_stone", 5) },
                openingText = "贬谪的路上，有人想杀你。你躲过了三次暗杀，但第四次来的时候——\n你的体内突然爆发出一股力量。那是……灵力？\n你从未修炼过。但那股力量救了你的命。\n现在你知道了：你的贬谪，不是因为政治。是因为有人发现了你的秘密。"
            }
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 随机选择出身和金手指，展示开场故事。
        /// </summary>
        public static (PlayerOrigin origin, OriginConfig config) RollOrigin(bool random = true, PlayerOrigin? specific = null)
        {
            if (!random && specific.HasValue)
                ChosenOrigin = specific.Value;
            else
            {
                var values = System.Enum.GetValues(typeof(PlayerOrigin));
                ChosenOrigin = (PlayerOrigin)values.GetValue(_rng.Next(values.Length));
            }

            var config = _origins[ChosenOrigin];
            Debug.Log($"═══════════════════════════════════");
            Debug.Log($"  出身：{config.name}");
            Debug.Log($"  {config.description}");
            Debug.Log($"═══════════════════════════════════");
            Debug.Log($"");
            Debug.Log($"{config.openingText}");
            Debug.Log($"");

            return (ChosenOrigin, config);
        }

        /// <summary>
        /// 应用出身配置到玩家
        /// </summary>
        public static void ApplyOrigin(PlayerOrigin origin, GameObject player)
        {
            var config = _origins[origin];

            // 设置位置
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = config.startPos;
            if (cc != null) cc.enabled = true;

            // 设置起始灵石
            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.spiritStones = config.startSpiritStones;
                stats.UpdateHUD();
            }

            // 给予起始物品
            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                foreach (var (itemId, qty) in config.startItems)
                {
                    inv.AddItem(new Item { id = itemId, name = itemId, quantity = qty });
                }
            }

            Debug.Log($"[Origin] 出身已应用: {config.name} | {config.startRealm} | 灵石:{config.startSpiritStones}");
        }

        public class OriginConfig
        {
            public string name, description, startScene, startRealm, openingText;
            public Vector3 startPos;
            public int startLayer, startSpiritStones;
            public (string itemId, int qty)[] startItems;
        }
    }
}
