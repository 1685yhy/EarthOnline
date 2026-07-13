using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Core
{
    // ─── Enums ──────────────────────────────────────────────────────────────

    /// <summary>三档火候</summary>
    public enum HeatLevel
    {
        High,   // 大火 — +8°C/s
        Medium, // 中火 — +2°C/s
        Low     // 小火 — -1°C/s
    }

    /// <summary>炼丹阶段状态机</summary>
    public enum AlchemyStage
    {
        Idle,          // 未开始
        Boiling,       // 沸腾期
        Fusion,        // 融合期
        Purification,  // 提纯期
        Finishing,     // 收丹期
        Complete,      // 成丹
        Exploded       // 炸炉
    }

    /// <summary>丹药品质四级</summary>
    public enum PillQuality
    {
        Fail,      // 失败（炸炉或废渣）
        Low,       // 下品 (白)
        Mid,       // 中品 (绿)
        High,      // 上品 (蓝)
        Legendary  // 极品 (紫)
    }

    /// <summary>变异类型</summary>
    public enum MutationType
    {
        None,
        Normal,     // 普通变异 — 产出不同物品
        Recipe,     // 配方变异 — 自创配方
        Fusion,     // 融合变异 — 混沌型物品
        Dangerous   // 危险变异 — 强力副作用
    }

    // ─── Data Structures ──────────────────────────────────────────────────

    /// <summary>炼丹配方数据</summary>
    [Serializable]
    public struct AlchemyRecipeData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public float OptimalTemperature;      // 推荐炼制温度 (°C)
        public float Duration;                // 总炼制时长 (秒)
        public float BaseQualityMin;          // 基底品质最低 (0.3~1.0)
        public float BaseQualityMax;          // 基底品质最高
        public string[] RecommendedOrder;     // 推荐投料顺序 (material IDs)
        public int Difficulty;                // 炼制难度 1-10
        public int RequiredProficiency;       // 要求最低熟练度
    }

    /// <summary>投入的材料数据</summary>
    [Serializable]
    public struct AlchemyMaterialInput
    {
        public string ItemId;
        public string DisplayName;
        public float QualityCoefficient;      // 品质系数: 普通0.7, 良好0.85, 优质1.0, 完美1.2
        public int InputOrderIndex;           // 投入时的顺序索引 (0-based)
    }

    /// <summary>丹炉数据</summary>
    [Serializable]
    public struct CauldronData
    {
        public string Id;
        public string DisplayName;
        public float QualityCoefficient;      // 品质系数: 新手0.8, 精良1.0, 传说1.2
        public float MaxDurability;           // 最大耐久
        public float CurrentDurability;       // 当前耐久

        /// <summary>磨损系数: 0=全新, 1=报废</summary>
        public float WearFactor => 1f - (CurrentDurability / MaxDurability);
    }

    /// <summary>炼制产出结果</summary>
    [Serializable]
    public struct AlchemyResult
    {
        public float FinalQuality;
        public PillQuality Quality;
        public string PillId;
        public string PillName;
        public bool IsMutation;
        public MutationType MutationType;
        public Color QualityColor;
    }

    /// <summary>炼制熟练度</summary>
    [Serializable]
    public class AlchemyProficiency
    {
        public int Level = 1;
        public float CurrentExp;
        public float ExpToNext = 100f;

        /// <summary>控火精度提升</summary>
        public float TemperatureToleranceBonus => Level * 0.001f;

        /// <summary>极品概率加成</summary>
        public float LegendaryBonus => Level >= 71 ? (Level - 70) * 0.003f : 0f;

        /// <summary>炸炉概率减免</summary>
        public float ExplosionReduction => Level >= 71 ? (Level - 70) * 0.001f : 0f;

        /// <summary>品质系数加成 (1~100级)</summary>
        public float QualityBonus => 1.0f + (Level - 1) * 0.002f;

        public void AddExp(float amount)
        {
            CurrentExp += amount;
            while (CurrentExp >= ExpToNext && Level < 100)
            {
                CurrentExp -= ExpToNext;
                Level++;
                ExpToNext = 100f + Level * 20f;
                OnLevelUp?.Invoke(Level);
            }
        }

        public string GetTitle()
        {
            if (Level <= 10)   return "炼丹学徒";
            if (Level <= 25)   return "炼丹工匠";
            if (Level <= 45)   return "炼丹大师";
            if (Level <= 70)   return "炼丹宗师";
            if (Level <= 90)   return "圣手丹师";
            return "传说丹仙";
        }

        public System.Action<int> OnLevelUp;
    }

    // ─── Alchemy Event Structs ─────────────────────────────────────────────

    /// <summary>Published when alchemy begins.</summary>
    public struct AlchemyStartedEvent
    {
        public string RecipeId;
        public string RecipeName;
        public float TotalDuration;
        public float OptimalTemperature;
        public AlchemyStage InitialStage;
    }

    /// <summary>Published when player switches heat level.</summary>
    public struct HeatSwitchedEvent
    {
        public HeatLevel NewLevel;
        public HeatLevel PreviousLevel;
        public float CooldownRemaining;
        public bool OnCooldown;
    }

    /// <summary>Published each frame for temperature UI updates.</summary>
    public struct TemperatureChangedEvent
    {
        public float CurrentTemperature;
        public float OptimalTemperature;
        public float TemperatureDeviation;
        public float AvgTemperature;
    }

    /// <summary>Published when alchemy stage transitions.</summary>
    public struct AlchemyStageChangedEvent
    {
        public AlchemyStage NewStage;
        public AlchemyStage PreviousStage;
        public float Progress;
        public HeatLevel RecommendedHeat;
    }

    /// <summary>Published when a material is input into the cauldron.</summary>
    public struct MaterialInputEvent
    {
        public string MaterialId;
        public string MaterialName;
        public int InputIndex;
        public bool IsCorrectOrder;
        public int TotalErrors;
        public int TotalInputs;
    }

    /// <summary>Published when alchemy completes successfully.</summary>
    public struct AlchemyCompletedEvent
    {
        public float FinalQuality;
        public PillQuality Quality;
        public string PillId;
        public string PillName;
        public bool IsMutation;
        public MutationType MutationType;
        public float ProficiencyGained;
        public Color QualityColor;
    }

    /// <summary>Published when the cauldron explodes.</summary>
    public struct AlchemyExplodedEvent
    {
        public float PlayerDamage;
        public float MaterialsLostPercent;
        public float CauldronDurabilityLoss;
        public string FailReason;
    }

    /// <summary>Published for warnings during alchemy (overheat, temp deviation).</summary>
    public struct AlchemyWarningEvent
    {
        public string WarningType; // "overheat", "temp_deviation", "order_error"
        public string Message;
        public float Severity;
    }

    /// <summary>Published when a mutation is triggered.</summary>
    public struct AlchemyMutationEvent
    {
        public MutationType Type;
        public string ResultItemId;
        public string ResultItemName;
    }

    /// <summary>Published when proficiency changes.</summary>
    public struct AlchemyProficiencyChangedEvent
    {
        public int Level;
        public float CurrentExp;
        public float ExpToNext;
        public string Title;
    }

    /// <summary>Published periodically for overall progress.</summary>
    public struct AlchemyProgressEvent
    {
        public float Progress;
        public AlchemyStage CurrentStage;
        public float TimeRemaining;
    }

    // ─── AlchemyController ─────────────────────────────────────────────────

    /// <summary>
    /// 控火炼丹核心控制器 (Alchemy Crafting Core)
    ///
    /// Story 003: 控火炼丹核心
    /// - 三档火候 (大火/中火/小火) with 1.5s CD
    /// - 四阶段炼制流程: 沸腾 → 融合 → 提纯 → 收丹
    /// - 品质公式: FinalQuality = Base × TempScore × OrderScore × MatScore × EquipMod
    /// - 炸炉系统: 5% × Overheat × EquipmentHealth
    /// - 投料顺序影响品质 (每错 -15%)
    /// - 四级品质 (下/中/上/极品)
    /// - 变异配方系统
    /// - 熟练度系统
    /// </summary>
    public class AlchemyController : MonoBehaviour
    {
        #region Singleton

        public static AlchemyController Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("温度 & 火候")]
        [SerializeField] private float highHeatRate = 8f;       // 大火 +8°C/s
        [SerializeField] private float mediumHeatRate = 2f;     // 中火 +2°C/s
        [SerializeField] private float lowHeatRate = -1f;       // 小火 -1°C/s
        [SerializeField] private float heatSwitchCooldown = 1.5f;
        [SerializeField] private float ambientTemperature = 25f;
        [SerializeField] private float minTemperature = 0f;
        [SerializeField] private float maxTemperature = 500f;
        [SerializeField] private float optimalTempRange = 50f;  // ±°C around optimal considered "good range"

        [Header("温度评分")]
        [SerializeField] private float temperaturePenaltyFactor = 0.5f; // 偏离惩罚系数

        [Header("炸炉系统")]
        [SerializeField] private float baseExplosionRate = 0.05f;       // 基础5%
        [SerializeField] private float overheatFactorPerSecond = 0.1f; // 每连续大火秒数+0.1
        [SerializeField] private float wearFactorMultiplier = 0.5f;    // 磨损系数乘数
        [SerializeField] private float explosionCheckInterval = 1f;    // 每秒检查一次
        [SerializeField, Range(0.5f, 1f)] private float materialsLostMin = 0.5f;
        [SerializeField, Range(1f, 1f)] private float materialsLostMax = 1f;
        [SerializeField] private float cauldronDurabilityLossMin = 20f;
        [SerializeField] private float cauldronDurabilityLossMax = 50f;
        [SerializeField] private float explosionDamageMultiplier = 0.5f;

        [Header("投料顺序")]
        [SerializeField] private float orderErrorPenalty = 0.15f;       // 每次错误-15%
        [SerializeField] private float minOrderScore = 0.4f;           // OrderScore下限

        [Header("品质阈值")]
        [SerializeField] private float legendaryThreshold = 0.95f;     // ≥0.95 → 极品
        [SerializeField] private float highThreshold = 0.80f;          // ≥0.80 → 上品
        [SerializeField] private float midThreshold = 0.60f;           // ≥0.60 → 中品
        [SerializeField] private float failThreshold = 0.40f;          // <0.40 → 失败

        [Header("变异系统")]
        [SerializeField] private float normalMutationChance = 0.15f;
        [SerializeField] private float recipeMutationChance = 0.05f;
        [SerializeField] private float fusionMutationChance = 0.02f;
        [SerializeField] private float dangerousMutationChance = 0.03f;
        [SerializeField] private int minErrorsForMutation = 3;         // 至少错3次才可能变异

        [Header("熟练度")]
        [SerializeField] private float profPerCraft = 5f;
        [SerializeField] private float profHighQualityBonus = 3f;
        [SerializeField] private float profFirstCraftBonus = 10f;
        [SerializeField] private float profFailGain = 0.5f;

        #endregion

        #region Private State

        // ─── Active Craft State ───
        private AlchemyStage _currentStage = AlchemyStage.Idle;
        private HeatLevel _currentHeat = HeatLevel.Medium;
        private HeatLevel _previousHeat = HeatLevel.Medium;
        private AlchemyRecipeData _currentRecipe;

        // Temperature
        private float _currentTemperature;
        private float _temperatureSum;
        private int _temperatureSamples;

        // Timing
        private float _craftElapsed;
        private float _craftTotalDuration;
