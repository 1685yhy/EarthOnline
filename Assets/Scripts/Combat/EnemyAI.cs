using UnityEngine;
using UnityEngine.UI;
using EarthOnline.Framework;
using EarthOnline.NPC;
using System.Collections.Generic;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 敌人AI —— 空闲→巡逻→发现玩家→追击→攻击→死亡→掉落。
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }

        [Header("基础")]
        public string enemyId = "enemy_001";
        public string enemyName = "野狼";
        public int maxHP = 50;
        public int currentHP;
        public int attackPower = 8;
        public float moveSpeed = 2f;
        public float chaseSpeed = 4f;
        public float attackRange = 1.8f;
        public float detectRange = 8f;
        public float attackCooldown = 1.5f;
        public bool IsDead => currentHP <= 0;

        [Header("巡逻")]
        public float patrolRadius = 6f;
        public float waitTime = 3f;

        [Header("掉落")]
        public string dropItemId = "item_spirit_stone";
        public string dropItemName = "灵石碎片";
        public int dropQuantity = 2;
        public float dropChance = 0.7f;

        [Header("血条")]
        public GameObject healthBarCanvas;
        public Image healthBarFill;

        private State _state = State.Idle;
        private Transform _player;
        private CharacterController _cc;
        private Vector3 _homePosition;
        private Vector3 _patrolTarget;
        private float _waitTimer;
        private float _lastAttackTime;
        private float _stateTimer;

        void Start()
        {
            currentHP = maxHP;
            _homePosition = transform.position;
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _cc = GetComponent<CharacterController>();
            if (_cc == null) { _cc = gameObject.AddComponent<CharacterController>(); }
            if (_cc != null) { _cc.center = new Vector3(0, 0.8f, 0); _cc.height = 1.6f; _cc.radius = 0.4f; }

            CreateHealthBar();
            PickNewPatrolTarget();
            _state = State.Patrol;
        }

        void CreateHealthBar()
        {
            healthBarCanvas = new GameObject("HealthBar");
            healthBarCanvas.transform.SetParent(transform);
            healthBarCanvas.transform.localPosition = new Vector3(0, 1.8f, 0);
            var canvas = healthBarCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            healthBarCanvas.AddComponent<CanvasScaler>();
            var barBg = new GameObject("BG"); barBg.transform.SetParent(healthBarCanvas.transform);
            var bgImg = barBg.AddComponent<Image>(); bgImg.color = new Color(0, 0, 0, 0.5f);
            var bgRect = barBg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(100, 10); bgRect.localPosition = Vector3.zero;
            var barFill = new GameObject("Fill"); barFill.transform.SetParent(barBg.transform);
            healthBarFill = barFill.AddComponent<Image>(); healthBarFill.color = Color.red;
            var fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero; fillRect.pivot = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            healthBarCanvas.AddComponent<Billboard>();
        }

        void Update()
        {
            if (IsDead) return;

            float distToPlayer = _player != null
                ? Vector3.Distance(transform.position, _player.position)
                : float.MaxValue;

            switch (_state)
            {
                case State.Idle:
                    _waitTimer -= Time.deltaTime;
                    if (_waitTimer <= 0) { _state = State.Patrol; PickNewPatrolTarget(); }
                    break;

                case State.Patrol:
                    if (distToPlayer <= detectRange && _player != null)
                    {
                        _state = State.Chase;
                        Debug.Log($"[{enemyName}] 发现玩家！距离{distToPlayer:F1}m");
                        break;
                    }
                    MoveToward(_patrolTarget, moveSpeed);
                    if (Vector3.Distance(
                        new Vector3(transform.position.x, 0, transform.position.z),
                        new Vector3(_patrolTarget.x, 0, _patrolTarget.z)) <= 0.5f)
                    {
                        _state = State.Idle; _waitTimer = waitTime;
                    }
                    break;

                case State.Chase:
                    if (distToPlayer > detectRange * 1.5f || _player == null)
                    {
                        _state = State.Patrol; PickNewPatrolTarget();
                        break;
                    }
                    if (distToPlayer <= attackRange)
                    {
                        _state = State.Attack;
                        break;
                    }
                    MoveToward(_player.position, chaseSpeed);
                    break;

                case State.Attack:
                    if (distToPlayer > attackRange * 1.3f)
                    {
                        _state = State.Chase;
                        break;
                    }
                    if (Time.time - _lastAttackTime >= attackCooldown)
                    {
                        _lastAttackTime = Time.time;
                        var stats = PlayerStats.Instance;
                        if (stats != null)
                        {
                            stats.TakeDamage(attackPower);
                            Debug.Log($"[{enemyName}] 攻击玩家！-{attackPower}HP");
                        FloatingDamage.Spawn(stats.transform.position,
                            $"-{attackPower}", new Color(1f, 0.3f, 0.2f));
                        }
                    }
                    FaceTarget(_player.position);
                    break;
            }

            UpdateHealthBar();
        }

        void MoveToward(Vector3 target, float speed)
        {
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0;
            FaceTarget(target);
            if (_cc != null) _cc.SimpleMove(dir * speed);
        }

        void FaceTarget(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0;
            if (dir.magnitude > 0.1f)
                transform.forward = Vector3.Lerp(transform.forward, dir.normalized, 5f * Time.deltaTime);
        }

        void PickNewPatrolTarget()
        {
            Vector2 rand = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = new Vector3(_homePosition.x + rand.x, _homePosition.y, _homePosition.z + rand.y);
        }

        public void TakeDamage(int damage, bool crit = false)
        {
            currentHP -= damage;
            if (currentHP <= 0) { currentHP = 0; Die(); }

            // 受伤后立刻追击
            if (_state != State.Attack && _state != State.Chase)
            {
                _state = State.Chase;
                Debug.Log($"[{enemyName}] 受到攻击！转而追击玩家...");
            }
        }

        void Die()
        {
            _state = State.Dead;
            Debug.Log($"[{enemyName}] 被击败！");
            EventBus.Publish("OnEnemyKilled", new Dictionary<string, object> {
                {"enemyId", enemyId}, {"enemyName", enemyName}
            });

            if (Random.value < dropChance)
            {
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    inv.AddItem(new Item
                    {
                        id = dropItemId, name = dropItemName,
                        type = "Material", rarity = "R",
                        quantity = dropQuantity, value = 15 * dropQuantity
                    });
                    Debug.Log($"[{enemyName}] 掉落: {dropItemName} x{dropQuantity}");
                }
            }

            Destroy(healthBarCanvas);
            Destroy(gameObject, 2f);
        }

        void UpdateHealthBar()
        {
            if (healthBarFill != null)
                healthBarFill.fillAmount = (float)currentHP / maxHP;
            if (healthBarCanvas != null)
                healthBarCanvas.SetActive(currentHP < maxHP && currentHP > 0);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1, 1, 0, 0.2f); Gizmos.DrawWireSphere(transform.position, detectRange);
        }
    }

    // Billboard is already defined in NPCBase.cs, skip redefining
}
