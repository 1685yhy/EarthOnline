using EarthOnline.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthOnline.Core
{
    /// <summary>
    /// Quality tiers for tribulation platforms.
    /// </summary>
    public enum TribulationQuality
    {
        Normal,  // 凡品天劫台
        Ancient, // 古品天劫台
        Secret   // 秘品天劫台
    }

    /// <summary>
    /// Four-dimension readiness scores used for tribulation preparation.
    ///
    /// ReadinessScore = Pill * 0.25 + Equip * 0.30 + Form * 0.20 + Escort * 0.25
    /// </summary>
    [System.Serializable]
    public struct ReadinessScores
    {
        [Range(0f, 1f)] public float pill;   // 丹药 readiness (weight 0.25)
        [Range(0f, 1f)] public float equip;  // 装备 readiness (weight 0.30)
        [Range(0f, 1f)] public float form;   // 阵法 readiness (weight 0.20)
        [Range(0f, 1f)] public float escort; // 护法 readiness (weight 0.25)

        /// <summary>
        /// Weighted total readiness score in [0, 1].
        /// ReadinessScore = Pill * 0.25 + Equip * 0.30 + Form * 0.20 + Escort * 0.25
        /// </summary>
        public readonly float Total =>
            Mathf.Clamp01(pill * 0.25f + equip * 0.30f + form * 0.20f + escort * 0.25f);
    }

    /// <summary>
    /// Central controller for the tribulation (渡劫) system.
    ///
    /// Responsibilities:
    /// - Computes four-dimension readiness scores
    /// - Enforces scattered-cultivator (散修) escort restrictions
    /// - Handles Dao Body quality ↔ failure rate trade-off
    /// - Spawns the barrier dome when tribulation begins
    /// - Publishes events so UI / audio / VFX can react
    /// </summary>
    public class TribulationManager : MonoBehaviour
    {
        public static TribulationManager Instance { get; private set; }

        [Header("Barrier")]
        [SerializeField] private float barrierRadius = 30f;
        [SerializeField] private float barrierMaxDurability = 100f;

        [Header("Tribulation State")]
        [SerializeField] private bool isTribulationActive = false;

        private TribulationQuality currentQuality;
        private GameObject activeBarrier;
        private MeshRenderer barrierRenderer;
        private Material barrierMaterial;
        private float barrierDurability;

        // ── Scattered Cultivator (散修) limits ──────────────────────────
        private const int MAX_ESCORT_COUNT = 3;
        private const float SCATTERED_ESCORT_EFFECT_MULTIPLIER = 0.7f;

        // ── Dao Body (道体) trade-off ───────────────────────────────────
        private const float DAO_BODY_FAILURE_RATE_BONUS = 0.20f;
        private const int DAO_BODY_QUALITY_INCREASE = 1;

        // ── Quality success-rate bonuses ─────────────────────────────────
        private const float ANCIENT_QUALITY_BONUS = 0.10f;
        private const float SECRET_QUALITY_BONUS = 0.20f;

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

        // ══════════════════════════════════════════════════════════════════
        //  Readiness Score Calculation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute the weighted readiness score from the four dimensions.
        /// ReadinessScore = Pill*0.25 + Equip*0.30 + Form*0.20 + Escort*0.25
        /// </summary>
        public float CalculateReadinessScore(ReadinessScores scores) => scores.Total;

        /// <summary>
        /// Build a ReadinessScores struct from individual values (clamped to [0,1]).
        /// </summary>
        public ReadinessScores BuildScores(float pill, float equip, float form, float escort)
        {
            return new ReadinessScores
            {
                pill = Mathf.Clamp01(pill),
                equip = Mathf.Clamp01(equip),
                form = Mathf.Clamp01(form),
                escort = Mathf.Clamp01(escort)
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Scattered Cultivator (散修) Restrictions
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply scattered-cultivator restrictions to escort effectiveness.
        ///
        /// Rules:
        /// - Maximum 3 escorts contribute (effectiveCount).
        /// - If actual escort count > 3, the effective base effect is reduced to 70%.
        /// </summary>
        /// <param name="escortCount">Total number of escorts the player brought.</param>
        /// <param name="baseEscortEffect">The raw escort readiness before restrictions.</param>
        /// <param name="effectiveCount">Output: how many escorts actually count.</param>
        /// <returns>Adjusted escort effect in [0, 1].</returns>
        public float ApplyEscortRestrictions(int escortCount, float baseEscortEffect, out int effectiveCount)
        {
            effectiveCount = Mathf.Min(escortCount, MAX_ESCORT_COUNT);

            float effect = Mathf.Clamp01(baseEscortEffect);
            if (escortCount > MAX_ESCORT_COUNT)
            {
                effect *= SCATTERED_ESCORT_EFFECT_MULTIPLIER;
            }

            return effect;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Dao Body (道体) Quality ↔ Failure Rate Trade-Off
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the Dao Body trade-off: +1 quality ↔ +20% failure rate.
        ///
        /// When the tribulation platform's quality exceeds the player's Dao Body,
        /// the player's Dao Body quality increases by 1 level, but the tribulation
        /// failure rate increases by 20% as a penalty for overreaching.
        /// </summary>
        /// <param name="platformQuality">Quality of the activated platform.</param>
        /// <param name="currentDaoBodyQuality">Player's current Dao Body level (0-based).</param>
        /// <param name="newDaoBodyQuality">Output: new Dao Body level after bonus.</param>
        /// <returns>Additional failure rate penalty to apply (0.20 if triggered, 0 otherwise).</returns>
        public float CalculateDaoBodyBonus(TribulationQuality platformQuality, int currentDaoBodyQuality, out int newDaoBodyQuality)
        {
            int platformLevel = (int)platformQuality; // Normal = 0, Ancient = 1, Secret=2

            if (platformLevel > currentDaoBodyQuality)
            {
                // Player overreaches — Dao Body improves but failure rate rises
                newDaoBodyQuality = currentDaoBodyQuality + DAO_BODY_QUALITY_INCREASE;
                return DAO_BODY_FAILURE_RATE_BONUS;
            }

            newDaoBodyQuality = currentDaoBodyQuality;
            return 0f;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Estimated Success Rate
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the estimated tribulation success rate.
        ///
        /// Base = ReadinessScore.Total
        /// Quality bonus: Ancient +10%, Secret +20%
        /// Then subtract any Dao Body failure penalty.
        /// </summary>
        public float CalculateEstimatedSuccessRate(
            ReadinessScores scores,
            TribulationQuality quality,
            float daoBodyFailurePenalty)
        {
            float baseRate = scores.Total;

            float qualityBonus = quality switch
            {
                TribulationQuality.Ancient => ANCIENT_QUALITY_BONUS,
                TribulationQuality.Secret  => SECRET_QUALITY_BONUS,
                _                          => 0f
            };

            return Mathf.Clamp01(baseRate + qualityBonus - daoBodyFailurePenalty);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Tribulation Lifecycle
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Start the tribulation: validate state, create barrier, publish events.
        ///
        /// Prerequisites:
        /// - No other tribulation in progress.
        /// - Player has reached Great Perfection (CultivationManager).
        /// </summary>
        public void StartTribulation(TribulationQuality quality, ReadinessScores readiness)
        {
            if (isTribulationActive)
            {
                Debug.LogWarning("[Tribulation] A tribulation is already in progress.");
                return;
            }

            // Great Perfection gate
            if (CultivationManager.Instance == null || CultivationManager.Instance.CurrentRealm != CultivationManager.Realm.Tribulation)
            {
                Debug.LogWarning("[Tribulation] Cultivation has not reached Great Perfection. Aborting.");
                return;
            }

            isTribulationActive = true;
            currentQuality = quality;

            // Apply escort restrictions
            // (escort count is passed by the caller based on actual party size)
            // For automatic calculation caller must pass the count separately.

            // Calculate metrics
            float readinessScore = CalculateReadinessScore(readiness);
            float daoBodyPenalty = CalculateDaoBodyBonus(quality, 0, out _);
            float successRate = CalculateEstimatedSuccessRate(readiness, quality, daoBodyPenalty);

            // Create physical barrier
            CreateBarrier();

            // Publish activation event
            EventBus.Publish(new TribulationPlatformActivatedEvent
            {
                PlatformId = "default",
                Quality = quality.ToString(),
                PlayerId = "player"
            });

            // Publish start event
            EventBus.Publish(new TribulationStartedEvent
            {
                Quality = quality.ToString(),
                ReadinessScore = readinessScore,
                EstimatedSuccessRate = successRate,
                BarrierRadius = barrierRadius,
                BarrierMaxDurability = barrierMaxDurability
            });

            Debug.Log($"[Tribulation] STARTED | Quality: {quality} | Readiness: {readinessScore:P1} | Success Rate: {successRate:P1}");
        }

        /// <summary>
        /// End the current tribulation (success or failure).
        /// </summary>
        public void EndTribulation(bool success)
        {
            if (!isTribulationActive) return;

            // Destroy barrier
            if (activeBarrier != null)
            {
                Destroy(activeBarrier);
                activeBarrier = null;
            }

            isTribulationActive = false;

            EventBus.Publish(new TribulationCompletedEvent
            {
                Success = success,
                Quality = currentQuality.ToString(),
                ReadinessScore = 0f // caller can override before ending
            });

            Debug.Log($"[Tribulation] ENDED | Success: {success}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Barrier
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create the golden semi-transparent barrier dome at the player's position.
        /// Barrier has 30m radius and 100 durability.
        /// </summary>
        private void CreateBarrier()
        {
            if (activeBarrier != null) return;

            barrierDurability = barrierMaxDurability;

            activeBarrier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            activeBarrier.name = "TribulationBarrier";
            activeBarrier.transform.localScale = Vector3.one * barrierRadius * 2f;

            // Remove physics collider — barrier is a VFX/blocker zone, not a physics body
            var collider = activeBarrier.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            // Configure material — golden semi-transparent
            barrierRenderer = activeBarrier.GetComponent<MeshRenderer>();

            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                barrierMaterial = new Material(shader);
                barrierMaterial.color = new Color(1f, 0.84f, 0f, 0.25f); // Golden, translucent
                barrierMaterial.SetFloat("_Mode", 2); // Fade rendering mode
                barrierMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                barrierMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                barrierMaterial.SetInt("_ZWrite", 0);
                barrierMaterial.DisableKeyword("_ALPHATEST_ON");
                barrierMaterial.EnableKeyword("_ALPHABLEND_ON");
                barrierMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                barrierMaterial.renderQueue = 3000;
            }
            else
            {
                // Fallback if Standard shader is not available
                barrierMaterial = new Material(Shader.Find("Unlit/Color"));
                if (barrierMaterial != null)
                    barrierMaterial.color = new Color(1f, 0.84f, 0f, 0.3f);
            }

            barrierRenderer.material = barrierMaterial;
            barrierRenderer.shadowCastingMode = ShadowCastingMode.Off;
            barrierRenderer.receiveShadows = false;

            // Position at player (or cultivation manager)
            if (CultivationManager.Instance != null)
                activeBarrier.transform.position = CultivationManager.Instance.transform.position;

            // Tag for gameplay queries
            activeBarrier.tag = "TribulationBarrier";

            EventBus.Publish(new TribulationBarrierCreatedEvent
            {
                Radius = barrierRadius,
                MaxDurability = barrierMaxDurability
            });

            Debug.Log($"[Tribulation] Barrier created: radius={barrierRadius}m, durability={barrierMaxDurability}");
        }

        /// <summary>
        /// Apply damage to the barrier. If durability reaches 0, the tribulation fails.
        /// </summary>
        /// <param name="damage">Amount of damage to deal.</param>
        public void DamageBarrier(float damage)
        {
            if (!isTribulationActive || activeBarrier == null) return;

            barrierDurability = Mathf.Max(0f, barrierDurability - damage);

            // Pulse the barrier alpha to indicate damage
            if (barrierMaterial != null)
            {
                float alpha = 0.15f + (barrierDurability / barrierMaxDurability) * 0.15f;
                Color c = barrierMaterial.color;
                c.a = Mathf.Clamp(alpha, 0.05f, 0.35f);
                barrierMaterial.color = c;
            }

            EventBus.Publish(new TribulationBarrierDamagedEvent
            {
                Damage = damage,
                RemainingDurability = barrierDurability
            });

            if (barrierDurability <= 0f)
            {
                EventBus.Publish(new TribulationBarrierDestroyedEvent
                {
                    TimeSurvived = Time.time
                });
                EndTribulation(false);
            }
        }

        // ── Public Properties ─────────────────────────────────────────────

        public bool IsTribulationActive => isTribulationActive;
        public TribulationQuality CurrentQuality => currentQuality;
        public float BarrierDurability => barrierDurability;
        public float BarrierMaxDurability => barrierMaxDurability;
        public float BarrierRadius => barrierRadius;
        public bool HasBarrier => activeBarrier != null;

        /// <summary>
        /// When true (default), HeartDemonTribulation auto-calls EndTribulation on clear.
        /// Story 003's DaoQuestioning sets this to false to take over the outcome flow.
        /// </summary>
        public bool AutoEndOnHeartClear { get; set; } = true;
    }
}
