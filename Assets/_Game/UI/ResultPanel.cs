using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ResultPanel : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text subText;
    [SerializeField] private Text failureMessageText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button adReviveButton;
    [SerializeField] private Text primaryButtonText;
    [SerializeField] private Text secondaryButtonText;
    [SerializeField] private Text reviveButtonText;
    [SerializeField] private Text adReviveButtonText;

    private bool wasLevelComplete;
    private Text[] starTexts = new Text[3];
    private Text provText;
    private CanvasGroup canvasGroup;
    private RectTransform cardRect;

    private void Awake()
    {
        // Find all components in children (GameBootstrap creates these at runtime)
        cardRect = transform.Find("Card")?.GetComponent<RectTransform>();

        // Find buttons by name
        if (primaryButton == null)
        {
            var obj = transform.Find("Card/PrimaryBtn");
            if (obj != null) primaryButton = obj.GetComponent<Button>();
        }
        if (secondaryButton == null)
        {
            var obj = transform.Find("Card/SecondaryBtn");
            if (obj != null) secondaryButton = obj.GetComponent<Button>();
        }
        if (reviveButton == null)
        {
            var obj = transform.Find("Card/ReviveBtn");
            if (obj != null) reviveButton = obj.GetComponent<Button>();
        }
        if (adReviveButton == null)
        {
            var obj = transform.Find("Card/AdReviveBtn");
            if (obj != null) adReviveButton = obj.GetComponent<Button>();
        }

        // Find text labels for buttons
        if (primaryButton != null) primaryButtonText = primaryButton.transform.Find("Label")?.GetComponent<Text>();
        if (secondaryButton != null) secondaryButtonText = secondaryButton.transform.Find("Label")?.GetComponent<Text>();
        if (reviveButton != null) reviveButtonText = reviveButton.transform.Find("Label")?.GetComponent<Text>();
        if (adReviveButton != null) adReviveButtonText = adReviveButton.transform.Find("Label")?.GetComponent<Text>();

        // Wire button listeners
        if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimaryClick);
        if (secondaryButton != null) secondaryButton.onClick.AddListener(OnSecondaryClick);
        if (reviveButton != null) reviveButton.onClick.AddListener(OnReviveClick);
        if (adReviveButton != null) adReviveButton.onClick.AddListener(OnAdReviveClick);
        else CreateAdReviveButton(); // Fallback

        // Find star texts by name
        for (int i = 0; i < 3; i++)
        {
            var starObj = transform.Find("Card/Star" + i);
            if (starObj != null) starTexts[i] = starObj.GetComponent<Text>();
        }

        // Wire share button
        var shareBtn = transform.Find("Card/ShareBtn")?.GetComponent<Button>();
        if (shareBtn != null) shareBtn.onClick.AddListener(OnShareClick);

        // Find province comparison text
        var provObj = transform.Find("Card/ProvText");
        if (provObj != null) provText = provObj.GetComponent<Text>();

        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void ShowGameOver()
    {
        wasLevelComplete = false;
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "游戏结束";
        if (scoreText != null) scoreText.text = "0";
        if (subText != null)
            subText.text = $"最高 {GameManager.Instance.MaxCombo} 连击 · {GameManager.Instance.StackManager.LayerCount} 层";
        if (failureMessageText != null)
            failureMessageText.text = GameManager.Instance.GetFailureMessage();
        if (primaryButtonText != null) primaryButtonText.text = "🔄 再来一局";
        if (secondaryButtonText != null) secondaryButtonText.text = "🏠 返回首页";
        if (reviveButton != null) reviveButton.gameObject.SetActive(true);
        if (reviveButtonText != null) reviveButtonText.text = "\U0001F4E4 分享复活";
        if (adReviveButton != null) adReviveButton.gameObject.SetActive(true);
        if (adReviveButtonText != null) adReviveButtonText.text = "📺 看广告复活";

        // Update province comparison text dynamically
        UpdateProvText();

        // Hide stars for game over
        for (int i = 0; i < 3; i++)
        {
            if (starTexts[i] != null)
                starTexts[i].gameObject.SetActive(false);
        }

        // Reset card scale for entrance animation
        ResetCardScale();
    }

    public void ShowLevelComplete()
    {
        wasLevelComplete = true;
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "🎉 通关!";
        if (scoreText != null) scoreText.text = "0";
        if (subText != null)
            subText.text = $"最高 {GameManager.Instance.MaxCombo} 连击";
        if (primaryButtonText != null) primaryButtonText.text = "▶ 下一关";
        if (secondaryButtonText != null) secondaryButtonText.text = "🔁 重玩本关";
        if (reviveButton != null) reviveButton.gameObject.SetActive(false);
        if (adReviveButton != null) adReviveButton.gameObject.SetActive(false);

        // Reset stars: hide all, they'll animate in
        for (int i = 0; i < 3; i++)
        {
            if (starTexts[i] != null)
            {
                starTexts[i].gameObject.SetActive(false);
                starTexts[i].color = new Color(1f, 0.84f, 0f); // gold for earned stars
            }
        }

        // Update province comparison text dynamically
        UpdateProvText();

        // Reset card scale for entrance animation
        ResetCardScale();
    }

    private void UpdateProvText()
    {
        if (provText == null) return;
        int score = GameManager.Instance.CurrentScore;
        int target = GameManager.Instance.TargetScore;
        float pct = target > 0 ? (float)score / target : 0f;
        // Map to percentile: 0% → 10%, 50% → 50%, 100% → 85%, 150% → 99%
        float percentile;
        if (pct >= 1.5f) percentile = Random.Range(96f, 99.5f);
        else if (pct >= 1.0f) percentile = Mathf.Lerp(75f, 95f, (pct - 1f) / 0.5f);
        else if (pct >= 0.5f) percentile = Mathf.Lerp(40f, 75f, (pct - 0.5f) / 0.5f);
        else percentile = Mathf.Lerp(10f, 40f, pct / 0.5f);
        percentile = (float)System.Math.Round(percentile, 1);
        provText.text = $"🏅 你超过了 {percentile}% 的玩家";
    }

    private void ResetCardScale()
    {
        if (cardRect != null)
            cardRect.localScale = Vector3.zero;
    }

    /// <summary>
    /// Called by UIManager after the panel fade-in completes.
    /// Starts card scale-up, score counting and star animations.
    /// </summary>
    public void AnimateIn()
    {
        // Card scale-up entrance
        if (cardRect != null)
            StartCoroutine(CardEntranceAnimation());

        StartCoroutine(AnimateScore(GameManager.Instance.CurrentScore));
        if (wasLevelComplete)
        {
            int stars = GetStarCount();
            StartCoroutine(AnimateStars(stars));
        }
    }

    private IEnumerator CardEntranceAnimation()
    {
        if (cardRect == null) yield break;
        float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            // Bounce ease (overshoot to 1.05 then settle)
            float scale;
            if (p < 0.7f)
                scale = Mathf.Lerp(0f, 1.05f, p / 0.7f);
            else
                scale = Mathf.Lerp(1.05f, 1f, (p - 0.7f) / 0.3f);
            cardRect.localScale = Vector3.one * scale;
            yield return null;
        }
        cardRect.localScale = Vector3.one;
    }

    private IEnumerator AnimateScore(int target)
    {
        // Faster initial count, slower near the end for dramatic effect
        float duration = Mathf.Min(1.0f, target * 0.03f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease-out for satisfying count-up
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            int current = (int)Mathf.Lerp(0, target, eased);
            if (scoreText != null)
            {
                scoreText.text = current.ToString();
                // Subtle scale pulse during counting
                scoreText.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * Mathf.PI * 4f));
            }
            yield return null;
        }
        if (scoreText != null)
        {
            scoreText.text = target.ToString();
            scoreText.transform.localScale = Vector3.one;
            // Final pop
            float popDuration = 0.2f;
            float popElapsed = 0f;
            while (popElapsed < popDuration)
            {
                popElapsed += Time.deltaTime;
                float p = popElapsed / popDuration;
                float scale = 1f + 0.2f * (1f - p);
                scoreText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            scoreText.transform.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimateStars(int earned)
    {
        for (int i = 0; i < 3; i++)
        {
            if (starTexts[i] != null)
            {
                if (i < earned)
                {
                    starTexts[i].gameObject.SetActive(true);
                    yield return StartCoroutine(ScaleBounce(starTexts[i].transform));
                }
                else
                {
                    // Unearned stars show dimmed
                    starTexts[i].gameObject.SetActive(true);
                    starTexts[i].color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                }
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator ScaleBounce(Transform t)
    {
        Vector3 original = t.localScale;
        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tVal = elapsed / duration;
            // Overshoot to 1.4, then settle to 1.0 — more dramatic for stars
            float scale;
            if (tVal < 0.5f)
                scale = Mathf.Lerp(0f, 1.4f, tVal / 0.5f);
            else
                scale = Mathf.Lerp(1.4f, 1f, (tVal - 0.5f) / 0.5f);
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = original;
    }

    private int GetStarCount()
    {
        return SaveManager.Instance?.Current?.GetLevelStars(GameManager.Instance.CurrentLevelId) ?? 1;
    }

    private void OnShareClick()
    {
        if (PlatformBridge.Instance?.SDK != null)
        {
            string msg = wasLevelComplete
                ? $"🎉 弹弹塔 第{GameManager.Instance.CurrentLevelId}关通关! {GameManager.Instance.CurrentScore}层!"
                : $"💪 弹弹塔 {GameManager.Instance.CurrentScore}层! 再来一次!";
            PlatformBridge.Instance.SDK.Share(msg, "", null);
        }
    }

    private void OnReviveClick()
    {
        if (PlatformBridge.Instance?.SDK == null) return;
        string msg = $"\U0001F4AA 弹弹塔 {GameManager.Instance.CurrentScore}层! 求复活!";
        PlatformBridge.Instance.SDK.Share(msg, "", (success) =>
        {
            if (success)
            {
                gameObject.SetActive(false);
                GameManager.Instance.RestartLevel();
            }
        });
    }

    private void OnAdReviveClick()
    {
        if (PlatformBridge.Instance?.SDK == null) return;
        PlatformBridge.Instance.SDK.ShowRewardAd("revive", (success) =>
        {
            if (success)
            {
                gameObject.SetActive(false);
                GameManager.Instance.Revive();
            }
        });
    }

    private void CreateAdReviveButton()
    {
        if (reviveButton == null) return;
        var adReviveGo = new GameObject("AdReviveButton", typeof(RectTransform));
        adReviveGo.transform.SetParent(reviveButton.transform.parent, false);
        var rrt = adReviveGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0.5f);
        rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(240, 44);
        rrt.anchoredPosition = new Vector2(0, reviveButton.GetComponent<RectTransform>().anchoredPosition.y - 56f);

        var img = adReviveGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 0.85f);
        var btn = adReviveGo.AddComponent<Button>();
        btn.onClick.AddListener(OnAdReviveClick);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(adReviveGo.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
        var txt = labelGo.AddComponent<Text>();
        txt.font = DesignSystem.GameFont;
        txt.fontSize = 16; txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = "📺 看广告复活";
        txt.raycastTarget = false;

        adReviveButton = btn;
        adReviveButtonText = txt;
    }

    private void OnPrimaryClick()
    {
        gameObject.SetActive(false);
        var gm = GameManager.Instance;
        if (wasLevelComplete)
            gm.NextLevel();
        else
            gm.RestartLevel();
    }

    private void OnSecondaryClick()
    {
        gameObject.SetActive(false);
        var gm = GameManager.Instance;
        if (wasLevelComplete)
            gm.RestartLevel();
        else
            UIManager.Instance?.ShowHome();
    }
}
