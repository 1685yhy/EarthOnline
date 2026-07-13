using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    #region Enums

    /// <summary>Five risk levels for zone display (RSK-01 ~ RSK-06).</summary>
    public enum RiskLevel
    {
        Safe,       // < 0.2
        Low,        // 0.2 ~ 0.4
        Medium,     // 0.4 ~ 0.6
        High,       // 0.6 ~ 0.8
        Extreme     // >= 0.8
    }

    #endregion

    #region Data Structures

    /// <summary>Runtime data for each zone's risk configuration.</summary>
    [System.Serializable]
    public class ZoneRiskData
    {
        public string ZoneId;
        public string ZoneName;

        [Range(0f, 100f)]
        public float BaseRiskRating;        // 0~100 base difficulty of the zone

        public string ThreatType;            // e.g., "妖兽", "阵法", "天灾"

        public Bounds ZoneBounds;            // World-space AABB boundary
    }

    /// <summary>Serializable data snapshot for save/load.</summary>
    [System.Serializable]
    public class RiskRatingSaveData
    {
        public string[] ConfirmedZoneIds;
    }

    #endregion

    /// <summary>
    /// Risk Rating System (Story 002) — evaluates zone danger relative to player power.
    ///
    /// Formula: RiskFactor = Clamp01((BaseRiskRating - PlayerEffectivePower) / BaseRiskRating)
    ///
    /// Features:
    /// - 5-level risk display (安全/低风险/中等/高风险/极度危险)
    /// - 50m boundary edge warning (RSK-01)
    /// - Zone crossing confirmation panel (RSK-02)
    /// - Dynamic risk by player realm (RSK-03)
    /// - Night risk modifier +15 (RSK-04)
    /// - Dynamic event modifier +20 (RSK-05)
    /// - Confirm-and-enter, no blocking (RSK-07)
    /// - HUD corner persistent indicator via EventBus (RSK-08)
    /// - RiskFactor influences death cultivation loss (RSK-09)
    /// </summary>
    public class RiskRating : MonoBehaviour
    {
        #region Constants

        private const float WARNING_DISTANCE = 50f;             // 50m edge warning (RSK-01)
        private const float CHECK_INTERVAL = 0.3f;              // re-check every 0.3s
        private const int DEFAULT_PLAYER_POWER = 30;            // placeholder (~Foundation初期)

        // Risk modifiers (RSK-04, RSK-05)
        private const int NIGHT_RISK_MODIFIER = 15;
        private const int EVENT_RISK_MODIFIER = 20;

        // Level thresholds
        private const float LEVEL_SAFE_THRESHOLD = 0.2f;
        private const float LEVEL_LOW_THRESHOLD = 0.4f;
        private const float LEVEL_MEDIUM_THRESHOLD = 0.6f;
        private const float LEVEL_HIGH_THRESHOLD = 0.8f;

        #endregion

        #region Singleton

        public static RiskRating Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Zone Definitions")]
        [SerializeField] private ZoneRiskData[] _zoneDefinitions;

        [Header("Player Reference")]
        [SerializeField] private string _playerTag = "Player";

        [Header("Debug")]
        [SerializeField] private bool _debugMode;

        #endregion

        #region Private State

        // Player tracking
        private Transform _playerTransform;
        private Vector3 _lastPlayerPosition;
        private float _checkCooldown;

        // Current zone
        private ZoneRiskData _currentZone;
        private string _currentZoneId;
        private bool _hasEnteredCurrentZone;          // true after confirmation (RSK-07)

        // Boundary proximity (RSK-01)
        private ZoneRiskData _nearestBoundaryZone;
        private float _nearestBoundaryDistance;
        private bool _isWarningActive;
        private float _lastWarningIntensity;

        // Risk level state
        private float _currentRiskFactor;
        private RiskLevel _currentRiskLevel;
        private RiskLevel _previousRiskLevel;

        // Environmental modifiers
        private bool _isNightTime;
        private int _eventModifier;

        // Crossing confirmation (RSK-02)
        private string _pendingCrossingZoneId;
        private bool _crossingPanelOpen;

        // Confirmed zone IDs (persists across sessions within a play-through)
        private HashSet<string> _confirmedZoneIds = new HashSet<string>();

        #endregion

        #region Public Properties

        /// <summary>Current risk factor (0~1).</summary>
        public float CurrentRiskFactor => _currentRiskFactor;

        /// <summary>Whether it is currently nighttime.</summary>
        public bool IsNight => _isNightTime;

        /// <summary>Current risk level enum.</summary>
        public RiskLevel CurrentRiskLevel => _currentRiskLevel;

        /// <summary>Current zone data (null if outside all zones).</summary>
        public ZoneRiskData CurrentZone => _currentZone;

        /// <summary>Whether the player is within 50m of any zone boundary.</summary>
        public bool IsNearBoundary => _isWarningActive;

        /// <summary>Distance to the nearest zone boundary edge.</summary>
        public float NearestBoundaryDistance => _nearestBoundaryDistance;

        /// <summary>Chinese display name for the current risk level.</summary>
        public string RiskLevelName
        {
            get
            {
                switch (_currentRiskLevel)
                {
                    case RiskLevel.Safe:    return "安全";
                    case RiskLevel.Low:     return "低风险";
                    case RiskLevel.Medium:  return "中等风险";
                    case RiskLevel.High:    return "高风险";
                    case RiskLevel.Extreme: return "极度危险";
                    default:                return "未知";
                }
            }
        }

        /// <summary>Color mapping for HUD display: green -> yellow -> orange -> red.</summary>
        public Color RiskLevelColor
        {
            get
            {
                switch (_currentRiskLevel)
                {
                    case RiskLevel.Safe:    return Color.green;
                    case RiskLevel.Low:     return Color.yellow;
                    case RiskLevel.Medium:  return new Color(1f, 0.64f, 0f);    // Orange
                    case RiskLevel.High:    return new Color(1f, 0.27f, 0f);    // DarkOrange
                    case RiskLevel.Extreme: return Color.red;
                    default:                return Color.white;
                }
            }
        }

        /// <summary>Intensity of the current boundary warning (0~1).</summary>
        public float WarningIntensity
        {
            get
            {
                if (!_isWarningActive) return 0f;
                return Mathf.Clamp01(1f - (_nearestBoundaryDistance / WARNING_DISTANCE));
            }
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

            _checkCooldown = 0f;
            _currentRiskFactor = 0f;
            _currentRiskLevel = RiskLevel.Safe;
            _previousRiskLevel = RiskLevel.Safe;
            _hasEnteredCurrentZone = false;
            _isWarningActive = false;
            _crossingPanelOpen = false;
            _eventModifier = 0;
            _isNightTime = false;
            _lastWarningIntensity = 0f;
        }

        private void Start()
        {
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            _checkCooldown -= Time.deltaTime;
            if (_checkCooldown > 0f) return;
            _checkCooldown = CHECK_INTERVAL;

            if (!ResolvePlayer())
                return;

            EvaluateZoneProximity();
            UpdateRiskLevel();
        }

        #endregion

        #region EventBus Subscription

        private void SubscribeEvents()
        {
            EventBus.Subscribe<RiskCrossingConfirmedEvent>(OnCrossingConfirmed);
            EventBus.Subscribe<RiskCrossingDeclinedEvent>(OnCrossingDeclined);
            EventBus.Subscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
            EventBus.Subscribe<DynamicEventActiveEvent>(OnDynamicEventChanged);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<RiskCrossingConfirmedEvent>(OnCrossingConfirmed);
            EventBus.Unsubscribe<RiskCrossingDeclinedEvent>(OnCrossingDeclined);
            EventBus.Unsubscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
            EventBus.Unsubscribe<DynamicEventActiveEvent>(OnDynamicEventChanged);
        }

        private void OnCrossingConfirmed(RiskCrossingConfirmedEvent evt)
        {
            if (evt.ZoneId == _pendingCrossingZoneId)
            {
                _hasEnteredCurrentZone = true;
                _crossingPanelOpen = false;
                _confirmedZoneIds.Add(_pendingCrossingZoneId);
                _pendingCrossingZoneId = null;

                // Notify other systems that player entered the zone
                EventBus.Publish(new RiskZoneEnteredEvent
                {
                    ZoneId = _currentZone?.ZoneId,
                    ZoneName = _currentZone?.ZoneName,
                    RiskLevel = _currentRiskLevel,
                    RiskFactor = _currentRiskFactor
                });

                if (_debugMode)
                    Debug.Log($"[RiskRating] Player confirmed entry to zone: {_currentZone?.ZoneName}");
            }
        }

        private void OnCrossingDeclined(RiskCrossingDeclinedEvent evt)
        {
            if (evt.ZoneId == _pendingCrossingZoneId)
            {
                _crossingPanelOpen = false;
                _pendingCrossingZoneId = null;

                if (_debugMode)
                    Debug.Log($"[RiskRating] Player declined entry to zone: {evt.ZoneId}");
            }
        }

        private void OnTimeOfDayChanged(TimeOfDayChangedEvent evt)
        {
            _isNightTime = evt.IsNight is true;

            if (_debugMode)
                Debug.Log($"[RiskRating] Time of day changed: {((evt.IsNight is true) ? "Night" : "Day")} (RSK-04)");
        }

        private void OnDynamicEventChanged(DynamicEventActiveEvent evt)
        {
            _eventModifier = (evt.IsActive is true) ? EVENT_RISK_MODIFIER : 0;

            if (_debugMode)
                Debug.Log($"[RiskRating] Dynamic event modifier: {_eventModifier} (RSK-05)");
        }

        #endregion

        #region Player Resolution

        /// <summary>Find the player GameObject by tag (called each check cycle until found).</summary>
        private bool ResolvePlayer()
        {
            if (_playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
                if (player != null)
                    _playerTransform = player.transform;
            }
            return _playerTransform != null;
        }

        #endregion

        #region Zone Proximity Evaluation (RSK-01, RSK-02)

        /// <summary>
        /// Evaluate the player's position relative to all defined zones.
        /// Determines if player is inside a zone, near a boundary, or outside.
        /// </summary>
        private void EvaluateZoneProximity()
        {
            Vector3 playerPos = _playerTransform.position;

            // Track the closest boundary zone across all zones
            ZoneRiskData closestBoundaryZone = null;
            float closestBoundaryDist = float.MaxValue;
            bool playerInsideAnyZone = false;

            foreach (var zone in _zoneDefinitions)
            {
                Bounds bounds = zone.ZoneBounds;

                if (bounds.Contains(playerPos))
                {
                    // ── Player is inside this zone ──
                    playerInsideAnyZone = true;
                    bool isNewZone = _currentZoneId != zone.ZoneId;

                    if (isNewZone)
                    {
                        HandleZoneEntry(zone);
                    }

                    // Update current zone ref
                    _currentZone = zone;
                    _currentZoneId = zone.ZoneId;

                    // Check distance to this zone's boundary edge (for internal boundary warning)
                    float distToEdge = DistanceToBoundaryEdge(playerPos, bounds);

                    // Find nearest neighboring zone outside this one
                    FindNearestNeighborZone(playerPos, zone, out closestBoundaryZone, out closestBoundaryDist);

                    // Override distance if we're also near the boundary of this zone
                    if (distToEdge < closestBoundaryDist)
                    {
                        closestBoundaryDist = distToEdge;
                        // closestBoundaryZone stays as the neighbor (for the "what's beyond" info)
                    }

                    break; // Player can only be inside one zone
                }
            }

            if (!playerInsideAnyZone)
            {
                // Player is outside all zones — find nearest zone boundary
                foreach (var zone in _zoneDefinitions)
                {
                    Vector3 closestPoint = zone.ZoneBounds.ClosestPoint(playerPos);
                    float dist = Vector3.Distance(playerPos, closestPoint);

                    if (dist < closestBoundaryDist)
                    {
                        closestBoundaryDist = dist;
                        closestBoundaryZone = zone;
                    }
                }

                // Clear current zone state
                if (_currentZone != null)
                {
                    _currentZone = null;
                    _currentZoneId = null;
                    _hasEnteredCurrentZone = false;
                }
            }

            // Update boundary warning state (RSK-01)
            UpdateBoundaryWarning(closestBoundaryZone, closestBoundaryDist);
        }

        /// <summary>
        /// Handle a player entering a new zone.
        /// Shows confirmation panel on first entry (RSK-02, RSK-07).
        /// </summary>
        private void HandleZoneEntry(ZoneRiskData zone)
        {
            bool alreadyConfirmed = _confirmedZoneIds.Contains(zone.ZoneId);

            if (!alreadyConfirmed && !_crossingPanelOpen && _pendingCrossingZoneId != zone.ZoneId)
            {
                // First time crossing into this zone — show confirmation panel (RSK-02)
                _pendingCrossingZoneId = zone.ZoneId;
                _crossingPanelOpen = true;

                EventBus.Publish(new RiskCrossingConfirmEvent
                {
                    ZoneId = zone.ZoneId,
                    ZoneName = zone.ZoneName,
                    BaseRiskRating = zone.BaseRiskRating,
                    RiskLevel = _currentRiskLevel,
                    RiskLevelName = RiskLevelName,
                    ThreatType = zone.ThreatType,
                    RiskFactor = _currentRiskFactor
                });

                if (_debugMode)
                    Debug.Log($"[RiskRating] Crossing confirmation shown for zone: {zone.ZoneName} (RSK-02)");
            }

            if (alreadyConfirmed && !_hasEnteredCurrentZone)
            {
                _hasEnteredCurrentZone = true;

                // Fire entry event for already-confirmed zones
                EventBus.Publish(new RiskZoneEnteredEvent
                {
                    ZoneId = zone.ZoneId,
                    ZoneName = zone.ZoneName,
                    RiskLevel = _currentRiskLevel,
                    RiskFactor = _currentRiskFactor
                });
            }
        }

        /// <summary>
        /// Find the nearest neighboring zone (for showing what's beyond the boundary).
        /// </summary>
        private void FindNearestNeighborZone(Vector3 playerPos, ZoneRiskData currentZone,
            out ZoneRiskData nearestZone, out float nearestDist)
        {
            nearestZone = null;
            nearestDist = float.MaxValue;

            foreach (var zone in _zoneDefinitions)
            {
                if (zone.ZoneId == currentZone.ZoneId) continue;

                Vector3 closest = zone.ZoneBounds.ClosestPoint(playerPos);
                float dist = Vector3.Distance(playerPos, closest);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestZone = zone;
                }
            }
        }

        /// <summary>
        /// Update boundary warning state and publish events (RSK-01).
        /// </summary>
        private void UpdateBoundaryWarning(ZoneRiskData nearestZone, float nearestDist)
        {
            bool wasWarningActive = _isWarningActive;

            // Warning activates when within 50m of a boundary and not in a confirmed state
            _isWarningActive = nearestZone != null && nearestDist <= WARNING_DISTANCE;
            _nearestBoundaryZone = nearestZone;
            _nearestBoundaryDistance = nearestDist;

            // Publish warning event (RSK-01)
            if (_isWarningActive && nearestZone != null)
            {
                float intensity = Mathf.Clamp01(1f - (nearestDist / WARNING_DISTANCE));
                _lastWarningIntensity = intensity;

                EventBus.Publish(new RiskBoundaryWarningEvent
                {
                    ZoneId = nearestZone.ZoneId,
                    ZoneName = nearestZone.ZoneName,
                    Distance = nearestDist,
                    WarningIntensity = intensity,
                    ThreatType = nearestZone.ThreatType
                });
            }
            else if (wasWarningActive && !_isWarningActive)
            {
                // Clear warning
                _lastWarningIntensity = 0f;

                EventBus.Publish(new RiskBoundaryWarningEvent
                {
                    ZoneId = null,
                    ZoneName = null,
                    Distance = float.MaxValue,
                    WarningIntensity = 0f,
                    ThreatType = null
                });
            }
        }

        /// <summary>
        /// Distance from a point to the nearest point on the boundary edge of a Bounds.
        /// Returns 0 if the point is inside the Bounds.
        /// </summary>
        private float DistanceToBoundaryEdge(Vector3 point, Bounds bounds)
        {
            Vector3 closest = bounds.ClosestPoint(point);
            return Vector3.Distance(point, closest);
        }

        #endregion

        #region Risk Level Calculation (RSK-03, RSK-04, RSK-05, RSK-06, RSK-09)

        /// <summary>
        /// Calculate the current risk factor and level.
        /// Formula: RiskFactor = Clamp01((BaseRiskRating - PlayerEffectivePower) / BaseRiskRating)
        /// </summary>
        private void UpdateRiskLevel()
        {
            if (_currentZone == null)
            {
                // Outside any zone — no risk
                _currentRiskFactor = 0f;
                _previousRiskLevel = _currentRiskLevel;
                _currentRiskLevel = RiskLevel.Safe;

                // Publish if level changed from non-safe
                if (_previousRiskLevel != RiskLevel.Safe)
                {
                    PublishLevelChanged();
                }
                return;
            }

            float playerPower = CalculatePlayerEffectivePower();
            float baseRating = _currentZone.BaseRiskRating;

            // Apply environmental modifiers (RSK-04, RSK-05)
            float effectiveRating = baseRating;
            if (_isNightTime)
                effectiveRating += NIGHT_RISK_MODIFIER;
            effectiveRating += _eventModifier;

            // RiskFactor = Clamp01((BaseRiskRating - PlayerEffectivePower) / BaseRiskRating)
            _currentRiskFactor = Mathf.Clamp01(
                (effectiveRating - playerPower) / Mathf.Max(effectiveRating, 0.001f)
            );

            // Evaluate level
            _previousRiskLevel = _currentRiskLevel;
            _currentRiskLevel = EvaluateLevel(_currentRiskFactor);

            // Publish on change
            if (_currentRiskLevel != _previousRiskLevel)
            {
                PublishLevelChanged();

                if (_debugMode)
                    Debug.Log($"[RiskRating] Level changed: {_previousRiskLevel} -> {_currentRiskLevel}, " +
                              $"factor={_currentRiskFactor:F3}, zone={_currentZone.ZoneName} (RSK-03)");
            }
        }

        /// <summary>
        /// Calculate player's effective power based on cultivation realm (RSK-03).
        /// Placeholder pending CultivationManager integration.
        /// Realm mapping: 练气 = 10, 筑基 = 20, 结丹 = 40, 元婴 = 70, 化神 = 110, etc.
        /// </summary>
        private float CalculatePlayerEffectivePower()
        {
            // TODO: Integrate with CultivationManager once available:
            // CultivationRealm realm = CultivationManager.Instance?.CurrentRealm ?? CultivationRealm.QiRefining;
            // float basePower = RealmToPower(realm);
            // float equipmentBonus = EquipmentManager.Instance?.GetPowerBonus() ?? 0f;
            // return basePower + equipmentBonus;

            // Placeholder: returns 30 (~Foundation初期)
            return DEFAULT_PLAYER_POWER;
        }

        /// <summary>Evaluate 5-level risk from a 0~1 factor value.</summary>
        private RiskLevel EvaluateLevel(float factor)
        {
            if (factor < LEVEL_SAFE_THRESHOLD) return RiskLevel.Safe;
            if (factor < LEVEL_LOW_THRESHOLD) return RiskLevel.Low;
            if (factor < LEVEL_MEDIUM_THRESHOLD) return RiskLevel.Medium;
            if (factor < LEVEL_HIGH_THRESHOLD) return RiskLevel.High;
            return RiskLevel.Extreme;
        }

        /// <summary>Publish RiskLevelChangedEvent for HUD / UI listeners (RSK-08).</summary>
        private void PublishLevelChanged()
        {
            EventBus.Publish(new RiskLevelChangedEvent
            {
                PreviousLevel = _previousRiskLevel,
                CurrentLevel = _currentRiskLevel,
                RiskFactor = _currentRiskFactor,
                LevelName = RiskLevelName,
                Color = RiskLevelColor
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Query the risk level for a specific zone by ID.
        /// Useful for map UI, quest system, and other external consumers (RSK-06).
        /// </summary>
        public RiskLevel QueryZoneRisk(string zoneId)
        {
            foreach (var zone in _zoneDefinitions)
            {
                if (zone.ZoneId == zoneId)
                {
                    float playerPower = CalculatePlayerEffectivePower();
                    float effectiveRating = zone.BaseRiskRating;
                    if (_isNightTime) effectiveRating += NIGHT_RISK_MODIFIER;
                    effectiveRating += _eventModifier;

                    float factor = Mathf.Clamp01(
                        (effectiveRating - playerPower) / Mathf.Max(effectiveRating, 0.001f)
                    );
                    return EvaluateLevel(factor);
                }
            }
            return RiskLevel.Safe;
        }

        /// <summary>
        /// Get the numeric risk factor (0~1) for evaluating death cultivation loss (RSK-09).
        /// Higher factor = greater loss on death.
        /// </summary>
        public float GetDeathCultivationLossMultiplier()
        {
            // RSK-09: RiskFactor affects death cultivation loss
            // Base loss multiplier = 0.5 + RiskFactor * 0.5 (ranges 0.5 ~ 1.0)
            return 0.5f + _currentRiskFactor * 0.5f;
        }

        /// <summary>
        /// Mark a zone as confirmed (called when player accepts risk).
        /// </summary>
        public void ConfirmZone(string zoneId)
        {
            if (!_confirmedZoneIds.Contains(zoneId))
            {
                _confirmedZoneIds.Add(zoneId);
            }
        }

        /// <summary>Get the zone definitions array (read-only for external queries).</summary>
        public ZoneRiskData[] GetZoneDefinitions()
        {
            return _zoneDefinitions;
        }

        /// <summary>Get debug status string for console/editor tools.</summary>
        public string GetDebugStatus()
        {
            return $"=== Risk Rating Status ===\n" +
                   $"Current Zone: {_currentZone?.ZoneName ?? "None"}\n" +
                   $"Risk Factor: {_currentRiskFactor:F3}\n" +
                   $"Risk Level: {RiskLevelName} ({(int)_currentRiskLevel})\n" +
                   $"Night Modifier: {(_isNightTime ? $"+{NIGHT_RISK_MODIFIER}" : "Off")}\n" +
                   $"Event Modifier: {(_eventModifier > 0 ? $"+{_eventModifier}" : "Off")}\n" +
                   $"Near Boundary: {_isWarningActive} ({_nearestBoundaryDistance:F1}m)\n" +
                   $"Crossing Panel: {(_crossingPanelOpen ? "Open" : "Closed")}\n" +
                   $"Confirmed Zones: {_confirmedZoneIds.Count}";
        }

        #endregion

        #region Save/Load

        /// <summary>Capture confirmed zone IDs for save (RSK-07 persistence).</summary>
        public RiskRatingSaveData GetSaveData()
        {
            var data = new RiskRatingSaveData();
            data.ConfirmedZoneIds = new string[_confirmedZoneIds.Count];
            _confirmedZoneIds.CopyTo(data.ConfirmedZoneIds);
            return data;
        }

        /// <summary>Restore confirmed zone IDs from save data.</summary>
        public void LoadSaveData(RiskRatingSaveData data)
        {
            if (data?.ConfirmedZoneIds == null)
                return;

            _confirmedZoneIds.Clear();
            foreach (var id in data.ConfirmedZoneIds)
            {
                if (!string.IsNullOrEmpty(id))
                    _confirmedZoneIds.Add(id);
            }

            if (_debugMode)
                Debug.Log($"[RiskRating] Loaded {_confirmedZoneIds.Count} confirmed zones from save.");
        }

        #endregion
    }
}
