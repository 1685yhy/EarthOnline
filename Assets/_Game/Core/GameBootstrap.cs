using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Bootstraps 弹弹塔 game infrastructure at runtime.
/// Creates Canvas, EventSystem, core managers if they don't exist.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // 1. Create EventSystem if missing
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform);
        }

        // 2. Create Canvas if missing
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750, 1334);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.transform.SetParent(transform);
        }

        // 3. Create/ensure core managers
        if (GameManager.Instance == null)
        {
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(transform);
            gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<BlockSpawner>();
            gmGo.AddComponent<StackManager>();
        }

        if (LevelManager.Instance == null)
        {
            var lmGo = new GameObject("LevelManager");
            lmGo.transform.SetParent(transform);
            lmGo.AddComponent<LevelManager>();
        }

        if (AudioManager.Instance == null)
        {
            var amGo = new GameObject("AudioManager");
            amGo.transform.SetParent(transform);
            amGo.AddComponent<AudioManager>();
            amGo.AddComponent<ProceduralAudio>();
        }

        if (ParticleManager.Instance == null)
        {
            var pmGo = new GameObject("ParticleManager");
            pmGo.transform.SetParent(transform);
            pmGo.AddComponent<ParticleManager>();
            pmGo.AddComponent<ProceduralParticles>();
            pmGo.AddComponent<ScreenShake>();
            pmGo.AddComponent<BlockAnimator>();
        }

        if (AchievementManager.Instance == null)
        {
            var achGo = new GameObject("AchievementManager");
            achGo.transform.SetParent(transform);
            achGo.AddComponent<AchievementManager>();
        }
        if (ThemeManager.Instance == null)
        {
            var tmGo = new GameObject("ThemeManager");
            tmGo.transform.SetParent(transform);
            tmGo.AddComponent<ThemeManager>();
        }
        if (SaveManager.Instance == null)
        {
            var smGo = new GameObject("SaveManager");
            smGo.transform.SetParent(transform);
            smGo.AddComponent<SaveManager>();
        }
        if (DailyChallenge.Instance == null)
        {
            var dcGo = new GameObject("DailyChallenge");
            dcGo.transform.SetParent(transform);
            dcGo.AddComponent<DailyChallenge>();
        }
        if (ToolSystem.Instance == null)
        {
            var tsGo = new GameObject("ToolSystem");
            tsGo.transform.SetParent(transform);
            tsGo.AddComponent<ToolSystem>();
        }

        // 4. Create UI if UIManager doesn't exist yet
        if (UIManager.Instance == null)
        {
            var uiManager = gameObject.AddComponent<UIManager>();

            // Build HUD first (structure only, add HUDController last)
            var hudGo = BuildHUDStructure(canvas.transform);
            var hudCtrl = hudGo.AddComponent<HUDController>();
            hudGo.AddComponent<CanvasGroup>();

            // Build Result panel structure first, add ResultPanel last
            var resultGo = BuildResultStructure(canvas.transform);
            var resultCtrl = resultGo.AddComponent<ResultPanel>();
            resultGo.AddComponent<CanvasGroup>();

            // Wire UIManager via reflection
            var uiType = typeof(UIManager);
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            uiType.GetField("hud", bf)?.SetValue(uiManager, hudCtrl);
            uiType.GetField("resultPanel", bf)?.SetValue(uiManager, resultGo);
            uiType.GetField("resultPanelController", bf)?.SetValue(uiManager, resultCtrl);
        }
    }

    /// <summary>
    /// Build HUD visual structure without adding HUDController.
    /// Returns the HUD root GameObject.
    /// </summary>
    private GameObject BuildHUDStructure(Transform canvasParent)
    {
        var hud = new GameObject("HUD", typeof(RectTransform));
        hud.transform.SetParent(canvasParent, false);
        var hrt = hud.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.sizeDelta = Vector2.zero;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Score text (top center)
        var scoreGo = MakeText("ScoreText", hud.transform, "0", 48, FontStyle.Bold, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(360, 56));
        var scoreShadow = scoreGo.AddComponent<Shadow>();
        scoreShadow.effectColor = new Color(0, 0, 0, 0.25f);
        scoreShadow.effectDistance = new Vector2(0, 2);

        // Combo text
        MakeText("ComboText", hud.transform, "", 22, FontStyle.Bold, new Color(1f, 0.42f, 0.208f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -68), new Vector2(300, 28));

        // Level text
        MakeText("LevelText", hud.transform, "", 13, FontStyle.Normal, new Color(0.72f, 0.64f, 0.55f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -98), new Vector2(300, 20));

        // Progress bar
        var barGo = new GameObject("ProgressBar", typeof(RectTransform));
        barGo.transform.SetParent(hud.transform, false);
        var brt = barGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 1f);
        brt.anchorMax = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0, -86);
        brt.sizeDelta = new Vector2(360, 10);
        barGo.AddComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(barGo.transform, false);
        var fart = fillArea.GetComponent<RectTransform>();
        fart.anchorMin = Vector2.zero;
        fart.anchorMax = Vector2.one;
        fart.sizeDelta = Vector2.zero;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(fillArea.transform, false);
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.sizeDelta = Vector2.zero;
        fillGo.AddComponent<Image>().color = new Color(0.96f, 0.45f, 0.21f);

        var slider = barGo.AddComponent<Slider>();
        slider.fillRect = fillGo.GetComponent<RectTransform>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        MakeText("ProgressText", hud.transform, "", 11, FontStyle.Normal, new Color(0.72f, 0.64f, 0.55f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(200, 16));

        // Tool bar at bottom
        var toolBar = new GameObject("ToolBar", typeof(RectTransform));
        toolBar.transform.SetParent(hud.transform, false);
        var trt = toolBar.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0f);
        trt.anchorMax = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0, 40);
        trt.sizeDelta = new Vector2(320, 60);

        string[] toolEmojis = { "🐢", "📏", "🔄" };
        for (int i = 0; i < 3; i++)
        {
            var btnGo = new GameObject("ToolBtn" + i, typeof(RectTransform));
            btnGo.transform.SetParent(toolBar.transform, false);
            var btnrt = btnGo.GetComponent<RectTransform>();
            btnrt.anchorMin = new Vector2(0.5f, 0.5f);
            btnrt.anchorMax = new Vector2(0.5f, 0.5f);
            btnrt.anchoredPosition = new Vector2(-100 + i * 100, 0);
            btnrt.sizeDelta = new Vector2(64, 64);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(1, 1, 1, 0.85f);
            btnGo.AddComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            labelGo.GetComponent<RectTransform>().SetStretch();
            var labelT = labelGo.AddComponent<Text>();
            labelT.font = font; labelT.fontSize = 24;
            labelT.alignment = TextAnchor.MiddleCenter;
            labelT.text = toolEmojis[i];
            labelT.raycastTarget = false;

            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(btnGo.transform, false);
            var crt = countGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1f);
            crt.anchorMax = new Vector2(0, 1f);
            crt.anchoredPosition = new Vector2(0, 0);
            crt.sizeDelta = new Vector2(28, 18);
            var countT = countGo.AddComponent<Text>();
            countT.font = font; countT.fontSize = 12; countT.fontStyle = FontStyle.Bold;
            countT.alignment = TextAnchor.MiddleCenter;
            countT.color = new Color(0, 0, 0, 0.5f);
            countT.text = "1";
            countT.raycastTarget = false;

            var adGo = new GameObject("AdPrompt", typeof(RectTransform));
            adGo.transform.SetParent(btnGo.transform, false);
            var adrt = adGo.GetComponent<RectTransform>();
            adrt.anchorMin = new Vector2(0, 0);
            adrt.anchorMax = new Vector2(1, 0);
            adrt.anchoredPosition = new Vector2(0, -2);
            adrt.sizeDelta = new Vector2(0, 14);
            var adT = adGo.AddComponent<Text>();
            adT.font = font; adT.fontSize = 9; adT.fontStyle = FontStyle.Bold;
            adT.alignment = TextAnchor.MiddleCenter;
            adT.color = new Color(1, 0.5f, 0, 0.9f);
            adT.text = "📺 看广告+";
            adT.raycastTarget = false;
            adGo.SetActive(false);
        }

        hud.SetActive(false);
        return hud;
    }

    /// <summary>
    /// Build Result panel visual structure without adding ResultPanel component.
    /// Returns the ResultPanel root GameObject.
    /// </summary>
    private GameObject BuildResultStructure(Transform canvasParent)
    {
        var rp = new GameObject("ResultPanel", typeof(RectTransform));
        rp.transform.SetParent(canvasParent, false);
        var rrt = rp.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.sizeDelta = Vector2.zero;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Dark overlay
        var ov = new GameObject("Overlay", typeof(RectTransform));
        ov.transform.SetParent(rp.transform, false);
        var ovrt = ov.GetComponent<RectTransform>();
        ovrt.SetStretch();
        var ovImg = ov.AddComponent<Image>();
        ovImg.color = new Color(0.102f, 0.055f, 0.020f, 0.55f);

        // Card
        var card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(rp.transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0, -10);
        crt.sizeDelta = new Vector2(300, 380);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(1, 1, 1, 0.93f);
        var cardShadow = card.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0.831f, 0.749f, 0.659f, 0.35f);
        cardShadow.effectDistance = new Vector2(0, 3);

        // Title
        MakeText("Title", card.transform, "", 26, FontStyle.Bold, new Color(0.239f, 0.180f, 0.118f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(200, 36));

        // Stars
        for (int i = 0; i < 3; i++)
        {
            MakeText("Star" + i, card.transform, "⭐", 40, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-42 + i * 42, 30), new Vector2(42, 42));
        }

        // Score
        MakeText("Score", card.transform, "0", 48, FontStyle.Bold, new Color(0.96f, 0.45f, 0.21f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(200, 56));

        // Sub info
        MakeText("SubInfo", card.transform, "", 12, FontStyle.Normal, new Color(0.545f, 0.451f, 0.333f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -68), new Vector2(240, 20));

        // Fail message
        MakeText("FailMsg", card.transform, "", 15, FontStyle.Normal, new Color(0.957f, 0.263f, 0.212f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(260, 24));

        // Province text
        MakeText("ProvText", card.transform, "🏅 你超过了 75% 的玩家", 12, FontStyle.Normal, new Color(0.545f, 0.451f, 0.333f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -114), new Vector2(240, 18));

        // Share button
        MakePillButton("ShareBtn", card.transform, "📤 分享",
            new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(160, 36),
            new Color(0.30f, 0.59f, 0.82f));

        // Revive button
        MakePillButton("ReviveBtn", card.transform, "📤 分享复活",
            new Vector2(0.5f, 0f), new Vector2(0, 95), new Vector2(210, 38),
            new Color(0.30f, 0.59f, 0.82f));

        // Ad revive button
        MakePillButton("AdReviveBtn", card.transform, "📺 看广告复活",
            new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(210, 38),
            new Color(0.2f, 0.6f, 1f, 0.85f));

        // Primary button
        var primBtn = MakePillButton("PrimaryBtn", card.transform, "继续",
            new Vector2(0.5f, 0.5f), new Vector2(0, 58), new Vector2(210, 46),
            new Color(0.96f, 0.45f, 0.21f));

        // Secondary button
        var secBtn = MakePillButton("SecondaryBtn", card.transform, "返回",
            new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(210, 38),
            new Color(0.96f, 0.96f, 0.94f));

        rp.SetActive(false);
        return rp;
    }

    private GameObject MakeText(string name, Transform parent, string text, int size, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        return go;
    }

    private GameObject MakePillButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        go.AddComponent<Image>().color = bgColor;
        go.AddComponent<Button>();

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.831f, 0.749f, 0.659f, 0.35f);
        shadow.effectDistance = new Vector2(0, 2);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.GetComponent<RectTransform>().SetStretch();
        var t = labelGo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        t.raycastTarget = false;
        return go;
    }
}
