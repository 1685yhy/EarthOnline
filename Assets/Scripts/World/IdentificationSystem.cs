using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.World
{
    #region Enums & Data Structures

    /// <summary>药性 — fundamental property of medicinal materials.</summary>
    public enum MedicineProperty
    {
        Unknown,    // 未知 — unidentified
        Cold,       // 寒性
        Hot,        // 热性
        Neutral,    // 平性
        Toxic       // 毒性
    }

    /// <summary>品质 tiers for materials.</summary>
    public enum MaterialQuality
    {
        Unknown,    // 未鉴定
        Mortal,     // 凡品
        Refined,    // 精品
        Mystic,     // 灵品
        Celestial,  // 仙品
        Divine      // 神品
    }

    /// <summary>Methods by which a material can be identified.</summary>
    public enum IdentificationMethod
    {
        Self,   // 自学鉴定 — player attempts based on skill
        NPC,    // NPC鉴定 — pay an NPC for guaranteed result
        Trial   // 试药鉴定 — risk poisoning but free
    }

    /// <summary>Full data for an identification-capable material.</summary>
    [Serializable]
    public class MaterialIdentificationData
    {
        /// <summary>Unique identifier for this material instance.</summary>
        public string Id;

        /// <summary>Display name shown once identified.</summary>
        public string DisplayName;

        /// <summary>Whether the material has been identified.</summary>
        public bool IsIdentified;

        /// <summary>Hidden property revealed on identification.</summary>
        public MedicineProperty Property = MedicineProperty.Unknown;

        /// <summary>Hidden quality revealed on identification.</summary>
        public MaterialQuality Quality = MaterialQuality.Unknown;

        /// <summary>Internal identification difficulty (0.0 ~ 1.0).</summary>
        public float Difficulty;

        /// <summary>配方 hint — name of the pill/elixir this material is used in.</summary>
        public string RecipeHint;

        /// <summary>List of recipe names this material can be used for.</summary>
        public List<string> Recipes = new List<string>();

        /// <summary>Whether this material has been trialed (trial can only be attempted once).</summary>
        public bool HasBeenTrialed;

        /// <summary>Chance of poisoning when trialing (0.0 ~ 1.0). Overrides default if set.</summary>
        public float TrialPoisonChanceOverride = -1f;

        // ─── Display Helpers ───

        /// <summary>Get the display text for material property.</summary>
        public string PropertyDisplay
        {
            get
            {
                if (!IsIdentified) return "未知药性";
                return Property switch
                {
                    MedicineProperty.Cold    => "寒性",
                    MedicineProperty.Hot     => "热性",
                    MedicineProperty.Neutral => "平性",
                    MedicineProperty.Toxic   => "毒性",
                    _                        => "未知药性"
                };
            }
        }

        /// <summary>Get the display text for material quality.</summary>
        public string QualityDisplay
        {
            get
            {
                if (!IsIdentified) return "未知";
                return Quality switch
                {
                    MaterialQuality.Mortal     => "凡品",
                    MaterialQuality.Refined    => "精品",
                    MaterialQuality.Mystic     => "灵品",
                    MaterialQuality.Celestial  => "仙品",
                    MaterialQuality.Divine     => "神品",
                    _                          => "未知"
                };
            }
        }

        /// <summary>Get the full material tooltip text (identified or unidentified).</summary>
        public string GetTooltip()
        {
            if (!IsIdentified)
            {
                return $"??? — 未知灵材\n药性: 未知\n品质: 未知\n需鉴定后方可查看属性。";
            }

            string recipeStr = Recipes != null && Recipes.Count > 0
                ? "可用配方: " + string.Join(", ", Recipes)
                : "暂无已知配方";

            return $"{DisplayName}\n" +
                   $"药性: {PropertyDisplay}\n" +
                   $"品质: {QualityDisplay}\n" +
                   $"{recipeStr}";
        }
    }

    #endregion

    #region Identification Events

    /// <summary>Published when player begins identifying a material.</summary>
    public struct IdentificationStartedEvent
    {
        public string MaterialId;
        public string MaterialName;
        public IdentificationMethod Method;
        public float EstimatedSuccessRate;
    }

    /// <summary>Published when identification completes successfully.</summary>
    public struct IdentificationCompletedEvent
    {
        public string MaterialId;
        public string MaterialName;
        public IdentificationMethod Method;
        public MedicineProperty Property;
        public MaterialQuality Quality;
    }

    /// <summary>Published when self-identification fails.</summary>
    public struct IdentificationFailedEvent
    {
        public string MaterialId;
        public string MaterialName;
        public IdentificationMethod Method;
        public string FailReason;
    }

    /// <summary>Published when trial identification results in poison/damage.</summary>
    public struct TrialIdentificationResultEvent
    {
        public string MaterialId;
        public string MaterialName;
        public bool IsPoisoned;
        public int DamageAmount;
        public string ResultDescription;
    }

    /// <summary>Published when NPC identification leaks the material info to others.</summary>
    public struct IdentificationLeakedEvent
    {
        public string MaterialId;
        public string MaterialName;
        public MedicineProperty Property;
        public MaterialQuality Quality;
        public string LeakedTo;
    }

    /// <summary>Published when a material's identification state changes.</summary>
    public struct MaterialIdentifiedEvent
    {
        public string MaterialId;
        public string DisplayName;
        public MedicineProperty Property;
        public MaterialQuality Quality;
    }

    #endregion

    /// <summary>
    /// 丹药炼器 — Story 002: 灵材鉴定系统 (Identification System).
    /// Handles self-identification, NPC identification, and trial-based identification
    /// for medicinal materials in the alchemy-crafting pipeline.
    /// </summary>
    public class IdentificationSystem : MonoBehaviour
    {
        #region Singleton

        public static IdentificationSystem Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Self-Identification Formula")]
        [SerializeField] private float baseSuccessRate = 0.5f;        // 50% base
        [SerializeField] private float successPerLevel = 0.003f;      // +0.3% per level
        [SerializeField] private float maxSelfIdentifyChance = 0.95f; // hard cap

        [Header("NPC Identification")]
        [SerializeField] private int npcCostSpiritStones = 50;
        [SerializeField] private float npcLeakChance = 0.2f;          // 20%

        [Header("Trial Identification")]
        [SerializeField] private float baseTrialPoisonChance = 0.3f;  // 30% base poison chance
        [SerializeField] private int trialPoisonDamage = 15;
        [SerializeField] private float trialDestroyChance = 0.1f;     // 10% chance to destroy material

        [Header("Experience & Level")]
        [SerializeField] private float identifyExpGain = 10f;
        [SerializeField] private float failedExpGain = 2f;

        #endregion

        #region Private State

        // All identification-capable materials tracked by this system.
        private Dictionary<string, MaterialIdentificationData> _materials = new Dictionary<string, MaterialIdentificationData>();

        // Player's identification skill level.
        private int _identifyLevel = 1;
        private float _currentExp;
        private float _expToNext = 100f;

        // Tool bonus from equipped identification tools.
        private float _toolBonus;

        // Player identifier (to be connected to player system).
        private string _playerId = "Player";

        #endregion

        #region Public Properties

        /// <summary>Player's current identification level.</summary>
        public int IdentifyLevel
        {
            get => _identifyLevel;
            set => _identifyLevel = Mathf.Max(1, value);
        }

        /// <summary>Current tool bonus applied to identification success.</summary>
        public float ToolBonus
        {
            get => _toolBonus;
            set => _toolBonus = Mathf.Max(0f, value);
        }

        /// <summary>Player ID for event publishing.</summary>
        public string PlayerId
        {
            get => _playerId;
            set => _playerId = value;
        }

        /// <summary>Experience progress as 0-1 value.</summary>
        public float ExpProgress => _currentExp / _expToNext;

        public IReadOnlyDictionary<string, MaterialIdentificationData> Materials => _materials;

        /// <summary>NPC identification cost in spirit stones.</summary>
        public int NpcCost => npcCostSpiritStones;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Material Registration

        /// <summary>
        /// Register a new unidentified material with the system.
        /// Returns the material ID.
        /// </summary>
        public string RegisterMaterial(MaterialIdentificationData data)
        {
            if (string.IsNullOrEmpty(data.Id))
            {
                data.Id = $"mat_{Guid.NewGuid():N}";
            }

            data.IsIdentified = false;
            _materials[data.Id] = data;
            return data.Id;
        }

        /// <summary>
        /// Create and register a new unidentified material with basic parameters.
        /// </summary>
        public string CreateMaterial(string displayName, MedicineProperty property, MaterialQuality quality,
                                     float difficulty, List<string> recipes = null)
        {
            var data = new MaterialIdentificationData
            {
                DisplayName = displayName,
                Property = property,
                Quality = quality,
                Difficulty = Mathf.Clamp01(difficulty),
                IsIdentified = "false",
                Recipes = recipes ?? new List<string>(),
                HasBeenTrialed = false
            };
            return RegisterMaterial(data);
        }

        /// <summary>
        /// Get identification data for a material by ID.
        /// Returns null if not found.
        /// </summary>
        public MaterialIdentificationData GetMaterial(string materialId)
        {
            _materials.TryGetValue(materialId, out MaterialIdentificationData data);
            return data;
        }

        /// <summary>
        /// Check if a material is identified.
        /// </summary>
        public bool IsIdentified(string materialId)
        {
            return _materials.TryGetValue(materialId, out MaterialIdentificationData data) && data.IsIdentified;
        }

        #endregion

        #region Self Identification

        /// <summary>
        /// Attempt to identify a material through self-study (自学鉴定).
        /// Success rate: 50% + level * 0.3% + tool bonus - difficulty.
        /// On success: material is identified and full attributes are revealed.
        /// On failure: nothing happens, but the attempt costs a small amount of spiritual energy.
        /// </summary>
        public bool TrySelfIdentify(string materialId)
        {
            if (!_materials.TryGetValue(materialId, out MaterialIdentificationData data))
            {
                Debug.LogWarning($"[IdentificationSystem] Material {materialId} not found.");
                return false;
            }

            if (data.IsIdentified)
            {
                Debug.Log($"[IdentificationSystem] Material {data.DisplayName} is already identified.");
                return false;
            }

            // Calculate success chance.
            float successChance = baseSuccessRate
                                  + _identifyLevel * successPerLevel
                                  + _toolBonus
                                  - data.Difficulty;

            successChance = Mathf.Clamp(successChance, 0f, maxSelfIdentifyChance);

            // Publish started event.
            EventBus.Publish(new IdentificationStartedEvent
            {
                MaterialId = materialId,
                MaterialName = data.DisplayName ?? "???",
                Method = IdentificationMethod.Self,
                EstimatedSuccessRate = successChance
            });

            Debug.Log($"[IdentificationSystem] Self-identifying {data.DisplayName ?? "???"}. " +
                      $"Chance: {successChance * 100:F1}% (Lv.{_identifyLevel}, Tool: +{_toolBonus * 100:F0}%, Diff: {data.Difficulty})");

            // Roll for success.
            bool success = UnityEngine.Random.value < successChance;

            if (success)
            {
                CompleteIdentification(data, IdentificationMethod.Self);
            }
            else
            {
                // Failed — still gain a little experience.
                AddExperience(failedExpGain);

                EventBus.Publish(new IdentificationFailedEvent
                {
                    MaterialId = materialId,
                    MaterialName = data.DisplayName ?? "???",
                    Method = IdentificationMethod.Self,
                    FailReason = "鉴定失败，灵力不足或经验不够"
                });

                Debug.Log($"[IdentificationSystem] Self-identification of {data.DisplayName ?? "???"} failed.");
            }

            return success;
        }

        /// <summary>
        /// Calculate the current self-identification success rate for a material.
        /// Useful for UI display before the player commits.
        /// </summary>
        public float CalculateSelfIdentifyChance(string materialId)
        {
            if (!_materials.TryGetValue(materialId, out MaterialIdentificationData data))
                return 0f;

            if (data.IsIdentified)
                return 1f;

            float chance = baseSuccessRate
                           + _identifyLevel * successPerLevel
                           + _toolBonus
                           - data.Difficulty;

            return Mathf.Clamp(chance, 0f, maxSelfIdentifyChance);
        }

        #endregion

        #region NPC Identification

        /// <summary>
        /// Attempt to identify a material through an NPC鉴定.
        /// 100% success rate, costs 50 spirit stones, 20% leak chance.
        /// Returns true if identification succeeds (always) and costs are deducted.
        /// </summary>
        /// <param name="materialId">The material to identify.</param>
        /// <param name="playerSpiritStones">Reference to player's spirit stone count (deducted on success).</param>
        /// <param name="leakedTo">Optional — who the info was leaked to, if leakage occurs.</param>
        /// <returns>True if identification was performed and paid for.</returns>
        public bool TryNpcIdentify(string materialId, ref int playerSpiritStones, string leakedTo = "坊市")
        {
            if (!_materials.TryGetValue(materialId, out MaterialIdentificationData data))
            {
                Debug.LogWarning($"[IdentificationSystem] Material {materialId} not found.");
                return false;
            }

            if (data.IsIdentified)
            {
                Debug.Log($"[IdentificationSystem] Material {data.DisplayName} is already identified.");
                return false;
            }

            // Check if player can afford.
            if (playerSpiritStones < npcCostSpiritStones)
            {
                EventBus.Publish(new IdentificationFailedEvent
                {
                    MaterialId = materialId,
                    MaterialName = data.DisplayName ?? "???",
                    Method = IdentificationMethod.NPC,
                    FailReason = $"灵石不足，需要{npcCostSpiritStones}灵石"
                });

                Debug.Log($"[IdentificationSystem] NPC identification failed: insufficient spirit stones " +
                          $"({playerSpiritStones}/{npcCostSpiritStones}).");
                return false;
            }

            // Deduct cost.
            playerSpiritStones -= npcCostSpiritStones;

            // Publish started event.
            EventBus.Publish(new IdentificationStartedEvent
            {
                MaterialId = materialId,
                MaterialName = data.DisplayName ?? "???",
                Method = IdentificationMethod.NPC,
                EstimatedSuccessRate = 1f
            });

            // 100% success.
            CompleteIdentification(data, IdentificationMethod.NPC);

            Debug.Log($"[IdentificationSystem] NPC identified {data.DisplayName}. Cost: {npcCostSpiritStones} 灵石.");

            // Check for information leak (20% chance).
            if (UnityEngine.Random.value < npcLeakChance)
            {
                EventBus.Publish(new IdentificationLeakedEvent
                {
                    MaterialId = materialId,
                    MaterialName = data.DisplayName,
                    Property = data.Property,
                    Quality = data.Quality,
                    LeakedTo = leakedTo
                });

                Debug.Log($"[IdentificationSystem] !! 情报泄露: {data.DisplayName} 的信息被泄露到了{leakedTo} !!");
            }

            return true;
        }

        #endregion

        #region Trial Identification

        /// <summary>
        /// Attempt to identify a material by trial (试药鉴定).
        /// If the material is toxic or the roll fails, the player takes poison damage.
        /// There is also a chance the material is destroyed.
        /// Can only be attempted once per material.
        /// </summary>
        /// <param name="materialId">The material to trial.</param>
        /// <param name="playerHP">Reference to player's HP (damage deducted on poison).</param>
        /// <returns>True if the material was successfully identified.</returns>
        public bool TryTrialIdentify(string materialId, ref int playerHP)
        {
            if (!_materials.TryGetValue(materialId, out MaterialIdentificationData data))
            {
                Debug.LogWarning($"[IdentificationSystem] Material {materialId} not found.");
                return false;
            }

            if (data.IsIdentified)
            {
                Debug.Log($"[IdentificationSystem] Material {data.DisplayName} is already identified.");
                return false;
            }

            if (data.HasBeenTrialed)
            {
                Debug.LogWarning($"[IdentificationSystem] Material {data.DisplayName} has already been trialed.");
                return false;
            }

            data.HasBeenTrialed = true;

            // Determine poison chance.
            float poisonChance = data.TrialPoisonChanceOverride >= 0f
                ? data.TrialPoisonChanceOverride
                : baseTrialPoisonChance;

            // If the material is inherently toxic, increase poison chance.
            if (data.Property == MedicineProperty.Toxic)
            {
                poisonChance = Mathf.Clamp01(poisonChance + 0.4f);
            }

            // Publish started event.
            EventBus.Publish(new IdentificationStartedEvent
            {
                MaterialId = materialId,
                MaterialName = data.DisplayName ?? "???",
                Method = IdentificationMethod.Trial,
                EstimatedSuccessRate = 1f - poisonChance
            });

            bool isPoisoned = UnityEngine.Random.value < poisonChance;
            bool isDestroyed = false;

            if (isPoisoned)
            {
                // Apply poison damage.
                int damage = Mathf.RoundToInt(trialPoisonDamage * (1f + data.Difficulty));
                playerHP -= damage;

                // Chance to destroy the material.
                isDestroyed = UnityEngine.Random.value < trialDestroyChance;

                string resultDesc = isDestroyed
                    ? $"试药中毒! 损失{damage}气血，灵材被焚毁!"
                    : $"试药中毒! 损失{damage}气血，但灵材得以保留。";

                EventBus.Publish(new TrialIdentificationResultEvent
                {
                    MaterialId = materialId,
                    MaterialName = data.DisplayName ?? "???",
                    IsPoisoned = "true",
                    DamageAmount = damage,
                    ResultDescription = resultDesc
                });

                Debug.Log($"[IdentificationSystem] Trial: {resultDesc}");

                if (isDestroyed)
                {
                    // Material is destroyed — remove from registry.
                    _materials.Remove(materialId);

                    EventBus.Publish(new IdentificationFailedEvent
                    {
                        MaterialId = materialId,
                        MaterialName = data.DisplayName ?? "???",
                        Method = IdentificationMethod.Trial,
                        FailReason = $"试药失败，{data.DisplayName}被焚毁"
                    });

                    return false;
                }

                // Even when poisoned, the player learns the properties.
                CompleteIdentification(data, IdentificationMethod.Trial);

                // Gain extra experience for surviving the trial.
                AddExperience(identifyExpGain * 1.5f);

                return true;
            }
            else
            {
                // No poison — identification succeeds cleanly.
                string resultDesc = $"试药成功! {data.DisplayName}的药性被确认。";

                EventBus.Publish(new TrialIdentificationResultEvent
                {
                    MaterialId = materialId,
                    MaterialName = data.DisplayName ?? "???",
                    IsPoisoned = "false",
                    DamageAmount = "0",
                    ResultDescription = resultDesc
                });

                CompleteIdentification(data, IdentificationMethod.Trial);

                Debug.Log($"[IdentificationSystem] Trial: {resultDesc}");
                return true;
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Mark a material as identified and publish the appropriate events.
        /// </summary>
        private void CompleteIdentification(MaterialIdentificationData data, IdentificationMethod method)
        {
            data.IsIdentified = true;

            // Gain experience.
            AddExperience(identifyExpGain);

            // Publish completion event.
            EventBus.Publish(new IdentificationCompletedEvent
            {
                MaterialId = data.Id,
                MaterialName = data.DisplayName,
                Method = method,
                Property = data.Property,
                Quality = data.Quality
            });

            // Publish material identified event for other systems.
            EventBus.Publish(new MaterialIdentifiedEvent
            {
                MaterialId = data.Id,
                DisplayName = data.DisplayName,
                Property = data.Property,
                Quality = data.Quality
            });

            Debug.Log($"[IdentificationSystem] {method} identification complete: {data.DisplayName} — " +
                      $"{data.PropertyDisplay}, {data.QualityDisplay}");
        }

        /// <summary>
        /// Add identification experience and handle level-up.
        /// </summary>
        private void AddExperience(float amount)
        {
            _currentExp += amount;

            while (_currentExp >= _expToNext && _identifyLevel < 100)
            {
                _currentExp -= _expToNext;
                _identifyLevel++;
                _expToNext = 100f + _identifyLevel * 20f;

                Debug.Log($"[IdentificationSystem] Level up! Identification skill now Lv.{_identifyLevel}");
            }
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>
        /// Get a debug status string for the identification system.
        /// </summary>
        public string GetDebugStatus()
        {
            int identified = 0;
            int total = 0;
            foreach (var kvp in _materials)
            {
                total++;
                if (kvp.Value.IsIdentified) identified++;
            }

            return $"=== IdentificationSystem Status ===\n" +
                   $"Level: {_identifyLevel} (Exp: {_currentExp}/{_expToNext}, {ExpProgress:P1})\n" +
                   $"Tool Bonus: +{_toolBonus * 100:F0}%\n" +
                   $"Materials: {identified}/{total} identified\n" +
                   $"NPC Cost: {npcCostSpiritStones} 灵石, Leak: {npcLeakChance * 100:F0}%";
        }

        /// <summary>
        /// Create a test unidentified material (for debug/editor use).
        /// </summary>
        public string CreateTestMaterial(string displayName, MedicineProperty property = MedicineProperty.Neutral,
                                          MaterialQuality quality = MaterialQuality.Mortal, float difficulty = 0.3f)
        {
            return CreateMaterial(displayName, property, quality, difficulty,
                                  new List<string> { "聚气丹", "培元丹" });
        }

        #endregion
    }
}
