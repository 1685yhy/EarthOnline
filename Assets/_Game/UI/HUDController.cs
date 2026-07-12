using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HUDController : MonoBehaviour
{
    [SerializeField] private Text scoreText;
    [SerializeField] private Text comboText;
    [SerializeField] private Text levelText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private GameObject toolBar;
    [SerializeField] private GameObject achievementToast;
    [SerializeField] private Text toastText;

    [SerializeField] private Text hintText;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject pausePanel;

    private int displayedScore = 0;
    private float scoreAnimVelocity;
    private int lastCombo = 0;
    private float comboBreakTimer = 0f;
    private float toastTimer = 0f;

    // Animation state
    private Coroutine comboBounceRoutine;
    private Coroutine scorePopRoutine;

    // Hint system state
    private enum HintPhase { Inactive, FirstTip, PerfectTip, Done }

    private void Start()
    {
        // Add Settings button to pause panel if one doesn't exist yet
        if (pausePanel != null)
        {
            var existingBtn = pausePanel.transform.Find("SettingsButton");
            if (existingBtn == null)
            {
                CreatePauseSettingsButton();
            }
        }
    }

    private void CreatePauseSettingsButton()
    {
        if (pausePanel == null) return;
        var settingsBtnGo = new GameObject("SettingsButton", typeof(RectTransform));
        settingsBtnGo.transform.SetParent(pausePanel.transform, false);
        var srt = settingsBtnGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(200, 44);
        // Position below the ResumeButton
        var resumeBtn = pausePanel.transform.Find("ResumeButton");
        srt.anchoredPosition = resumeBtn != null
            ? new Vector2(0, resumeBtn.GetComponent<RectTransform>().anchoredPosition.y - 60f)
            : new Vector2(0, -20);

        var img = settingsBtnGo.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.30f);
        var btn = settingsBtnGo.AddComponent<Button>();
        btn.onClick.AddListener(() => ToggleSettings());

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(settingsBtnGo.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
        var txt = labelGo.AddComponent<Text>();
        txt.font = DesignSystem.GameFont;
        txt.fontSize = 16; txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = "⚙ 设置";
        txt.raycastTarget = false;
    }

    private void ToggleSettings()
    {
        var settingsPanel = GameObject.Find("Canvas/SettingsPanel");
        if (settingsPanel != null)
        {
            bool active = !settingsPanel.activeSelf;
            settingsPanel.SetActive(active);
            // Keep pause panel visible behind settings
        }
        else
        {
            Debug.LogWarning("SettingsPanel not found in scene");
        }
    }
    private HintPhase hintPhase = HintPhase.Inactive;
    private float hintPhaseTimer = 0f;
    private int hintPlacementCount = 0;
    private int lastHintScore = -1;
    private bool hintPerfectGiven = false;

    private void Update()
    {
        if (GameManager.Instance == null) return;

        int targetScore = GameManager.Instance.CurrentScore;
        int prevDisplayed = displayedScore;
        // Smooth score animation — use unscaledDeltaTime so it doesn't freeze when paused
        displayedScore = (int)Mathf.SmoothDamp(displayedScore, targetScore, ref scoreAnimVelocity, 0.15f, float.MaxValue, Time.unscaledDeltaTime);
        if (Mathf.Abs(displayedScore - targetScore) < 0.5f) displayedScore = targetScore;

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();

            // Score pop effect when score increases
            if (displayedScore > prevDisplayed && scorePopRoutine == null)
                scorePopRoutine = StartCoroutine(ScorePopAnimation());
        }

        // Combo display
        int combo = GameManager.Instance.CurrentCombo;
        if (comboText != null)
        {
            if (combo >= 2)
            {
                comboText.text = $"🔥 {combo}x 连击";
                comboText.gameObject.SetActive(true);
                // Bounce combo text on new combo level
                if (combo != lastCombo)
                {
                    if (comboBounceRoutine != null) StopCoroutine(comboBounceRoutine);
                    comboBounceRoutine = StartCoroutine(BounceText(comboText.transform));
                }
            }
            else if (comboBreakTimer > 0)
            {
                comboText.text = "💔 连击中断";
                comboText.gameObject.SetActive(true);
                comboBreakTimer -= Time.unscaledDeltaTime;
                // Pulse alpha for combo break
                float alpha = 0.5f + Mathf.Sin(comboBreakTimer * 10f) * 0.3f;
                comboText.color = new Color(comboText.color.r, comboText.color.g, comboText.color.b, alpha);
            }
            else
            {
                comboText.gameObject.SetActive(false);
            }
        }

        // Detect combo break
        if (combo == 0 && lastCombo >= 3)
        {
            comboBreakTimer = 1.5f;
            // Reset combo text color on break
            if (comboText != null)
                comboText.color = DesignSystem.FailRed;
        }
        lastCombo = combo;

        // Progress bar smooth animation
        if (progressBar != null && GameManager.Instance.TargetScore > 0)
        {
            float target = (float)GameManager.Instance.CurrentScore / GameManager.Instance.TargetScore;
            progressBar.value = Mathf.Lerp(progressBar.value, target, Time.unscaledDeltaTime * 5f);
        }
        if (progressText != null && GameManager.Instance.TargetScore > 0)
        {
            progressText.text = $"{GameManager.Instance.CurrentScore}/{GameManager.Instance.TargetScore} 层";
        }

        // Tool count updates
        UpdateToolCounts();

        // Step-by-step tutorial hint
        UpdateHint();
    }

    private IEnumerator ScorePopAnimation()
    {
        if (scoreText == null) yield break;
        Vector3 originalScale = scoreText.transform.localScale;
        float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / duration;
            float scale = 1f + 0.15f * (1f - p);
            scoreText.transform.localScale = originalScale * scale;
            yield return null;
        }
        scoreText.transform.localScale = originalScale;
        scorePopRoutine = null;
    }

    private IEnumerator BounceText(Transform t)
    {
        Vector3 originalScale = t.localScale;
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / duration;
            float scale;
            if (p < 0.5f)
                scale = Mathf.Lerp(1f, 1.4f, p / 0.5f);
            else
                scale = Mathf.Lerp(1.4f, 1f, (p - 0.5f) / 0.5f);
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = originalScale;
    }

    private void UpdateHint()
    {
        if (hintText == null) return;

        // Track placements by watching score changes
        int currentScore = GameManager.Instance.CurrentScore;
        bool newPlacement = currentScore > lastHintScore;
        if (newPlacement)
        {
            if (currentScore > 0)
                hintPlacementCount++;
        }
        lastHintScore = currentScore;

        // Initialize hint when game starts playing
        if (hintPhase == HintPhase.Inactive && GameManager.Instance.CurrentState == GameState.Playing)
        {
            hintPhase = HintPhase.FirstTip;
            hintPhaseTimer = 0f;
            hintPlacementCount = 0;
            hintPerfectGiven = false;
            lastHintScore = GameManager.Instance.CurrentScore;
        }

        switch (hintPhase)
        {
            case HintPhase.FirstTip:
            {
                hintText.text = "👆 点击屏幕放下方块";
                hintText.gameObject.SetActive(true);
                hintPhaseTimer += Time.unscaledDeltaTime;

                // Pulse alpha for visibility
                float pulse = Mathf.Sin(hintPhaseTimer * 3f) * 0.25f + 0.75f;
                hintText.color = new Color(1, 1, 1, pulse);

                // If a perfect placement just happened and we haven't praised yet
                if (newPlacement && GameManager.Instance.CurrentCombo > 0 && !hintPerfectGiven)
                {
                    hintPerfectGiven = true;
                    hintPhase = HintPhase.PerfectTip;
                    hintPhaseTimer = 0f;
                }
                // Hide after 3 seconds if no perfect happened
                else if (hintPhaseTimer > 3f)
                {
                    hintPhase = HintPhase.Done;
                    hintText.gameObject.SetActive(false);
                }
                break;
            }

            case HintPhase.PerfectTip:
            {
                hintText.text = "✨ 完美! 对准中心更准!";
                hintText.color = new Color(1, 0.84f, 0f, 1f);
                hintText.gameObject.SetActive(true);
                hintPhaseTimer += Time.deltaTime;

                if (hintPhaseTimer > 2f)
                {
                    hintPhase = HintPhase.Done;
                    hintText.gameObject.SetActive(false);
                }
                break;
            }

            case HintPhase.Done:
                // No re-activation — once done, stay done
                break;
        }

        // After 3 placements, completely hide and never show again
        if (hintPlacementCount >= 3)
        {
            hintPhase = HintPhase.Done;
            if (hintText != null && hintText.gameObject.activeSelf)
                hintText.gameObject.SetActive(false);
        }
    }

    private void UpdateToolCounts()
    {
        if (toolBar == null || !toolBar.activeSelf) return;
        var ts = Object.FindObjectOfType<ToolSystem>();
        if (ts == null) return;

        // Original H5 has exactly 3 tools: Slow, Widen, Reverse
        ToolType[] types = { ToolType.Slow, ToolType.Widen, ToolType.Reverse };
        int maxBtns = Mathf.Min(types.Length, toolBar.transform.childCount);
        for (int i = 0; i < maxBtns; i++)
        {
            var btn = toolBar.transform.GetChild(i);
            var countLabel = btn.Find("Count");
            if (countLabel != null)
            {
                var txt = countLabel.GetComponent<Text>();
                if (txt != null)
                {
                    int count = ts.GetCount(types[i]);
                    txt.text = count.ToString();
                    txt.color = count > 0 ? new Color(0, 0, 0, 0.5f) : new Color(0.8f, 0.2f, 0.2f, 0.7f);
                }
            }
            // Dim button if no uses left
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                int count = ts.GetCount(types[i]);
                img.color = count > 0 ? new Color(1, 1, 1, 0.85f) : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }

            // Show ad prompt when tool count reaches zero, with click handler
            var adPrompt = btn.Find("AdPrompt");
            if (adPrompt != null)
            {
                int count = ts.GetCount(types[i]);
                bool wasActive = adPrompt.gameObject.activeSelf;
                adPrompt.gameObject.SetActive(count == 0);
                if (count == 0 && !wasActive)
                {
                    var adBtn = adPrompt.GetComponent<Button>() ?? adPrompt.gameObject.AddComponent<Button>();
                    adBtn.onClick.RemoveAllListeners();
                    ToolType capturedType = types[i];
                    adBtn.onClick.AddListener(() =>
                    {
                        PlatformBridge.Instance?.SDK.ShowRewardAd("tool_refill", (success) =>
                        {
                            if (success) ToolSystem.Instance?.AddTool(capturedType, 1);
                        });
                    });
                }
            }
        }
    }

    public void Show()
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        if (toolBar != null) toolBar.SetActive(true);
        // Reset progress bar value
        if (progressBar != null) progressBar.value = 0f;

        // Reset hint system for new game session
        hintPhase = HintPhase.Inactive;
        hintPhaseTimer = 0f;
        hintPlacementCount = 0;
        lastHintScore = -1;
        hintPerfectGiven = false;
    }

    public void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        if (toolBar != null) toolBar.SetActive(false);
        // Close settings panel if open
        var settingsPanel = GameObject.Find("Canvas/SettingsPanel");
        if (settingsPanel != null && settingsPanel.activeSelf)
            settingsPanel.SetActive(false);
    }

    public void SetLevelName(string name)
    {
        if (levelText != null) levelText.text = name;
    }

    public void TogglePause()
    {
        if (pausePanel == null) return;
        bool pause = !pausePanel.activeSelf;
        pausePanel.SetActive(pause);
        Time.timeScale = pause ? 0f : 1f;
    }

    public void ResumeGame()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ShowAchievement(string msg)
    {
        if (achievementToast == null || toastText == null) return;
        toastText.text = "🏆 " + msg;
        achievementToast.SetActive(true);
        toastTimer = 2.5f;
    }

    private void LateUpdate()
    {
        // Auto-hide achievement toast
        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0 && achievementToast != null)
                achievementToast.SetActive(false);
        }
    }
}
