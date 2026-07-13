using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// BossCombatTest — Play mode BOSS combat verification harness.
    ///
    /// Verifies the full BOSS combat lifecycle end-to-end:
    ///   Idle -> Detecting -> Entrance -> Combat -> PhaseTransition -> Defeated
    ///
    /// Features:
    ///   1. Finds BossAI on the target GameObject and sets player as target
    ///   2. Polls state machine transitions and logs every change
    ///   3. Subscribes to key Boss events via EventBus.Subscribe<T>
    ///   4. Outputs periodic combat reports: "[BossTest] Boss {name} phase {N}, HP {current}/{max}"
    ///   5. ForceEngage() public method: teleports player next to boss and triggers detection
    ///
    /// Usage:
    ///   - Attach to any GameObject in the scene, OR
    ///   - Auto-created via [RuntimeInitializeOnLoadMethod] when none exists
    ///   - Right-click the component in Inspector -> "Force Engage - Teleport Player to Boss"
    /// </summary>
    public class BossCombatTest : MonoBehaviour
    {
        [Header("-- Test Configuration --")]
        [SerializeField, Tooltip("Target boss GameObject name in the scene")]
        private string bossGameObjectName = "Enemy_Leviathan";

        [SerializeField, Tooltip("Interval between combat report logs (seconds)")]
        private float combatReportInterval = 3f;

        [SerializeField, Tooltip("Auto-create on scene load if no instance exists")]
        private bool autoCreateEnabled = true;

        [Header("-- Runtime State (read-only) --")]
        [SerializeField, ReadOnly] private string _lastKnownState = "None";
        [SerializeField, ReadOnly] private string _bossName = "Unknown";
        [SerializeField, ReadOnly] private float _bossCurrentHP = 0f;
        [SerializeField, ReadOnly] private float _bossMaxHP = 1f;
        [SerializeField, ReadOnly] private float _bossHPPercent = 0f;
        [SerializeField, ReadOnly] private int _bossPhase = 0;
        [SerializeField, ReadOnly] private bool _bossAlive = false;
        [SerializeField, ReadOnly] private bool _isEnraged = false;
        [SerializeField, ReadOnly] private bool _isInBreathingWindow = false;
        [SerializeField, ReadOnly] private float _timeUntilEnrage = 0f;
        [SerializeField, ReadOnly] private int _totalStateTransitions = 0;

        private BossAI _bossAI;
        private float _reportTimer;
        private float _pollTimer;
        private bool _initialized;

        private const float POLL_INTERVAL = 0.25f;
        private const float INIT_RETRY_INTERVAL = 1f;
        private const string LOG_PREFIX = "[BossTest]";

        // ─── Auto-Creation ────────────────────────────────────────────────

        /// <summary>
        /// Auto-create BossCombatTest if none exists in the scene.
        /// Fires before any scene objects' Start() so player can find us.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            // Don't auto-create if the user disabled it on a prefab instance
            // We check in Start() — the singleton approach is simpler:
            // delay resolution until after scene load so FindObjectOfType works.
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateAfterLoad()
        {
            if (FindObjectOfType<BossCombatTest>() != null)
                return;

            // 查找或创建 GameManager，将本组件附加到它上面
            GameObject gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                gameManager = new GameObject("GameManager");
                DontDestroyOnLoad(gameManager);
                Debug.Log($"{LOG_PREFIX} Created GameManager GameObject (was missing from scene).");
            }

            var test = gameManager.AddComponent<BossCombatTest>();
            test.autoCreateEnabled = true;
            Debug.Log($"{LOG_PREFIX} Auto-created BossCombatTest harness on GameManager.");
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (!autoCreateEnabled)
                Debug.Log($"{LOG_PREFIX} BossCombatTest attached manually to '{gameObject.name}'.");
        }

        private void Start()
        {
            Debug.Log("==============================================");
            Debug.Log($"{LOG_PREFIX} BOSS Combat Test Harness Starting");
            Debug.Log($"  Target GameObject: {bossGameObjectName}");
            Debug.Log($"  Report Interval: {combatReportInterval}s");
            Debug.Log("==============================================");

            // Register event subscriptions immediately so we don't miss early events
            SubscribeEvents();

            // Attempt to find the boss — may not exist yet if scene not fully loaded
            if (!LocateBoss())
            {
                Debug.Log($"{LOG_PREFIX} '{bossGameObjectName}' not found yet. Retrying every {INIT_RETRY_INTERVAL}s...");
                InvokeRepeating(nameof(RetryLocateBoss), INIT_RETRY_INTERVAL, INIT_RETRY_INTERVAL);
            }
        }

        private void Update()
        {
            if (!_initialized || _bossAI == null)
                return;

            // ── Poll state transitions ────────────────────────────────────
            _pollTimer -= Time.deltaTime;
            if (_pollTimer <= 0f)
            {
                _pollTimer = POLL_INTERVAL;
                PollBossState();
            }

            // ── Periodic combat report ────────────────────────────────────
            if (!_bossAlive && _lastKnownState == "Defeated")
                return; // Boss is dead — stop combat reports

            _reportTimer -= Time.deltaTime;
            if (_reportTimer <= 0f)
            {
                _reportTimer = combatReportInterval;
                LogCombatReport();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ─── Boss Discovery ───────────────────────────────────────────────

        /// <summary>
        /// Try to find the boss GameObject and initialize.
        /// </summary>
        private bool LocateBoss()
        {
            GameObject bossGO = GameObject.Find(bossGameObjectName);
            if (bossGO == null)
                return false;

            _bossAI = bossGO.GetComponent<BossAI>();
            if (_bossAI == null)
            {
                Debug.LogError($"{LOG_PREFIX} BossAI component not found on '{bossGameObjectName}'!");
                return false;
            }

            Initialize(bossGO);
            return true;
        }

        private void RetryLocateBoss()
        {
            if (LocateBoss())
            {
                CancelInvoke(nameof(RetryLocateBoss));
            }
        }

        private void Initialize(GameObject bossGO)
        {
            // ── Set player as target ──────────────────────────────────────
            SetPlayerTarget();

            // ── Cache boss info ───────────────────────────────────────────
            _bossName = _bossAI.bossDef != null ? _bossAI.bossDef.displayName : bossGO.name;
            _lastKnownState = _bossAI.CurrentState;
            _reportTimer = combatReportInterval;
            _pollTimer = POLL_INTERVAL;

            LogDivider();
            Debug.Log($"{LOG_PREFIX} === BOSS COMBAT TEST INITIALIZED ===");
            Debug.Log($"{LOG_PREFIX} Boss: {_bossName}");
            Debug.Log($"{LOG_PREFIX} State: {_lastKnownState}");

            if (_bossAI.bossDef != null)
            {
                var def = _bossAI.bossDef;
                Debug.Log($"{LOG_PREFIX} Def: bossId={def.bossId}, realm={def.realm}");
                Debug.Log($"{LOG_PREFIX} Stats: baseHP={def.baseMaxHP}, baseATK={def.baseAttack}, baseDEF={def.baseDefense}");
                Debug.Log($"{LOG_PREFIX} Ranges: detect={def.detectRange}m, aggro={def.aggroRange}m, leash={def.leashRange}m");
                Debug.Log($"{LOG_PREFIX} Phases: {(def.phases != null ? def.phases.Length : 0)} configured");
                Debug.Log($"{LOG_PREFIX} Enrage: {def.enrageTimeLimit}s");
                Debug.Log($"{LOG_PREFIX} Weaknesses: {(def.weaknesses != null ? def.weaknesses.Length : 0)} configured");
                Debug.Log($"{LOG_PREFIX} Attacks: {(def.attacks != null ? def.attacks.Length : 0)} in attack pool");
                Debug.Log($"{LOG_PREFIX} Entrance Dialogue: \"{def.entranceDialogue}\"");
                Debug.Log($"{LOG_PREFIX} Defeat Dialogue: \"{def.defeatDialogue}\"");
                Debug.Log($"{LOG_PREFIX} Scaled HP: {def.ScaledMaxHP:F0}, ATK: {def.ScaledAttack:F1}, DEF: {def.ScaledDefense:F1}");
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} BossDef is NULL on BossAI! Events may not fire correctly.");
            }

            // Log all subsystems found on the boss
            LogSubsystem<BossWeaknessSystem>(bossGO, "BossWeaknessSystem");
            LogSubsystem<BossGrudgeSystem>(bossGO, "BossGrudgeSystem");
            LogSubsystem<BossDropTable>(bossGO, "BossDropTable");

            LogDivider();

            _initialized = true;
        }

        private void SetPlayerTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _bossAI.SetPlayerTarget(player.transform);
                Debug.Log($"{LOG_PREFIX} Player target set: '{player.name}' at position {player.transform.position}");
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Player not found via 'Player' tag. " +
                                 "BossAI will fall back to OverlapSphere detection (requires 'Player' layer).");
            }
        }

        private void LogSubsystem<T>(GameObject go, string label) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null)
                Debug.Log($"{LOG_PREFIX} Subsystem {label}: ATTACHED");
            else
                Debug.LogWarning($"{LOG_PREFIX} Subsystem {label}: MISSING");
        }

        // ─── Event Subscriptions ──────────────────────────────────────────

        private void SubscribeEvents()
        {
            EventBus.Subscribe<BossEntranceEvent>(OnBossEntrance);
            EventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<BossEnrageEvent>(OnBossEnrage);
            EventBus.Subscribe<BossDialogueEvent>(OnBossDialogue);
            EventBus.Subscribe<BossBreathingWindowEvent>(OnBossBreathingWindow);
            EventBus.Subscribe<BossRetreatEvent>(OnBossRetreat);
            EventBus.Subscribe<BossDropRolledEvent>(OnBossDropRolled);
            EventBus.Subscribe<BossEscapeEvent>(OnBossEscape);

            Debug.Log($"{LOG_PREFIX} Subscribed to 9 boss event types");
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<BossEntranceEvent>(OnBossEntrance);
            EventBus.Unsubscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<BossEnrageEvent>(OnBossEnrage);
            EventBus.Unsubscribe<BossDialogueEvent>(OnBossDialogue);
            EventBus.Unsubscribe<BossBreathingWindowEvent>(OnBossBreathingWindow);
            EventBus.Unsubscribe<BossRetreatEvent>(OnBossRetreat);
            EventBus.Unsubscribe<BossDropRolledEvent>(OnBossDropRolled);
            EventBus.Unsubscribe<BossEscapeEvent>(OnBossEscape);
        }

        // ─── State Polling ────────────────────────────────────────────────

        private void PollBossState()
        {
            if (_bossAI == null) return;

            string currentState = _bossAI.CurrentState;

            // Detect state transitions
            if (currentState != _lastKnownState)
            {
                _totalStateTransitions++;
                string arrow = " -> ";
                Debug.Log($"{LOG_PREFIX} State Transition [{_totalStateTransitions}]: {_lastKnownState}{arrow}{currentState}");

                if (currentState == "Detecting")
                    Debug.Log($"{LOG_PREFIX} Player detected! Boss moving to entrance sequence.");

                if (currentState == "Combat" && _lastKnownState == "Entrance")
                    Debug.Log($"{LOG_PREFIX} Combat started! Boss is now fighting.");

                if (currentState == "Defeated")
                    Debug.Log($"{LOG_PREFIX} Boss defeated. Combat lifecycle complete.");

                _lastKnownState = currentState;
            }

            // Update cached values
            _bossCurrentHP = _bossAI.CurrentHP;
            _bossMaxHP = _bossAI.MaxHP;
            _bossHPPercent = _bossAI.HPPercent;
            _bossPhase = _bossAI.CurrentPhase;
            _bossAlive = _bossAI.IsAlive;
            _isEnraged = _bossAI.IsEnraged;
            _isInBreathingWindow = _bossAI.IsInBreathingWindow;
            _timeUntilEnrage = _bossAI.TimeUntilEnrage;
        }

        // ─── Combat Report ────────────────────────────────────────────────

        private void LogCombatReport()
        {
            if (_bossAI == null) return;

            float hpPercent = _bossMaxHP > 0f ? (_bossCurrentHP / _bossMaxHP * 100f) : 0f;
            string enrageTag = _isEnraged ? " [ENRAGED]" : "";
            string breathingTag = _isInBreathingWindow ? " [Breathing]" : "";

            string report = $"{LOG_PREFIX} Boss {_bossName}, phase {_bossPhase}, HP {_bossCurrentHP:F0}/{_bossMaxHP:F0} ({hpPercent:F1}%){enrageTag}{breathingTag}";

            if (_timeUntilEnrage > 0f && !_isEnraged && _timeUntilEnrage <= 60f)
            {
                report += $" | Enrage in {_timeUntilEnrage:F0}s";
            }

            Debug.Log(report);
        }

        // ─── Event Handlers ───────────────────────────────────────────────

        /// <summary>
        /// BossEntranceEvent — boss出场演出开始。
        /// Fired by BossAI.PlayEntranceSequence().
        /// </summary>
        private void OnBossEntrance(BossEntranceEvent evt)
        {
            string name = SafeToString(evt.BossName, "Unknown");
            string title = SafeToString(evt.Title, "");
            string realmStr = SafeToString(evt.Realm, "?");
            string dialogue = SafeToString(evt.Dialogue, "");

            LogDivider();
            Debug.Log($"{LOG_PREFIX} +++++ BOSS ENTRANCE +++++");
            Debug.Log($"{LOG_PREFIX} Monster: {name}");
            Debug.Log($"{LOG_PREFIX} Title: {title}");
            Debug.Log($"{LOG_PREFIX} Realm: Lv.{realmStr}");
            Debug.Log($"{LOG_PREFIX} Entrance Dialogue: \"{dialogue}\"");
            Debug.Log($"{LOG_PREFIX} +++++ ENTRANCE EVENT VERIFIED +++++");
            LogDivider();
        }

        /// <summary>
        /// BossPhaseChangedEvent — BOSS阶段转换。
        /// Fired by BossAI.StartPhaseTransition().
        /// </summary>
        private void OnBossPhaseChanged(BossPhaseChangedEvent evt)
        {
            int phaseIndex = SafeParseInt(evt.NewPhaseIndex, 0);
            float hpPercent = SafeParseFloat(evt.CurrentHPPercent, 0f);
            string dialogue = SafeToString(evt.Dialogue, "");
            float breathingDuration = SafeParseFloat(evt.BreathingWindowDuration, 0f);

            Debug.Log($"{LOG_PREFIX} >>> PHASE TRANSITION <<<");
            Debug.Log($"{LOG_PREFIX} Entering Phase {phaseIndex} at {hpPercent * 100f:F1}% HP");
            Debug.Log($"{LOG_PREFIX} Phase Dialogue: \"{dialogue}\"");
            Debug.Log($"{LOG_PREFIX} Breathing Window: {breathingDuration}s");
            Debug.Log($"{LOG_PREFIX} >>> PHASE TRANSITION VERIFIED <<<");
        }

        /// <summary>
        /// BossDefeatedEvent — BOSS被击杀。
        /// Fired by BossAI.Die().
        /// </summary>
        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            string name = SafeToString(evt.BossName, "Unknown");
            string bossId = SafeToString(evt.BossId, "?");
            int finalPhase = SafeParseInt(evt.FinalPhase, 0);

            LogDivider();
            Debug.Log($"{LOG_PREFIX} *** BOSS DEFEATED ***");
            Debug.Log($"{LOG_PREFIX} Boss: {name} (id: {bossId})");
            Debug.Log($"{LOG_PREFIX} Final Phase Reached: {finalPhase}");
            Debug.Log($"{LOG_PREFIX} Total State Transitions: {_totalStateTransitions}");
            Debug.Log($"{LOG_PREFIX} *** DEFEAT EVENT VERIFIED ***");

            Debug.Log("==============================================");
            Debug.Log($"{LOG_PREFIX} BOSS COMBAT TEST COMPLETE");
            Debug.Log("==============================================");
        }

        /// <summary>
        /// BossEnrageEvent — 狂暴警告/激活。
        /// Fired by BossAI.TriggerEnrage() and BossAI.UpdateCombat().
        /// </summary>
        private void OnBossEnrage(BossEnrageEvent evt)
        {
            bool isEnraged = evt.IsEnraged is bool b && b;

            if (isEnraged)
            {
                Debug.Log($"{LOG_PREFIX} !!! BOSS ENRAGED !!!");
                Debug.Log($"{LOG_PREFIX} Boss has entered berserk mode! Damage increased!");
            }
            else
            {
                string timeLeft = SafeToString(evt.TimeUntilEnrage, "0");
                Debug.Log($"{LOG_PREFIX} Enrage Warning: {timeLeft}s remaining before berserk!");
            }
        }

        /// <summary>
        /// BossDialogueEvent — BOSS台词。
        /// Fired by BossAI at various lifecycle points.
        /// </summary>
        private void OnBossDialogue(BossDialogueEvent evt)
        {
            string speaker = SafeToString(evt.Speaker, "???");
            string line = SafeToString(evt.Line, "");
            float duration = SafeParseFloat(evt.DisplayDuration, 3f);

            Debug.Log($"{LOG_PREFIX} [{speaker}] \"{line}\" (display: {duration}s)");
        }

        /// <summary>
        /// BossBreathingWindowEvent — 喘息窗口启用/结束。
        /// Fired by BossAI.BreathingWindow() and BossAI.UpdateBreathingWindow().
        /// </summary>
        private void OnBossBreathingWindow(BossBreathingWindowEvent evt)
        {
            bool isActive = evt.IsActive is bool b2 && b2;
            float duration = SafeParseFloat(evt.Duration, 0f);

            if (isActive)
            {
                Debug.Log($"{LOG_PREFIX} === Breathing Window STARTED ({duration:F1}s) ===");
                Debug.Log($"{LOG_PREFIX} Boss is vulnerable to player recovery.");
            }
            else
            {
                Debug.Log($"{LOG_PREFIX} === Breathing Window ENDED ===");
            }
        }

        /// <summary>
        /// BossRetreatEvent — BOSS撤退。
        /// Fired by BossGrudgeSystem.RetreatResetRoutine().
        /// </summary>
        private void OnBossRetreat(BossRetreatEvent evt)
        {
            string name = SafeToString(evt.BossName, "Unknown");
            string region = SafeToString(evt.RegionId, "?");
            float aggroIncrease = SafeParseFloat(evt.AggressionIncrease, 0f);

            Debug.Log($"{LOG_PREFIX} === BOSS RETREAT ===");
            Debug.Log($"{LOG_PREFIX} {name} retreated to region [{region}]");
            Debug.Log($"{LOG_PREFIX} Regional aggression +{aggroIncrease * 100f:F0}%");
        }

        /// <summary>
        /// BossDropRolledEvent — 掉落结算。
        /// Fired by BossDropTable.PublishDropEvents().
        /// </summary>
        private void OnBossDropRolled(BossDropRolledEvent evt)
        {
            string name = SafeToString(evt.BossName, "Unknown");
            bool isFirstKill = evt.IsFirstKill is bool fk && fk;
            bool perfectHunt = evt.PerfectHuntBonus is bool ph && ph;

            Debug.Log($"{LOG_PREFIX} === DROP ROLLED ===");
            Debug.Log($"{LOG_PREFIX} Boss: {name}");
            Debug.Log($"{LOG_PREFIX} First Kill: {isFirstKill}");
            Debug.Log($"{LOG_PREFIX} Perfect Hunt Bonus: {perfectHunt}");

            // Decode item arrays
            string[] itemNames = evt.ItemNames as string[];
            int[] quantities = evt.Quantities as int[];
            string[] qualities = evt.Qualities as string[];

            if (itemNames != null)
            {
                Debug.Log($"{LOG_PREFIX} Total Drops: {itemNames.Length}");
                for (int i = 0; i < itemNames.Length; i++)
                {
                    string qtyStr = (quantities != null && i < quantities.Length) ? $"x{quantities[i]}" : "";
                    string qualityStr = (qualities != null && i < qualities.Length) ? $"[{qualities[i]}]" : "";
                    Debug.Log($"{LOG_PREFIX}   Drop {i + 1}: {qualityStr} {itemNames[i]} {qtyStr}");
                }
            }

            Debug.Log($"{LOG_PREFIX} === DROP VERIFIED ===");
        }

        /// <summary>
        /// BossEscapeEvent — BOSS记仇逃跑登记。
        /// Fired by BossGrudgeSystem.RecordEscape().
        /// </summary>
        private void OnBossEscape(BossEscapeEvent evt)
        {
            string name = SafeToString(evt.BossName, "Unknown");
            int grudgeIncrease = SafeParseInt(evt.GrudgeIncrease, 0);

            Debug.Log($"{LOG_PREFIX} === ESCAPE RECORDED ===");
            Debug.Log($"{LOG_PREFIX} {name}: Grudge +{grudgeIncrease}");
        }

        // ─── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Force engage: teleport the player next to the boss and trigger detection.
        ///
        /// The player is placed at 70% of detectRange from the boss, facing the boss.
        /// This is equivalent to walking up to the boss — the Idle->Detecting->Entrance
        /// state machine should fire automatically.
        ///
        /// Access via Inspector context menu or from any script.
        /// </summary>
        [ContextMenu("Force Engage - Teleport Player to Boss")]
        public void ForceEngage()
        {
            if (_bossAI == null)
            {
                Debug.LogError($"{LOG_PREFIX} BossAI not found. Searching for '{bossGameObjectName}' again...");
                if (!LocateBoss())
                {
                    Debug.LogError($"{LOG_PREFIX} ForceEngage failed: BossAI not found.");
                    return;
                }
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"{LOG_PREFIX} ForceEngage failed: Player not found (tag 'Player').");
                return;
            }

            // Ensure player target is set on the boss
            _bossAI.SetPlayerTarget(player.transform);

            // Calculate approach distance: place player within detectRange but far enough
            // to not immediately aggro (so we can observe the Detecting state)
            float detectRange = _bossAI.bossDef != null ? _bossAI.bossDef.detectRange : 30f;
            float aggroRange = _bossAI.bossDef != null ? _bossAI.bossDef.aggroRange : 20f;

            // Place between detectRange and aggroRange to trigger detection but not aggro immediately
            float approachDistance = detectRange * 0.85f;

            Vector3 bossPos = _bossAI.transform.position;
            Vector3 direction = (player.transform.position - bossPos);
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
            {
                // Player is too close or at boss position - use a default direction
                direction = Vector3.forward;
            }
            else
            {
                direction.Normalize();
            }

            Vector3 targetPosition = bossPos + direction * approachDistance;
            targetPosition.y = bossPos.y; // Same height as boss

            // Move the player using CharacterController if available, else direct transform
            TeleportPlayer(player, targetPosition);

            Debug.Log(string.Empty);
            LogDivider();
            Debug.Log($"{LOG_PREFIX} === FORCE ENGAGE ===");
            Debug.Log($"{LOG_PREFIX} Player teleported to {targetPosition}");
            Debug.Log($"{LOG_PREFIX} Distance from boss: {approachDistance:F1}m");
            Debug.Log($"{LOG_PREFIX} detectRange: {detectRange}m, aggroRange: {aggroRange}m");
            Debug.Log($"{LOG_PREFIX} Boss should transition: Idle -> Detecting -> Entrance -> Combat");
            Debug.Log($"{LOG_PREFIX} Current boss state: {_bossAI.CurrentState}");
            LogDivider();
        }

        /// <summary>
        /// Force engage variant: place the player directly within aggro range
        /// to skip Detecting state and immediately trigger the entrance sequence.
        /// </summary>
        [ContextMenu("Force Engage (Direct Aggro) - Teleport Player Into Aggro Range")]
        public void ForceEngageDirectAggro()
        {
            if (_bossAI == null)
            {
                Debug.LogError($"{LOG_PREFIX} ForceEngageDirectAggro failed: BossAI not found.");
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"{LOG_PREFIX} ForceEngageDirectAggro failed: Player not found.");
                return;
            }

            _bossAI.SetPlayerTarget(player.transform);

            float aggroRange = _bossAI.bossDef != null ? _bossAI.bossDef.aggroRange : 20f;
            float approachDistance = aggroRange * 0.7f;

            Vector3 bossPos = _bossAI.transform.position;
            Vector3 direction = (player.transform.position - bossPos);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            Vector3 targetPosition = bossPos + direction * approachDistance;
            targetPosition.y = bossPos.y;

            TeleportPlayer(player, targetPosition);

            LogDivider();
            Debug.Log($"{LOG_PREFIX} === FORCE ENGAGE (DIRECT AGGRO) ===");
            Debug.Log($"{LOG_PREFIX} Player teleported to {targetPosition} (aggro range: {aggroRange}m)");
            Debug.Log($"{LOG_PREFIX} Boss should queue Entrance immediately!");
            LogDivider();
        }

        /// <summary>
        /// Simulate damage to the boss to test phase transitions.
        /// </summary>
        /// <param name="damageAmount">Raw damage to deal.</param>
        [ContextMenu("Simulate Damage 500")]
        public void SimulateDamage()
        {
            SimulateDamageToBoss(500f);
        }

        /// <summary>
        /// Simulate damage with a specific amount.
        /// </summary>
        public void SimulateDamageToBoss(float damageAmount)
        {
            if (_bossAI == null || !_bossAI.IsAlive)
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot simulate damage: Boss not found or dead.");
                return;
            }

            float actualDamage = _bossAI.TakeDamage(damageAmount);
            Debug.Log($"{LOG_PREFIX} Simulated damage: {damageAmount} raw -> {actualDamage:F1} actual");
            Debug.Log($"{LOG_PREFIX} Boss HP now: {_bossAI.CurrentHP:F0}/{_bossAI.MaxHP:F0}");
        }

        // ─── Utilities ────────────────────────────────────────────────────

        private static void TeleportPlayer(GameObject player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = position;
                controller.enabled = true;
            }
            else
            {
                player.transform.position = position;
            }
        }

        private static string SafeToString(object obj, string fallback)
        {
            if (obj == null) return fallback;
            string str = obj.ToString();
            return string.IsNullOrEmpty(str) ? fallback : str;
        }

        private static int SafeParseInt(object obj, int fallback)
        {
            if (obj is int i) return i;
            if (obj is float f) return Mathf.RoundToInt(f);
            if (obj is double d) return (int)d;
            if (obj != null && int.TryParse(obj.ToString(), out int parsed))
                return parsed;
            return fallback;
        }

        private static float SafeParseFloat(object obj, float fallback)
        {
            if (obj is float f) return f;
            if (obj is int i) return i;
            if (obj is double d) return (float)d;
            if (obj != null && float.TryParse(obj.ToString(), out float parsed))
                return parsed;
            return fallback;
        }

        private static void LogDivider()
        {
            Debug.Log("----------------------------------------------");
        }
    }
}
