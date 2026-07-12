using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot editor tool: builds a clean home panel with 30-level grid.
/// Run via Tools menu or ContextMenu.
/// </summary>
public class HomeBuilder : MonoBehaviour
{
    [ContextMenu("Build Home")]
    public void Build()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("No Canvas found!"); return; }

        // Remove old
        var old = GameObject.Find("HomePanel");
        if (old != null) DestroyImmediate(old);

        // Create
        var home = new GameObject("HomePanel", typeof(RectTransform));
        home.transform.SetParent(canvas.transform, false);
        home.AddComponent<CanvasGroup>();
        home.AddComponent<Image>().color = new Color(0.996f, 0.976f, 0.941f);
        var hrt = home.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.sizeDelta = Vector2.zero;

        // Logo
        MakeText("Logo", home.transform, "🪜", 52, new Color(0.91f, 0.45f, 0.29f), false, font, 0, -80, 80, 80);
        // Title
        MakeText("Title", home.transform, "弹弹塔", 36, new Color(0.91f, 0.45f, 0.29f), true, font, 0, -150, 240, 50);
        // Subtitle
        MakeText("Subtitle", home.transform, "今天你堆了吗？", 15, new Color(0.5f, 0.45f, 0.33f), false, font, 0, -195, 200, 24);

        // CTA Button
        var cta = MakeButton("StartButton", home.transform, "🎮 开始游戏", 22, Color.white, new Color(0.96f, 0.45f, 0.21f), font);
        var ctaRT = cta.GetComponent<RectTransform>();
        ctaRT.anchorMin = new Vector2(0.5f, 0.5f); ctaRT.anchorMax = new Vector2(0.5f, 0.5f);
        ctaRT.anchoredPosition = new Vector2(0, 30); ctaRT.sizeDelta = new Vector2(240, 60);
        cta.GetComponent<Button>().onClick.AddListener(() => {
            home.SetActive(false);
            var gm = FindObjectOfType<GameManager>();
            var lm = FindObjectOfType<LevelManager>();
            var lvl = lm.GetLevel(1);
            gm.StartLevel(lvl.levelId, lvl.targetLayers, lvl.initialBlockWidth, lvl.baseSpeed, lvl.useInternalCurve, lvl.speedCurve);
        });

        // Level Grid
        var grid = new GameObject("LevelGrid", typeof(RectTransform));
        grid.transform.SetParent(home.transform, false);
        var grt = grid.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0, 0); grt.anchorMax = new Vector2(1, 0);
        grt.pivot = new Vector2(0.5f, 0);
        grt.anchoredPosition = new Vector2(0, 50);
        grt.sizeDelta = new Vector2(-30, 200);
        var gl = grid.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(50, 50);
        gl.spacing = new Vector2(6, 6);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 6;
        gl.childAlignment = TextAnchor.UpperCenter;

        for (int i = 1; i <= 30; i++)
        {
            int levelId = i;
            var lb = MakeButton("Lvl" + i, grid.transform, i.ToString(), 17, new Color(0.3f, 0.3f, 0.3f), new Color(1, 1, 1, 0.85f), font);
            lb.GetComponent<Button>().onClick.AddListener(() => {
                home.SetActive(false);
                var gm = FindObjectOfType<GameManager>();
                var lm = FindObjectOfType<LevelManager>();
                var lvl = lm.GetLevel(levelId);
                if (lvl != null) gm.StartLevel(lvl.levelId, lvl.targetLayers, lvl.initialBlockWidth, lvl.baseSpeed, lvl.useInternalCurve, lvl.speedCurve);
            });
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("HomePanel built successfully with 30-level grid.");
    }

    private void MakeText(string name, Transform parent, string text, int size, Color color, bool bold, Font font, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        var t = go.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
        t.color = color; t.text = text; t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyle.Bold;
    }

    private GameObject MakeButton(string name, Transform parent, string label, int fontSize, Color textColor, Color bgColor, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = bgColor;
        go.AddComponent<Button>();
        var tg = new GameObject("Text", typeof(RectTransform));
        tg.transform.SetParent(go.transform, false);
        var trt = tg.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;
        var t = tg.AddComponent<Text>();
        t.font = font; t.fontSize = fontSize; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = textColor; t.text = label; t.raycastTarget = false;
        return go;
    }
}
