using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 敌人刷新管理器 —— 每天刷新敌人，记录被击杀的敌人。
    /// </summary>
    public class EnemyRespawner : MonoBehaviour
    {
        public static EnemyRespawner Instance { get; private set; }

        [System.Serializable]
        public class EnemySpawnPoint
        {
            public string prefabName;
            public Vector3 position;
            public string enemyId;
            public string enemyName;
            public int maxHP;
            public int attackPower;
            public float moveSpeed;
            public float detectRange;
            public float patrolRadius;
            public string dropItemId;
            public string dropItemName;
            public int dropQuantity;
            public Color color;
        }

        private List<EnemySpawnPoint> _spawnPoints = new List<EnemySpawnPoint>();
        private int _killedToday;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
            RegisterSpawnPoints();
        }

        void RegisterSpawnPoints()
        {
            _spawnPoints = new List<EnemySpawnPoint> {
                new() { position = new Vector3(-12, 1, -3), enemyId = "wolf_001", enemyName = "野狼",
                    maxHP = 40, attackPower = 6, moveSpeed = 2.5f, detectRange = 10f, patrolRadius = 8f,
                    dropItemId = "item_spirit_stone", dropItemName = "灵石碎片", dropQuantity = 2,
                    color = new Color(0.4f, 0.3f, 0.2f) },
                new() { position = new Vector3(10, 1, -8), enemyId = "wolf_002", enemyName = "灰狼",
                    maxHP = 40, attackPower = 6, moveSpeed = 2.5f, detectRange = 10f, patrolRadius = 8f,
                    dropItemId = "item_spirit_stone", dropItemName = "灵石碎片", dropQuantity = 2,
                    color = new Color(0.5f, 0.35f, 0.25f) },
                new() { position = new Vector3(-8, 1.5f, 10), enemyId = "bear_001", enemyName = "狂暴熊",
                    maxHP = 100, attackPower = 15, moveSpeed = 2f, detectRange = 6f, patrolRadius = 4f,
                    dropItemId = "item_pill_001", dropItemName = "聚气丹", dropQuantity = 3,
                    color = new Color(0.5f, 0.25f, 0.1f) },
            };
        }

        void OnDayPassed(Dictionary<string, object> data)
        {
            int day = data.ContainsKey("day") ? (int)data["day"] : 0;
            Debug.Log($"[Respawner] 新的一天！昨天击杀{_killedToday}个敌人。敌人已刷新。");
            _killedToday = 0;
            RespawnAll();
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            _killedToday++;
        }

        public void RespawnAll()
        {
            // 清除已有敌人(排除Player/NPC等)
            var allEnemies = Object.FindObjectsOfType<EarthOnline.Combat.EnemyAI>();
            foreach (var e in allEnemies)
            {
                if (e != null && e.gameObject != null && !e.IsDead)
                    Destroy(e.gameObject);
            }

            // 重新生成
            foreach (var sp in _spawnPoints)
            {
                SpawnEnemy(sp);
            }
        }

        void SpawnEnemy(EnemySpawnPoint sp)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Enemy_{sp.enemyId}";
            go.transform.position = sp.position;
            go.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            Object.DestroyImmediate(go.GetComponent<Rigidbody>());

            // Scale with player level
            int playerLv = PlayerStats.Instance?.playerLevel ?? 1;
            float scaleMult = 1f + (playerLv - 1) * 0.10f; // +10% per level (was 15%, too aggressive)
            int scaledHP = Mathf.RoundToInt(sp.maxHP * scaleMult);
            int scaledAtk = Mathf.RoundToInt(sp.attackPower * scaleMult);

            var enemyType = System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");
            if (enemyType != null)
            {
                var comp = go.AddComponent(enemyType);
                enemyType.GetField("enemyId")?.SetValue(comp, sp.enemyId);
                enemyType.GetField("enemyName")?.SetValue(comp, sp.enemyName);
                enemyType.GetField("maxHP")?.SetValue(comp, scaledHP);
                enemyType.GetField("attackPower")?.SetValue(comp, scaledAtk);
                enemyType.GetField("moveSpeed")?.SetValue(comp, sp.moveSpeed);
                enemyType.GetField("detectRange")?.SetValue(comp, sp.detectRange);
                enemyType.GetField("patrolRadius")?.SetValue(comp, sp.patrolRadius);
                enemyType.GetField("dropItemId")?.SetValue(comp, sp.dropItemId);
                enemyType.GetField("dropItemName")?.SetValue(comp, sp.dropItemName);
                enemyType.GetField("dropQuantity")?.SetValue(comp, sp.dropQuantity + playerLv / 3);
            }

            var r = go.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = sp.color; r.material = m; }

            Debug.Log($"[Respawner] {sp.enemyName} 出现在 ({sp.position.x:F0},{sp.position.z:F0})");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
            EventBus.Unsubscribe("OnEnemyKilled", OnEnemyKilled);
        }
    }
}
