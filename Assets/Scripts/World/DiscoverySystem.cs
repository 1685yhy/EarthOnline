using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Core;
using EarthOnline.Framework;

namespace EarthOnline.World
{
    #region Save Data

    /// <summary>Serializable discovery system save data.</summary>
    [Serializable]
    public class DiscoverySystemSaveData
    {
        /// <summary>Records for all discovery points.</summary>
        public DiscoveryRecord[] Records;
    }

    #endregion

    /// <summary>
    /// Three-layer world discovery system for EarthOnline.
    ///
    /// Manages the detection, triggering, and map-marking of three discovery types:
    ///
    /// Layer  | Radius | Conditions      | Map Marker
    /// -------|--------|-----------------|------------------------------
    /// Landmark| 15m   | Auto            | Permanent name marker
    /// POI     | 10m   | Fog cleared     | Question-mark marker
    /// Hidden | 6m    | Realm + weather | No auto-marker, rewards
    ///
    /// Detection probability: P = 0.6 / (1 + (dist / idealRadius)^2)
    ///
    /// Integrates with FogOfWar for POI fog checks, EventBus for UI communication,
    /// CultivationManager for realm conditions, and supports the "神识探查"
    /// (Spirit Gaze) skill for detecting nearby hidden discoveries.
    /// </summary>
    public class DiscoverySystem : MonoBehaviour
    {
        #region Constants

        private const float DEFAULT_LANDMARK_RADIUS = 15f;
        private const float DEFAULT_POI_RADIUS = 10f;
        private const float DEFAULT_HIDDEN_RADIUS = 6f;
        private const float DEFAULT_SCAN_INTERVAL = 0.5f;
        private const float DEFAULT_SPIRIT_SCAN_RADIUS = 25f;
        private const float DEFAULT_SPIRIT_SCAN_COOLDOWN = 30f;
        private const float DEFAULT_LANDMARK_LOCK_RADIUS = 15f;

        #endregion

        #region Singleton

        /// <summary>Singleton instance for global access.</summary>
        public static DiscoverySystem Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Scan Settings")]
        [SerializeField] private float _scanInterval = DEFAULT_SCAN_INTERVAL;

        [Header("Layer Radii (overrides discovery component defaults)")]
        [SerializeField] private float _landmarkRadius = DEFAULT_LANDMARK_RADIUS;
        [SerializeField] private float _poiRadius = DEFAULT_POI_RADIUS;
        [SerializeField] private float _hiddenRadius = DEFAULT_HIDDEN_RADIUS;

        [Header("Spirit Scan (神识探查)")]
        [SerializeField] private float _spiritScanRadius = DEFAULT_SPIRIT_SCAN_RADIUS;
        [SerializeField] private float _spiritScanCooldown = DEFAULT_SPIRIT_SCAN_COOLDOWN;

        [Header("Time-of-Day Modifiers")]
        [SerializeField, Range(0f, 2f)] private float _dayDetectionModifier = 1.0f;
        [SerializeField, Range(0f, 2f)] private float _nightDetectionModifier = 0.7f;

        [Header("Weather Modifiers")]
        [SerializeField, Range(0f, 2f)] private float _clearWeatherModifier = 1.0f;
        [SerializeField, Range(0f, 2f)] private float _rainWeatherModifier = 0.8f;
        [SerializeField, Range(0f, 2f)] private float _fogWeatherModifier = 0.5f;
        [SerializeField, Range(0f, 2f)] private float _mistWeatherModifier = 1.2f;

        [Header("Landmark Lock-On")]
        [SerializeField] private float _landmarkLockRadius = DEFAULT_LANDMARK_LOCK_RADIUS;

        [Header("Gizmo Debug")]
        [SerializeField] private bool _showGizmos = true;

        #endregion

        #region Private State

        // All discoverable points in the scene.
        private readonly List<HiddenDiscovery> _allDiscoveries = new();
        private readonly Dictionary<string, HiddenDiscovery> _discoveryMap = new();

