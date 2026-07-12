#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds/rebuilds the game UI using the DesignSystem.
/// Editor-only — stripped from builds.
/// </summary>
public class UIRebuilder : MonoBehaviour
{
    private Font _font;
    private System.Reflection.BindingFlags _bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [ContextMenu("Rebuild All UI")]
    public void Rebuild()
    {
        _font = DesignSystem.GameFont;
        RebuildHome();
        RebuildHUD();
        RebuildResult();
        Debug.Log("UI Rebuild complete.");
    }

    // ======================== HOME PANEL ========================
    private void RebuildHome()
    {
        var home = FindInactive("HomePanel");
        if (home == null) { home = new GameObject("HomePanel", typeof(RectTransform)); home.transform.SetParent(null, false); }
        var wasActive = home.activeSelf; home.SetActive(true);
        while (home.transform.childCount > 0) DestroyImmediate(home.transform.GetChild(0).gameObject);

        var hrt = home.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.sizeDelta = Vector2.zero;
        if (home.GetComponent<CanvasGroup>() == null) home.AddComponent<CanvasGroup>();
        if (home.GetComponent<HomePanelController>() == null) home.AddComponent<HomePanelController>();

        // Background: warm cream
        var bg = new GameObject("BG", typeof(RectTransform)); bg.transform.SetParent(home.transform, false);
        bg.GetComponent<RectTransform>().SetStretch();
        bg.AddComponent<Image>().color = DesignSystem.BgPrimary;

        // --- Daily streak (top center, hidden by default) ---
        var streakGo = new GameObject("StreakText", typeof(RectTransform));
        streakGo.transform.SetParent(home.transform, false);
        var srt = streakGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0, -40);
        srt.sizeDelta = new Vector2(200, 20);
        var streakT = streakGo.AddComponent<Text>();
        streakT.font = _font; streakT.fontSize = 14; streakT.fontStyle = FontStyle.Bold;
        streakT.alignment = TextAnchor.MiddleCenter;
        streakT.color = DesignSystem.AccentOrange;
        streakT.text = "";
        streakT.raycastTarget = false;
        streakGo.SetActive(false);

        // --- Decorative tower blocks (compact pyramid, 4 blocks) ---
        float towerTopY = -75f;
        float blockH = 8f;
        int towerBlocks = 4;
        float[] widths = { 22f, 34f, 46f, 58f };
        Color[] towerColors = {
            new Color(0f, 0.808f, 0.788f),     // teal
            new Color(0.992f, 0.796f, 0.431f),  // gold
            new Color(1f, 0.329f, 0.439f),      // coral
            new Color(0.424f, 0.361f, 0.906f),  // violet
        };