#pragma warning disable CS0414 // reserved for future use (heat switch timing display)
        private float _lastHeatSwitchTime;
#pragma warning restore CS0414
        private float _heatSwitchTimer; // 0 = ready, counts up to cooldown
        private float _explosionTimer;

        // Overheat tracking (consecutive high-heat seconds)
        private float _continuousHighHeatSeconds;
        private bool _wasHighHeat;

        // Material input
        private List<AlchemyMaterialInput> _inputMaterials = new List<AlchemyMaterialInput>();
        private int _inputOrderErrors;
        private bool _allMaterialsInput;
        private bool _orderIsReverse;     // completely reversed order?
        private bool _hasExtraMaterials;  // non-standard materials added?

        // Cauldron
        private CauldronData _cauldron;

        // Proficiency
        private AlchemyProficiency _proficiency = new AlchemyProficiency();

        // Recipe tracking
        private HashSet<string> _craftedRecipes = new HashSet<string>(); // for first-craft bonus

        // Result
        private AlchemyResult _lastResult;

        // Warning cooldowns (prevent event spam)
        private float _lastWarningTime;

        #endregion

        #region Public Properties

        public AlchemyStage CurrentStage => _currentStage;
        public HeatLevel CurrentHeat => _currentHeat;
        public float CurrentTemperature => _currentTemperature;
        public float AvgTemperature => _temperatureSamples > 0 ? _temperatureSum / _temperatureSamples : ambientTemperature;
        public float CraftElapsed => _craftElapsed;
        public float CraftTotalDuration => _craftTotalDuration;
        public float Progress => _craftTotalDuration > 0f ? Mathf.Clamp01(_craftElapsed / _craftTotalDuration) : 0f;
        public bool IsCrafting => _currentStage >= AlchemyStage.Boiling && _currentStage <= AlchemyStage.Finishing;
        public bool IsCompleted => _currentStage == AlchemyStage.Complete || _currentStage == AlchemyStage.Exploded;
        public bool IsHeatOnCooldown => _heatSwitchTimer < heatSwitchCooldown;
        public CauldronData ActiveCauldron => _cauldron;
        public AlchemyProficiency Proficiency => _proficiency;
        public AlchemyResult LastResult => _lastResult;
        public float OrderScore => Mathf.Clamp(1f - _inputOrderErrors * orderErrorPenalty, minOrderScore, 1f);
        public int InputMaterialCount => _inputMaterials.Count;
        public bool AllMaterialsInput => _allMaterialsInput;
        public AlchemyRecipeData CurrentRecipe => _currentRecipe;
        public float HeatCooldownProgress => Mathf.Clamp01(_heatSwitchTimer / heatSwitchCooldown);

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
                UnsubscribeFromEvents();
                Instance = null;
            }
        }

        private void SubscribeToEvents()
        {
            // Future: subscribe to events that impact alchemy (e.g., environment changes).
        }

        private void UnsubscribeFromEvents()
        {
            // Future: cleanup.
        }

        private void Update()
        {
            if (IsCrafting)
            {
                float dt = Time.deltaTime;

                // Update heat switch cooldown timer.
                if (_heatSwitchTimer < heatSwitchCooldown)
                {
                    _heatSwitchTimer += dt;
                }

                // Update temperature.
                UpdateTemperature(dt);

                // Update craft timer.
                _craftElapsed += dt;

                // Update overheat tracking.
                UpdateOverheatTracking(dt);

                // Check explosion.
                UpdateExplosionCheck(dt);

                // Update stage.
                UpdateStage();

                // Publish progress event.
                EventBus.Publish(new AlchemyProgressEvent
                {
                    Progress = Progress,
                    CurrentStage = _currentStage,
                    TimeRemaining = Mathf.Max(0f, _craftTotalDuration - _craftElapsed)
                });
            }
        }

        #endregion

        #region Temperature

        /// <summary>Update temperature based on current heat level.</summary>
        private void UpdateTemperature(float dt)
        {
            float rate = GetHeatRate(_currentHeat);
            _currentTemperature += rate * dt;
            _currentTemperature = Mathf.Clamp(_currentTemperature, minTemperature, maxTemperature);

            // Track for running average.
            _temperatureSum += _currentTemperature;
            _temperatureSamples++;

            // Publish temperature event.
            float deviation = _currentTemperature - _currentRecipe.OptimalTemperature;
            EventBus.Publish(new TemperatureChangedEvent
            {
                CurrentTemperature = _currentTemperature,
                OptimalTemperature = _currentRecipe.OptimalTemperature,
                TemperatureDeviation = deviation,
                AvgTemperature = AvgTemperature
            });

            // Fire warning if temperature is dangerously far from optimal.
            float absDeviation = Mathf.Abs(deviation);
            float warningSeverity = Mathf.Clamp01(absDeviation / (_currentRecipe.OptimalTemperature * 0.5f));
            if (warningSeverity > 0.6f && Time.time - _lastWarningTime > 3f)
            {
                _lastWarningTime = Time.time;
                string direction = deviation > 0f ? "过高" : "过低";
                EventBus.Publish(new AlchemyWarningEvent
                {
                    WarningType = "temp_deviation",
                    Message = $"炉温{direction}! (当前:{_currentTemperature:F0}°C / 最佳:{_currentRecipe.OptimalTemperature:F0}°C)",
                    Severity = warningSeverity
                });
            }
        }

        /// <summary>Get the temperature change rate for a heat level.</summary>
        private float GetHeatRate(HeatLevel heat)
        {
            return heat switch
            {
                HeatLevel.High   => highHeatRate,
                HeatLevel.Medium => mediumHeatRate,
                HeatLevel.Low    => lowHeatRate,
                _                => 0f
            };
        }

        #endregion

        #region Overheat Tracking

        /// <summary>Track consecutive high-heat seconds for explosion calculation.</summary>
        private void UpdateOverheatTracking(float dt)
        {
            if (_currentHeat == HeatLevel.High)
            {
                _continuousHighHeatSeconds += dt;
                _wasHighHeat = true;

                // Overheat warning.
                if (_continuousHighHeatSeconds > 5f && Time.time - _lastWarningTime > 2f)
                {
                    _lastWarningTime = Time.time;
                    float severity = Mathf.Clamp01(_continuousHighHeatSeconds / 20f);
                    EventBus.Publish(new AlchemyWarningEvent
                    {
                        WarningType = "overheat",
                        Message = $"警告: 已连续大火 {_continuousHighHeatSeconds:F1}秒，炸炉风险上升!",
                        Severity = severity
                    });
                }
            }
            else if (_wasHighHeat)
            {
                // Reset when switching away from high heat.
                _continuousHighHeatSeconds = 0f;
                _wasHighHeat = false;
            }
        }

        #endregion

        #region Explosion

        /// <summary>Periodic explosion check.</summary>
        private void UpdateExplosionCheck(float dt)
        {
            _explosionTimer += dt;
            if (_explosionTimer >= explosionCheckInterval)
            {
                _explosionTimer = 0f;
                CheckExplosion();
            }
        }

        /// <summary>Calculate and check if explosion occurs.</summary>
        private void CheckExplosion()
        {
            float chance = CalculateExplosionChance();
            if (UnityEngine.Random.value < chance)
            {
                TriggerExplosion();
            }
        }

        /// <summary>
        /// Calculate explosion chance:
        /// ExplosionChance = BaseExplosionRate × OverheatFactor × EquipmentHealthFactor
        /// OverheatFactor = 1.0 + 连续大火秒数 × 0.1
        /// EquipmentHealthFactor = 1.0 + (1 - 当前耐久/最大耐久) × 0.5
        /// </summary>
        public float CalculateExplosionChance()
        {
            float overheatFactor = 1f + _continuousHighHeatSeconds * overheatFactorPerSecond;
            float healthFactor = 1f + _cauldron.WearFactor * wearFactorMultiplier;

            // Proficiency reduction.
            float profReduction = 1f - _proficiency.ExplosionReduction;

            float chance = baseExplosionRate * overheatFactor * healthFactor * profReduction;
            return Mathf.Clamp01(chance);
        }

        /// <summary>Trigger cauldron explosion with all consequences.</summary>
        private void TriggerExplosion()
        {
            // Record previous stage.
            AlchemyStage previousStage = _currentStage;
            _currentStage = AlchemyStage.Exploded;

            // Calculate consequences.
            float materialsLostPercent = UnityEngine.Random.Range(materialsLostMin, materialsLostMax);
            float duraLoss = UnityEngine.Random.Range(cauldronDurabilityLossMin, cauldronDurabilityLossMax);
            float playerDamage = _currentTemperature * explosionDamageMultiplier;

            // Apply durability damage.
            CauldronData damagedCauldron = _cauldron;
            damagedCauldron.CurrentDurability = Mathf.Max(0f, _cauldron.CurrentDurability - duraLoss);
            _cauldron = damagedCauldron;

            // Proficiency gain for failure.
            _proficiency.AddExp(profFailGain);

            // Record result.
            _lastResult = new AlchemyResult
            {
                FinalQuality = 0f,
                Quality = PillQuality.Fail,
                IsMutation = false,
                PillName = "炸炉废渣",
                QualityColor = Color.gray
            };

            // Publish stage change.
            EventBus.Publish(new AlchemyStageChangedEvent
            {
                NewStage = AlchemyStage.Exploded,
                PreviousStage = previousStage,
                Progress = Progress,
                RecommendedHeat = GetRecommendedHeatForStage(AlchemyStage.Exploded)
            });

            // Publish explosion event.
            EventBus.Publish(new AlchemyExplodedEvent
            {
                PlayerDamage = playerDamage,
                MaterialsLostPercent = materialsLostPercent,
                CauldronDurabilityLoss = duraLoss,
                FailReason = $"连续大火{_continuousHighHeatSeconds:F1}秒导致炸炉!"
            });

            // Publish proficiency change.
            PublishProficiencyEvent();

            Debug.Log($"[AlchemyController] 炸炉! " +
                      $"材料损失: {materialsLostPercent * 100:F0}%, " +
                      $"丹炉耐久-{duraLoss:F0}, " +
                      $"玩家伤害: {playerDamage:F0}");
        }

        #endregion

        #region Stage Management

        /// <summary>Get the recommended heat level for a given stage.</summary>
        public static HeatLevel GetRecommendedHeatForStage(AlchemyStage stage)
        {
            return stage switch
            {
                AlchemyStage.Boiling      => HeatLevel.High,
                AlchemyStage.Fusion       => HeatLevel.Medium,
                AlchemyStage.Purification => HeatLevel.Low,
                AlchemyStage.Finishing    => HeatLevel.Medium, // default; recipe may override
                _                         => HeatLevel.Medium
            };
        }

        /// <summary>Get the stage name in Chinese.</summary>
        public static string GetStageDisplayName(AlchemyStage stage)
        {
            return stage switch
            {
                AlchemyStage.Idle          => "待机",
                AlchemyStage.Boiling       => "沸腾期",
                AlchemyStage.Fusion        => "融合期",
                AlchemyStage.Purification  => "提纯期",
                AlchemyStage.Finishing     => "收丹期",
                AlchemyStage.Complete      => "成丹",
                AlchemyStage.Exploded      => "炸炉",
                _                          => "未知"
            };
        }

        /// <summary>Get the heat level display name in Chinese.</summary>
        public static string GetHeatDisplayName(HeatLevel heat)
        {
            return heat switch
            {
                HeatLevel.High   => "大火",
                HeatLevel.Medium => "中火",
                HeatLevel.Low    => "小火",
                _                => "未知"
            };
        }

        /// <summary>Determine the stage based on elapsed time percentage.</summary>
        private AlchemyStage DetermineStage(float progress)
        {
            // Stage timing as percentage of total duration:
            // Boiling:       0% ~ 20%
            // Fusion:       20% ~ 60%
            // Purification: 60% ~ 90%
            // Finishing:    90% ~ 100%
            if (progress < 0.20f) return AlchemyStage.Boiling;
            if (progress < 0.60f) return AlchemyStage.Fusion;
            if (progress < 0.90f) return AlchemyStage.Purification;
            return AlchemyStage.Finishing;
        }

        /// <summary>Update the current stage based on elapsed time.</summary>
        private void UpdateStage()
        {
            if (!IsCrafting) return;

            AlchemyStage newStage = DetermineStage(Progress);

            if (newStage != _currentStage)
            {
                AlchemyStage previousStage = _currentStage;
                _currentStage = newStage;

                HeatLevel recommended = GetRecommendedHeatForStage(_currentStage);

                EventBus.Publish(new AlchemyStageChangedEvent
                {
                    NewStage = _currentStage,
                    PreviousStage = previousStage,
                    Progress = Progress,
                    RecommendedHeat = recommended
                });

                // Check if the player is using wrong heat for this stage (provide warning).
                CheckStageHeatCompliance(previousStage);

                Debug.Log($"[AlchemyController] 阶段切换: {GetStageDisplayName(previousStage)} → " +
                          $"{GetStageDisplayName(_currentStage)} (建议: {GetHeatDisplayName(recommended)})");
            }

            // Check if craft completed.
            if (_craftElapsed >= _craftTotalDuration)
            {
                if (_currentStage == AlchemyStage.Finishing || Progress >= 1f)
                {
                    CompleteAlchemy();
                }
            }
        }

        /// <summary>Check if player's heat choice matches the stage recommendation.</summary>
        private void CheckStageHeatCompliance(AlchemyStage stage)
        {
            HeatLevel recommended = GetRecommendedHeatForStage(stage);
            if (_currentHeat != recommended)
            {
                string wrongHeatName = GetHeatDisplayName(_currentHeat);
                string recHeatName = GetHeatDisplayName(recommended);
                EventBus.Publish(new AlchemyWarningEvent
                {
                    WarningType = "temp_deviation",
                    Message = $"警告: {GetStageDisplayName(stage)}建议使用{recHeatName}，当前使用{wrongHeatName}!",
                    Severity = 0.4f
                });
            }
        }

        /// <summary>Get the stage-specific temperature modifier for quality calculation.</summary>
        private float GetStageTemperatureScore()
        {
            if (_temperatureSamples == 0) return 0.5f;

            float avgTemp = AvgTemperature;
            float optimal = _currentRecipe.OptimalTemperature;

            // Formula: TemperatureScore = 1.0 - (|ActualAvgTemp - OptimalTemp| / OptimalTemp) × 0.5
            float deviation = Mathf.Abs(avgTemp - optimal);
            float score = 1f - (deviation / optimal) * temperaturePenaltyFactor;

            // Apply proficiency temperature tolerance bonus.
            score += _proficiency.TemperatureToleranceBonus;

            return Mathf.Clamp01(score);
        }

        #endregion

        #region Public API: Alchemy Lifecycle

        /// <summary>
        /// Start a new alchemy session with the given recipe and cauldron.
        /// Returns false if already crafting or preconditions not met.
        /// </summary>
        public bool StartAlchemy(AlchemyRecipeData recipe, CauldronData cauldron)
        {
            if (IsCrafting)
            {
                Debug.LogWarning("[AlchemyController] Already crafting.");
                return false;
            }

            if (recipe.Duration <= 0f)
            {
                Debug.LogError("[AlchemyController] Invalid recipe duration.");
                return false;
            }

            if (cauldron.CurrentDurability <= 0f)
            {
                Debug.LogWarning("[AlchemyController] Cauldron is broken.");
                return false;
            }

            if (_proficiency.Level < recipe.RequiredProficiency)
            {
                Debug.LogWarning($"[AlchemyController] Proficiency too low. Required: {recipe.RequiredProficiency}, Have: {_proficiency.Level}");
                return false;
            }

            // Initialize state.
            _currentRecipe = recipe;
            _cauldron = cauldron;
            _currentStage = AlchemyStage.Boiling;
            _currentHeat = HeatLevel.Medium;
            _previousHeat = HeatLevel.Medium;
            _currentTemperature = ambientTemperature;
            _temperatureSum = ambientTemperature;
            _temperatureSamples = 1;
            _craftElapsed = 0f;
            _craftTotalDuration = recipe.Duration;
            _lastHeatSwitchTime = Time.time;
            _heatSwitchTimer = heatSwitchCooldown; // start off cooldown
            _explosionTimer = 0f;
            _continuousHighHeatSeconds = 0f;
            _wasHighHeat = false;
            _inputMaterials.Clear();
            _inputOrderErrors = 0;
            _allMaterialsInput = false;
            _orderIsReverse = false;
            _hasExtraMaterials = false;

            // Publish start event.
            EventBus.Publish(new AlchemyStartedEvent
            {
                RecipeId = recipe.Id,
                RecipeName = recipe.DisplayName,
                TotalDuration = recipe.Duration,
                OptimalTemperature = recipe.OptimalTemperature,
                InitialStage = AlchemyStage.Boiling
            });

            // Publish initial stage event.
            EventBus.Publish(new AlchemyStageChangedEvent
            {
                NewStage = AlchemyStage.Boiling,
                PreviousStage = AlchemyStage.Idle,
                Progress = 0f,
                RecommendedHeat = GetRecommendedHeatForStage(AlchemyStage.Boiling)
            });

            Debug.Log($"[AlchemyController] 开始炼丹: {recipe.DisplayName} " +
                      $"(时长: {recipe.Duration}s, 最佳温度: {recipe.OptimalTemperature}°C, " +
                      $"丹炉: {cauldron.DisplayName})");
            return true;
        }

        /// <summary>
        /// Switch the current heat level. Returns true if switch was applied.
        /// Respects 1.5s cooldown.
        /// </summary>
        public bool SwitchHeat(HeatLevel targetHeat)
        {
            if (_currentHeat == targetHeat) return false;

            if (_heatSwitchTimer < heatSwitchCooldown)
            {
                float remaining = heatSwitchCooldown - _heatSwitchTimer;
                EventBus.Publish(new HeatSwitchedEvent
                {
                    NewLevel = targetHeat,
                    PreviousLevel = _currentHeat,
                    CooldownRemaining = remaining,
                    OnCooldown = true
                });
                Debug.Log($"[AlchemyController] 火候切换冷却中... 剩余 {remaining:F1}s");
                return false;
            }

            _previousHeat = _currentHeat;
            _currentHeat = targetHeat;
            _lastHeatSwitchTime = Time.time;
            _heatSwitchTimer = 0f;

            EventBus.Publish(new HeatSwitchedEvent
            {
                NewLevel = _currentHeat,
                PreviousLevel = _previousHeat,
                CooldownRemaining = heatSwitchCooldown,
                OnCooldown = false
            });

            Debug.Log($"[AlchemyController] 火候切换: {GetHeatDisplayName(_previousHeat)} → {GetHeatDisplayName(_currentHeat)}");
            return true;
        }

        /// <summary>
        /// Input a material into the cauldron. Returns true if input was accepted.
        /// Checks against the recipe's recommended order.
        /// </summary>
        public bool InputMaterial(AlchemyMaterialInput material)
        {
            if (!IsCrafting)
            {
                Debug.LogWarning("[AlchemyController] Not currently crafting.");
                return false;
            }

            if (_allMaterialsInput)
            {
                Debug.LogWarning("[AlchemyController] All materials already input.");
                return false;
            }

            // Check if this material goes over the recipe requirements.
            // If there are more materials than the recipe recommends, flag as extra.
            bool isExtra = _currentRecipe.RecommendedOrder != null &&
                           _inputMaterials.Count >= _currentRecipe.RecommendedOrder.Length;

            if (isExtra)
            {
                _hasExtraMaterials = true;
            }

            // Check order correctness if within recommended order range.
            bool isCorrect = false;
            if (!isExtra && _currentRecipe.RecommendedOrder != null &&
                _inputMaterials.Count < _currentRecipe.RecommendedOrder.Length)
            {
                int expectedIndex = _inputMaterials.Count;
                isCorrect = expectedIndex < _currentRecipe.RecommendedOrder.Length &&
                            _currentRecipe.RecommendedOrder[expectedIndex] == material.ItemId;
            }

            if (!isCorrect && !isExtra)
            {
                _inputOrderErrors++;
            }

            // Track if completely reversed.
            if (_currentRecipe.RecommendedOrder != null && !isExtra)
            {
                int inputIdx = _inputMaterials.Count;
                int recipeLen = _currentRecipe.RecommendedOrder.Length;
                if (inputIdx < recipeLen)
                {
                    // Check if this material matches the reversed position.
                    int reversedIdx = recipeLen - 1 - inputIdx;
                    if (reversedIdx >= 0 && reversedIdx < recipeLen &&
                        _currentRecipe.RecommendedOrder[reversedIdx] == material.ItemId)
                    {
                        // Matching reversed position — could be fully reversed.
                    }
                }
            }

            material.InputOrderIndex = _inputMaterials.Count;
            _inputMaterials.Add(material);

            // Check if all materials have been input.
            _allMaterialsInput = _currentRecipe.RecommendedOrder == null ||
                                 _inputMaterials.Count >= _currentRecipe.RecommendedOrder.Length;

            // Detect if order is completely reversed (every input matches reverse of recommended).
            if (_currentRecipe.RecommendedOrder != null && !isExtra)
            {
                bool allReversed = _inputMaterials.Count == _currentRecipe.RecommendedOrder.Length;
                if (allReversed)
                {
                    for (int i = 0; i < _inputMaterials.Count && i < _currentRecipe.RecommendedOrder.Length; i++)
                    {
                        int revIdx = _currentRecipe.RecommendedOrder.Length - 1 - i;
                        if (_inputMaterials[i].ItemId != _currentRecipe.RecommendedOrder[revIdx])
                        {
                            allReversed = false;
                            break;
                        }
                    }
                    _orderIsReverse = allReversed;
                }
            }

            // Publish material input event.
            EventBus.Publish(new MaterialInputEvent
            {
                MaterialId = material.ItemId,
                MaterialName = material.DisplayName,
                InputIndex = material.InputOrderIndex,
                IsCorrectOrder = isCorrect,
                TotalErrors = _inputOrderErrors,
                TotalInputs = _inputMaterials.Count
            });

            if (!isCorrect && !isExtra)
            {
                EventBus.Publish(new AlchemyWarningEvent
                {
                    WarningType = "order_error",
                    Message = $"投料顺序错误! 已错{_inputOrderErrors}次 (品质-{_inputOrderErrors * orderErrorPenalty * 100:F0}%)",
                    Severity = Mathf.Clamp01(_inputOrderErrors * 0.2f)
                });
            }

            Debug.Log($"[AlchemyController] 投入材料: {material.DisplayName} " +
                      $"[{(isCorrect ? "顺序正确" : isExtra ? "额外材料" : "顺序错误")}] " +
                      $"(品质系数: {material.QualityCoefficient}) " +
                      $"总错误: {_inputOrderErrors}");

            return true;
        }

        /// <summary>
        /// Complete the alchemy process and calculate the final result.
        /// Called automatically when the timer expires.
        /// </summary>
        private void CompleteAlchemy()
        {
            if (_currentStage == AlchemyStage.Complete)
                return;

            AlchemyStage previousStage = _currentStage;
            _currentStage = AlchemyStage.Complete;

            // Calculate quality.
            AlchemyResult result = CalculateFinalQuality();

            // Proficiency gain.
            float profGain = profPerCraft;
            if (result.Quality >= PillQuality.High) profGain += profHighQualityBonus;
            if (!_craftedRecipes.Contains(_currentRecipe.Id))
            {
                profGain += profFirstCraftBonus;
                _craftedRecipes.Add(_currentRecipe.Id);
            }
            _proficiency.AddExp(profGain);

            // Store result.
            _lastResult = result;

            // Publish stage change.
            EventBus.Publish(new AlchemyStageChangedEvent
            {
                NewStage = AlchemyStage.Complete,
                PreviousStage = previousStage,
                Progress = 1f,
                RecommendedHeat = HeatLevel.Medium
            });

            // Publish completion event.
            EventBus.Publish(new AlchemyCompletedEvent
            {
                FinalQuality = result.FinalQuality,
                Quality = result.Quality,
                PillId = result.PillId,
                PillName = result.PillName,
                IsMutation = result.IsMutation,
                MutationType = result.MutationType,
                ProficiencyGained = profGain,
                QualityColor = result.QualityColor
            });

            // If mutation, publish mutation event.
            if (result.IsMutation)
            {
                EventBus.Publish(new AlchemyMutationEvent
                {
                    Type = result.MutationType,
                    ResultItemId = result.PillId,
                    ResultItemName = result.PillName
                });
            }

            // Publish proficiency change.
            PublishProficiencyEvent();

            Debug.Log($"[AlchemyController] 炼丹完成! " +
                      $"品质: {GetQualityDisplayName(result.Quality)} " +
                      $"(评分: {result.FinalQuality:F3})" +
                      (result.IsMutation ? " [变异!]" : ""));
        }

        /// <summary>
        /// Force-stop the current alchemy (e.g., player walks away, interrupted).
        /// Materials are lost.
        /// </summary>
        public void CancelAlchemy()
        {
            if (!IsCrafting) return;

            _currentStage = AlchemyStage.Idle;

            EventBus.Publish(new AlchemyWarningEvent
            {
                WarningType = "temp_deviation",
                Message = "炼丹已取消，材料已损失。",
                Severity = 1f
            });

            Debug.Log("[AlchemyController] 炼丹已取消。");
        }

        #endregion

        #region Quality Calculation

        /// <summary>
        /// Calculate the final quality using the complete formula:
        /// FinalQuality = BaseQuality × TemperatureScore × OrderScore × MaterialScore × EquipmentModifier
        /// </summary>
        private AlchemyResult CalculateFinalQuality()
        {
            // 1. Base quality: random within recipe's range.
            float baseQuality = UnityEngine.Random.Range(_currentRecipe.BaseQualityMin, _currentRecipe.BaseQualityMax);

            // 2. Temperature score.
            float tempScore = GetStageTemperatureScore();

            // 3. Order score.
            float orderScore = OrderScore;

            // 4. Material score: average of quality coefficients.
            float matScore = CalculateMaterialScore();

            // 5. Equipment modifier.
            float equipMod = _cauldron.QualityCoefficient * (1f - _cauldron.WearFactor * 0.01f);

            // 6. Proficiency quality bonus.
            float profBonus = _proficiency.QualityBonus;

            // Final quality.
            float finalQuality = baseQuality * tempScore * orderScore * matScore * equipMod * profBonus;

            // Check for mutation (deviant input → non-standard output).
            bool isMutation = false;
            MutationType mutationType = MutationType.None;
            if (CheckMutationTrigger())
            {
                isMutation = true;
                mutationType = RollMutationType();
                // Mutation modifies the result: it produces a different item.
                // The quality still determines the tier, but the item ID changes.
            }

            // Determine quality tier.
            PillQuality quality = QualityFromScore(finalQuality);

            // Determine pill name/ID.
            string pillId = _currentRecipe.Id + "_result";
            string pillName = _currentRecipe.DisplayName;

            if (isMutation)
            {
                switch (mutationType)
                {
                    case MutationType.Normal:
                        pillId = _currentRecipe.Id + "_mutant";
                        pillName = _currentRecipe.DisplayName + "·异变";
                        break;
                    case MutationType.Recipe:
                        pillId = "custom_recipe_" + Guid.NewGuid().ToString("N");
                        pillName = "自创·" + _currentRecipe.DisplayName;
                        break;
                    case MutationType.Fusion:
                        pillId = "chaos_" + Guid.NewGuid().ToString("N");
                        pillName = "混沌·" + _currentRecipe.DisplayName;
                        break;
                    case MutationType.Dangerous:
                        pillId = _currentRecipe.Id + "_cursed";
                        pillName = "咒蚀·" + _currentRecipe.DisplayName;
                        break;
                }
            }

            return new AlchemyResult
            {
                FinalQuality = finalQuality,
                Quality = quality,
                PillId = pillId,
                PillName = pillName,
                IsMutation = isMutation,
                MutationType = mutationType,
                QualityColor = GetQualityColor(quality)
            };
        }

        /// <summary>Calculate the material score: average quality coefficient of inputs.</summary>
        private float CalculateMaterialScore()
        {
            if (_inputMaterials.Count == 0) return 0.5f;

            float sum = 0f;
            foreach (var mat in _inputMaterials)
            {
                sum += mat.QualityCoefficient;
            }
            return sum / _inputMaterials.Count;
        }

        /// <summary>Determine pill quality tier from a quality score.</summary>
        public PillQuality QualityFromScore(float score)
        {
            if (score >= legendaryThreshold) return PillQuality.Legendary;
            if (score >= highThreshold)      return PillQuality.High;
            if (score >= midThreshold)       return PillQuality.Mid;
            if (score >= failThreshold)      return PillQuality.Low;
            return PillQuality.Fail;
        }

        /// <summary>Get a display color for each quality tier.</summary>
        public static Color GetQualityColor(PillQuality quality)
        {
            return quality switch
            {
                PillQuality.Fail      => new Color(0.5f, 0.5f, 0.5f),   // gray
                PillQuality.Low       => Color.white,                     // white
                PillQuality.Mid       => new Color(0.2f, 0.8f, 0.2f),    // green
                PillQuality.High      => new Color(0.2f, 0.4f, 0.9f),    // blue
                PillQuality.Legendary => new Color(0.7f, 0.2f, 0.9f),    // purple
                _                     => Color.white
            };
        }

        /// <summary>Get Chinese display name for quality tier.</summary>
        public static string GetQualityDisplayName(PillQuality quality)
        {
            return quality switch
            {
                PillQuality.Fail      => "失败",
                PillQuality.Low       => "下品",
                PillQuality.Mid       => "中品",
                PillQuality.High      => "上品",
                PillQuality.Legendary => "极品",
                _                     => "未知"
            };
        }

        /// <summary>Get the effect multiplier range for a quality tier.</summary>
        public static (float min, float max) GetEffectMultiplier(PillQuality quality)
        {
            return quality switch
            {
                PillQuality.Fail      => (0f, 0f),
                PillQuality.Low       => (0.5f, 0.7f),
                PillQuality.Mid       => (0.8f, 1.0f),
                PillQuality.High      => (1.1f, 1.3f),
                PillQuality.Legendary => (1.4f, 1.6f),
                _                     => (0f, 0f)
            };
        }

        #endregion

        #region Mutation System

        /// <summary>Check if mutation should trigger.</summary>
        private bool CheckMutationTrigger()
        {
            // Conditions that can trigger mutation:
            // 1. Order errors >= threshold.
            // 2. Extra materials added beyond recipe.
            // 3. Completely reversed order.
            if (_inputOrderErrors >= minErrorsForMutation) return true;
            if (_hasExtraMaterials) return true;
            if (_orderIsReverse) return true;
            return false;
        }

        /// <summary>Roll for the type of mutation.</summary>
        private MutationType RollMutationType()
        {
            float roll = UnityEngine.Random.value;

            if (roll < dangerousMutationChance)
                return MutationType.Dangerous;

            roll -= dangerousMutationChance;
            if (roll < fusionMutationChance)
                return MutationType.Fusion;

            roll -= fusionMutationChance;
            if (roll < recipeMutationChance)
                return MutationType.Recipe;

            // Default: normal mutation.
            return MutationType.Normal;
        }

        #endregion

        #region Heat Level Helpers

        /// <summary>Get heat level from string.</summary>
        public static HeatLevel HeatFromString(string name)
        {
            return name switch
            {
                "大火" or "high" or "High"   => HeatLevel.High,
                "中火" or "medium" or "Medium" => HeatLevel.Medium,
                "小火" or "low" or "Low"     => HeatLevel.Low,
                _                            => HeatLevel.Medium
            };
        }

        #endregion

        #region Proficiency

        /// <summary>Publish proficiency change event.</summary>
        private void PublishProficiencyEvent()
        {
            EventBus.Publish(new AlchemyProficiencyChangedEvent
            {
                Level = _proficiency.Level,
                CurrentExp = _proficiency.CurrentExp,
                ExpToNext = _proficiency.ExpToNext,
                Title = _proficiency.GetTitle()
            });
        }

        /// <summary>Set proficiency level directly (for saves/loading).</summary>
        public void SetProficiency(int level, float exp = 0f)
        {
            _proficiency.Level = Mathf.Clamp(level, 1, 100);
            _proficiency.CurrentExp = exp;
        }

        /// <summary>Add proficiency experience.</summary>
        public void AddProficiency(float amount)
        {
            int oldLevel = _proficiency.Level;
            _proficiency.AddExp(amount);
            if (_proficiency.Level > oldLevel)
            {
                Debug.Log($"[AlchemyController] 炼丹熟练度提升! Lv.{_proficiency.Level} — {_proficiency.GetTitle()}");
            }
            PublishProficiencyEvent();
        }

        /// <summary>Get the proficiency progress as 0-1.</summary>
        public float GetProficiencyProgress() => _proficiency.CurrentExp / _proficiency.ExpToNext;

        /// <summary>Register a callback for proficiency level up.</summary>
        public void OnProficiencyLevelUp(Action<int> callback)
        {
            _proficiency.OnLevelUp += callback;
        }

        #endregion

        #region Equipment (Cauldron) Management

        /// <summary>Set the active cauldron.</summary>
        public void SetCauldron(CauldronData cauldron)
        {
            _cauldron = cauldron;
        }

        /// <summary>Repair the active cauldron by a given amount.</summary>
        public void RepairCauldron(float amount)
        {
            CauldronData repaired = _cauldron;
            repaired.CurrentDurability = Mathf.Min(_cauldron.MaxDurability, _cauldron.CurrentDurability + amount);
            _cauldron = repaired;
        }

        #endregion

        #region Debug/Editor Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            string stageStr = GetStageDisplayName(_currentStage);
            string heatStr = GetHeatDisplayName(_currentHeat);
            string qualityStr = IsCompleted ? GetQualityDisplayName(_lastResult.Quality) : "进行中";

            return $"=== AlchemyController Status ===\n" +
                   $"Stage: {stageStr} ({Progress * 100:F1}%)\n" +
                   $"Heat: {heatStr} (CD: {HeatCooldownProgress * 100:F0}%)\n" +
                   $"Temp: {_currentTemperature:F1}°C (Avg: {AvgTemperature:F1}°C | Optimal: {_currentRecipe.OptimalTemperature:F0}°C)\n" +
                   $"Materials: {_inputMaterials.Count} input, {_inputOrderErrors} order errors\n" +
                   $"Overheat: {_continuousHighHeatSeconds:F1}s continuous high heat\n" +
                   $"Explosion Chance: {CalculateExplosionChance() * 100:F1}%\n" +
                   $"Cauldron: {_cauldron.CurrentDurability}/{_cauldron.MaxDurability} ({_cauldron.WearFactor * 100:F0}% wear)\n" +
                   $"Proficiency: Lv.{_proficiency.Level} ({_proficiency.GetTitle()}) [{GetProficiencyProgress():P1}]\n" +
                   $"Last Result: {qualityStr}";
        }

        /// <summary>Create a test recipe for debugging.</summary>
        public AlchemyRecipeData CreateTestRecipe(string name = "聚气丹",
                                                   float optimalTemp = 150f,
                                                   float duration = 50f)
        {
            return new AlchemyRecipeData
            {
                Id = "recipe_test_" + Guid.NewGuid().ToString("N"),
                DisplayName = name,
                Description = "测试用基础炼丹配方",
                OptimalTemperature = optimalTemp,
                Duration = duration,
                BaseQualityMin = 0.3f,
                BaseQualityMax = 0.8f,
                RecommendedOrder = new[] { "mat_herb_01", "mat_root_02", "mat_essence_03" },
                Difficulty = 1,
                RequiredProficiency = 1
            };
        }

        /// <summary>Create a test cauldron for debugging.</summary>
        public CauldronData CreateTestCauldron(string name = "新手丹炉",
                                                float qualityCoeff = 0.8f,
                                                float maxDurability = 200f)
        {
            return new CauldronData
            {
                Id = "cauldron_test_" + Guid.NewGuid().ToString("N"),
                DisplayName = name,
                QualityCoefficient = qualityCoeff,
                MaxDurability = maxDurability,
                CurrentDurability = maxDurability
            };
        }

        /// <summary>Quick automated alchemy test. Returns the final result.</summary>
        public AlchemyResult RunTestAlchemy(AlchemyRecipeData recipe,
                                             CauldronData cauldron,
                                             AlchemyMaterialInput[] materials,
                                             HeatLevel[] heatSchedule)
        {
            if (!StartAlchemy(recipe, cauldron))
                return default;

            // Input materials.
            foreach (var mat in materials)
            {
                InputMaterial(mat);
            }

            // Simulate heat schedule with time skipping.
            int heatIdx = 0;
            float simulateStep = 0.5f; // 0.5s per step
            float elapsed = 0f;

            while (elapsed < recipe.Duration && _currentStage != AlchemyStage.Exploded)
            {
                // Apply heat schedule.
                if (heatIdx < heatSchedule.Length)
                {
                    // Check if we should switch heat (tracked externally for simulation).
                    HeatLevel targetHeat = heatSchedule[Mathf.Min(heatIdx, heatSchedule.Length - 1)];

                    // Manually force heat for simulation (bypass cooldown).
                    if (_currentHeat != targetHeat)
                    {
                        _previousHeat = _currentHeat;
                        _currentHeat = targetHeat;
                        _heatSwitchTimer = 0f;
                        heatIdx++;
                    }
                }

                // Simulate time.
                elapsed += simulateStep;
                _craftElapsed = elapsed;

                // Update temperature.
                _currentTemperature += GetHeatRate(_currentHeat) * simulateStep;
                _currentTemperature = Mathf.Clamp(_currentTemperature, minTemperature, maxTemperature);
                _temperatureSum += _currentTemperature * simulateStep;
                _temperatureSamples++;

                // Update overheat.
                if (_currentHeat == HeatLevel.High)
                    _continuousHighHeatSeconds += simulateStep;
                else
                    _continuousHighHeatSeconds = Mathf.Max(0f, _continuousHighHeatSeconds - simulateStep * 2f); // decay

                // Check explosion.
                _explosionTimer += simulateStep;
                while (_explosionTimer >= explosionCheckInterval)
                {
                    _explosionTimer -= explosionCheckInterval;
                    CheckExplosion();
                    if (_currentStage == AlchemyStage.Exploded)
                        break;
                }

                // Update stage.
                AlchemyStage stage = DetermineStage(elapsed / recipe.Duration);
                if (stage != _currentStage && _currentStage != AlchemyStage.Exploded)
                {
                    _currentStage = stage;
                }
            }

            // Complete if not exploded.
            if (_currentStage != AlchemyStage.Exploded)
            {
                CompleteAlchemy();
            }

            return _lastResult;
        }

        #endregion
    }
}
