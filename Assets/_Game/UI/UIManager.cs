using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private HUDController hud;
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private ResultPanel resultPanelController;

    public HUDController HUD => hud;

    private CanvasGroup homeCG;
    private CanvasGroup hudCG;
    private CanvasGroup resultCG;
    private Coroutine currentTransition;

    private void Awake() => Instance = this;

    private void Start()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("No Canvas found!"); return; }

        // Create HomePanel if not assigned
        if (homePanel == null)
        {
            homePanel = new GameObject("HomePanel", typeof(RectTransform), typeof(CanvasGroup));
            homePanel.transform.SetParent(canvas.transform, false);
            var hrt = homePanel.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.sizeDelta = Vector2.zero;
            homePanel.AddComponent<Image>().color = new Color(0.996f, 0.976f, 0.941f);
            homePanel.AddComponent<HomePanelController>();
        }
        homeCG = homePanel.GetComponent<CanvasGroup>();
        if (homeCG == null) homeCG = homePanel.AddComponent<CanvasGroup>();

        // HUD is created by GameBootstrap — ensure CanvasGroup
        if (hud != null)
        {
            hudCG = hud.GetComponent<CanvasGroup>();
            if (hudCG == null) hudCG = hud.gameObject.AddComponent<CanvasGroup>();
        }

        // Result panel is created by GameBootstrap — ensure CanvasGroup
        if (resultPanel != null)
        {
            resultCG = resultPanel.GetComponent<CanvasGroup>();
            if (resultCG == null) resultCG = resultPanel.AddComponent<CanvasGroup>();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        ShowHome();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged.RemoveListener(OnGameStateChanged);
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (currentTransition != null) StopCoroutine(currentTransition);

        switch (newState)
        {
            case GameState.Playing:
                currentTransition = StartCoroutine(TransitionToGame());
                break;
            case GameState.GameOver:
                currentTransition = StartCoroutine(TransitionToResult(false));
                break;
            case GameState.LevelComplete:
                currentTransition = StartCoroutine(TransitionToResult(true));
                break;
        }
    }

    private IEnumerator TransitionToGame()
    {
        // Fade out home panel if still visible
        if (homePanel != null && homePanel.activeSelf)
        {
            yield return FadeCanvasGroup(homeCG, homeCG.alpha, 0f, 0.2f);
            homePanel.SetActive(false);
        }

        // Hide level select panel if visible
        var lsp = FindObjectOfType<LevelSelectPanel>();
        if (lsp != null) lsp.gameObject.SetActive(false);

        // Fade in HUD
        if (hud != null)
        {
            hud.gameObject.SetActive(true);
            hud.Show();
            hudCG.alpha = 0f;
            yield return FadeCanvasGroup(hudCG, 0f, 1f, 0.2f);
        }
    }

    private IEnumerator TransitionToResult(bool levelComplete)
    {
        // Handle BGM based on result type
        if (levelComplete)
        {
            // On level complete: restore BGM to normal pitch (was at high intensity)
            AudioManager.Instance?.RestoreBGM();
        }
        else
        {
            // On game over: BGM already pitch-downed by OnBlockMissed, fade volume to silent
            AudioManager.Instance?.SetBGMIntensity(0f);
        }

        // Fade out HUD
        if (hud != null)
        {
            yield return FadeCanvasGroup(hudCG, hudCG.alpha, 0f, 0.15f);
            hud.Hide();
        }

        // Show result panel with content, then fade in
        if (resultPanel != null)
        {
            // Call show methods first to set text content while alpha=0
            if (levelComplete)
                resultPanelController.ShowLevelComplete();
            else
                resultPanelController.ShowGameOver();

            // Start invisible, fade in
            resultCG.alpha = 0f;
            yield return FadeCanvasGroup(resultCG, 0f, 1f, 0.3f);

            // Start internal animations (score count-up, stars)
            resultPanelController.AnimateIn();
        }
    }

    public void ShowHome()
    {
        if (currentTransition != null) StopCoroutine(currentTransition);
        currentTransition = StartCoroutine(TransitionToHome());
    }

    private IEnumerator TransitionToHome()
    {
        // Restore BGM for home screen
        AudioManager.Instance?.RestoreBGM();
        AudioManager.Instance?.SetBGMIntensity(0f);

        // Fade out result if active
        if (resultPanel != null && resultPanel.activeSelf)
        {
            yield return FadeCanvasGroup(resultCG, resultCG.alpha, 0f, 0.2f);
            resultPanel.SetActive(false);
        }

        // Hide level select panel if visible
        var lsp = FindObjectOfType<LevelSelectPanel>();
        if (lsp != null) lsp.gameObject.SetActive(false);

        // Fade in home panel
        if (homePanel != null)
        {
            homePanel.SetActive(true);
            homeCG.alpha = 0f;
            yield return FadeCanvasGroup(homeCG, 0f, 1f, 0.2f);
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
