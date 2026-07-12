using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EarthOnline.Combat
{
    #region Enums

    /// <summary>
    /// 物品品质等级 (004-QUALITY-01)。
    /// 影响掉落的稀有度和价值。
    /// </summary>
    public enum ItemQuality
    {
        Common,     // 普通 — 灵石、修为结晶等基础物品
        Rare,       // 稀有 — 高品质基础材料
        SR,         // Super Rare — 高级装备/配方
        SSR,        // Super Super Rare — 顶级装备/稀有配方
        UR          // Ultra Rare — 传说级物品/技能书
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// BOSS掉落条目配置 (004-DROP-01)。
    /// 每个条目定义一个可能掉落的物品。
    /// </summary>
    [Serializable]
    public class BossDropEntry
    {
        [Header("-- 物品信息 --")]
        public string itemId;
        public string itemName;
        public ItemQuality quality = ItemQuality.Common;

        [Header("-- 掉落参数 --")]
        [Tooltip("掉落概率 (0~1)，概率池中每个物品独立判定。必定掉落的物品此项无效。")]
        [Range(0f, 1f)]
        public float dropChance = 1f;

        [Tooltip("最小掉落数量。")]
        public int minCount = 1;

        [Tooltip("最大掉落数量。实际数量在 [minCount, maxCount] 之间随机。")]
        public int maxCount = 1;

        [Header("-- 炼器配方关联 (004-FORGE-01) --")]
        [Tooltip("此材料可参与的炼器配方ID列表。仅 BOSS 材料类物品有效。")]
        public string[] forgeRecipeIds;

        [Tooltip("配方名称列表（用于UI显示）。与 forgeRecipeIds 一一对应。")]
        public string[] forgeRecipeNames;

        [Header("-- 首杀专属 (004-FIRSTKILL-01) --")]
        [Tooltip("是否为指定物品（用于首杀池和特殊物品），不影响掉落逻辑。")]
        public bool isSpecialItem;

        /// <summary>获取随机数量 (minCount ~ maxCount 之间)。</summary>
        public int GetRandomCount()
        {
            if (minCount >= maxCount) return minCount;
            return Random.Range(minCount, maxCount + 1);
        }
    }

    /// <summary>
    /// BOSS掉落池配置 (004-DROP-02)。
    /// 分为必定掉落、概率掉落和首杀掉落三个子池。
    /// </summary>
    [Serializable]
    public class BossDropPool
    {
        [Header("必定掉落 (004-DROP-GUARANTEED)")]
        [Tooltip("BOSS被击败后必定掉落的物品。\n通常包含: BOSS材料+灵石+修为结晶")]
        public BossDropEntry[] guaranteed;

        [Header("概率掉落 (004-DROP-PROBABILITY)")]
        [Tooltip("按独立概率掉落的物品。\n每个物品单独判定是否掉落。\n包含: SR/SSR/UR 装备+配方+技能书")]
        public BossDropEntry[] probability;

        [Header("首杀掉落 (004-DROP-FIRSTKILL)")]
        [Tooltip("仅首杀时额外掉落的物品。\n包含: 专属称号+特殊物品")]
        public BossDropEntry[] firstKill;
    }

    /// <summary>
    /// 单次掉落的结果，包含所有实际掉落的物品及其数量。
    /// </summary>
    public struct BossDropResult
    {
        /// <summary>实际掉落的物品列表。</summary>
        public BossDropEntry[] items;

        /// <summary>每个物品的实际掉落数量（与 items 数组一一对应）。</summary>
        public int[] counts;

        /// <summary>是否为首杀。</summary>
        public bool isFirstKill;

        /// <summary>是否因完美狩猎触发了品质提升。</summary>
        public bool perfectHuntApplied;

        /// <summary>因完美狩猎而品质提升的物品索引。</summary>
        public int[] upgradedIndices;
    }

    #endregion

    /// <summary>
    /// BOSS掉落表 (Story 004) — 掉落计算 + 品质修正 + 首杀 + 炼器联动。
    ///
    /// 必定掉落 (004-DROP-03):
    ///   BOSS材料 + 灵石 + 修为结晶
    ///
    /// 概率掉落 (004-DROP-04):
    ///   SR/SSR/UR 品质的 装备 + 配方 + 技能书
    ///   每个物品按独立概率判定
    ///
    /// 首杀 (004-DROP-05):
    ///   专属称号 + 特殊物品
    ///
    /// 完美狩猎 (004-DROP-06):
    ///   品质+1档: SR→SSR, SSR→UR, UR不变
    ///
    /// BOSS材料炼器联动 (004-FORGE-02):
    ///   每个BOSS材料条目携带 forgeRecipeIds，链接到炼器系统。
    /// </summary>
    public class BossDropTable : MonoBehaviour
    {
        #region Constants

        /// <summary>品质提升映射表 (004-QUALITY-UPGRADE)</summary>
        private static readonly Dictionary<ItemQuality, ItemQuality> QUALITY_UPGRADE = new Dictionary<ItemQuality, ItemQuality>
        {
            { ItemQuality.SR,  ItemQuality.SSR },
            { ItemQuality.SSR, ItemQuality.UR  },
            { ItemQuality.UR,  ItemQuality.UR  }  // UR 保持不变（已是最高）
        };

        #endregion

        #region Inspector Config

        [Header("-- BOSS 引用 --")]
        [Tooltip("关联的 BossAI 组件，为空则自动查找。")]
        public BossAI bossAI;

        [Tooltip("关联的 BossDef，为空则从 BossAI 读取。")]
        public BossDef bossDef;

        [Header("-- 掉落池配置 (004-DROP-POOL) --")]
        public BossDropPool dropPool;

        [Header("-- 完美狩猎联动 (004-PERFECT-HUNT) --")]
        [Tooltip("从 BossWeaknessSystem 读取完美狩猎状态。为空则自动查找。")]
        public BossWeaknessSystem weaknessSystem;

        [Header("-- 首杀追踪 --")]
        [Tooltip("首杀状态持久化键前缀 (格式: {prefix}{bossId})。接入存档系统后生效。")]
        public string firstKillPrefKey = "BOSS_FIRST_KILL_";

        [Header("-- 调试 --")]
        public bool enableDebugLogs = true;

        #endregion

        #region Private State

        private bool _initialized;
        private string _bossNameCache = "BOSS";

        #endregion

        #region Public Properties

        /// <summary>系统是否已完成初始化。</summary>
        public bool IsReady => _initialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (bossAI == null)
                bossAI = GetComponent<BossAI>();

            if (bossAI != null && bossDef == null)
                bossDef = bossAI.bossDef;

            if (bossDef == null)
            {
                Debug.LogError("[BossDropTable] BossDef 未配置，系统禁用。");
                enabled = false;
                return;
            }

            // 自动查找弱点系统（完美狩猎联动用）
            if (weaknessSystem == null)
                weaknessSystem = GetComponent<BossWeaknessSystem>();

            _bossNameCache = bossDef.displayName;

            // 确保掉落池有有效数据
            if (dropPool == null)
                dropPool = new BossDropPool();

            _initialized = true;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<PerfectHuntEvent>(OnPerfectHunt);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<PerfectHuntEvent>(OnPerfectHunt);
        }

        private void Start()
        {
            if (!_initialized) return;

            if (enableDebugLogs)
            {
                int guaranteedCount = dropPool.guaranteed?.Length ?? 0;
                int probabilityCount = dropPool.probability?.Length ?? 0;
                int firstKillCount = dropPool.firstKill?.Length ?? 0;
                DebugLog($"初始化完成。必定掉落: {guaranteedCount} 种, " +
                         $"概率掉落: {probabilityCount} 种, 首杀: {firstKillCount} 种");
            }
        }

        #endregion

        #region Public API — 掉落计算

        /// <summary>
        /// 核心掉落计算方法 (004-DROP-ROLL)。
        ///
        /// 流程:
        ///   1. 收集必定掉落物
        ///   2. 独立概率判定概率池
        ///   3. 首杀检查 + 额外首杀物品
        ///   4. 完美狩猎品质提升
        ///   5. 发布掉落事件
        /// </summary>
        /// <param name="isFirstKill">是否为首杀。</param>
        /// <param name="forcePerfectHuntBonus">强制应用完美狩猎加成（用于外部覆盖）。</param>
        /// <returns>掉落结果。</returns>
        public BossDropResult RollLoot(bool isFirstKill = false, bool forcePerfectHuntBonus = false)
        {
            if (!_initialized)
            {
                DebugLogError("掉落表未初始化，返回空掉落。");
                return EmptyResult();
            }

            // 检测完美狩猎状态
            bool perfectHunt = forcePerfectHuntBonus ||
                               (weaknessSystem != null && weaknessSystem.PerfectHuntAchieved);

            // 收集掉落物品
            List<BossDropEntry> resultItems = new List<BossDropEntry>();
            List<int> resultCounts = new List<int>();
            List<int> upgradedIndices = new List<int>();

            // ---- 步骤1: 必定掉落 ----
            AddGuaranteedDrops(resultItems, resultCounts);

            // ---- 步骤2: 概率判定 ----
            AddProbabilityDrops(resultItems, resultCounts, perfectHunt, upgradedIndices);

            // ---- 步骤3: 首杀额外掉落 ----
            if (isFirstKill)
            {
                AddFirstKillDrops(resultItems, resultCounts);
            }

            // 构造结果
            BossDropResult result = new BossDropResult
            {
                items = resultItems.ToArray(),
                counts = resultCounts.ToArray(),
                isFirstKill = isFirstKill,
                perfectHuntApplied = perfectHunt,
                upgradedIndices = upgradedIndices.ToArray()
            };

            // ---- 步骤4: 发布事件 ----
            PublishDropEvents(result, isFirstKill);

            // ---- 步骤5: 炼器配方联动 ----
            PublishForgeMaterialEvents(result);

            DebugLog($"掉落结算完成。共 {result.items.Length} 种物品。" +
                     (perfectHunt ? " (完美狩猎品质提升)" : "") +
                     (isFirstKill ? " (首杀)" : ""));

            return result;
        }

        /// <summary>
        /// 简化掉落接口 — 自动判断首杀状态。
        /// 首杀状态通过 PlayerPrefs 持久化（项目中应接入存档系统）。
        /// </summary>
        /// <returns>掉落结果。</returns>
        public BossDropResult RollLootAuto()
        {
            bool isFirstKill = !IsFirstKillDone();
            BossDropResult result = RollLoot(isFirstKill);

            if (isFirstKill)
            {
                MarkFirstKillDone();
            }

            return result;
        }

        /// <summary>
        /// 检查某物品是否为炼器材料（有配方关联）。
        /// </summary>
        public bool IsForgeMaterial(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            // 检查所有掉落池
            BossDropEntry[] allPools = GetAllPoolEntries();
            foreach (var entry in allPools)
            {
                if (entry.itemId == itemId && entry.forgeRecipeIds != null && entry.forgeRecipeIds.Length > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取某物品关联的炼器配方ID列表 (004-FORGE-03)。
        /// </summary>
        public string[] GetForgeRecipeIds(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Array.Empty<string>();

            BossDropEntry[] allPools = GetAllPoolEntries();
            foreach (var entry in allPools)
            {
                if (entry.itemId == itemId && entry.forgeRecipeIds != null)
                    return entry.forgeRecipeIds;
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// 获取某物品关联的炼器配方名称列表。
        /// </summary>
        public string[] GetForgeRecipeNames(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Array.Empty<string>();

            BossDropEntry[] allPools = GetAllPoolEntries();
            foreach (var entry in allPools)
            {
                if (entry.itemId == itemId && entry.forgeRecipeNames != null)
                    return entry.forgeRecipeNames;
            }
            return Array.Empty<string>();
        }

        #endregion

        #region Internal — 掉落子流程

        /// <summary>
        /// 添加必定掉落物品。
        /// 必定掉落池中的条目 100% 掉落，不受概率影响。
        /// </summary>
        private void AddGuaranteedDrops(List<BossDropEntry> items, List<int> counts)
        {
            BossDropEntry[] guaranteed = dropPool?.guaranteed;
            if (guaranteed == null || guaranteed.Length == 0)
            {
                DebugLogWarning("必定掉落池为空。请配置 BOSS 材料 + 灵石 + 修为结晶。");
                return;
            }

            foreach (BossDropEntry entry in guaranteed)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;

                items.Add(entry);
                counts.Add(entry.GetRandomCount());

                if (enableDebugLogs)
                    DebugLog($"  必定掉落: {entry.itemName} x{counts[counts.Count - 1]}");
            }
        }

        /// <summary>
        /// 概率判定概率掉落池。
        /// 每个物品独立判定是否掉落 (004-DROP-PROBABILITY-ROLL)。
        /// 如果完美狩猎已达成，触发品质提升。
        /// </summary>
        private void AddProbabilityDrops(List<BossDropEntry> items, List<int> counts,
            bool perfectHunt, List<int> upgradedIndices)
        {
            BossDropEntry[] probability = dropPool?.probability;
            if (probability == null || probability.Length == 0) return;

            foreach (BossDropEntry entry in probability)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;

                // 独立概率判定
                if (Random.value > entry.dropChance) continue;

                // 确定最终品质
                ItemQuality finalQuality = entry.quality;
                bool wasUpgraded = false;

                if (perfectHunt && CanUpgradeQuality(entry.quality))
                {
                    finalQuality = QUALITY_UPGRADE[entry.quality];
                    wasUpgraded = true;
                }

                // 实际掉落——使用最终品质创建副本条目
                BossDropEntry actualEntry = new BossDropEntry
                {
                    itemId = entry.itemId,
                    itemName = entry.itemName,
                    quality = finalQuality,
                    dropChance = 1f,
                    minCount = entry.minCount,
                    maxCount = entry.maxCount,
                    forgeRecipeIds = entry.forgeRecipeIds,
                    forgeRecipeNames = entry.forgeRecipeNames,
                    isSpecialItem = entry.isSpecialItem
                };

                int index = items.Count;
                items.Add(actualEntry);
                counts.Add(entry.GetRandomCount());

                if (wasUpgraded)
                {
                    upgradedIndices.Add(index);
                }

                if (enableDebugLogs)
                {
                    string qualityStr = wasUpgraded
                        ? $"{entry.quality}→{actualEntry.quality} (品质提升!)"
                        : $"{entry.quality}";
                    DebugLog($"  概率掉落: {entry.itemName} [{qualityStr}] x{counts[counts.Count - 1]} " +
                             $"(概率: {entry.dropChance:P0})");
                }
            }
        }

        /// <summary>
        /// 添加首杀额外掉落 (004-FIRSTKILL-02)。
        /// 包含专属称号和特殊物品，仅首杀时触发。
        /// </summary>
        private void AddFirstKillDrops(List<BossDropEntry> items, List<int> counts)
        {
            BossDropEntry[] firstKill = dropPool?.firstKill;
            if (firstKill == null || firstKill.Length == 0) return;

            foreach (BossDropEntry entry in firstKill)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;

                items.Add(entry);
                counts.Add(entry.GetRandomCount());

                if (enableDebugLogs)
                    DebugLog($"  首杀掉落: {entry.itemName} x{counts[counts.Count - 1]}");
            }
        }

        #endregion

        #region Internal — 事件发布

        /// <summary>
        /// 发布掉落事件和首杀事件。
        /// </summary>
        private void PublishDropEvents(BossDropResult result, bool isFirstKill)
        {
            // 收集数组用于事件
            int itemCount = result.items.Length;
            string[] itemIds = new string[itemCount];
            string[] itemNames = new string[itemCount];
            int[] quantities = new int[itemCount];
            string[] qualities = new string[itemCount];
            bool[] isForgeMaterials = new bool[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                itemIds[i] = result.items[i].itemId;
                itemNames[i] = result.items[i].itemName;
                quantities[i] = result.counts[i];
                qualities[i] = result.items[i].quality.ToString();
                isForgeMaterials[i] = result.items[i].forgeRecipeIds != null &&
                                      result.items[i].forgeRecipeIds.Length > 0;
            }

            // 掉落事件
            EventBus.Publish(new BossDropRolledEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                ItemIds = itemIds,
                ItemNames = itemNames,
                Quantities = quantities,
                Qualities = qualities,
                IsForgeMaterials = isForgeMaterials,
                IsFirstKill = isFirstKill,
                PerfectHuntBonus = result.perfectHuntApplied
            });

            // 首杀事件
            if (isFirstKill)
            {
                // 查找称号和特殊物品
                string titleId = "";
                string titleName = "";
                string specialItemId = "";
                string specialItemName = "";

                BossDropEntry[] firstKillEntries = dropPool?.firstKill;
                if (firstKillEntries != null)
                {
                    for (int i = 0; i < firstKillEntries.Length; i++)
                    {
                        if (firstKillEntries[i].isSpecialItem)
                        {
                            if (string.IsNullOrEmpty(titleId))
                            {
                                titleId = firstKillEntries[i].itemId;
                                titleName = firstKillEntries[i].itemName;
                            }
                            else
                            {
                                specialItemId = firstKillEntries[i].itemId;
                                specialItemName = firstKillEntries[i].itemName;
                            }
                        }
                    }
                }

                EventBus.Publish(new BossFirstKillEvent
                {
                    BossId = bossDef.bossId,
                    BossName = _bossNameCache,
                    TitleId = titleId,
                    TitleName = titleName,
                    SpecialItemId = specialItemId,
                    SpecialItemName = specialItemName
                });
            }
        }

        /// <summary>
        /// 发布炼器材料掉落事件 (004-FORGE-04)。
        /// 遍历掉落结果，对有配方关联的材料单独发布事件。
        /// </summary>
        private void PublishForgeMaterialEvents(BossDropResult result)
        {
            for (int i = 0; i < result.items.Length; i++)
            {
                BossDropEntry entry = result.items[i];
                if (entry.forgeRecipeIds == null || entry.forgeRecipeIds.Length == 0) continue;

                EventBus.Publish(new BossForgeMaterialDropEvent
                {
                    BossId = bossDef.bossId,
                    BossName = _bossNameCache,
                    MaterialItemId = entry.itemId,
                    MaterialItemName = entry.itemName,
                    Quantity = result.counts[i],
                    ForgeRecipeIds = entry.forgeRecipeIds,
                    ForgeRecipeNames = entry.forgeRecipeNames ?? Array.Empty<string>()
                });

                DebugLog($"  炼器材料 [{entry.itemName}] → 可锻造: {string.Join(", ", entry.forgeRecipeNames ?? entry.forgeRecipeIds)}");
            }
        }

        #endregion

        #region EventBus Handlers

        /// <summary>
        /// 监听 BOSS 被击败事件，自动触发掉落。
        /// </summary>
        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            if (evt.BossId != bossDef?.bossId) return;

            // 自动执行掉落
            RollLootAuto();
        }

        /// <summary>
        /// 完美狩猎达成时记录日志（掉落品质提升在 RollLoot 中处理）。
        /// </summary>
        private void OnPerfectHunt(PerfectHuntEvent evt)
        {
            if (evt.BossId != bossDef?.bossId) return;

            DebugLog($"★ 完美狩猎达成！下次掉落品质将提升一档！");
        }

        #endregion

        #region Tools

        /// <summary>
        /// 检查品质是否可提升 (004-QUALITY-CAN-UPGRADE)。
        /// Common 和 Rare 不参与提升（只有 SR+ 可提升）。
        /// </summary>
        private static bool CanUpgradeQuality(ItemQuality quality)
        {
            return quality == ItemQuality.SR ||
                   quality == ItemQuality.SSR ||
                   quality == ItemQuality.UR;
        }

        /// <summary>
        /// 获取所有掉落池中的条目（用于查询）。
        /// </summary>
        private BossDropEntry[] GetAllPoolEntries()
        {
            var all = new List<BossDropEntry>();

            if (dropPool?.guaranteed != null)
                all.AddRange(dropPool.guaranteed);

            if (dropPool?.probability != null)
                all.AddRange(dropPool.probability);

            if (dropPool?.firstKill != null)
                all.AddRange(dropPool.firstKill);

            return all.ToArray();
        }

        /// <summary>
        /// 创建空掉落结果。
        /// </summary>
        private static BossDropResult EmptyResult()
        {
            return new BossDropResult
            {
                items = Array.Empty<BossDropEntry>(),
                counts = Array.Empty<int>(),
                isFirstKill = false,
                perfectHuntApplied = false,
                upgradedIndices = Array.Empty<int>()
            };
        }

        /// <summary>
        /// 检查首杀是否已完成（通过 PlayerPrefs 持久化 — 应接入存档系统）。
        /// </summary>
        private bool IsFirstKillDone()
        {
            string key = firstKillPrefKey + bossDef.bossId;
            return PlayerPrefs.GetInt(key, 0) == 1;
        }

        /// <summary>
        /// 标记首杀已完成。
        /// </summary>
        private void MarkFirstKillDone()
        {
            string key = firstKillPrefKey + bossDef.bossId;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 获取调试状态摘要。
        /// </summary>
        public string GetDebugStatus()
        {
            if (!_initialized)
                return "[BossDropTable] 未初始化";

            int guaranteedCount = dropPool?.guaranteed?.Length ?? 0;
            int probabilityCount = dropPool?.probability?.Length ?? 0;
            int firstKillCount = dropPool?.firstKill?.Length ?? 0;
            bool isFirstKillDone = IsFirstKillDone();

            string result = $"=== BOSS掉落表: {_bossNameCache} ===\n" +
                            $"必定掉落: {guaranteedCount} 种\n" +
                            $"概率掉落: {probabilityCount} 种\n" +
                            $"首杀掉落: {firstKillCount} 种\n" +
                            $"首杀状态: {(isFirstKillDone ? "已完成" : "未完成")}\n" +
                            $"完美狩猎联动: {(weaknessSystem != null ? weaknessSystem.GetReconProgress() : "未连接")}\n\n";

            // 列出每种配置
            result += "--- 必定掉落 ---\n";
            if (dropPool?.guaranteed != null)
            {
                foreach (var entry in dropPool.guaranteed)
                {
                    string forgeInfo = (entry.forgeRecipeIds?.Length ?? 0) > 0
                        ? $" [炼器配方: {entry.forgeRecipeIds.Length}种]"
                        : "";
                    result += $"  [{entry.quality}] {entry.itemName} x{entry.minCount}-{entry.maxCount}{forgeInfo}\n";
                }
            }

            result += "--- 概率掉落 ---\n";
            if (dropPool?.probability != null)
            {
                foreach (var entry in dropPool.probability)
                {
                    string forgeInfo = (entry.forgeRecipeIds?.Length ?? 0) > 0
                        ? $" [炼器: {entry.forgeRecipeIds.Length}种]"
                        : "";
                    result += $"  [{entry.quality}] {entry.itemName} ({entry.dropChance:P0}) x{entry.minCount}-{entry.maxCount}{forgeInfo}\n";
                }
            }

            result += "--- 首杀 ---\n";
            if (dropPool?.firstKill != null)
            {
                foreach (var entry in dropPool.firstKill)
                {
                    result += $"  {(entry.isSpecialItem ? "★ " : "")}[{entry.quality}] {entry.itemName} x{entry.minCount}-{entry.maxCount}\n";
                }
            }

            return result;
        }

        #endregion

        #region Debug

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[BossDropTable] {_bossNameCache}: {message}");
        }

        private void DebugLogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[BossDropTable] {_bossNameCache}: {message}");
        }

        private void DebugLogError(string message)
        {
            Debug.LogError($"[BossDropTable] {_bossNameCache}: {message}");
        }

        [ContextMenu("Debug: 打印掉落表状态")]
        private void DebugPrintStatus()
        {
            Debug.Log(GetDebugStatus());
        }

        [ContextMenu("Debug: 模拟掉落 (自动首杀)")]
        private void DebugRollLootAuto()
        {
            if (!Application.isPlaying) return;
            BossDropResult result = RollLootAuto();
            Debug.Log($"[BossDropTable] 模拟掉落完成。共 {result.items.Length} 种物品。" +
                      (result.perfectHuntApplied ? " 品质提升已应用" : ""));
            for (int i = 0; i < result.items.Length; i++)
            {
                Debug.Log($"  [{result.items[i].quality}] {result.items[i].itemName} x{result.counts[i]}");
            }
        }

        [ContextMenu("Debug: 模拟掉落 (强制首杀)")]
        private void DebugRollLootFirstKill()
        {
            if (!Application.isPlaying) return;
            RollLoot(true);
        }

        [ContextMenu("Debug: 模拟掉落 (强制完美狩猎)")]
        private void DebugRollLootPerfectHunt()
        {
            if (!Application.isPlaying) return;
            RollLoot(false, true);
        }

        [ContextMenu("Debug: 重置首杀状态")]
        private void DebugResetFirstKill()
        {
            string key = firstKillPrefKey + bossDef.bossId;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log($"[BossDropTable] 首杀状态已重置: {key}");
        }

        #endregion
    }
}