        for (int i = 0; i < towerBlocks; i++)
        {
            float yPos = towerTopY + i * blockH;
            float w = widths[i];
            var block = new GameObject("TowerBlock" + i, typeof(RectTransform));
            block.transform.SetParent(home.transform, false);
            var brt = block.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0, yPos);
            brt.sizeDelta = new Vector2(w, blockH);
            var img = block.AddComponent<Image>();
            img.color = towerColors[i];
            img.sprite = MakeWhiteSpriteWithRadius(4f);
            img.type = Image.Type.Sliced;
        }

        // --- Title "弹弹塔" ---
        var titleObj = Txt("Title", home.transform, "弹弹塔", 44, DesignSystem.TextPrimary);
        titleObj.GetComponent<RectTransform>().SetTop(-120, 280, 44);
        titleObj.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // --- Subtitle ---
        Txt("Subtitle", home.transform, "今天你堆了吗？", 15, DesignSystem.TextSecondary)
            .GetComponent<RectTransform>().SetTop(-162, 200, 20);

        // --- Achievement count ---
        var achGo = new GameObject("AchievementText", typeof(RectTransform));
        achGo.transform.SetParent(home.transform, false);
        var art = achGo.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0.5f, 1f);
        art.anchorMax = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(0, -195);
        art.sizeDelta = new Vector2(180, 18);
        var achT = achGo.AddComponent<Text>();
        achT.font = _font; achT.fontSize = 12; achT.fontStyle = FontStyle.Normal;
        achT.alignment = TextAnchor.MiddleCenter;
        achT.color = DesignSystem.TextSecondary;
        achT.text = "🏆 0/15";
        achT.raycastTarget = false;

        // --- Primary CTA button "开始游戏" ---
        var cta = DesignSystem.MakePillButton("StartButton", home.transform, "🎮 开始游戏", 247, 280, 58, _font, DesignSystem.AccentOrange);
        var ctaBtn = cta.GetComponent<Button>();

        // --- Level Grid (30 levels, 6 columns, matching original H5) ---
        var gridGo = new GameObject("LevelGrid", typeof(RectTransform));
        gridGo.transform.SetParent(home.transform, false);
        var grdRt = gridGo.GetComponent<RectTransform>();
        grdRt.anchorMin = new Vector2(0.5f, 1f);
        grdRt.anchorMax = new Vector2(0.5f, 1f);
        grdRt.anchoredPosition = new Vector2(0, -280);
        grdRt.sizeDelta = new Vector2(360, 380);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(55, 62);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.MiddleCenter;

        // Create 30 level buttons
        var lm = Object.FindObjectOfType<LevelManager>();
        for (int i = 1; i <= 30; i++)
        {
            var btnIdx = i; // capture for closure
            var btnGo = new GameObject("LevelBtn" + i, typeof(RectTransform));
            btnGo.transform.SetParent(gridGo.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(55, 62);

            var bImg = btnGo.AddComponent<Image>();
            bImg.sprite = MakeWhiteSpriteWithRadius(8f);
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(1, 1, 1, 0.95f);

            var bBtn = btnGo.AddComponent<Button>();
            bBtn.onClick.AddListener(() =>
            {
                var homeCtrl = home.GetComponent<HomePanelController>();
                var lm2 = Object.FindObjectOfType<LevelManager>();
                if (lm2 != null)
                {
                    var level = lm2.GetLevel(btnIdx);
                    if (level != null)
                    {
                        home.SetActive(false);
                        GameManager.Instance.StartLevel(level.levelId, level.targetLayers,
                            level.initialBlockWidth, level.baseSpeed, level.useInternalCurve, level.speedCurve);
                    }
                }
            });

            // Level number
            var numGo = new GameObject("Num", typeof(RectTransform));
            numGo.transform.SetParent(btnGo.transform, false);
            var numRt = numGo.GetComponent<RectTransform>();
            numRt.anchorMin = new Vector2(0.5f, 0.6f);
            numRt.anchorMax = new Vector2(0.5f, 1f);
            numRt.sizeDelta = new Vector2(50, 24);
            var nTxt = numGo.AddComponent<Text>();
            nTxt.font = _font; nTxt.fontSize = 18; nTxt.fontStyle = FontStyle.Bold;
            nTxt.alignment = TextAnchor.LowerCenter;
            nTxt.color = DesignSystem.TextPrimary;
            nTxt.text = i.ToString();
            nTxt.raycastTarget = false;

            // Target layers label
            int targetLayers = 2;
            if (lm != null)
            {
                var levelData = lm.GetLevel(i);
                if (levelData != null) targetLayers = levelData.targetLayers;
            }
            var targetGo = new GameObject("Target", typeof(RectTransform));
            targetGo.transform.SetParent(btnGo.transform, false);
            var tgtRt = targetGo.GetComponent<RectTransform>();
            tgtRt.anchorMin = new Vector2(0.5f, 0f);
            tgtRt.anchorMax = new Vector2(0.5f, 0.55f);
            tgtRt.sizeDelta = new Vector2(50, 18);
            var tTxt = targetGo.AddComponent<Text>();
            tTxt.font = _font; tTxt.fontSize = 8; tTxt.fontStyle = FontStyle.Normal;
            tTxt.alignment = TextAnchor.UpperCenter;
            tTxt.color = DesignSystem.TextMuted;
            tTxt.text = targetLayers + "层";
            tTxt.raycastTarget = false;

            // Stars
            var starGo = new GameObject("Stars", typeof(RectTransform));
            starGo.transform.SetParent(btnGo.transform, false);
            var srtStars = starGo.GetComponent<RectTransform>();
            srtStars.anchorMin = new Vector2(0.5f, 0f);
            srtStars.anchorMax = new Vector2(0.5f, 0.25f);
            srtStars.sizeDelta = new Vector2(50, 12);
            var sTxt = starGo.AddComponent<Text>();
            sTxt.font = _font; sTxt.fontSize = 8;
            sTxt.alignment = TextAnchor.UpperCenter;
            sTxt.color = Color.yellow;
            sTxt.text = "";
            sTxt.raycastTarget = false;
        }

        // --- Secondary buttons row (关卡选择, 无尽模式, 每日挑战) ---
        float btnY = -700;
        var levelBtn = MakeSecondaryBtn("LevelSelectBtn", home.transform, "📋 关卡", -126, btnY);
        var endlessBtn = MakeSecondaryBtn("EndlessBtn", home.transform, "♾️ 无尽", 0, btnY);
        var dailyBtn = MakeSecondaryBtn("DailyBtn", home.transform, "📅 每日", 126, btnY);

        // --- Province Ranking Card ---
        var provCard = DesignSystem.MakeCard("ProvinceCard", home.transform, new Vector2(0, 67), new Vector2(320, 70), DesignSystem.RadiusCard);
        provCard.GetComponent<Image>().color = DesignSystem.CardBg;

        // Province rank text
        var provRankGo = new GameObject("ProvinceRankText", typeof(RectTransform));
        provRankGo.transform.SetParent(provCard.transform, false);
        var prrt = provRankGo.GetComponent<RectTransform>();
        prrt.anchorMin = new Vector2(0.5f, 1f);
        prrt.anchorMax = new Vector2(0.5f, 1f);
        prrt.anchoredPosition = new Vector2(0, -18);
        prrt.sizeDelta = new Vector2(290, 20);
        var prText = provRankGo.AddComponent<Text>();
        prText.font = _font; prText.fontSize = 14; prText.fontStyle = FontStyle.Bold;
        prText.alignment = TextAnchor.MiddleCenter;
        prText.color = DesignSystem.TextPrimary;
        prText.text = "\U0001F3C5 加载中...";
        prText.raycastTarget = false;

        // Clear rate text
        var clearRateGo = new GameObject("ClearRateText", typeof(RectTransform));
        clearRateGo.transform.SetParent(provCard.transform, false);
        var crrt = clearRateGo.GetComponent<RectTransform>();
        crrt.anchorMin = new Vector2(0.5f, 1f);
        crrt.anchorMax = new Vector2(0.5f, 1f);
        crrt.anchoredPosition = new Vector2(0, -46);
        crrt.sizeDelta = new Vector2(290, 16);
        var crText = clearRateGo.AddComponent<Text>();
        crText.font = _font; crText.fontSize = 11; crText.fontStyle = FontStyle.Normal;
        crText.alignment = TextAnchor.MiddleCenter;
        crText.color = DesignSystem.TextSecondary;
        crText.text = "今日通关率: --%";
        crText.raycastTarget = false;

        // --- Version text (bottom-right) ---
        var verGo = new GameObject("VersionText", typeof(RectTransform));
        verGo.transform.SetParent(home.transform, false);
        var vrt = verGo.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(1f, 0f);
        vrt.anchorMax = new Vector2(1f, 0f);
        vrt.anchoredPosition = new Vector2(-12f, 12f);
        vrt.sizeDelta = new Vector2(100f, 18f);
        var vt = verGo.AddComponent<Text>();
        vt.font = _font; vt.fontSize = 10;
        vt.alignment = TextAnchor.LowerRight;
        vt.color = DesignSystem.TextMuted;
        vt.text = "v1.0.0";
        vt.raycastTarget = false;

        // --- Wire up HomePanelController ---
        var hpc = home.GetComponent<HomePanelController>();
        var hpcType = hpc.GetType();
        hpcType.GetField("startButton", _bf).SetValue(hpc, ctaBtn);
        hpcType.GetField("levelSelectButton", _bf).SetValue(hpc, levelBtn.GetComponent<Button>());
        hpcType.GetField("endlessButton", _bf).SetValue(hpc, endlessBtn.GetComponent<Button>());
        hpcType.GetField("dailyButton", _bf).SetValue(hpc, dailyBtn.GetComponent<Button>());

        home.SetActive(wasActive);
    }

    /// <summary>
    /// Creates a small white rounded square sprite for UI use.
    /// </summary>
    private Sprite MakeWhiteSpriteWithRadius(float radius)
    {
        int size = Mathf.Max((int)radius * 2 + 4, 16);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = Color.clear;
        Color white = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius - 2;
                float dy = y - radius - 2;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool inTL = x < radius + 2 && y < radius + 2;
                bool inTR = x >= size - (radius + 2) && y < radius + 2;
                bool inBL = x < radius + 2 && y >= size - (radius + 2);
                bool inBR = x >= size - (radius + 2) && y >= size - (radius + 2);
                if (inTL || inTR || inBL || inBR)
                    tex.SetPixel(x, y, dist <= radius ? white : clear);
                else
                    tex.SetPixel(x, y, white);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
    }

    // ======================== HUD ========================
    private void RebuildHUD()
    {
        var hud = FindInactive("HUD");
        if (hud == null) return;
        var wasActive = hud.activeSelf; hud.SetActive(true);

        var hudCtrl = hud.GetComponent<HUDController>();
        if (hudCtrl == null) { hudCtrl = hud.AddComponent<HUDController>(); hud.AddComponent<CanvasGroup>(); }

        // --- Score text: make it large and prominent at top center ---
        var scoreText = hud.transform.Find("ScoreText");
        if (scoreText != null)
        {
            var st = scoreText.GetComponent<Text>();
            if (st != null)
            {
                st.fontSize = DesignSystem.FontScoreHUD; // 56px as per original H5
                st.fontStyle = FontStyle.Bold;
                st.color = Color.white;
                // Add gold gradient effect (white -> #ffd700)
                var grad = scoreText.GetComponent<GradientText>() ?? scoreText.gameObject.AddComponent<GradientText>();
                grad.TopColor = Color.white;
                grad.BottomColor = DesignSystem.ScoreGold;
                // Add golden glow shadow
                var shadow = scoreText.GetComponent<Shadow>() ?? scoreText.AddComponent<Shadow>();
                shadow.effectColor = DesignSystem.GoldGlow;
                shadow.effectDistance = new Vector2(0, 2);
            }
            // Make the ScoreText rect wider for big numbers
            var srt = scoreText.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(360, srt.sizeDelta.y);
            srt.anchoredPosition = new Vector2(0, -20);
        }

        // --- Combo text: style with fire color ---
        var comboText = hud.transform.Find("ComboText");
        if (comboText != null)
        {
            var ct = comboText.GetComponent<Text>();
            if (ct != null)
            {
                ct.fontSize = DesignSystem.FontCombo + 4;
                ct.fontStyle = FontStyle.Bold;
                ct.color = DesignSystem.ComboFire;
            }
            var csh = comboText.GetComponent<Shadow>() ?? comboText.AddComponent<Shadow>();
            csh.effectColor = new Color(0, 0, 0, 0.10f);
            csh.effectDistance = new Vector2(0, 2);

            var crt = comboText.GetComponent<RectTransform>();
            crt.anchoredPosition = new Vector2(0, -60);
        }

        // --- Level text: subtle styling ---
        var levelText = hud.transform.Find("LevelText");
        if (levelText != null)
        {
            var lt = levelText.GetComponent<Text>();
            if (lt != null)
            {
                lt.fontSize = DesignSystem.FontLevelName;
                lt.color = DesignSystem.TextMuted;
            }
        }

        // --- Progress bar: wider and styled with accent color ---
        var progressBar = hud.transform.Find("ProgressBar");
        if (progressBar != null)
        {
            var barImg = progressBar.GetComponent<Image>();
            if (barImg != null)
            {
                barImg.color = new Color(0.95f, 0.93f, 0.88f); // warm bg
                // Make it wider
                var prt = progressBar.GetComponent<RectTransform>();
                prt.sizeDelta = new Vector2(360, 10);
            }

            // Style the Fill child
            var fill = progressBar.Find("Fill");
            if (fill != null)
            {
                var fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.color = DesignSystem.AccentOrange;
                    // Soft rounded look via sprite hint
                    fillImg.sprite = MakeWhiteSpriteWithRadius(5f);
                    fillImg.type = Image.Type.Sliced;
                }
            }

            // Style the FillArea
            var fillArea = progressBar.Find("FillArea");
            if (fillArea != null)
            {
                var faImg = fillArea.GetComponent<Image>();
                if (faImg == null) { faImg = fillArea.gameObject.AddComponent<Image>(); }
                faImg.color = new Color(0, 0, 0, 0);
            }

            // Reposition progress bar lower
            var brt = progressBar.GetComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(0, -88);
        }

        // --- Progress text: style ---
        var progressText = hud.transform.Find("ProgressText");
        if (progressText != null)
        {
            var pt = progressText.GetComponent<Text>();
            if (pt != null)
            {
                pt.fontSize = 11;
                pt.color = DesignSystem.TextMuted;
            }
            var prt2 = progressText.GetComponent<RectTransform>();
            prt2.anchoredPosition = new Vector2(0, -102);
        }

        // --- Tool bar: enhanced styling with rounded buttons ---
        var toolBar = hud.transform.Find("ToolBar");
        if (toolBar != null)
        {
            foreach (Transform btn in toolBar)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(1, 1, 1, 0.88f);
                    // Give toolbar buttons a rounded look with more visible rounding
                    img.sprite = MakeWhiteSpriteWithRadius(14f);
                    img.type = Image.Type.Sliced;
                }
                // Shadow on buttons
                var btnShadow = btn.GetComponent<Shadow>() ?? btn.gameObject.AddComponent<Shadow>();
                btnShadow.effectColor = new Color(0, 0, 0, 0.08f);
                btnShadow.effectDistance = new Vector2(0, 2);

                // Style count label
                var cnt = btn.Find("Count");
                if (cnt != null)
                {
                    var t = cnt.GetComponent<Text>();
                    if (t != null)
                    {
                        t.fontSize = 12;
                        t.fontStyle = FontStyle.Bold;
                        t.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                    }
                }
                // Style label (the emoji/icon)
                var lbl = btn.Find("Label");
                if (lbl == null) lbl = btn.Find("L");
                if (lbl != null)
                {
                    var lt2 = lbl.GetComponent<Text>();
                    if (lt2 != null) { lt2.fontSize = 20; }
                }

                // Ad prompt when tool count reaches zero (hidden by default)
                var adPrompt = btn.Find("AdPrompt");
                if (adPrompt == null)
                {
                    var adGo = new GameObject("AdPrompt", typeof(RectTransform));
                    adGo.transform.SetParent(btn, false);
                    var adrt = adGo.GetComponent<RectTransform>();
                    adrt.anchorMin = new Vector2(0f, 0f);
                    adrt.anchorMax = new Vector2(1f, 0f);
                    adrt.anchoredPosition = new Vector2(0, -2f);
                    adrt.sizeDelta = new Vector2(0, 14);
                    var adT = adGo.AddComponent<Text>();
                    adT.font = _font; adT.fontSize = 9; adT.fontStyle = FontStyle.Bold;
                    adT.alignment = TextAnchor.MiddleCenter;
                    adT.color = new Color(1, 0.5f, 0, 0.9f);
                    adT.text = "\U0001F4FA 看广告+";
                    adT.raycastTarget = false;
                    adPrompt = adGo.transform;
                }
                adPrompt.gameObject.SetActive(false);
            }
        }

        // --- Pause button: subtle style ---
        var pauseBtn = hud.transform.Find("PauseButton");
        if (pauseBtn != null)
        {
            var pImg = pauseBtn.GetComponent<Image>();
            if (pImg != null)
            {
                pImg.color = new Color(1, 1, 1, 0.50f);
                pImg.sprite = MakeWhiteSpriteWithRadius(10f);
                pImg.type = Image.Type.Sliced;
                var prt3 = pauseBtn.GetComponent<RectTransform>();
                prt3.sizeDelta = new Vector2(32, 32);
            }
        }

        hud.SetActive(wasActive);
    }

    // ======================== RESULT PANEL ========================
    private void RebuildResult()
    {
        var rp = FindInactive("ResultPanel");
        if (rp == null) { rp = new GameObject("ResultPanel", typeof(RectTransform)); rp.transform.SetParent(null, false); }
        var wasActive = rp.activeSelf; rp.SetActive(true);
        foreach (Transform child in rp.transform) { DestroyImmediate(child.gameObject); }

        var rrt = rp.GetComponent<RectTransform>(); rrt.SetStretch();
        if (rp.GetComponent<CanvasGroup>() == null) rp.AddComponent<CanvasGroup>();
        if (rp.GetComponent<ResultPanel>() == null) rp.AddComponent<ResultPanel>();

        // Dark overlay
        var ov = new GameObject("Overlay", typeof(RectTransform)); ov.transform.SetParent(rp.transform, false);
        ov.GetComponent<RectTransform>().SetStretch(); ov.AddComponent<Image>().color = DesignSystem.Overlay;

        // Result Card — slightly larger for better proportions
        var card = DesignSystem.MakeCard("Card", rp.transform, new Vector2(0, -10), new Vector2(300, 360), DesignSystem.RadiusCard);

        // Title
        Txt("Title", card.transform, "", DesignSystem.FontResultTitle, DesignSystem.TextPrimary).GetComponent<RectTransform>().SetTop(-30, 200, 36);
        card.transform.Find("Title").GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Stars (positioned better)
        for (int i = 0; i < 3; i++)
        {
            Txt("Star" + i, card.transform, "⭐", 40, new Color(0.7f, 0.7f, 0.7f)).GetComponent<RectTransform>().SetTop(-72, 42, 42);
            card.transform.Find("Star" + i).GetComponent<RectTransform>().anchoredPosition = new Vector2(-42 + i * 42, -72);
        }

        // Score
        Txt("Score", card.transform, "0", DesignSystem.FontResultScore, DesignSystem.AccentOrange).GetComponent<RectTransform>().SetTop(-125, 200, 56);
        card.transform.Find("Score").GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Sub text
        Txt("SubInfo", card.transform, "", DesignSystem.FontStats, DesignSystem.TextSecondary).GetComponent<RectTransform>().SetTop(-172, 240, 20);

        // Failure message
        Txt("FailMsg", card.transform, "", DesignSystem.FontFailMsg, DesignSystem.FailRed).GetComponent<RectTransform>().SetTop(-196, 260, 24);

        // Province compare
        Txt("ProvText", card.transform, "🏅 你超过了 75% 的玩家", DesignSystem.FontStats, DesignSystem.TextSecondary).GetComponent<RectTransform>().SetTop(-218, 240, 18);

        // Share button — below ProvText, above the card buttons
        var shareBtn = new GameObject("ShareBtn", typeof(RectTransform));
        shareBtn.transform.SetParent(card.transform, false);
        var shrt = shareBtn.GetComponent<RectTransform>();
        shrt.anchorMin = new Vector2(0.5f, 0.5f);
        shrt.anchorMax = new Vector2(0.5f, 0.5f);
        shrt.anchoredPosition = new Vector2(0f, -75f);
        shrt.sizeDelta = new Vector2(160, 36);
        var shImg = shareBtn.AddComponent<Image>();
        shImg.color = new Color(0.30f, 0.59f, 0.82f);
        // Rounded corners via sprite
        shImg.sprite = MakeWhiteSpriteWithRadius(18f);
        shImg.type = Image.Type.Sliced;
        var shShadow = shareBtn.AddComponent<Shadow>();
        shShadow.effectColor = DesignSystem.CardShadow;
        shShadow.effectDistance = new Vector2(0, 2);
        var shBtn = shareBtn.AddComponent<Button>();
        var shLabel = new GameObject("Label", typeof(RectTransform));
        shLabel.transform.SetParent(shareBtn.transform, false);
        var shlrt = shLabel.GetComponent<RectTransform>();
        shlrt.anchorMin = Vector2.zero; shlrt.anchorMax = Vector2.one; shlrt.sizeDelta = Vector2.zero;
        var shTxt = shLabel.AddComponent<Text>();
        shTxt.font = _font; shTxt.fontSize = 14; shTxt.fontStyle = FontStyle.Bold;
        shTxt.alignment = TextAnchor.MiddleCenter;
        shTxt.color = Color.white;
        shTxt.text = "📤 分享";
        shTxt.raycastTarget = false;

        // Primary button
        var primBtn = DesignSystem.MakePillButton("PrimaryBtn", card.transform, "继续", 0, 210, 46, _font, DesignSystem.AccentOrange);
        primBtn.GetComponent<RectTransform>().SetAnchor(Vector2.one * 0.5f, Vector2.one * 0.5f);
        primBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 55);
        foreach (Transform c in primBtn.transform) { var t = c.GetComponent<Text>(); if (t != null) t.text = "继续"; }
        var primB = primBtn.GetComponent<Button>();

        // Secondary button
        var secBtn = new GameObject("SecondaryBtn", typeof(RectTransform)); secBtn.transform.SetParent(card.transform, false);
        var srt = secBtn.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 0);
        srt.anchoredPosition = new Vector2(0, 12); srt.sizeDelta = new Vector2(210, 38);
        secBtn.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.94f);
        // Rounded corners
        secBtn.GetComponent<Image>().sprite = MakeWhiteSpriteWithRadius(19f);
        secBtn.GetComponent<Image>().type = Image.Type.Sliced;
        var secB = secBtn.AddComponent<Button>();
        var stgo = new GameObject("T", typeof(RectTransform)); stgo.transform.SetParent(secBtn.transform, false);
        stgo.GetComponent<RectTransform>().SetStretch();
        var sbt = stgo.AddComponent<Text>(); sbt.font = _font; sbt.fontSize = DesignSystem.FontResultBtn2;
        sbt.fontStyle = FontStyle.Bold; sbt.alignment = TextAnchor.MiddleCenter;
        sbt.color = DesignSystem.TextSecondary; sbt.text = "返回"; sbt.raycastTarget = false;

        // Revive button (share to continue — shown only in GameOver)
        var revGo = new GameObject("ReviveBtn", typeof(RectTransform));
        revGo.transform.SetParent(card.transform, false);
        var revrt = revGo.GetComponent<RectTransform>();
        revrt.anchorMin = new Vector2(0.5f, 0f);
        revrt.anchorMax = new Vector2(0.5f, 0f);
        revrt.anchoredPosition = new Vector2(0, 95);
        revrt.sizeDelta = new Vector2(210, 38);
        var revImg = revGo.AddComponent<Image>();
        revImg.color = new Color(0.30f, 0.59f, 0.82f);
        revImg.sprite = MakeWhiteSpriteWithRadius(19f);
        revImg.type = Image.Type.Sliced;
        var revShadow = revGo.AddComponent<Shadow>();
        revShadow.effectColor = DesignSystem.CardShadow;
        revShadow.effectDistance = new Vector2(0, 2);
        var revB = revGo.AddComponent<Button>();
        var revLabel = new GameObject("Label", typeof(RectTransform));
        revLabel.transform.SetParent(revGo.transform, false);
        revLabel.GetComponent<RectTransform>().SetStretch();
        var revTxt = revLabel.AddComponent<Text>();
        revTxt.font = _font; revTxt.fontSize = 14; revTxt.fontStyle = FontStyle.Bold;
        revTxt.alignment = TextAnchor.MiddleCenter;
        revTxt.color = Color.white;
        revTxt.text = "\U0001F4E4 分享复活";
        revTxt.raycastTarget = false;

        // Wire ResultPanel
        var rpComp = rp.GetComponent<ResultPanel>();
        var rpT = rpComp.GetType();
        rpT.GetField("titleText", _bf).SetValue(rpComp, card.transform.Find("Title").GetComponent<Text>());
        rpT.GetField("scoreText", _bf).SetValue(rpComp, card.transform.Find("Score").GetComponent<Text>());
        rpT.GetField("subText", _bf).SetValue(rpComp, card.transform.Find("SubInfo").GetComponent<Text>());
        rpT.GetField("failureMessageText", _bf).SetValue(rpComp, card.transform.Find("FailMsg").GetComponent<Text>());
        rpT.GetField("primaryButton", _bf).SetValue(rpComp, primB);
        rpT.GetField("secondaryButton", _bf).SetValue(rpComp, secB);
        rpT.GetField("reviveButton", _bf).SetValue(rpComp, revB);
        rpT.GetField("primaryButtonText", _bf).SetValue(rpComp, primBtn.transform.Find("Label").GetComponent<Text>());
        rpT.GetField("secondaryButtonText", _bf).SetValue(rpComp, stgo.GetComponent<Text>());
        rpT.GetField("reviveButtonText", _bf).SetValue(rpComp, revTxt);

        rp.SetActive(wasActive);
    }

    // ======================== HELPERS ========================
    private GameObject FindInactive(string name)
    {
        var r = GameObject.Find(name); if (r) return r;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var g in all) if (g.name == name && g.scene.IsValid()) return g;
        return null;
    }

    private GameObject Txt(string name, Transform parent, string text, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>(); t.font = _font; t.fontSize = size;
        t.text = text; t.color = color; t.raycastTarget = false; t.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    /// <summary>
    /// Creates a small secondary button with light background and dark text.
    /// </summary>
    private GameObject MakeSecondaryBtn(string name, Transform parent, string label, float x, float y)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(110, 42);

        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.88f);
        img.sprite = MakeWhiteSpriteWithRadius(21f);
        img.type = Image.Type.Sliced;

        go.AddComponent<Button>();

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.08f);
        shadow.effectDistance = new Vector2(0, 2);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        var t = labelGo.AddComponent<Text>();
        t.font = _font;
        t.fontSize = 13;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = DesignSystem.TextPrimary;
        t.text = label;
        t.raycastTarget = false;

        return go;
    }
}

#endif

public static class RectTransformExtensions
{
    public static void SetStretch(this RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; }
    public static void SetTop(this RectTransform rt, float y, float w, float h) { rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(w, h); }
    public static void SetAnchor(this RectTransform rt, Vector2 min, Vector2 max) { rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = Vector2.zero; }
}
