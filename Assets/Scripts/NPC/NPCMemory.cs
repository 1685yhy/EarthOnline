using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V2.1 NPC记忆系统 —— NPC记住玩家做过的事，不只是talkCount。
    /// 每个事件有类型+情绪标签+时间戳，NPC基于记忆改变态度。
    /// 不再是"好感度75/100"——是"你偷过我的东西，但你也救过我。我该信你吗？"
    /// </summary>
    public enum MemoryType { Helped, Harmed, GaveGift, Stole, WitnessedCombat, SavedLife, Betrayed, TalkedTo, Ignored }

    [System.Serializable]
    public class NPCMemoryEntry
    {
        public MemoryType type;
        public string description;   // "帮我采了3株止血草"
        public int emotionalWeight;  // -10到+10，影响态度
        public float timestamp;      // 发生时间
        public int dayOccurred;      // 发生在第几天
        public string location;      // 发生地点
    }

    [RequireComponent(typeof(NPCBase))]
    public class NPCMemory : MonoBehaviour
    {
        private NPCBase _npc;
        private List<NPCMemoryEntry> _memories = new();
        private Dictionary<MemoryType, int> _typeCounts = new();

        // 综合态度：基于所有记忆的加权平均
        public int NetAttitude
        {
            get
            {
                if (_memories.Count == 0) return 0;
                int sum = 0;
                foreach (var m in _memories) sum += m.emotionalWeight;
                return Mathf.Clamp(sum, -100, 100);
            }
        }

        // 信任度：正面记忆越多越信任，但一次背叛会大幅降低
        public int TrustLevel
        {
            get
            {
                int trust = NetAttitude;
                if (HasMemoryOfType(MemoryType.Betrayed)) trust -= 30;
                if (HasMemoryOfType(MemoryType.SavedLife)) trust += 25;
                return Mathf.Clamp(trust, -100, 100);
            }
        }

        public int MemoryCount => _memories.Count;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            EventBus.Subscribe("OnNPCInteract", OnInteraction);
            EventBus.Subscribe("OnItemAdded", OnItemPickup);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
        }

        /// <summary>记录一个新记忆</summary>
        public void Remember(MemoryType type, string description, int weight = 1)
        {
            var mem = new NPCMemoryEntry
            {
                type = type, description = description, emotionalWeight = weight,
                timestamp = Time.time,
                dayOccurred = TimeManager.Instance?.GameDay ?? 1,
                location = _npc != null ? _npc.npcName + "附近" : "未知"
            };
            _memories.Add(mem);

            if (!_typeCounts.ContainsKey(type)) _typeCounts[type] = 0;
            _typeCounts[type]++;

            // 重要记忆——NPC可能会主动提起
            if (Mathf.Abs(weight) >= 5)
            {
                Debug.Log($"[记忆·{_npc?.npcName}] {(weight > 0 ? "💚" : "💔")} {description} (态度:{NetAttitude})");
            }
        }

        public bool HasMemoryOfType(MemoryType type) => _typeCounts.ContainsKey(type) && _typeCounts[type] > 0;

        /// <summary>NPC根据记忆生成的随机自语</summary>
        public string GetMemoryReflection()
        {
            if (_memories.Count == 0) return null;

            var recent = _memories[_memories.Count - 1];
            if (recent.emotionalWeight >= 5)
                return "上次的事...多谢了。";
            if (recent.emotionalWeight <= -5)
                return "...你还有脸来见我？";
            if (HasMemoryOfType(MemoryType.SavedLife))
                return "你救过我的命。我不会忘。";
            if (HasMemoryOfType(MemoryType.Betrayed))
                return "我不确定还能不能信你。";
            if (HasMemoryOfType(MemoryType.GaveGift))
                return "你送的东西...我一直留着。";

            return null;
        }

        void OnInteraction(Dictionary<string, object> data)
        {
            string npcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            if (npcId != _npc?.npcId) return;
            Remember(MemoryType.TalkedTo, $"与{_npc?.npcName}交谈", 1);
        }

        void OnItemPickup(Dictionary<string, object> data)
        {
            // 如果捡到和此NPC有关的物品
            string itemId = data.ContainsKey("itemId") ? data["itemId"].ToString() : "";
            // 未来：根据物品ID判断是否和此NPC有关
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || _npc == null) return;
            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (dist < 20f)
                Remember(MemoryType.WitnessedCombat, $"目睹玩家在附近击杀{data["enemyName"]}", 2);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnInteraction);
            EventBus.Unsubscribe("OnItemAdded", OnItemPickup);
            EventBus.Unsubscribe("OnEnemyKilled", OnEnemyKilled);
        }
    }
}
