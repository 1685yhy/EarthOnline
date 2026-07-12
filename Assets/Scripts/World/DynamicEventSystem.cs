using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EarthOnline.World
{
    #region Enums

    /// <summary>事件类型枚举</summary>
    public enum DynamicEventType
    {
        MonsterWave,     // 妖兽潮
        ResourceBloom,   // 灵物丰收
        WeatherShift,    // 天气异变
        SectCall,        // 门派征召
        TreasureFall,    // 天降宝箱
        Mercenary,       // 佣兵任务
        Disaster         // 天灾
    }

    /// <summary>事件状态</summary>
    public enum DynamicEventState
    {
        Pending,         // 等待触发检查
        Active,          // 进行中
        Merged,          // 已合并为连锁事件
        Completed,       // 已完成
        CleanedUp        // 已清理恢复
    }

    /// <summary>事件互斥级别</summary>
    public enum EventExclusivity
    {
        None,            // 可完全共存
        Soft,            // 可叠加但部分效果互斥 → 合并
        Hard             // 完全互斥 → 强制合并为连锁
    }

    #endregion

    #region Data Structures

    /// <summary>区域事件定义</summary>
    [Serializable]
    public class ZoneEventDefinition
    {
        public string EventId;
        public string DisplayName;
        public string Description;
        public DynamicEventType EventType;
        public EventExclusivity Exclusivity;
        public float BaseTriggerChance = 0.05f;         // 5%/h 基础概率
        public float DurationHours = 2f;                 // 事件持续小时数
        public float ActivityModifier = 1f;              // 活跃度倍率 (×2 max)
        public string[] CompatibleWith;                   // 可共存的事件ID列表
        public string[] MutuallyExclusiveWith;            // 互斥事件ID列表
        public string[] RewardItemIds;                    // 奖励物品ID
        public int MinPlayerLevel;
        public int SpawnCountMin = 3;
        public int SpawnCountMax = 8;
        public Color EventColor = Color.yellow;
    }

    /// <summary>运行时事件实例</summary>
    [Serializable]
    public class DynamicEventInstance
    {
        public string InstanceId;
        public string EventId;
        public string DisplayName;
        public DynamicEventType EventType;
        public DynamicEventState State = DynamicEventState.Pending;
        public EventExclusivity Exclusivity;
        public string ZoneId;
        public string ZoneName;
        public float ElapsedGameHours;
        public float DurationHours;
        public float ActivityModifier;
        public float RiskModifier;
        public string[] RewardItemIds;
        public int SpawnCount;
        public Color EventColor;
        public float TriggerGameTime;                    // world time when triggered
        public List<string> MergedEventIds = new List<string>();  // merged siblings
    }

    /// <summary>连锁事件定义</summary>
    [Serializable]
    public class ChainEventDefinition
    {
        public string ChainId;
        public string DisplayName;
        public string Description;
        public string[] ComponentEventIds;                // 组成事件ID
        public float DurationHours;
        public float RiskModifierBonus = 0.4f;             // 连锁额外风险
        public Color ChainColor = Color.red;
    }

    /// <summary>区域事件运行时快照 — 用来恢复状态</summary>
    [Serializable]
    public struct ZoneSnapshot
    {
        public string ZoneId;
        public float RiskModifier;
        public float ActivityModifier;
        public string[] ResourceAvailability;
        public bool SpawnsActive;
    }

    /// <summary>可序列化的系统状态</summary>
    [Serializable]
    public class DynamicEventSaveData
    {
        public DynamicEventInstance[] ActiveEvents;
        public string[] ActiveChainIds;
        public ZoneSnapshot[] ZoneSnapshots;
        public float LastCheckGameTime;
    }

    #endregion

    #region Event Bus Event Structs

    /// <summary>Published when a dynamic event is triggered.</summary>
    public struct DynamicEventTriggeredEvent
    {
        public string EventId;
        public string DisplayName;
        public DynamicEventType EventType;
        public string ZoneId;
        public string ZoneName;
        public float DurationHours;
        public int SpawnCount;
        public bool IsChainEvent;
        public string ChainId;
        public string[] MergedEventNames;
    }

    /// <summary>Published when a dynamic event completes.</summary>
    public struct DynamicEventCompletedEvent
    {
        public string EventId;
        public string DisplayName;
        public string ZoneId;
        public bool WasChainEvent;
        public string ChainId;
        public float ElapsedGameHours;
    }

    /// <summary>Published when a chain event is formed from merging exclusive events.</summary>
    public struct ChainEventFormedEvent
    {
        public string ChainId;
        public string DisplayName;
        public string[] ComponentEventNames;
        public string ZoneId;
        public float DurationHours;
        public float RiskModifierBonus;
    }

    /// <summary>Published when zone state is restored after event cleanup.</summary>
    public struct ZoneStateRestoredEvent
    {
        public string ZoneId;
        public string ZoneName;
    }

    /// <summary>Published when event count changes for UI updates.</summary>
    public struct ZoneEventCountChangedEvent
    {
        public string ZoneId;
        public int ActiveCount;
        public int MaxConcurrent;
    }

    #endregion

    /// <summary>
    /// 动态事件系统 (Story 007)
    ///
    /// EVT-01: 每区域基于概率触发动态事件 (5%/游戏小时)
    /// EVT-02: 事件触发时区域内玩家收到通知
    /// EVT-03: 事件期间区域风险等级改变
    /// EVT-04: 同一区域最多同时触发3个事件
    /// EVT-05: 互斥事件合并为连锁事件
    /// EVT-06: 事件结束后区域状态恢复
    ///
    /// 概率公式: EventTriggerChance = 0.05/h × ActivityModifier × TimeModifier
    /// </summary>
    public class DynamicEventSystem : MonoBehaviour
    {
        #region Constants

        private const int MAX_CONCURRENT_PER_ZONE = 3;
        private const float BASE_TRIGGER_CHANCE = 0.05f;          // 5%/h
        private const float NIGHT_TIME_MODIFIER = 1.5f;           // 夜晚 ×1.5
        private const float DAY_TIME_MODIFIER = 1.0f;             // 白天 ×1.0
        private const float EVENT_CHECK_INTERVAL_GAME_HOURS = 1f; // 每游戏小时检查一次
        private const float ACTIVITY_MODIFIER_MAX = 2f;           // 活跃度上限 ×2
        private const int MAX_CHAIN_EVENTS = 5;                   // 连锁最大组件数
        private const float WIND_DOWN_TIME = 0.5f;                // 收尾阶段(小时)

        #endregion

        #region Singleton

        public static DynamicEventSystem Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("全局配置")]
        [SerializeField] private float _baseTriggerChance = BASE_TRIGGER_CHANCE;
        [SerializeField] private int _maxConcurrentPerZone = MAX_CONCURRENT_PER_ZONE;
        [SerializeField] private float _checkIntervalGameHours = EVENT_CHECK_INTERVAL_GAME_HOURS;
        [SerializeField] private float _nightModifier = NIGHT_TIME_MODIFIER;
        [SerializeField] private float _dayModifier = DAY_TIME_MODIFIER;
        [SerializeField] private float _activityModifierMax = ACTIVITY_MODIFIER_MAX;

        [Header("区域事件定义")]
        [SerializeField] private ZoneEventDefinition[] _zoneEventDefinitions;

        [Header("连锁事件定义")]
        [SerializeField] private ChainEventDefinition[] _chainEventDefinitions;

        #endregion

        #region Private State

        // ─── Runtime Event Instances ───
        private Dictionary<string, List<DynamicEventInstance>> _zoneActiveEvents
            = new Dictionary<string, List<DynamicEventInstance>>();

        // ─── Active Chain Events ───
        private Dictionary<string, DynamicEventInstance> _activeChains
            = new Dictionary<string, DynamicEventInstance>();

        // ─── Zone Snapshots (pre-event state for restoration) ───
        private Dictionary<string, ZoneSnapshot> _zoneSnapshots
            = new Dictionary<string, ZoneSnapshot>();

        // ─── Tracking state ───
        private float _lastCheckGameTime;
        private bool _isNight;
        private HashSet<string> _zonesWithModifiedRisk = new HashSet<string>();

        // ─── Zone activity modifiers (dynamic, can be overridden by events/players) ───
        private Dictionary<string, float> _zoneActivityModifiers = new Dictionary<string, float>();

        #endregion

        #region Public Properties

        /// <summary>Get active events in a specific zone.</summary>
        public IReadOnlyList<DynamicEventInstance> GetActiveEventsInZone(string zoneId)
        {
            if (_zoneActiveEvents.TryGetValue(zoneId, out var list))
                return list.AsReadOnly();
            return Array.Empty<DynamicEventInstance>();
        }

        /// <summary>Get all active events across all zones.</summary>
        public IEnumerable<DynamicEventInstance> GetAllActiveEvents()
        {
            foreach (var kvp in _zoneActiveEvents)
            {
                foreach (var evt in kvp.Value)
                {
                    if (evt.State == DynamicEventState.Active)
                        yield return evt;
                }
            }
        }

        /// <summary>Get active chain events.</summary>
        public IReadOnlyDictionary<string, DynamicEventInstance> ActiveChains => _activeChains;

        /// <summary>Count of active events in a zone.</summary>
        public int GetActiveEventCount(string zoneId)
        {
            if (_zoneActiveEvents.TryGetValue(zoneId, out var list))
            {
                int count = 0;
                foreach (var e in list)
                {
                    if (e.State == DynamicEventState.Active || e.State == DynamicEventState.Merged)
                        count++;
                }
                return count;
            }
            return 0;
        }

        /// <summary>Get total events active globally.</summary>
        public int TotalActiveEvents
        {
            get
            {
                int count = 0;
                foreach (var kvp in _zoneActiveEvents)
                {
                    foreach (var e in kvp.Value)
                    {
                        if (e.State == DynamicEventState.Active)
                            count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Is night time active? (affects trigger chance).</summary>
        public bool IsNight
        {
            get => _isNight;
            set => _isNight = value;
        }

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
            EventBus.Subscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
        }

        private void Update()
        {
            // Check if we should evaluate event triggers (once per game hour).
            // In real gameplay, this would be driven by the world time system.
            // For now, we simulate game-time progress with scaled real time.
        }

        #endregion

        #region Time Integration

        private void OnTimeOfDayChanged(TimeOfDayChangedEvent evt)
        {
            _isNight = evt.IsNight;
        }

        /// <summary>
        /// Advance the event system by a given number of game hours.
        /// Called by the world time system each game-hour tick.
        /// </summary>
        public void AdvanceGameHours(float hours)
        {
            float totalCheckHours = _lastCheckGameTime + hours;

            // Process checks in intervals of _checkIntervalGameHours.
            while (_lastCheckGameTime + _checkIntervalGameHours <= totalCheckHours)
            {
                _lastCheckGameTime += _checkIntervalGameHours;
                ProcessEventCheck();
                ProcessActiveEventTick(_checkIntervalGameHours);
            }
        }

        /// <summary>Set the current game time (for save loading).</summary>
        public void SetGameTime(float gameTimeHours)
        {
            _lastCheckGameTime = gameTimeHours;
        }

        #endregion

        #region Event Check Cycle

        /// <summary>
        /// Main event check: evaluate all zones for new events.
        /// Called each game hour.
        /// </summary>
        private void ProcessEventCheck()
        {
            if (_zoneEventDefinitions == null || _zoneEventDefinitions.Length == 0)
                return;

            foreach (var zoneEventDef in _zoneEventDefinitions)
            {
                if (zoneEventDef == null) continue;

                string zoneId = zoneEventDef.EventId; // zone identified by the event def's zone scope

                // Check concurrent limit (EVT-04).
                int activeCount = GetActiveEventCount(zoneId);
                if (activeCount >= _maxConcurrentPerZone)
                {
                    // Zone is at capacity — skip.
                    continue;
                }

                // Calculate trigger probability.
                float timeModifier = _isNight ? _nightModifier : _dayModifier;
                float activityMod = GetZoneActivityModifier(zoneId);

                float chance = _baseTriggerChance * activityMod * timeModifier;
                chance = Mathf.Clamp01(chance);

                // Roll for trigger.
                if (Random.value < chance)
                {
                    TriggerEvent(zoneEventDef, zoneId);
                }
            }
        }

        /// <summary>Get the current activity modifier for a zone.</summary>
        private float GetZoneActivityModifier(string zoneId)
        {
            if (_zoneActivityModifiers.TryGetValue(zoneId, out float mod))
            {
                return Mathf.Clamp(mod, 0.5f, _activityModifierMax);
            }
            return 1f;
        }

        /// <summary>Set zone activity modifier (called by other systems).</summary>
        public void SetZoneActivityModifier(string zoneId, float modifier)
        {
            _zoneActivityModifiers[zoneId] = Mathf.Clamp(modifier, 0.5f, _activityModifierMax);
        }

        #endregion

        #region Event Triggering

        /// <summary>Trigger a single event in a zone.</summary>
        private void TriggerEvent(ZoneEventDefinition def, string zoneId)
        {
            // Check exclusivity with existing events in this zone (EVT-05).
            List<DynamicEventInstance> existingEvents = GetZoneActiveEventList(zoneId);
            List<DynamicEventInstance> exclusiveMatches = new List<DynamicEventInstance>();

            foreach (var existing in existingEvents)
            {
                if (existing.State != DynamicEventState.Active) continue;

                if (def.Exclusivity == EventExclusivity.Hard || def.Exclusivity == EventExclusivity.Soft)
                {
                    // Check if mutually exclusive.
                    if (def.MutuallyExclusiveWith != null &&
                        Array.IndexOf(def.MutuallyExclusiveWith, existing.EventId) >= 0)
                    {
                        exclusiveMatches.Add(existing);
                    }
                    // Check if existing is mutually exclusive with this event.
                    // (We'd need to look up existing's definition; for now, cross-check IDs.)
                    if (existing.Exclusivity == EventExclusivity.Hard)
                    {
                        exclusiveMatches.Add(existing);
                    }
                }
            }

            if (exclusiveMatches.Count > 0)
            {
                // Merge into chain event (EVT-05).
                CreateChainEvent(def, zoneId, exclusiveMatches);
                return;
            }

            // Check event cap again (in case merged events changed count).
            if (GetActiveEventCount(zoneId) >= _maxConcurrentPerZone)
                return;

            // Check compatibility — if compatible with all existing, just add.
            bool allCompatible = true;
            foreach (var existing in existingEvents)
            {
                if (existing.State != DynamicEventState.Active) continue;
                if (def.CompatibleWith != null && Array.IndexOf(def.CompatibleWith, existing.EventId) < 0)
                {
                    // Not explicitly compatible, but may still stack if no exclusivity conflict.
                    if (def.Exclusivity == EventExclusivity.None && existing.Exclusivity == EventExclusivity.None)
                        continue; // fine, both are None
                    allCompatible = false;
                    break;
                }
            }

            // Create instance.
            DynamicEventInstance instance = CreateEventInstance(def, zoneId);

            // Apply zone state changes (EVT-03).
            ApplyEventZoneModifiers(instance, zoneId);

            // Add to active list.
            existingEvents.Add(instance);

            // Notify (EVT-02).
            EventBus.Publish(new DynamicEventTriggeredEvent
            {
                EventId = instance.EventId,
                DisplayName = instance.DisplayName,
                EventType = instance.EventType,
                ZoneId = instance.ZoneId,
                ZoneName = instance.ZoneName,
                DurationHours = instance.DurationHours,
                SpawnCount = instance.SpawnCount,
                IsChainEvent = "false",
                ChainId = null,
                MergedEventNames = null
            });

            PublishEventCountChanged(zoneId);

            Debug.Log($"[DynamicEventSystem] 事件触发: {def.DisplayName} 在区域 {instance.ZoneName} " +
                      $"(概率: {def.BaseTriggerChance * GetZoneActivityModifier(zoneId) * (_isNight ? _nightModifier : _dayModifier) * 100:F1}%)");
        }

        /// <summary>Create a chain event from mutually exclusive events.</summary>
        private void CreateChainEvent(ZoneEventDefinition triggerDef, string zoneId,
                                      List<DynamicEventInstance> exclusiveEvents)
        {
            // Save pre-event zone state if not already saved.
            SaveZoneSnapshot(zoneId);

            // Determine chain name from component events.
            List<string> componentNames = new List<string> { triggerDef.DisplayName };
            List<string> componentIds = new List<string> { triggerDef.EventId };

            foreach (var excl in exclusiveEvents)
            {
                componentNames.Add(excl.DisplayName);
                componentIds.Add(excl.EventId);
                // Mark excluded events as merged.
                excl.State = DynamicEventState.Merged;
                excl.MergedEventIds.Add(triggerDef.EventId);
            }

            // Clamp component count.
            if (componentIds.Count > MAX_CHAIN_EVENTS)
            {
                componentIds = componentIds.GetRange(0, MAX_CHAIN_EVENTS);
                componentNames = componentNames.GetRange(0, MAX_CHAIN_EVENTS);
            }

            string chainId = $"chain_{zoneId}_{DateTime.UtcNow.Ticks}";
            string chainName = $"连锁·{string.Join("+", componentNames)}";

            // Calculate chain duration (longer than individual events).
            float totalDuration = triggerDef.DurationHours;
            foreach (var e in exclusiveEvents)
            {
                totalDuration = Mathf.Max(totalDuration, e.DurationHours);
            }
            totalDuration *= 1.5f; // chain lasts 1.5x longer

            // Create chain instance.
            DynamicEventInstance chainInstance = new DynamicEventInstance
            {
                InstanceId = chainId,
                EventId = chainId,
                DisplayName = chainName,
                EventType = DynamicEventType.Disaster, // chain events are "disaster" tier
                State = DynamicEventState.Active,
                Exclusivity = EventExclusivity.Hard,
                ZoneId = zoneId,
                ZoneName = GetZoneName(zoneId),
                ElapsedGameHours = "0f",
                DurationHours = totalDuration,
                ActivityModifier = _activityModifierMax,
                RiskModifier = "0.4f", // chain events are more dangerous
                TriggerGameTime = _lastCheckGameTime,
                MergedEventIds = componentIds,
                EventColor = Color.red
            };

            // Remove old active events that were merged.
            var activeList = GetZoneActiveEventList(zoneId);
            activeList.RemoveAll(e => e.State == DynamicEventState.Merged);

            // Add chain to zone.
            activeList.Add(chainInstance);
            _activeChains[chainId] = chainInstance;

            // Apply strong zone modifiers.
            ApplyChainZoneModifiers(chainInstance, zoneId);

            // Notify.
            EventBus.Publish(new ChainEventFormedEvent
            {
                ChainId = chainId,
                DisplayName = chainName,
                ComponentEventNames = componentNames.ToArray(),
                ZoneId = zoneId,
                DurationHours = totalDuration,
                RiskModifierBonus = 0.4f
            });

            EventBus.Publish(new DynamicEventTriggeredEvent
            {
                EventId = chainId,
                DisplayName = chainName,
                EventType = DynamicEventType.Disaster,
                ZoneId = zoneId,
                ZoneName = GetZoneName(zoneId),
                DurationHours = totalDuration,
                SpawnCount = triggerDef.SpawnCountMax * 2,
                IsChainEvent = "true",
                ChainId = chainId,
                MergedEventNames = componentNames.ToArray()
            });

            PublishEventCountChanged(zoneId);

            Debug.Log($"[DynamicEventSystem] 连锁事件形成: {chainName} 在 {GetZoneName(zoneId)} " +
                      $"(组件: {string.Join(", ", componentNames)})");
        }

        /// <summary>Create an event instance from a definition.</summary>
        private DynamicEventInstance CreateEventInstance(ZoneEventDefinition def, string zoneId)
        {
            return new DynamicEventInstance
            {
                InstanceId = $"{def.EventId}_{DateTime.UtcNow.Ticks}",
                EventId = def.EventId,
                DisplayName = def.DisplayName,
                EventType = def.EventType,
                State = DynamicEventState.Active,
                Exclusivity = def.Exclusivity,
                ZoneId = zoneId,
                ZoneName = GetZoneName(zoneId),
                ElapsedGameHours = "0f",
                DurationHours = def.DurationHours,
                ActivityModifier = def.ActivityModifier,
                RiskModifier = "0.2f", // base risk increase during event
                RewardItemIds = def.RewardItemIds,
                SpawnCount = Random.Range(def.SpawnCountMin, def.SpawnCountMax + 1),
                EventColor = def.EventColor,
                TriggerGameTime = _lastCheckGameTime
            };
        }

        /// <summary>Get or create the active event list for a zone.</summary>
        private List<DynamicEventInstance> GetZoneActiveEventList(string zoneId)
        {
            if (!_zoneActiveEvents.TryGetValue(zoneId, out var list))
            {
                list = new List<DynamicEventInstance>();
                _zoneActiveEvents[zoneId] = list;
            }
            return list;
        }

        /// <summary>Get a zone display name from event definitions (best-effort).</summary>
        private string GetZoneName(string zoneId)
        {
            // Try to find a matching zone definition.
            if (_zoneEventDefinitions != null)
            {
                foreach (var def in _zoneEventDefinitions)
                {
                    if (def != null && def.EventId == zoneId)
                        return def.DisplayName;
                }
            }
            return zoneId;
        }

        #endregion

        #region Active Event Tick

        /// <summary>Process elapsed time for all active events.</summary>
        private void ProcessActiveEventTick(float gameHours)
        {
            List<string> zonesToCleanup = new List<string>();

            foreach (var kvp in _zoneActiveEvents)
            {
                string zoneId = kvp.Key;
                List<DynamicEventInstance> events = kvp.Value;

                for (int i = events.Count - 1; i >= 0; i--)
                {
                    DynamicEventInstance evt = events[i];

                    if (evt.State != DynamicEventState.Active) continue;

                    evt.ElapsedGameHours += gameHours;

                    // Check if event has expired.
                    float effectiveDuration = evt.DurationHours;
                    if (evt.ElapsedGameHours >= effectiveDuration)
                    {
                        // Event completed.
                        CompleteEvent(evt, zoneId, events, i);
                    }
                }

                if (events.Count == 0)
                {
                    zonesToCleanup.Add(zoneId);
                }
            }

            // Cleanup empty zone lists.
            foreach (var zoneId in zonesToCleanup)
            {
                _zoneActiveEvents.Remove(zoneId);
            }
        }

        #endregion

        #region Event Completion & Cleanup

        /// <summary>Complete an event and restore zone state (EVT-06).</summary>
        private void CompleteEvent(DynamicEventInstance evt, string zoneId,
                                   List<DynamicEventInstance> events, int index)
        {
            evt.State = DynamicEventState.Completed;

            // Notify completion.
            EventBus.Publish(new DynamicEventCompletedEvent
            {
                EventId = evt.EventId,
                DisplayName = evt.DisplayName,
                ZoneId = zoneId,
                WasChainEvent = _activeChains.ContainsKey(evt.EventId),
                ChainId = _activeChains.ContainsKey(evt.EventId) ? evt.EventId : null,
                ElapsedGameHours = evt.ElapsedGameHours
            });

            // Remove from list.
            events.RemoveAt(index);

            // If chain event, remove from chains dict.
            if (_activeChains.ContainsKey(evt.EventId))
            {
                _activeChains.Remove(evt.EventId);
            }

            // If no more active events in this zone, restore zone state.
            if (GetActiveEventCount(zoneId) == 0)
            {
                RestoreZoneState(zoneId);
            }

            // Clean up the evt.
            evt.State = DynamicEventState.CleanedUp;

            PublishEventCountChanged(zoneId);

            Debug.Log($"[DynamicEventSystem] 事件结束: {evt.DisplayName} 在 {evt.ZoneName} " +
                      $"(持续 {evt.ElapsedGameHours:F1} 游戏小时)");
        }

        /// <summary>Force-complete all events in a zone (e.g., player relog).</summary>
        public void ForceCompleteAllInZone(string zoneId)
        {
            if (!_zoneActiveEvents.TryGetValue(zoneId, out var events)) return;

            for (int i = events.Count - 1; i >= 0; i--)
            {
                DynamicEventInstance evt = events[i];
                if (evt.State == DynamicEventState.Active || evt.State == DynamicEventState.Merged)
                {
                    evt.State = DynamicEventState.Completed;
                    evt.State = DynamicEventState.CleanedUp;
                }
            }

            events.Clear();
            _zoneActiveEvents.Remove(zoneId);

            // Cleanup chains.
            List<string> chainsToRemove = new List<string>();
            foreach (var kvp in _activeChains)
            {
                if (kvp.Value.ZoneId == zoneId)
                    chainsToRemove.Add(kvp.Key);
            }
            foreach (var cid in chainsToRemove)
                _activeChains.Remove(cid);

            RestoreZoneState(zoneId);
            PublishEventCountChanged(zoneId);

            Debug.Log($"[DynamicEventSystem] 强制清除区域所有事件: {zoneId}");
        }

        #endregion

        #region Zone State Management

        /// <summary>Save the current zone state before an event modifies it.</summary>
        private void SaveZoneSnapshot(string zoneId)
        {
            if (_zoneSnapshots.ContainsKey(zoneId)) return; // already saved

            ZoneRiskData riskData = FindZoneRiskData(zoneId);

            _zoneSnapshots[zoneId] = new ZoneSnapshot
            {
                ZoneId = zoneId,
                RiskModifier = "0f",
                ActivityModifier = GetZoneActivityModifier(zoneId),
                ResourceAvailability = null,
                SpawnsActive = true
            };

            Debug.Log($"[DynamicEventSystem] 保存区域状态快照: {GetZoneName(zoneId)}");
        }

        /// <summary>Apply zone modifiers for a normal event (EVT-03).</summary>
        private void ApplyEventZoneModifiers(DynamicEventInstance evt, string zoneId)
        {
            SaveZoneSnapshot(zoneId);

            // Increase risk in the zone.
            _zonesWithModifiedRisk.Add(zoneId);

            // Publish risk level change for the zone.
            EventBus.Publish(new RiskLevelChangedEvent
            {
                PreviousLevel = RiskLevel.Low,
                CurrentLevel = RiskLevel.Medium,
                RiskFactor = 0.2f + evt.RiskModifier,
                LevelName = "事件活跃",
                Color = evt.EventColor
            });

            // Publish dynamic event active state.
            EventBus.Publish(new DynamicEventActiveEvent
            {
                IsActive = "true",
                EventId = evt.EventId,
                EventName = evt.DisplayName
            });
        }

        /// <summary>Apply zone modifiers for a chain event (more severe).</summary>
        private void ApplyChainZoneModifiers(DynamicEventInstance chainInstance, string zoneId)
        {
            _zonesWithModifiedRisk.Add(zoneId);

            EventBus.Publish(new RiskLevelChangedEvent
            {
                PreviousLevel = RiskLevel.Medium,
                CurrentLevel = RiskLevel.High,
                RiskFactor = 0.4f + chainInstance.RiskModifier,
                LevelName = "连锁事件活跃",
                Color = Color.red
            });

            EventBus.Publish(new DynamicEventActiveEvent
            {
                IsActive = "true",
                EventId = chainInstance.EventId,
                EventName = chainInstance.DisplayName
            });
        }

        /// <summary>Restore zone state after all events complete (EVT-06).</summary>
        private void RestoreZoneState(string zoneId)
        {
            if (!_zoneSnapshots.TryGetValue(zoneId, out var snapshot))
                return;

            _zoneSnapshots.Remove(zoneId);
            _zonesWithModifiedRisk.Remove(zoneId);

            // Publish zone state restored.
            EventBus.Publish(new ZoneStateRestoredEvent
            {
                ZoneId = zoneId,
                ZoneName = GetZoneName(zoneId)
            });

            // Publish risk normalization.
            EventBus.Publish(new RiskLevelChangedEvent
            {
                PreviousLevel = RiskLevel.High,
                CurrentLevel = RiskLevel.Safe,
                RiskFactor = "0f",
                LevelName = "正常",
                Color = Color.green
            });

            // Publish dynamic event deactivation.
            EventBus.Publish(new DynamicEventActiveEvent
            {
                IsActive = "false",
                EventId = "",
                EventName = ""
            });

            Debug.Log($"[DynamicEventSystem] 区域状态已恢复: {GetZoneName(zoneId)}");
        }

        /// <summary>Find ZoneRiskData for a zone ID from RiskRating system.</summary>
        private ZoneRiskData FindZoneRiskData(string zoneId)
        {
            return null; // Runtime data resolved via RiskRating system at runtime.
        }

        /// <summary>Check if a zone currently has active events modifying risk.</summary>
        public bool IsZoneRiskModified(string zoneId)
        {
            return _zonesWithModifiedRisk.Contains(zoneId);
        }

        /// <summary>Get the total risk modifier from events in a zone.</summary>
        public float GetZoneRiskModifier(string zoneId)
        {
            if (!_zoneActiveEvents.TryGetValue(zoneId, out var events))
                return 0f;

            float totalMod = 0f;
            foreach (var e in events)
            {
                if (e.State == DynamicEventState.Active)
                    totalMod += e.RiskModifier;
            }
            return totalMod;
        }

        #endregion

        #region Event Count UI Updates

        private void PublishEventCountChanged(string zoneId)
        {
            EventBus.Publish(new ZoneEventCountChangedEvent
            {
                ZoneId = zoneId,
                ActiveCount = GetActiveEventCount(zoneId),
                MaxConcurrent = _maxConcurrentPerZone
            });
        }

        #endregion

        #region Public API — External Triggering

        /// <summary>
        /// Manually trigger an event in a zone (for quests or admin).
        /// Returns the instance ID if successful, null otherwise.
        /// </summary>
        public string ManualTriggerEvent(string eventId, string zoneId)
        {
            ZoneEventDefinition def = FindEventDefinition(eventId);
            if (def.EventId == null)
            {
                Debug.LogWarning($"[DynamicEventSystem] 未找到事件定义: {eventId}");
                return null;
            }

            if (GetActiveEventCount(zoneId) >= _maxConcurrentPerZone)
            {
                Debug.LogWarning($"[DynamicEventSystem] 区域 {zoneId} 事件已达上限");
                return null;
            }

            DynamicEventInstance instance = CreateEventInstance(def, zoneId);
            ApplyEventZoneModifiers(instance, zoneId);
            GetZoneActiveEventList(zoneId).Add(instance);

            EventBus.Publish(new DynamicEventTriggeredEvent
            {
                EventId = instance.EventId,
                DisplayName = instance.DisplayName,
                EventType = instance.EventType,
                ZoneId = zoneId,
                ZoneName = GetZoneName(zoneId),
                DurationHours = instance.DurationHours,
                SpawnCount = instance.SpawnCount,
                IsChainEvent = "false",
                ChainId = null,
                MergedEventNames = null
            });

            PublishEventCountChanged(zoneId);

            return instance.InstanceId;
        }

        /// <summary>Find a zone event definition by event ID.</summary>
        private ZoneEventDefinition FindEventDefinition(string eventId)
        {
            if (_zoneEventDefinitions == null) return null;
            foreach (var def in _zoneEventDefinitions)
            {
                if (def != null && def.EventId == eventId)
                    return def;
            }
            return null;
        }

        #endregion

        #region Save/Load

        /// <summary>Capture save data for the event system.</summary>
        public DynamicEventSaveData GetSaveData()
        {
            List<DynamicEventInstance> allActive = new List<DynamicEventInstance>();
            foreach (var kvp in _zoneActiveEvents)
            {
                foreach (var e in kvp.Value)
                {
                    if (e.State == DynamicEventState.Active)
                        allActive.Add(e);
                }
            }

            return new DynamicEventSaveData
            {
                ActiveEvents = allActive.ToArray(),
                ActiveChainIds = new List<string>(_activeChains.Keys).ToArray(),
                ZoneSnapshots = new List<ZoneSnapshot>(_zoneSnapshots.Values).ToArray(),
                LastCheckGameTime = _lastCheckGameTime
            };
        }

        /// <summary>Restore system state from save data.</summary>
        public void LoadSaveData(DynamicEventSaveData data)
        {
            if (data == null) return;

            _zoneActiveEvents.Clear();
            _activeChains.Clear();
            _zoneSnapshots.Clear();
            _lastCheckGameTime = data.LastCheckGameTime;

            if (data.ActiveEvents != null)
            {
                foreach (var evt in data.ActiveEvents)
                {
                    if (evt == null) continue;
                    string zoneId = evt.ZoneId;
                    var list = GetZoneActiveEventList(zoneId);
                    list.Add(evt);
                }
            }

            if (data.ActiveChainIds != null)
            {
                foreach (var chainId in data.ActiveChainIds)
                {
                    foreach (var kvp in _zoneActiveEvents)
                    {
                        foreach (var e in kvp.Value)
                        {
                            if (e.InstanceId == chainId)
                            {
                                _activeChains[chainId] = e;
                                break;
                            }
                        }
                    }
                }
            }

            if (data.ZoneSnapshots != null)
            {
                foreach (var snap in data.ZoneSnapshots)
                {
                    _zoneSnapshots[snap.ZoneId] = snap;
                }
            }

            Debug.Log($"[DynamicEventSystem] 加载存档: {TotalActiveEvents} 活跃事件, {_activeChains.Count} 连锁事件");
        }

        /// <summary>Clear all event state (for new game).</summary>
        public void ClearAll()
        {
            _zoneActiveEvents.Clear();
            _activeChains.Clear();
            _zoneSnapshots.Clear();
            _zonesWithModifiedRisk.Clear();
            _lastCheckGameTime = 0f;

            Debug.Log("[DynamicEventSystem] 所有事件状态已清除");
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>Get a debug status string for the event system.</summary>
        public string GetDebugStatus()
        {
            int totalEvents = TotalActiveEvents;
            int totalChains = _activeChains.Count;
            int totalZones = _zoneActiveEvents.Count;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== DynamicEventSystem Status ===");
            sb.AppendLine($"Total Active Events: {totalEvents}");
            sb.AppendLine($"Active Chains: {totalChains}");
            sb.AppendLine($"Active Zones: {totalZones}");
            sb.AppendLine($"Night: {_isNight}");
            sb.AppendLine($"Last Check: {_lastCheckGameTime:F1}h");

            foreach (var kvp in _zoneActiveEvents)
            {
                sb.AppendLine($"  Zone {GetZoneName(kvp.Key)}: {kvp.Value.Count} events");
                foreach (var e in kvp.Value)
                {
                    if (e.State == DynamicEventState.Active)
                    {
                        float remaining = Mathf.Max(0, e.DurationHours - e.ElapsedGameHours);
                        sb.AppendLine($"    - {e.DisplayName} [{remaining:F1}h remaining] " +
                                      $"({e.EventType}) RiskMod: +{e.RiskModifier}");
                    }
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
