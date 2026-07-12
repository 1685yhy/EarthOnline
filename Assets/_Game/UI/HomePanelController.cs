using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HomePanelController : MonoBehaviour
{
    private GameManager gm;
    private LevelManager lm;
    private Canvas canvas;

    private List<Button> levelButtons = new List<Button>();
    private Button startButton;
    private Button settingsBtn;

    private void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        lm = FindObjectOfType<LevelManager>();
        if (lm != null && lm.MaxLevel == 0) lm.GenerateDefaultLevels();

        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        // Build the home screen immediately to avoid blank flash
        BuildHome();
    }

    private void BuildHome()
    {
        // Clear existing
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);

        // Background: warm cream
        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        var bgrt = bg.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.996f, 0.976f, 0.941f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // === Logo emoji ===
        var logo = MakeText("Logo", transform, "🪜", 52, FontStyle.Normal,
            new Color(0.91f, 0.45f, 0.29f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -80), new Vector2(80, 80));

        // === Title ===
        MakeText("Title", transform, "弹弹塔", 36, FontStyle.Bold,
            new Color(0.91f, 0.45f, 0.29f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -150), new Vector2(240, 50));

        // === Subtitle ===
        MakeText("Subtitle", transform, "今天你堆了吗？", 15, FontStyle.Normal,
            new Color(0.5f, 0.45f, 0.33f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -195), new Vector2(200, 24));

        // === Settings gear (top-right) ===
        settingsBtn = MakeButton("SettingsBtn", transform, "⚙", 22, Color.white,
            new Color(0.5f, 0.5f, 0.5f, 0.5f),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20, -20), new Vector2(44, 44));
        settingsBtn.onClick.AddListener(() =>
        {
            // Toggle settings panel
            var sp = FindObjectOfType<SettingsPanel>();
            if (sp != null) sp.gameObject.SetActive(!sp.gameObject.activeSelf);
        });

        // === CTA Button "🎮 开始游戏" (most prominent) ===
        startButton = MakeButton("StartButton", transform, "🎮 开始游戏", 22, Color.white,
            new Color(0.96f, 0.45f, 0.21f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 40), new Vector2(240, 60));
        startButton.onClick.AddListener(() => StartLevel(1));

        // === Level Grid (30 levels, 6 columns, below CTA) ===
        var gridGo = new GameObject("LevelGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(transform, false);
        var grt = gridGo.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0, 0);
        grt.anchorMax = new Vector2(1, 0);
        grt.pivot = new Vector2(0.5f, 0);
        grt.anchoredPosition = new Vector2(0, 60);
        grt.sizeDelta = new Vector2(-30, 280);

        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(50, 50);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperCenter;

        levelButtons.Clear();
        for (int i = 1; i <= 30; i++)
        {
            int levelId = i;
            var btn = MakeButton("Lvl" + i, gridGo.transform, i.ToString(), 17,
                new Color(0.3f, 0.3f, 0.3f), new Color(1, 1, 1, 0.85f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(50, 50));
            btn.onClick.AddListener(() => StartLevel(levelId));
            levelButtons.Add(btn);
        }

        // Refresh grid lock states
        RefreshLevelGrid();
    }

    private void RefreshLevelGrid()
    {
        var save = SaveManager.Instance?.Current;
        int maxUnlocked = save != null ? save.MaxUnlockedLevel() : 1;

        var grid = transform.Find("LevelGrid");
        if (grid == null) return;

        for (int i = 0; i < grid.childCount && i < 30; i++)
        {
            var btn = grid.GetChild(i);
            int levelId = i + 1;
            bool unlocked = levelId <= maxUnlocked;
            int stars = save != null ? save.GetLevelStars(levelId) : 0;

            var img = btn.GetComponent<Image>();
            var button = btn.GetComponent<Button>();
            if (img != null)
            {
                if (!unlocked)
                {
                    img.color = new Color(0.85f, 0.85f, 0.85f, 0.4f);
                    if (button != null) button.interactable = false;
                }
                else if (stars >= 1)
                {
                    img.color = new Color(0.94f, 1f, 0.94f);
                    if (button != null) button.interactable = true;
                }
                else
                {
                    img.color = new Color(1f, 1f, 1f, 0.95f);
                    if (button != null) button.interactable = true;
                }
            }
        }
    }

    private void OnEnable()
    {
        RefreshLevelGrid();
    }

    private void Update()
    {
        // Gentle pulse on start button
        if (startButton != null)
        {
            float t = (1f - Mathf.Cos(Time.unscaledTime * Mathf.PI)) / 2f;
            float scale = Mathf.Lerp(1f, 1.04f, t);
            startButton.transform.localScale = Vector3.one * scale;
        }
    }

    private void StartLevel(int id)
    {
        gameObject.SetActive(false);
        var level = lm.GetLevel(id);
        if (level == null) return;
        AudioManager.Instance?.PlaySFX(SfxType.UIClick);
        gm.StartLevel(level.levelId, level.targetLayers,
            level.initialBlockWidth, level.baseSpeed,
            level.useInternalCurve, level.speedCurve);
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

    private Button MakeButton(string name, Transform parent, string label, int fontSize, Color textColor, Color bgColor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;

        var t = labelGo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = textColor;
        t.text = label;
        t.raycastTarget = false;
        return btn;
    }
}
