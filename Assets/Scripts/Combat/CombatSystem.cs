using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 战斗系统 —— 玩家左键攻击，伤害计算，击杀事件。
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        public float attackRange = 2.5f;
        public float attackCooldown = 0.6f;
        public int baseAttackPower = 15;
        public LayerMask enemyLayer = -1;

        private float _lastAttackTime;
        private Camera _cam;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _cam = Camera.main;
            if (enemyLayer == -1) enemyLayer = LayerMask.GetMask("Default");
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0) && Time.time - _lastAttackTime >= attackCooldown)
            {
                Attack();
            }
        }

        void Attack()
        {
            _lastAttackTime = Time.time;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            // 前方范围内的敌人检测
            var hits = Physics.OverlapSphere(player.transform.position, attackRange, enemyLayer);
            EnemyAI closestEnemy = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.IsDead)
                {
                    float dist = Vector3.Distance(player.transform.position, hit.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = enemy;
                    }
                }
            }

            if (closestEnemy != null)
            {
                // 计算伤害（含暴击）
                bool crit = Random.value < 0.15f;
                int damage = crit ? Mathf.FloorToInt(baseAttackPower * 1.8f) : baseAttackPower;
                closestEnemy.TakeDamage(damage, crit);

                Debug.Log($"[Combat] 攻击{(crit ? "暴击！" : "")} {damage}伤害 → {closestEnemy.enemyName}");
                FloatingDamage.Spawn(closestEnemy.transform.position,
                    crit ? $"-{damage} 暴击!" : $"-{damage}",
                    crit ? new Color(1f, 0.8f, 0f) : Color.white);
                EventBus.Publish("OnPlayerAttack", new Dictionary<string, object> {
                    {"target", closestEnemy.enemyName}, {"damage", damage}, {"crit", crit}
                });
            }
            else
            {
                Debug.Log("[Combat] 挥空了...附近没有敌人。");
            }
        }

        void OnDrawGizmosSelected()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawWireSphere(player.transform.position, attackRange);
            }
        }
    }
}
