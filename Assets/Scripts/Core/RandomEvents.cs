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
        }

        IEnumerator EventLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);
                if (Random.value < eventChance)
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
