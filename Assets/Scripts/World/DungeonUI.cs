using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;
using UnityEngine.UI;
using CultivationRealm = EarthOnline.CultivationManager.Realm;

namespace EarthOnline.World
{
    // ─── UI Event Data ──────────────────────────────────────────────────

    /// <summary>Published when the entrance panel is shown or hidden.</summary>
    public struct DungeonEntrancePanelEvent
    {
        public bool Show;
        public string DungeonId;
    }

    /// <summary>Published after the minimap is built from the dungeon layout.</summary>
    public struct DungeonMinimapBuiltEvent
    {
        public int RoomCount;
        public int MaxDepth;
    }

    /// <summary>Published when the settlement panel is displayed after a run.</summary>
    public struct DungeonSettlementShowEvent
    {
        public bool Show;
        public DungeonRating Rating;
        public int Score;
        public float BonusMultiplier;
    }

    /// <summary>
    /// Published after the run, describing how the dungeon's environment changes
    /// in the overworld based on the player's performance.
    /// </summary>
    public struct DungeonEnvironmentFeedbackEvent
    {
        public string DungeonId;
        public float SpiritConcentrationDelta;  // percentage change (0.10 = +10%)
        public string[] NewUnlockedRewards;
        public string EntranceAppearanceDescription;
        public string NewUnlockDescription;
    }

    // ─── Minimap Room Marker ───────────────────────────────────────────

    /// <summary>
    /// Behaviour for a single room node on the dungeon minimap.
    /// Instantiated as a child of the minimap container for each room in the layout.
    /// </summary>
    public class MinimapRoomMarker : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;

        public int RoomIndex { get; private set; }
        public RoomType Type { get; private set; }

        public Image BackgroundImage => _background;
        public Image IconImage => _icon;
        public Text LabelText => _label;

        /// <summary>Set up the marker with room data and visual state.</summary>
        public void Initialize(int index, RoomType type, Color bgColor, Sprite iconSprite, string label)
        {
            RoomIndex = index;
            Type = type;
            if (_background != null) _background.color = bgColor;
            if (_icon != null && iconSprite != null) _icon.sprite = iconSprite;
            if (_label != null) _label.text = label;
        }

        /// <summary>Toggle the "current room" highlight (scale).</summary>
        public void SetHighlight(bool isCurrent)
        {
            transform.localScale = isCurrent ? Vector3.one * 1.35f : Vector3.one;
            if (_background != null)
            {
                // Brighter border when current
                _background.transform.localScale = isCurrent ? Vector3.one * 1.15f : Vector3.one;
            }
        }

