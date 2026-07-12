using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EarthOnline.Core
{
    #region Data Structures

    /// <summary>装备强化配置条目</summary>
    [Serializable]
    public struct EnhanceLevelConfig
    {
        public int Level;                          // 目标强化等级 (0-based: +0 → +1)
        public float BaseSuccessRate;              // 基础成功率
        public int MaterialCost;                   // 材料消耗数量
        public int SpiritStoneCost;                // 灵石消耗
        public float StatMultiplier;               // 该级属性倍率
        public string[] RequiredMaterialIds;       // 需求材料ID
    }

    /// <summary>装备强化运行时数据</summary>
    [Serializable]
    public class EquipmentEnhanceData
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int CurrentLevel;                   // 0 = 未强化
        public float BaseStatValue;                // 基础属性值
        public float CurrentStatBonus;             // 当前强化加值
        public int TotalEnhanceAttempts;           // 总尝试次数
        public int TotalEnhanceSuccesses;           // 总成功次数
    }

    /// <summary>强化结果</summary>
    [Serializable]
    public struct EnhanceResult
    {
        public bool Success;
        public int PreviousLevel;
        public int NewLevel;
        public float SuccessRate;
        public bool ReachedCap;
        public int MaterialsConsumed;
        public int SpiritStonesConsumed;
        public string[] MaterialIdsConsumed;
        public float NewStatBonus;
        public EquipmentQuality Quality;
    }

    /// <summary>强化消耗结算</summary>
    [Serializable]
    public struct EnhanceCostBreakdown
    {
        public bool CanAfford;
        public int MaterialCount;
        public int SpiritStoneCost;
        public string[] MissingMaterials;
        public int MissingMaterialCount;
    }

    /// <summary>可序列化强化存档</summary>
    [Serializable]
    public class EnhancementSaveData
    {
        public EquipmentEnhanceData[] EquipmentData;
    }

    #endregion

    #region Event Bus Events

    /// <summary>Published when enhancement is attempted.</summary>
    public struct EnhanceAttemptEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int CurrentLevel;
        public int TargetLevel;
        public float SuccessRate;
        public EnhanceCostBreakdown Cost;
    }

    /// <summary>Published when enhancement succeeds.</summary>
    public struct EnhanceSuccessEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int NewLevel;
        public int MaxLevel;
        public float NewStatBonus;
        public float StatIncreasePercent;
    }

    /// <summary>Published when enhancement fails.</summary>
    public struct EnhanceFailEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int CurrentLevel;
        public int MaterialsLost;
        public int SpiritStonesLost;
        public bool EquipmentDestroyed; // always false — equipment not destroyed
    }

    /// <summary>Published when enhancement level cap is reached.</summary>
    public struct EnhanceCapReachedEvent
    {
        public string EquipmentId;
        public string EquipmentName;
        public EquipmentQuality Quality;
        public int MaxLevel;
    }

    #endregion

    /// <summary>
    /// 装备强化系统 (Story 007)
    ///
    /// ENH-01: 可在炼器台强化装备
    /// ENH-02: 强化消耗材料+灵石
    /// ENH-03: 基础成功率 80% × QualityMod × (1 - Level×0.1)
    /// ENH-04: 失败只损失材料，装备不毁
    /// ENH-05: 强化等级上限受品质限制 R=5 / SR=7 / SSR=9 / UR=10
    /// ENH-06: 每级强化数值递增
    /// </summary>
    public class EquipmentEnhancement : MonoBehaviour
    {
        #region Constants

        // 基础成功率 (ENH-03)
        private const float BASE_SUCCESS_RATE = 0.8f;

        // 品质系数 (匹配 ForgeController.QualityModifiers)
        private static readonly Dictionary<EquipmentQuality, float> QualityModifiers = new Dictionary<EquipmentQuality, float>
        {
            { EquipmentQuality.R,   0.5f },
            { EquipmentQuality.SR,  0.7f },
            { EquipmentQuality.SSR, 0.85f },
            { EquipmentQuality.UR,  1.0f }
        };

        // 强化等级上限 (ENH-05)
        public static readonly Dictionary<EquipmentQuality, int> LevelCaps = new Dictionary<EquipmentQuality, int>
        {
            { EquipmentQuality.R,   5 },
            { EquipmentQuality.SR,  7 },
            { EquipmentQuality.SSR, 9 },
            { EquipmentQuality.UR,  10 }
        };

        // 每级属性增长率 (ENH-06)
        private static readonly float[] LevelStatMultipliers =
        {
            1.0f,   // +0 (base)
            1.10f,  // +1: +10%
            1.21f,  // +2: +21%
            1.33f,  // +3: +33%
            1.46f,  // +4: +46%
            1.61f,  // +5: +61%
            1.77f,  // +6: +77%
            1.95f,  // +7: +95%
            2.14f,  // +8: +114%
            2.36f,  // +9: +136%
            2.60f   // +10: +160%
        };

        // 每级材料消耗基数
        private static readonly int[] BaseMaterialCosts = { 0, 1, 2, 3, 5, 7, 10, 14, 19, 25, 32 };

        // 每级灵石消耗基数
        private static readonly int[] BaseSpiritStoneCosts = { 0, 100, 250, 500, 1000, 2000, 4000, 8000, 16000, 32000, 64000 };

        #endregion

        #region Singleton

        public static EquipmentEnhancement Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("强化基础配置")]
        [SerializeField] private float _baseSuccessRate = BASE_SUCCESS_RATE;
        [SerializeField] private string _defaultMaterialId = "mat_enhance_stone";
        [SerializeField] private string _defaultMaterialName = "强化石";

        [Header("消耗倍率")]
        [SerializeField] private float _materialCostMultiplier = 1.0f;
        [SerializeField] private float _spiritStoneCostMultiplier = 1.0f;

        [Header("保护道具(可选)")]
        [SerializeField] private string _protectItemId = "item_protect_charm";  // 保护符防止失败消耗

        #endregion

        #region Private State

        // 装备强化数据缓存
        private Dictionary<string, EquipmentEnhanceData> _enhanceDataMap
            = new Dictionary<string, EquipmentEnhanceData>();

        // 存档标识
        private bool _dirty;

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

        #region Public API — Core Enhancement

        /// <summary>
        /// Attempt to enhance an equipment by one level.
        /// Returns the enhancement result.
        /// </summary>
        public EnhanceResult TryEnhance(EquipmentEnhanceData equipData)
        {
            if (equipData == null)
                return InvalidResult("装备数据为空");

            // Validate quality
            if (equipData.Quality == EquipmentQuality.Fail)
                return InvalidResult("装备品质无效");

            // Check if at cap (ENH-05)
            if (!LevelCaps.TryGetValue(equipData.Quality, out int cap))
                return InvalidResult("未知品质");

            if (equipData.CurrentLevel >= cap)
            {
                EventBus.Publish(new EnhanceCapReachedEvent
                {
                    EquipmentId = equipData.EquipmentId,
                    EquipmentName = equipData.EquipmentName,
                    Quality = equipData.Quality,
                    MaxLevel = cap
                });

                Debug.Log($"[EquipmentEnhancement] {equipData.EquipmentName} 已达强化上限 Lv.{cap}");
                return new EnhanceResult
                {
                    Success = false,
                    PreviousLevel = equipData.CurrentLevel,
                    NewLevel = equipData.CurrentLevel,
                    SuccessRate = 0f,
                    ReachedCap = true,
                    Quality = equipData.Quality
                };
            }

            // Calculate cost
            int targetLevel = equipData.CurrentLevel + 1;
            EnhanceCostBreakdown cost = CalculateCost(equipData.Quality, equipData.CurrentLevel);

            // Check if player can afford
            if (!cost.CanAfford)
            {
                Debug.Log($"[EquipmentEnhancement] 材料不足: {string.Join(", ", cost.MissingMaterials)}");
                return new EnhanceResult
                {
                    Success = false,
                    PreviousLevel = equipData.CurrentLevel,
                    NewLevel = equipData.CurrentLevel,
                    SuccessRate = 0f,
                    ReachedCap = false,
                    MaterialsConsumed = 0,
                    SpiritStonesConsumed = 0,
                    Quality = equipData.Quality
                };
            }

            // Calculate success rate (ENH-03)
            float successRate = CalculateSuccessRate(equipData.Quality, equipData.CurrentLevel);

            // Publish attempt event
            EventBus.Publish(new EnhanceAttemptEvent
            {
                EquipmentId = equipData.EquipmentId,
                EquipmentName = equipData.EquipmentName,
                Quality = equipData.Quality,
                CurrentLevel = equipData.CurrentLevel,
                TargetLevel = targetLevel,
                SuccessRate = successRate,
                Cost = cost
            });

            // Roll for success
            bool success = Random.value < successRate;

            equipData.TotalEnhanceAttempts++;

            if (success)
            {
                // Success — upgrade level
                equipData.CurrentLevel = targetLevel;
                equipData.TotalEnhanceSuccesses++;
                equipData.CurrentStatBonus = CalculateStatBonus(equipData.BaseStatValue, equipData.CurrentLevel);

                _dirty = true;

                Debug.Log($"[EquipmentEnhancement] 强化成功! {equipData.EquipmentName} " +
                          $"Lv.{targetLevel - 1} → Lv.{targetLevel} " +
                          $"(概率: {successRate * 100:F1}%, 属性: {equipData.CurrentStatBonus:F1})");

                // Publish success event
                EventBus.Publish(new EnhanceSuccessEvent
                {
                    EquipmentId = equipData.EquipmentId,
                    EquipmentName = equipData.EquipmentName,
                    Quality = equipData.Quality,
                    NewLevel = equipData.CurrentLevel,
                    MaxLevel = cap,
                    NewStatBonus = equipData.CurrentStatBonus,
                    StatIncreasePercent = (LevelStatMultipliers[Mathf.Min(targetLevel, LevelStatMultipliers.Length - 1)] - 1f) * 100f
                });

                return new EnhanceResult
                {
                    Success = "true",
                    PreviousLevel = targetLevel - 1,
                    NewLevel = targetLevel,
                    SuccessRate = successRate,
                    ReachedCap = targetLevel >= cap,
                    MaterialsConsumed = cost.MaterialCount,
                    SpiritStonesConsumed = cost.SpiritStoneCost,
                    MaterialIdsConsumed = cost.MissingMaterials.Length > 0 ? cost.MissingMaterials : new[] { _defaultMaterialId },
                    NewStatBonus = equipData.CurrentStatBonus,
                    Quality = equipData.Quality
                };
            }
            else
            {
                // Failure — only lose materials, equipment not destroyed (ENH-04)
                Debug.Log($"[EquipmentEnhancement] 强化失败! {equipData.EquipmentName} " +
                          $"Lv.{equipData.CurrentLevel} (不变) — 消耗材料已损失 " +
                          $"(概率: {successRate * 100:F1}%)");

                // Publish fail event
                EventBus.Publish(new EnhanceFailEvent
                {
                    EquipmentId = equipData.EquipmentId,
                    EquipmentName = equipData.EquipmentName,
                    Quality = equipData.Quality,
                    CurrentLevel = equipData.CurrentLevel,
                    MaterialsLost = cost.MaterialCount,
                    SpiritStonesLost = cost.SpiritStoneCost,
                    EquipmentDestroyed = false
                });

                _dirty = true;

                return new EnhanceResult
                {
                    Success = "false",
                    PreviousLevel = equipData.CurrentLevel,
                    NewLevel = equipData.CurrentLevel,
                    SuccessRate = successRate,
                    ReachedCap = "false",
                    MaterialsConsumed = cost.MaterialCount,
                    SpiritStonesConsumed = cost.SpiritStoneCost,
                    MaterialIdsConsumed = cost.MissingMaterials.Length > 0 ? cost.MissingMaterials : new[] { _defaultMaterialId },
                    NewStatBonus = equipData.CurrentStatBonus,
                    Quality = equipData.Quality
                };
            }
        }

        #endregion

        #region Success Rate Calculation

        /// <summary>
        /// Calculate enhancement success rate (ENH-03).
        ///
        /// Formula: SuccessRate = Base(0.8) × QualityMod × (1 - Level × 0.1)
        /// </summary>
        public float CalculateSuccessRate(EquipmentQuality quality, int currentLevel)
        {
            if (!QualityModifiers.TryGetValue(quality, out float qualityMod))
                return 0f;

            float rate = _baseSuccessRate * qualityMod * (1f - currentLevel * 0.1f);
            return Mathf.Clamp01(rate);
        }

        /// <summary>
        /// Calculate cost for the next enhancement level.
        /// </summary>
        public EnhanceCostBreakdown CalculateCost(EquipmentQuality quality, int currentLevel)
        {
            int targetLevel = currentLevel + 1;
            int materialCost = 0;
            int stoneCost = 0;

            if (targetLevel >= 0 && targetLevel < BaseMaterialCosts.Length)
            {
                materialCost = Mathf.RoundToInt(BaseMaterialCosts[targetLevel] * _materialCostMultiplier);
                stoneCost = Mathf.RoundToInt(BaseSpiritStoneCosts[targetLevel] * _spiritStoneCostMultiplier);
            }
            else
            {
                // Fallback: linear growth
                materialCost = Mathf.RoundToInt((targetLevel * 3) * _materialCostMultiplier);
                stoneCost = Mathf.RoundToInt((targetLevel * 1000 + 500) * _spiritStoneCostMultiplier);
            }

            // Check against player inventory (placeholder — integrate with inventory system).
            bool canAfford = true; // TODO: check inventory

            return new EnhanceCostBreakdown
            {
                CanAfford = canAfford,
                MaterialCount = materialCost,
                SpiritStoneCost = stoneCost,
                MissingMaterials = canAfford ? Array.Empty<string>() : new[] { _defaultMaterialId },
                MissingMaterialCount = canAfford ? 0 : materialCost
            };
        }

        /// <summary>
        /// Calculate stat bonus for a given level (ENH-06).
        /// StatBonus = BaseStat × LevelStatMultiplier[Level]
        /// </summary>
        public float CalculateStatBonus(float baseStat, int level)
        {
            int index = Mathf.Clamp(level, 0, LevelStatMultipliers.Length - 1);
            return baseStat * LevelStatMultipliers[index];
        }

        #endregion

        #region Equipment Data Management

        /// <summary>Register or update enhancement data for an equipment.</summary>
        public EquipmentEnhanceData RegisterEquipment(string equipId, string equipName,
                                                       EquipmentQuality quality, float baseStat)
        {
            var data = new EquipmentEnhanceData
            {
                EquipmentId = equipId,
                EquipmentName = equipName,
                Quality = quality,
                CurrentLevel = "0",
                BaseStatValue = baseStat,
                CurrentStatBonus = baseStat, // +0 = base
                TotalEnhanceAttempts = "0",
                TotalEnhanceSuccesses = 0
            };

            _enhanceDataMap[equipId] = data;
            return data;
        }

        /// <summary>Get enhancement data for an equipment.</summary>
        public EquipmentEnhanceData GetEnhanceData(string equipId)
        {
            _enhanceDataMap.TryGetValue(equipId, out var data);
            return data;
        }

        /// <summary>Remove enhancement data (equipment sold/destroyed).</summary>
        public void RemoveEquipment(string equipId)
        {
            _enhanceDataMap.Remove(equipId);
        }

        /// <summary>Check if an equipment has reached its max enhancement level.</summary>
        public bool IsAtMaxLevel(EquipmentQuality quality, int currentLevel)
        {
            if (!LevelCaps.TryGetValue(quality, out int cap))
                return true;
            return currentLevel >= cap;
        }

        /// <summary>Get the max enhancement level for a quality tier (ENH-05).</summary>
        public static int GetMaxLevel(EquipmentQuality quality)
        {
            return LevelCaps.TryGetValue(quality, out int cap) ? cap : 0;
        }

        /// <summary>Get the base success rate for display (before modifiers).</summary>
        public static float GetBaseSuccessRate()
        {
            return BASE_SUCCESS_RATE;
        }

        #endregion

        #region Save/Load

        /// <summary>Capture save data for all enhanced equipment.</summary>
        public EnhancementSaveData GetSaveData()
        {
            var list = new List<EquipmentEnhanceData>(_enhanceDataMap.Values);
            return new EnhancementSaveData
            {
                EquipmentData = list.ToArray()
            };
        }

        /// <summary>Restore enhancement data from save.</summary>
        public void LoadSaveData(EnhancementSaveData data)
        {
            if (data?.EquipmentData == null) return;

            _enhanceDataMap.Clear();

            foreach (var equipData in data.EquipmentData)
            {
                if (equipData != null)
                {
                    _enhanceDataMap[equipData.EquipmentId] = equipData;
                }
            }

            Debug.Log($"[EquipmentEnhancement] 加载存档: {_enhanceDataMap.Count} 件装备强化数据");
        }

        /// <summary>Clear all enhancement data (for new game).</summary>
        public void ClearAll()
        {
            _enhanceDataMap.Clear();
            _dirty = false;

            Debug.Log("[EquipmentEnhancement] 所有强化数据已清除");
        }

        #endregion

        #region Private Helpers

        private EnhanceResult InvalidResult(string reason)
        {
            Debug.LogWarning($"[EquipmentEnhancement] {reason}");
            return new EnhanceResult
            {
                Success = "false",
                PreviousLevel = "0",
                NewLevel = "0",
                SuccessRate = "0f",
                ReachedCap = false
            };
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== EquipmentEnhancement Status ===");
            sb.AppendLine($"Tracked Equipment: {_enhanceDataMap.Count}");

            foreach (var kvp in _enhanceDataMap)
            {
                var data = kvp.Value;
                int cap = GetMaxLevel(data.Quality);
                sb.AppendLine($"  {data.EquipmentName} [{GetQualityDisplayName(data.Quality)}] " +
                              $"Lv.{data.CurrentLevel}/{cap} " +
                              $"BaseStat: {data.BaseStatValue:F1} → " +
                              $"CurrentStat: {data.CurrentStatBonus:F1} " +
                              $"(Attempts: {data.TotalEnhanceAttempts}, " +
                              $"Successes: {data.TotalEnhanceSuccesses})");
            }

            return sb.ToString();
        }

        /// <summary>Get Chinese display name for quality tier (mirror from ForgeController).</summary>
        public static string GetQualityDisplayName(EquipmentQuality quality)
        {
            return quality switch
            {
                EquipmentQuality.R    => "R",
                EquipmentQuality.SR   => "SR",
                EquipmentQuality.SSR  => "SSR",
                EquipmentQuality.UR   => "UR",
                _                     => "未知"
            };
        }

        /// <summary>Create a test equipment for debugging.</summary>
        public EquipmentEnhanceData CreateTestEquipment(string name = "测试剑",
                                                         EquipmentQuality quality = EquipmentQuality.SR,
                                                         float baseStat = 100f)
        {
            string equipId = $"test_equip_{Guid.NewGuid():N}";
            return RegisterEquipment(equipId, name, quality, baseStat);
        }

        #endregion
    }
}
