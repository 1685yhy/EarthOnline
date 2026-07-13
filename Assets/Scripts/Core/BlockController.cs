using UnityEngine;

namespace StackingCute
{
    public class BlockController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private float _moveRange = 3f;
        [SerializeField] private float _flyDuration = 0.25f;
        [Header("References")]
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private DebrisEffect _debrisEffect;
        [SerializeField] private PerfectEffect _perfectEffect;
        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _blockColor = new Color(1f, 0.62f, 0.68f, 1f);

        private enum BlockState { Moving, Flying, Landed }
        private BlockState _state = BlockState.Moving;
        private int _moveDirection = 1;
        private float _blockWidth = 1f;
        private float _lastTapTime;
        private const float TAP_COOLDOWN = 0.2f;
        private Vector3 _flyStart, _flyTarget;
        private float _flyElapsed;
        private TowerManager _tower;

        public float BlockWidth => _blockWidth;
        public float LastOverlapRatio { get; private set; }

        private void Awake()
        {
            if (_gameManager == null) _gameManager = FindObjectOfType<GameManager>();
            if (_debrisEffect == null) _debrisEffect = GetComponent<DebrisEffect>();
            if (_perfectEffect == null) _perfectEffect = GetComponent<PerfectEffect>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null) _spriteRenderer.color = _blockColor;
        }

        private void Start()
        {
            _tower = FindObjectOfType<TowerManager>();
            InvokeRepeating(nameof(Tick), 0f, 0.016f);
        }

        public void Tick()
        {
            if (_gameManager == null || _gameManager.CurrentState != GameState.Playing) return;
            if (_state == BlockState.Moving) { MoveBlock(); CheckInput(); }
            else if (_state == BlockState.Flying) FlyBlock();
        }

        private void MoveBlock()
        {
            float speed = _moveSpeed, range = _moveRange;
            var cfg = _gameManager.CurrentLevelConfig;
            if (cfg != null) { speed = cfg.GetSpeedForLayer(_gameManager.CurrentLayer + 1); range = cfg.GetRangeForLayer(_gameManager.CurrentLayer + 1); }
            float newX = transform.position.x + _moveDirection * speed * Time.deltaTime;
            if (Mathf.Abs(newX) > range) { newX = Mathf.Sign(newX) * range; _moveDirection *= -1; }
            if (cfg != null && _gameManager.CurrentLayer + 1 > cfg.ReverseAfterLayer && Random.value < cfg.ReverseChancePerLayer) _moveDirection *= -1;
            if (_spriteRenderer != null)
            {
                bool cd = Time.time - _lastTapTime < TAP_COOLDOWN;
                float f = cd ? Mathf.PingPong(Time.time * 20f, 0.25f) : 0f;
                _spriteRenderer.color = cd ? new Color(_blockColor.r, Mathf.Max(0, _blockColor.g - f), Mathf.Max(0, _blockColor.b - f), 1f) : _blockColor;
            }
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        }

        private void CheckInput()
        {
            bool t = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!t || Time.time - _lastTapTime < TAP_COOLDOWN) return;
            _lastTapTime = Time.time; StartFlight();
        }

        private void StartFlight()
        {
            _state = BlockState.Flying; _flyStart = transform.position;
            float ty = _tower != null && _tower.LayerCount > 0 ? _tower.GetNextLayerPosition().y : 0.15f;
            _flyTarget = new Vector3(transform.position.x, ty, 0);
            _flyElapsed = 0f; if (_spriteRenderer != null) _spriteRenderer.color = _blockColor;
        }

        private void FlyBlock()
        {
            _flyElapsed += Time.deltaTime; float t = Mathf.Clamp01(_flyElapsed / _flyDuration);
            float e = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(_flyStart, _flyTarget, e);
            transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 0.5f)) * _blockWidth;
            if (t >= 1f) LandBlock();
        }

        private void LandBlock()
        {
            _state = BlockState.Landed; transform.position = _flyTarget;
            transform.localScale = Vector3.one * _blockWidth;
            if (_gameManager == null) return;
            float tw = _gameManager.CurrentTowerWidth, offset = Mathf.Abs(transform.position.x);
            float hs = _blockWidth / 2f + tw / 2f;
            float ow = Mathf.Max(0, Mathf.Min(hs - offset, tw));
            float or = _blockWidth > 0.001f ? ow / _blockWidth : 0f;
            LastOverlapRatio = or; _gameManager.NotifyOverlap(or);
            float ew = _blockWidth - ow;
            if (ew > 0.01f && _debrisEffect != null) _debrisEffect.SpawnDebris(new Vector3(transform.position.x + Mathf.Sign(transform.position.x) * ow * 0.5f, transform.position.y, 0), ew, _blockWidth);
            if (or < 0.05f) { _gameManager.SetState(GameState.Over); return; }
            _gameManager.CurrentLayer++; _gameManager.CurrentTowerWidth = ow; _gameManager.CurrentGold += 1;
            if (or >= 0.9f) { _gameManager.CurrentCombo = Mathf.Min(_gameManager.CurrentCombo + 1, 5); int b = 3 * _gameManager.CurrentCombo; _gameManager.CurrentScore += 1 + b; _gameManager.CurrentGold += 5; if (_perfectEffect != null) _perfectEffect.PlayPerfect(_gameManager.CurrentCombo); }
            else { _gameManager.CurrentCombo = 0; _gameManager.CurrentScore += 1; }
            if (_tower != null) _tower.AddLayer(_flyTarget, ow);
            if (_gameManager.CurrentLevelConfig != null && _gameManager.CurrentLayer >= _gameManager.CurrentLevelConfig.TargetLayers) { _gameManager.CurrentScore += _gameManager.CurrentLevelConfig.LevelId * 10; _gameManager.SetState(GameState.Over); return; }
            if (_tower != null) _tower.SpawnNextBlock(ow);
        }

        public void Initialize(float width, float speed, float range, float yPosition)
        {
            _blockWidth = width; _moveSpeed = speed; _moveRange = range;
            _state = BlockState.Moving; _flyElapsed = 0f; LastOverlapRatio = 0f;
            _moveDirection = Random.value > 0.5f ? 1 : -1;
            transform.position = new Vector3(0, yPosition, 0);
            transform.localScale = new Vector3(_blockWidth, _blockWidth, 1f);
            if (_spriteRenderer != null) _spriteRenderer.color = _blockColor;
        }
    }
}