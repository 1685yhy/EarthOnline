using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Core
{
    /// <summary>
    /// Represents a Tribulation Platform (天劫台) in the game world.
    ///
    /// Handles:
    /// - Platform quality tiers (Normal / Ancient / Secret)
    /// - Player proximity detection and Great Perfection gate
    /// - Confirmation panel data assembly (readiness, estimated success rate)
    /// - Tribulation initiation
    /// </summary>
    public class TribulationPlatform : MonoBehaviour
    {
        [Header("Platform Config")]
        [SerializeField] private TribulationQuality quality = TribulationQuality.Normal;
        [SerializeField] private string platformId = "default";
        [SerializeField] private float interactionRadius = 2f;

        [Header("Visual Feedback")]
        [SerializeField] private Color normalColor = new Color(0.6f, 0.6f, 0.8f, 1f);    // Blue-silver
        [SerializeField] private Color ancientColor = new Color(0.8f, 0.6f, 0.2f, 1f);   // Gold
        [SerializeField] private Color secretColor = new Color(0.6f, 0.2f, 0.8f, 1f);    // Purple

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private bool isPlayerInRange = false;
        private bool canInteract = false;

        // Current readiness — set by caller before confirmation
        private ReadinessScores currentReadiness;
        private int currentEscortCount;
        private int currentDaoBodyQuality;

        // ══════════════════════════════════════════════════════════════════
        //  Unity Lifecycle
        // ══════════════════════════════════════════════════════════════════

private void Start()
        {
            ApplyPlatformVisuals();

            // Ensure trigger collider exists and matches interaction radius
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = gameObject.AddComponent<SphereCollider>();
            }
            sc.isTrigger = true;
            sc.radius = interactionRadius;
        }

        private void Update()
        {
            CheckInteractionState();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInRange = true;
                if (canInteract)
                {
                    ShowConfirmationPrompt();
                }
                else if (showDebugLogs)
                {
                    Debug.Log($"[TribulationPlatform:{platformId}] Player in range but cannot interact. Great Perfection required.");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInRange = false;
                HideConfirmationPrompt();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Interaction State
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Check whether the player can interact with this platform.
        ///
        /// Conditions:
        /// 1. Player is in trigger range.
        /// 2. Player has reached Great Perfection (CultivationManager).
        /// 3. No tribulation is currently active.
        /// </summary>
        private void CheckInteractionState()
        {
            bool hasPerfection = CultivationManager.Instance != null &&
                                  CultivationManager.Instance.CurrentRealm == CultivationManager.Realm.Tribulation;

            bool noActiveTribulation = TribulationManager.Instance == null ||
                                        !TribulationManager.Instance.IsTribulationActive;

            bool newState = isPlayerInRange && hasPerfection && noActiveTribulation;

            if (newState != canInteract)
            {
                canInteract = newState;
                if (canInteract && isPlayerInRange)
                {
                    ShowConfirmationPrompt();
                }
                else if (!canInteract && isPlayerInRange)
                {
                    HideConfirmationPrompt();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Confirmation Panel
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the tribulation confirmation panel with readiness assessment.
        /// Called when the player can interact with the platform.
        /// </summary>
        private void ShowConfirmationPrompt()
        {
            if (TribulationManager.Instance == null) return;

            // Build default readiness from current stats
            ReadinessScores scores = TribulationManager.Instance.BuildScores(0f, 0f, 0f, 0f);
            float daoBodyPenalty = TribulationManager.Instance.CalculateDaoBodyBonus(quality, currentDaoBodyQuality, out _);
            float successRate = TribulationManager.Instance.CalculateEstimatedSuccessRate(scores, quality, daoBodyPenalty);

            EventBus.Publish(new TribulationConfirmationEvent
            {
                Show = "true",
                PlatformId = platformId,
                Quality = quality.ToString(),
                ReadinessScore = scores.Total.ToString("F2"),
                EstimatedSuccessRate = successRate.ToString("F2")
            });

            if (showDebugLogs)
            {
                Debug.Log($"[TribulationPlatform:{platformId}] Confirmation shown | Quality: {quality}");
            }
        }

        /// <summary>
        /// Hide the tribulation confirmation panel.
        /// </summary>
        private void HideConfirmationPrompt()
        {
            EventBus.Publish(new TribulationConfirmationEvent
            {
                Show = "false",
                PlatformId = platformId,
                Quality = quality.ToString(),
                ReadinessScore = "0.00",
                EstimatedSuccessRate = "0.00"
            });
        }

        // ══════════════════════════════════════════════════════════════════
        //  Tribulation Initiation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by the confirmation panel UI or gameplay code to start the tribulation.
        ///
        /// Flow:
        /// 1. Apply scattered-cultivator escort restrictions.
        /// 2. Rebuild readiness scores with restricted escort effect.
        /// 3. Delegate to TribulationManager.StartTribulation.
        /// </summary>
        /// <param name="rawPill">Raw pill readiness (before restrictions).</param>
        /// <param name="rawEquip">Raw equipment readiness.</param>
        /// <param name="rawForm">Raw formation readiness.</param>
        /// <param name="rawEscort">Raw escort readiness (before restrictions).</param>
        /// <param name="escortCount">Total number of escort NPCs.</param>
        /// <param name="daoBodyQuality">Player's current Dao Body level.</param>
        public void ConfirmTribulation(
            float rawPill,
            float rawEquip,
            float rawForm,
            float rawEscort,
            int escortCount,
            int daoBodyQuality)
        {
            if (!canInteract)
            {
                Debug.LogWarning("[TribulationPlatform] Cannot confirm — interaction not allowed.");
                return;
            }

            if (TribulationManager.Instance == null)
            {
                Debug.LogError("[TribulationPlatform] TribulationManager not found.");
                return;
            }

            // Step 1: Apply escort restrictions
            float restrictedEscort = TribulationManager.Instance.ApplyEscortRestrictions(
                escortCount, rawEscort, out int effectiveCount);

            // Step 2: Build final readiness
            ReadinessScores finalScores = TribulationManager.Instance.BuildScores(
                rawPill, rawEquip, rawForm, restrictedEscort);

            // Step 3: Apply Dao Body bonus
            float daoBodyPenalty = TribulationManager.Instance.CalculateDaoBodyBonus(
                quality, daoBodyQuality, out int newDaoBodyQuality);

            if (daoBodyPenalty > 0f && showDebugLogs)
            {
                Debug.Log($"[TribulationPlatform] Dao Body upgraded from Lv.{daoBodyQuality} → Lv.{newDaoBodyQuality}. " +
                          $"Failure rate +{daoBodyPenalty:P0}.");
            }

            // Step 4: Start tribulation
            TribulationManager.Instance.StartTribulation(quality, finalScores);

            if (showDebugLogs)
            {
                float finalScore = finalScores.Total;
                float successRate = TribulationManager.Instance.CalculateEstimatedSuccessRate(
                    finalScores, quality, daoBodyPenalty);
                Debug.Log($"[TribulationPlatform] Confirmed. " +
                          $"Readiness: {finalScore:P1} | Success Rate: {successRate:P1} | " +
                          $"Effective Escorts: {effectiveCount}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Platform Quality
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Change this platform's quality tier and update visuals.
        /// </summary>
        public void SetQuality(TribulationQuality newQuality)
        {
            TribulationQuality oldQuality = quality;
            quality = newQuality;
            ApplyPlatformVisuals();

            EventBus.Publish(new TribulationPlatformQualityChangedEvent
            {
                PlatformId = platformId,
                OldQuality = oldQuality.ToString(),
                NewQuality = newQuality.ToString()
            });
        }

        /// <summary>
        /// Apply visual appearance based on platform quality.
        /// Override this method to use custom models/materials.
        /// </summary>
        private void ApplyPlatformVisuals()
        {
            Color platformColor = quality switch
            {
                TribulationQuality.Normal  => normalColor,
                TribulationQuality.Ancient => ancientColor,
                TribulationQuality.Secret  => secretColor,
                _                          => normalColor
            };

            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = platformColor;
            }
        }

        // ── Public Properties ─────────────────────────────────────────────

        public TribulationQuality Quality => quality;
        public string PlatformId => platformId;
        public bool CanInteract => canInteract;
        public bool IsPlayerInRange => isPlayerInRange;
    }
}