        /// <summary>Mark as cleared (visited) or uncleared.</summary>
        public void SetCleared(bool cleared)
        {
            if (_background == null) return;
            float alpha = cleared ? 0.9f : 0.5f;
            var c = _background.color;
            _background.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    // ─── Dungeon UI Controller ─────────────────────────────────────────

    /// <summary>
    /// Manages all dungeon-related UI presented to the player:
    ///
    /// 1) Entrance info panel  — recommended realm, known rewards, run history.
    /// 2) In-dungeon minimap   — room nodes with type markers, connection lines,
    ///                           current-room highlight, visited tracking.
    /// 3) Rating settlement    — S/A/B/C/D display, score breakdown, bonus.
    /// 4) Environment feedback — spirit-concentration delta, output changes,
    ///                           entrance appearance evolution.
    ///
    /// Communication via EventBus (typed structs).  UGUI SerializeFields for
    /// all visual references — drag-and-drop wiring in the Unity Editor.
    /// </summary>
    public class DungeonUI : MonoBehaviour
    {
        // ── Panel References ─────────────────────────────────────────────

        [Header("Panels (root GameObjects)")]
        [SerializeField] private GameObject _entrancePanel;
        [SerializeField] private GameObject _minimapPanel;
        [SerializeField] private GameObject _settlementPanel;
        [SerializeField] private GameObject _feedbackPanel;

        // ── Entrance Panel ───────────────────────────────────────────────

        [Header("Entrance Panel")]
        [SerializeField] private Text _dungeonNameText;
        [SerializeField] private Text _recommendedRealmText;
        [SerializeField] private Text _knownRewardsText;
        [SerializeField] private Text _historyText;
        [SerializeField] private Text _entranceDescriptionText;
        [SerializeField] private Button _enterButton;
        [SerializeField] private Button _closeEntranceButton;

        [Header("Difficulty Selection")]
        [SerializeField] private Button _easyButton;
        [SerializeField] private Button _normalButton;
        [SerializeField] private Button _hardButton;
        [SerializeField] private Button _nightmareButton;

        // ── Minimap ──────────────────────────────────────────────────────

        [Header("Minimap")]
        [SerializeField] private RectTransform _minimapContainer;
        [SerializeField] private GameObject _roomMarkerPrefab;
        [SerializeField] private Vector2 _roomSpacing = new Vector2(75f, 55f);
        [SerializeField] private float _lineThickness = 2f;

        [Header("Minimap Colors")]
        [SerializeField] private Color _currentRoomColor = new Color(1f, 0.92f, 0.016f, 1f);
        [SerializeField] private Color _visitedRoomColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);
        [SerializeField] private Color _lockedRoomColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
        [SerializeField] private Color _lineColor = new Color(1f, 1f, 1f, 0.25f);

        // ── Settlement Panel ─────────────────────────────────────────────

        [Header("Settlement Panel")]
        [SerializeField] private Text _ratingText;
        [SerializeField] private Text _scoreText;
        [SerializeField] private Text _breakdownText;
        [SerializeField] private Text _bonusText;
        [SerializeField] private Button _settlementConfirmButton;

        [Header("Settlement Colors")]
        [SerializeField] private Color _ratingSColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color _ratingAColor = new Color(1f, 0.55f, 0f);
        [SerializeField] private Color _ratingBColor = new Color(0.4f, 0.6f, 1f);
        [SerializeField] private Color _ratingCColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color _ratingDColor = new Color(0.6f, 0.6f, 0.6f);

        // ── Feedback Panel ───────────────────────────────────────────────

        [Header("Feedback Panel")]
        [SerializeField] private Text _spiritConcentrationText;
        [SerializeField] private Text _outputChangesText;
        [SerializeField] private Text _appearanceChangeText;
        [SerializeField] private Text _unlockDescriptionText;
        [SerializeField] private Button _feedbackConfirmButton;

        // ── Dungeon Config ───────────────────────────────────────────────

        [Header("Dungeon Config")]
        [SerializeField] private string _dungeonId = "default_dungeon";
        [SerializeField] private CultivationRealm _recommendedRealm = CultivationRealm.QiRefining;
        [SerializeField] private string[] _knownRewardNames;
        [SerializeField][TextArea(3, 5)] private string _entranceDescription =
            "An ancient ruin shrouded in mystery.";

        // ── Room Type Icons (assign in Editor) ───────────────────────────

        [Header("Room Icons")]
        [SerializeField] private Sprite _combatIcon;
        [SerializeField] private Sprite _treasureIcon;
        [SerializeField] private Sprite _trapIcon;
        [SerializeField] private Sprite _merchantIcon;
        [SerializeField] private Sprite _restIcon;
        [SerializeField] private Sprite _bossIcon;

        // ── Runtime State ────────────────────────────────────────────────

        private DungeonInstance _dungeonInstance;
        private DungeonProgress _dungeonProgress;
        private DungeonReward _dungeonReward;
        private DungeonLayout _currentLayout;

        private readonly List<MinimapRoomMarker> _roomMarkers = new List<MinimapRoomMarker>(24);
        private readonly List<Image> _connectionLines = new List<Image>(32);
        private readonly HashSet<int> _visitedRooms = new HashSet<int>();
        private int _lastRoomIndex = -1;

        // ── History persistence ──────────────────────────────────────────

        private const string HISTORY_KEY_PREFIX = "DungeonHistory_";
        private DungeonHistoryData _historyData;

        [Serializable]
        private struct DungeonHistoryData
        {
            public int TotalRuns;
            public int BestScore;
            public string BestRating;      // "S", "A", "B", "C", "D"
            public float BestElapsedTime;
            public int BestRoomsCleared;
        }

        // =================================================================
        //  LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _dungeonInstance = GetComponent<DungeonInstance>();
            _dungeonProgress = GetComponent<DungeonProgress>();
            _dungeonReward = GetComponent<DungeonReward>();

            LoadHistory();
            SetAllPanelsActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DungeonEnteredEvent>(OnDungeonEntered);
            EventBus.Subscribe<DungeonRoomChangedEvent>(OnDungeonRoomChanged);
            EventBus.Subscribe<DungeonCompletedEvent>(OnDungeonCompleted);
            EventBus.Subscribe<DungeonExitedEvent>(OnDungeonExited);
            EventBus.Subscribe<DungeonRatingEvent>(OnDungeonRating);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DungeonEnteredEvent>(OnDungeonEntered);
            EventBus.Unsubscribe<DungeonRoomChangedEvent>(OnDungeonRoomChanged);
            EventBus.Unsubscribe<DungeonCompletedEvent>(OnDungeonCompleted);
            EventBus.Unsubscribe<DungeonExitedEvent>(OnDungeonExited);
            EventBus.Unsubscribe<DungeonRatingEvent>(OnDungeonRating);
        }

