using UnityEngine;
using UnityEngine.Events;

public enum GameState { Idle, Playing, Dropping, GameOver, LevelComplete }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameConfigSO config;
    // Backing config SO (optional — inline defaults used when missing)
    public GameConfigSO Config
    {
        get
        {
            if (config == null)
            {
                config = Resources.Load<GameConfigSO>("GameConfig");
            }
            return config;
        }
    }

    /// <summary>
    /// Safe accessors with fallback defaults when Config SO is missing.
    /// </summary>
    public float BlockHeight => Config?.blockHeight ?? 1.2f;
    public float InitialBlockWidth => Config?.initialBlockWidth ?? 2.8f;

    public GameState CurrentState { get; private set; } = GameState.Idle;
    public UnityEvent<GameState, GameState> OnStateChanged;

    [Header("References")]
    [SerializeField] private BlockSpawner blockSpawner;
    [SerializeField] private StackManager stackManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private UIManager uiManager;

    public BlockSpawner BlockSpawner => blockSpawner;
    public StackManager StackManager => stackManager;

    private int currentScore = 0;
    private int targetScore = 0;
    private int currentLevelId = 1;
    private int currentCombo = 0;
    private int maxCombo = 0;
    private bool isPerfectLastPlacement = false;

    // Drop animation
    private float dropProgress = 0f;
    private float dropStartY = 0f;
    private const float DROP_SPEED = 7.8f;  // ~0.13 per frame at 60fps, matching H5

    // Interstitial ad throttling
    private static int gameOverCount = 0;

    // Landing zone indicator
    private GameObject landingZone;
    private SpriteRenderer landingZoneSr;

    public int CurrentScore => currentScore;
    public int TargetScore => targetScore;
    public int CurrentCombo => currentCombo;
    public int MaxCombo => maxCombo;
    public int CurrentLevelId => currentLevelId;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // Ensure levels are always fresh
        var lm = FindObjectOfType<LevelManager>();
        if (lm != null) lm.GenerateDefaultLevels();
        CreateLandingZone();
    }

    private void Start()
    {
        // Find UIManager dynamically if serialized reference is missing
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null) uiManager = UIManager.Instance;
        uiManager?.ShowHome();
    }

    private void Update()
    {
        // Drop animation
        if (CurrentState == GameState.Dropping)
        {
            dropProgress += Time.deltaTime * DROP_SPEED;
            if (dropProgress >= 1f)
            {
                dropProgress = 1f;
                CompleteDrop();
            }
            else
            {
                // Single-phase eOutBounce animation (H5 original)
                // Block flies directly upward from spawn position to stack top
                var cb = blockSpawner?.CurrentBlock;
                if (cb != null)
                {
                    float targetY = stackManager.GetTopY() + BlockHeight;
                    float currentY = Mathf.Lerp(dropStartY, targetY, EOutBounce(dropProgress));
                    Vector3 pos = cb.transform.position;
                    pos.y = currentY;
                    cb.transform.position = pos;
                }
            }
            return;
        }

        // BGM intensity: combine score progress with combo intensity
        if (CurrentState == GameState.Playing && targetScore > 0)
        {
            float scoreProgress = (float)currentScore / targetScore;
            float comboIntensity = Mathf.Clamp01(currentCombo / 10f);
            AudioManager.Instance?.SetBGMIntensity(Mathf.Max(scoreProgress, comboIntensity));
        }

        // Update landing zone glow indicator during playing
        if (CurrentState == GameState.Playing && blockSpawner?.CurrentBlock != null)
        {
            UpdateLandingZone();
        }
        else if (CurrentState != GameState.Playing && landingZoneSr != null)
        {
            // Hide landing zone when not playing
            landingZoneSr.color = new Color(0, 0, 0, 0);
        }

        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0
            && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            HandleTap();
        }
    }

    private void CompleteDrop()
    {
        var cb = blockSpawner?.CurrentBlock;
        if (cb != null)
        {
            float targetY = stackManager.GetTopY() + BlockHeight;
            Vector3 pos = cb.transform.position;
            pos.y = targetY;
            cb.transform.position = pos;
            blockSpawner.OnDropComplete();
        }
        dropProgress = 0f;
        if (CurrentState == GameState.Dropping)
            SetState(GameState.Playing);
    }

    /// <summary>
    /// H5 eOutBounce easing function.
    /// Produces a snappy upward bounce with no anticipation dip.
    /// </summary>
    private static float EOutBounce(float t)
    {
        float n1 = 7.5625f;
        float d1 = 2.75f;
        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    public void HandleTap()
    {
        switch (CurrentState)
        {
            case GameState.Idle:
                return;
            case GameState.Playing:
                // Play a quick click SFX
                AudioManager.Instance?.PlaySFX(SfxType.UIClick, 1.2f);
                dropStartY = blockSpawner?.CurrentBlock?.transform.position.y ?? 0f;
                dropProgress = 0f;
                SetState(GameState.Dropping);
                break;
            case GameState.GameOver:
            case GameState.LevelComplete:
                return;
        }
    }

    public void SetState(GameState newState)
    {
        if (newState == CurrentState) return;
        var old = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(old, newState);
    }

    public void StartLevel(int levelId, int target, float blockWidth, float speed, bool useInternalCurve, AnimationCurve speedCurve)
    {
        Time.timeScale = 1f;
        currentLevelId = levelId;
        targetScore = target;
        currentScore = 0;
        currentCombo = 0;
        maxCombo = 0;

        // Reset tools for new game
        ToolSystem.Instance?.ResetForNewGame();

        stackManager.ClearStack();
        blockSpawner.Configure(blockWidth, speed, useInternalCurve, speedCurve, target);
        blockSpawner.SpawnFirstBlock();

        // Show level name in HUD
        var lm = FindObjectOfType<LevelManager>();
        var lvl = lm?.GetLevel(levelId);
        uiManager?.HUD?.SetLevelName($"第{levelId}关 · {lvl?.levelName ?? ""}");

        SetState(GameState.Playing);
    }

    public void OnBlockPlaced(int score, bool isPerfect, float overlapWidth)
    {
        currentScore = score;

        if (isPerfect)
        {
            currentCombo++;
            if (currentCombo > maxCombo) maxCombo = currentCombo;
        }
        else
        {
            currentCombo = 0;
        }

        isPerfectLastPlacement = isPerfect;

        // Check level complete
        if (score >= targetScore && targetScore > 0)
        {
            // Calculate stars (H5 original: no combo requirement)
            int stars = 1;
            if (score >= targetScore * 1.5f) stars = 3;
            else if (score >= targetScore * 1.2f) stars = 2;

            var save = SaveManager.Instance?.Current;
            if (save != null)
            {
                save.SetLevelStars(currentLevelId, stars);
                save.totalLayers++;
                save.totalGames++;
                if (maxCombo > save.bestComboEver) save.bestComboEver = maxCombo;
                SaveManager.Instance.Save();
            }
            AchievementManager.Instance?.CheckAndUnlock();
            AudioManager.Instance?.PlaySFX(SfxType.Star);
            // Emit level complete particles at the top of the stack
            ParticleManager.Instance?.EmitLevelComplete(
                new Vector3(stackManager.TopX, stackManager.TopY, 0), stars);
            SetState(GameState.LevelComplete);
            return;
        }

        // Spawn next block
        var nextParams = blockSpawner.GetNextBlockParams(score);
        blockSpawner.SpawnNextBlock(nextParams.width, nextParams.speed);
    }

    public void OnBlockMissed()
    {
        SetState(GameState.GameOver);
        ScreenShake.Instance?.Trigger(0.3f, 0.4f);
        AudioManager.Instance?.PitchDownBGM();
        AudioManager.Instance?.PlaySFX(SfxType.Fail);
        var save = SaveManager.Instance?.Current;
        if (save != null) { save.totalGames++; SaveManager.Instance.Save(); }
        AchievementManager.Instance?.CheckAndUnlock();

        // Show interstitial ad every 3rd game over to avoid annoying players
        gameOverCount++;
        if (gameOverCount % 3 == 0)
        {
            PlatformBridge.Instance?.SDK.ShowInterstitial(null);
        }
    }

    public void RestartLevel()
    {
        // Reset time scale in case we were paused
        Time.timeScale = 1f;
        stackManager.ClearStack();
        currentScore = 0;
        currentCombo = 0;
        maxCombo = 0;
        dropProgress = 0f;
        blockSpawner.ResetForLevel();
        var lm = FindObjectOfType<LevelManager>();
        if (lm == null) { Debug.LogError("LevelManager not found! Cannot restart level."); return; }
        var level = lm.GetLevel(currentLevelId);
        if (level == null) { Debug.LogError("Level not found for ID: " + currentLevelId); return; }
        StartLevel(level.levelId, level.targetLayers, level.initialBlockWidth, level.baseSpeed, level.useInternalCurve, level.speedCurve);
    }

    public void NextLevel()
    {
        int nextId = currentLevelId + 1;
        var lm = FindObjectOfType<LevelManager>();
        if (lm == null) { Debug.LogError("LevelManager not found! Cannot go to next level."); SetState(GameState.Idle); return; }
        var level = lm.GetLevel(nextId);
        if (level != null)
            StartLevel(level.levelId, level.targetLayers, level.initialBlockWidth, level.baseSpeed, level.useInternalCurve, level.speedCurve);
        else
            SetState(GameState.Idle);
    }

    /// <summary>
    /// Create the landing zone glow indicator quad.
    /// A colored rectangle at the stack top position that shows alignment quality.
    /// </summary>
    private void CreateLandingZone()
    {
        landingZone = new GameObject("LandingZoneIndicator");
        landingZoneSr = landingZone.AddComponent<SpriteRenderer>();

        // Simple white 1x1 pixel texture
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        landingZoneSr.sprite = Sprite.Create(tex,
            new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100);
        landingZoneSr.color = new Color(0, 0, 0, 0); // Start hidden
        landingZoneSr.sortingOrder = -1; // Behind blocks
    }

    /// <summary>
    /// Update the landing zone glow position, size, and color based on overlap.
    /// Green > 70%, Amber > 30%, Red < 30%.
    /// </summary>
    private void UpdateLandingZone()
    {
        if (landingZoneSr == null || blockSpawner?.CurrentBlock == null) return;

        float movingX = blockSpawner.CurrentBlock.transform.position.x;
        float movingW = blockSpawner.CurrentBlock.transform.localScale.x;
        float stackX = stackManager.TopX;
        float stackW = stackManager.TopWidth;

        // Calculate overlap amount and percentage
        float overlapLeft = Mathf.Max(movingX - movingW / 2f, stackX - stackW / 2f);
        float overlapRight = Mathf.Min(movingX + movingW / 2f, stackX + stackW / 2f);
        float overlap = Mathf.Max(0, overlapRight - overlapLeft);
        float pct = overlap / Mathf.Max(movingW, stackW);

        // Pick glow color based on overlap percentage
        Color glowColor;
        if (pct > 0.7f)
            glowColor = new Color(0.29f, 0.87f, 0.50f, 0.30f); // Green
        else if (pct > 0.3f)
            glowColor = new Color(1f, 0.71f, 0f, 0.20f);       // Amber
        else
            glowColor = new Color(1f, 0.39f, 0.39f, 0.15f);    // Red

        float zoneY = stackManager.TopY + BlockHeight;
        landingZone.transform.position = new Vector3(stackX, zoneY, 0);
        landingZone.transform.localScale = new Vector3(stackW, BlockHeight, 1);
        landingZoneSr.color = glowColor;
    }

    public string GetFailureMessage()
    {
        if (targetScore <= 0) return "再试一次吧！";
        float pct = (float)currentScore / targetScore;
        if (pct >= 1.0f) return "就差最后一口气! 下次必过! 💪🔥";
        if (pct >= 0.95f) return $"天呐! 只差{targetScore - currentScore}层! 我的心在滴血 😭💔";
        if (pct >= 0.85f) return $"只差{targetScore - currentScore}层! 就在眼前! 我不甘心啊!! 💪🔥";
        if (pct >= 0.70f) return $"已经{currentScore}层了! 差一点点就封神了! ⚡😤";
        if (pct >= 0.50f) return $"过半了! 这局手感不错, 再来一次必过! 🎯✨";
        if (pct >= 0.30f) return $"已经{currentScore}层, 稳住心态, 胜利在望! ✨💪";
        if (pct >= 0.15f) return $"就差那么一点点感觉! 再来一次手感就来了! 🔥😎";
        return "热身完毕! 这局才是真正的开始! 💫🚀";
    }

    public void Revive()
    {
        Time.timeScale = 1f;
        // Respawn moving block based on current stack top — preserves all progress
        blockSpawner.Configure(stackManager.TopWidth, blockSpawner.BaseSpeed, false, null, targetScore);
        blockSpawner.SpawnFirstBlock();
        SetState(GameState.Playing);
    }

    public void SetCombo(int combo) => currentCombo = Mathf.Max(0, combo);

    public void DecrementScore()
    {
        if (currentScore > 0) currentScore--;
    }
}
