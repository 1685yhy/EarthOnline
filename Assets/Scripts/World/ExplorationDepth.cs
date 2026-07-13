using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    #region Enums & Data Structures

    /// <summary>
    /// Five stages of exploration depth for a region.
    /// Maps to 0-100% progress: each stage unlocks new map features.
    /// </summary>
    public enum ExplorationStage
    {
        /// <summary>0-19% — Just arrived, mostly blank map.</summary>
        Novice = 0,         // 初来乍到

        /// <summary>20-39% — Getting familiar, major landmarks visible.</summary>
        Familiar = 1,       // 渐渐熟悉

        /// <summary>40-59% — Medium/small POIs begin to show.</summary>
        Deep = 2,           // 深入探索

        /// <summary>60-79% — Most content visible.</summary>
        Master = 3,         // 了如指掌

        /// <summary>80-100% — Hidden entrances hinted, full mastery.</summary>
        Complete = 4        // 完全掌控
    }

    /// <summary>Serializable exploration data for a single region.</summary>
    [Serializable]
    public struct RegionExplorationRecord
    {
        public string RegionId;
        public float Progress;           // 0-100
        public int StageIndex;
        public bool TitleGranted;
    }

    /// <summary>Serializable snapshot for persistence.</summary>
    [Serializable]
    public class ExplorationSaveData
    {
        public RegionExplorationRecord[] Regions;
    }

    /// <summary>
    /// Tracks what was added by a single discovery event so we can report it.
    /// </summary>
    [Serializable]
    public struct DiscoveryContribution
    {
        /// <summary>How much exploration % was added.</summary>
        public float Amount;

        /// <summary>Display name of the thing discovered.</summary>
        public string SourceName;

        /// <summary>Type of the discovery (Entry/Landmark/POI/Hidden).</summary>
        public string SourceType;
    }

    #endregion

    #region Events

    /// <summary>Published when a region's exploration progress changes.</summary>
    public struct ExplorationProgressChangedEvent
    {
        public string RegionId;
        public float PreviousProgress;
        public float CurrentProgress;
        public ExplorationStage Stage;
        public string StageName;
    }

    /// <summary>Published when a region's exploration stage advances.</summary>
    public struct ExplorationStageUpEvent
    {
        public string RegionId;
        public ExplorationStage NewStage;
        public string StageName;
        public float Progress;
    }

    /// <summary>Published when a single contribution is added (for UI feed).</summary>
    public struct ExplorationContributionEvent
    {
        public string RegionId;
        public DiscoveryContribution Contribution;
        public float TotalProgress;
    }

    /// <summary>Published when exploration reaches 100% and title is granted.</summary>
    public struct ExplorationCompleteEvent
    {
        public string RegionId;
        public string TitleId;
        public string TitleName;
    }

    #endregion

    /// <summary>
    /// Exploration Depth System for EarthOnline (EXP-01 ~ EXP-05).
    ///
    /// Tracks per-region exploration progress (0-100%) across five stages.
    /// Integrates with FogOfWar (coverage area), DiscoverySystem (landmarks/POIs/hidden),
    /// and EventBus for UI communication.
    ///
    /// Stage thresholds:
    ///   0-19%  Novice    (初来乍到)  — mostly blank map
    ///  20-39%  Familiar  (渐渐熟悉)  — major landmarks visible
    ///  40-59%  Deep      (深入探索)  — plus medium/small POIs (EXP-03)
    ///  60-79%  Master    (了如指掌)  — most content visible
    ///  80-100% Complete  (完全掌控)  — plus hidden entrance hints (EXP-04), title at 100% (EXP-05)
    /// </summary>
    public class ExplorationDepth : MonoBehaviour
    {
        #region Constants

        // ── Per-event contributions ───────────────────────────────────
        private const float FIRST_ENTRY_BONUS = 5f;          // EXP-01: first enter +5%
        private const float LANDMARK_BONUS_MIN = 3f;         // EXP-02: landmark +3~5%
        private const float LANDMARK_BONUS_MAX = 5f;
        private const float POI_BONUS_MIN = 1f;              // EXP-02: POI +1~2%
        private const float POI_BONUS_MAX = 2f;
        private const float HIDDEN_BONUS_MIN = 2f;           // EXP-02: hidden +2~3%
        private const float HIDDEN_BONUS_MAX = 3f;

        // ── Coverage contribution ───────────────────────────────────
        // Every 1% of map area explored = +0.1% exploration progress
        private const float COVERAGE_PER_PERCENT = 0.1f;     // per 1% area explored
        private const float COVERAGE_CHECK_INTERVAL = 5f;    // seconds between area-recalc

        // ── Stage thresholds (inclusive lower bound) ────────────────
        private const float STAGE_2_THRESHOLD = 20f;         // Familiar
        private const float STAGE_3_THRESHOLD = 40f;         // Deep    (EXP-03: medium/small POI show)
        private const float STAGE_4_THRESHOLD = 60f;         // Master
        private const float STAGE_5_THRESHOLD = 80f;         // Complete (EXP-04: hidden hints)

        // ── Title ───────────────────────────────────────────────────
        private const string MASTER_TITLE_ID = "exploration_master";
        private const string MASTER_TITLE_NAME = "探索大师";

        #endregion

        #region Singleton

        /// <summary>Singleton instance for global access.</summary>
        public static ExplorationDepth Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Discovery Contributions")]
        [SerializeField, Range(0f, 20f)] private float _firstEntryBonus = FIRST_ENTRY_BONUS;
        [SerializeField, Range(0f, 10f)] private float _landmarkBonusMin = LANDMARK_BONUS_MIN;
        [SerializeField, Range(0f, 10f)] private float _landmarkBonusMax = LANDMARK_BONUS_MAX;
        [SerializeField, Range(0f, 10f)] private float _poiBonusMin = POI_BONUS_MIN;
        [SerializeField, Range(0f, 10f)] private float _poiBonusMax = POI_BONUS_MAX;
        [SerializeField, Range(0f, 10f)] private float _hiddenBonusMin = HIDDEN_BONUS_MIN;
        [SerializeField, Range(0f, 10f)] private float _hiddenBonusMax = HIDDEN_BONUS_MAX;

        [Header("Coverage")]
        [SerializeField, Range(0f, 1f)] private float _coveragePerPercent = COVERAGE_PER_PERCENT;
        [SerializeField] private float _coverageCheckInterval = COVERAGE_CHECK_INTERVAL;

        [Header("Stage Thresholds")]
        [SerializeField, Range(0f, 100f)] private float _stage2Threshold = STAGE_2_THRESHOLD;
        [SerializeField, Range(0f, 100f)] private float _stage3Threshold = STAGE_3_THRESHOLD;
        [SerializeField, Range(0f, 100f)] private float _stage4Threshold = STAGE_4_THRESHOLD;
        [SerializeField, Range(0f, 100f)] private float _stage5Threshold = STAGE_5_THRESHOLD;

        [Header("Debug")]
        [SerializeField] private bool _debugLogging;

        #endregion

        #region Private State

        // Per-region exploration data.
        // Key = RegionId (string), Value = exploration state.
        private readonly Dictionary<string, RegionExplorationState> _regions = new();

        // Total map cell count per region for coverage calculations.
        // Set externally when regions are configured.
        private readonly Dictionary<string, int> _regionTotalCells = new();

        // Tracking which discoveries have already contributed per region.
        private readonly Dictionary<string, HashSet<string>> _discoveryContributions = new();

        // Coverage timer.
        private float _coverageTimer;

        // Player tracking for region entry.
        private Transform _playerTransform;
        private string _currentRegionId;

        #endregion

        #region Public Properties

        /// <summary>All region IDs currently tracked.</summary>
        public IReadOnlyCollection<string> TrackedRegions => _regions.Keys;

        /// <summary>
        /// Total region cell counts for external configuration.
        /// Register expected cells via RegisterRegionCells() before runtime.
        /// </summary>
        public IReadOnlyDictionary<string, int> RegionTotalCells => _regionTotalCells;

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
            EventBus.Subscribe<DiscoveryTriggeredEvent>(OnDiscoveryTriggered);
            EventBus.Subscribe<FogBatchRevealedEvent>(OnFogBatchRevealed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DiscoveryTriggeredEvent>(OnDiscoveryTriggered);
            EventBus.Unsubscribe<FogBatchRevealedEvent>(OnFogBatchRevealed);
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
            CachePlayerTransform();
        }

        private void Update()
        {
            CachePlayerTransform();

            // Update coverage area every N seconds (cheap).
            _coverageTimer -= Time.deltaTime;
            if (_coverageTimer <= 0f)
            {
                _coverageTimer = _coverageCheckInterval;

                if (!string.IsNullOrEmpty(_currentRegionId))
                {
                    UpdateCoverageContribution(_currentRegionId);
                }
            }
        }

        #endregion

        #region Player & Region Tracking

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

        /// <summary>
        /// Set the player's current region. Called externally (e.g., by zone trigger).
        /// On first entry to a region, grants the first-entry bonus (EXP-01).
        /// </summary>
        public void SetPlayerRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
                return;

            // Track whether this is a region change.
            bool isNewRegion = _currentRegionId != regionId;
            _currentRegionId = regionId;

            // Ensure region is tracked.
            EnsureRegionTracked(regionId);

            // EXP-01: Grant first-entry bonus on first ever entry.
            if (isNewRegion && !_regions[regionId].HasEntered)
            {
                var state = _regions[regionId];
                state.HasEntered = true;
                _regions[regionId] = state;
                AddProgress(regionId, _firstEntryBonus,
                    new DiscoveryContribution
                    {
                        Amount = _firstEntryBonus,
                        SourceName = "首次踏入",
                        SourceType = "Entry"
                    });
            }
        }

        /// <summary>
        /// Get the player's current region ID. Returns null if not in any region.
        /// </summary>
        public string CurrentRegionId => _currentRegionId;

        #endregion

        #region Region Registration

        /// <summary>
        /// Register a region's total map cell count for coverage calculations.
        /// Should be called during scene setup (e.g., by a RegionConfig component).
        /// </summary>
        public void RegisterRegionCells(string regionId, int totalCells)
        {
            if (string.IsNullOrEmpty(regionId) || totalCells <= 0)
                return;

            _regionTotalCells[regionId] = totalCells;
            EnsureRegionTracked(regionId);

            if (_debugLogging)
                Debug.Log($"[ExplorationDepth] Registered region '{regionId}' with {totalCells} cells.");
        }

        /// <summary>Ensure a region has an entry in the tracking dictionary.</summary>
        private void EnsureRegionTracked(string regionId)
        {
            if (!_regions.ContainsKey(regionId))
            {
                _regions[regionId] = new RegionExplorationState
                {
                    Progress = 0f,
                    Stage = ExplorationStage.Novice,
                    StageName = GetStageLocalizedName(ExplorationStage.Novice),
                    HasEntered = false,
                    TitleGranted = false
                };
            }

            if (!_discoveryContributions.ContainsKey(regionId))
            {
                _discoveryContributions[regionId] = new HashSet<string>();
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle discovery events from DiscoverySystem (EXP-02).
        /// </summary>
        private void OnDiscoveryTriggered(DiscoveryTriggeredEvent evt)
        {
            if (string.IsNullOrEmpty(_currentRegionId))
                return;

            string regionId = _currentRegionId;

            // Prevent double-counting the same discovery.
            if (!_discoveryContributions.TryGetValue(regionId, out var contributed))
            {
                contributed = new HashSet<string>();
                _discoveryContributions[regionId] = contributed;
            }

            var discId = evt.DiscoveryId?.ToString() ?? "";
            if (contributed.Contains(discId))
                return;

            contributed.Add(discId);

            // Calculate contribution amount based on type.
            float amount = 0f;
            string sourceType = "";

            switch (evt.DiscoveryType)
            {
                case DiscoveryType.Landmark:
                    amount = UnityEngine.Random.Range(_landmarkBonusMin, _landmarkBonusMax);
                    sourceType = "Landmark";
                    break;

                case DiscoveryType.POI:
                    amount = UnityEngine.Random.Range(_poiBonusMin, _poiBonusMax);
                    sourceType = "POI";
                    break;

                case DiscoveryType.Hidden:
                    amount = UnityEngine.Random.Range(_hiddenBonusMin, _hiddenBonusMax);
                    sourceType = "Hidden";
                    break;

                default:
                    return;
            }

            var contribution = new DiscoveryContribution
            {
                Amount = amount,
                SourceName = (evt.DisplayName as string) ?? "",
                SourceType = sourceType
            };

            AddProgress(regionId, amount, contribution);
        }

        /// <summary>
        /// Handle fog reveal events — triggers coverage recalculation.
        /// </summary>
        private void OnFogBatchRevealed(FogBatchRevealedEvent evt)
        {
            // Coverage recalculation runs on a timer in Update(),
            // but we can trigger an immediate check to be responsive.
            if (!string.IsNullOrEmpty(_currentRegionId))
            {
                UpdateCoverageContribution(_currentRegionId);
            }
        }

        #endregion

        #region Progress & Stages

        /// <summary>
        /// Core method to add exploration progress and handle stage transitions.
        /// </summary>
        private void AddProgress(string regionId, float amount, DiscoveryContribution contribution)
        {
            if (!_regions.TryGetValue(regionId, out var state))
                return;

            if (state.Progress >= 100f)
                return; // Already complete.

            float previousProgress = state.Progress;
            float newProgress = Mathf.Min(100f, state.Progress + amount);

            state.Progress = newProgress;
            _regions[regionId] = state;

            // Publish contribution event (for UI feed).
            EventBus.Publish(new ExplorationContributionEvent
            {
                RegionId = regionId,
                Contribution = contribution,
                TotalProgress = newProgress
            });

            // Check for stage change.
            ExplorationStage newStage = CalculateStage(newProgress);
            if (newStage != state.Stage)
            {
                ExplorationStage oldStage = state.Stage;
                state.Stage = newStage;
                state.StageName = GetStageLocalizedName(newStage);
                _regions[regionId] = state;

                // Apply stage-gated unlocks.
                OnStageChanged(regionId, oldStage, newStage, newProgress);

                // Publish stage-up event.
                EventBus.Publish(new ExplorationStageUpEvent
                {
                    RegionId = regionId,
                    NewStage = newStage,
                    StageName = state.StageName,
                    Progress = newProgress
                });

                if (_debugLogging)
                    Debug.Log($"[ExplorationDepth] Region '{regionId}' reached stage {newStage} ({state.StageName}) at {newProgress:F1}%");
            }

            // Publish progress-changed event.
            EventBus.Publish(new ExplorationProgressChangedEvent
            {
                RegionId = regionId,
                PreviousProgress = previousProgress,
                CurrentProgress = newProgress,
                Stage = state.Stage,
                StageName = state.StageName
            });

            // EXP-05: Check for 100% completion — grant title.
            if (newProgress >= 100f && !state.TitleGranted)
            {
                GrantMasterTitle(regionId);
            }

            if (_debugLogging)
                Debug.Log($"[ExplorationDepth] Region '{regionId}': {previousProgress:F1}% -> {newProgress:F1}% (+{amount:F1} from {contribution.SourceType}:{contribution.SourceName})");
        }

        /// <summary>Calculate which stage a progress value falls into.</summary>
        private ExplorationStage CalculateStage(float progress)
        {
            if (progress < _stage2Threshold)
                return ExplorationStage.Novice;
            if (progress < _stage3Threshold)
                return ExplorationStage.Familiar;
            if (progress < _stage4Threshold)
                return ExplorationStage.Deep;
            if (progress < _stage5Threshold)
                return ExplorationStage.Master;
            return ExplorationStage.Complete;
        }

        /// <summary>Handle stage-dependent unlocks (side effects only).</summary>
        /// <remarks>
        /// The ExplorationStageUpEvent is published in AddProgress — this method
        /// handles only the specific side effects that each stage threshold unlocks.
        /// </remarks>
        private void OnStageChanged(string regionId, ExplorationStage oldStage, ExplorationStage newStage, float progress)
        {
            // EXP-03: At 40% (Deep stage), medium/small POIs start showing.
            // Map UI listens for stage >= Deep to show POI markers.
            if (oldStage < ExplorationStage.Deep && newStage >= ExplorationStage.Deep)
            {
                if (_debugLogging)
                    Debug.Log($"[ExplorationDepth] EXP-03: Region '{regionId}' Deep stage — medium/small POIs now visible.");
            }

            // EXP-04: At 80% (Complete stage), hidden entrance hints appear.
            // Map UI listens for stage >= Complete to show hidden indicator markers.
            if (oldStage < ExplorationStage.Complete && newStage >= ExplorationStage.Complete)
            {
                if (_debugLogging)
                    Debug.Log($"[ExplorationDepth] EXP-04: Region '{regionId}' Complete stage — hidden entrance hints now visible.");
            }
        }

        #endregion

        #region Coverage Contribution

        /// <summary>
        /// Calculate and add exploration progress from map coverage.
        /// Every 1% of the region's total cells explored = +0.1% exploration progress.
        ///
        /// NOTE: FogOfWar.ExploredCellCount is currently global (not per-region).
        /// Coverage contribution uses this global count against the registered region
        /// total cells as an approximation. For accurate per-region coverage,
        /// FogOfWar would need per-region cell tracking (future enhancement).
        /// </summary>
        private void UpdateCoverageContribution(string regionId)
        {
            if (!_regions.ContainsKey(regionId))
                return;

            // Need both total cells and explored cells from FogOfWar.
            if (!_regionTotalCells.TryGetValue(regionId, out int totalCells) || totalCells <= 0)
                return;

            if (FogOfWar.Instance == null)
                return;

            int exploredCells = FogOfWar.Instance.ExploredCellCount;
            if (exploredCells <= 0)
                return;

            // Calculate explored percentage of total cells.
            float exploredPercent = (float)exploredCells / totalCells * 100f;

            // Each 1% explored = _coveragePerPercent exploration progress.
            float expectedCoverageProgress = exploredPercent * _coveragePerPercent;

            // Only add if coverage contribution would be higher than what we've already
            // tracked. CoverageProgress tracks the running contribution from area coverage.
            float currentCoverageProgress = _regions[regionId].CoverageProgress;
            float delta = expectedCoverageProgress - currentCoverageProgress;

            if (delta > 0.01f)
            {
                // Update coverage progress before AddProgress to prevent re-entry overlap.
                var mutableState = _regions[regionId];
                mutableState.CoverageProgress = expectedCoverageProgress;
                _regions[regionId] = mutableState;

                var contribution = new DiscoveryContribution
                {
                    Amount = delta,
                    SourceName = "区域探索",
                    SourceType = "Coverage"
                };

                AddProgress(regionId, delta, contribution);
            }
        }

        #endregion

        #region Title Granting

        /// <summary>
        /// Grant the "探索大师" (Exploration Master) title when a region reaches 100% (EXP-05).
        /// </summary>
        private void GrantMasterTitle(string regionId)
        {
            var state = _regions[regionId];
            state.TitleGranted = true;
            _regions[regionId] = state;

            // Publish completion event.
            EventBus.Publish(new ExplorationCompleteEvent
            {
                RegionId = regionId,
                TitleId = MASTER_TITLE_ID,
                TitleName = MASTER_TITLE_NAME
            });

            // TODO: Integrate with TitleSystem once implemented.
            // TitleSystem.Grant(regionId, MASTER_TITLE_ID, MASTER_TITLE_NAME);
            // Currently handled by the UI layer listening to ExplorationCompleteEvent.

            Debug.Log($"[ExplorationDepth] EXP-05: Region '{regionId}' 100% explored! Title '{MASTER_TITLE_NAME}' granted.");
        }

        #endregion

        #region Public Query API

        /// <summary>
        /// Get exploration progress (0-100) for a region.
        /// Returns 0 if region is not tracked.
        /// </summary>
        public float GetProgress(string regionId)
        {
            return _regions.TryGetValue(regionId, out var state) ? state.Progress : 0f;
        }

        /// <summary>
        /// Get the exploration stage for a region.
        /// Returns Novice if region is not tracked.
        /// </summary>
        public ExplorationStage GetStage(string regionId)
        {
            return _regions.TryGetValue(regionId, out var state) ? state.Stage : ExplorationStage.Novice;
        }

        /// <summary>
        /// Get the localized stage name for a region.
        /// Returns "初来乍到" if region is not tracked.
        /// </summary>
        public string GetStageName(string regionId)
        {
            return _regions.TryGetValue(regionId, out var state) ? state.StageName : GetStageLocalizedName(ExplorationStage.Novice);
        }

        /// <summary>
        /// Check if a region has reached the Deep stage (40%+).
        /// When true, medium/small POIs should be visible on the map (EXP-03).
        /// </summary>
        public bool AreSmallPOIVisible(string regionId)
        {
            return GetStage(regionId) >= ExplorationStage.Deep;
        }

        /// <summary>
        /// Check if a region has reached the Complete stage (80%+).
        /// When true, hidden entrance hints should appear on the map (EXP-04).
        /// </summary>
        public bool AreHiddenHintsVisible(string regionId)
        {
            return GetStage(regionId) >= ExplorationStage.Complete;
        }

        /// <summary>
        /// Check if a region has been fully explored (EXP-05).
        /// </summary>
        public bool IsRegionComplete(string regionId)
        {
            return _regions.TryGetValue(regionId, out var state) && state.Progress >= 100f;
        }

        /// <summary>
        /// Check if the master title has been granted for a region.
        /// </summary>
        public bool HasMasterTitle(string regionId)
        {
            return _regions.TryGetValue(regionId, out var state) && state.TitleGranted;
        }

        /// <summary>
        /// Get the localized display name for an exploration stage.
        /// </summary>
        public static string GetStageLocalizedName(ExplorationStage stage)
        {
            return stage switch
            {
                ExplorationStage.Novice   => "初来乍到",
                ExplorationStage.Familiar => "渐渐熟悉",
                ExplorationStage.Deep     => "深入探索",
                ExplorationStage.Master   => "了如指掌",
                ExplorationStage.Complete => "完全掌控",
                _                         => "未知"
            };
        }

        /// <summary>
        /// Get a full snapshot of all region states.
        /// </summary>
        public Dictionary<string, RegionExplorationState> GetAllRegionStates()
        {
            return new Dictionary<string, RegionExplorationState>(_regions);
        }

        #endregion

        #region Save / Load

        /// <summary>Capture the current exploration state as serializable data.</summary>
        public ExplorationSaveData GetSaveData()
        {
            var records = new RegionExplorationRecord[_regions.Count];
            int index = 0;
            foreach (var kvp in _regions)
            {
                records[index] = new RegionExplorationRecord
                {
                    RegionId = kvp.Key,
                    Progress = kvp.Value.Progress,
                    StageIndex = (int)kvp.Value.Stage,
                    TitleGranted = kvp.Value.TitleGranted
                };
                index++;
            }

            return new ExplorationSaveData
            {
                Regions = records
            };
        }

        /// <summary>Restore exploration state from previously saved data.</summary>
        public void LoadSaveData(ExplorationSaveData data)
        {
            if (data?.Regions == null)
            {
                Debug.LogWarning("[ExplorationDepth] LoadSaveData: null or empty data.");
                return;
            }

            _regions.Clear();

            foreach (var record in data.Regions)
            {
                if (string.IsNullOrEmpty(record.RegionId))
                    continue;

                ExplorationStage stage = (ExplorationStage)Mathf.Clamp(
                    record.StageIndex, 0, (int)ExplorationStage.Complete);

                _regions[record.RegionId] = new RegionExplorationState
                {
                    Progress = Mathf.Clamp(record.Progress, 0f, 100f),
                    Stage = stage,
                    StageName = GetStageLocalizedName(stage),
                    HasEntered = record.Progress > 0f,
                    TitleGranted = record.TitleGranted,
                    CoverageProgress = 0f // Recalculated on next coverage tick.
                };
            }

            Debug.Log($"[ExplorationDepth] Loaded exploration data: {_regions.Count} regions restored.");
        }

        /// <summary>Reset all exploration data (for new game).</summary>
        public void ClearAll()
        {
            _regions.Clear();
            _discoveryContributions.Clear();
            _regionTotalCells.Clear();
            _currentRegionId = null;

            Debug.Log("[ExplorationDepth] All exploration data cleared.");
        }

        #endregion

        #region Editor / Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            if (_regions.Count == 0)
                return "=== Exploration Depth ===\nNo regions tracked.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Exploration Depth ===");

            foreach (var kvp in _regions)
            {
                var s = kvp.Value;
                string cellsInfo = _regionTotalCells.TryGetValue(kvp.Key, out int totalCells)
                    ? $"Cells: {FogOfWar.Instance?.ExploredCellCount ?? 0}/{totalCells}"
                    : "Cells: N/A";

                sb.AppendLine($"[{kvp.Key}] {s.Progress:F1}% | {s.StageName} | {cellsInfo} | Title: {s.TitleGranted}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Forcibly set exploration progress for a region (debug/testing only).
        /// </summary>
        public void DebugSetProgress(string regionId, float progress)
        {
            if (string.IsNullOrEmpty(regionId))
                return;

            EnsureRegionTracked(regionId);
            float clamped = Mathf.Clamp(progress, 0f, 100f);

            var state = _regions[regionId];
            float previous = state.Progress;
            state.Progress = clamped;
            state.Stage = CalculateStage(clamped);
            state.StageName = GetStageLocalizedName(state.Stage);
            _regions[regionId] = state;

            EventBus.Publish(new ExplorationProgressChangedEvent
            {
                RegionId = regionId,
                PreviousProgress = previous,
                CurrentProgress = clamped,
                Stage = state.Stage,
                StageName = state.StageName
            });

            Debug.Log($"[ExplorationDepth] [DEBUG] Region '{regionId}' progress set to {clamped:F1}%");
        }

        #endregion
    }

    #region Internal State

    /// <summary>
    /// Internal runtime state for a single region's exploration tracking.
    /// Not serialized directly — use RegionExplorationRecord for save/load.
    /// </summary>
    public struct RegionExplorationState
    {
        /// <summary>Current exploration progress 0-100.</summary>
        public float Progress;

        /// <summary>Current exploration stage.</summary>
        public ExplorationStage Stage;

        /// <summary>Cached localized stage name.</summary>
        public string StageName;

        /// <summary>Whether the player has ever entered this region.</summary>
        public bool HasEntered;

        /// <summary>Whether the master title has been granted.</summary>
        public bool TitleGranted;

        /// <summary>
        /// Progress derived from map coverage area.
        /// Used to avoid double-counting coverage contributions.
        /// </summary>
        public float CoverageProgress;
    }

    #endregion
}