        // =================================================================
        //  PANEL 1 — ENTRANCE INFO + DIFFICULTY SELECTION
        // =================================================================

        /// <summary>Show the entrance info panel (recommended realm, rewards, history).</summary>
        public void ShowEntrancePanel()
        {
            SetAllPanelsActive(false);
            _entrancePanel.SetActive(true);

            // ── Dungeon name ──
            if (_dungeonNameText != null)
                _dungeonNameText.text = _dungeonInstance != null
                    ? _dungeonInstance.DungeonId
                    : _dungeonId;

            // ── Recommended realm ──
            if (_recommendedRealmText != null)
                _recommendedRealmText.text = $"推荐境界: {GetRealmDisplayName(_recommendedRealm)}";

            // ── Known rewards ──
            if (_knownRewardsText != null)
            {
                _knownRewardsText.text = "已知产出:";
                if (_knownRewardNames != null && _knownRewardNames.Length > 0)
                {
                    foreach (var reward in _knownRewardNames)
                        _knownRewardsText.text += $"\n  • {reward}";
                }
                else
                {
                    _knownRewardsText.text += "\n  (暂无记录)";
                }
            }

            // ── Run history ──
            if (_historyText != null)
                UpdateHistoryDisplay();

            // ── Description ──
            if (_entranceDescriptionText != null)
                _entranceDescriptionText.text = _entranceDescription;

            // ── Wire buttons ──
            SetupDifficultyButtons();

            if (_closeEntranceButton != null)
            {
                _closeEntranceButton.onClick.RemoveAllListeners();
                _closeEntranceButton.onClick.AddListener(HideEntrancePanel);
            }

            EventBus.Publish(new DungeonEntrancePanelEvent
            {
                Show = "true",
                DungeonId = _dungeonId
            });

            Debug.Log($"[DungeonUI] Entrance panel shown for '{_dungeonId}'.");
        }

        /// <summary>Hide the entrance panel without entering a dungeon.</summary>
        public void HideEntrancePanel()
        {
            _entrancePanel.SetActive(false);
            EventBus.Publish(new DungeonEntrancePanelEvent
            {
                Show = "false",
                DungeonId = _dungeonId
            });
        }

        private void SetupDifficultyButtons()
        {
            WireDifficultyButton(_easyButton, DungeonDifficulty.Easy);
            WireDifficultyButton(_normalButton, DungeonDifficulty.Normal);
            WireDifficultyButton(_hardButton, DungeonDifficulty.Hard);
            WireDifficultyButton(_nightmareButton, DungeonDifficulty.Nightmare);
        }

