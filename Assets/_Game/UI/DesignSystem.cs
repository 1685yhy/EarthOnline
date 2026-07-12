using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Design system constants and helpers for 弹弹塔 UI.
/// Based on the professional UI design spec.
/// </summary>
public static class DesignSystem
{
    // === Color Palette ===
    public static readonly Color BgPrimary       = new Color(1.000f, 0.973f, 0.941f); // #FFF8F0
    public static readonly Color BgSecondary     = new Color(1.000f, 0.949f, 0.878f); // #FFF2E0
    public static readonly Color AccentOrange    = new Color(0.961f, 0.451f, 0.212f); // #F57336
    public static readonly Color AccentOrangeDark= new Color(0.878f, 0.353f, 0.118f); // #E05A1E
    public static readonly Color AccentYellow    = new Color(1.000f, 0.722f, 0.000f); // #FFB800
    public static readonly Color TextPrimary     = new Color(0.239f, 0.180f, 0.118f); // #3D2E1E
    public static readonly Color TextSecondary   = new Color(0.545f, 0.451f, 0.333f); // #8B7355
    public static readonly Color TextMuted       = new Color(0.722f, 0.643f, 0.545f); // #B8A48B
    public static readonly Color CardBg          = new Color(1.000f, 1.000f, 1.000f, 0.93f);
    public static readonly Color CardShadow      = new Color(0.831f, 0.749f, 0.659f, 0.35f);
    public static readonly Color Overlay         = new Color(0.102f, 0.055f, 0.020f, 0.55f);
    public static readonly Color FailRed         = new Color(0.957f, 0.263f, 0.212f); // #F44336
    public static readonly Color ComboFire       = new Color(1.000f, 0.420f, 0.208f); // #FF6B35
    public static readonly Color GoldGlow        = new Color(1.000f, 0.420f, 0.420f, 0.30f); // glow for score
    public static readonly Color ScoreGold       = new Color(1.000f, 0.843f, 0.000f); // #FFD700

    // === Font Sizes ===
    public const int FontTitle       = 48;
    public const int FontSubtitle    = 16;
    public const int FontCardTitle   = 14;
    public const int FontRankNumber  = 28;
    public const int FontCTA         = 20;
    public const int FontBottomBtn   = 12;
    public const int FontScore       = 36;
    public const int FontScoreHUD    = 56;
    public const int FontCombo       = 18;
    public const int FontLevelName   = 13;
    public const int FontHint        = 15;
    public const int FontResultTitle = 26;
    public const int FontResultScore = 48;
    public const int FontFailMsg     = 15;
    public const int FontResultBtn   = 18;
    public const int FontResultBtn2  = 14;
    public const int FontStats       = 12;

    // === Rounded Corners ===
    public const float RadiusPill   = 30f;
    public const float RadiusCard   = 16f;
    public const float RadiusSmall  = 10f;
    public const float RadiusTiny   = 8f;

    // === Layout Reference ===
    public const float RefWidth  = 750f;
    public const float RefHeight = 1334f;

    // === Font ===
    private static Font _gameFont;
    public static Font GameFont
    {
        get
        {
            if (_gameFont == null)
            {
                _gameFont = Resources.Load<Font>("Fonts/wqy-zenhei");
                if (_gameFont == null)
                    _gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _gameFont;
        }
    }

    // === Helper: Make a styled card ===
    public static GameObject MakeCard(string name, Transform parent, Vector2 anchoredPos, Vector2 size, float radius = RadiusCard)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = CardBg;

        // Add shadow via Shadow component
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = CardShadow;
        shadow.effectDistance = new Vector2(0, 3);

        return go;
    }

    // === Helper: Make styled text ===
    public static GameObject MakeText(string name, Transform parent, string content, int fontSize, Color color, Font font, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.text = content;
        txt.raycastTarget = false;

        return go;
    }

    // === Helper: Make a pill button ===
    public static GameObject MakePillButton(string name, Transform parent, string label, float y, float width, float height, Font font, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();

        // Shadow
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = CardShadow;
        shadow.effectDistance = new Vector2(0, 2);

        // Label
        var tgo = new GameObject("Label", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        var t = tgo.AddComponent<Text>();
        t.font = font; t.fontSize = FontCTA; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        t.raycastTarget = false;

        return go;
    }

}
