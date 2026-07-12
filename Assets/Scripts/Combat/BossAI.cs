using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// BOSS AI 核心 —— 行为树 + 阶段管理 + 出场演出
    /// 
    /// 状态机流转:
    ///   Idle/Patrol → Detecting → Entrance → Combat → PhaseTransition(循环) → Victory/Defeat
    ///                                                          ↑                    |
    ///                                                     EnrageTimeout ────────→ Enraged
    /// </summary>
    public class BossAI : MonoBehaviour
    {
        [Header("-- BOSS 定义 --")]
        public BossDef bossDef;

        [Header("-- 运行时状态 --")]
        [SerializeField, ReadOnly] private string _currentState = "Idle";
        [SerializeField, ReadOnly] private float _currentHP;
        [SerializeField, ReadOnly] private int _currentPhaseIndex = 0;
        [SerializeField, ReadOnly] private int _partySize = 1;
        [SerializeField, ReadOnly] private bool _isEnraged = false;
        [SerializeField, ReadOnly] private bool _isInBreathingWindow = false;
        [SerializeField, ReadOnly] private bool _isAlive = true;

        // 组件引用
        private Collider _collider;
        private Renderer _renderer;
        private Animator _animator;
        private Transform _playerTransform;

        // 内部计时器
        private float _battleTimer = 0f;
        private float _breathingWindowRemaining = 0f;
        private float _attackCooldownRemaining = 0f;
        private int _currentAttackIndex = 0;

        // 玩家检测缓存
        private Collider[] _detectionOverlapBuffer = new Collider[10];

        // ─── 常量 ──────────────────────────────────────────────────────────

        private const float BREATHING_WINDOW_DURATION = 3f;
        private const float DETECTION_INTERVAL = 0.5f;
        private const float PLAYER_LAYER_BUFFER_SIZE = 5f;

        // ─── Unity 生命周期 ────────────────────────────────────────────────

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _renderer = GetComponentInChildren<Renderer>();
            _animator = GetComponent<Animator>();

            if (bossDef == null)
            {
                Debug.LogError("[BossAI] BossDef is not assigned! Disabling.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossBreathingWindowEvent>(OnBreathingWindowEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossBreathingWindowEvent>(OnBreathingWindowEvent);
        }

        private void Start()
        {
            InitializeBoss();
            _currentState = "Idle";
        }

        private void Update()
        {
            if (!_isAlive || bossDef == null) return;

            switch (_currentState)
            {
                case "Idle":
                    UpdateIdle();
                    break;
                case "Detecting":
                    UpdateDetecting();
                    break;
                case "Entrance":
                    UpdateEntrance();
                    break;
                case "Combat":
                    UpdateCombat();
                    break;
                case "PhaseTransition":
                    UpdatePhaseTransition();
                    break;
                case "BreathingWindow":
                    UpdateBreathingWindow();
                    break;
                case "Victory":
                case "Defeated":
                    break;
            }
        }

        // ─── 初始化 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 初始化BOSS属性（按境界+组队人数缩放）
        /// </summary>
        public void InitializeBoss()
        {
            if (bossDef == null) return;

            bossDef.CalculateScaledStats(_partySize);
            _currentHP = bossDef.ScaledMaxHP;
            _currentPhaseIndex = 0;
            _isEnraged = false;
            _isAlive = true;
            _battleTimer = 0f;
        }

        /// <summary>
        /// 设置组队人数（战斗开始前调用）
        /// </summary>
        public void SetPartySize(int size)
        {
            _partySize = Mathf.Clamp(size, 1, 10);
            if (bossDef != null)
                bossDef.CalculateScaledStats(_partySize);
        }

        /// <summary>
        /// 外部设置玩家Transform引用
        /// </summary>
        public void SetPlayerTarget(Transform player)
        {
            _playerTransform = player;
        }

        // ─── 状态更新 ──────────────────────────────────────────────────────

        private void UpdateIdle()
        {
            // 每帧检测玩家是否进入detectRange
            if (DetectPlayerInRange(bossDef.detectRange))
            {
                _currentState = "Detecting";
                Debug.Log($"[BossAI] {bossDef.displayName} detected player. Entering Detecting state.");
            }
        }

        private void UpdateDetecting()
        {
            // 进入警戒范围 -> 触发BOSS出场演出
            if (DetectPlayerInRange(bossDef.aggroRange))
            {
                StartCoroutine(PlayEntranceSequence());
            }
        }

        private void UpdateEntrance()
        {
            // 出场演出由协程处理，这里等待协程完成
            // 协程完成后会设置状态为 "Combat"
        }

        private void UpdateCombat()
        {
            _battleTimer += Time.deltaTime;

            // --- 检查狂暴（时间触发） ---
            if (!_isEnraged && _battleTimer >= bossDef.enrageTimeLimit)
            {
                TriggerEnrage();
                return;
            }

            // --- 检查HP阈值阶段转换 ---
            float hpPercent = _currentHP / bossDef.ScaledMaxHP;
            int expectedPhase = bossDef.GetPhaseIndexForHP(hpPercent);
            if (expectedPhase > _currentPhaseIndex && !_isInBreathingWindow)
            {
                StartPhaseTransition(expectedPhase);
                return;
            }

            // --- 攻击循环 ---
            if (_attackCooldownRemaining > 0f)
            {
                _attackCooldownRemaining -= Time.deltaTime;
            }
            else
            {
                ExecuteAttack();
            }

            // 发狂暴警告事件
            float timeUntilEnrage = bossDef.enrageTimeLimit - _battleTimer;
            if (timeUntilEnrage <= 30f && timeUntilEnrage > 0f)
            {
                EventBus.Publish(new BossEnrageEvent
                {
                    TimeUntilEnrage = timeUntilEnrage.ToString("F1"),
                    IsEnraged = "false"
                });
            }
        }

        private void UpdatePhaseTransition()
        {
            // 阶段转换由协程处理
        }

        private void UpdateBreathingWindow()
        {
            if (_breathingWindowRemaining > 0f)
            {
                _breathingWindowRemaining -= Time.deltaTime;
            }
            else
            {
                _isInBreathingWindow = false;
                _currentState = "Combat";
                EventBus.Publish(new BossBreathingWindowEvent
                {
                    IsActive = "false",
                    Duration = "0"
                });
                Debug.Log($"[BossAI] Breathing window ended. Returning to Combat.");
            }
        }

private void OnBreathingWindowEvent(BossBreathingWindowEvent evt)
{
    _isInBreathingWindow = evt.IsActive == "true";
    if (_isInBreathingWindow)
    {
        _currentState = "BreathingWindow";
        _breathingWindowRemaining = evt.Duration is float f ? f : float.Parse(evt.Duration?.ToString() ?? "0");
    }
}


        // ─── 出场演出 ──────────────────────────────────────────────────────

        private IEnumerator PlayEntranceSequence()
        {
            _currentState = "Entrance";
            Debug.Log($"[BossAI] === BOSS ENTRANCE: {bossDef.displayName} ===");

            // 1. 发布出场事件（UI响应：显示名号+称号+境界+触发特效）
            EventBus.Publish(new BossEntranceEvent
            {
                BossName = bossDef.displayName,
                Title = bossDef.title,
                Realm = bossDef.realm,
                Dialogue = bossDef.entranceDialogue
            });

            // 2. 播放出场动画 / 震动（3-5秒出场时间）
            if (_animator != null)
                _animator.SetTrigger("Entrance");

            // 屏幕震动效果
            StartCoroutine(ScreenShake(0.5f, 0.3f));

            // 1秒后显示第一句台词
            yield return new WaitForSeconds(1f);
            EventBus.Publish(new BossDialogueEvent
            {
                Speaker = bossDef.displayName,
                Line = bossDef.entranceDialogue,
                DisplayDuration = 3f
            });

            // 再等2秒后进入战斗
            yield return new WaitForSeconds(2f);

            _currentState = "Combat";
            Debug.Log($"[BossAI] Entrance complete. Entering Combat state.");
        }

        private IEnumerator ScreenShake(float duration, float magnitude)
        {
            // 简单的屏幕震动效果（可被Camera系统扩展）
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                // 这里在实际项目中应通过EventBus通知Camera系统
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // ─── 阶段转换 ──────────────────────────────────────────────────────

        private void StartPhaseTransition(int newPhaseIndex)
        {
            _currentState = "PhaseTransition";
            _currentPhaseIndex = newPhaseIndex;

            PhaseTransitionDef phaseDef = bossDef.phases[newPhaseIndex - 1];

            Debug.Log($"[BossAI] Phase Transition: {newPhaseIndex} | {phaseDef.dialogue}");

            // 1. 发布阶段转换事件（UI更新+视觉变化）
            EventBus.Publish(new BossPhaseChangedEvent
            {
                NewPhaseIndex = newPhaseIndex,
                Dialogue = phaseDef.dialogue,
                CurrentHPPercent = _currentHP / bossDef.ScaledMaxHP,
                BreathingWindowDuration = BREATHING_WINDOW_DURATION
            });

            // 2. 播放台词
            EventBus.Publish(new BossDialogueEvent
            {
                Speaker = bossDef.displayName,
                Line = phaseDef.dialogue,
                DisplayDuration = 2.5f
            });

            // 3. 视觉变化（颜色特效）
            if (_renderer != null && phaseDef.visualColor != default)
            {
                StartCoroutine(FlashColor(_renderer.material, phaseDef.visualColor, 1.5f));
            }

            // 4. 触发喘息窗口（BOSS不可攻击，玩家恢复）
            StartCoroutine(BreathingWindow(BREATHING_WINDOW_DURATION));
        }

        private IEnumerator BreathingWindow(float duration)
        {
            _isInBreathingWindow = true;
            _breathingWindowRemaining = duration;

            EventBus.Publish(new BossBreathingWindowEvent
            {
                IsActive = "true",
                Duration = duration
            });

            Debug.Log($"[BossAI] Breathing window started: {duration}s");

            // 锁定位置（BOSS不移动）
            Vector3 lockedPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                transform.position = lockedPosition;
                elapsed += Time.deltaTime;
                _breathingWindowRemaining = duration - elapsed;
                yield return null;
            }

            _isInBreathingWindow = false;
            _breathingWindowRemaining = 0f;

            EventBus.Publish(new BossBreathingWindowEvent
            {
                IsActive = "false",
                Duration = 0f
            });

            Debug.Log($"[BossAI] Breathing window ended.");

            // 检查是否在喘息窗口结束时又有新的阶段转换
            float hpPercent = _currentHP / bossDef.ScaledMaxHP;
            int expectedPhase = bossDef.GetPhaseIndexForHP(hpPercent);
            if (expectedPhase > _currentPhaseIndex)
            {
                StartPhaseTransition(expectedPhase);
            }
            else
            {
                _currentState = "Combat";
            }
        }

        // ─── 狂暴系统 ──────────────────────────────────────────────────────

        private void TriggerEnrage()
        {
            _isEnraged = true;
            _currentState = "Combat";

            Debug.Log($"[BossAI] === ENRAGED! {bossDef.displayName} has entered berserk mode! ===");

            EventBus.Publish(new BossEnrageEvent
            {
                TimeUntilEnrage = "0f",
                IsEnraged = true
            });

            EventBus.Publish(new BossDialogueEvent
            {
                Speaker = bossDef.displayName,
                Line = "时间到了！和这个世界说再见吧！！",
                DisplayDuration = 3f
            });

            // 狂暴后视觉变化（红色闪烁）
            if (_renderer != null)
            {
                StartCoroutine(FlashColor(_renderer.material, Color.red, 2f));
            }
        }

        // ─── 攻击 ─────────────────────────────────────────────────────────

        private void ExecuteAttack()
        {
            if (bossDef.attacks == null || bossDef.attacks.Length == 0)
            {
                _attackCooldownRemaining = 1f;
                return;
            }

            AttackDef attack = SelectAttackForCurrentPhase();
            float damage = bossDef.ScaledAttack * attack.damageMultiplier;

            Debug.Log($"[BossAI] {bossDef.displayName} uses {attack.attackName} (DMG: {damage})");

            if (_animator != null)
                _animator.SetTrigger(attack.animationTrigger);

            // 通知伤害系统
            // 实际伤害计算由 CombatSystem 处理，这里仅触发
            OnAttackPerformed(attack);

            _attackCooldownRemaining = attack.cooldown;
        }

        private AttackDef SelectAttackForCurrentPhase()
        {
            // 收集当前阶段可用的所有攻击
            List<AttackDef> availableAttacks = new List<AttackDef>();
            availableAttacks.AddRange(bossDef.attacks);

            // 从低到高遍历阶段，累积新增攻击
            for (int i = 0; i < _currentPhaseIndex && i < bossDef.phases.Length; i++)
            {
                foreach (string attackName in bossDef.phases[i].newAttacks)
                {
                    // 查找并添加到可用池
                    foreach (AttackDef def in bossDef.attacks)
                    {
                        if (def.attackName == attackName && !availableAttacks.Contains(def))
                            availableAttacks.Add(def);
                    }
                }
            }

            // 循环使用攻击
            _currentAttackIndex = (_currentAttackIndex + 1) % availableAttacks.Count;
            return availableAttacks[_currentAttackIndex];
        }

        private void OnAttackPerformed(AttackDef attack)
        {
            // 发布攻击事件（CombatSystem 或玩家监听此事件处理伤害）
            // 实际项目中通过 CombatSystem 处理
        }

        // ─── 受伤 ─────────────────────────────────────────────────────────

        /// <summary>
        /// BOSS 受到伤害。由 CombatSystem 调用。
        /// </summary>
        /// <param name="rawDamage">原始伤害值</param>
        /// <param name="playerRealm">攻击者境界（用于计算境界压制）</param>
        /// <param name="weaknessMultiplier">弱点加成倍率（默认1.0）</param>
        /// <returns>实际造成的伤害</returns>
        public float TakeDamage(float rawDamage, int playerRealm, float weaknessMultiplier = 1f)
        {
            if (!_isAlive || _isInBreathingWindow) return 0f;

            // 1. 境界压制计算
            float realmMult = BossDef.CalculateRealmSuppression(playerRealm, bossDef.realm);

            // 2. 防御减免（简化版）
            float defenseReduction = bossDef.ScaledDefense / (bossDef.ScaledDefense + 100f);
            float damageAfterDefense = rawDamage * (1f - defenseReduction);

            // 3. 最终伤害
            float finalDamage = damageAfterDefense * realmMult * weaknessMultiplier;
            finalDamage = Mathf.Max(finalDamage, 1f); // 最少造成1点伤害

            _currentHP -= finalDamage;
            _currentHP = Mathf.Max(_currentHP, 0f);

            Debug.Log($"[BossAI] {bossDef.displayName} took {finalDamage:F1} damage. HP: {_currentHP:F1}/{bossDef.ScaledMaxHP:F1}");

            // 检查是否死亡
            if (_currentHP <= 0f)
            {
                Die();
            }

            return finalDamage;
        }

        /// <summary>
        /// 简化的受伤接口（默认玩家境界与BOSS相同，无弱点）
        /// </summary>
        public float TakeDamage(float rawDamage)
        {
            return TakeDamage(rawDamage, bossDef.realm, 1f);
        }

        // ─── 死亡 ─────────────────────────────────────────────────────────

        private void Die()
        {
            _isAlive = false;
            _currentState = "Defeated";

            Debug.Log($"[BossAI] === {bossDef.displayName} DEFEATED! ===");

            EventBus.Publish(new BossDefeatedEvent
            {
                BossId = bossDef.bossId,
                BossName = bossDef.displayName,
                FinalPhase = _currentPhaseIndex
            });

            EventBus.Publish(new BossDialogueEvent
            {
                Speaker = bossDef.displayName,
                Line = bossDef.defeatDialogue,
                DisplayDuration = 4f
            });

            // 播放死亡动画
            if (_animator != null)
                _animator.SetTrigger("Defeated");

            // 0.5秒后销毁或停用
            StartCoroutine(DefeatedCleanup(2f));
        }

        private IEnumerator DefeatedCleanup(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
            // 实际项目中：触发掉落系统、解锁区域等
        }

        // ─── 工具 ─────────────────────────────────────────────────────────

        private bool DetectPlayerInRange(float range)
        {
            // 基于玩家Transform引用检测
            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                return dist <= range;
            }

            // 无玩家引用时使用OverlapSphere
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                range,
                _detectionOverlapBuffer,
                LayerMask.GetMask("Player")
            );
            return count > 0;
        }

        private IEnumerator FlashColor(Material material, Color targetColor, float duration)
        {
            Color originalColor = material.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                material.color = Color.Lerp(originalColor, targetColor, Mathf.PingPong(elapsed * 3f, 1f));
                elapsed += Time.deltaTime;
                yield return null;
            }

            material.color = originalColor;
        }

        // ─── 公共查询接口 ────────────────────────────────────────────────

        public string CurrentState => _currentState;
        public float CurrentHP => _currentHP;
        public float MaxHP => bossDef != null ? bossDef.ScaledMaxHP : 0f;
        public float HPPercent => MaxHP > 0f ? _currentHP / MaxHP : 0f;
        public int CurrentPhase => _currentPhaseIndex;
        public bool IsEnraged => _isEnraged;
        public bool IsInBreathingWindow => _isInBreathingWindow;
        public bool IsAlive => _isAlive;
        public float BattleTimer => _battleTimer;
        public float TimeUntilEnrage => bossDef != null ? Mathf.Max(0f, bossDef.enrageTimeLimit - _battleTimer) : 0f;
    }

    /// <summary>
    /// Editor helper: show [ReadOnly] fields in Inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
