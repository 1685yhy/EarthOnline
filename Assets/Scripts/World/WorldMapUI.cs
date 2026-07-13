using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EarthOnline.World
{
    /// <summary>
    /// World map overlay UI.
    ///
    /// - M key toggles the full-screen world map.
    /// - Reads explored cells from FogOfWar.Instance and renders them onto
    ///   a Texture2D displayed via a RawImage on a screen-space overlay canvas.
    /// - Mouse wheel zooms in/out. Click-drag pans the view.
    /// - Uses the string-based EventBus API only (no typed structs).
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        #region Singleton

        public static WorldMapUI Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Controls")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.M;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 0.5f;
        [SerializeField] private float _minZoom = 0.3f;
        [SerializeField] private float _maxZoom = 4f;
        [SerializeField] private float _defaultZoom = 1f;

        [Header("Map Texture")]
        [SerializeField] private int _textureWidth = 1024;
        [SerializeField] private int _textureHeight = 1024;
        [SerializeField] private int _cellPixelSize = 4;

        [Header("Colors")]
        [SerializeField] private Color _backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        [SerializeField] private Color _unexploredColor = new Color(0.15f, 0.15f, 0.16f, 1f);
        [SerializeField] private Color _lightlyExploredColor = new Color(0.35f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color _deepExploredColor = new Color(0.2f, 0.8f, 0.2f, 1f);

        [Header("World Grid")]
        [SerializeField] private int _worldCellsX = 300;
        [SerializeField] private int _worldCellsY = 300;

        #endregion

        #region Private State

        private Canvas _canvas;
        private GameObject _canvasGo;
        private RawImage _mapImage;
        private RectTransform _mapRt;
        private GameObject _mapImageGo;
        private Texture2D _mapTexture;
        private Color[] _pixelBuffer;

        private bool _isOpen;
        private float _currentZoom;
        private Vector2 _panOffset;

        // Drag state.
        private bool _isDragging;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartOffset;

        // Dirty tracking for lazy redraw.
        private int _lastExploredCount;
        private bool _needsRedraw;

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

            _currentZoom = _defaultZoom;
            _panOffset = Vector2.zero;
            _isOpen = false;
            _needsRedraw = true;
            _lastExploredCount = 0;
        }

        private void Start()
        {
            CreateMapUI();
            SetActive(false);
        }

        private void Update()
        {
            // Toggle on M key.
            if (Input.GetKeyDown(_toggleKey))
            {
                ToggleMap();
            }

            if (!_isOpen) return;

            // Zoom with mouse wheel.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _currentZoom = Mathf.Clamp(
                    _currentZoom + scroll * _zoomSpeed,
                    _minZoom,
                    _maxZoom
                );
                ApplyTransform();
            }

            // Drag to pan.
            HandleDrag();

            // Lazy redraw when fog has changed.
            if (FogOfWar.Instance != null)
            {
                int currentCount = FogOfWar.Instance.ExploredCellCount;
                if (currentCount != _lastExploredCount)
                {
                    _lastExploredCount = currentCount;
                    _needsRedraw = true;
                }
            }

            if (_needsRedraw)
            {
                RenderMap();
                _needsRedraw = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_mapTexture != null)
            {
                Destroy(_mapTexture);
                _mapTexture = null;
            }

            if (_canvasGo != null)
            {
                Destroy(_canvasGo);
                _canvasGo = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>Open the world map overlay.</summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _needsRedraw = true;

            if (FogOfWar.Instance != null)
                _lastExploredCount = FogOfWar.Instance.ExploredCellCount;

            _currentZoom = _defaultZoom;
            _panOffset = Vector2.zero;

            SetActive(true);
            ApplyTransform();

            // Notify via string-based EventBus.
            EventBus.Publish("WorldMapToggled", new Dictionary<string, object>
            {
                { "isOpen", true }
            });
        }

        /// <summary>Close the world map overlay.</summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            SetActive(false);

            EventBus.Publish("WorldMapToggled", new Dictionary<string, object>
            {
                { "isOpen", false }
            });
        }

        /// <summary>Whether the world map is currently open.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Force a full redraw on next Update.</summary>
        public void Refresh()
        {
            _needsRedraw = true;
        }

        #endregion

        #region UI Creation

        private void CreateMapUI()
        {
            // Root canvas.
            _canvasGo = new GameObject("WorldMapCanvas");
            _canvasGo.transform.SetParent(transform, false);

            _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // above most UI

            // Add a CanvasScaler so coordinates are consistent.
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Blocking raycast panel (covers the full screen so clicks
            // don't pass through to the game).
            var blockerGo = new GameObject("Blocker");
            blockerGo.transform.SetParent(_canvasGo.transform, false);
            var blockerImg = blockerGo.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.65f);
            var blockerRt = blockerGo.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.sizeDelta = Vector2.zero;

            // Container that holds the map image — we scale/position this for zoom+pan.
            var containerGo = new GameObject("MapContainer");
            containerGo.transform.SetParent(_canvasGo.transform, false);
            var containerRt = containerGo.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            // Map image (RawImage with our texture).
            _mapImageGo = new GameObject("MapImage");
            _mapImageGo.transform.SetParent(containerGo.transform, false);
            _mapImage = _mapImageGo.AddComponent<RawImage>();
            _mapRt = _mapImageGo.GetComponent<RectTransform>();

            // Anchor to centre of container.
            _mapRt.anchorMin = new Vector2(0.5f, 0.5f);
            _mapRt.anchorMax = new Vector2(0.5f, 0.5f);
            _mapRt.pivot = new Vector2(0.5f, 0.5f);

            // Create the texture.
            _mapTexture = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false);
            _mapTexture.name = "WorldMapTexture";
            _mapTexture.filterMode = FilterMode.Point;
            _mapTexture.wrapMode = TextureWrapMode.Clamp;
            _mapImage.texture = _mapTexture;

            // Initial size (in canvas pixels) — before zoom scaling.
            // We want the map to fit nicely; use the smaller screen dimension as a guide.
            float baseSize = Mathf.Min(Screen.width, Screen.height) * 0.75f;
            _mapRt.sizeDelta = new Vector2(baseSize, baseSize);

            // Allocate the pixel buffer once (avoid per-frame allocs).
            _pixelBuffer = new Color[_textureWidth * _textureHeight];

            // Populate with a first render.
            RenderMap();
            ApplyTransform();
        }

        private void SetActive(bool active)
        {
            if (_canvasGo != null)
                _canvasGo.SetActive(active);
        }

        #endregion

        #region Map Rendering

        /// <summary>
        /// Render the fog-of-war state onto _mapTexture.
        /// Each fog cell is drawn as a _cellPixelSize x _cellPixelSize block.
        /// </summary>
        private void RenderMap()
        {
            // Fill background.
            for (int i = 0; i < _pixelBuffer.Length; i++)
                _pixelBuffer[i] = _backgroundColor;

            if (FogOfWar.Instance == null)
            {
                _mapTexture.SetPixels(_pixelBuffer);
                _mapTexture.Apply();
                return;
            }

            // Offset to centre the world in the texture.
            float offsetX = _worldCellsX * 0.5f;
            float offsetY = _worldCellsY * 0.5f;

            // Iterate over explored cells and draw them.
            IReadOnlyDictionary<Vector2Int, byte> cells = FogOfWar.Instance.ExploredCells;

            foreach (KeyValuePair<Vector2Int, byte> kvp in cells)
            {
                Vector2Int cell = kvp.Key;
                byte layer = kvp.Value;

                // Map cell coordinate -> texture pixel coordinate (centred).
                float nx = (cell.x + 0.5f - offsetX) / _worldCellsX + 0.5f;
                float ny = (cell.y + 0.5f - offsetY) / _worldCellsY + 0.5f;

                int px = Mathf.RoundToInt(nx * _textureWidth);
                int py = Mathf.RoundToInt(ny * _textureHeight);

                // Clamp to texture bounds.
                px = Mathf.Clamp(px, 0, _textureWidth - _cellPixelSize);
                py = Mathf.Clamp(py, 0, _textureHeight - _cellPixelSize);

                Color color = layer switch
                {
                    2 => _deepExploredColor,
                    1 => _lightlyExploredColor,
                    _ => _unexploredColor
                };

                DrawBlock(px, py, _cellPixelSize, _cellPixelSize, color);
            }

            _mapTexture.SetPixels(_pixelBuffer);
            _mapTexture.Apply();
        }

        /// <summary>Fill a block of pixels with a single color.</summary>
        private void DrawBlock(int startX, int startY, int width, int height, Color color)
        {
            for (int y = startY; y < startY + height && y < _textureHeight; y++)
            {
                int rowOffset = y * _textureWidth;
                for (int x = startX; x < startX + width && x < _textureWidth; x++)
                {
                    _pixelBuffer[rowOffset + x] = color;
                }
            }
        }

        #endregion

        #region Zoom & Pan

        private void ApplyTransform()
        {
            if (_mapRt == null) return;

            // Zoom: scale the image. Pan: offset its anchored position.
            float scale = _currentZoom;
            _mapRt.localScale = new Vector3(scale, scale, 1f);
            _mapRt.anchoredPosition = _panOffset;
        }

        private void HandleDrag()
        {
            // Start drag on left mouse button down.
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _dragStartMouse = Input.mousePosition;
                _dragStartOffset = _panOffset;
                return;
            }

            // End drag.
            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                return;
            }

            // Update drag.
            if (_isDragging)
            {
                Vector2 currentMouse = Input.mousePosition;
                Vector2 delta = currentMouse - _dragStartMouse;

                // Apply zoom factor so drag distance feels consistent at all zoom levels.
                float zoomFactor = Mathf.Max(0.1f, _currentZoom);
                _panOffset = _dragStartOffset + delta / zoomFactor;

                ApplyTransform();
            }
        }

        #endregion

        #region EventBus Helpers

        private void ToggleMap()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        #endregion
    }
}
