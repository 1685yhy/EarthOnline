using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Core
{
    /// <summary>
    /// Dao Body (道体) types. Determined by the dominant dimension
    /// from Dao Questioning.
    /// </summary>
    public enum DaoBodyType
    {
        Guardian,       // 守成 — +30% defense
        Breaker,        // 破虚 — +30% attack
        Transcendent,   // 超然 — +10% all stats
        Mortal          // 凡人 — can fuse with other body types
    }

    /// <summary>
    /// Serializable player Dao Body data for persistence.
    /// </summary>
    [System.Serializable]
    public struct DaoBodyData
    {
        public DaoBodyType bodyType;
        public int quality;          // 1-5
        public int failureCount;     // consecutive formation failures
        public bool isFormed;        // successfully solidified
    }

    /// <summary>
    /// Manages Dao Body (道体) formation, stat bonuses, and persistence.
    ///
    /// Listens for DaoQuestioningCompletedEvent, determines body type
    /// and quality from the dimension scores, then attempts formation.
    ///
    /// Quality names (1-5):
    ///   1: 凡体 (Mortal Body)
    ///   2: 灵体 (Spirit Body)
    ///   3: 道体 (Dao Body)
    ///   4: 圣体 (Saint Body)
    ///   5: 混沌体 (Chaos Body)
    ///
    /// Success: cultivation advances to TribulationPassed (渡劫成功)
    ///          +200 regional reputation
    ///          world announcement if quality >= 4
    ///
    /// Failure: cultivation reverts to GreatPerfection (大圆满)
    ///          +5% experience (cap 25%)
    ///          4th failure triggers mercy rule (guaranteed success)
    ///
    /// 散修 (scattered cultivator): base quality +1
    /// </summary>
    public class TribulationBody : MonoBehaviour
    {
        [Header("Formation Rates")]
        [SerializeField] private float baseFormationRate = 0.6f;
        [SerializeField] private float qualityPenaltyPerLevel = 0.10f;
        [SerializeField] private float failureRecoveryPerCount = 0.10f;

        [Header("Mercy Rule")]
        [SerializeField] private int mercyFailureCount = 4;

        [Header("Reputation Reward")]
        [SerializeField] private int successReputation = 200;
        [SerializeField] private string reputationRegion = "default";

        [Header("Failure Experience")]
        [SerializeField] private float failureExpPercent = 0.05f;
        [SerializeField] private float failureExpCap = 0.25f;

        [Header("Quality Bonuses")]
        [SerializeField] private int ancientQualityBonus = 1;
        [SerializeField] private int secretQualityBonus = 1;

        // ── State ────────────────────────────────────────────────────────

        private DaoBodyData currentBody;
        private bool isScatteredCultivator;

        // ── Singleton ────────────────────────────────────────────────────

        public static TribulationBody Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DaoQuestioningCompletedEvent>(OnDaoQuestioningCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DaoQuestioningCompletedEvent>(OnDaoQuestioningCompleted);
        }

        // ── Trigger ──────────────────────────────────────────────────────

        private void OnDaoQuestioningCompleted(DaoQuestioningCompletedEvent evt)
        {
            FormDaoBody(evt);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Dao Body Formation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Determine Dao Body type from questioning results, calculate quality,
        /// roll formation, and apply success/failure outcome.
        /// </summary>
        public void FormDaoBody(DaoQuestioningCompletedEvent evt)
        {
            // 1. Determine body type from dominant dimension
            DaoDimension dominant = (DaoDimension)evt.DominantDimension;
            DaoBodyType bodyType = DimensionToBodyType(dominant);

            // 2. Calculate quality (1-5)
            float strength = evt.AlignmentStrength is float f ? f : float.Parse(evt.AlignmentStrength?.ToString() ?? "0");
            int quality = CalculateQuality(strength, bodyType);

            // 3. Calculate and roll formation
            float successRate = CalculateFormationRate(quality);
            float roll = Random.value;
            bool success = roll <= successRate;

            // 4. Mercy rule: Nth consecutive failure guarantees success
            if (!success && currentBody.failureCount >= mercyFailureCount - 1)
            {
                Debug.Log($"[TribulationBody] MERCY RULE: #{currentBody.failureCount + 1} overridden to success.");
                success = true;
            }

            // 5. Apply outcome
            if (success)
            {
                OnFormationSuccess(bodyType, quality);
            }
            else
            {
                OnFormationFailure(bodyType, quality);
            }
        }

        // ── Dimension -> Body Type ───────────────────────────────────────

        /// <summary>
        /// Map a Dao Dimension to its corresponding Dao Body type.
        /// </summary>
        public static DaoBodyType DimensionToBodyType(DaoDimension dimension)
        {
            return dimension switch
            {
                DaoDimension.DaoHeart  => DaoBodyType.Transcendent,  // 道之心 -> 超然
                DaoDimension.PowerView => DaoBodyType.Breaker,       // 力量观 -> 破虚
                DaoDimension.Emotion   => DaoBodyType.Guardian,      // 情绪   -> 守成
                DaoDimension.Obsession => DaoBodyType.Mortal,        // 执念   -> 凡人
                _ => DaoBodyType.Transcendent
            };
        }

        // ── Quality Calculation ──────────────────────────────────────────

        /// <summary>
        /// Calculate Dao Body quality (1-5).
        ///
        /// Base from alignment strength (how clearly one dimension dominates):
        ///   >= 0.5f -> 3
        ///   >= 0.25f -> 2
        ///   else    -> 1
        ///
        /// Bonuses (stack):
        ///   Ancient platform: +1
        ///   Secret platform: +2 (includes Ancient's +1)
        ///   散修: +1
        ///
        /// Capped at 5.
        /// </summary>
        public int CalculateQuality(float alignmentStrength, DaoBodyType bodyType)
        {
            int baseQuality = alignmentStrength >= 0.5f ? 3
                            : alignmentStrength >= 0.25f ? 2
                            : 1;

            // Tribulation quality bonus
            if (TribulationManager.Instance != null)
            {
                baseQuality += TribulationManager.Instance.CurrentQuality switch
                {
                    TribulationQuality.Ancient => ancientQualityBonus,
                    TribulationQuality.Secret  => ancientQualityBonus + secretQualityBonus,
                    _ => 0
                };
            }

            // 散修 bonus
            if (isScatteredCultivator)
            {
                baseQuality++;
            }

            return Mathf.Clamp(baseQuality, 1, 5);
        }

        // ── Formation Success Rate ──────────────────────────────────────

        /// <summary>
        /// Calculate the formation success rate.
        ///
        /// rate = baseRate
        ///        - (quality - 1) * qualityPenalty   (higher quality = harder)
        ///        + failureCount * failureRecovery     (each prior failure helps)
        ///
        /// Clamped to [0, 1].
        /// </summary>
        public float CalculateFormationRate(int quality)
        {
            float rate = baseFormationRate
                       - (quality - 1) * qualityPenaltyPerLevel
                       + currentBody.failureCount * failureRecoveryPerCount;

            return Mathf.Clamp01(rate);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Success
        // ══════════════════════════════════════════════════════════════════

        private void OnFormationSuccess(DaoBodyType bodyType, int quality)
        {
            // Record the formed body
            currentBody = new DaoBodyData
            {
                bodyType = bodyType,
                quality = quality,
                failureCount = 0,
                isFormed = true
            };

            // Advance cultivation: GreatPerfection -> TribulationPassed (大成)
            if (CultivationManager.Instance != null)
            {
                // From GreatPerfection, one AdvanceRealm step goes to TribulationPassed
                // Realm advancement handled by CultivationManager's OnRealmBreakthrough event
                Debug.Log($"[TribulationBody] Tribulation passed. Realm: {CultivationManager.Instance.CurrentRealm}");
            }

            // Regional reputation
            EventBus.Publish(new ReputationGainedEvent
            {
                RegionId = reputationRegion,
                Amount = successReputation,
                Reason = "dao_body_formed"
            });

            // World announcement for quality >= 4 (圣体 or 混沌体)
            if (quality >= 4)
            {
                string msg = $"天道感应！有修士凝聚{GetQualityName(quality)}·{GetBodyTypeName(bodyType)}，震动天地！";

                EventBus.Publish(new WorldAnnouncementEvent
                {
                    Message = msg,
                    Category = "tribulation"
                });

                Debug.Log($"[TribulationBody] WORLD ANNOUNCEMENT: {msg}");
            }

            // Publish formation event
            EventBus.Publish(new DaoBodyFormedEvent
            {
                BodyType = (int)bodyType,
                BodyTypeName = GetBodyTypeName(bodyType),
                Quality = quality,
                QualityName = GetQualityName(quality),
                Success = true,
                FailureCount = currentBody.failureCount
            });

            // End tribulation as success
            if (TribulationManager.Instance != null)
            {
                TribulationManager.Instance.EndTribulation(true);
            }

            Debug.Log($"[TribulationBody] Dao Body FORMED: {GetQualityName(quality)}·{GetBodyTypeName(bodyType)} (q{quality})");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Failure
        // ══════════════════════════════════════════════════════════════════

        private void OnFormationFailure(DaoBodyType bodyType, int quality)
        {
            currentBody.failureCount++;

            // Revert cultivation: TribulationPassed -> GreatPerfection (大圆满)
            if (CultivationManager.Instance != null)
            {
                // Realm setback handled by CultivationManager's OnRealmBreakthrough event via BreakthroughFallbackEvent
                Debug.Log($"[TribulationBody] Tribulation failed. Current realm: {CultivationManager.Instance.CurrentRealm}");
            }

            // Accumulate experience bonus (5% per failure, cap 25%)
            float accumulatedExp = Mathf.Min(currentBody.failureCount * failureExpPercent, failureExpCap);
            Debug.Log($"[TribulationBody] Failure #{currentBody.failureCount}. Exp +{accumulatedExp:P0} (cap {failureExpCap:P0})");

            // Publish formation event
            EventBus.Publish(new DaoBodyFormedEvent
            {
                BodyType = (int)bodyType,
                BodyTypeName = GetBodyTypeName(bodyType),
                Quality = quality,
                QualityName = GetQualityName(quality),
                Success = false,
                FailureCount = currentBody.failureCount
            });

            // End tribulation as failure
            if (TribulationManager.Instance != null)
            {
                TribulationManager.Instance.EndTribulation(false);
            }

            Debug.Log($"[TribulationBody] Dao Body FAILED (#{currentBody.failureCount}). " +
                      $"Next attempt rate: {CalculateFormationRate(quality):P1}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Stat Multipliers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attack multiplier from the current Dao Body.
        /// Breaker: +30%  | Transcendent: +10%  | Others: no bonus.
        /// Each quality level above 1 adds +5% to the bonus magnitude.
        /// </summary>
        public float GetAttackMultiplier()
        {
            if (!currentBody.isFormed) return 1f;
            float scale = 1f + (currentBody.quality - 1) * 0.05f;

            return currentBody.bodyType switch
            {
                DaoBodyType.Breaker      => 1f + 0.30f * scale,
                DaoBodyType.Transcendent => 1f + 0.10f * scale,
                _                        => 1f
            };
        }

        /// <summary>
        /// Defense multiplier from the current Dao Body.
        /// Guardian: +30%  | Transcendent: +10%  | Others: no bonus.
        /// </summary>
        public float GetDefenseMultiplier()
        {
            if (!currentBody.isFormed) return 1f;
            float scale = 1f + (currentBody.quality - 1) * 0.05f;

            return currentBody.bodyType switch
            {
                DaoBodyType.Guardian     => 1f + 0.30f * scale,
                DaoBodyType.Transcendent => 1f + 0.10f * scale,
                _                        => 1f
            };
        }

        /// <summary>
        /// Speed multiplier (only Transcendent grants +10%).
        /// </summary>
        public float GetSpeedMultiplier()
        {
            if (!currentBody.isFormed) return 1f;
            float scale = 1f + (currentBody.quality - 1) * 0.05f;

            return currentBody.bodyType switch
            {
                DaoBodyType.Transcendent => 1f + 0.10f * scale,
                _                        => 1f
            };
        }

        /// <summary>
        /// Whether the current Dao Body can fuse with another body type.
        /// Only Mortal (凡人) body has this property.
        /// </summary>
        public bool CanFuse() => currentBody.isFormed && currentBody.bodyType == DaoBodyType.Mortal;

        // ══════════════════════════════════════════════════════════════════
        //  Display Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Chinese display name for each Dao Body type.</summary>
        public static string GetBodyTypeName(DaoBodyType type)
        {
            return type switch
            {
                DaoBodyType.Guardian     => "守成道体",
                DaoBodyType.Breaker      => "破虚道体",
                DaoBodyType.Transcendent => "超然道体",
                DaoBodyType.Mortal       => "凡人道体",
                _                        => "未知道体"
            };
        }

        /// <summary>Chinese quality name for each level 1-5.</summary>
        public static string GetQualityName(int quality)
        {
            return quality switch
            {
                1 => "凡体",
                2 => "灵体",
                3 => "道体",
                4 => "圣体",
                5 => "混沌体",
                _ => "未知"
            };
        }

        /// <summary>Full display string: "圣体·守成道体"</summary>
        public string GetDisplayString()
        {
            if (!currentBody.isFormed) return "未凝聚道体";
            return $"{GetQualityName(currentBody.quality)}·{GetBodyTypeName(currentBody.bodyType)}";
        }

        // ══════════════════════════════════════════════════════════════════
        //  Save / Load
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the current Dao Body data for external persistence.
        /// Follows the project's existing save-data pattern:
        /// <c>FooSaveData GetSaveData()</c> / <c>LoadSaveData(FooSaveData)</c>.
        /// </summary>
        public DaoBodyData GetSaveData() => currentBody;

        /// <summary>Restore Dao Body data from saved state.</summary>
        public void LoadSaveData(DaoBodyData data) { currentBody = data; }

        // ── Public Properties ─────────────────────────────────────────────

        /// <summary>Mark the player as a scattered cultivator (散修), granting +1 quality.</summary>
        public void SetScatteredCultivator(bool value) => isScatteredCultivator = value;
        public bool IsScatteredCultivator => isScatteredCultivator;

        public DaoBodyData CurrentBody       => currentBody;
        public DaoBodyType BodyType          => currentBody.bodyType;
        public int Quality                   => currentBody.quality;
        public int FailureCount              => currentBody.failureCount;
        public bool IsFormed                 => currentBody.isFormed;
        public int MaxQuality                => 5;
    }
}
