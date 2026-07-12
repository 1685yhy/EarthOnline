using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Core
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Events (Story 005: 药抗系统 + 装备耐久)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Published when a pill is consumed (药抗追踪).</summary>
    public struct MedicineConsumedEvent
    {
        public string ItemId;
        public string ItemName;
        public PillQuality Quality;
        public float EffectMultiplier;     // 本次实际效果倍率
        public float ResistanceCount;      // 累计药抗计数
        public string NextEffectHint;      // UI 提示: 下次效果预览
    }

    /// <summary>Published when any resistance count changes.</summary>
    public struct MedicineResistanceChangedEvent
    {
        public string ItemId;
        public float NewResistanceCount;
        public float EffectMultiplier;
    }

    /// <summary>Published daily when resistance decays.</summary>
    public struct MedicineResistanceDecayedEvent
    {
        public string[] ItemIds;           // 触发了衰减的物品
        public int ItemsDecayed;
    }

    /// <summary>Published when equipment durability changes.</summary>
    public struct EquipmentDurabilityChangedEvent
    {
        public string ItemId;
        public string DisplayName;
        public float CurrentDurability;
        public float MaxDurability;
        public float DurabilityPercent;
        public float DamageTaken;          // 本次消耗
    }

    /// <summary>Published when equipment durability reaches zero.</summary>
    public struct EquipmentBrokenEvent
    {
        public string ItemId;
        public string DisplayName;
    }

    /// <summary>Published when equipment is repaired.</summary>
    public struct EquipmentRepairedEvent
    {
        public string ItemId;
        public string DisplayName;
        public float AmountRepaired;
        public float CurrentDurability;
        public bool IsFullyRepaired;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MedicineResistance — 药抗系统 + 装备耐久管理
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Story 005: 药抗与使用循环
    ///
    /// 药抗机制:
    /// - 同种丹药连续服用效果逐步衰减 (100% -> 70% -> 40% -> 0%)
    /// - 第4次服用效果为0
    /// - 抗性每日衰减1计数 (7天从满抗恢复到0)
    /// - 不同品级丹药交替可最大化收益
    /// - 上品以上丹药的抗性积累更慢 (品质系数稀释)
    ///
    /// 装备耐久:
    /// - 每场战斗后消耗装备耐久
    /// - 耐久归零装备失效但不消失 (保留30%基础属性)
    /// - 装备可维修
    /// </summary>
    public class MedicineResistance : MonoBehaviour
    {
        #region Singleton

        public static MedicineResistance Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("=== 药抗系数 ===")]

        [Header("每次服用的抗性积累基数")]
        [SerializeField, Tooltip("Low/Mid品质累积基数")]
        private float baseResistanceIncrement = 1f;

        [SerializeField, Tooltip("High(上品)品质累积基数")]
        private float highQualityIncrement = 0.7f;

        [SerializeField, Tooltip("Legendary(极品)品质累积基数")]
        private float legendaryIncrement = 0.4f;

        [Header("每日衰减量")]
        [SerializeField, Tooltip("Low/Mid品质每日衰减计数")]
        private float dailyDecayNormal = 1f;

        [SerializeField, Tooltip("High(上品)品质每日衰减计数 (更慢)")]
        private float dailyDecayHigh = 0.5f;

        [SerializeField, Tooltip("Legendary(极品)每日衰减计数 (最慢)")]
        private float dailyDecayLegendary = 0.25f;

        [Header("效果倍率阈值 (基于Count)")]
        [SerializeField, Range(0f, 1f)] private float firstDoseMultiplier = 1.0f;   // count < 1
        [SerializeField, Range(0f, 1f)] private float secondDoseMultiplier = 0.7f;  // count < 2
        [SerializeField, Range(0f, 1f)] private float thirdDoseMultiplier = 0.4f;   // count < 3
        [SerializeField, Range(0f, 1f)] private float zeroMultiplier = 0f;          // count >= 3

        [Header("=== 装备耐久 ===")]

        [Header("耐久消耗")]
        [SerializeField, Tooltip("每次战斗基础消耗")]
        private float combatBaseDurabilityCost = 5f;

        [SerializeField, Tooltip("被攻击额外消耗")]
        private float combatHitDurabilityCost = 0.5f;

        [SerializeField, Tooltip("炼器/炼丹时消耗")]
        private float craftingDurabilityCost = 2f;

        [Header("耐久阈值")]
        [SerializeField, Tooltip("装备破损后保留的属性倍率")]
        private float brokenStatMultiplier = 0.3f;

        [SerializeField, Tooltip("耐久低于此百分比时触发警告")]
        private float lowDurabilityWarningThreshold = 0.2f;

        #endregion

        #region Data Structures

        /// <summary>一条药抗记录 (可序列化)</summary>
        [Serializable]
        public class ResistanceEntry
        {
            public string ItemId;
            public string ItemName;
            public float ResistanceCount;           // 0.0 ~ 3.0+, 小数 = 部分积累
            public float LastUsedGameTime;          // 上次服用的游戏时间
            public PillQuality LastQuality;         // 上次服用品质

            /// <summary>当前效果倍率 (基于累计计数)</summary>
            public float GetEffectMultiplier(float step1, float step2, float step3, float zero)
            {
                if (ResistanceCount >= 3f) return zero;
                if (ResistanceCount >= 2f) return step3;
                if (ResistanceCount >= 1f) return step2;
                return step1;
            }

            /// <summary>距下次衰减还需天数的文本提示</summary>
            public string RecoveryHint => ResistanceCount <= 0f
                ? "已完全恢复"
                : $"还需 {(int)Math.Ceiling(ResistanceCount)} 天恢复";
        }

        /// <summary>一条装备耐久记录 (可序列化)</summary>
        [Serializable]
        public class EquipmentEntry
        {
            public string ItemId;
            public string DisplayName;
            public float CurrentDurability;
            public float MaxDurability;
            public bool IsBroken;
            public EquipmentQuality Quality;         // 装备品质 (影响维修成本等)

            public float DurabilityPercent => MaxDurability > 0
                ? CurrentDurability / MaxDurability
                : 0f;

            public bool NeedsRepair => CurrentDurability < MaxDurability;
            public bool IsLowDurability => DurabilityPercent <= 0.2f;
            public bool IsFunctioning => !IsBroken && CurrentDurability > 0f;
        }

        #endregion

        #region Private State

        // ─── 药抗数据 (Dictionary runtime + List serialization) ───
        [SerializeField] private List<ResistanceEntry> _resistanceSerializedList = new();
        private Dictionary<string, ResistanceEntry> _resistanceMap = new();

        // ─── 装备耐久数据 ───
        [SerializeField] private List<EquipmentEntry> _equipmentSerializedList = new();
        private Dictionary<string, EquipmentEntry> _equipmentMap = new();

        // ─── 低耐久警告冷却 ───
        private HashSet<string> _lowDurabilityWarnedToday = new();

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
            RebuildMaps();
        }

        private void Start()
        {
            SubscribeToEvents();
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
            // Listen for combat events to consume durability (if CombatSystem exists).
            // Future: EventBus.Subscribe<CombatEndedEvent>(OnCombatEnded);
        }

        private void UnsubscribeFromEvents()
        {
            // Future: cleanup subscriptions.
        }

        #endregion

        #region Map Management (Serialization Bridge)

        /// <summary>Rebuild runtime dictionaries from serialized lists.</summary>
        private void RebuildMaps()
        {
            _resistanceMap.Clear();
            foreach (var entry in _resistanceSerializedList)
            {
                if (!string.IsNullOrEmpty(entry.ItemId))
                    _resistanceMap[entry.ItemId] = entry;
            }

            _equipmentMap.Clear();
            foreach (var entry in _equipmentSerializedList)
            {
                if (!string.IsNullOrEmpty(entry.ItemId))
                    _equipmentMap[entry.ItemId] = entry;
            }
        }

        /// <summary>Sync lists to maps (call after loading save data).</summary>
        public void RebuildFromSerializedData()
        {
            RebuildMaps();
        }

        #endregion

        #region ─── 药抗 API ─────────────────────────────────────────────────

        /// <summary>
        /// 记录服用丹药, 返回本次实际效果倍率
        /// </summary>
        public float RecordConsumption(string itemId, string itemName, PillQuality quality)
        {
            float increment = GetResistanceIncrement(quality);

            if (_resistanceMap.TryGetValue(itemId, out var entry))
            {
                entry.ResistanceCount = Mathf.Min(entry.ResistanceCount + increment, 5f);
                entry.LastUsedGameTime = GetGameTimeDays();
                entry.LastQuality = quality;
            }
            else
            {
                entry = new ResistanceEntry
                {
                    ItemId = itemId,
                    ItemName = itemName,
                    ResistanceCount = Mathf.Min(increment, 3f),
                    LastUsedGameTime = GetGameTimeDays(),
                    LastQuality = quality
                };
                _resistanceMap[itemId] = entry;
                _resistanceSerializedList.Add(entry);
            }

            float multiplier = GetEffectMultiplier(itemId);
            string nextHint = GetNextEffectHint(itemId);

            // Publish event.
            EventBus.Publish(new MedicineConsumedEvent
            {
                ItemId = itemId,
                ItemName = itemName,
                Quality = quality,
                EffectMultiplier = multiplier,
                ResistanceCount = entry.ResistanceCount,
                NextEffectHint = nextHint
            });

            EventBus.Publish(new MedicineResistanceChangedEvent
            {
                ItemId = itemId,
                NewResistanceCount = entry.ResistanceCount,
                EffectMultiplier = multiplier
            });

            return multiplier;
        }

        /// <summary>
        /// 获取指定丹药的当前效果倍率
        /// </summary>
        public float GetEffectMultiplier(string itemId)
        {
            if (!_resistanceMap.TryGetValue(itemId, out var entry))
                return 1.0f;

            return entry.GetEffectMultiplier(
                firstDoseMultiplier,
                secondDoseMultiplier,
                thirdDoseMultiplier,
                zeroMultiplier);
        }

        /// <summary>
        /// 获取抗性计数
        /// </summary>
        public float GetResistanceCount(string itemId)
        {
            return _resistanceMap.TryGetValue(itemId, out var entry)
                ? entry.ResistanceCount
                : 0f;
        }

        /// <summary>
        /// 获取药抗条目 (UI 显示用)
        /// </summary>
        public ResistanceEntry GetResistanceEntry(string itemId)
        {
            _resistanceMap.TryGetValue(itemId, out var entry);
            return entry;
        }

        /// <summary>
        /// 获取所有药抗条目 (UI 列表用)
        /// </summary>
        public List<ResistanceEntry> GetAllResistanceEntries()
        {
            return _resistanceSerializedList;
        }

        /// <summary>
        /// 获取有药抗的丹药数量
        /// </summary>
        public int GetResistanceCount() => _resistanceSerializedList.Count;

        /// <summary>
        /// 获取下次服用的效果提示文本
        /// </summary>
        public string GetNextEffectHint(string itemId)
        {
            if (!_resistanceMap.TryGetValue(itemId, out var entry))
                return "下次效果: 100%";

            float nextCount = Mathf.Min(entry.ResistanceCount + GetResistanceIncrement(entry.LastQuality), 5f);
            if (nextCount >= 3f) return "下次效果: 0% (已达最大抗性)";
            if (nextCount >= 2f) return "下次效果: 40%";
            if (nextCount >= 1f) return "下次效果: 70%";
            return "下次效果: 100%";
        }

        /// <summary>
        /// 获取品质对应的抗性积累基数
        /// </summary>
        private float GetResistanceIncrement(PillQuality quality)
        {
            return quality switch
            {
                PillQuality.Legendary => legendaryIncrement,
                PillQuality.High => highQualityIncrement,
                _ => baseResistanceIncrement
            };
        }

        /// <summary>
        /// 获取品质对应的每日衰减量
        /// </summary>
        private float GetDailyDecay(PillQuality quality)
        {
            return quality switch
            {
                PillQuality.Legendary => dailyDecayLegendary,
                PillQuality.High => dailyDecayHigh,
                _ => dailyDecayNormal
            };
        }

        #endregion

        #region ─── 每日衰减 ─────────────────────────────────────────────────

        /// <summary>
        /// 推进一日, 所有药抗计数衰减
        /// 调用方: GameTimeManager 或 SaveManager (每日触发一次)
        /// </summary>
        public void ProgressDay()
        {
            if (_resistanceSerializedList.Count == 0) return;

            var decayedIds = new List<string>();
            int decayedCount = 0;

            for (int i = _resistanceSerializedList.Count - 1; i >= 0; i--)
            {
                var entry = _resistanceSerializedList[i];
                float decay = GetDailyDecay(entry.LastQuality);
                entry.ResistanceCount = Mathf.Max(0f, entry.ResistanceCount - decay);

                if (entry.ResistanceCount <= 0f)
                {
                    _resistanceSerializedList.RemoveAt(i);
                    _resistanceMap.Remove(entry.ItemId);
                    decayedIds.Add(entry.ItemId);
                    decayedCount++;
                }
                else
                {
                    decayedIds.Add(entry.ItemId);
                    decayedCount++;
                }
            }

            // 重置低耐久每日警告
            _lowDurabilityWarnedToday.Clear();

            if (decayedCount > 0)
            {
                EventBus.Publish(new MedicineResistanceDecayedEvent
                {
                    ItemIds = decayedIds.ToArray(),
                    ItemsDecayed = decayedCount
                });
            }

            Debug.Log($"[MedicineResistance] 日更: {decayedCount} 种丹药抗性衰减");
        }

        /// <summary>
        /// 强制清除指定丹药的抗性
        /// </summary>
        public void ClearResistance(string itemId)
        {
            if (_resistanceMap.TryGetValue(itemId, out var entry))
            {
                _resistanceSerializedList.Remove(entry);
                _resistanceMap.Remove(itemId);

                EventBus.Publish(new MedicineResistanceChangedEvent
                {
                    ItemId = itemId,
                    NewResistanceCount = "0f",
                    EffectMultiplier = 1.0f
                });
            }
        }

        /// <summary>
        /// 清除所有药抗
        /// </summary>
        public void ClearAllResistance()
        {
            _resistanceSerializedList.Clear();
            _resistanceMap.Clear();
        }

        #endregion

        #region ─── 装备耐久 API ─────────────────────────────────────────────

        /// <summary>
        /// 注册一件装备 (创建时调用)
        /// </summary>
        public void RegisterEquipment(string itemId, string displayName, float maxDurability,
                                       EquipmentQuality quality = EquipmentQuality.R)
        {
            if (_equipmentMap.ContainsKey(itemId))
            {
                Debug.LogWarning($"[MedicineResistance] 装备已注册: {itemId}");
                return;
            }

            var entry = new EquipmentEntry
            {
                ItemId = itemId,
                DisplayName = displayName,
                CurrentDurability = maxDurability,
                MaxDurability = maxDurability,
                IsBroken = "false",
                Quality = quality
            };

            _equipmentMap[itemId] = entry;
            _equipmentSerializedList.Add(entry);
        }

        /// <summary>
        /// 移除一件装备 (丢弃/销毁时调用)
        /// </summary>
        public void UnregisterEquipment(string itemId)
        {
            if (_equipmentMap.TryGetValue(itemId, out var entry))
            {
                _equipmentSerializedList.Remove(entry);
                _equipmentMap.Remove(itemId);
            }
        }

        /// <summary>
        /// 消耗耐久 (战斗结束时调用)
        /// </summary>
        public void ConsumeCombatDurability(string itemId, int hitsReceived = 0)
        {
            if (!TryGetEquipmentValid(itemId, out var entry)) return;

            float cost = combatBaseDurabilityCost + hitsReceived * combatHitDurabilityCost;
            ApplyDurabilityDamage(entry, cost);
        }

        /// <summary>
        /// 消耗耐久 (炼丹/炼器时调用)
        /// </summary>
        public void ConsumeCraftingDurability(string itemId)
        {
            if (!TryGetEquipmentValid(itemId, out var entry)) return;

            ApplyDurabilityDamage(entry, craftingDurabilityCost);
        }

        /// <summary>
        /// 直接消耗指定数值的耐久
        /// </summary>
        public void ConsumeDurability(string itemId, float amount)
        {
            if (!TryGetEquipmentValid(itemId, out var entry)) return;

            ApplyDurabilityDamage(entry, amount);
        }

        /// <summary>
        /// 修理装备, 返回实际修理量
        /// </summary>
        public float RepairEquipment(string itemId, float amount)
        {
            if (!_equipmentMap.TryGetValue(itemId, out var entry))
            {
                Debug.LogWarning($"[MedicineResistance] 装备不存在: {itemId}");
                return 0f;
            }

            float before = entry.CurrentDurability;
            entry.CurrentDurability = Mathf.Min(entry.MaxDurability, entry.CurrentDurability + amount);
            float actualRepair = entry.CurrentDurability - before;

            if (entry.IsBroken && entry.CurrentDurability > 0f)
            {
                entry.IsBroken = false;
            }

            if (actualRepair > 0f)
            {
                EventBus.Publish(new EquipmentRepairedEvent
                {
                    ItemId = itemId,
                    DisplayName = entry.DisplayName,
                    AmountRepaired = actualRepair,
                    CurrentDurability = entry.CurrentDurability,
                    IsFullyRepaired = entry.CurrentDurability >= entry.MaxDurability
                });

                EventBus.Publish(new EquipmentDurabilityChangedEvent
                {
                    ItemId = itemId,
                    DisplayName = entry.DisplayName,
                    CurrentDurability = entry.CurrentDurability,
                    MaxDurability = entry.MaxDurability,
                    DurabilityPercent = entry.DurabilityPercent,
                    DamageTaken = 0f
                });
            }

            Debug.Log($"[MedicineResistance] 修理 {entry.DisplayName}: +{actualRepair:F1} " +
                      $"({entry.CurrentDurability}/{entry.MaxDurability})");
            return actualRepair;
        }

        /// <summary>
        /// 满修装备
        /// </summary>
        public void FullyRepairEquipment(string itemId)
        {
            RepairEquipment(itemId, GetMaxDurability(itemId));
        }

        /// <summary>
        /// 获取装备的属性倍率 (破损后仅保留部分)
        /// </summary>
        public float GetEquipmentStatMultiplier(string itemId)
        {
            if (!_equipmentMap.TryGetValue(itemId, out var entry))
                return 1f;

            if (entry.IsBroken || entry.CurrentDurability <= 0f)
                return brokenStatMultiplier;

            return 1f;
        }

        /// <summary>
        /// 检查装备是否可用
        /// </summary>
        public bool IsEquipmentFunctional(string itemId)
        {
            return _equipmentMap.TryGetValue(itemId, out var entry) && entry.IsFunctioning;
        }

        /// <summary>
        /// 获取装备耐久数据
        /// </summary>
        public EquipmentEntry GetEquipmentEntry(string itemId)
        {
            _equipmentMap.TryGetValue(itemId, out var entry);
            return entry;
        }

        /// <summary>
        /// 获取指定装备的当前耐久
        /// </summary>
        public float GetCurrentDurability(string itemId)
        {
            return _equipmentMap.TryGetValue(itemId, out var entry)
                ? entry.CurrentDurability
                : 0f;
        }

        /// <summary>
        /// 获取指定装备的最大耐久
        /// </summary>
        public float GetMaxDurability(string itemId)
        {
            return _equipmentMap.TryGetValue(itemId, out var entry)
                ? entry.MaxDurability
                : 0f;
        }

        /// <summary>
        /// 获取所有装备耐久条目 (UI 列表用)
        /// </summary>
        public List<EquipmentEntry> GetAllEquipmentEntries()
        {
            return _equipmentSerializedList;
        }

        /// <summary>
        /// 获取耐久低于阈值的装备数量
        /// </summary>
        public int GetLowDurabilityCount()
        {
            int count = 0;
            foreach (var entry in _equipmentSerializedList)
            {
                if (entry.NeedsRepair && entry.IsLowDurability)
                    count++;
            }
            return count;
        }

        #endregion

        #region ─── 装备内部逻辑 ─────────────────────────────────────────────

        /// <summary>TryGet 且校验装备有效</summary>
        private bool TryGetEquipmentValid(string itemId, out EquipmentEntry entry)
        {
            entry = null;
            if (!_equipmentMap.TryGetValue(itemId, out entry))
            {
                Debug.LogWarning($"[MedicineResistance] 装备未注册: {itemId}");
                return false;
            }
            return true;
        }

        /// <summary>应用耐久伤害 (含破碎判断 + 事件)</summary>
        private void ApplyDurabilityDamage(EquipmentEntry entry, float damage)
        {
            if (entry.IsBroken || entry.CurrentDurability <= 0f)
                return;

            float before = entry.CurrentDurability;
            entry.CurrentDurability = Mathf.Max(0f, entry.CurrentDurability - damage);
            float actualDamage = before - entry.CurrentDurability;

            // 检查是否破损
            bool justBroke = false;
            if (entry.CurrentDurability <= 0f && !entry.IsBroken)
            {
                entry.IsBroken = true;
                justBroke = true;
            }

            // 发布耐久变化事件
            EventBus.Publish(new EquipmentDurabilityChangedEvent
            {
                ItemId = entry.ItemId,
                DisplayName = entry.DisplayName,
                CurrentDurability = entry.CurrentDurability,
                MaxDurability = entry.MaxDurability,
                DurabilityPercent = entry.DurabilityPercent,
                DamageTaken = actualDamage
            });

            // 低耐久警告 (每日一次)
            if (entry.IsLowDurability && !_lowDurabilityWarnedToday.Contains(entry.ItemId))
            {
                _lowDurabilityWarnedToday.Add(entry.ItemId);
                // UI 层监听 EquipmentDurabilityChangedEvent 自行判断显示
            }

            // 破碎事件
            if (justBroke)
            {
                EventBus.Publish(new EquipmentBrokenEvent
                {
                    ItemId = entry.ItemId,
                    DisplayName = entry.DisplayName
                });

                Debug.Log($"[MedicineResistance] {entry.DisplayName} 已破损!");
            }
        }

        #endregion

        #region ─── 工具方法 ─────────────────────────────────────────────────

        /// <summary>获取当前游戏天数 (从 GameTimeManager 或 System time).</summary>
        private float GetGameTimeDays()
        {
            // 如果项目有 GameTimeManager, 替换为调用.
            // 兜底: 用 Time.time 除以游戏日秒数 (默认为现实1天=24h of game time)
            // 此处假设外部注入, 纯逻辑层不做强耦合.
            return Time.time / 86400f;
        }

        /// <summary>获取品质对应的衰减量 (供外部调用).</summary>
        public float GetDecayForQuality(PillQuality quality)
        {
            return GetDailyDecay(quality);
        }

        /// <summary>获取品质对应的抗性积累基数 (供外部调用).</summary>
        public float GetIncrementForQuality(PillQuality quality)
        {
            return GetResistanceIncrement(quality);
        }

        #endregion

        #region ─── 调试 ──────────────────────────────────────────────────────

        /// <summary>调试状态字符串</summary>
        public string GetDebugStatus()
        {
            string output = "=== MedicineResistance Status ===\n";
            output += $"[药抗] {_resistanceSerializedList.Count} 种丹药有抗性:\n";
            foreach (var entry in _resistanceSerializedList)
            {
                float mult = entry.GetEffectMultiplier(firstDoseMultiplier, secondDoseMultiplier,
                    thirdDoseMultiplier, zeroMultiplier);
                output += $"  {entry.ItemName} (count={entry.ResistanceCount:F2}, eff={mult:P0})\n";
            }

            output += $"\n[耐久] {_equipmentSerializedList.Count} 件装备:\n";
            foreach (var entry in _equipmentSerializedList)
            {
                output += $"  {entry.DisplayName}: {entry.CurrentDurability:F0}/{entry.MaxDurability}" +
                          (entry.IsBroken ? " [破损!]" : "") + "\n";
            }

            return output;
        }

        // ─── Test Helpers ───────────────────────────────────────────────

        /// <summary>注册测试装备</summary>
        public EquipmentEntry RegisterTestEquipment(string name = "测试装备",
                                                      float maxDurability = 100f)
        {
            string id = "test_equip_" + Guid.NewGuid().ToString("N");
            RegisterEquipment(id, name, maxDurability);
            return _equipmentMap[id];
        }

        /// <summary>快速测试: 连续服用指定丹药 n 次, 返回效果倍率序列</summary>
        public float[] TestConsecutiveDoses(string itemId, string itemName, int count,
                                              PillQuality quality = PillQuality.Low)
        {
            ClearResistance(itemId);
            var results = new float[count];
            for (int i = 0; i < count; i++)
            {
                results[i] = RecordConsumption(itemId, itemName, quality);
            }
            return results;
        }

        #endregion
    }
}
