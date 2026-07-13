using UnityEngine;
using UnityEngine.Events;

namespace StackingCute
{
    public enum GameState { Menu, Playing, Paused, Over }

    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        [Header("State")]
        [SerializeField] private GameState _currentState = GameState.Menu;

        [Header("Testing")]
        [SerializeField] private bool _autoStart = true;

        [Header("Debug")]
        [SerializeField] private bool _showDebugOverlay = true;

        [Header("Level")]
        [SerializeField] private LevelConfig _currentLevel;

        [System.Serializable]
        public class GameStateEvent : UnityEvent<GameState, GameState> { }
        public GameStateEvent OnStateChanged;

        public int CurrentLayer { get; set; }
        public int CurrentCombo { get; set; }
        public int CurrentScore { get; set; }
        public int CurrentGold { get; set; }
        public float CurrentTowerWidth { get; set; } = 1f;
        public int BestRecord { get; set; }
        public float LastOverlapRatio { get; set; }
        public int FrameCount { get; private set; }

        public GameState CurrentState => _currentState;
        public LevelConfig CurrentLevelConfig => _currentLevel;

        private const string BEST_RECORD_KEY = "BestRecord";

        private void Awake()
        {
            if (_instance == null) { _instance = this; }
            else if (_instance != this) { Destroy(gameObject); return; }
            BestRecord = PlayerPrefs.GetInt(BEST_RECORD_KEY, 0);
        }

        private void Start()
        {
            if (_autoStart && _currentLevel != null)
                StartGame(_currentLevel);
        }

        private void Update()
        {
            FrameCount++;
            if (Input.GetKeyDown(KeyCode.R)) RestartGame();
            if (Input.GetKeyDown(KeyCode.Space) && _currentState == GameState.Menu && _currentLevel != null)
                StartGame(_currentLevel);
        }

        public void StartGame(LevelConfig config)
        {
            _currentLevel = config;
            CurrentLayer = 0; CurrentCombo = 0; CurrentScore = 0; CurrentGold = 0; CurrentTowerWidth = 1f;
            SetState(GameState.Playing);
            var tm = FindObjectOfType<TowerManager>();
            if (tm != null) tm.SpawnFirstBlock();
        }

        public void RestartGame()
        {
            var config = _currentLevel;
            var tm = FindObjectOfType<TowerManager>();
            if (tm != null) tm.ClearTower();
            CurrentLayer = 0; CurrentCombo = 0; CurrentScore = 0; CurrentGold = 0; CurrentTowerWidth = 1f;
            if (config != null) StartGame(config);
            else SetState(GameState.Menu);
        }

        public void NotifyOverlap(float overlapRatio)
        {
            LastOverlapRatio = overlapRatio;
        }

        public void GameOver()
        {
            SetState(GameState.Over);
            if (CurrentLayer > BestRecord)
            {
                BestRecord = CurrentLayer;
                PlayerPrefs.SetInt(BEST_RECORD_KEY, BestRecord);
                PlayerPrefs.Save();
            }
        }

        public void SetState(GameState newState)
        {
            if (_currentState == newState) return;
            var old = _currentState;
            _currentState = newState;
            OnStateChanged?.Invoke(old, newState);
        }

        public void AddScore(int baseScore, int comboBonus, int gold)
        {
            CurrentScore += baseScore + comboBonus;
            CurrentGold += gold;
        }

        private void OnGUI()
        {
            if (!_showDebugOverlay) return;
            if (_currentState != GameState.Playing && _currentState != GameState.Over) return;
            int y = 10;
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, y, 400, 24), "Layer: " + CurrentLayer + "  Score: " + CurrentScore + "  Gold: " + CurrentGold, style);
            y += 22;
            GUI.Label(new Rect(10, y, 400, 24), "Combo: x" + CurrentCombo + "  Width: " + CurrentTowerWidth.ToString("F2") + "  Best: " + BestRecord, style);
            y += 22;
            GUI.Label(new Rect(10, y, 400, 24), "Frame: " + FrameCount + "  State: " + _currentState, style);
        }
    }
}