        // Map marker tracking: markerId -> isPermanent.
        private readonly Dictionary<string, bool> _mapMarkers = new();

        // Scan cooldown.
        private float _scanCooldown;

        // Spirit scan (神识探查).
        private bool _spiritScanReady = true;
        private float _spiritScanRemainingCooldown;

        // Active spirit scan detection results (persist across frames for UI).
        private readonly List<SpiritScanResultEvent> _activeScanResults = new();
        private float _scanResultDisplayDuration = 3f;
        private float _scanResultTimer;

        // Time/weather state.
        private bool _isNight;
        private string _currentWeather = "clear";

        // Player reference (cached).
        private Transform _playerTransform;
        private Vector3 _lastScanPlayerPos;

        // Total discovery count for first-discovery tracking.
        private int _totalDiscoveryCount;

        #endregion

        #region Public Properties

        /// <summary>Whether it is currently night time (18:00-06:00).</summary>
        public bool IsNight => _isNight;

        /// <summary>Current weather string identifier.</summary>
        public string CurrentWeather => _currentWeather;

        /// <summary>Number of discovery points registered in the scene.</summary>
        public int TotalDiscoveryPoints => _allDiscoveries.Count;

        /// <summary>Number of discovered points.</summary>
        public int DiscoveredCount { get; private set; }

        /// <summary>Whether the spirit scan (神识探查) is ready to use.</summary>
        public bool IsSpiritScanReady => _spiritScanReady;

        /// <summary>Spirit scan cooldown remaining in seconds.</summary>
        public float SpiritScanCooldownRemaining => _spiritScanRemainingCooldown;

        /// <summary>Spirit scan total cooldown time.</summary>
        public float SpiritScanCooldownTotal => _spiritScanCooldown;

        /// <summary>Active spirit scan results for UI consumption.</summary>
        public IReadOnlyList<SpiritScanResultEvent> ActiveScanResults => _activeScanResults;

        /// <summary>Read-only view of map markers.</summary>
        public IReadOnlyDictionary<string, bool> MapMarkers => _mapMarkers;

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

        private void OnEnable()
        {
            // Subscribe to time-of-day changes.
            EventBus.Subscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TimeOfDayChangedEvent>(OnTimeOfDayChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            RegisterAllDiscoveriesInScene();
        }

        private void Update()
        {
            // Cache player transform.
            CachePlayerTransform();

            if (_playerTransform == null)
                return;

            // Decay scan cooldown.
            _scanCooldown -= Time.deltaTime;

            // Decay spirit scan cooldown.
            if (!_spiritScanReady)
            {
                _spiritScanRemainingCooldown -= Time.deltaTime;
                if (_spiritScanRemainingCooldown <= 0f)
                {
                    _spiritScanRemainingCooldown = 0f;
                    _spiritScanReady = true;

                    EventBus.Publish(new SpiritScanStateEvent
                    {
                        IsActive = "false",
                        CooldownRemaining = "0f",
                        TotalCooldown = _spiritScanCooldown
                    });
                }
            }

            // Decay scan result display timer.
            if (_activeScanResults.Count > 0)
            {
                _scanResultTimer -= Time.deltaTime;
                if (_scanResultTimer <= 0f)
                {
                    _activeScanResults.Clear();
                }
            }

            // Periodic scan.
            if (_scanCooldown <= 0f)
            {
                PerformScan(_playerTransform.position);
                _scanCooldown = _scanInterval;
            }
        }

        #endregion

        #region Player Tracking

        /// <summary>Cache the player transform reference.</summary>
        private void CachePlayerTransform()
        {
            if (_playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                }
            }
        }

        #endregion

        #region Discovery Registration

