using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Core
{
    // ─── Enums ──────────────────────────────────────────────────────────────

    /// <summary>炼器四步阶段状态机</summary>
    public enum ForgeStage
    {
        Idle,           // 未开始
        Smelting,       // 熔炼期
        Shaping,        // 塑形期
        Quenching,      // 淬火期
        Enlightening,   // 开光期
        Complete,       // 成器
        Failed          // 失败
    }

    /// <summary>装备品质五级 (R/SR/SSR/UR)</summary>
    public enum EquipmentQuality
    {
        Fail,   // 失败
        R,      // R (白)
        SR,     // SR (蓝)
        SSR,    // SSR (紫)
        UR      // UR (金)
    }

    /// <summary>淬火液类型</summary>
    public enum QuenchingLiquid
    {
        None,           // 未选择
        SpiritSpring,   // 灵泉 — 基础属性提升
        BeastBlood      // 妖兽血 — 攻击/暴击属性
    }

    /// <summary>词缀类型</summary>
    public enum AffixType
    {
        None,
        Attack,         // 攻击
        Defense,        // 防御
        CritRate,       // 暴击率
        CritDamage,     // 暴击伤害
        Speed,          // 速度
        LifeSteal,      // 吸血
        Spirit,         // 灵力
        Resistance      // 抗性
    }

    // ─── Data Structures ──────────────────────────────────────────────────

    /// <summary>炼器配方/蓝图数据</summary>
    [Serializable]
    public struct ForgeRecipeData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public float BaseQualityMin;          // 基底品质最低 (0.3~1.0)
        public float BaseQualityMax;          // 基底品质最高
        public float SmeltingOptimalTemp;     // 熔炼最佳温度 (°C)
        public float SmeltingDuration;        // 熔炼持续时间 (秒)
        public int RequiredProficiency;       // 要求最低熟练度
        public int Difficulty;                // 炼器难度 1-10
        public string[] RecommendedMaterials; // 推荐材料顺序
        public EquipmentQuality MinQuality;   // 最低可产出品质
        public float BaseStatsMultiplier;     // 基础属性倍率
    }

    /// <summary>投入的材料数据</summary>
    [Serializable]
    public struct ForgeMaterialInput
    {
        public string ItemId;
        public string DisplayName;
        public float QualityCoefficient;      // 品质系数: 普通0.7, 良好0.85, 优质1.0, 完美1.2
        public int InputOrderIndex;           // 投入时的顺序索引 (0-based)
    }

    /// <summary>炼器台数据</summary>
    [Serializable]
    public struct ForgeAnvilData
    {
        public string Id;
        public string DisplayName;
        public float QualityCoefficient;      // 品质系数: 新手0.8, 精良1.0, 传说1.2
        public float MaxDurability;           // 最大耐久
        public float CurrentDurability;       // 当前耐久

        /// <summary>磨损系数: 0=全新, 1=报废</summary>
        public float WearFactor => 1f - (CurrentDurability / MaxDurability);
    }

    /// <summary>锤击塑形记录</summary>
    [Serializable]
    public struct ShapingStrikeData
    {
        public int StrikeIndex;
        public float TargetForce;             // 目标力道 (0~1)
        public float AppliedForce;            // 实际施力 (0~1)
        public float Accuracy;                // 精准度 (1.0 = perfect)
        public float ScoreContribution;       // 本次敲击对ShapeScore的贡献
    }

    /// <summary>淬火液数据</summary>
    [Serializable]
    public struct QuenchingLiquidData
    {
        public QuenchingLiquid Type;
        public string DisplayName;
        public string Description;
        public float AffinityBonus;           // 亲和度加成
        public float PropertyMultiplier;      // 属性倍率调整
        public string StatBonusType;          // 加成的属性类型
        public Color LiquidColor;
    }

    /// <summary>装备词缀数据</summary>
    [Serializable]
    public struct EquipmentAffix
    {
        public AffixType Type;
        public string DisplayName;
        public float Value;
        public string Description;
    }

    /// <summary>炼器产出结果</summary>
    [Serializable]
    public struct ForgeResult
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public float FinalQuality;
        public float BaseStats;
        public float FinalStats;
        public float PurityScore;             // 熔炼纯度
        public float ShapeScore;              // 塑形评分
        public float QuenchScore;             // 淬火评分
        public float EnlightenScore;          // 开光评分
        public QuenchingLiquid UsedLiquid;    // 使用的淬火液
        public EquipmentAffix[] Affixes;       // 词缀列表 (0~3条)
        public int AffixCount;
        public Color QualityColor;
    }

    /// <summary>炼器熟练度</summary>
    [Serializable]
    public class ForgeProficiency
    {
        public int Level = 1;
        public float CurrentExp;
        public float ExpToNext = 100f;

        /// <summary>温度控制精度提升</summary>
        public float TemperatureToleranceBonus => Level * 0.001f;

        /// <summary>塑形精准加成</summary>
        public float ShapeAccuracyBonus => Level * 0.002f;

        /// <summary>品质系数加成 (1~100级)</summary>
        public float QualityBonus => 1.0f + (Level - 1) * 0.002f;

        /// <summary>UR品质概率加成 (71级后)</summary>
        public float UrQualityBonus => Level >= 71 ? (Level - 70) * 0.003f : 0f;

        /// <summary>淬火效果加成</summary>
        public float QuenchBonus => 1.0f + (Level - 1) * 0.0015f;

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
            if (Level <= 10)   return "炼器学徒";
            if (Level <= 25)   return "炼器工匠";
            if (Level <= 45)   return "炼器大师";
            if (Level <= 70)   return "炼器宗师";
            if (Level <= 90)   return "圣手器师";
            return "传说器仙";
        }

        public System.Action<int> OnLevelUp;
    }

    // ─── Forge Event Structs ─────────────────────────────────────────────

    /// <summary>Published when forging begins.</summary>
    public struct ForgeStartedEvent
    {
        public string RecipeId;
        public string RecipeName;
        public float SmeltingDuration;
        public float OptimalTemperature;
        public ForgeStage InitialStage;
    }

    /// <summary>Published when forge stage transitions.</summary>
    public struct ForgeStageChangedEvent
    {
        public ForgeStage NewStage;
        public ForgeStage PreviousStage;
        public float Progress;
    }

    /// <summary>Published each frame for smelting temperature UI updates.</summary>
    public struct SmeltingTemperatureEvent
    {
        public float CurrentTemperature;
        public float OptimalTemperature;
        public float TemperatureDeviation;
        public float PurityProgress;         // 当前纯度进度 (0~1)
    }

    /// <summary>Published during shaping QTE phase.</summary>
    public struct ShapingStrikeEvent
    {
        public int CurrentStrike;
        public int TotalStrikes;
        public float TargetForce;
        public float ForceRangeMin;
        public float ForceRangeMax;
        public bool IsPerfect;
        public float Accuracy;
        public float CumulativeShapeScore;
    }

    /// <summary>Published when player selects a quenching liquid.</summary>
    public struct QuenchingSelectedEvent
    {
        public QuenchingLiquid LiquidType;
        public string LiquidName;
        public float AffinityBonus;
        public string StatBonusType;
    }

    /// <summary>Published during enlightening phase.</summary>
    public struct EnlighteningProgressEvent
    {
        public float SpiritualPowerInjected;
        public float MaxSpiritualPower;
        public float Affinity;
        public float Progress;
    }

    /// <summary>Published when forging completes successfully.</summary>
    public struct ForgeCompletedEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public float FinalQuality;
        public float FinalStats;
        public int AffixCount;
        public EquipmentAffix[] Affixes;
        public float ProficiencyGained;
        public Color QualityColor;
    }

    /// <summary>Published when forging fails.</summary>
    public struct ForgeFailedEvent
    {
        public string FailReason;
        public float MaterialsLostPercent;
        public float AnvilDurabilityLoss;
    }

    /// <summary>Published for warnings during forging.</summary>
    public struct ForgeWarningEvent
    {
        public string WarningType;  // "overheat", "temp_deviation", "strike_miss"
        public string Message;
        public float Severity;
    }

    /// <summary>Published when proficiency changes.</summary>
    public struct ForgeProficiencyChangedEvent
    {
        public int Level;
        public float CurrentExp;
        public float ExpToNext;
        public string Title;
    }

    /// <summary>Published when an equipment is enhanced.</summary>
    public struct EquipmentEnhanceEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int OldLevel;
        public int NewLevel;
        public bool Success;
        public float Chance;
    }

    // ─── ForgeController ─────────────────────────────────────────────────

    /// <summary>
    /// 炼器四步流程核心控制器 (Forge Crafting Core)
    ///
    /// Story 004: 炼器四步流程
    /// - 四步流程: 熔炼 → 塑形 → 淬火 → 开光
    /// - 熔炼温度影响纯度
    /// - 锤击塑形力度QTE
    /// - 不同淬火液(灵泉/妖兽血)赋予不同属性
    /// - 开光灵力注入影响亲和度
    /// - 装备品质 R/SR/SSR/UR + 词缀(0/1/2/3条)
    /// - 品质阈值: <0.6=0词缀, ≥0.6 = 1, ≥0.8 = 2, ≥0.95=3
    /// - 强化: EnhanceChance = 0.8 × QualityMod × (1 - Level×0.1)
    /// - 升级上限: R=5 / SR=7 / SSR=9 / UR=10
    /// </summary>
    public class ForgeController : MonoBehaviour
    {
        #region Singleton

        public static ForgeController Instance { get; private set; }

        #endregion

        #region Constants & Static Data

        /// <summary>品质阈值: FinalQuality对应的品质等级</summary>
        private static readonly (float threshold, EquipmentQuality quality)[] QualityThresholds =
        {
            (0.95f, EquipmentQuality.UR),
            (0.80f, EquipmentQuality.SSR),
            (0.60f, EquipmentQuality.SR),
            (0f,    EquipmentQuality.R)
        };

        /// <summary>词缀数量阈值 (按FinalQuality)</summary>
        private static readonly (float threshold, int count)[] AffixCountThresholds =
        {
            (0.95f, 3),
            (0.80f, 2),
            (0.60f, 1),
            (0f,    0)
        };

        /// <summary>品质对应的强化上限</summary>
        public static readonly Dictionary<EquipmentQuality, int> EnhanceLevelCaps = new Dictionary<EquipmentQuality, int>
        {
            { EquipmentQuality.R,   5 },
            { EquipmentQuality.SR,  7 },
            { EquipmentQuality.SSR, 9 },
            { EquipmentQuality.UR,  10 }
        };

        /// <summary>品质对应的品质系数 Mod</summary>
        public static readonly Dictionary<EquipmentQuality, float> QualityModifiers = new Dictionary<EquipmentQuality, float>
        {
            { EquipmentQuality.Fail, 0f },
            { EquipmentQuality.R,    0.5f },
            { EquipmentQuality.SR,   0.7f },
            { EquipmentQuality.SSR,  0.85f },
            { EquipmentQuality.UR,   1.0f }
        };

        /// <summary>淬火液静态数据</summary>
        public static readonly Dictionary<QuenchingLiquid, QuenchingLiquidData> QuenchingLiquidDataMap = new Dictionary<QuenchingLiquid, QuenchingLiquidData>
        {
            { QuenchingLiquid.SpiritSpring, new QuenchingLiquidData
                {
                    Type = QuenchingLiquid.SpiritSpring,
                    DisplayName = "灵泉",
                    Description = "蕴含天地灵气的泉水，提升基础属性亲和度",
                    AffinityBonus = 0.15f,
                    PropertyMultiplier = 1.0f,
                    StatBonusType = "Spirit",
                    LiquidColor = new Color(0.3f, 0.6f, 1.0f)
                }
            },
            { QuenchingLiquid.BeastBlood, new QuenchingLiquidData
                {
                    Type = QuenchingLiquid.BeastBlood,
                    DisplayName = "妖兽血",
                    Description = "蕴含妖兽狂暴力量的血液，提升攻击与暴击属性",
                    AffinityBonus = 0.10f,
                    PropertyMultiplier = 1.2f,
                    StatBonusType = "Attack",
                    LiquidColor = new Color(0.9f, 0.2f, 0.2f)
                }
            }
        };

        #endregion

        #region Inspector Configuration

        [Header("熔炼系统")]
        [SerializeField] private float highHeatRate = 8f;          // 大火 +8°C/s
        [SerializeField] private float mediumHeatRate = 2f;        // 中火 +2°C/s
        [SerializeField] private float lowHeatRate = -1f;          // 小火 -1°C/s
        [SerializeField] private float heatSwitchCooldown = 1.5f;
        [SerializeField] private float ambientTemperature = 25f;
        [SerializeField] private float minTemperature = 0f;
        [SerializeField] private float maxTemperature = 500f;
        [SerializeField] private float optimalTempRange = 50f;     // ±°C around optimal
        [SerializeField] private float temperaturePenaltyFactor = 0.5f;

        [Header("塑形QTE")]
        [SerializeField] private int totalStrikes = 5;             // 总锤击次数
        [SerializeField] private float strikeCooldown = 0.8f;      // 每次锤击间隔
        [SerializeField] private float forceRangeRatio = 0.2f;     // 完美力道范围 ±20% 目标值
        [SerializeField] private float perfectBonusMultiplier = 1.5f;
        [SerializeField] private float missPenalty = 0.3f;         // 完全脱离范围惩罚

        [Header("淬火液")]
        [SerializeField] private float quenchingBaseScore = 0.7f;  // 基础淬火评分
        [SerializeField] private float quenchLiquidBonus = 0.2f;   // 淬火液加成

        [Header("开光灵力")]
        [SerializeField] private float maxSpiritualPower = 100f;   // 最大可注入灵力
        [SerializeField] private float powerInjectionRate = 20f;   // 每秒注入速度
        [SerializeField] private float overflowPenalty = 0.3f;     // 灵力溢出惩罚
        [SerializeField] private float affinityDecayRate = 2f;     // 未注入时亲和度衰减

        [Header("品质阈值")]
        [SerializeField] private float urThreshold = 0.95f;        // ≥0.95 → UR
        [SerializeField] private float ssrThreshold = 0.80f;       // ≥0.80 → SSR
        [SerializeField] private float srThreshold = 0.60f;        // ≥0.60 → SR
        [SerializeField] private float failThreshold = 0.30f;      // <0.30 → 失败

        [Header("熟练度")]
        [SerializeField] private float profPerCraft = 5f;
        [SerializeField] private float profHighQualityBonus = 3f;
        [SerializeField] private float profFirstCraftBonus = 10f;
        [SerializeField] private float profFailGain = 0.5f;

        #endregion

        #region Private State

        // ─── Active Forge State ───
        private ForgeStage _currentStage = ForgeStage.Idle;
        private ForgeRecipeData _currentRecipe;
        private ForgeAnvilData _currentAnvil;
        private QuenchingLiquid _selectedLiquid = QuenchingLiquid.None;

        // ─── Smelting State ───
        private HeatLevel _currentHeat = HeatLevel.Medium;
#pragma warning disable CS0414 // reserved for future UI display of previous heat level
        private HeatLevel _previousHeat = HeatLevel.Medium;
#pragma warning restore CS0414
        private float _currentTemperature;
        private float _temperatureSum;
        private int _temperatureSamples;
        private float _purityProgress;          // 纯度进展 (0~1)

        // ─── Shaping State ───
        private int _currentStrike;
        private List<ShapingStrikeData> _strikeHistory = new List<ShapingStrikeData>();
        private float _cumulativeShapeScore;
        private float _strikeTimer;
        private float _currentTargetForce;

        // ─── Quenching State ───
        private bool _quenchingApplied;

        // ─── Enlightening State ───
        private float _injectedPower;
        private float _currentAffinity;

        // ─── Timing ───
        private float _stageElapsed;
        private float _stageDuration;
#pragma warning disable CS0414 // reserved for future use (heat switch timing display)
        private float _lastHeatSwitchTime;
#pragma warning restore CS0414
        private float _heatSwitchTimer;

        // ─── Material Input ───
        private List<ForgeMaterialInput> _inputMaterials = new List<ForgeMaterialInput>();
        private bool _allMaterialsInput;

        // ─── Proficiency ───
        private ForgeProficiency _proficiency = new ForgeProficiency();

        // ─── Recipe tracking ───
        private HashSet<string> _craftedRecipes = new HashSet<string>();

        // ─── Result ───
        private ForgeResult _lastResult;

        // ─── Warning cooldown ───
        private float _lastWarningTime;

        #endregion

        #region Public Properties

        public ForgeStage CurrentStage => _currentStage;
        public ForgeRecipeData CurrentRecipe => _currentRecipe;
        public HeatLevel CurrentHeat => _currentHeat;
        public float CurrentTemperature => _currentTemperature;
        public float PurityProgress => _purityProgress;
        public int CurrentStrike => _currentStrike;
        public int TotalStrikes => totalStrikes;
        public float CumulativeShapeScore => _cumulativeShapeScore;
        public float CurrentTargetForce => _currentTargetForce;
        public QuenchingLiquid SelectedLiquid => _selectedLiquid;
        public float InjectedPower => _injectedPower;
        public float MaxSpiritualPower => maxSpiritualPower;
        public float CurrentAffinity => _currentAffinity;
        public float StageElapsed => _stageElapsed;
        public float StageDuration => _stageDuration;
        public bool IsHeatOnCooldown => _heatSwitchTimer < heatSwitchCooldown;
        public float HeatCooldownProgress => Mathf.Clamp01(_heatSwitchTimer / heatSwitchCooldown);
        public ForgeAnvilData ActiveAnvil => _currentAnvil;
        public ForgeProficiency Proficiency => _proficiency;
        public ForgeResult LastResult => _lastResult;
        public bool IsForging => _currentStage >= ForgeStage.Smelting && _currentStage <= ForgeStage.Enlightening;
        public bool IsCompleted => _currentStage == ForgeStage.Complete || _currentStage == ForgeStage.Failed;
        public float StageProgress => _stageDuration > 0f ? Mathf.Clamp01(_stageElapsed / _stageDuration) : 0f;

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

        private void Update()
        {
            if (!IsForging) return;

            float dt = Time.deltaTime;

            switch (_currentStage)
            {
                case ForgeStage.Smelting:
                    UpdateSmelting(dt);
                    break;
                case ForgeStage.Shaping:
                    UpdateShaping(dt);
                    break;
                case ForgeStage.Quenching:
                    // Quenching is player-action driven, no passive update needed.
                    break;
                case ForgeStage.Enlightening:
                    UpdateEnlightening(dt);
                    break;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════
        //  STEP 1: 熔炼 (Smelting)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Update smelting logic each frame.</summary>
        private void UpdateSmelting(float dt)
        {
            // Update heat switch cooldown.
            if (_heatSwitchTimer < heatSwitchCooldown)
            {
                _heatSwitchTimer += dt;
            }

            // Update temperature.
            float rate = GetHeatRate(_currentHeat);
            _currentTemperature += rate * dt;
            _currentTemperature = Mathf.Clamp(_currentTemperature, minTemperature, maxTemperature);

            // Track for running average.
            _temperatureSum += _currentTemperature;
            _temperatureSamples++;

            // Update purity based on temperature proximity to optimal.
            UpdatePurity(dt);

            _stageElapsed += dt;

            // Publish temperature event.
            float deviation = _currentTemperature - _currentRecipe.SmeltingOptimalTemp;
            float purityProgress = _purityProgress;
            EventBus.Publish(new SmeltingTemperatureEvent
            {
                CurrentTemperature = _currentTemperature,
                OptimalTemperature = _currentRecipe.SmeltingOptimalTemp,
                TemperatureDeviation = deviation,
                PurityProgress = purityProgress
            });

            // Fire warning if temperature is dangerously off.
            float absDeviation = Mathf.Abs(deviation);
            float warningSeverity = Mathf.Clamp01(absDeviation / (_currentRecipe.SmeltingOptimalTemp * 0.5f));
            if (warningSeverity > 0.6f && Time.time - _lastWarningTime > 3f)
            {
                _lastWarningTime = Time.time;
                string direction = deviation > 0f ? "过高" : "过低";
                EventBus.Publish(new ForgeWarningEvent
                {
                    WarningType = "temp_deviation",
                    Message = $"炉温{direction}! (当前:{_currentTemperature:F0}°C / 最佳:{_currentRecipe.SmeltingOptimalTemp:F0}°C)",
                    Severity = warningSeverity
                });
            }

            // Check if smelting is complete.
            if (_purityProgress >= 1f && _stageElapsed >= _stageDuration * 0.8f)
            {
                AdvanceToNextStage();
            }
            else if (_stageElapsed >= _stageDuration)
            {
                // Force-complete even if purity not maxed.
                AdvanceToNextStage();
            }
        }

        /// <summary>Update purity: better temp = faster purity growth.</summary>
        private void UpdatePurity(float dt)
        {
            float deviation = Mathf.Abs(_currentTemperature - _currentRecipe.SmeltingOptimalTemp);
            float optimalRange = _currentRecipe.SmeltingOptimalTemp * 0.15f;
            float purityRate;

            if (deviation <= optimalRange)
            {
                // Optimal range: fastest purity gain.
                purityRate = 0.15f;
            }
            else if (deviation <= optimalRange * 2f)
            {
                // Near range: moderate gain.
                purityRate = 0.08f;
            }
            else if (deviation <= optimalRange * 3f)
            {
                // Far range: slow gain.
                purityRate = 0.03f;
            }
            else
            {
                // Danger zone: very slow or stagnant.
                purityRate = 0.01f;
            }

            // Apply proficiency temperature tolerance bonus.
            purityRate += _proficiency.TemperatureToleranceBonus;

            _purityProgress = Mathf.Clamp01(_purityProgress + purityRate * dt);
        }

        // ══════════════════════════════════════════════════════════════════
        //  STEP 2: 塑形 (Shaping)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Update shaping QTE timer.</summary>
        private void UpdateShaping(float dt)
        {
            _strikeTimer += dt;

            // Randomize target force periodically for visual feedback.
            // The target force is set when player calls Strike().
        }

        /// <summary>
        /// Player performs a hammer strike during shaping phase.
        /// Called from UI/input when the player clicks the strike button.
        /// Returns the accuracy of the strike (0~1).
        /// </summary>
        public float PerformStrike(float appliedForce)
        {
            if (_currentStage != ForgeStage.Shaping)
            {
                Debug.LogWarning("[ForgeController] Not in shaping phase.");
                return 0f;
            }

            if (_strikeTimer < strikeCooldown)
            {
                float remaining = strikeCooldown - _strikeTimer;
                EventBus.Publish(new ForgeWarningEvent
                {
                    WarningType = "strike_miss",
                    Message = $"锤击冷却中... 剩余 {remaining:F1}s",
                    Severity = 0.3f
                });
                return 0f;
            }

            if (_currentStrike >= totalStrikes)
            {
                Debug.LogWarning("[ForgeController] All strikes completed.");
                return 0f;
            }

            _strikeTimer = 0f;

            // Generate a new target force for this strike.
            _currentTargetForce = GenerateTargetForce(_currentStrike, totalStrikes);

            // Calculate accuracy.
            float forceDiff = Mathf.Abs(appliedForce - _currentTargetForce);
            float forceRange = forceRangeRatio;

            float accuracy;
            if (forceDiff <= forceRange)
            {
                // Perfect hit: accuracy = 1.0, with bonus.
                accuracy = 1f;
            }
            else if (forceDiff <= forceRange * 3f)
            {
                // Good hit: linear falloff from 1.0 to 0.4
                float excess = (forceDiff - forceRange) / (forceRange * 2f);
                accuracy = Mathf.Lerp(1f, 0.4f, excess);
            }
            else
            {
                // Miss: heavy penalty.
                accuracy = Mathf.Max(0f, 0.4f - (forceDiff - forceRange * 3f) * missPenalty);
            }

            // Apply proficiency shape accuracy bonus.
            accuracy = Mathf.Clamp01(accuracy + _proficiency.ShapeAccuracyBonus);

            // Calculate score contribution.
            float contribution = accuracy / totalStrikes;
            if (accuracy >= 0.95f)
            {
                // Perfect strike bonus.
                contribution *= perfectBonusMultiplier;
            }
            contribution = Mathf.Clamp(contribution, 0f, 1f / totalStrikes * perfectBonusMultiplier);

            _cumulativeShapeScore += contribution;
            _cumulativeShapeScore = Mathf.Clamp01(_cumulativeShapeScore);

            // Record strike.
            var strikeData = new ShapingStrikeData
            {
                StrikeIndex = _currentStrike,
                TargetForce = _currentTargetForce,
                AppliedForce = appliedForce,
                Accuracy = accuracy,
                ScoreContribution = contribution
            };
            _strikeHistory.Add(strikeData);

            _currentStrike++;

            // Publish strike event.
            EventBus.Publish(new ShapingStrikeEvent
            {
                CurrentStrike = _currentStrike,
                TotalStrikes = totalStrikes,
                TargetForce = _currentTargetForce,
                ForceRangeMin = Mathf.Max(0f, _currentTargetForce - forceRange),
                ForceRangeMax = Mathf.Min(1f, _currentTargetForce + forceRange),
                IsPerfect = accuracy >= 0.95f,
                Accuracy = accuracy,
                CumulativeShapeScore = _cumulativeShapeScore
            });

            Debug.Log($"[ForgeController] 锤击 #{_currentStrike}/{totalStrikes}: " +
                      $"目标={_currentTargetForce:F2}, 施力={appliedForce:F2}, " +
                      $"精度={accuracy:F3}{(accuracy >= 0.95f ? " [完美!]" : "")}");

            // Check if shaping is complete.
            if (_currentStrike >= totalStrikes)
            {
                Debug.Log($"[ForgeController] 塑形完成! 总分: {_cumulativeShapeScore:F3}");
                AdvanceToNextStage();
            }

            return accuracy;
        }

        /// <summary>Generate a target force for the given strike index.</summary>
        private float GenerateTargetForce(int strikeIndex, int total)
        {
            // Progressively harder: early strikes have moderate targets,
            // later strikes become more varied.
            float progress = (float)strikeIndex / Mathf.Max(1, total - 1);

            // Base target varies by stage of shaping.
            float baseTarget;
            if (progress < 0.33f)
            {
                // Early: gentle shaping — moderate force.
                baseTarget = UnityEngine.Random.Range(0.35f, 0.55f);
            }
            else if (progress < 0.66f)
            {
                // Middle: main shaping — higher force.
                baseTarget = UnityEngine.Random.Range(0.50f, 0.75f);
            }
            else
            {
                // Final: fine finishing — varied force.
                baseTarget = UnityEngine.Random.Range(0.30f, 0.80f);
            }

            return Mathf.Clamp01(baseTarget);
        }

        // ══════════════════════════════════════════════════════════════════
        //  STEP 3: 淬火 (Quenching)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Player selects a quenching liquid for the forging process.
        /// Must be called during the Quenching stage.
        /// </summary>
        public bool SelectQuenchingLiquid(QuenchingLiquid liquidType)
        {
            if (_currentStage != ForgeStage.Quenching)
            {
                Debug.LogWarning("[ForgeController] Not in quenching phase.");
                return false;
            }

            if (liquidType == QuenchingLiquid.None)
            {
                Debug.LogWarning("[ForgeController] Invalid quenching liquid.");
                return false;
            }

            if (!QuenchingLiquidDataMap.ContainsKey(liquidType))
            {
                Debug.LogWarning($"[ForgeController] Unknown quenching liquid: {liquidType}");
                return false;
            }

            _selectedLiquid = liquidType;
            _quenchingApplied = true;

            QuenchingLiquidData liquidData = QuenchingLiquidDataMap[liquidType];

            EventBus.Publish(new QuenchingSelectedEvent
            {
                LiquidType = liquidType,
                LiquidName = liquidData.DisplayName,
                AffinityBonus = liquidData.AffinityBonus,
                StatBonusType = liquidData.StatBonusType
            });

            Debug.Log($"[ForgeController] 选择淬火液: {liquidData.DisplayName} " +
                      $"(亲和+{liquidData.AffinityBonus}, 属性: {liquidData.StatBonusType})");

            // Auto-advance to next stage.
            AdvanceToNextStage();

            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  STEP 4: 开光 (Enlightening)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Update enlightening logic each frame.</summary>
        private void UpdateEnlightening(float dt)
        {
            _stageElapsed += dt;

            // If player is not actively injecting, affinity decays.
            if (_injectedPower < _stageElapsed * 0.5f)
            {
                // Player is not injecting fast enough — decay affinity.
                _currentAffinity = Mathf.Max(0f, _currentAffinity - affinityDecayRate * dt);
            }

            // Publish progress event.
            EventBus.Publish(new EnlighteningProgressEvent
            {
                SpiritualPowerInjected = _injectedPower,
                MaxSpiritualPower = maxSpiritualPower,
                Affinity = _currentAffinity,
                Progress = Mathf.Clamp01(_injectedPower / maxSpiritualPower)
            });

            // Check for time-out auto-complete.
            if (_stageElapsed >= _stageDuration)
            {
                Debug.Log($"[ForgeController] 开光阶段超时, 注入灵力: {_injectedPower:F1}, 亲和度: {_currentAffinity:F3}");
                AdvanceToNextStage();
            }
        }

        /// <summary>
        /// Player injects spiritual power during the enlightening phase.
        /// Called each frame or on button hold from input/UI.
        /// Returns the current affinity after injection.
        /// </summary>
        public float InjectSpiritualPower(float amount)
        {
            if (_currentStage != ForgeStage.Enlightening)
            {
                return _currentAffinity;
            }

            float previousPower = _injectedPower;
            _injectedPower += amount * Time.deltaTime;

            if (_injectedPower > maxSpiritualPower)
            {
                // Overflow: penalty to affinity.
                float overflow = _injectedPower - maxSpiritualPower;
                _currentAffinity -= overflow * overflowPenalty * Time.deltaTime;
                _injectedPower = maxSpiritualPower;
            }

            // Affinity grows with injected power, with diminishing returns.
            float injectionRatio = _injectedPower / maxSpiritualPower;

            // Base affinity from injection.
            float targetAffinity = Mathf.Pow(injectionRatio, 0.7f);

            // Apply proficiency quench bonus.
            targetAffinity *= _proficiency.QuenchBonus;

            // Apply selected liquid's affinity bonus.
            if (_selectedLiquid != QuenchingLiquid.None && QuenchingLiquidDataMap.TryGetValue(_selectedLiquid, out var liquidData))
            {
                targetAffinity += liquidData.AffinityBonus;
            }

            // Smoothly move current affinity toward target.
            _currentAffinity = Mathf.Lerp(_currentAffinity, Mathf.Clamp01(targetAffinity), Time.deltaTime * 2f);

            return _currentAffinity;
        }

        /// <summary>Complete the enlightening phase manually (player chooses to stop injecting).</summary>
        public void CompleteEnlightening()
        {
            if (_currentStage != ForgeStage.Enlightening) return;
            AdvanceToNextStage();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Stage Management
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Advance to the next forge stage.</summary>
        private void AdvanceToNextStage()
        {
            ForgeStage previousStage = _currentStage;
            ForgeStage nextStage = GetNextStage(_currentStage);

            if (nextStage == ForgeStage.Complete)
            {
                // Forging is complete — calculate result.
                CompleteForging();
                return;
            }

            _currentStage = nextStage;
            _stageElapsed = 0f;
            _stageDuration = GetStageDuration(nextStage);

            // Reset stage-specific state.
            switch (nextStage)
            {
                case ForgeStage.Shaping:
                    _currentStrike = 0;
                    _cumulativeShapeScore = 0f;
                    _strikeHistory.Clear();
                    _strikeTimer = strikeCooldown; // Ready immediately.
                    _currentTargetForce = 0f;
                    break;
                case ForgeStage.Quenching:
                    _selectedLiquid = QuenchingLiquid.None;
                    _quenchingApplied = false;
                    break;
                case ForgeStage.Enlightening:
                    _injectedPower = 0f;
                    _currentAffinity = 0f;
                    break;
            }

            EventBus.Publish(new ForgeStageChangedEvent
            {
                NewStage = _currentStage,
                PreviousStage = previousStage,
                Progress = 0f
            });

            Debug.Log($"[ForgeController] 阶段切换: {GetStageDisplayName(previousStage)} → {GetStageDisplayName(_currentStage)}");
        }

        /// <summary>Get the next stage in the forging sequence.</summary>
        private static ForgeStage GetNextStage(ForgeStage current)
        {
            return current switch
            {
                ForgeStage.Smelting     => ForgeStage.Shaping,
                ForgeStage.Shaping      => ForgeStage.Quenching,
                ForgeStage.Quenching    => ForgeStage.Enlightening,
                ForgeStage.Enlightening => ForgeStage.Complete,
                ForgeStage.Complete     => ForgeStage.Complete,
                ForgeStage.Failed       => ForgeStage.Failed,
                _                       => ForgeStage.Complete
            };
        }

        /// <summary>Get the duration for a given stage.</summary>
        private float GetStageDuration(ForgeStage stage)
        {
            return stage switch
            {
                ForgeStage.Smelting     => _currentRecipe.SmeltingDuration,
                ForgeStage.Shaping      => totalStrikes * strikeCooldown + 1f,
                ForgeStage.Quenching    => 5f,
                ForgeStage.Enlightening => 15f,
                _                       => 0f
            };
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

        // ══════════════════════════════════════════════════════════════════
        //  Heat Level (shared with AlchemyController)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Switch the current heat level. Returns true if switch was applied.
        /// Respects cooldown.
        /// </summary>
        public bool SwitchHeat(HeatLevel targetHeat)
        {
            if (_currentStage != ForgeStage.Smelting)
            {
                Debug.LogWarning("[ForgeController] Can only switch heat during smelting phase.");
                return false;
            }

            if (_currentHeat == targetHeat) return false;

            if (_heatSwitchTimer < heatSwitchCooldown)
            {
                float remaining = heatSwitchCooldown - _heatSwitchTimer;
                Debug.Log($"[ForgeController] 火候切换冷却中... 剩余 {remaining:F1}s");
                return false;
            }

            _previousHeat = _currentHeat;
            _currentHeat = targetHeat;
            _lastHeatSwitchTime = Time.time;
            _heatSwitchTimer = 0f;

            Debug.Log($"[ForgeController] 火候切换: {AlchemyController.GetHeatDisplayName(_previousHeat)} → {AlchemyController.GetHeatDisplayName(_currentHeat)}");
            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API: Forge Lifecycle
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Start a new forging session with the given recipe and anvil.
        /// Returns false if already forging or preconditions not met.
        /// </summary>
        public bool StartForging(ForgeRecipeData recipe, ForgeAnvilData anvil)
        {
            if (IsForging)
            {
                Debug.LogWarning("[ForgeController] Already forging.");
                return false;
            }

            if (recipe.SmeltingDuration <= 0f)
            {
                Debug.LogError("[ForgeController] Invalid recipe duration.");
                return false;
            }

            if (anvil.CurrentDurability <= 0f)
            {
                Debug.LogWarning("[ForgeController] Anvil is broken.");
                return false;
            }

            if (_proficiency.Level < recipe.RequiredProficiency)
            {
                Debug.LogWarning($"[ForgeController] Proficiency too low. Required: {recipe.RequiredProficiency}, Have: {_proficiency.Level}");
                return false;
            }

            // Initialize state.
            _currentRecipe = recipe;
            _currentAnvil = anvil;
            _currentStage = ForgeStage.Smelting;
            _currentHeat = HeatLevel.Medium;
            _previousHeat = HeatLevel.Medium;
            _currentTemperature = ambientTemperature;
            _temperatureSum = ambientTemperature;
            _temperatureSamples = 1;
            _purityProgress = 0f;
            _stageElapsed = 0f;
            _stageDuration = recipe.SmeltingDuration;
            _lastHeatSwitchTime = Time.time;
            _heatSwitchTimer = heatSwitchCooldown;
            _inputMaterials.Clear();
            _allMaterialsInput = false;

            // Shaping state.
            _currentStrike = 0;
            _cumulativeShapeScore = 0f;
            _strikeHistory.Clear();
            _strikeTimer = 0f;
            _currentTargetForce = 0f;

            // Quenching state.
            _selectedLiquid = QuenchingLiquid.None;
            _quenchingApplied = false;

            // Enlightening state.
            _injectedPower = 0f;
            _currentAffinity = 0f;

            // Publish start event.
            EventBus.Publish(new ForgeStartedEvent
            {
                RecipeId = recipe.Id,
                RecipeName = recipe.DisplayName,
                SmeltingDuration = recipe.SmeltingDuration,
                OptimalTemperature = recipe.SmeltingOptimalTemp,
                InitialStage = ForgeStage.Smelting
            });

            // Publish initial stage event.
            EventBus.Publish(new ForgeStageChangedEvent
            {
                NewStage = ForgeStage.Smelting,
                PreviousStage = ForgeStage.Idle,
                Progress = 0f
            });

            Debug.Log($"[ForgeController] 开始炼器: {recipe.DisplayName} " +
                      $"(熔炼时长: {recipe.SmeltingDuration}s, 最佳温度: {recipe.SmeltingOptimalTemp}°C, " +
                      $"炼器台: {anvil.DisplayName})");
            return true;
        }

        /// <summary>
        /// Input a material into the forge.
        /// </summary>
        public bool InputMaterial(ForgeMaterialInput material)
        {
            if (!IsForging)
            {
                Debug.LogWarning("[ForgeController] Not currently forging.");
                return false;
            }

            if (_allMaterialsInput)
            {
                Debug.LogWarning("[ForgeController] All materials already input.");
                return false;
            }

            material.InputOrderIndex = _inputMaterials.Count;
            _inputMaterials.Add(material);

            _allMaterialsInput = _currentRecipe.RecommendedMaterials == null ||
                                 _inputMaterials.Count >= _currentRecipe.RecommendedMaterials.Length;

            Debug.Log($"[ForgeController] 投入材料: {material.DisplayName} (品质系数: {material.QualityCoefficient})");

            return true;
        }

        /// <summary>
        /// Complete the forging process and calculate the final result.
        /// Called automatically when all stages complete.
        /// </summary>
        private void CompleteForging()
        {
            if (_currentStage == ForgeStage.Complete)
                return;

            ForgeStage previousStage = _currentStage;
            _currentStage = ForgeStage.Complete;

            // Calculate result.
            ForgeResult result = CalculateFinalResult();

            // Proficiency gain.
            float profGain = profPerCraft;
            if (result.Quality >= EquipmentQuality.SSR) profGain += profHighQualityBonus;
            if (!_craftedRecipes.Contains(_currentRecipe.Id))
            {
                profGain += profFirstCraftBonus;
                _craftedRecipes.Add(_currentRecipe.Id);
            }
            _proficiency.AddExp(profGain);

            // Store result.
            _lastResult = result;

            // Publish stage change.
            EventBus.Publish(new ForgeStageChangedEvent
            {
                NewStage = ForgeStage.Complete,
                PreviousStage = previousStage,
                Progress = 1f
            });

            // Publish completion event.
            EventBus.Publish(new ForgeCompletedEvent
            {
                EquipmentId = result.EquipmentId,
                EquipmentName = result.EquipmentName,
                Quality = result.Quality,
                FinalQuality = result.FinalQuality,
                FinalStats = result.FinalStats,
                AffixCount = result.AffixCount,
                Affixes = result.Affixes,
                ProficiencyGained = profGain,
                QualityColor = result.QualityColor
            });

            // Publish proficiency change.
            PublishProficiencyEvent();

            Debug.Log($"[ForgeController] 炼器完成! " +
                      $"品质: {GetQualityDisplayName(result.Quality)} " +
                      $"(评分: {result.FinalQuality:F3}) " +
                      $"词缀: {result.AffixCount}条" +
                      $"\n  纯度={result.PurityScore:F3}, 塑形={result.ShapeScore:F3}, " +
                      $"淬火={result.QuenchScore:F3}, 开光={result.EnlightenScore:F3}");
        }

        /// <summary>
        /// Force-fail the current forging (e.g., player walks away, interrupted).
        /// </summary>
        public void FailForging(string reason = "炼器被打断")
        {
            if (!IsForging) return;

            ForgeStage previousStage = _currentStage;
            _currentStage = ForgeStage.Failed;

            EventBus.Publish(new ForgeFailedEvent
            {
                FailReason = reason,
                MaterialsLostPercent = 1f,
                AnvilDurabilityLoss = 10f
            });

            EventBus.Publish(new ForgeStageChangedEvent
            {
                NewStage = ForgeStage.Failed,
                PreviousStage = previousStage,
                Progress = 0f
            });

            Debug.Log($"[ForgeController] 炼器失败: {reason}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quality & Result Calculation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the final forging result using all four stage scores:
        /// FinalQuality = Base × PurityScore × ShapeScore × QuenchScore × EnlightenScore × EquipMod × ProfBonus
        /// </summary>
        private ForgeResult CalculateFinalResult()
        {
            // 1. Base quality from recipe range.
            float baseQuality = UnityEngine.Random.Range(_currentRecipe.BaseQualityMin, _currentRecipe.BaseQualityMax);

            // 2. Purity score (from smelting temperature control).
            float purityScore = CalculatePurityScore();

            // 3. Shape score (from QTE hammer strikes).
            float shapeScore = _cumulativeShapeScore;

            // 4. Quench score (from selected liquid).
            float quenchScore = CalculateQuenchScore();

            // 5. Enlighten score (from spiritual power injection).
            float enlightenScore = CalculateEnlightenScore();

            // 6. Equipment modifier (anvil quality).
            float equipMod = _currentAnvil.QualityCoefficient * (1f - _currentAnvil.WearFactor * 0.01f);

            // 7. Proficiency quality bonus.
            float profBonus = _proficiency.QualityBonus;

            // 8. Apply UR bonus from proficiency.
            float urBonus = 1f + _proficiency.UrQualityBonus;

            // Final quality.
            float finalQuality = baseQuality * purityScore * shapeScore * quenchScore * enlightenScore * equipMod * profBonus * urBonus;

            // Determine equipment quality tier.
            EquipmentQuality quality = QualityFromScore(finalQuality);

            // Determine affixes.
            int affixCount = AffixCountFromQuality(finalQuality);
            EquipmentAffix[] affixes = affixCount > 0 ? GenerateAffixes(affixCount, quality, _selectedLiquid) : Array.Empty<EquipmentAffix>();

            // Calculate stats.
            float baseStats = _currentRecipe.BaseStatsMultiplier * _currentAnvil.QualityCoefficient;
            float matScore = CalculateMaterialScore();
            float finalStats = baseStats * matScore * finalQuality * urBonus;

            // Apply quenching stat multiplier.
            if (_selectedLiquid != QuenchingLiquid.None && QuenchingLiquidDataMap.TryGetValue(_selectedLiquid, out var liquidData))
            {
                finalStats *= liquidData.PropertyMultiplier;
            }

            // Build equipment ID.
            string equipId = $"{_currentRecipe.Id}_{Guid.NewGuid():N}";
            string equipName = _currentRecipe.DisplayName;

            return new ForgeResult
            {
                EquipmentId = equipId,
                EquipmentName = equipName,
                Quality = quality,
                FinalQuality = finalQuality,
                BaseStats = baseStats,
                FinalStats = finalStats,
                PurityScore = purityScore,
                ShapeScore = shapeScore,
                QuenchScore = quenchScore,
                EnlightenScore = enlightenScore,
                UsedLiquid = _selectedLiquid,
                Affixes = affixes,
                AffixCount = affixCount,
                QualityColor = GetQualityColor(quality)
            };
        }

        /// <summary>Calculate purity score based on smelting performance.</summary>
        private float CalculatePurityScore()
        {
            if (_temperatureSamples == 0) return 0.5f;

            float avgTemp = _temperatureSum / _temperatureSamples;
            float optimal = _currentRecipe.SmeltingOptimalTemp;

            // TemperatureScore = 1.0 - (|ActualAvgTemp - OptimalTemp| / OptimalTemp) × 0.5
            float deviation = Mathf.Abs(avgTemp - optimal);
            float score = 1f - (deviation / Mathf.Max(optimal, 1f)) * temperaturePenaltyFactor;

            // Purity progress adds a bonus if fully purified.
            score += _purityProgress * 0.1f;

            // Apply proficiency temperature tolerance bonus.
            score += _proficiency.TemperatureToleranceBonus;

            return Mathf.Clamp01(score);
        }

        /// <summary>Calculate quench score based on selected liquid.</summary>
        private float CalculateQuenchScore()
        {
            float score = quenchingBaseScore;

            if (_selectedLiquid != QuenchingLiquid.None)
            {
                score += quenchLiquidBonus;

                if (QuenchingLiquidDataMap.TryGetValue(_selectedLiquid, out var liquidData))
                {
                    score += liquidData.AffinityBonus;
                }
            }

            // Apply proficiency quench bonus.
            score *= _proficiency.QuenchBonus;

            return Mathf.Clamp(score, 0.1f, 1.0f);
        }

        /// <summary>Calculate enlighten score based on spiritual power injection.</summary>
        private float CalculateEnlightenScore()
        {
            // Score based on affinity achieved.
            float score = _currentAffinity;

            // Bonus if player hit max spiritual power without overflow.
            if (_injectedPower >= maxSpiritualPower * 0.95f && _injectedPower <= maxSpiritualPower * 1.05f)
            {
                score += 0.1f; // Near-perfect injection bonus.
            }

            return Mathf.Clamp01(score);
        }

        /// <summary>Calculate material score: average quality coefficient of inputs.</summary>
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

        /// <summary>Determine equipment quality tier from a quality score.</summary>
        public EquipmentQuality QualityFromScore(float score)
        {
            foreach (var (threshold, quality) in QualityThresholds)
            {
                if (score >= threshold) return quality;
            }
            return EquipmentQuality.Fail;
        }

        /// <summary>Determine affix count from quality score using thresholds.</summary>
        public static int AffixCountFromQuality(float finalQuality)
        {
            foreach (var (threshold, count) in AffixCountThresholds)
            {
                if (finalQuality >= threshold) return count;
            }
            return 0;
        }

        /// <summary>Generate random affixes for the forged equipment.</summary>
        private static EquipmentAffix[] GenerateAffixes(int count, EquipmentQuality quality, QuenchingLiquid liquid)
        {
            if (count <= 0) return Array.Empty<EquipmentAffix>();

            var affixes = new EquipmentAffix[count];
            var usedTypes = new HashSet<AffixType>();

            // Ensure first affix matches quenching liquid if applicable.
            if (count >= 1 && liquid != QuenchingLiquid.None)
            {
                AffixType liquidAffix = LiquidToAffixType(liquid);
                affixes[0] = CreateAffix(liquidAffix, quality);
                usedTypes.Add(liquidAffix);
            }

            // Fill remaining affixes randomly.
            var availableAffixes = GetAvailableAffixTypes(usedTypes);
            for (int i = (liquid != QuenchingLiquid.None ? 1 : 0); i < count; i++)
            {
                if (availableAffixes.Count == 0) break;
                AffixType chosen = availableAffixes[UnityEngine.Random.Range(0, availableAffixes.Count)];
                affixes[i] = CreateAffix(chosen, quality);
                usedTypes.Add(chosen);
                availableAffixes.Remove(chosen);
            }

            return affixes;
        }

        /// <summary>Map a quenching liquid to its primary affix type.</summary>
        private static AffixType LiquidToAffixType(QuenchingLiquid liquid)
        {
            return liquid switch
            {
                QuenchingLiquid.SpiritSpring => AffixType.Spirit,
                QuenchingLiquid.BeastBlood   => AffixType.Attack,
                _                            => AffixType.Attack
            };
        }

        /// <summary>Get list of affix types not yet used.</summary>
        private static List<AffixType> GetAvailableAffixTypes(HashSet<AffixType> used)
        {
            var all = new List<AffixType> { AffixType.Attack, AffixType.Defense, AffixType.CritRate, AffixType.CritDamage, AffixType.Speed, AffixType.LifeSteal, AffixType.Spirit, AffixType.Resistance };
            return all.FindAll(a => !used.Contains(a));
        }

        /// <summary>Create an affix with a value based on quality tier.</summary>
        private static EquipmentAffix CreateAffix(AffixType type, EquipmentQuality quality)
        {
            float baseValue = quality switch
            {
                EquipmentQuality.UR  => UnityEngine.Random.Range(0.15f, 0.25f),
                EquipmentQuality.SSR => UnityEngine.Random.Range(0.10f, 0.18f),
                EquipmentQuality.SR  => UnityEngine.Random.Range(0.06f, 0.12f),
                EquipmentQuality.R   => UnityEngine.Random.Range(0.03f, 0.08f),
                _                    => 0.05f
            };

            string displayName = type switch
            {
                AffixType.Attack     => "攻击",
                AffixType.Defense    => "防御",
                AffixType.CritRate   => "暴击率",
                AffixType.CritDamage => "暴击伤害",
                AffixType.Speed      => "速度",
                AffixType.LifeSteal  => "吸血",
                AffixType.Spirit     => "灵力",
                AffixType.Resistance => "抗性",
                _                    => "未知"
            };

            return new EquipmentAffix
            {
                Type = type,
                DisplayName = displayName,
                Value = baseValue,
                Description = $"{displayName}+{baseValue * 100:F1}%"
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Enhancement System
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempt to enhance an equipment by one level.
        ///
        /// Formula: EnhanceChance = 0.8 × QualityMod × (1 - Level × 0.1)
        ///
        /// Caps: R = 5, SR = 7, SSR = 9, UR=10
        /// </summary>
        /// <param name="equipmentName">The equipment display name (for logging).</param>
        /// <param name="quality">The equipment quality.</param>
        /// <param name="currentLevel">Current enhance level (0-based).</param>
        /// <returns>True if enhancement succeeded.</returns>
        public static bool TryEnhance(string equipmentName, EquipmentQuality quality, int currentLevel, out int newLevel)
        {
            newLevel = currentLevel;

            if (quality == EquipmentQuality.Fail) return false;

            // Check cap.
            if (!EnhanceLevelCaps.TryGetValue(quality, out int cap))
                return false;

            if (currentLevel >= cap)
            {
                Debug.Log($"[ForgeController] {equipmentName} 已达强化上限 ({cap}级)");
                return false;
            }

            // Get quality modifier.
            if (!QualityModifiers.TryGetValue(quality, out float qualityMod))
                return false;

            // Calculate chance: EnhanceChance = 0.8 × QualityMod × (1 - Level × 0.1)
            float chance = 0.8f * qualityMod * (1f - currentLevel * 0.1f);
            chance = Mathf.Clamp01(chance);

            bool success = UnityEngine.Random.value < chance;

            if (success)
            {
                newLevel = currentLevel + 1;
            }

            Debug.Log($"[ForgeController] 强化{(success ? "成功" : "失败")}: " +
                      $"{equipmentName} [{GetQualityDisplayName(quality)}] " +
                      $"Lv.{currentLevel} → {(success ? $"Lv.{newLevel}" : "不变")} " +
                      $"(概率: {chance * 100:F1}%)");

            return success;
        }

        /// <summary>Get the max enhance level for a given quality.</summary>
        public static int GetMaxEnhanceLevel(EquipmentQuality quality)
        {
            return EnhanceLevelCaps.TryGetValue(quality, out int cap) ? cap : 0;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Display Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Get the stage name in Chinese.</summary>
        public static string GetStageDisplayName(ForgeStage stage)
        {
            return stage switch
            {
                ForgeStage.Idle          => "待机",
                ForgeStage.Smelting      => "熔炼期",
                ForgeStage.Shaping       => "塑形期",
                ForgeStage.Quenching     => "淬火期",
                ForgeStage.Enlightening  => "开光期",
                ForgeStage.Complete      => "成器",
                ForgeStage.Failed        => "失败",
                _                        => "未知"
            };
        }

        /// <summary>Get Chinese display name for quality tier.</summary>
        public static string GetQualityDisplayName(EquipmentQuality quality)
        {
            return quality switch
            {
                EquipmentQuality.Fail => "失败",
                EquipmentQuality.R    => "R",
                EquipmentQuality.SR   => "SR",
                EquipmentQuality.SSR  => "SSR",
                EquipmentQuality.UR   => "UR",
                _                     => "未知"
            };
        }

        /// <summary>Get color for each quality tier.</summary>
        public static Color GetQualityColor(EquipmentQuality quality)
        {
            return quality switch
            {
                EquipmentQuality.Fail => new Color(0.5f, 0.5f, 0.5f),   // gray
                EquipmentQuality.R    => Color.white,                     // white
                EquipmentQuality.SR   => new Color(0.2f, 0.6f, 1.0f),    // blue
                EquipmentQuality.SSR  => new Color(0.7f, 0.2f, 0.9f),    // purple
                EquipmentQuality.UR   => new Color(1.0f, 0.7f, 0.0f),    // gold
                _                     => Color.white
            };
        }

        /// <summary>Get quenching liquid display name.</summary>
        public static string GetLiquidDisplayName(QuenchingLiquid liquid)
        {
            return liquid switch
            {
                QuenchingLiquid.SpiritSpring => "灵泉",
                QuenchingLiquid.BeastBlood   => "妖兽血",
                QuenchingLiquid.None         => "未选择",
                _                            => "未知"
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Proficiency
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Publish proficiency change event.</summary>
        private void PublishProficiencyEvent()
        {
            EventBus.Publish(new ForgeProficiencyChangedEvent
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
                Debug.Log($"[ForgeController] 炼器熟练度提升! Lv.{_proficiency.Level} — {_proficiency.GetTitle()}");
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

        // ══════════════════════════════════════════════════════════════════
        //  Equipment (Anvil) Management
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Set the active anvil.</summary>
        public void SetAnvil(ForgeAnvilData anvil)
        {
            _currentAnvil = anvil;
        }

        /// <summary>Repair the active anvil by a given amount.</summary>
        public void RepairAnvil(float amount)
        {
            ForgeAnvilData repaired = _currentAnvil;
            repaired.CurrentDurability = Mathf.Min(_currentAnvil.MaxDurability, _currentAnvil.CurrentDurability + amount);
            _currentAnvil = repaired;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Debug / Editor Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            string stageStr = GetStageDisplayName(_currentStage);
            string heatStr = _currentStage == ForgeStage.Smelting ? AlchemyController.GetHeatDisplayName(_currentHeat) : "N/A";
            string qualityStr = IsCompleted ? GetQualityDisplayName(_lastResult.Quality) : "进行中";
            string liquidStr = _selectedLiquid != QuenchingLiquid.None ? GetLiquidDisplayName(_selectedLiquid) : "无";

            return $"=== ForgeController Status ===\n" +
                   $"Stage: {stageStr} ({StageProgress * 100:F1}%)\n" +
                   $"Heat: {heatStr} (CD: {HeatCooldownProgress * 100:F0}%)\n" +
                   $"Temp: {_currentTemperature:F1}°C\n" +
                   $"Purity: {_purityProgress * 100:F1}%\n" +
                   $"Shaping: {_currentStrike}/{totalStrikes} strikes (Score: {_cumulativeShapeScore:F3})\n" +
                   $"Liquid: {liquidStr}\n" +
                   $"Spiritual Power: {_injectedPower:F1}/{maxSpiritualPower:F0} (Affinity: {_currentAffinity:F3})\n" +
                   $"Anvil: {_currentAnvil.CurrentDurability}/{_currentAnvil.MaxDurability} ({_currentAnvil.WearFactor * 100:F0}% wear)\n" +
                   $"Proficiency: Lv.{_proficiency.Level} ({_proficiency.GetTitle()}) [{GetProficiencyProgress():P1}]\n" +
                   $"Last Result: {qualityStr}";
        }

        /// <summary>Create a test recipe for debugging.</summary>
        public ForgeRecipeData CreateTestRecipe(string name = "青锋剑",
                                                 float optimalTemp = 200f,
                                                 float duration = 40f)
        {
            return new ForgeRecipeData
            {
                Id = "recipe_forge_test_" + Guid.NewGuid().ToString("N"),
                DisplayName = name,
                Description = "测试用基础炼器配方",
                BaseQualityMin = 0.3f,
                BaseQualityMax = 0.8f,
                SmeltingOptimalTemp = optimalTemp,
                SmeltingDuration = duration,
                RequiredProficiency = 1,
                Difficulty = 1,
                RecommendedMaterials = new[] { "mat_iron_01", "mat_crystal_02", "mat_essence_03" },
                MinQuality = EquipmentQuality.R,
                BaseStatsMultiplier = 1.0f
            };
        }

        /// <summary>Create a test anvil for debugging.</summary>
        public ForgeAnvilData CreateTestAnvil(string name = "新手炼器台",
                                               float qualityCoeff = 0.8f,
                                               float maxDurability = 200f)
        {
            return new ForgeAnvilData
            {
                Id = "anvil_test_" + Guid.NewGuid().ToString("N"),
                DisplayName = name,
                QualityCoefficient = qualityCoeff,
                MaxDurability = maxDurability,
                CurrentDurability = maxDurability
            };
        }

        /// <summary>
        /// Quick automated forging test.
        /// Simulates the full 4-step process and returns the result.
        /// </summary>
        public ForgeResult RunTestForging(ForgeRecipeData recipe,
                                           ForgeAnvilData anvil,
                                           ForgeMaterialInput[] materials,
                                           QuenchingLiquid quenchingLiquid,
                                           float[] strikeForces = null,
                                           float injectPowerAmount = 80f)
        {
            if (!StartForging(recipe, anvil))
                return default;

            // Input materials.
            foreach (var mat in materials)
            {
                InputMaterial(mat);
            }

            // Simulate smelting with optimal temperature.
            float simulateStep = 0.5f;
            float elapsed = 0f;

            while (elapsed < recipe.SmeltingDuration)
            {
                // Try to maintain optimal temperature.
                float diff = _currentTemperature - recipe.SmeltingOptimalTemp;
                HeatLevel desiredHeat;
                if (diff < -30f)
                    desiredHeat = HeatLevel.High;
                else if (diff > 30f)
                    desiredHeat = HeatLevel.Low;
                else
                    desiredHeat = HeatLevel.Medium;

                if (_currentHeat != desiredHeat && _heatSwitchTimer >= heatSwitchCooldown)
                {
                    _previousHeat = _currentHeat;
                    _currentHeat = desiredHeat;
                    _heatSwitchTimer = 0f;
                }

                // Simulate time.
                elapsed += simulateStep;
                _stageElapsed = elapsed;
                _currentTemperature += GetHeatRate(_currentHeat) * simulateStep;
                _currentTemperature = Mathf.Clamp(_currentTemperature, minTemperature, maxTemperature);
                _temperatureSum += _currentTemperature;
                _temperatureSamples++;

                // Update purity.
                float deviation = Mathf.Abs(_currentTemperature - recipe.SmeltingOptimalTemp);
                float optimalRange = recipe.SmeltingOptimalTemp * 0.15f;
                float purityRate;
                if (deviation <= optimalRange) purityRate = 0.15f;
                else if (deviation <= optimalRange * 2f) purityRate = 0.08f;
                else if (deviation <= optimalRange * 3f) purityRate = 0.03f;
                else purityRate = 0.01f;
                purityRate += _proficiency.TemperatureToleranceBonus;
                _purityProgress = Mathf.Clamp01(_purityProgress + purityRate * simulateStep);

                // Temperature tracking.
                if (_heatSwitchTimer < heatSwitchCooldown)
                    _heatSwitchTimer += simulateStep;

                // Check stage advancement.
                if (_purityProgress >= 1f && _stageElapsed >= recipe.SmeltingDuration * 0.8f)
                {
                    _currentStage = ForgeStage.Shaping;
                    _stageElapsed = 0f;
                    _strikeTimer = strikeCooldown;
                    _currentStrike = 0;
                    _cumulativeShapeScore = 0f;
                    _strikeHistory.Clear();
                    break;
                }
            }

            // If still in smelting, advance.
            if (_currentStage == ForgeStage.Smelting)
            {
                _currentStage = ForgeStage.Shaping;
                _stageElapsed = 0f;
                _strikeTimer = strikeCooldown;
                _currentStrike = 0;
                _cumulativeShapeScore = 0f;
                _strikeHistory.Clear();
            }

            // Simulate shaping QTE.
            int strikesToSim = Mathf.Min(totalStrikes, strikeForces?.Length ?? totalStrikes);
            for (int i = 0; i < strikesToSim; i++)
            {
                float force = strikeForces != null && i < strikeForces.Length
                    ? strikeForces[i]
                    : UnityEngine.Random.Range(0.3f, 0.7f);

                _currentTargetForce = GenerateTargetForce(i, totalStrikes);
                float forceDiff = Mathf.Abs(force - _currentTargetForce);
                float accuracy;
                if (forceDiff <= forceRangeRatio)
                    accuracy = 1f;
                else if (forceDiff <= forceRangeRatio * 3f)
                {
                    float excess = (forceDiff - forceRangeRatio) / (forceRangeRatio * 2f);
                    accuracy = Mathf.Lerp(1f, 0.4f, excess);
                }
                else
                    accuracy = Mathf.Max(0f, 0.4f - (forceDiff - forceRangeRatio * 3f) * missPenalty);

                accuracy = Mathf.Clamp01(accuracy + _proficiency.ShapeAccuracyBonus);
                float contribution = accuracy / totalStrikes;
                if (accuracy >= 0.95f) contribution *= perfectBonusMultiplier;
                contribution = Mathf.Clamp(contribution, 0f, 1f / totalStrikes * perfectBonusMultiplier);
                _cumulativeShapeScore += contribution;
                _currentStrike++;

                _strikeHistory.Add(new ShapingStrikeData
                {
                    StrikeIndex = i,
                    TargetForce = _currentTargetForce,
                    AppliedForce = force,
                    Accuracy = accuracy,
                    ScoreContribution = contribution
                });
            }
            _cumulativeShapeScore = Mathf.Clamp01(_cumulativeShapeScore);

            // Advance to quenching.
            _currentStage = ForgeStage.Quenching;
            _stageElapsed = 0f;

            // Select quenching liquid.
            if (quenchingLiquid != QuenchingLiquid.None)
            {
                _selectedLiquid = quenchingLiquid;
                _quenchingApplied = true;
            }

            // Advance to enlightening.
            _currentStage = ForgeStage.Enlightening;
            _stageElapsed = 0f;
            _injectedPower = 0f;
            _currentAffinity = 0f;

            // Simulate enlightening.
            float injectRate = injectPowerAmount / 15f; // Simulate 15s of injection.
            for (float t = 0; t < 15f; t += simulateStep)
            {
                _injectedPower = Mathf.Min(_injectedPower + injectRate * simulateStep, maxSpiritualPower);
                float injectionRatio = _injectedPower / maxSpiritualPower;
                float targetAffinity = Mathf.Pow(injectionRatio, 0.7f);
                targetAffinity *= _proficiency.QuenchBonus;
                if (_selectedLiquid != QuenchingLiquid.None && QuenchingLiquidDataMap.TryGetValue(_selectedLiquid, out var liqData))
                {
                    targetAffinity += liqData.AffinityBonus;
                }
                _currentAffinity = Mathf.Lerp(_currentAffinity, Mathf.Clamp01(targetAffinity), simulateStep * 2f);
                _stageElapsed = t + simulateStep;
            }

            // Complete.
            CompleteForging();
            return _lastResult;
        }

        }
}