        private void WireDifficultyButton(Button btn, DungeonDifficulty difficulty)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectDifficultyAndEnter(difficulty));
        }

        private void SelectDifficultyAndEnter(DungeonDifficulty difficulty)
        {
            if (_dungeonInstance == null)
            {
                Debug.LogError("[DungeonUI] No DungeonInstance found.");
                return;
            }
            _dungeonInstance.SelectDifficulty(difficulty);
            _entrancePanel.SetActive(false);

            // The minimap will be built when OnDungeonEntered fires
            Debug.Log($"[DungeonUI] Difficulty {difficulty} selected, entering dungeon.");
        }

        // =================================================================
        //  PANEL 2 — IN-DUNGEON MINIMAP + ROOM MARKERS
        // =================================================================

        /// <summary>Build minimap room nodes and connection lines from a layout.</summary>
        private void BuildMinimap(DungeonLayout layout)
        {
            _currentLayout = layout;
            ClearMinimap();

            if (layout == null || _minimapContainer == null || _roomMarkerPrefab == null)
                return;

            // ── Discover max depth ──
            int maxDepth = 0;
            for (int i = 0; i < layout.RoomCount; i++)
            {
                var room = layout.GetRoom(i);
                if (room != null && room.Depth > maxDepth)
                    maxDepth = room.Depth;
            }

            // ── Group rooms by depth ──
            var roomsByDepth = new Dictionary<int, List<int>>();
            for (int i = 0; i < layout.RoomCount; i++)
            {
                var room = layout.GetRoom(i);
                if (room == null) continue;
                if (!roomsByDepth.ContainsKey(room.Depth))
                    roomsByDepth[room.Depth] = new List<int>();
                roomsByDepth[room.Depth].Add(i);
            }

            // ── Instantiate room markers ──
            int maxRoomsInLayer = 0;
            foreach (var kvp in roomsByDepth)
                if (kvp.Value.Count > maxRoomsInLayer)
                    maxRoomsInLayer = kvp.Value.Count;

            foreach (var kvp in roomsByDepth)
            {
                int depth = kvp.Key;
                var indices = kvp.Value;
                float layerWidth = indices.Count * _roomSpacing.x;
                float startX = -layerWidth / 2f + _roomSpacing.x / 2f;

                for (int r = 0; r < indices.Count; r++)
                {
                    int roomIndex = indices[r];
                    var room = layout.GetRoom(roomIndex);
                    if (room == null) continue;

                    GameObject go = Instantiate(_roomMarkerPrefab, _minimapContainer);
                    MinimapRoomMarker marker = go.GetComponent<MinimapRoomMarker>();
                    if (marker == null)
                        marker = go.AddComponent<MinimapRoomMarker>();

                    float posX = startX + r * _roomSpacing.x;
                    float posY = -(depth * _roomSpacing.y);
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, posY);

                    marker.Initialize(
                        roomIndex,
                        room.RoomType,
                        _lockedRoomColor,
                        GetRoomTypeIcon(room.RoomType),
                        GetRoomTypeShortLabel(room.RoomType)
                    );

                    _roomMarkers.Add(marker);
                }
            }

            // ── Connection lines ──
            for (int i = 0; i < layout.RoomCount; i++)
            {
                var room = layout.GetRoom(i);
                if (room == null) continue;

                MinimapRoomMarker fromMarker = GetMarkerForRoom(i);
                if (fromMarker == null) continue;

                foreach (int branch in room.Branches)
                {
                    MinimapRoomMarker toMarker = GetMarkerForRoom(branch);
                    if (toMarker == null) continue;

                    Image line = CreateConnectionLine(fromMarker.transform, toMarker.transform);
                    _connectionLines.Add(line);
                }
            }

            // ── Highlight first room ──
            _lastRoomIndex = 0;
            UpdateMinimapForRoom(0);

            EventBus.Publish(new DungeonMinimapBuiltEvent
            {
                RoomCount = layout.RoomCount,
                MaxDepth = maxDepth
            });

            Debug.Log($"[DungeonUI] Minimap built: {layout.RoomCount} rooms, {maxDepth + 1} layers.");
        }

        private void ClearMinimap()
        {
            foreach (var m in _roomMarkers)
                if (m != null) Destroy(m.gameObject);
            _roomMarkers.Clear();

            foreach (var l in _connectionLines)
                if (l != null) Destroy(l.gameObject);
            _connectionLines.Clear();

            _visitedRooms.Clear();
            _lastRoomIndex = -1;
        }

        /// <summary>Draw a UGUI line (thin rotated Image) between two room markers.</summary>
        private Image CreateConnectionLine(Transform from, Transform to)
        {
            var go = new GameObject("ConnectionLine", typeof(Image));
            go.transform.SetParent(_minimapContainer, false);
            var image = go.GetComponent<Image>();
            image.color = _lineColor;

            var rect = go.GetComponent<RectTransform>();
            Vector2 fromPos = ((RectTransform)from).anchoredPosition;
            Vector2 toPos = ((RectTransform)to).anchoredPosition;
            Vector2 mid = (fromPos + toPos) / 2f;
            Vector2 delta = toPos - fromPos;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            rect.anchoredPosition = mid;
            rect.sizeDelta = new Vector2(length, _lineThickness);
            rect.rotation = Quaternion.Euler(0f, 0f, angle);

            return image;
        }

        private MinimapRoomMarker GetMarkerForRoom(int roomIndex)
        {
            foreach (var m in _roomMarkers)
                if (m != null && m.RoomIndex == roomIndex)
                    return m;
            return null;
        }

        /// <summary>Update minimap visuals when the player enters a new room.</summary>
        private void UpdateMinimapForRoom(int roomIndex)
        {
            // Mark previous room as visited
            if (_lastRoomIndex >= 0 && _lastRoomIndex != roomIndex)
                _visitedRooms.Add(_lastRoomIndex);

            _lastRoomIndex = roomIndex;

            foreach (var marker in _roomMarkers)
            {
                if (marker == null) continue;

                if (marker.RoomIndex == roomIndex)
                {
                    marker.SetHighlight(true);
                    marker.SetCleared(false);
                }
                else if (_visitedRooms.Contains(marker.RoomIndex))
                {
                    marker.SetHighlight(false);
                    marker.SetCleared(true);
                }
                else
                {
                    marker.SetHighlight(false);
                    marker.SetCleared(false);
                }
            }
        }

        // =================================================================
        //  PANEL 3 — RATING SETTLEMENT (S/A/B/C/D)
        // =================================================================

        private void ShowSettlementPanel(DungeonRating rating, int score, float bonusMultiplier)
        {
            SetAllPanelsActive(false);
            _settlementPanel.SetActive(true);

            // ── Rating ──
            if (_ratingText != null)
            {
                _ratingText.text = GetRatingDisplayName(rating);
                _ratingText.color = GetRatingColor(rating);
            }

            // ── Score ──
            if (_scoreText != null)
                _scoreText.text = $"总分: {score}";

            // ── Breakdown ──
            if (_breakdownText != null && _dungeonProgress != null)
            {
                var data = _dungeonProgress.CurrentProgress;
                _breakdownText.text =
                    "评分明细:\n" +
                    $"  通关房间: {data.RoomsCleared}\n" +
                    $"  Boss击败: {(data.BossDefeated ? "✔" : "✘")}\n" +
                    $"  杀敌数:    {data.EnemiesDefeated}\n" +
                    $"  受伤:      {data.DamageTaken}\n" +
                    $"  收集物:    {data.CollectiblesFound}\n" +
                    $"  用时:      {data.ElapsedTime:F1}s";
            }

            // ── Bonus ──
            if (_bonusText != null)
                _bonusText.text = $"奖励倍率: ×{bonusMultiplier:F1}";

            // ── Confirm button → save history + show feedback ──
            if (_settlementConfirmButton != null)
            {
                _settlementConfirmButton.onClick.RemoveAllListeners();
                _settlementConfirmButton.onClick.AddListener(() =>
                {
                    _settlementPanel.SetActive(false);
                    SaveHistory(rating, score);
                    ShowEnvironmentFeedback(rating);
                });
            }

            EventBus.Publish(new DungeonSettlementShowEvent
            {
                Show = "true",
                Rating = rating,
                Score = score,
                BonusMultiplier = bonusMultiplier
            });

            Debug.Log($"[DungeonUI] Settlement shown: {rating} ({score} pts, ×{bonusMultiplier}).");
        }

        // =================================================================
        //  PANEL 4 — ENVIRONMENT FEEDBACK (AFTER SETTLEMENT)
        // =================================================================

        private void ShowEnvironmentFeedback(DungeonRating rating)
        {
            _feedbackPanel.SetActive(true);

            // ── Spirit concentration delta ──
            float spiritDelta = rating switch
            {
                DungeonRating.S => 0.15f,
                DungeonRating.A => 0.10f,
                DungeonRating.B => 0.05f,
                DungeonRating.C => 0.02f,
                DungeonRating.D => -0.02f,
                _ => 0f
            };

            if (_spiritConcentrationText != null)
            {
                string sign = spiritDelta >= 0f ? "+" : "";
                string colorTag = spiritDelta >= 0f ? "#66FF66" : "#FF6666";
                _spiritConcentrationText.text =
                    $"灵气浓度变化: <color={colorTag}>{sign}{spiritDelta * 100f:F0}%</color>";
            }

            // ── New unlocked output ──
            string[] newRewards = GetNewRewards(rating);
            if (_outputChangesText != null)
            {
                _outputChangesText.text = "新增产出:";
                if (newRewards.Length > 0)
                {
                    foreach (var r in newRewards)
                        _outputChangesText.text += $"\n  • {r}";
                }
                else
                {
                    _outputChangesText.text += "\n  (无变化)";
                }
            }

            // ── Entrance appearance ──
            if (_appearanceChangeText != null)
                _appearanceChangeText.text = $"入口外观: {GetEntranceAppearanceChange(rating)}";

            // ── Unlock description ──
            if (_unlockDescriptionText != null)
                _unlockDescriptionText.text = GetUnlockDescription(rating);

            // ── Confirm button ──
            if (_feedbackConfirmButton != null)
            {
                _feedbackConfirmButton.onClick.RemoveAllListeners();
                _feedbackConfirmButton.onClick.AddListener(() =>
                {
                    _feedbackPanel.SetActive(false);
                    Debug.Log("[DungeonUI] Feedback acknowledged, panel closed.");
                });
            }

            // ── Publish for the world system to react ──
            EventBus.Publish(new DungeonEnvironmentFeedbackEvent
            {
                DungeonId = _dungeonId,
                SpiritConcentrationDelta = spiritDelta,
                NewUnlockedRewards = newRewards,
                EntranceAppearanceDescription = GetEntranceAppearanceChange(rating),
                NewUnlockDescription = GetUnlockDescription(rating)
            });

            Debug.Log($"[DungeonUI] Environment feedback shown (rating={rating}).");
        }

        // =================================================================
        //  EVENT HANDLERS
        // =================================================================

        private void OnDungeonEntered(DungeonEnteredEvent evt)
        {
            if (_dungeonInstance == null) return;
            _currentLayout = _dungeonInstance.Layout;
            if (_currentLayout != null)
            {
                BuildMinimap(_currentLayout);
            }
            _minimapPanel.SetActive(true);
        }

        private void OnDungeonRoomChanged(DungeonRoomChangedEvent evt)
        {
            UpdateMinimapForRoom(evt.RoomIndex);
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            // Mark boss room as visited on minimap
            if (_currentLayout != null)
            {
                int bossIdx = _currentLayout.RoomCount - 1;
                if (!_visitedRooms.Contains(bossIdx))
                    _visitedRooms.Add(bossIdx);
                UpdateMinimapForRoom(bossIdx);
            }
        }

        private void OnDungeonRating(DungeonRatingEvent evt)
        {
            _minimapPanel.SetActive(false);
            ShowSettlementPanel(evt.Rating, evt.Score, evt.BonusMultiplier);
        }

        private void OnDungeonExited(DungeonExitedEvent evt)
        {
            SetAllPanelsActive(false);
            ClearMinimap();
        }

        // =================================================================
        //  HISTORY (PlayerPrefs)
        // =================================================================

        private void LoadHistory()
        {
            string key = HISTORY_KEY_PREFIX + _dungeonId;
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                _historyData = JsonUtility.FromJson<DungeonHistoryData>(json);
            }
            else
            {
                _historyData = new DungeonHistoryData();
            }
        }

        private void SaveHistory(DungeonRating rating, int score)
        {
            _historyData.TotalRuns++;

            if (score > _historyData.BestScore)
            {
                _historyData.BestScore = score;
                _historyData.BestRating = rating.ToString();
            }

            if (_dungeonProgress != null)
            {
                float elapsed = _dungeonProgress.CurrentProgress.ElapsedTime;
                if (_historyData.BestElapsedTime <= 0f || elapsed < _historyData.BestElapsedTime)
                    _historyData.BestElapsedTime = elapsed;

                int cleared = _dungeonProgress.CurrentProgress.RoomsCleared;
                if (cleared > _historyData.BestRoomsCleared)
                    _historyData.BestRoomsCleared = cleared;
            }

            string key = HISTORY_KEY_PREFIX + _dungeonId;
            string json = JsonUtility.ToJson(_historyData);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        private void UpdateHistoryDisplay()
        {
            if (_historyData.TotalRuns == 0)
            {
                _historyText.text = "历史记录:\n  (暂无记录)";
                return;
            }

            _historyText.text = "历史记录:\n" +
                $"  总挑战次数: {_historyData.TotalRuns}\n" +
                $"  最高评分:    {_historyData.BestRating ?? "-"}\n" +
                $"  最高得分:    {_historyData.BestScore}\n" +
                $"  最多通关:    {_historyData.BestRoomsCleared} 间\n" +
                $"  最快用时:    {(_historyData.BestElapsedTime > 0f ? $"{_historyData.BestElapsedTime:F1}s" : "-")}";
        }

        // =================================================================
        //  HELPERS
        // =================================================================

        private void SetAllPanelsActive(bool active)
        {
            if (_entrancePanel != null) _entrancePanel.SetActive(active);
            if (_minimapPanel != null) _minimapPanel.SetActive(active);
            if (_settlementPanel != null) _settlementPanel.SetActive(active);
            if (_feedbackPanel != null) _feedbackPanel.SetActive(active);
        }

        private static string GetRealmDisplayName(CultivationRealm realm)
        {
            return realm switch
            {
                CultivationRealm.QiRefining => "练气期",
                CultivationRealm.Foundation => "筑基期",
                CultivationRealm.GoldenCore => "结丹期",
                CultivationRealm.NascentSoul => "元婴期",
                CultivationRealm.SpiritSevering => "化神期",
                CultivationRealm.Tribulation => "大圆满",
                CultivationRealm.GreatAscension => "渡劫成功",
                _ => "未知"
            };
        }

        private static string GetRatingDisplayName(DungeonRating rating)
        {
            return rating switch
            {
                DungeonRating.S => "S — 完美通关",
                DungeonRating.A => "A — 优秀",
                DungeonRating.B => "B — 良好",
                DungeonRating.C => "C — 合格",
                DungeonRating.D => "D — 险胜",
                _ => "?"
            };
        }

        private Color GetRatingColor(DungeonRating rating)
        {
            return rating switch
            {
                DungeonRating.S => _ratingSColor,
                DungeonRating.A => _ratingAColor,
                DungeonRating.B => _ratingBColor,
                DungeonRating.C => _ratingCColor,
                DungeonRating.D => _ratingDColor,
                _ => Color.white
            };
        }

        private Sprite GetRoomTypeIcon(RoomType type)
        {
            return type switch
            {
                RoomType.Combat => _combatIcon,
                RoomType.Treasure => _treasureIcon,
                RoomType.Trap => _trapIcon,
                RoomType.Merchant => _merchantIcon,
                RoomType.Rest => _restIcon,
                RoomType.Boss => _bossIcon,
                _ => null
            };
        }

        private static string GetRoomTypeShortLabel(RoomType type)
        {
            return type switch
            {
                RoomType.Combat => "战",      // 战
                RoomType.Treasure => "宝",    // 宝
                RoomType.Trap => "陷",        // 陷
                RoomType.Merchant => "商",     // 商
                RoomType.Rest => "息",         // 息
                RoomType.Boss => "B",
                _ => "?"
            };
        }

        /// <summary>Determine new rewards unlocked based on rating.</summary>
        private static string[] GetNewRewards(DungeonRating rating)
        {
            return rating switch
            {
                DungeonRating.S => new[] { "高阶丹药配方", "稀有灵材", "传说级武器图纸" },
                DungeonRating.A => new[] { "中阶丹药配方", "稀有灵材" },
                DungeonRating.B => new[] { "低阶丹药配方" },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>Describe entrance appearance change based on rating.</summary>
        private static string GetEntranceAppearanceChange(DungeonRating rating)
        {
            return rating switch
            {
                DungeonRating.S =>
                    "入口绽放出璀璨金光，灵气凝为实质，光芒照彻四方。古老的符文被激活，" +
                    "在空中勾勒出华丽的纹路。",
                DungeonRating.A =>
                    "入口灵光闪烁，周围的草木变得繁茂翠绿，空气中灵气充沛，令人心旷神怡。",
                DungeonRating.B =>
                    "入口处隐约有灵气波动，石门上的纹路发出微弱的荧光，显得古老而神秘。",
                DungeonRating.C =>
                    "入口依旧如常，唯有门缝间偶尔透出些许灵光，几乎不可察觉。",
                DungeonRating.D =>
                    "入口显得有些暗淡，门上的纹路逐渐失去光泽，周围的灵气似乎也变得稀薄了一些。",
                _ => "入口外观无明显变化。"
            };
        }

        private static string GetUnlockDescription(DungeonRating rating)
        {
            return rating switch
            {
                DungeonRating.S =>
                    "高阶奖励已完全解锁！入口升华至传说级外观，周边灵气浓度大幅提升。",
                DungeonRating.A =>
                    "额外奖励已解锁！入口得到灵气滋养，基础产出获得加成。",
                DungeonRating.B =>
                    "少量额外奖励已解锁，入口外观略有改善。",
                DungeonRating.C =>
                    "基础奖励已解锁。继续挑战更高评价以解锁更多内容。",
                DungeonRating.D =>
                    "奖励减少，入口灵气流失。下次争取更高评价吧。",
                _ => ""
            };
        }
    }
}
