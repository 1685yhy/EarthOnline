using System.Collections;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Core
{
    /// <summary>
    /// Thunder Tribulation (雷劫) phase — sequential lightning strikes with
    /// warning zones, splash damage, perfect dodge tracking, and equipment
    /// mitigation.
    ///
    /// Story 002:
    /// - 9 + correction bolts, damage +15%/strike
    /// - 1-second warning circle before each strike
    /// - Splash damage: 30% at center, -10%/m falloff
    /// - 抗雷甲/阵法/护法 correctly reduce damage
    /// - Perfect dodge -> heart demon difficulty -15% + Dao Body +1
    ///
    /// Starts automatically when TribulationStartedEvent fires.
    /// Publishes ThunderTribulationCompletedEvent when done.
    /// </summary>
    public class ThunderTribulation : MonoBehaviour
    {
        [Header("Thunder Configuration")]
        [SerializeField] private int baseStrikeCount = 9;
        [SerializeField] private float timeBetweenStrikes = 2.5f;
        [SerializeField] private float warningDuration = 1f;
        [SerializeField] private float warningRadius = 3f;
        [SerializeField] private float baseBoltDamage = 30f;
        [SerializeField] private float damageEscalationPerStrike = 0.15f;

        [Header("Splash Damage")]
        [SerializeField] private float splashRatioAtCenter = 0.30f;
        [SerializeField] private float splashFalloffPerMeter = 0.10f;
        [SerializeField] private float splashMaxRange = 3.5f;

        [Header("Strike Area")]
        [SerializeField] private float minStrikeRadius = 3f;
        [SerializeField] private float maxStrikeRadius = 25f;

        [Header("Equipment Mitigation Caps")]
        [SerializeField][Range(0f, 1f)] private float antiThunderArmorMitigation = 0.40f;
        [SerializeField][Range(0f, 1f)] private float formationMitigation = 0.25f;
        [SerializeField][Range(0f, 1f)] private float escortMitigation = 0.20f;
        [SerializeField][Range(0f, 1f)] private float totalMitigationCap = 0.70f;

        // State
        private int totalStrikeCount;
        private int currentStrikeIndex;
        private int perfectDodgeCount;
        private int consecutiveDodges;
        private bool isActive;
        private float accumulatedDifficultyModifier;
        private int daoBodyBonusAccumulated;
        private Transform playerTransform;

        // Quality difficulty modifiers (applied to damage)
        private static readonly System.Collections.Generic.Dictionary<string, float> QualityDamageMod = new()
        {
            { "Normal", 1.0f },
            { "Ancient", 0.85f },
            { "Secret", 0.70f }
        };

        // ── Event Subscriptions ──────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<TribulationStartedEvent>(OnTribulationStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TribulationStartedEvent>(OnTribulationStarted);
        }

        private void Start()
        {
            LocatePlayer();
        }

        // ── Player Reference ─────────────────────────────────────────────

        private void LocatePlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else if (CultivationManager.Instance != null)
                playerTransform = CultivationManager.Instance.transform;
        }

        // ── Tribulation Start Handler ────────────────────────────────────

        private void OnTribulationStarted(TribulationStartedEvent evt)
        {
            if (isActive) return;

            LocatePlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[ThunderTribulation] Cannot locate player. Aborting.");
                return;
            }

            // Strike count: 9 base + (1 - readiness) * 6 correction
            float readiness = evt.ReadinessScore is float r ? r : float.Parse(evt.ReadinessScore?.ToString() ?? "0");
            int correction = Mathf.FloorToInt((1f - readiness) * 6f);
            totalStrikeCount = baseStrikeCount + Mathf.Min(correction, 9);

            currentStrikeIndex = 0;
            perfectDodgeCount = 0;
            consecutiveDodges = 0;
            accumulatedDifficultyModifier = 0f;
            daoBodyBonusAccumulated = 0;
            isActive = true;

            string qualityKey = evt.Quality is string s ? s : evt.Quality?.ToString() ?? "Normal";
            float qualityMod = QualityDamageMod.ContainsKey(qualityKey) ? QualityDamageMod[qualityKey] : 1f;

            Debug.Log($"[ThunderTribulation] {totalStrikeCount} strikes. Correction: +{correction}. Quality damage mod: {qualityMod:P0}");

            StartCoroutine(RunThunderSequence(qualityMod));
        }

        // ── Core Sequence ────────────────────────────────────────────────

        private IEnumerator RunThunderSequence(float qualityMod)
        {
            for (int i = 0; i < totalStrikeCount; i++)
            {
                currentStrikeIndex = i + 1;

                float boltDamage = CalculateBoltDamage(i, qualityMod);
                Vector3 strikePos = PickStrikePosition();

                // --- Warning phase ---
                EventBus.Publish(new ThunderStrikeWarningEvent
                {
                    StrikeIndex = currentStrikeIndex,
                    TotalStrikes = totalStrikeCount,
                    CenterPosition = strikePos,
                    WarningRadius = warningRadius,
                    TimeUntilStrike = warningDuration,
                    BaseDamage = boltDamage
                });

                yield return new WaitForSeconds(warningDuration);

                // --- Strike phase ---
                float dist = DistanceOnGround(playerTransform.position, strikePos);
                float splashDamage = 0f;
                float damageToPlayer = 0f;
                bool directHit = false;

                if (dist <= 1.5f)
                {
                    // Direct hit
                    damageToPlayer = boltDamage;
                    directHit = true;
                    consecutiveDodges = 0;
                }
                else if (dist <= splashMaxRange)
                {
                    // Splash
                    splashDamage = CalculateSplashDamage(dist, boltDamage);
                    damageToPlayer = splashDamage;
                    consecutiveDodges = 0;
                }
                else
                {
                    // Perfect dodge
                    perfectDodgeCount++;
                    consecutiveDodges++;
                    accumulatedDifficultyModifier -= 0.15f;
                    daoBodyBonusAccumulated++;
                }

                // Apply damage to player (delegates to health system or barrier fallback)
                if (damageToPlayer > 0f)
                    ApplyDamageToPlayer(damageToPlayer);

                // Publish strike event
                EventBus.Publish(new ThunderStrikeStruckEvent
                {
                    StrikeIndex = currentStrikeIndex,
                    StrikePosition = strikePos,
                    Damage = damageToPlayer,
                    PlayerHit = damageToPlayer > 0f,
                    DistanceFromPlayer = dist,
                    SplashDamage = splashDamage
                });

                // Publish dodge event on perfect dodge
                if (damageToPlayer <= 0f)
                {
                    EventBus.Publish(new ThunderStrikeDodgedEvent
                    {
                        StrikeIndex = currentStrikeIndex,
                        ConsecutiveDodges = consecutiveDodges,
                        TotalPerfectDodges = perfectDodgeCount
                    });

                    Debug.Log($"[ThunderTribulation] Strike #{currentStrikeIndex}: PERFECT DODGE (consecutive: {consecutiveDodges})");
                }
                else
                {
                    string hitType = directHit ? "DIRECT HIT" : "SPLASH HIT";
                    Debug.Log($"[ThunderTribulation] Strike #{currentStrikeIndex}: {hitType} — damage: {damageToPlayer:F1}, distance: {dist:F1}m");
                }

                // Splash damage to barrier (20% of player damage leaks through)
                if (damageToPlayer > 0f && TribulationManager.Instance is { HasBarrier: true })
                {
                    float barrierDmg = damageToPlayer * 0.2f;
                    TribulationManager.Instance.DamageBarrier(barrierDmg);

                    EventBus.Publish(new ThunderSplashDamageEvent
                    {
                        DamageToBarrier = barrierDmg,
                        RemainingBarrierDurability = TribulationManager.Instance.BarrierDurability
                    });
                }

                // Inter-strike delay (shorter with consecutive dodges — flow bonus)
                float cooldown = Mathf.Max(1.2f, timeBetweenStrikes - consecutiveDodges * 0.08f);
                yield return new WaitForSeconds(cooldown);
            }

            // --- Sequence complete ---
            isActive = false;

            EventBus.Publish(new ThunderTribulationCompletedEvent
            {
                TotalStrikes = totalStrikeCount,
                PerfectDodges = perfectDodgeCount,
                DifficultyModifier = accumulatedDifficultyModifier,
                DaoBodyBonus = daoBodyBonusAccumulated
            });

            Debug.Log($"[ThunderTribulation] COMPLETE | {perfectDodgeCount}/{totalStrikeCount} dodged | " +
                      $"Heart demon difficulty mod: {accumulatedDifficultyModifier:P0} | Dao Body +{daoBodyBonusAccumulated}");
        }

        // ── Damage Calculations ──────────────────────────────────────────

        /// <summary>
        /// Bolt damage = base * (1 + strikeIndex * 0.15) * qualityMod, then
        /// reduced by equipment mitigation.
        /// </summary>
        private float CalculateBoltDamage(int strikeIndex, float qualityMod)
        {
            float raw = baseBoltDamage * (1f + strikeIndex * damageEscalationPerStrike) * qualityMod;
            return ApplyEquipmentMitigation(raw);
        }

        /// <summary>
        /// Reduce damage based on items present in readiness dimensions.
        /// 抗雷甲 (armor): up to 40%, 阵法 (formation): up to 25%, 护法 (escort): up to 20%.
        /// </summary>
        private float ApplyEquipmentMitigation(float damage)
        {
            float totalMitigation = 0f;

            // Equipment presence is inferred from TribulationManager readiness
            if (HasEquipmentReduction("armor"))
                totalMitigation += antiThunderArmorMitigation;

            if (HasEquipmentReduction("formation"))
                totalMitigation += formationMitigation;

            if (HasEquipmentReduction("escort"))
                totalMitigation += escortMitigation;

            totalMitigation = Mathf.Min(totalMitigation, totalMitigationCap);
            return damage * (1f - totalMitigation);
        }

        /// <summary>
        /// Placeholder equipment check. Currently maps readiness quality to
        /// equipment presence. Replace with Inventory system hook when available.
        /// </summary>
        private bool HasEquipmentReduction(string slot)
        {
            // TODO: Replace with actual player inventory check
            // Current logic: higher quality platform implies better preparation
            if (TribulationManager.Instance == null) return false;

            return slot switch
            {
                "armor" => TribulationManager.Instance.CurrentQuality >= TribulationQuality.Ancient,
                "formation" => TribulationManager.Instance.CurrentQuality >= TribulationQuality.Secret,
                "escort" => true, // Escorts are always present once tribulation starts
                _ => false
            };
        }

        /// <summary>
        /// Splash damage: 30% at center, -10% per meter away.
        /// </summary>
        private float CalculateSplashDamage(float distance, float boltDamage)
        {
            float ratio = splashRatioAtCenter - (distance - 1.5f) * splashFalloffPerMeter;
            ratio = Mathf.Max(0f, ratio);
            return boltDamage * ratio;
        }

        // ── Positioning Helpers ──────────────────────────────────────────

        private Vector3 PickStrikePosition()
        {
            if (playerTransform == null) return Vector3.zero;

            Vector3 playerPos = playerTransform.position;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(minStrikeRadius, maxStrikeRadius);

            return new Vector3(
                playerPos.x + Mathf.Cos(angle) * dist,
                playerPos.y,
                playerPos.z + Mathf.Sin(angle) * dist
            );
        }

        private static float DistanceOnGround(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(
                new Vector3(a.x, 0f, a.z),
                new Vector3(b.x, 0f, b.z)
            );
        }

        // ── Player Damage ────────────────────────────────────────────────

        /// <summary>
        /// Apply damage to player. Falls back to barrier damage as proxy until
        /// a player Health component exists.
        /// </summary>
        private void ApplyDamageToPlayer(float damage)
        {
            Debug.Log($"[ThunderTribulation] Player takes {damage:F1} thunder damage.");

            // TODO: Replace with PlayerHealth.TakeDamage when available.
            // For now, damage the barrier as a proxy so the tribulation can fail.
            if (TribulationManager.Instance != null)
            {
                TribulationManager.Instance.DamageBarrier(damage * 0.15f);
            }
        }

        // ── Public Accessors ─────────────────────────────────────────────

        public float DifficultyModifier => accumulatedDifficultyModifier;
        public int DaoBodyBonus => daoBodyBonusAccumulated;
        public bool IsActive => isActive;
        public int PerfectDodgeCount => perfectDodgeCount;
        public int TotalStrikeCount => totalStrikeCount;
        public int CurrentStrikeIndex => currentStrikeIndex;
        public int ConsecutiveDodges => consecutiveDodges;
    }
}
