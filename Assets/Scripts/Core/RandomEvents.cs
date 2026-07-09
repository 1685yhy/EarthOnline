using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 随机事件系统 —— 游戏中随机触发特殊事件，增加可玩性。
    /// </summary>
    public class RandomEvents : MonoBehaviour
    {
        public float checkInterval = 120f; // 每2分钟检查一次
        public float eventChance = 0.3f;

        private List<GameEvent> _events = new();
        private string _lastEvent = "";

        [System.Serializable]
        public class GameEvent
        {
            public string id, title, description;
            public System.Action OnTrigger;
        }

        void Start()
        {
            SetupEvents();
            StartCoroutine(EventLoop());
        }

        void SetupEvents()
        {
            _events.Add(new GameEvent { id = "merchant", title = "流浪商人来了！",
                description = "陈半仙的哥哥陈大仙路过村子，所有物品8折！",
                OnTrigger = () => {
                    Debug.Log("[Event] 🧳 流浪商人路过！陈半仙的商店8折优惠(仅限今日)。");
                    var stats = PlayerStats.Instance;
                    if (stats != null) stats.AddSpiritStone(50);
                    Debug.Log("[Event] 陈大仙给了你50灵石的见面礼。");
                }
            });

            _events.Add(new GameEvent { id = "wolf_raid", title = "狼群袭击！",
                description = "村子周围的野狼变得异常活跃，攻击力+50%。但击杀掉落翻倍。",
                OnTrigger = () => {
                    Debug.Log("[Event] 🐺 狼群袭击！敌人变强了但掉落更丰厚。");
                    // Spawn extra enemy
                    for (int i = 0; i < 2; i++)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        go.name = $"Event_Wolf_{i}";
                        go.transform.position = new Vector3(Random.Range(-10, 10), 1, Random.Range(-10, 10));
                        go.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
                        Object.DestroyImmediate(go.GetComponent<Rigidbody>());
                        var t = System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");
                        if (t != null)
                        {
                            var c = go.AddComponent(t);
                            t.GetField("enemyId")?.SetValue(c, "wolf_event");
                            t.GetField("enemyName")?.SetValue(c, "狂暴野狼");
                            t.GetField("maxHP")?.SetValue(c, 60);
                            t.GetField("attackPower")?.SetValue(c, 10);
                            t.GetField("dropItemId")?.SetValue(c, "item_spirit_stone");
                            t.GetField("dropItemName")?.SetValue(c, "灵石碎片");
                            t.GetField("dropQuantity")?.SetValue(c, 4);
                        }
                        var r = go.GetComponent<Renderer>();
                        if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.6f,0.2f,0.1f); r.material = m; }
                    }
                }
            });

            _events.Add(new GameEvent { id = "spirit_rain", title = "灵雨降临！",
                description = "天空降下灵力之雨，修为获取+100%。",
                OnTrigger = () => {
                    Debug.Log("[Event] ✨ 灵雨降临！修为获取翻倍，持续今日。");
                    var stats = PlayerStats.Instance;
                    if (stats != null) stats.AddCultivation(50);
                    Debug.Log("[Event] 你感受到灵力涌入体内 +50修为。");
                }
            });

            _events.Add(new GameEvent { id = "goblin", title = "宝藏哥布林！",
                description = "一只背着宝袋的哥布林！击败它获得灵石！",
                OnTrigger = () => {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = "Goblin_Treasure"; go.transform.position = new Vector3(Random.Range(-8,8), 1, Random.Range(-8,8));
                    go.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
                    Object.DestroyImmediate(go.GetComponent<Rigidbody>());
                    var t = System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");
                    if (t != null) {
                        var c = go.AddComponent(t);
                        t.GetField("enemyId")?.SetValue(c, "goblin_treasure");
                        t.GetField("enemyName")?.SetValue(c, "宝藏哥布林");
                        t.GetField("maxHP")?.SetValue(c, 20); t.GetField("attackPower")?.SetValue(c, 0);
                        t.GetField("moveSpeed")?.SetValue(c, 6f); t.GetField("detectRange")?.SetValue(c, 15f);
                        t.GetField("dropItemId")?.SetValue(c, "item_spirit_core_001");
                        t.GetField("dropItemName")?.SetValue(c, "灵气核心"); t.GetField("dropQuantity")?.SetValue(c, 2);
                    }
                    var rr = go.GetComponent<Renderer>();
                    if (rr != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(1f,0.85f,0.1f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f,0.85f,0.1f)*0.5f); rr.material = m; }
                    PlayerStats.Instance?.AddSpiritStone(100);
                }
            });

            _events.Add(new GameEvent { id = "treasure_map", title = "发现藏宝图！",
                description = "在地上捡到一张破旧的藏宝图。宝箱已重置！",
                OnTrigger = () => {
                    Debug.Log("[Event] 🗺️ 发现藏宝图！所有宝箱已刷新。");
                    var chests = Object.FindObjectsOfType<TreasureChest>();
                    foreach (var c in chests) c.isOpened = false;
                }
            });

            _events.Add(new GameEvent { id = "secret_realm", title = "秘境裂缝出现！",
                description = "一道空间裂缝在空中裂开——秘境入口出现了。限时30分钟！",
                OnTrigger = () => {
                    Debug.Log("[Event] 🌌 秘境裂缝！限时30分钟，先到先得！");
                    var stats = PlayerStats.Instance;
                    if (stats != null) { stats.AddCultivation(30); stats.spiritStones += 80; }
                    Debug.Log("[Event] 你在裂缝边缘吸收了大量逸散灵气：+30修为 +80灵石。");
                }
            });

            _events.Add(new GameEvent { id = "meteor", title = "天降陨石！",
                description = "一颗陨石坠落在村子附近。里面可能有好东西——也可能有不好的东西。",
                OnTrigger = () => {
                    Debug.Log("[Event] ☄️ 天降陨石！一颗发着紫光的陨石坠落在不远处。");
                    var stats = PlayerStats.Instance;
                    if (stats != null) { stats.AddCultivation(50); stats.spiritStones += 100; }
                    Debug.Log("[Event] 陨石碎片中蕴含着浓郁的灵气 +50修为 +100灵石。");
                    Debug.Log("[Event] 但陨石坑里还有一些...蠕动的东西。最好不要在那里待太久。");
                }
            });

            _events.Add(new GameEvent { id = "elder_visit", title = "神秘老者来访",
                description = "一位白发老者来到村子。他说他在找一个人——'戴着黑铁戒指的人'。",
                OnTrigger = () => {
                    Debug.Log("[Event] 👴 神秘老者：'我在找一个人。一个戴着黑铁戒指的人。你见过吗？'");
                    Debug.Log("[Event] 老者没有等你回答，自顾自地走远了。但他走后——");
                    Debug.Log("[Event] 你发现地上有一卷泛黄的羊皮纸。上面写着：'天陨丹方——残卷（一）'。");
                    var stats = PlayerStats.Instance;
                    if (stats != null) stats.AddCultivation(100);
                    Debug.Log("[Event] 残卷中蕴含的古老知识让你获得了+100修为。");
                }
            });

            var hunterEvent = new GameEvent { id = "hunter_guild", title = "猎人工会招募！", description = "猎人工会今日招募新人。击杀妖兽可获得额外奖励。" };
            hunterEvent.OnTrigger = () => { Debug.Log("[Event] 🏹 猎人工会招募！击杀妖兽掉落翻倍，持续10分钟。"); };
            _events.Add(hunterEvent);
        }

        IEnumerator EventLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);
                if (Random.value < eventChance)
            _events.Add(new GameEvent { id = "heavens_tear", title = "天裂", description = "天空裂开了一道口子——不是虚空，是飞升通道。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(500); Debug.Log("[Event] 🌌 天裂！飞升通道短暂打开——逸散的仙灵之气涌入了这个世界。+500修为。"); Debug.Log("[Event] 通道关闭前——你听到了一个声音：上来——我们在这里等你。"); } });
            _events.Add(new GameEvent { id = "abyss_opens", title = "深渊裂开", description = "大地裂开了一道深渊——不是虚空，是更古老的东西。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(200); Debug.Log("[Event] 🌑 深渊裂开。底下不是虚空——是上一个轮回的灵气大陆。埋在下面三千年了。下面的东西——在往上爬。"); } });
            _events.Add(new GameEvent { id = "final_warning", title = "最终警告", description = "所有穿越者的残影同时出现在你面前。", OnTrigger = () => { Debug.Log("[Event] 👻 46个穿越者的残影同时出现。他们齐声说：我们失败了——但你还有机会。虚空知道你的存在了。它在加速。你只有30天。"); PlayerStats.Instance?.AddCultivation(300); } });
            _events.Add(new GameEvent { id = "reincarnation", title = "轮回异象", description = "你看到了前世的最后时刻。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(300); Debug.Log("[Event] 🔄 第46号穿越者最后的警告：不要相信地球意志——它在利用我们。"); } });
            _events.Add(new GameEvent { id = "void_storm", title = "虚空风暴", description = "虚空风暴席卷大陆——敌人强化但奖励x3。", OnTrigger = () => { Debug.Log("[Event] 虚空风暴！野外敌人被虚空强化——击杀奖励x3。持续5分钟。"); } });
            _events.Add(new GameEvent { id = "immortal_herb", title = "仙草现世", description = "一株万年灵芝在山巅发光——所有修士都看到了。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(100); Debug.Log("[Event] 仙草现世——但天元宗已经派人去采了。你得在他们之前赶到。"); } });
            _events.Add(new GameEvent { id = "betrayal", title = "叛徒", description = "有人出卖了你——你的行踪被透露给了猎血人。", OnTrigger = () => { Debug.Log("[Event] 有人出卖了你——猎血人知道了你的位置。小心——他就在附近。"); ReputationSystem.Instance?.AddInfamy(10, "被出卖"); } });
            _events.Add(new GameEvent { id = "ancient_war", title = "上古战场重现", description = "地下的古代战场浮上地表——满是遗骸和宝物。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(150); Debug.Log("[Event] 上古战场重现——三千年前的修士大战遗迹。骸骨中还握着未碎的灵剑。"); } });
            _events.Add(new GameEvent { id = "divine_beast", title = "神兽现世", description = "传说中的麒麟出现了——它在找人。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(300); Debug.Log("[Event] 麒麟现世：第47号——我在你身上看到了地球意志的印记。跟我来——我带你去见其他穿越者。他们还活着。"); } });
            _events.Add(new GameEvent { id = "world_memory", title = "世界记忆", description = "灵气大陆本身开始对你说话。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(500); Debug.Log("[Event] 灵气大陆：我是活的。地球意志创造了我——但虚空腐蚀了我三千年。第47号——你是我的解药。"); } });
            _events.Add(new GameEvent { id = "void_king", title = "虚空之王苏醒", description = "虚空的真正主人醒来——它注意到了你。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(500); Debug.Log("[Event] 虚空之王：第47号——你以为你在拯救世界？你以为地球意志是你朋友？来虚空边缘——我告诉你真相。"); } });
            _events.Add(new GameEvent { id = "hero_returns", title = "英雄归来", description = "一位失踪的穿越者活着回来了。", OnTrigger = () => { Debug.Log("[Event] 第3号穿越者回来了——他在虚空里待了五百年。他说：虚空不是敌人——虚空是牢笼。地球意志把我们关在这里——是为了保护外面的世界。"); PlayerStats.Instance?.AddCultivation(300); } });
            _events.Add(new GameEvent { id = "celestial_trial", title = "天劫降临", description = "不是你的天劫——是大成期修士的。", OnTrigger = () => { Debug.Log("[Event] 有人在大成飞升！天劫的余波席卷了半个大陆。所有修士获得+100修为——来自飞升者散逸的灵力。"); PlayerStats.Instance?.AddCultivation(100); } });
            _events.Add(new GameEvent { id = "underground_city", title = "地下城出现", description = "一座被封印万年的地下城市浮出地面。", OnTrigger = () => { Debug.Log("[Event] 地下城——不是遗迹。里面还有人。他们问：虚空战争——结束了吗？"); PlayerStats.Instance?.AddCultivation(200); } });
            _events.Add(new GameEvent { id = "final_prophecy", title = "最终预言", description = "天道亲自给出了预言——关于你。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(600); Debug.Log("[Event] 天道预言：第48个穿越者——将会终结虚空——或者成为虚空。选择在你。时间不多了。"); } });
            _events.Add(new GameEvent { id = "world_merge", title = "世界融合", description = "灵气大陆和虚空开始融合——两个世界的法则碰撞。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(1000); Debug.Log("[Event] 世界融合——虚空和灵气大陆的边界消失了。地球意志的声音：第47号——最后的选择——就在此刻。"); } });
            _events.Add(new GameEvent { id = "all_heroes", title = "穿越者集结", description = "所有存活的穿越者聚集在一起——准备最后的战斗。", OnTrigger = () => { Debug.Log("[Event] 穿越者集结——7位穿越者站在你面前。第1号说：我们准备了很久。第46号说：我承认——我曾经恨你。但现在——我愿意和你并肩。"); PlayerStats.Instance?.AddCultivation(500); } });
            _events.Add(new GameEvent { id = "final_choice", title = "最终抉择", description = "地球意志和虚空之王同时对你说话。你必须选择。", OnTrigger = () => { Debug.Log("[Event] 地球意志：相信我——我是为了所有世界。虚空之王：相信我——我是为了真相。第47号——你选择谁？"); PlayerStats.Instance?.AddCultivation(800); } });
            _events.Add(new GameEvent { id = "solar_eclipse", title = "日蚀", description = "太阳被黑暗吞噬——灵气消失了3分钟。", OnTrigger = () => { Debug.Log("[Event] 🌑 日蚀！灵气消失了3分钟——所有修士陷入恐慌。但凡人没事。你意识到：灵气不是自然存在的——是被人为注入这个世界的。"); PlayerStats.Instance?.AddCultivation(50); } });
            _events.Add(new GameEvent { id = "earth_cries", title = "大地悲鸣", description = "地球意志的哭声传遍了整个大陆。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(500); Debug.Log("[Event] 地球意志在哭泣...虚空源头不是这个世界。"); } });
            _events.Add(new GameEvent { id = "fate_converges", title = "命运交汇", description = "所有47个穿越者的命运线在此刻交汇。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(500); Debug.Log("[Event] 命运交汇——47条线在此刻重叠。你看到了所有穿越者的最后一刻。他们齐声说：你不是一个人。"); } });
            _events.Add(new GameEvent { id = "prophecy", title = "古老预言", description = "一块石碑从地下升起——上面刻着第48个穿越者的预言。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(400); Debug.Log("[Event] 石碑预言：第48个穿越者将会终结虚空——但代价是成为新的虚空。"); } });
            _events.Add(new GameEvent { id = "moon_falls", title = "月落", description = "月亮——坠落了。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(300); Debug.Log("[Event] 月亮坠落——不是真的月亮，是封印在月亮上的一个远古存在挣脱了。它说：虚空是我创造的。我很抱歉。"); } });
            _events.Add(new GameEvent { id = "void_whispers", title = "虚空低语", description = "虚空直接对你说话了——不是威胁，是请求。", OnTrigger = () => { Debug.Log("[Event] 虚空低语：我不是敌人。我是上一个轮回的地球意志。我被困在这里——帮我出去。作为交换——我会告诉你所有世界的真相。"); PlayerStats.Instance?.AddCultivation(200); } });
            _events.Add(new GameEvent { id = "void_expansion", title = "虚空扩张", description = "虚空裂缝——比昨天大了一倍。", OnTrigger = () => { Debug.Log("[Event] 🕳️ 虚空裂缝扩大了一倍。张老的声音从远处传来：它不会停的——直到吞掉一切。"); PlayerStats.Instance?.AddCultivation(50); } });
            _events.Add(new GameEvent { id = "forest_awakens", title = "森林苏醒", description = "古树精从千年沉睡中醒来——它在找人。", OnTrigger = () => { Debug.Log("[Event] 🌳 森林苏醒！古树精低沉的声音回荡：第47号...来见我。我有话——关于你之前的46个人。"); } });
            _events.Add(new GameEvent { id = "starfall", title = "星辰坠落", description = "一颗星星从天而降——不是陨石，是一座塔。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(150); Debug.Log("[Event] 🌟 星之塔坠落！一座来自天外的塔插在大地上。门上写着：入此塔者——可窥天道。"); } });
            _events.Add(new GameEvent { id = "immortal_visits", title = "仙人下凡", description = "一位真正的仙人下凡——他在找一个人。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(200); Debug.Log("[Event] ✨ 仙人下凡！他环顾四周：第47号...你在这里。你体内的东西——不属于这个世界。它在找你。做好准备。"); } });

            _events.Add(new GameEvent { id = "blood_moon", title = "血月当空", description = "月亮变成了血红色——妖兽狂暴，但击杀奖励翻倍。", OnTrigger = () => { Debug.Log("[Event] 🌑 血月！妖兽狂暴——但击杀奖励x2。持续至天明。"); } });
            _events.Add(new GameEvent { id = "sage_appears", title = "圣人降临", description = "一位大乘期修士路过——他看了你一眼。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(200); Debug.Log("[Event] 👁️ 大乘期修士看了你一眼——有意思。你体内有不止一个世界的力量。等你到了渡劫期——来找我。他留下了坐标。"); } });

            _events.Add(new GameEvent { id = "time_rift", title = "时间裂缝", description = "一道时间裂缝短暂打开——你看到了过去。", OnTrigger = () => { Debug.Log("[Event] ⏰ 时间裂缝！你看到了三百年前的灵气大陆——那时的虚空还没有来。那时的张老还是个年轻人。他的妻子——还在他身边。"); PlayerStats.Instance?.AddCultivation(80); Debug.Log("[Event] 裂缝关闭了。但那幅画面——你忘不掉。"); } });

            _events.Add(new GameEvent { id = "blizzard", title = "暴风雪", description = "突如其来的暴风雪——妖兽躲进巢穴，灵脉被冰封。", OnTrigger = () => { Debug.Log("[Event] ❄️ 暴风雪！妖兽躲藏——但灵脉被冰封，修炼效率下降。"); } });
            _events.Add(new GameEvent { id = "earth_voice", title = "大地之音", description = "大地深处传来低沉的声音——地球意志在呼唤。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(120); Debug.Log("[Event] 🌍 大地之音：你做得很好——但还不够。虚空在加速。你需要在它到来之前变得更强。"); Debug.Log("[Event] 地球意志直接对你说话了。这是第一次——不会是最后一次。"); } });

            _events.Add(new GameEvent { id = "phoenix", title = "凤凰涅槃", description = "一只凤凰在远处涅槃——凤羽散落大地。", OnTrigger = () => { Debug.Log("[Event] 🔥 凤凰涅槃！凤羽散落。+100修为。"); PlayerStats.Instance?.AddCultivation(100); Debug.Log("[Event] 传说集齐三根凤羽可以炼制不死药——但从未有人做到过。"); } });

            _events.Add(new GameEvent { id = "stars_aligned", title = "七星连珠", description = "七颗星辰排成一线——天地灵气暴涨。", OnTrigger = () => { Debug.Log("[Event] 🌟 七星连珠！天地灵气暴涨！所有修炼效率翻倍持续至天明。"); PlayerStats.Instance?.AddCultivation(70); } });
            _events.Add(new GameEvent { id = "refugees", title = "难民潮", description = "南部小镇被虚空侵蚀——难民逃到了这里。", OnTrigger = () => { Debug.Log("[Event] 🏃 难民潮！南边的镇子没了——被虚空吞了。难民们带来了消息：虚空的扩张速度在加快。"); Debug.Log("[Event] 一个小女孩拉着你的衣角：大哥哥/大姐姐——你能帮我们打回去吗？"); PlayerStats.Instance?.AddSpiritStone(20); } });

            _events.Add(new GameEvent { id = "cave_in", title = "矿洞塌方", description = "北部矿脉发生了塌方！有矿工被困。", OnTrigger = () => { Debug.Log("[Event] ⛏️ 矿难！你救出了一个矿工。他感激地给了你报酬。+50灵石 +30修为"); PlayerStats.Instance?.AddSpiritStone(50); PlayerStats.Instance?.AddCultivation(30); } });

            _events.Add(new GameEvent { id = "ancient_ruins", title = "遗迹发光", description = "地底深处传来震动——远古遗迹在苏醒。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(100); Debug.Log("[Event] 🏛️ 远古遗迹！地面裂开——一座被封印万年的地宫露出了入口。+100修为。"); Debug.Log("[Event] 地宫入口的石门上刻着一行字：我们封印的不是怪物——是我们自己。"); } });

            _events.Add(new GameEvent { id = "rare_herb", title = "珍稀药草出现", description = "一株百年难遇的灵草在附近出现了！", OnTrigger = () => { Debug.Log("[Event] 🌿 发现百年灵草！+80修为。——可惜刚采完就被天元宗的采药队看到了。他们记下了你的脸。"); PlayerStats.Instance?.AddCultivation(80); FactionSystem.Instance?.ModifyReputation("tianyuan", -5); } });

            _events.Add(new GameEvent { id = "ghost_rumors", title = "鬼镇传闻", description = "传言南边废弃的镇子晚上会发出奇怪的光。", OnTrigger = () => { Debug.Log("[Event] 👻 南边废弃小镇晚上有光——不是鬼火，是灵力波动。那里可能有被封存的法宝。"); } });
            _events.Add(new GameEvent { id = "rainbow_cloud", title = "七彩祥云", description = "天边出现了七彩祥云——吉兆！", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(30); PlayerStats.Instance?.AddSpiritStone(50); Debug.Log("[Event] 🌈 七彩祥云！吉兆降临。+30修为 +50灵石。"); } });
            _events.Add(new GameEvent { id = "wandering_poet", title = "游吟诗人", description = "一个游吟诗人在客栈里弹唱。歌词里有虚空裂缝的秘密。", OnTrigger = () => { Debug.Log("[Event] 🎵 游吟诗人唱道：虚空有口，吞天噬地；穿越者来，有去无回。——这是在说虚空裂缝？"); PlayerStats.Instance?.AddCultivation(20); } });

            _events.Add(new GameEvent { id = "dragon_sighting", title = "龙影掠过", description = "天空中有巨大的影子飞过——龙？", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(50); Debug.Log("[Event] 🐉 龙影！天空中有龙飞过——所有人都看到了。+50修为。"); } });
            _events.Add(new GameEvent { id = "merchant_caravan", title = "大商队抵达", description = "一支大型商队抵达村子。稀有商品限时供应！", OnTrigger = () => { Debug.Log("[Event] 🐪 大商队！所有商店库存翻倍、价格-30%。"); PlayerStats.Instance?.AddSpiritStone(30); } });

            _events.Add(new GameEvent { id = "full_moon", title = "满月之夜", description = "月圆之夜——灵气浓度翻倍。修炼事半功倍。", OnTrigger = () => { Debug.Log("[Event] 🌕 满月！灵气浓度翻倍——修炼效率x2持续至天明。"); } });

            _events.Add(new GameEvent { id = "tournament", title = "宗门大比！", description = "一年一度的宗门比武大会。观众也有奖励。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(40); Debug.Log("[Event] 🏟️ 宗门大比！观看比赛+40修为。"); Debug.Log("[Event] 天元宗的代表——是一个只有十岁的孩子。他在决赛中击败了所有成年修士。观众席上的大人们在鼓掌——但他们的眼睛里不是骄傲。是恐惧。"); } });

                {
                    TriggerRandomEvent();
                }
            }
        }

        void TriggerRandomEvent()
        {
            var available = _events.FindAll(e => e.id != _lastEvent);
            if (available.Count == 0) available = _events;

            var evt = available[Random.Range(0, available.Count)];
            _lastEvent = evt.id;

            Debug.Log($"══════════════════════════════");
            Debug.Log($"  ⚡ 随机事件: {evt.title}");
            Debug.Log($"  {evt.description}");
            Debug.Log($"══════════════════════════════");

            evt.OnTrigger?.Invoke();

            EventBus.Publish("OnRandomEvent", new Dictionary<string, object> {
                {"id", evt.id}, {"title", evt.title}
            });
        }
    }
}
