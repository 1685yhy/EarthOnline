using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 闲话传播系统 —— 你做的好事/坏事会在NPC之间传播。
    /// 你今天帮了张老——三天后李灵儿也知道了。你偷了东西——全村人都躲着你。
    /// 社会运行逻辑：消息不会只停留在目击者那里。
    /// </summary>
    public class GossipSystem : MonoBehaviour
    {
        public static GossipSystem Instance { get; private set; }

        private List<Gossip> _activeGossips = new();
        private float _nextSpreadTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _nextSpreadTime = Time.time + 60f;
            EventBus.Subscribe("OnNPCInteract", OnInteraction);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
        }

        void Update()
        {
            if (Time.time >= _nextSpreadTime)
            {
                _nextSpreadTime = Time.time + Random.Range(60f, 120f);
                SpreadGossip();
            }
        }

        /// <summary>添加一条新闲话</summary>
        public void AddGossip(string content, string source, int impact)
        {
            _activeGossips.Add(new Gossip { content=content, source=source, impact=impact, time=Time.time });
            if (_activeGossips.Count > 10) _activeGossips.RemoveAt(0);
        }

        void SpreadGossip()
        {
            if (_activeGossips.Count == 0) return;

            var gossip = _activeGossips[Random.Range(0, _activeGossips.Count)];
            var allNpcs = Object.FindObjectsOfType<NPCBase>();

            if (allNpcs.Length == 0) return;

            // 随机选一个NPC"听说"了这个闲话
            var npc = allNpcs[Random.Range(0, allNpcs.Length)];
            string reaction = gossip.impact switch
            {
                >= 5 => $"'{npc.npcName}点点头：'听说了。做得好。''",
                <= -5 => $"'{npc.npcName}皱了皱眉：'原来是他干的...''",
                _ => $"'{npc.npcName}若有所思：'有意思...''"
            };

            Debug.Log($"[闲话] 🗣️ {npc.npcName}听说了：'{gossip.content}（来自{gossip.source}）' {reaction}");

            // 根据闲话影响NPC的记忆
            var mem = npc.GetComponent<NPCMemory>();
            if (mem != null && Mathf.Abs(gossip.impact) >= 3)
            {
                var type = gossip.impact > 0 ? MemoryType.Helped : MemoryType.Harmed;
                mem.Remember(type, $"听说:{gossip.content}", gossip.impact / 2);
            }
        }

        void OnInteraction(Dictionary<string, object> data)
        {
            string npcName = data.ContainsKey("npcName") ? data["npcName"].ToString() : "";
            if (!string.IsNullOrEmpty(npcName))
                AddGossip($"有人和{npcName}聊了很久", npcName, 1);
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            string enemy = data.ContainsKey("enemyName")?.ToString() ?? "敌人";
            AddGossip($"有人在附近击杀了{enemy}", "目击者", 3);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnInteraction);
            EventBus.Unsubscribe("OnEnemyKilled", OnEnemyKilled);
        }
    }

    class Gossip
    {
        public string content, source;
        public int impact;
        public float time;
    }
}
