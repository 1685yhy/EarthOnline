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
