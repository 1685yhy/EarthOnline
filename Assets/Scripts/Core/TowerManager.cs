using UnityEngine;
using System.Collections.Generic;

namespace StackingCute
{
    public class TowerManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager _gameManager;
        [Header("Tower Settings")]
        [SerializeField] private float _layerHeight = 0.15f;
        [SerializeField] private float _baseY = 0f;
        [Header("Block Spawn")]
        [SerializeField] private float _spawnOffsetY = -3f;
        [Header("Camera")]
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private float _cameraSmoothTime = 0.3f;
        [SerializeField] private float _cameraLookAhead = 3f;

        private List<TowerLayer> _layers = new List<TowerLayer>();
        private float _cameraVelocity;
        private struct TowerLayer { public GameObject gameObject; public float width; public float yPosition; }
        public int LayerCount => _layers.Count;
        public float TopY => _layers.Count > 0 ? _layers[_layers.Count - 1].yPosition : _baseY;

        private void Awake()
        {
            if (_gameManager == null) _gameManager = FindObjectOfType<GameManager>();
            if (_mainCamera == null) _mainCamera = Camera.main;
        }

        public Vector3 GetNextLayerPosition() { return new Vector3(0, TopY + _layerHeight, 0); }

        public void AddLayer(Vector3 position, float width)
        {
            var obj = new GameObject(string.Format("Layer_{0:D3}", _layers.Count));
            obj.transform.SetParent(transform); obj.transform.position = position;
            var sr = obj.AddComponent<SpriteRenderer>();
            int s = 64; var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var p = new Color[s * s]; for (int i = 0; i < p.Length; i++) p[i] = Color.white;
            t.SetPixels(p); t.Apply(); t.filterMode = FilterMode.Point;
            sr.sprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            sr.color = Color.HSVToRGB((_layers.Count * 0.07f) % 1f, 0.25f, 0.92f);
            sr.sortingOrder = _layers.Count;
            float sz = sr.sprite.bounds.size.x;
            obj.transform.localScale = sz > 0.001f ? new Vector3(width / sz, _layerHeight / sz, 1f) : new Vector3(width, _layerHeight, 1f);
            _layers.Add(new TowerLayer { gameObject = obj, width = width, yPosition = position.y });
        }

        public void SpawnNextBlock(float width)
        {
            var block = FindObjectOfType<BlockController>();
            if (block == null) return;
            var cfg = _gameManager?.CurrentLevelConfig;
            float w = cfg != null && cfg.ShouldSpawnNarrow(_gameManager.CurrentLayer) ? width * cfg.NarrowWidthRatio : width;
            float spd = cfg != null ? cfg.GetSpeedForLayer(_gameManager.CurrentLayer + 1) : 2f;
            float rng = cfg != null ? cfg.GetRangeForLayer(_gameManager.CurrentLayer + 1) : 3f;
            float y = _mainCamera != null ? _mainCamera.transform.position.y - _mainCamera.orthographicSize + 1f : _baseY + _spawnOffsetY;
            block.Initialize(w, spd, rng, y);
        }

        public void SpawnFirstBlock()
        {
            var block = FindObjectOfType<BlockController>();
            if (block == null) return;
            var cfg = _gameManager?.CurrentLevelConfig;
            float spd = cfg != null ? cfg.GetSpeedForLayer(1) : 1f;
            float rng = cfg != null ? cfg.GetRangeForLayer(1) : 3f;
            float y = _mainCamera != null ? _mainCamera.transform.position.y - _mainCamera.orthographicSize + 1f : _baseY + _spawnOffsetY;
            block.Initialize(1f, spd, rng, y);
        }

        private void LateUpdate()
        {
            if (_mainCamera == null || _layers.Count == 0) return;
            float ty = TopY + _cameraLookAhead;
            float ny = Mathf.SmoothDamp(_mainCamera.transform.position.y, ty, ref _cameraVelocity, _cameraSmoothTime);
            _mainCamera.transform.position = new Vector3(0, ny, _mainCamera.transform.position.z);
        }

        public void ClearTower() { foreach (var l in _layers) if (l.gameObject != null) Destroy(l.gameObject); _layers.Clear(); }
    }
}