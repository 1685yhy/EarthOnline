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
        private int _targetIndex = -1;
        private EnemyAI _currentTarget;

        public EnemyAI CurrentTarget => _currentTarget;

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
                // 计算伤害（含装备加成+暴击）
                int eqBonus = EquipmentManager.Instance?.AttackBonus ?? 0;
                bool crit = Random.value < 0.15f;
                int totalAtk = baseAttackPower + eqBonus;
                int damage = crit ? Mathf.FloorToInt(totalAtk * 1.8f) : totalAtk;
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

        public void CycleTarget()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var hits = Physics.OverlapSphere(player.transform.position, attackRange * 3f, enemyLayer);
            var enemies = new System.Collections.Generic.List<EnemyAI>();
            foreach (var h in hits)
            {
                var e = h.GetComponent<EnemyAI>();
                if (e != null && !e.IsDead) enemies.Add(e);
            }
            enemies.Sort((a, b) =>
                Vector3.Distance(player.transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(player.transform.position, b.transform.position)));

            if (enemies.Count == 0) { Debug.Log("[Combat] 附近没有敌人。"); return; }

            _targetIndex = (_targetIndex + 1) % enemies.Count;
            _currentTarget = enemies[_targetIndex];
            Debug.Log($"[Combat] 🎯 目标切换: {_currentTarget.enemyName} ({_currentTarget.currentHP}/{_currentTarget.maxHP}HP)");
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
