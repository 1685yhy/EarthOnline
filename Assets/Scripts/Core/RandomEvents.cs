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
            _events.Add(new GameEvent { id = "meteor_shower", title = "流星雨！", description = "百年一遇的流星雨。许愿吧。", OnTrigger = () => { PlayerStats.Instance?.AddCultivation(60); Debug.Log("[Event] 🌠 流星雨！许愿+60修为。"); } });
            _events.Add(new GameEvent { id = "beast_tide", title = "兽潮来袭！", description = "妖兽大规模迁徙——冲过村子。", OnTrigger = () => { Debug.Log("[Event] 🐗 兽潮！大量妖兽冲过村子。"); for(int i=0;i<3;i++){var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="Beast_"+i;go.transform.position=new Vector3(Random.Range(-10,10),1,Random.Range(-10,10));go.transform.localScale=new Vector3(0.8f,1f,0.8f);Object.DestroyImmediate(go.GetComponent<Rigidbody>());var t=System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");if(t!=null){var c=go.AddComponent(t);t.GetField("enemyId")?.SetValue(c,"beast_"+i);t.GetField("enemyName")?.SetValue(c,"狂暴妖兽");t.GetField("maxHP")?.SetValue(c,30);t.GetField("attackPower")?.SetValue(c,8);t.GetField("moveSpeed")?.SetValue(c,4f);t.GetField("dropItemId")?.SetValue(c,"item_spirit_stone");t.GetField("dropItemName")?.SetValue(c,"灵石碎片");t.GetField("dropQuantity")?.SetValue(c,2);}var r=go.GetComponent<Renderer>();if(r!=null){var m=new Material(Shader.Find("Standard"));m.color=new Color(0.6f,0.1f,0.1f);r.material=m;}} Debug.Log("[Event] 兽潮退去——留下了满地战利品。"); } });

                {
                    var e1 = new GameEvent { id = "master_visits", title = "高人路过", description = "一位隐世高人路过村子。" };
                    e1.OnTrigger = () => { PlayerStats.Instance?.AddCultivation(80); Debug.Log("[Event] 🧙 隐世高人路过！+80修为"); };
                    _events.Add(e1);
                    var e2 = new GameEvent { id = "smugglers", title = "走私贩来了", description = "一伙走私贩在村口摆摊——价格极低但来源不明。" };
                    e2.OnTrigger = () => { PlayerStats.Instance?.spiritStones += 30; Debug.Log("[Event] 🕵️ 走私贩 +30灵石"); };
                    _events.Add(e2);
                    var e3 = new GameEvent { id = "heavenly_omens", title = "天降异象！", description = "天空中出现了一道金色的裂缝——有人在飞升？" };
                    e3.OnTrigger = () => { PlayerStats.Instance?.AddCultivation(100); Debug.Log("[Event] 🌟 天降异象！+100修为"); };
                    _events.Add(e3);
                }

            _events.Add(new GameEvent { id = "auction", title = "拍卖会开始！", description = "稀有物品拍卖！谁出价高归谁。", OnTrigger = () => { Debug.Log("[Event] 🔨 拍卖会！灵气核心正在拍卖..."); var s = PlayerStats.Instance; if(s!=null && s.spiritStones>=100){s.spiritStones-=100; s.AddCultivation(50); Debug.Log("[Event] 你以100灵石拍到了灵气核心！+50修为。");}else{Debug.Log("[Event] 灵石不够——被别人拍走了。");} } });
            _events.Add(new GameEvent { id = "festival", title = "灵灯节！", description = "一年一度的灵灯节，全村欢庆。", OnTrigger = () => { Debug.Log("[Event] 🏮 灵灯节！村子沉浸在节日的喜悦中。所有NPC好感+2。"); var npcs = Object.FindObjectsOfType<EarthOnline.NPC.NPCBase>(); foreach(var n in npcs){var m=n.GetComponent<EarthOnline.NPC.NPCMemory>();if(m!=null)m.Remember(EarthOnline.NPC.MemoryType.Helped,"一起庆祝灵灯节",3);} } });
            _events.Add(new GameEvent { id = "earthquake", title = "地动！", description = "一场地震——震开了一个隐藏的洞口。", OnTrigger = () => { Debug.Log("[Event] 🌋 地动山摇！一个新洞口出现了。+30修为（探索奖励）。"); PlayerStats.Instance?.AddCultivation(30); } });

                {
                    TriggerRandomEvent();
                }
            }
            _events.Add(new GameEvent { id = "market_day", title = "月度坊市开放！", description = "每月一次的大型坊市今日开放。", OnTrigger = () => { Debug.Log("[Event] 🏪 月度坊市！所有商人价格-20%。"); PlayerStats.Instance.spiritStones += 50; Debug.Log("[Event] 捡到钱包 +50灵石。"); } });

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
