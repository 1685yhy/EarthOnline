using System;
using System.Collections.Generic;
using EarthOnline.Core;
using EarthOnline.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthOnline.World
{
    #region Enums

    /// <summary>世界地图标记类型</summary>
    public enum MapMarkerType
    {
        FastTravel,      // 传送点
        DungeonEntrance, // 副本入口
        Landmark,        // 地标
        POI,             // 兴趣点
        Resource,        // 资源点
        Player,          // 玩家位置
        Quest            // 任务目标
    }

    /// <summary>地图显示模式</summary>
    public enum MapDisplayMode
    {
        Full,            // 全部显示
        FactionOnly,     // 仅显示势力
        ResourcesOnly,   // 仅显示资源
        QuestOnly        // 仅显示任务
    }

    #endregion

    #region Data Structures

    /// <summary>世界地图区域数据定义</summary>
    [Serializable]
    public class WorldMapRegionData
    {
        public string RegionId;
        public string DisplayName;
        public string FactionId;                    // 控制门派ID
        public Color FactionColor = Color.gray;     // 门派势力色
        public Rect RegionBounds;                   // 归一化坐标矩形 (0~1)
        public string RecommendedRealm;             // 推荐境界
        public string[] ResourceTypes;              // 资源产出类型
        public string[] DungeonIds;                 // 副本ID列表
        public string[] FastTravelPointIds;         // 传送点ID列表
        public bool RequiresExploration;            // 是否需要探索解锁
        public int ExplorationPercent;              // 当前探索度(0~100)
    }

    /// <summary>世界地图标记数据</summary>
    [Serializable]
    public class WorldMapMarkerData
    {
        public string MarkerId;
        public string DisplayName;
        public MapMarkerType MarkerType;
        public Vector2 NormalizedPosition;          // 归一化坐标 (0~1)
        public string RegionId;
        public string RelatedId;                    // 关联ID (dungeon/fast-travel id)
        public string SubText;                      // 副文本 (如推荐境界)
        public bool IsPermanent = true;
        public bool IsDiscovered = true;
        public Sprite MarkerIcon;
        public Color MarkerColor = Color.white;
        public float MarkerScale = 1f;
    }

    /// <summary>对象池条目</summary>
    internal class MapMarkerPoolItem
    {
        public GameObject GameObject;
        public Image IconImage;
        public Text LabelText;
        public RectTransform RectTransform;
        public bool IsActive;
        public MapMarkerType MarkerType;
    }

    #endregion

    #region Event Bus Events

    /// <summary>Published when world map is opened/closed.</summary>
    public struct WorldMapToggleEvent
    {
        public bool IsOpen;
    }

    /// <summary>Published when map zoom level changes.</summary>
    public struct WorldMapZoomEvent
    {
        public float ZoomLevel;
        public Vector2 FocusPoint;
    }

    /// <summary>Published when a marker is clicked on the world map.</summary>
    public struct WorldMapMarkerClickedEvent
    {
        public string MarkerId;
        public string DisplayName;
        public MapMarkerType MarkerType;
        public Vector2 NormalizedPosition;
    }

    #endregion

    /// <summary>
    /// 世界地图界面控制器 (Story 008)
    ///
    /// INT-01: 门派控制区域正确显示势力色
    /// INT-02: 地图上正确显示副本入口+推荐境界
    /// INT-03: 区域资源产出标记正确
    /// INT-04: 超过3个区域100%探索度不出现性能问题
    /// INT-05: 新手5分钟内可完成第一次探索（零学习成本）
    /// INT-06: 高等级玩家回低等级区域探索仍有收益
    /// INT-07: 越级探索收益与风险成正比
    /// INT-08: 地图支持缩放+拖拽+点击标记
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        #region Constants

        private const string PLAYER_TAG = "Player";
        private const float MIN_ZOOM = 0.3f;
        private const float MAX_ZOOM = 3.0f;
        private const float ZOOM_SPEED = 0.15f;
        private const float DRAG_THRESHOLD = 5f;      // pixels to start drag
        private const float PLAYER_MARKER_UPDATE_INTERVAL = 0.5f;
        private const int POOL_INITIAL_SIZE = 30;
        private const int POOL_MAX_SIZE = 100;
        private const float EXPLORATION_DARK_ALPHA = 0.65f; // 未探索遮罩透明度

        #endregion

        #region Singleton

        public static WorldMapUI Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("面板引用")]
        [SerializeField] private GameObject _mapPanel;
        [SerializeField] private RawImage _mapTextureDisplay;
        [SerializeField] private RectTransform _mapContentArea;    // 地图内容(缩放拖拽目标)
        [SerializeField] private RectTransform _markerContainer;   // 标记容器
        [SerializeField] private RectTransform _regionContainer;   // 区域容器
        [SerializeField] private RectTransform _fogContainer;      // 迷雾遮罩容器
        [SerializeField] private ScrollRect _mapScrollRect;

        [Header("UI元素")]
        [SerializeField] private Text _regionInfoText;
        [SerializeField] private Text _playerPositionText;
        [SerializeField] private Text _zoomLevelText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _zoomInButton;
        [SerializeField] private Button _zoomOutButton;
        [SerializeField] private Button _resetViewButton;
        [SerializeField] private ToggleGroup _displayModeToggles;

        [Header("地图配置")]
        [SerializeField] private WorldMapRegionData[] _regionDefinitions;
        [SerializeField] private WorldMapMarkerData[] _markerDefinitions;
        [SerializeField] private Sprite _defaultMarkerIcon;
        [SerializeField] private Sprite _fastTravelIcon;
        [SerializeField] private Sprite _dungeonIcon;
        [SerializeField] private Sprite _landmarkIcon;
        [SerializeField] private Sprite _playerIcon;
        [SerializeField] private Sprite _questIcon;
        [SerializeField] private Sprite _resourceIcon;
        [SerializeField] private Texture2D _defaultMapTexture;

        [Header("性能")]
        [SerializeField] private int _poolInitialSize = POOL_INITIAL_SIZE;
        [SerializeField] private int _poolMaxSize = POOL_MAX_SIZE;
        [SerializeField] private bool _enableFrustumCulling = true;
        [SerializeField] private float _cullingMargin = 1.5f;      // 视口外扩余量

        [Header("标记预制体")]
        [SerializeField] private GameObject _markerPrefab;

        #endregion

        #region Private State

        // ─── Zoom / Pan ───
        private float _currentZoom = 1.0f;
        private Vector2 _panOffset;
        private bool _isDragging;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartOffset;

        // ─── Player tracking ───
        private Transform _playerTransform;
        private float _playerMarkerTimer;
        private GameObject _playerMarkerGameObject;
        private MapMarkerPoolItem _playerMarkerItem;

        // ─── Marker Pool ───
        private List<MapMarkerPoolItem> _markerPool = new List<MapMarkerPoolItem>();
        private List<MapMarkerPoolItem> _activeMarkers = new List<MapMarkerPoolItem>();

        // ─── Region UI ───
        private Dictionary<string, GameObject> _regionUIObjects = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> _fogUIObjects = new Dictionary<string, GameObject>();

        // ─── State ───
        private bool _isOpen;
        private MapDisplayMode _currentDisplayMode = MapDisplayMode.Full;
        private Dictionary<string, WorldMapMarkerData> _markerDataMap = new Dictionary<string, WorldMapMarkerData>();
        private Camera _uiCamera;

        // ─── Event subscriptions ───
        private Action<DiscoveryMapMarkerEvent> _onDiscoveryMarker;
        private Action<FogBatchRevealedEvent> _onFogRevealed;

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

            _uiCamera = GameObject.Find("UICamera")?.GetComponent<Camera>()
                        ?? Camera.main;

            BuildMarkerDataMap();
            SetupEventListeners();
        }

        private void Start()
        {
            InitializePool();
            InitializeRegionUI();
            InitializeFogOverlay();
            InitializeMarkers();
            InitializePlayerMarker();

            if (_mapPanel != null)
                _mapPanel.SetActive(false);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseMap);

            if (_zoomInButton != null)
                _zoomInButton.onClick.AddListener(() => SetZoom(_currentZoom + 0.25f));

            if (_zoomOutButton != null)
                _zoomOutButton.onClick.AddListener(() => SetZoom(_currentZoom - 0.25f));

            if (_resetViewButton != null)
                _resetViewButton.onClick.AddListener(ResetView);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupEventListeners();
                Instance = null;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            HandleKeyboardInput();
            UpdatePlayerMarkerPosition();

            // Update region info based on cursor position.
            UpdateRegionInfoUnderCursor();
        }

        #endregion

        #region Event Setup

        private void SetupEventListeners()
        {
            _onDiscoveryMarker = OnDiscoveryMarker;
            _onFogRevealed = OnFogRevealed;

            EventBus.Subscribe<DiscoveryMapMarkerEvent>(_onDiscoveryMarker);
            EventBus.Subscribe<FogBatchRevealedEvent>(_onFogRevealed);
        }

        private void CleanupEventListeners()
        {
            if (_onDiscoveryMarker != null)
                EventBus.Unsubscribe<DiscoveryMapMarkerEvent>(_onDiscoveryMarker);
            if (_onFogRevealed != null)
                EventBus.Unsubscribe<FogBatchRevealedEvent>(_onFogRevealed);
        }

        private void OnDiscoveryMarker(DiscoveryMapMarkerEvent evt)
        {
            if (!evt.AddMarker) return;

            // Add a new marker dynamically.
            var markerData = new WorldMapMarkerData
            {
                MarkerId = evt.DiscoveryId,
                DisplayName = evt.DisplayName,
                MarkerType = DiscoveryTypeToMarkerType(evt.DiscoveryType),
                NormalizedPosition = WorldToNormalizedPosition(evt.WorldPosition),
                RegionId = ResolveRegionId(evt.WorldPosition),
                SubText = evt.IsFirstDiscovery ? "首次发现" : "",
                IsPermanent = evt.IsPermanent,
                IsDiscovered = "true",
                MarkerColor = Color.white,
                MarkerScale = evt.ShowQuestionMark ? 0.8f : 1.0f
            };

            _markerDataMap[evt.DiscoveryId] = markerData;
            CreateOrUpdateMarker(markerData);
        }

        private void OnFogRevealed(FogBatchRevealedEvent evt)
        {
            // Refresh fog overlay if needed.
            RefreshFogOverlay();
        }

        #endregion

        #region Public API — Open / Close

        /// <summary>Toggle the world map open/closed.</summary>
        public void ToggleMap()
        {
            if (_isOpen)
                CloseMap();
            else
                OpenMap();
        }

        /// <summary>Open the world map.</summary>
        public void OpenMap()
        {
            if (_mapPanel == null) return;

            _isOpen = true;
            _mapPanel.SetActive(true);

            // Pause game time? (optional)
            UpdatePlayerMarkerPosition();
            RefreshFogOverlay();

            EventBus.Publish(new WorldMapToggleEvent { IsOpen = true });

            Debug.Log("[WorldMapUI] 世界地图已打开");
        }

        /// <summary>Close the world map.</summary>
        public void CloseMap()
        {
            if (_mapPanel == null) return;

            _isOpen = false;
            _mapPanel.SetActive(false);

            EventBus.Publish(new WorldMapToggleEvent { IsOpen = false });

            Debug.Log("[WorldMapUI] 世界地图已关闭");
        }

        /// <summary>Is the map currently open?</summary>
        public bool IsOpen => _isOpen;

        #endregion

        #region Input Handling

        /// <summary>Handle M key and mouse input.</summary>
        private void HandleKeyboardInput()
        {
            // M键打开/关闭世界地图 (INT-08)
            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleMap();
                return;
            }

            // ESC closes map.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMap();
                return;
            }

            // Zoom (scroll wheel).
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector2 mousePos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _mapContentArea, Input.mousePosition, _uiCamera, out mousePos);
                ZoomAtPoint(scroll > 0 ? 0.1f : -0.1f, mousePos);
            }

            // Drag (mouse button hold).
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
            {
                if (!IsPointerOverUI())
                {
                    _isDragging = true;
                    _dragStartMouse = Input.mousePosition;
                    _dragStartOffset = _panOffset;
                }
            }

            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(2))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _dragStartMouse;
                _panOffset = _dragStartOffset + delta;
                ClampPanOffset();
                ApplyContentTransform();
            }

            // Click (not drag) on marker.
            if (Input.GetMouseButtonUp(0) && !_isDragging)
            {
                HandleMarkerClick();
            }
        }

        /// <summary>Check if the pointer is over a UI element.</summary>
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>Handle a click on a map marker.</summary>
        private void HandleMarkerClick()
        {
            // Raycast against marker objects.
            if (EventSystem.current == null) return;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                // Check if the clicked object is a marker.
                foreach (var marker in _activeMarkers)
                {
                    if (marker.GameObject == result.gameObject)
                    {
                        var markerData = FindMarkerDataByGameObject(marker.GameObject);
                        if (markerData != null)
                        {
                            EventBus.Publish(new WorldMapMarkerClickedEvent
                            {
                                MarkerId = markerData.MarkerId,
                                DisplayName = markerData.DisplayName,
                                MarkerType = markerData.MarkerType,
                                NormalizedPosition = markerData.NormalizedPosition
                            });
                        }
                        return;
                    }
                }
            }
        }

        #endregion

        #region Zoom & Pan

        /// <summary>Set zoom level directly (clamped).</summary>
        public void SetZoom(float zoom)
        {
            _currentZoom = Mathf.Clamp(zoom, MIN_ZOOM, MAX_ZOOM);
            ApplyContentTransform();

            EventBus.Publish(new WorldMapZoomEvent
            {
                ZoomLevel = _currentZoom,
                FocusPoint = _panOffset
            });
        }

        /// <summary>Zoom at a specific point (screen-space).</summary>
        private void ZoomAtPoint(float delta, Vector2 localPoint)
        {
            float oldZoom = _currentZoom;
            _currentZoom = Mathf.Clamp(_currentZoom + delta, MIN_ZOOM, MAX_ZOOM);

            // Adjust pan so the point under cursor stays fixed.
            float zoomFactor = _currentZoom / oldZoom;
            _panOffset = localPoint - (localPoint - _panOffset) * zoomFactor;
            ClampPanOffset();
            ApplyContentTransform();

            EventBus.Publish(new WorldMapZoomEvent
            {
                ZoomLevel = _currentZoom,
                FocusPoint = _panOffset
            });
        }

        /// <summary>Reset view to default zoom and center.</summary>
        public void ResetView()
        {
            _currentZoom = 1.0f;
            _panOffset = Vector2.zero;
            ApplyContentTransform();

            if (_zoomLevelText != null)
                _zoomLevelText.text = "100%";

            Debug.Log("[WorldMapUI] 视图已重置");
        }

        /// <summary>Clamp the pan offset so content doesn't go out of bounds.</summary>
        private void ClampPanOffset()
        {
            if (_mapContentArea == null) return;

            float contentWidth = _mapContentArea.rect.width * _currentZoom;
            float contentHeight = _mapContentArea.rect.height * _currentZoom;
            float viewWidth = _mapContentArea.parent.GetComponent<RectTransform>()?.rect.width ?? Screen.width;
            float viewHeight = _mapContentArea.parent.GetComponent<RectTransform>()?.rect.height ?? Screen.height;

            float maxPanX = Mathf.Max(0, (contentWidth - viewWidth) * 0.5f);
            float maxPanY = Mathf.Max(0, (contentHeight - viewHeight) * 0.5f);

            _panOffset.x = Mathf.Clamp(_panOffset.x, -maxPanX, maxPanX);
            _panOffset.y = Mathf.Clamp(_panOffset.y, -maxPanY, maxPanY);
        }

        /// <summary>Apply zoom+pan transform to the content area.</summary>
        private void ApplyContentTransform()
        {
            if (_mapContentArea == null) return;

            _mapContentArea.localScale = Vector3.one * _currentZoom;
            _mapContentArea.anchoredPosition = _panOffset;

            if (_zoomLevelText != null)
                _zoomLevelText.text = $"{_currentZoom * 100:F0}%";

            // Apply frustum culling on markers.
            if (_enableFrustumCulling)
            {
                CullOffscreenMarkers();
            }

            // Update fog overlay scale.
            UpdateFogOverlayTransform();
        }

        #endregion

        #region Marker Pool

        /// <summary>Initialize the marker object pool (INT-04).</summary>
        private void InitializePool()
        {
            if (_markerPrefab == null || _markerContainer == null)
            {
                Debug.LogWarning("[WorldMapUI] Marker prefab or container not set — pool init skipped");
                return;
            }

            for (int i = 0; i < _poolInitialSize; i++)
            {
                CreatePoolItem();
            }
        }

        private MapMarkerPoolItem CreatePoolItem()
        {
            GameObject go = Instantiate(_markerPrefab, _markerContainer);
            go.SetActive(false);

            var item = new MapMarkerPoolItem
            {
                GameObject = go,
                IconImage = go.GetComponentInChildren<Image>(),
                LabelText = go.GetComponentInChildren<Text>(),
                RectTransform = go.GetComponent<RectTransform>(),
                IsActive = false
            };

            _markerPool.Add(item);
            return item;
        }

        /// <summary>Get a marker from the pool.</summary>
        private MapMarkerPoolItem GetPoolItem()
        {
            // Find inactive item.
            foreach (var item in _markerPool)
            {
                if (!item.IsActive)
                {
                    item.IsActive = true;
                    item.GameObject.SetActive(true);
                    return item;
                }
            }

            // Pool exhausted — create new if under max.
            if (_markerPool.Count < _poolMaxSize)
            {
                var item = CreatePoolItem();
                item.IsActive = true;
                item.GameObject.SetActive(true);
                return item;
            }

            // Reuse the oldest active marker (overflow).
            if (_activeMarkers.Count > 0)
            {
                var reused = _activeMarkers[0];
                _activeMarkers.RemoveAt(0);
                reused.IsActive = true;
                return reused;
            }

            return null;
        }

        /// <summary>Return a marker to the pool.</summary>
        private void ReturnPoolItem(MapMarkerPoolItem item)
        {
            if (item == null) return;
            item.IsActive = false;
            item.GameObject.SetActive(false);
        }

        /// <summary>Cull markers outside the visible rect (INT-04).</summary>
        private void CullOffscreenMarkers()
        {
            if (_mapContentArea == null) return;

            Rect viewRect = GetViewportRect();

            foreach (var marker in _activeMarkers)
            {
                if (marker.GameObject == null) continue;

                Vector3 screenPos = _mapContentArea.TransformPoint(
                    marker.RectTransform.localPosition);

                bool isVisible = viewRect.Contains(
                    new Vector2(screenPos.x, screenPos.y));

                bool shouldShow = isVisible && marker.IsActive;

                if (marker.GameObject.activeSelf != shouldShow)
                {
                    marker.GameObject.SetActive(shouldShow);
                }
            }
        }

        /// <summary>Get the current viewport rect in screen coordinates.</summary>
        private Rect GetViewportRect()
        {
            RectTransform parentRect = _mapContentArea.parent.GetComponent<RectTransform>();
            if (parentRect == null)
                return new Rect(0, 0, Screen.width, Screen.height);

            Vector3[] corners = new Vector3[4];
            parentRect.GetWorldCorners(corners);

            float margin = _cullingMargin;
            Vector2 min = new Vector2(
                Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x) - margin,
                Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y) - margin);
            Vector2 max = new Vector2(
                Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x) + margin,
                Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y) + margin);

            return new Rect(min, max - min);
        }

        #endregion

        #region Region UI

        /// <summary>Initialize region overlay boxes with faction colors (INT-01).</summary>
        private void InitializeRegionUI()
        {
            if (_regionContainer == null || _regionDefinitions == null) return;

            foreach (var region in _regionDefinitions)
            {
                if (region == null) continue;

                GameObject regionGO = new GameObject($"Region_{region.RegionId}");
                regionGO.transform.SetParent(_regionContainer, false);

                RectTransform rt = regionGO.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = region.RegionBounds.size * _mapContentArea.rect.size;
                rt.anchoredPosition = region.RegionBounds.position * _mapContentArea.rect.size;

                // Add image with faction color.
                Image img = regionGO.AddComponent<Image>();
                img.color = region.FactionColor;
                img.raycastTarget = false;

                // Add faction name label.
                GameObject labelGO = new GameObject($"Label_{region.RegionId}");
                labelGO.transform.SetParent(regionGO.transform, false);
                RectTransform labelRt = labelGO.AddComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0.5f, 0.5f);
                labelRt.anchorMax = new Vector2(0.5f, 0.5f);
                labelRt.pivot = new Vector2(0.5f, 0.5f);
                labelRt.sizeDelta = new Vector2(200, 30);
                labelRt.anchoredPosition = Vector2.zero;

                Text label = labelGO.AddComponent<Text>();
                label.text = region.DisplayName;
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 16;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.raycastTarget = false;

                _regionUIObjects[region.RegionId] = regionGO;
            }
        }

        /// <summary>Update region info text on hover (INT-02, INT-03).</summary>
        private void UpdateRegionInfoUnderCursor()
        {
            if (_regionInfoText == null) return;

            Vector2 normalizedPos = ScreenToNormalizedPosition(Input.mousePosition);

            foreach (var region in _regionDefinitions)
            {
                if (region == null) continue;

                if (region.RegionBounds.Contains(normalizedPos))
                {
                    string resources = region.ResourceTypes != null
                        ? string.Join(", ", region.ResourceTypes)
                        : "无";

                    _regionInfoText.text = $"区域: {region.DisplayName}\n" +
                                           $"势力: {region.FactionId}\n" +
                                           $"推荐境界: {region.RecommendedRealm}\n" +
                                           $"资源: {resources}";
                    return;
                }
            }

            _regionInfoText.text = "世界地图 — 鼠标悬停查看区域信息";
        }

        #endregion

        #region Fog Overlay (未探索遮罩)

        /// <summary>Initialize dark overlay for unexplored areas.</summary>
        private void InitializeFogOverlay()
        {
            if (_fogContainer == null || _regionDefinitions == null) return;

            foreach (var region in _regionDefinitions)
            {
                if (region == null || !region.RequiresExploration) continue;

                GameObject fogGO = new GameObject($"Fog_{region.RegionId}");
                fogGO.transform.SetParent(_fogContainer, false);

                RectTransform rt = fogGO.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = region.RegionBounds.size * _mapContentArea.rect.size;
                rt.anchoredPosition = region.RegionBounds.position * _mapContentArea.rect.size;

                Image fogImage = fogGO.AddComponent<Image>();
                fogImage.color = new Color(0, 0, 0, EXPLORATION_DARK_ALPHA);
                fogImage.raycastTarget = false;

                _fogUIObjects[region.RegionId] = fogGO;
            }
        }

        /// <summary>Refresh fog overlay visibility based on exploration state.</summary>
        public void RefreshFogOverlay()
        {
            if (_fogContainer == null) return;

            foreach (var region in _regionDefinitions)
            {
                if (region == null || !region.RequiresExploration) continue;

                if (_fogUIObjects.TryGetValue(region.RegionId, out var fogGO))
                {
                    float alpha = region.ExplorationPercent >= 100
                        ? 0f : EXPLORATION_DARK_ALPHA * (1f - region.ExplorationPercent / 100f);

                    Image fogImage = fogGO.GetComponent<Image>();
                    if (fogImage != null)
                    {
                        Color c = fogImage.color;
                        c.a = alpha;
                        fogImage.color = c;
                    }
                }
            }
        }

        /// <summary>Sync fog overlay transform with zoom/pan.</summary>
        private void UpdateFogOverlayTransform()
        {
            // Fog container scales with content.
            if (_fogContainer != null)
            {
                _fogContainer.localScale = Vector3.one;
            }
        }

        #endregion

        #region Markers

        /// <summary>Build a lookup map of marker data.</summary>
        private void BuildMarkerDataMap()
        {
            _markerDataMap.Clear();
            if (_markerDefinitions == null) return;

            foreach (var marker in _markerDefinitions)
            {
                if (marker != null)
                    _markerDataMap[marker.MarkerId] = marker;
            }
        }

        /// <summary>Initialize all static markers.</summary>
        private void InitializeMarkers()
        {
            foreach (var kvp in _markerDataMap)
            {
                CreateOrUpdateMarker(kvp.Value);
            }
        }

        /// <summary>Create or update a marker on the map.</summary>
        private void CreateOrUpdateMarker(WorldMapMarkerData data)
        {
            if (!data.IsDiscovered) return;

            MapMarkerPoolItem item = GetPoolItem();
            if (item == null) return;

            // Position in normalized coordinates.
            Vector2 anchoredPos = new Vector2(
                data.NormalizedPosition.x * _mapContentArea.rect.width,
                data.NormalizedPosition.y * _mapContentArea.rect.height);

            item.RectTransform.anchorMin = Vector2.zero;
            item.RectTransform.anchorMax = Vector2.zero;
            item.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            item.RectTransform.anchoredPosition = anchoredPos;
            item.RectTransform.localScale = Vector3.one * data.MarkerScale;
            item.MarkerType = data.MarkerType;

            // Set icon.
            if (item.IconImage != null)
            {
                item.IconImage.sprite = GetMarkerIcon(data.MarkerType);
                item.IconImage.color = data.MarkerColor;
            }

            // Set label.
            if (item.LabelText != null)
            {
                item.LabelText.text = data.DisplayName;
                if (!string.IsNullOrEmpty(data.SubText))
                {
                    item.LabelText.text += $"\n<size=10>{data.SubText}</size>";
                }
            }

            // Store marker ID on the GameObject for click detection.
            item.GameObject.name = $"Marker_{data.MarkerId}";

            if (!_activeMarkers.Contains(item))
                _activeMarkers.Add(item);
        }

        /// <summary>Find marker data by the associated GameObject.</summary>
        private WorldMapMarkerData FindMarkerDataByGameObject(GameObject go)
        {
            if (go == null) return null;
            string markerId = go.name.Replace("Marker_", "");
            return _markerDataMap.TryGetValue(markerId, out var data) ? data : null;
        }

        #endregion

        #region Player Marker

        /// <summary>Initialize the player position marker.</summary>
        private void InitializePlayerMarker()
        {
            if (_markerPrefab == null || _markerContainer == null)
            {
                CreateSimplePlayerMarker();
                return;
            }

            GameObject go = Instantiate(_markerPrefab, _markerContainer);
            go.name = "PlayerMarker";

            _playerMarkerItem = new MapMarkerPoolItem
            {
                GameObject = go,
                IconImage = go.GetComponentInChildren<Image>(),
                LabelText = go.GetComponentInChildren<Text>(),
                RectTransform = go.GetComponent<RectTransform>(),
                IsActive = true
            };

            if (_playerMarkerItem.IconImage != null && _playerIcon != null)
                _playerMarkerItem.IconImage.sprite = _playerIcon;

            if (_playerMarkerItem.LabelText != null)
                _playerMarkerItem.LabelText.text = "你";

            go.SetActive(true);
            _playerMarkerGameObject = go;
        }

        /// <summary>Fallback if no marker prefab.</summary>
        private void CreateSimplePlayerMarker()
        {
            _playerMarkerGameObject = new GameObject("PlayerMarker");
            _playerMarkerGameObject.transform.SetParent(_markerContainer, false);

            var rt = _playerMarkerGameObject.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(16, 16);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            var img = _playerMarkerGameObject.AddComponent<Image>();
            img.sprite = _playerIcon ?? _defaultMarkerIcon;
            img.raycastTarget = false;

            _playerMarkerItem = new MapMarkerPoolItem
            {
                GameObject = _playerMarkerGameObject,
                IconImage = img,
                RectTransform = rt,
                IsActive = true
            };
        }

        /// <summary>Update the player marker position on the map.</summary>
        private void UpdatePlayerMarkerPosition()
        {
            if (_playerTransform == null)
            {
                GameObject playerGO = GameObject.FindGameObjectWithTag(PLAYER_TAG);
                if (playerGO != null)
                    _playerTransform = playerGO.transform;
                return;
            }

            _playerMarkerTimer -= Time.deltaTime;
            if (_playerMarkerTimer > 0f) return;
            _playerMarkerTimer = PLAYER_MARKER_UPDATE_INTERVAL;

            Vector2 normalizedPos = WorldToNormalizedPosition(_playerTransform.position);

            if (_playerMarkerItem?.RectTransform != null)
            {
                _playerMarkerItem.RectTransform.anchoredPosition = new Vector2(
                    normalizedPos.x * _mapContentArea.rect.width,
                    normalizedPos.y * _mapContentArea.rect.height);
            }

            // Update player position text.
            if (_playerPositionText != null)
            {
                string region = FindRegionAtPosition(normalizedPos);
                _playerPositionText.text = $"坐标: ({_playerTransform.position.x:F0}, {_playerTransform.position.z:F0}) | {region}";
            }
        }

        /// <summary>Find which region contains a normalized position.</summary>
        private string FindRegionAtPosition(Vector2 normalizedPos)
        {
            if (_regionDefinitions == null) return "未知区域";

            foreach (var region in _regionDefinitions)
            {
                if (region != null && region.RegionBounds.Contains(normalizedPos))
                    return region.DisplayName;
            }

            return "未知区域";
        }

        #endregion

        #region Coordinate Helpers

        /// <summary>Convert world position to map-normalized position (0~1).</summary>
        public Vector2 WorldToNormalizedPosition(Vector3 worldPosition)
        {
            // This assumes a known world bounds mapping.
            // For EarthOnline, the world is assumed to map to a ~1024x1024 unit area.
            float worldSizeX = 1024f;
            float worldSizeZ = 1024f;
            float worldOffsetX = 512f;
            float worldOffsetZ = 512f;

            return new Vector2(
                Mathf.Clamp01((worldPosition.x + worldOffsetX) / worldSizeX),
                Mathf.Clamp01((worldPosition.z + worldOffsetZ) / worldSizeZ)
            );
        }

        /// <summary>Convert screen position to map-normalized position.</summary>
        private Vector2 ScreenToNormalizedPosition(Vector2 screenPos)
        {
            if (_mapContentArea == null) return Vector2.zero;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _mapContentArea, screenPos, _uiCamera, out localPoint);

            return new Vector2(
                Mathf.Clamp01(localPoint.x / _mapContentArea.rect.width),
                Mathf.Clamp01(localPoint.y / _mapContentArea.rect.height)
            );
        }

        /// <summary>Resolve region ID from a world position.</summary>
        private string ResolveRegionId(Vector3 worldPosition)
        {
            Vector2 norm = WorldToNormalizedPosition(worldPosition);
            foreach (var region in _regionDefinitions)
            {
                if (region != null && region.RegionBounds.Contains(norm))
                    return region.RegionId;
            }
            return "unknown";
        }

        #endregion

        #region Icon Mapping

        /// <summary>Get the appropriate icon sprite for a marker type.</summary>
        private Sprite GetMarkerIcon(MapMarkerType type)
        {
            return type switch
            {
                MapMarkerType.FastTravel      => _fastTravelIcon ?? _defaultMarkerIcon,
                MapMarkerType.DungeonEntrance => _dungeonIcon ?? _defaultMarkerIcon,
                MapMarkerType.Landmark        => _landmarkIcon ?? _defaultMarkerIcon,
                MapMarkerType.POI             => _landmarkIcon ?? _defaultMarkerIcon,
                MapMarkerType.Resource        => _resourceIcon ?? _defaultMarkerIcon,
                MapMarkerType.Player          => _playerIcon ?? _defaultMarkerIcon,
                MapMarkerType.Quest           => _questIcon ?? _defaultMarkerIcon,
                _                             => _defaultMarkerIcon
            };
        }

        /// <summary>Convert DiscoveryType to MapMarkerType.</summary>
        private static MapMarkerType DiscoveryTypeToMarkerType(DiscoveryType dt)
        {
            return dt switch
            {
                DiscoveryType.Landmark  => MapMarkerType.Landmark,
                DiscoveryType.Hidden    => MapMarkerType.POI,
                DiscoveryType.Dungeon   => MapMarkerType.DungeonEntrance,
                _                       => MapMarkerType.POI
            };
        }

        #endregion

        #region Display Mode

        /// <summary>Switch the map display filter mode.</summary>
        public void SetDisplayMode(MapDisplayMode mode)
        {
            _currentDisplayMode = mode;
            RefreshMarkerVisibility();
        }

        /// <summary>Toggle marker visibility based on current display mode.</summary>
        private void RefreshMarkerVisibility()
        {
            foreach (var marker in _activeMarkers)
            {
                if (marker.GameObject == null) continue;

                bool visible = _currentDisplayMode switch
                {
                    MapDisplayMode.Full => true,
                    MapDisplayMode.FactionOnly => true, // region colors always show
                    MapDisplayMode.ResourcesOnly => marker.MarkerType == MapMarkerType.Resource,
                    MapDisplayMode.QuestOnly => marker.MarkerType == MapMarkerType.Quest,
                    _ => true
                };

                marker.GameObject.SetActive(visible && marker.IsActive);
            }
        }

        #endregion

        #region External Data Binding

        /// <summary>Set region exploration percentage from external system.</summary>
        public void SetRegionExploration(string regionId, int percent)
        {
            foreach (var region in _regionDefinitions)
            {
                if (region != null && region.RegionId == regionId)
                {
                    region.ExplorationPercent = Mathf.Clamp(percent, 0, 100);
                    RefreshFogOverlay();
                    return;
                }
            }
        }

        /// <summary>Update faction color for a region dynamically.</summary>
        public void SetRegionFactionColor(string regionId, Color factionColor, string factionId)
        {
            foreach (var region in _regionDefinitions)
            {
                if (region != null && region.RegionId == regionId)
                {
                    region.FactionColor = factionColor;
                    region.FactionId = factionId;

                    if (_regionUIObjects.TryGetValue(regionId, out var go))
                    {
                        Image img = go.GetComponent<Image>();
                        if (img != null) img.color = factionColor;
                    }
                    return;
                }
            }
        }

        /// <summary>Add or update a marker at runtime.</summary>
        public void AddMarker(WorldMapMarkerData markerData)
        {
            if (markerData == null) return;
            _markerDataMap[markerData.MarkerId] = markerData;
            CreateOrUpdateMarker(markerData);
        }

        /// <summary>Remove a marker from the map.</summary>
        public void RemoveMarker(string markerId)
        {
            _markerDataMap.Remove(markerId);

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];
                if (marker.GameObject != null &&
                    marker.GameObject.name == $"Marker_{markerId}")
                {
                    ReturnPoolItem(marker);
                    _activeMarkers.RemoveAt(i);
                    return;
                }
            }
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== WorldMapUI Status ===");
            sb.AppendLine($"Open: {_isOpen}");
            sb.AppendLine($"Zoom: {_currentZoom:F2}x");
            sb.AppendLine($"Pan: ({_panOffset.x:F0}, {_panOffset.y:F0})");
            sb.AppendLine($"Markers Active: {_activeMarkers.Count}");
            sb.AppendLine($"Pool: {_markerPool.Count} items ({POOL_INITIAL_SIZE}/{_poolMaxSize})");
            sb.AppendLine($"Regions: {_regionDefinitions?.Length ?? 0}");
            sb.AppendLine($"Fog Overlays: {_fogUIObjects.Count}");
            sb.AppendLine($"Display Mode: {_currentDisplayMode}");
            return sb.ToString();
        }

        #endregion
    }
}
