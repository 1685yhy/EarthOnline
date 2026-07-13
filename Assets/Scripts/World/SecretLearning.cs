using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>Methods of secret learning (偷学).</summary>
    public enum SecretLearningMethod
    {
        InfiltrateScriptureHall,  // 潜入藏经阁
        Bribe,                    // 贿赂
    }

    /// <summary>Time period affecting stealth success rate bonus.</summary>
    public enum TimePeriod
    {
        Day,        // 白天 — +0%
        Night,      // 夜晚 — +10%
        DeepNight,  // 深夜 — +20%
    }

    /// <summary>Severity level when a secret learning attempt is discovered.</summary>
    public enum DiscoverySeverity
    {
        Warning,        // 警告 — first offense, record only
        Confinement,    // 关禁闭72h — cannot attempt secret learning for 72 hours
        ForcedBetrayal, // 强制叛逃 — removed from formal sect, severe reputation penalties
    }

    /// <summary>Outcome of a secret learning attempt.</summary>
    public enum SecretLearningOutcome
    {
        Success,    // technique fragment acquired
        Failed,     // attempt failed but not discovered
        Discovered, // attempt failed and player was discovered
    }

    // ─── Event Data (EventBus) ──────────────────────────────────────

    /// <summary>Published when a secret learning attempt succeeds or fails without discovery.</summary>
    public struct SecretLearningAttemptEvent
    {
        public string PlayerId;
        public SecretLearningMethod Method;
        public bool Success;
        public string TargetSect;
        public string TechniqueId;
        public string TechniqueName;
        public float FragmentPercentage;   // 0.5 ~ 0.8 for success, 0 for failure
    }

    /// <summary>Published when a player is discovered during secret learning.</summary>
    public struct SecretLearningDiscoveredEvent
    {
        public string PlayerId;
        public DiscoverySeverity Severity;
        public string TargetSect;
        public SecretLearningMethod Method;
        public int OffenceCount;          // 1-based offence number
    }

    /// <summary>Published when a player leaves their sect due to forced betrayal.</summary>
    public struct ForcedBetrayalEvent
    {
        public string PlayerId;
        public string PreviousSectName;
        public string Reason;             // e.g. "偷学被发现，强制叛逃"
    }

    /// <summary>Published when a rogue cultivator completes an alliance bounty.</summary>
    public struct RogueBountyCompletedEvent
    {
        public string PlayerId;
        public string BountyId;
        public int RewardSpiritStones;
        public int RewardReputation;
    }

    /// <summary>Published when a rogue cultivator undergoes a tribulation,
    /// carrying the modified failure rate and dao body bonus.</summary>
    public struct RogueTribulationModifierEvent
    {
        public string PlayerId;
        public float ModifiedFailureRate; // base + 0.20 for rogues
        public int DaoBodyQualityBonus;   // +1 for rogues, 0 for formal
    }

    /// <summary>Published when the player purchases an item from the alliance market.</summary>
    public struct AllianceMarketPurchaseEvent
    {
        public string PlayerId;
        public string ItemId;
        public int Cost;
        public int RemainingStock;
    }

    /// <summary>Published when confinement begins or ends.</summary>
    public struct ConfinementStatusEvent
    {
        public string PlayerId;
        public bool IsActive;
        public double RemainingHours;
        public string TargetSect;         // sect that imposed the confinement
    }

    // ─── Config Data Classes ──────────────────────────────────────────

    /// <summary>Configurable parameters for secret learning mechanics.</summary>
    [Serializable]
    public class SecretLearningConfig
    {
        [Header("── Success Rate Formula ──")]
        [Tooltip("基础成功率 25%")]
        [Range(0f, 1f)] public float BaseSuccessRate = 0.25f;

        [Tooltip("每点潜行属性加成: 2%")]
        [Range(0f, 0.1f)] public float StealthBonusPerPoint = 0.02f;

        [Tooltip("每点目标门派警戒度扣减: 1%")]
        [Range(0f, 0.1f)] public float AlertnessPenaltyPerPoint = 0.01f;

        [Header("── Time Period Bonuses ──")]
        [Tooltip("白天: +0%")]
        [Range(0f, 0.5f)] public float DayBonus = 0f;
        [Tooltip("夜晚: +10%")]
        [Range(0f, 0.5f)] public float NightBonus = 0.10f;
        [Tooltip("深夜: +20%")]
        [Range(0f, 0.5f)] public float DeepNightBonus = 0.20f;

        [Header("── Technique Fragment ──")]
        [Tooltip("偷学成功获得的功法残篇比例 50%~80%")]
        [Range(0f, 1f)] public float MinFragmentPercent = 0.50f;
        [Range(0f, 1f)] public float MaxFragmentPercent = 0.80f;

        [Header("── Bribe Costs ──")]
        [Tooltip("贿赂基础费用（灵石）")]
        public int BribeBaseCost = 500;
        [Tooltip("每级门派职位额外费用")]
        public int BribeCostPerRank = 200;

        [Header("── Bribe Modifiers ──")]
        [Tooltip("贿赂的额外成功率加成")]
        [Range(0f, 0.3f)] public float BribeSuccessBonus = 0.05f;
        [Tooltip("双倍贿赂额外加成")]
        [Range(0f, 0.3f)] public float BribeOverpayBonus = 0.10f;
        [Tooltip("贿赂被发现概率折减系数（越小越安全）")]
        [Range(0f, 1f)] public float BribeDiscoveryFactor = 0.6f;

        [Header("── Discovery Thresholds ──")]
        [Tooltip("偷学失败触发[被发现]检定的概率乘数：失败时 x 此值")]
        [Range(0f, 1f)] public float DiscoveryChanceOnFailure = 0.5f;

        [Header("── Cooldowns ──")]
        [Tooltip("关禁闭时长（小时）")]
        public int ConfinementDurationHours = 72;
    }

    /// <summary>A bounty posted by the rogue cultivator alliance (散修联盟悬赏).</summary>
    [Serializable]
    public class RogueBountyDefinition
    {
        public string BountyId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public int RequiredRealmLevel;
        public int RewardSpiritStones;
        public int RewardReputation;
        public string TargetType;  // "monster", "material_gathering", "item_delivery", "elimination"
        public bool IsRepeatable;
    }

    /// <summary>An item for sale in the alliance market (散修联盟坊市).</summary>
    [Serializable]
    public class AllianceMarketItemDefinition
    {
        public string ItemId;
        public string DisplayName;
        [TextArea(1, 2)] public string Description;
        public int CostSpiritStones;
        public int MaxStock;       // -1 = unlimited
        public int InitialStock;
    }

    // ─── Player-State Data ───────────────────────────────────────────

    /// <summary>A technique fragment acquired through secret learning.</summary>
    [Serializable]
    public class LearnedTechniqueFragment
    {
        public string TechniqueId;
        public string TechniqueName;
        public float FragmentPercent;       // 0.50 ~ 0.80
        public SecretLearningMethod Method;
        public double LearnedTimestamp;     // Unix seconds
        public string SourceSect;           // which sect the technique was stolen from
    }

    /// <summary>Runtime player state for secret learning and rogue path.</summary>
    [Serializable]
    public class PlayerSecretLearningState
    {
        public string PlayerId;

        // ── Discovery Escalation ──
        /// <summary>Number of warning-level offences received (0, 1, or 2+).</summary>
        public int WarningCount;

        /// <summary>Unix timestamp when confinement ends (0 = not confined).</summary>
        public double ConfinementEndTimestamp;

        /// <summary>Sect that imposed the current or last confinement.</summary>
        public string LastConfinementSect;

        /// <summary>
        /// Whether the player is currently confined.
        /// Confinement prevents secret learning attempts.
        /// </summary>
        public bool IsConfinementActive =>
            ConfinementEndTimestamp > 0
            && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < ConfinementEndTimestamp;

        // ── Bounty Tracking ──
        public HashSet<string> CompletedBountyIds = new HashSet<string>();
        public HashSet<string> ActiveBountyIds = new HashSet<string>();

        // ── Market Stock Tracking ──
        /// <summary>ItemId → remaining stock count. Only tracks items with finite stock.</summary>
        public Dictionary<string, int> MarketStockRemaining = new Dictionary<string, int>();

        // ── Learned Techniques ──
        public List<LearnedTechniqueFragment> TechniqueFragments = new List<LearnedTechniqueFragment>();
    }

    // ─── Secret Learning Manager ──────────────────────────────────────

    /// <summary>
    /// Manages the secret learning (偷学) and rogue cultivator (散修) systems.
    ///
    /// ## 偷学 (Secret Learning)
    /// - Two methods: infiltrate scripture hall (潜入藏经阁) or bribe (贿赂)
    /// - Formula: base 25% + Stealth × 2% - Alertness + TimePeriodBonus
    /// - Time bonuses: Day 0%, Night +10%, DeepNight +20%
    /// - Success yields technique fragments at 50-80% completion
    ///
    /// ## Discovery Escalation
    /// - 1st offence: Warning (recorded)
    /// - 2nd offence: Confinement 72h (no secret learning allowed)
    /// - 3rd offence: Forced betrayal (kicked from formal sect)
    ///
    /// ## 散修 (Rogue Cultivator Path)
    /// - Independent of formal sect membership
    /// - Tribulation failure rate +20%, but success grants Dao Body quality +1
    /// - Access to alliance features: bounties (悬赏), market (坊市), intel (情报)
    ///
    /// Depends on: SectManager (Story 001, 002)
    /// Unlocks: Story 004
    /// </summary>
    public class SecretLearningManager : MonoBehaviour
    {
        // ─── Singleton ─────────────────────────────────────────────────

        public static SecretLearningManager Instance { get; private set; }

        // ─── Serialized Config Overrides ──────────────────────────────

        [Header("Configuration")]
        [SerializeField] private SecretLearningConfig _config = new SecretLearningConfig();

        [Header("散修联盟 — 悬赏榜")]
        [SerializeField] private RogueBountyDefinition[] _bountyDefinitions;

        [Header("散修联盟 — 坊市")]
        [SerializeField] private AllianceMarketItemDefinition[] _marketItemDefinitions;

        // ─── Runtime State ─────────────────────────────────────────────

        private Dictionary<string, PlayerSecretLearningState> _playerStates =
            new Dictionary<string, PlayerSecretLearningState>();

        // ─── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 1 — Success Rate Calculation
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the secret learning success rate.
        /// Formula: base(25%) + Stealth × 2% - Alertness(1%/pt) + TimePeriodBonus
        ///
        /// Parameters:
        ///   stealth    — player's stealth attribute (0~100 scale)
        ///   alertness  — target sect's current alertness level (0~100 scale)
        ///   timePeriod  — current time period affecting visibility
        ///
        /// Returns clamped [0, 1].
        /// </summary>
        public float CalculateSuccessRate(float stealth, float alertness, TimePeriod timePeriod)
        {
            float timeBonus = timePeriod switch
            {
                TimePeriod.Day      => _config.DayBonus,
                TimePeriod.Night    => _config.NightBonus,
                TimePeriod.DeepNight => _config.DeepNightBonus,
                _ => 0f,
            };

            float rate = _config.BaseSuccessRate
                + stealth * _config.StealthBonusPerPoint
                - alertness * _config.AlertnessPenaltyPerPoint
                + timeBonus;

            return Mathf.Clamp01(rate);
        }

        /// <summary>
        /// Determine the time period from a 0-24 hour clock value.
        ///
        ///   Day:       06:00 ~ 17:59  (+0%)
        ///   Night:     18:00 ~ 22:59  (+10%)
        ///   DeepNight: 23:00 ~ 05:59  (+20%)
        /// </summary>
        public TimePeriod GetTimePeriod(float hourOfDay)
        {
            if (hourOfDay >= 6f && hourOfDay < 18f) return TimePeriod.Day;
            if (hourOfDay >= 18f && hourOfDay < 23f) return TimePeriod.Night;
            return TimePeriod.DeepNight;
        }

        /// <summary>Localized display name for a time period.</summary>
        public static string GetTimePeriodName(TimePeriod period) => period switch
        {
            TimePeriod.Day      => "白天",
            TimePeriod.Night    => "夜晚",
            TimePeriod.DeepNight => "深夜",
            _ => "未知",
        };

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 2 — Secret Learning Attempts
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempt to infiltrate a sect's scripture hall (潜入藏经阁).
        ///
        /// Returns the outcome and, on success, the fragment percentage.
        /// On discovery, automatically escalates through the warning →
        /// confinement → forced betrayal chain.
        /// </summary>
        /// <param name="playerId">The player attempting the theft.</param>
        /// <param name="targetSectName">Display name of the target sect.</param>
        /// <param name="stealth">Player's stealth stat value.</param>
        /// <param name="alertness">Target sect's current alertness level.</param>
        /// <param name="timePeriod">Current time period for bonus calculation.</param>
        /// <param name="techniqueId">Identifier of the targeted technique.</param>
        /// <param name="techniqueName">Display name of the targeted technique.</param>
        /// <param name="fragmentOut">[out] Fragment percentage on success (0.5~0.8).</param>
        /// <returns>Outcome enum.</returns>
        public SecretLearningOutcome AttemptInfiltrate(
            string playerId,
            string targetSectName,
            float stealth,
            float alertness,
            TimePeriod timePeriod,
            string techniqueId,
            string techniqueName,
            out float fragmentOut)
        {
            fragmentOut = 0f;

            // ── Guard: confinement check ──
            var guardResult = PreAttemptGuard(playerId, targetSectName, out _);
            if (guardResult != SecretLearningOutcome.Success)
            {
                // guardResult is either Failed or Discovered (if already confined)
                return guardResult;
            }

            // ── Roll ──
            float successRate = CalculateSuccessRate(stealth, alertness, timePeriod);
            float roll = UnityEngine.Random.value;

            if (roll <= successRate)
            {
                // SUCCESS
                var state = EnsurePlayerState(playerId);
                fragmentOut = RollFragmentPercent();
                RecordLearnedTechnique(state, techniqueId, techniqueName,
                    SecretLearningMethod.InfiltrateScriptureHall, targetSectName, fragmentOut);

                Debug.Log($"[SecretLearning] {playerId} 潜入{targetSectName}藏经阁成功，"
                    + $"偷得【{techniqueName}】残篇 {fragmentOut * 100:F0}%");

                EventBus.Publish(new SecretLearningAttemptEvent
                {
                    PlayerId = playerId,
                    Method = SecretLearningMethod.InfiltrateScriptureHall,
                    Success = true,
                    TargetSect = targetSectName,
                    TechniqueId = techniqueId,
                    TechniqueName = techniqueName,
                    FragmentPercentage = fragmentOut,
                });

                return SecretLearningOutcome.Success;
            }

            // ── Failure: determine if discovered ──
            float failRoll = UnityEngine.Random.value;
            float discoveryChance = _config.DiscoveryChanceOnFailure;

            if (failRoll < discoveryChance)
            {
                // Discovered
                HandleDiscovery(playerId, targetSectName, SecretLearningMethod.InfiltrateScriptureHall);

                EventBus.Publish(new SecretLearningAttemptEvent
                {
                    PlayerId = playerId,
                    Method = SecretLearningMethod.InfiltrateScriptureHall,
                    Success = false,
                    TargetSect = targetSectName,
                    TechniqueId = techniqueId,
                    TechniqueName = techniqueName,
                    FragmentPercentage = 0f,
                });

                return SecretLearningOutcome.Discovered;
            }

            // Clean failure (not discovered)
            Debug.Log($"[SecretLearning] {playerId} 潜入{targetSectName}藏经阁失败（未被发现）");

            EventBus.Publish(new SecretLearningAttemptEvent
            {
                PlayerId = playerId,
                Method = SecretLearningMethod.InfiltrateScriptureHall,
                Success = false,
                TargetSect = targetSectName,
                TechniqueId = techniqueId,
                TechniqueName = techniqueName,
                FragmentPercentage = 0f,
            });

            return SecretLearningOutcome.Failed;
        }

        /// <summary>
        /// Attempt to bribe a sect member for a technique (贿赂).
        ///
        /// Bribery has a slightly higher base success rate (extra +5%),
        /// and a reduced discovery chance, but requires spirit stone payment.
        /// Paying double the required cost grants an additional +10% bonus.
        /// </summary>
        /// <param name="playerId">The player attempting the bribe.</param>
        /// <param name="targetSectName">Display name of the target sect.</param>
        /// <param name="stealth">Player's stealth stat (affects the bribe negotiation).</param>
        /// <param name="alertness">Target sect's current alertness level.</param>
        /// <param name="timePeriod">Current time period.</param>
        /// <param name="techniqueId">Identifier of the targeted technique.</param>
        /// <param name="techniqueName">Display name of the targeted technique.</param>
        /// <param name="bribePayment">Amount of spirit stones offered.</param>
        /// <param name="fragmentOut">[out] Fragment percentage on success (0.5~0.8).</param>
        /// <returns>Outcome enum.</returns>
        public SecretLearningOutcome AttemptBribe(
            string playerId,
            string targetSectName,
            float stealth,
            float alertness,
            TimePeriod timePeriod,
            string techniqueId,
            string techniqueName,
            int bribePayment,
            out float fragmentOut)
        {
            fragmentOut = 0f;

            // ── Guard: confinement check ──
            var guardResult = PreAttemptGuard(playerId, targetSectName, out _);
            if (guardResult != SecretLearningOutcome.Success)
                return guardResult;

            // ── Bribe amount check ──
            int requiredBribe = _config.BribeBaseCost;
            if (bribePayment < requiredBribe)
            {
                Debug.Log($"[SecretLearning] {playerId} 贿赂金额不足：需 {requiredBribe} 灵石，实际 {bribePayment}");
                return SecretLearningOutcome.Failed;
            }

            // ── Roll ──
            float successRate = CalculateSuccessRate(stealth, alertness, timePeriod)
                + _config.BribeSuccessBonus;

            if (bribePayment >= requiredBribe * 2)
            {
                successRate += _config.BribeOverpayBonus;
            }
            successRate = Mathf.Clamp01(successRate);

            float roll = UnityEngine.Random.value;

            if (roll <= successRate)
            {
                // SUCCESS
                var state = EnsurePlayerState(playerId);
                fragmentOut = RollFragmentPercent();
                RecordLearnedTechnique(state, techniqueId, techniqueName,
                    SecretLearningMethod.Bribe, targetSectName, fragmentOut);

                Debug.Log($"[SecretLearning] {playerId} 贿赂{targetSectName}成员成功，"
                    + $"花费{bribePayment}灵石，获得【{techniqueName}】残篇 {fragmentOut * 100:F0}%");

                EventBus.Publish(new SecretLearningAttemptEvent
                {
                    PlayerId = playerId,
                    Method = SecretLearningMethod.Bribe,
                    Success = true,
                    TargetSect = targetSectName,
                    TechniqueId = techniqueId,
                    TechniqueName = techniqueName,
                    FragmentPercentage = fragmentOut,
                });

                return SecretLearningOutcome.Success;
            }

            // ── Failure: bribery has reduced discovery chance ──
            float failRoll = UnityEngine.Random.value;
            float discoveryChance = _config.DiscoveryChanceOnFailure * _config.BribeDiscoveryFactor;

            if (failRoll < discoveryChance)
            {
                HandleDiscovery(playerId, targetSectName, SecretLearningMethod.Bribe);

                EventBus.Publish(new SecretLearningAttemptEvent
                {
                    PlayerId = playerId,
                    Method = SecretLearningMethod.Bribe,
                    Success = false,
                    TargetSect = targetSectName,
                    TechniqueId = techniqueId,
                    TechniqueName = techniqueName,
                    FragmentPercentage = 0f,
                });

                return SecretLearningOutcome.Discovered;
            }

            Debug.Log($"[SecretLearning] {playerId} 贿赂{targetSectName}失败（未被发现），花费{bribePayment}灵石");

            EventBus.Publish(new SecretLearningAttemptEvent
            {
                PlayerId = playerId,
                Method = SecretLearningMethod.Bribe,
                Success = false,
                TargetSect = targetSectName,
                TechniqueId = techniqueId,
                TechniqueName = techniqueName,
                FragmentPercentage = 0f,
            });

            return SecretLearningOutcome.Failed;
        }

        /// <summary>
        /// Roll a random fragment percentage in [MinFragmentPercent, MaxFragmentPercent].
        /// </summary>
        public float RollFragmentPercent()
        {
            return Mathf.Lerp(
                _config.MinFragmentPercent,
                _config.MaxFragmentPercent,
                UnityEngine.Random.value);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 3 — Discovery Escalation
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Handle a discovery event during secret learning.
        /// Escalation chain:
        ///   0 prior warnings  → Warning (recorded, no penalty)
        ///   1 prior warning   → Confinement 72h
        ///   2+ prior warnings → Forced Betrayal (kick from formal sect)
        ///
        /// If the player is currently confined and discovered again,
        /// this immediately triggers forced betrayal (skipping warning).
        /// </summary>
        private void HandleDiscovery(string playerId, string sectName, SecretLearningMethod method)
        {
            var state = EnsurePlayerState(playerId);
            state.WarningCount++;

            if (state.WarningCount == 1)
            {
                // ── First offence: Warning ──
                Debug.Log($"[SecretLearning] ⚠ {playerId} 在{sectName}{GetMethodName(method)}被发现（第{state.WarningCount}次）→ 警告");

                EventBus.Publish(new SecretLearningDiscoveredEvent
                {
                    PlayerId = playerId,
                    Severity = DiscoverySeverity.Warning,
                    TargetSect = sectName,
                    Method = method,
                    OffenceCount = state.WarningCount,
                });
            }
            else if (state.WarningCount == 2 && !state.IsConfinementActive)
            {
                // ── Second offence: Confinement 72h ──
                state.ConfinementEndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    + _config.ConfinementDurationHours * 3600L;
                state.LastConfinementSect = sectName;

                Debug.Log($"[SecretLearning] 🔒 {playerId} 在{sectName}{GetMethodName(method)}被发现（第{state.WarningCount}次）→ 关禁闭{_config.ConfinementDurationHours}h");

                EventBus.Publish(new SecretLearningDiscoveredEvent
                {
                    PlayerId = playerId,
                    Severity = DiscoverySeverity.Confinement,
                    TargetSect = sectName,
                    Method = method,
                    OffenceCount = state.WarningCount,
                });

                EventBus.Publish(new ConfinementStatusEvent
                {
                    PlayerId = playerId,
                    IsActive = true,
                    RemainingHours = _config.ConfinementDurationHours,
                    TargetSect = sectName,
                });
            }
            else
            {
                // ── Third+ offence (or during confinement): Forced Betrayal ──
                string reason = state.IsConfinementActive
                    ? $"关禁闭期间再次在{sectName}{GetMethodName(method)}"
                    : $"多次在{sectName}{GetMethodName(method)}被发现";

                Debug.Log($"[SecretLearning] ⛔ {playerId} 在{sectName}{GetMethodName(method)}被发现（第{state.WarningCount}次）→ 强制叛逃");

                EventBus.Publish(new SecretLearningDiscoveredEvent
                {
                    PlayerId = playerId,
                    Severity = DiscoverySeverity.ForcedBetrayal,
                    TargetSect = sectName,
                    Method = method,
                    OffenceCount = state.WarningCount,
                });

                // If the player is in a formal sect, trigger forced departure
                if (SectManager.Instance != null && SectManager.Instance.IsInFormalSect(playerId))
                {
                    var currentSect = SectManager.Instance.GetCurrentSect(playerId);
                    string previousSectName = currentSect.HasValue
                        ? SectManager.Instance.GetConfig(currentSect.Value).DisplayName
                        : "未知门派";

                    // Note: The actual expulsion logic is handled by the betrayal system
                    // (Story 002). This event signals that betrayal should occur.
                    EventBus.Publish(new ForcedBetrayalEvent
                    {
                        PlayerId = playerId,
                        PreviousSectName = previousSectName,
                        Reason = $"偷学被发现，被{previousSectName}强制驱逐",
                    });

                    // Reset discovery tracking on forced betrayal
                    state.WarningCount = 0;
                    state.ConfinementEndTimestamp = 0;
                }
            }
        }

        /// <summary>
        /// Common guard check before any secret learning attempt.
        /// Confined players fail immediately (with Discovery outcome).
        /// </summary>
        private SecretLearningOutcome PreAttemptGuard(string playerId, string targetSectName,
            out PlayerSecretLearningState state)
        {
            state = EnsurePlayerState(playerId);

            if (state.IsConfinementActive)
            {
                double remaining = GetRemainingConfinementHours(playerId);
                Debug.Log($"[SecretLearning] {playerId} 正在关禁闭中（剩余 {remaining:F1}h），无法在{targetSectName}偷学");
                return SecretLearningOutcome.Failed;
            }

            return SecretLearningOutcome.Success;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 4 — Rogue Cultivator: Tribulation Modifiers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply rogue cultivator tribulation modifiers.
        ///
        /// Rogue cultivators (not in any formal sect) face:
        ///   +20% tribulation failure rate (compensation for lack of sect resources)
        ///   +1 Dao Body quality on successful tribulation (forged through hardship)
        ///
        /// Parameters:
        ///   playerId         — The player undergoing tribulation.
        ///   baseFailureRate  — The original failure rate (0~1).
        ///   daoBodyBonus     — [out] +1 if rogue, 0 if formal sect member.
        ///
        /// Returns the modified failure rate.
        /// </summary>
        public float GetRogueTribulationModifiers(string playerId, float baseFailureRate, out int daoBodyBonus)
        {
            bool isRogue = !SectManager.Instance.IsInFormalSect(playerId);

            if (isRogue)
            {
                daoBodyBonus = 1;
                float modified = Mathf.Clamp01(baseFailureRate + 0.20f);

                EventBus.Publish(new RogueTribulationModifierEvent
                {
                    PlayerId = playerId,
                    ModifiedFailureRate = modified,
                    DaoBodyQualityBonus = 1,
                });

                return modified;
            }

            daoBodyBonus = 0;

            EventBus.Publish(new RogueTribulationModifierEvent
            {
                PlayerId = playerId,
                ModifiedFailureRate = baseFailureRate,
                DaoBodyQualityBonus = 0,
            });

            return baseFailureRate;
        }

        /// <summary>Check whether a player qualifies as a rogue cultivator (散修).</summary>
        public bool IsRogueCultivator(string playerId)
        {
            return SectManager.Instance != null && !SectManager.Instance.IsInFormalSect(playerId);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 5 — Rogue Cultivator Alliance: Bounties (悬赏)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get all bounty definitions configured in the inspector.</summary>
        public RogueBountyDefinition[] GetAllBountyDefinitions()
        {
            return _bountyDefinitions;
        }

        /// <summary>
        /// Get bounties available for a player (not completed, realm sufficient).
        /// </summary>
        public List<RogueBountyDefinition> GetAvailableBounties(string playerId, int playerRealmLevel)
        {
            var state = EnsurePlayerState(playerId);
            var available = new List<RogueBountyDefinition>();

            if (_bountyDefinitions == null) return available;

            for (int i = 0; i < _bountyDefinitions.Length; i++)
            {
                var bounty = _bountyDefinitions[i];
                if (bounty == null) continue;

                bool alreadyCompleted = state.CompletedBountyIds.Contains(bounty.BountyId)
                    && !bounty.IsRepeatable;

                if (!alreadyCompleted && playerRealmLevel >= bounty.RequiredRealmLevel)
                {
                    available.Add(bounty);
                }
            }

            return available;
        }

        /// <summary>Accept a bounty (marks as active in player state).</summary>
        public bool AcceptBounty(string playerId, string bountyId)
        {
            var state = EnsurePlayerState(playerId);

            if (state.CompletedBountyIds.Contains(bountyId))
            {
                Debug.LogWarning($"[SecretLearning] 悬赏 {bountyId} 已完成，不可重复接取");
                return false;
            }

            state.ActiveBountyIds.Add(bountyId);
            Debug.Log($"[SecretLearning] {playerId} 接受悬赏 {bountyId}");
            return true;
        }

        /// <summary>Complete a bounty and claim its rewards.</summary>
        public void CompleteBounty(string playerId, string bountyId)
        {
            var state = EnsurePlayerState(playerId);

            if (state.CompletedBountyIds.Contains(bountyId))
            {
                Debug.LogWarning($"[SecretLearning] 悬赏 {bountyId} 已被领取过奖励");
                return;
            }

            if (_bountyDefinitions == null) return;

            for (int i = 0; i < _bountyDefinitions.Length; i++)
            {
                var bounty = _bountyDefinitions[i];
                if (bounty == null || bounty.BountyId != bountyId) continue;

                state.CompletedBountyIds.Add(bountyId);
                state.ActiveBountyIds.Remove(bountyId);

                Debug.Log($"[SecretLearning] {playerId} 完成悬赏【{bounty.DisplayName}】，"
                    + $"获得 {bounty.RewardSpiritStones} 灵石，{bounty.RewardReputation} 声望");

                EventBus.Publish(new RogueBountyCompletedEvent
                {
                    PlayerId = playerId,
                    BountyId = bountyId,
                    RewardSpiritStones = bounty.RewardSpiritStones,
                    RewardReputation = bounty.RewardReputation,
                });

                return;
            }

            Debug.LogWarning($"[SecretLearning] 未找到悬赏定义: {bountyId}");
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 6 — Rogue Cultivator Alliance: Market (坊市)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get all market item definitions.</summary>
        public AllianceMarketItemDefinition[] GetAllMarketItems()
        {
            return _marketItemDefinitions;
        }

        /// <summary>
        /// Get market items with current stock levels for a specific player.
        /// Returns copies with stock info.
        /// </summary>
        public List<(AllianceMarketItemDefinition def, int currentStock)> GetMarketWithStock(string playerId)
        {
            var state = EnsurePlayerState(playerId);
            var result = new List<(AllianceMarketItemDefinition, int)>();

            if (_marketItemDefinitions == null) return result;

            for (int i = 0; i < _marketItemDefinitions.Length; i++)
            {
                var item = _marketItemDefinitions[i];
                if (item == null) continue;

                int currentStock = item.MaxStock;
                if (currentStock > 0)
                {
                    if (state.MarketStockRemaining.TryGetValue(item.ItemId, out int remaining))
                        currentStock = remaining;
                }

                if (currentStock != 0) // 0 = sold out
                {
                    result.Add((item, currentStock));
                }
            }

            return result;
        }

        /// <summary>Attempt to purchase an item from the alliance market.</summary>
        public bool PurchaseMarketItem(string playerId, string itemId, int playerSpiritStones)
        {
            var state = EnsurePlayerState(playerId);

            if (_marketItemDefinitions == null) return false;

            for (int i = 0; i < _marketItemDefinitions.Length; i++)
            {
                var item = _marketItemDefinitions[i];
                if (item == null || item.ItemId != itemId) continue;

                // ── Stock check ──
                if (item.MaxStock > 0)
                {
                    int remaining = item.MaxStock;
                    if (state.MarketStockRemaining.TryGetValue(itemId, out int stored))
                        remaining = stored;

                    if (remaining <= 0)
                    {
                        Debug.Log($"[SecretLearning] {item.DisplayName} 已售罄");
                        return false;
                    }
                }

                // ── Cost check ──
                if (playerSpiritStones < item.CostSpiritStones)
                {
                    Debug.Log($"[SecretLearning] 灵石不足：需要 {item.CostSpiritStones}，持有 {playerSpiritStones}");
                    return false;
                }

                // ── Deduct stock ──
                int newRemaining = item.MaxStock > 0
                    ? (state.MarketStockRemaining.TryGetValue(itemId, out int s) ? s : item.MaxStock) - 1
                    : -1;

                if (item.MaxStock > 0)
                {
                    state.MarketStockRemaining[itemId] = Math.Max(0, newRemaining);
                }

                Debug.Log($"[SecretLearning] {playerId} 在坊市购买【{item.DisplayName}】，花费 {item.CostSpiritStones} 灵石");

                EventBus.Publish(new AllianceMarketPurchaseEvent
                {
                    PlayerId = playerId,
                    ItemId = itemId,
                    Cost = item.CostSpiritStones,
                    RemainingStock = newRemaining,
                });

                return true;
            }

            Debug.LogWarning($"[SecretLearning] 未找到市场物品定义: {itemId}");
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 7 — Rogue Cultivator Alliance: Intelligence (情报)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get intelligence about a sect for planning secret learning.
        /// Returns intel items including alertness estimates, patrol patterns,
        /// and notable techniques known to be stored there.
        ///
        /// In a full implementation, this would draw from a proper intel system
        /// with player-gathered intelligence quality levels.
        /// </summary>
        public string[] GetSectIntelligence(string sectName)
        {
            // Placeholder: returns generic intel for the target sect.
            // Story 004 or a future intel system would replace this with
            // procedurally generated intelligence based on player reconnaissance.
            return new string[]
            {
                $"【{sectName}】潜入情报",
                "━━━━━━━━━━━━━━━━━",
                "● 藏经阁位置：后山三层塔楼，第三层为核心功法区",
                "● 巡逻频率：每六时辰一轮换（约现实40分钟）",
                "● 警戒度评估：中级（需潜行≥15可安全行动）",
                "● 推荐潜入时段：深夜（子时~寅时，守卫懈怠）",
                "● 高价值目标：第三层上古剑诀残卷、丹方秘录",
                "● 贿赂缺口：外门执事李四——好赌，常缺灵石",
                "━━━━━━━━━━━━━━━━━",
                "提示：携带足够灵石贿赂可降低被发现风险。",
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 8 — Queries and State Management
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Check if a player is currently confined.</summary>
        public bool IsPlayerConfined(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var state)
                && state.IsConfinementActive;
        }

        /// <summary>Get remaining confinement time in hours.</summary>
        public double GetRemainingConfinementHours(string playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out var state)
                || !state.IsConfinementActive)
            {
                return 0;
            }

            double remainingSeconds = state.ConfinementEndTimestamp
                - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Math.Max(0, remainingSeconds / 3600.0);
        }

        /// <summary>Get the player's current warning count.</summary>
        public int GetWarningCount(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var state)
                ? state.WarningCount : 0;
        }

        /// <summary>Get all technique fragments learned by a player.</summary>
        public List<LearnedTechniqueFragment> GetLearnedTechniques(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var state)
                ? state.TechniqueFragments
                : new List<LearnedTechniqueFragment>();
        }

        /// <summary>
        /// Get the total completion progress of a specific technique across all fragments.
        /// Returns 0~1. If multiple fragments of the same technique exist, the highest is used
        /// (you can't stack fragments of the same technique).
        /// </summary>
        public float GetTechniqueProgress(string playerId, string techniqueId)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
                return 0f;

            float highest = 0f;
            for (int i = 0; i < state.TechniqueFragments.Count; i++)
            {
                if (state.TechniqueFragments[i].TechniqueId == techniqueId
                    && state.TechniqueFragments[i].FragmentPercent > highest)
                {
                    highest = state.TechniqueFragments[i].FragmentPercent;
                }
            }
            return highest;
        }

        /// <summary>Get full mutable state for serialization (save system).</summary>
        public PlayerSecretLearningState GetPlayerState(string playerId)
        {
            _playerStates.TryGetValue(playerId, out var state);
            return state;
        }

        /// <summary>Restore a saved player state (e.g., on save load).</summary>
        public void RestorePlayerState(PlayerSecretLearningState state)
        {
            if (state != null)
                _playerStates[state.PlayerId] = state;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SECTION 9 — Administrative
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Manually clear a player's confinement. Primarily for debugging or admin use.
        /// </summary>
        public void ClearConfinement(string playerId)
        {
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                state.ConfinementEndTimestamp = 0;

                EventBus.Publish(new ConfinementStatusEvent
                {
                    PlayerId = playerId,
                    IsActive = false,
                    RemainingHours = 0,
                    TargetSect = state.LastConfinementSect,
                });

                Debug.Log($"[SecretLearning] {playerId} 禁闭已提前解除（管理员操作）");
            }
        }

        /// <summary>Reset all discovery tracking for a player (admin).</summary>
        public void ResetDiscoveryTracking(string playerId)
        {
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                state.WarningCount = 0;
                state.ConfinementEndTimestamp = 0;
                Debug.Log($"[SecretLearning] {playerId} 偷学记录已清除（管理员操作）");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Internal Helpers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get or create a player state entry.</summary>
        private PlayerSecretLearningState EnsurePlayerState(string playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                state = new PlayerSecretLearningState
                {
                    PlayerId = playerId,
                    WarningCount = 0,
                    ConfinementEndTimestamp = 0,
                };
                _playerStates[playerId] = state;
            }
            return state;
        }

        /// <summary>Record a learned technique fragment in player state.</summary>
        private static void RecordLearnedTechnique(
            PlayerSecretLearningState state,
            string techniqueId,
            string techniqueName,
            SecretLearningMethod method,
            string sourceSect,
            float fragmentPercent)
        {
            state.TechniqueFragments.Add(new LearnedTechniqueFragment
            {
                TechniqueId = techniqueId,
                TechniqueName = techniqueName,
                FragmentPercent = fragmentPercent,
                Method = method,
                LearnedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SourceSect = sourceSect,
            });
        }

        /// <summary>Get localized method name for logging.</summary>
        private static string GetMethodName(SecretLearningMethod method) => method switch
        {
            SecretLearningMethod.InfiltrateScriptureHall => "潜入藏经阁",
            SecretLearningMethod.Bribe => "贿赂",
            _ => "未知方式",
        };
    }
}
