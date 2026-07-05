using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 地下城入口 —— 走近按E进入地下城场景(传送至洞穴区域)。
    /// </summary>
    public class DungeonEntrance : MonoBehaviour
    {
        public float enterRange = 3f;
        public string dungeonName = "虚空裂缝";
        public string warningMessage = "⚠️ 前方高能反应！建议Lv.5+，装备武器后再进入。";

        private Transform _player;
        private bool _inRange;
        private bool _warned;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void Update()
        {
            if (_player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            _inRange = dist <= enterRange;

            if (dist <= enterRange * 2f && !_warned)
            {
                _warned = true;
                Debug.Log($"[Dungeon] {warningMessage}");
            }

            if (_inRange && Input.GetKeyDown(KeyCode.E))
            {
                EnterDungeon();
            }
        }

        void EnterDungeon()
        {
            Debug.Log($"══════════════════════════════");
            Debug.Log($"  进入: {dungeonName}");
            Debug.Log($"══════════════════════════════");

            // Teleport player to dungeon interior
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = new Vector3(0, 2f, -25);
                if (cc != null) cc.enabled = true;
            }

            // Spawn dungeon enemies
            SpawnDungeonEnemies();

            EventBus.Publish("OnDungeonEntered", new Dictionary<string, object> {
                {"dungeon", dungeonName}
            });

            Debug.Log($"[Dungeon] 你踏入了{dungeonName}...浓厚的灵气中夹杂着危险的气息。");
        }

        void SpawnDungeonEnemies()
        {
            // Spawn 2 tougher enemies in the dungeon
            for (int i = 0; i < 2; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"DungeonEnemy_{i}";
                go.transform.position = new Vector3(Random.Range(-3, 3), 1.5f, -25 + Random.Range(-5, 0));
                go.transform.localScale = new Vector3(1f, 1.3f, 1f);
                Object.DestroyImmediate(go.GetComponent<Rigidbody>());

                var t = System.Type.GetType("EarthOnline.Combat.EnemyAI, Assembly-CSharp");
                if (t != null)
                {
                    var c = go.AddComponent(t);
                    t.GetField("enemyId")?.SetValue(c, $"dungeon_{i}");
                    t.GetField("enemyName")?.SetValue(c, "虚空残影");
                    t.GetField("maxHP")?.SetValue(c, 80);
                    t.GetField("attackPower")?.SetValue(c, 12);
                    t.GetField("moveSpeed")?.SetValue(c, 3f);
                    t.GetField("detectRange")?.SetValue(c, 8f);
                    t.GetField("dropItemId")?.SetValue(c, "item_spirit_stone");
                    t.GetField("dropItemName")?.SetValue(c, "灵石碎片");
                    t.GetField("dropQuantity")?.SetValue(c, 3);
                }
                var r = go.GetComponent<Renderer>();
                if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.3f, 0.1f, 0.5f); r.material = m; }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, enterRange);
        }
    }
}