        /// <summary>
        /// Scan the scene for all HiddenDiscovery components and register them.
        /// Called once on Start.
        /// </summary>
        private void RegisterAllDiscoveriesInScene()
        {
            _allDiscoveries.Clear();
            _discoveryMap.Clear();

            // Find all HiddenDiscovery instances in the active scene.
            HiddenDiscovery[] discoveries = FindObjectsByType<HiddenDiscovery>(
                FindObjectsSortMode.None
            );

            foreach (var discovery in discoveries)
            {
                RegisterDiscovery(discovery);
            }

            Debug.Log($"[DiscoverySystem] Registered {_allDiscoveries.Count} discovery points in scene.");
        }

        /// <summary>Register a single discovery point.</summary>
        public bool RegisterDiscovery(HiddenDiscovery discovery)
        {
            if (discovery == null)
                return false;

            string id = discovery.DiscoveryId;

            // Auto-generate ID if empty.
            if (string.IsNullOrEmpty(id))
            {
                id = $"{discovery.DiscoveryType}_{discovery.name}_{_allDiscoveries.Count}";
                discovery.DiscoveryId = id;
            }

            // Prevent duplicate registration.
            if (_discoveryMap.ContainsKey(id))
            {
                Debug.LogWarning($"[DiscoverySystem] Duplicate discovery ID: '{id}'. Skipping.");
                return false;
            }

            _allDiscoveries.Add(discovery);
            _discoveryMap[id] = discovery;

            return true;
        }

