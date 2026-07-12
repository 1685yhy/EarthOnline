using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private StackManager stackManager;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float lookAheadOffset = 2f;

    private Camera cam;
    private float targetY;
    private float velocityY;
    private float initialY;

    // Background gradient quad
    private GameObject bgQuad;
    private SpriteRenderer bgSr;
    private Texture2D bgTex;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.996f, 0.976f, 0.941f); // #fef9f0 warm cream

        float targetGameWidth = 8f;
        float aspect = (float)Mathf.Max(Screen.width, 1) / Mathf.Max(Screen.height, 1);
        cam.orthographicSize = targetGameWidth / (2f * aspect);

        initialY = transform.position.y;
        CreateBackgroundGradient();
    }

    private void LateUpdate()
    {
        if (stackManager == null) return;

        float stackTopY = stackManager.GetTopY();
        targetY = stackTopY - lookAheadOffset;
        targetY = Mathf.Max(initialY, targetY);

        float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref velocityY, smoothTime);
        transform.position = new Vector3(0, newY, -10);
    }

    /// <summary>
    /// Create a full-screen background quad with a 3-color vertical gradient.
    /// Uses theme bgColors[0] (top), bgColors[1] (middle), bgColors[2] (bottom).
    /// </summary>
    private void CreateBackgroundGradient()
    {
        bgQuad = new GameObject("BackgroundGradient");
        bgSr = bgQuad.AddComponent<SpriteRenderer>();

        // Create a tall 1-pixel-wide gradient texture
        int texHeight = 128;
        bgTex = new Texture2D(1, texHeight, TextureFormat.RGBA32, false);
        bgTex.wrapMode = TextureWrapMode.Clamp;
        bgTex.filterMode = FilterMode.Bilinear;

        // Get theme colors or fallback defaults
        var theme = ThemeManager.Instance?.CurrentTheme;
        Color topC = theme != null && theme.bgColors.Length > 0
            ? theme.bgColors[0] : new Color(0.996f, 0.976f, 0.941f);
        Color midC = theme != null && theme.bgColors.Length > 1
            ? theme.bgColors[1] : topC;
        Color botC = theme != null && theme.bgColors.Length > 2
            ? theme.bgColors[2] : midC;

        for (int y = 0; y < texHeight; y++)
        {
            float t = y / (float)(texHeight - 1);
            Color c;
            if (t < 0.5f)
                c = Color.Lerp(topC, midC, t * 2f);
            else
                c = Color.Lerp(midC, botC, (t - 0.5f) * 2f);
            bgTex.SetPixel(0, y, c);
        }
        bgTex.Apply();

        bgSr.sprite = Sprite.Create(bgTex,
            new Rect(0, 0, 1, texHeight), new Vector2(0.5f, 0.5f), 100);
        bgSr.sortingOrder = -10; // Behind everything

        // Parent to camera and size to fill view
        bgQuad.transform.SetParent(transform, false);
        FitBackgroundToView();
    }

    /// <summary>
    /// Size the background quad to fill the camera view.
    /// </summary>
    private void FitBackgroundToView()
    {
        if (bgQuad == null) return;
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        bgQuad.transform.localPosition = new Vector3(0, 0, 5);
        bgQuad.transform.localScale = new Vector3(width, height, 1);
    }

    /// <summary>
    /// Called by ThemeManager when theme changes.
    /// Regenerates the gradient texture with new theme bg colors.
    /// </summary>
    public void UpdateBackgroundGradient()
    {
        if (bgTex == null || bgSr == null) return;

        var theme = ThemeManager.Instance?.CurrentTheme;
        if (theme == null) return;

        Color topC = theme.bgColors.Length > 0 ? theme.bgColors[0] : Color.white;
        Color midC = theme.bgColors.Length > 1 ? theme.bgColors[1] : topC;
        Color botC = theme.bgColors.Length > 2 ? theme.bgColors[2] : midC;

        int texHeight = bgTex.height;
        for (int y = 0; y < texHeight; y++)
        {
            float t = y / (float)(texHeight - 1);
            Color c;
            if (t < 0.5f)
                c = Color.Lerp(topC, midC, t * 2f);
            else
                c = Color.Lerp(midC, botC, (t - 0.5f) * 2f);
            bgTex.SetPixel(0, y, c);
        }
        bgTex.Apply();

        FitBackgroundToView();
    }
}