        /// <summary>Unregister a previously registered discovery point.</summary>
        public bool UnregisterDiscovery(HiddenDiscovery discovery)
        {
            if (discovery == null || string.IsNullOrEmpty(discovery.DiscoveryId))
                return false;

            if (_discoveryMap.Remove(discovery.DiscoveryId))
            {
                _allDiscoveries.Remove(discovery);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get a registered discovery by its ID.
        /// Returns null if not found.
        /// </summary>
        public HiddenDiscovery GetDiscovery(string discoveryId)
        {
            _discoveryMap.TryGetValue(discoveryId, out var discovery);
            return discovery;
        }

        /// <summary>Get all currently registered discoveries (read-only).</summary>
        public IReadOnlyList<HiddenDiscovery> GetAllDiscoveries() => _allDiscoveries;

        /// <summary>Get all discoveries of a specific type.</summary>
        public List<HiddenDiscovery> GetDiscoveriesByType(DiscoveryType type)
        {
            var results = new List<HiddenDiscovery>();
            foreach (var d in _allDiscoveries)
            {
                if (d.DiscoveryType == type)
                    results.Add(d);
            }
            return results;
        }

        #endregion

        #region Main Scan

        /// <summary>
        /// Perform a scan around the player position, checking all three discovery layers.
        ///
        /// Landmark: 15m auto-discover + permanent map marker (DSC-01, DSC-02)
        /// POI:      10m + fog cleared + question-mark marker (DSC-03, DSC-04)
        /// Hidden:   6m + condition check + rewards, no auto-marker (DSC-05, DSC-06, DSC-07, DSC-08)
        /// </summary>
        private void PerformScan(Vector3 playerPos)
        {
            float weatherModifier = GetWeatherModifier();
            float timeModifier = _isNight ? _nightDetectionModifier : _dayDetectionModifier;

            foreach (var discovery in _allDiscoveries)
            {
                if (discovery == null || discovery.Discovered)
                    continue;

                float distance = Vector3.Distance(playerPos, discovery.transform.position);
                float typeRadius = GetTypeRadius(discovery.DiscoveryType);

                // Skip if beyond type-specific radius.
                if (distance > typeRadius)
                    continue;

                // Type-specific logic.
                switch (discovery.DiscoveryType)
                {
                    case DiscoveryType.Landmark:
                        TryTriggerLandmark(discovery, distance, weatherModifier, timeModifier);
                        break;

                    case DiscoveryType.POI:
                        TryTriggerPOI(discovery, distance, weatherModifier, timeModifier);
                        break;

                    case DiscoveryType.Hidden:
                        TryTriggerHidden(discovery, distance, weatherModifier, timeModifier);
                        break;
                }
            }
        }

        /// <summary>Get the effective detection radius for a given discovery type.</summary>
        private float GetTypeRadius(DiscoveryType type)
        {
            return type switch
            {
                DiscoveryType.Landmark => _landmarkRadius,
                DiscoveryType.POI => _poiRadius,
                DiscoveryType.Hidden => _hiddenRadius,
                _ => _landmarkRadius
            };
        }

        #endregion

        #region Layer-Specific Triggers

        /// <summary>
        /// Landmark discovery logic (DSC-01, DSC-02):
        /// - 15m auto-trigger (deterministic, no probability roll)
        /// - Permanent marker on world map
        /// - Shows name + description on discovery
        /// </summary>
        private void TryTriggerLandmark(HiddenDiscovery discovery, float distance,
            float weatherModifier, float timeModifier)
        {
            // Landmarks are deterministic within radius — no roll needed.
            // But we still respect global modifiers (excluded if modifiers push below threshold).
            float effectiveModifier = weatherModifier * timeModifier;
            if (effectiveModifier <= 0f)
                return;

            // Always trigger at this distance for Landmarks.
            TriggerDiscovery(discovery);
        }

        /// <summary>
        /// POI discovery logic (DSC-03, DSC-04):
        /// - 10m range + fog must be cleared (at least LightlyExplored)
        /// - Shows question-mark marker on world map
        /// - Uses detection probability formula
        /// </summary>
        private void TryTriggerPOI(HiddenDiscovery discovery, float distance,
            float weatherModifier, float timeModifier)
        {
            // Fog check (DSC-03): POI requires surrounding fog to be dissipated.
            if (!IsFogClearedAtPosition(discovery.transform.position))
                return;

            // Calculate detection probability.
            float baseChance = discovery.GetDetectionChance(distance);
            float effectiveChance = baseChance * weatherModifier * timeModifier;

            // Roll for detection.
            if (UnityEngine.Random.value < effectiveChance)
            {
                TriggerDiscovery(discovery);
            }
        }

        /// <summary>
        /// Hidden discovery logic (DSC-05, DSC-06, DSC-07, DSC-08):
        /// - 6m range + condition check (realm, weather, time, items, quests)
        /// - No automatic map marker
        /// - Rewards include items / cultivation XP / reputation
        /// - Detection probability with weather/time modifiers
        /// </summary>
        private void TryTriggerHidden(HiddenDiscovery discovery, float distance,
            float weatherModifier, float timeModifier)
        {
            // Condition check (DSC-05): hidden discoveries require specific conditions.
            if (!discovery.CanDiscover(_currentWeather, _isNight))
                return;

            // Calculate detection probability with all modifiers.
            float baseChance = discovery.GetDetectionChance(distance);
            float effectiveChance = baseChance * weatherModifier * timeModifier;

            // Clamp to reasonable range.
            effectiveChance = Mathf.Clamp01(effectiveChance);

            // Roll for detection.
            if (UnityEngine.Random.value < effectiveChance)
            {
                TriggerDiscovery(discovery);
            }
        }

        #endregion

        #region Trigger & Publish

        /// <summary>Core trigger method: marks as discovered and publishes events.</summary>
        private void TriggerDiscovery(HiddenDiscovery discovery)
        {
            if (discovery == null || discovery.Discovered)
                return;

            discovery.OnDiscoveryTriggered();
            DiscoveredCount++;
            bool isFirstDiscovery = _totalDiscoveryCount == 0;
            _totalDiscoveryCount++;

            // Publish discovery event for UI.
            EventBus.Publish(new DiscoveryTriggeredEvent
            {
                DiscoveryId = discovery.DiscoveryId,
                DisplayName = discovery.DisplayName,
                DiscoveryType = discovery.DiscoveryType,
                Description = discovery.Description,
                WorldPosition = discovery.transform.position,
                IsFirstDiscovery = isFirstDiscovery,
                IsFromSave = false
            });

            // Handle map markers.
            if (discovery.AutoMarkOnMap)
            {
                bool isPermanent = discovery.DiscoveryType == DiscoveryType.Landmark;
                bool showQuestionMark = discovery.DiscoveryType == DiscoveryType.POI;

                AddMapMarker(discovery.DiscoveryId, discovery.DisplayName,
                    discovery.DiscoveryType, discovery.transform.position,
                    isPermanent, showQuestionMark);
            }

            Debug.Log($"[DiscoverySystem] Triggered: {discovery.DisplayName} ({discovery.DiscoveryType})");
        }

        /// <summary>
        /// Force-trigger a discovery by ID (used by save-load restoration or debug).
        /// </summary>
        public bool ForceTriggerDiscovery(string discoveryId)
        {
            if (_discoveryMap.TryGetValue(discoveryId, out var discovery))
            {
                if (discovery.Discovered)
                    return false;

                TriggerDiscovery(discovery);
                return true;
            }

            return false;
        }

        #endregion

        #region Map Markers

        /// <summary>Add a map marker for a discovered point.</summary>
        private void AddMapMarker(string discoveryId, string displayName,
            DiscoveryType type, Vector3 worldPosition,
            bool isPermanent, bool showQuestionMark)
        {
            if (_mapMarkers.ContainsKey(discoveryId))
                return;

            _mapMarkers[discoveryId] = isPermanent;

            EventBus.Publish(new DiscoveryMapMarkerEvent
            {
                DiscoveryId = discoveryId,
                DisplayName = displayName,
                DiscoveryType = type,
                WorldPosition = worldPosition,
                IsPermanent = isPermanent,
                ShowQuestionMark = showQuestionMark,
                AddMarker = true
            });
        }

        /// <summary>Remove a map marker (used when a POI/Hidden should be cleared).</summary>
        public void RemoveMapMarker(string discoveryId)
        {
            if (_mapMarkers.Remove(discoveryId))
            {
                EventBus.Publish(new DiscoveryMapMarkerEvent
                {
                    DiscoveryId = discoveryId,
                    AddMarker = false
                });
            }
        }

        #endregion

        #region Spirit Scan (神识探查) — DSC-09

        /// <summary>
        /// Activate the "神识探查" (Spirit Gaze) skill.
        /// Scans within spiritScanRadius for hidden discoveries that meet conditions
        /// and reports their positions without triggering them.
        ///
        /// DSC-09: The skill can detect nearby hidden discoveries.
        /// </summary>
        public bool ActivateSpiritScan()
        {
            if (!_spiritScanReady)
            {
                Debug.Log($"[DiscoverySystem] Spirit scan on cooldown: {_spiritScanRemainingCooldown:F1}s remaining.");
                return false;
            }

            if (_playerTransform == null)
            {
                CachePlayerTransform();
                if (_playerTransform == null)
                    return false;
            }

            Vector3 playerPos = _playerTransform.position;
            _activeScanResults.Clear();

            // Scan for hidden discoveries within spirit scan radius.
            foreach (var discovery in _allDiscoveries)
            {
                if (discovery == null || discovery.Discovered)
                    continue;

                // Spirit scan only detects Hidden type.
                if (discovery.DiscoveryType != DiscoveryType.Hidden)
                    continue;

                float distance = Vector3.Distance(playerPos, discovery.transform.position);

                // Skip if beyond spirit scan range.
                if (distance > _spiritScanRadius)
                    continue;

                // Check if conditions are met for detection.
                // Spirit scan reveals the existence even if the player wouldn't normally
                // meet all conditions — it gives a hint that "something is here."
                if (!discovery.CanDiscover(_currentWeather, _isNight))
                    continue;

                float baseChance = discovery.GetDetectionChance(distance);
                float weatherMod = GetWeatherModifier();
                float timeMod = _isNight ? _nightDetectionModifier : _dayDetectionModifier;
                float effectiveChance = Mathf.Clamp01(baseChance * weatherMod * timeMod);

                // Add to scan results regardless of roll — spirit scan reveals presence
                // but the probability indicates how clearly it's detected.
                Vector3 dirToDiscovery = (discovery.transform.position - playerPos).normalized;

                var result = new SpiritScanResultEvent
                {
                    Position = discovery.transform.position,
                    DetectionChance = effectiveChance,
                    Distance = distance,
                    DisplayName = effectiveChance > 0.3f ? discovery.DisplayName : "???",
                    Direction = dirToDiscovery
                };

                _activeScanResults.Add(result);
            }

            // Apply cooldown.
            _spiritScanReady = false;
            _spiritScanRemainingCooldown = _spiritScanCooldown;
            _scanResultTimer = _scanResultDisplayDuration;

            _lastScanPlayerPos = playerPos;

            // Publish state event.
            EventBus.Publish(new SpiritScanStateEvent
            {
                IsActive = "true",
                CooldownRemaining = _spiritScanRemainingCooldown,
                TotalCooldown = _spiritScanCooldown
            });

            // Publish individual scan results.
            foreach (var result in _activeScanResults)
            {
                EventBus.Publish(result);
            }

            Debug.Log($"[DiscoverySystem] Spirit scan complete: {_activeScanResults.Count} hidden discoveries detected.");

            return true;
        }

        #endregion

        #region Fog Integration

        /// <summary>
        /// Check if the fog at a given world position is sufficiently cleared
        /// for a POI discovery to trigger.
        ///
        /// Requires at least FogLayer.LightlyExplored (Layer 1).
        /// </summary>
        /// <param name="worldPosition">World position to check.</param>
        /// <returns>True if fog is at least LightlyExplored.</returns>
        private bool IsFogClearedAtPosition(Vector3 worldPosition)
        {
            if (FogOfWar.Instance == null)
            {
                // If no fog system, assume cleared.
                return true;
            }

            FogLayer layer = FogOfWar.Instance.GetFogLayer(worldPosition);
            return layer >= FogLayer.LightlyExplored;
        }

        #endregion

        #region Weather / Time Modifiers — DSC-10

        /// <summary>Handle time-of-day change events from the game time system.</summary>
        private void OnTimeOfDayChanged(TimeOfDayChangedEvent evt)
        {
            _isNight = evt.IsNight;
        }

        /// <summary>
        /// Set the current weather state externally (called by a weather system).
        /// Valid values: "clear", "rain", "fog", "mist"
        /// </summary>
        public void SetWeather(string weather)
        {
            _currentWeather = weather.ToLowerInvariant();
        }

        /// <summary>
        /// Get the current weather detection modifier.
        /// DSC-10: Weather conditions affect hidden discovery detection.
        /// </summary>
        private float GetWeatherModifier()
        {
            return _currentWeather switch
            {
                "clear" => _clearWeatherModifier,
                "rain" => _rainWeatherModifier,
                "fog" => _fogWeatherModifier,
                "mist" => _mistWeatherModifier,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the effective detection probability for a discovery at a given distance,
        /// factoring in all active modifiers (weather, time, etc.).
        /// Useful for UI hints and skill feedback.
        /// </summary>
        public float GetEffectiveDetectionChance(HiddenDiscovery discovery, float distance)
        {
            float baseChance = discovery.GetDetectionChance(distance);
            float weatherMod = GetWeatherModifier();
            float timeMod = _isNight ? _nightDetectionModifier : _dayDetectionModifier;
            return Mathf.Clamp01(baseChance * weatherMod * timeMod);
        }

        #endregion

        #region Save / Load

        /// <summary>Capture the full state of all discoveries for serialization.</summary>
        public DiscoverySystemSaveData GetSaveData()
        {
            var records = new DiscoveryRecord[_allDiscoveries.Count];
            for (int i = 0; i < _allDiscoveries.Count; i++)
            {
                records[i] = _allDiscoveries[i].GetRecord();
            }

            return new DiscoverySystemSaveData
            {
                Records = records
            };
        }

        /// <summary>Restore discovery states from saved data.</summary>
        public void LoadSaveData(DiscoverySystemSaveData data)
        {
            if (data?.Records == null)
            {
                Debug.LogWarning("[DiscoverySystem] LoadSaveData: null or empty data.");
                return;
            }

            int restoredCount = 0;

            foreach (var record in data.Records)
            {
                if (_discoveryMap.TryGetValue(record.DiscoveryId, out var discovery))
                {
                    discovery.LoadRecord(record);
                    if (record.Discovered)
                    {
                        DiscoveredCount++;

                        // Re-publish the map marker if applicable.
                        if (discovery.AutoMarkOnMap)
                        {
                            bool isPermanent = discovery.DiscoveryType == DiscoveryType.Landmark;
                            bool showQuestionMark = discovery.DiscoveryType == DiscoveryType.POI;

                            AddMapMarker(discovery.DiscoveryId, discovery.DisplayName,
                                discovery.DiscoveryType, discovery.transform.position,
                                isPermanent, showQuestionMark);

                            // Re-publish discovery event for UI re-sync.
                            EventBus.Publish(new DiscoveryTriggeredEvent
                            {
                                DiscoveryId = discovery.DiscoveryId,
                                DisplayName = discovery.DisplayName,
                                DiscoveryType = discovery.DiscoveryType,
                                Description = discovery.Description,
                                WorldPosition = discovery.transform.position,
                                IsFirstDiscovery = "false",
                                IsFromSave = true
                            });
                        }
                    }

                    restoredCount++;
                }
                else
                {
                    Debug.LogWarning($"[DiscoverySystem] Cannot restore discovery '{record.DiscoveryId}': not found in scene.");
                }
            }

            Debug.Log($"[DiscoverySystem] Restored {restoredCount}/{data.Records.Length} discoveries.");
        }

        /// <summary>Reset all discovery states (for new game).</summary>
        public void ClearAll()
        {
            foreach (var discovery in _allDiscoveries)
            {
                if (discovery != null)
                {
                    discovery.LoadRecord(new DiscoveryRecord
                    {
                        DiscoveryId = discovery.DiscoveryId,
                        Type = discovery.DiscoveryType,
                        Discovered = "false",
                        DiscoverTime = 0f
                    });
                }
            }

            _mapMarkers.Clear();
            DiscoveredCount = 0;
            _totalDiscoveryCount = 0;
            _activeScanResults.Clear();

            Debug.Log("[DiscoverySystem] All discovery states cleared.");
        }

        #endregion

        #region Editor / Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            int landmarks = "0", pois = "0", hiddens = 0;
            int landmarksD = "0", poisD = "0", hiddensD = 0;

            foreach (var d in _allDiscoveries)
            {
                switch (d.DiscoveryType)
                {
                    case DiscoveryType.Landmark: landmarks++; if (d.Discovered) landmarksD++; break;
                    case DiscoveryType.POI: pois++; if (d.Discovered) poisD++; break;
                    case DiscoveryType.Hidden: hiddens++; if (d.Discovered) hiddensD++; break;
                }
            }

            return $"=== Discovery System ===\n" +
                   $"Landmarks: {landmarksD}/{landmarks} | POIs: {poisD}/{pois} | Hidden: {hiddensD}/{hiddens}\n" +
                   $"Weather: {_currentWeather} | Night: {_isNight}\n" +
                   $"Spirit Scan: {(_spiritScanReady ? "Ready" : $"Cooldown {_spiritScanRemainingCooldown:F1}s")}\n" +
                   $"Map Markers: {_mapMarkers.Count}";
        }

        /// <summary>Draw discovery system gizmos in the Scene view.</summary>
        private void OnDrawGizmos()
        {
            if (!_showGizmos || _playerTransform == null)
                return;

            // Draw spirit scan radius when active.
            if (!_spiritScanReady)
            {
                Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.15f);
                Gizmos.DrawWireSphere(_playerTransform.position, _spiritScanRadius);
            }
        }

        #endregion
    }
}
