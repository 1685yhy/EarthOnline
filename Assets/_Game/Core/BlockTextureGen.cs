using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates textured sprites for blocks at runtime.
/// Creates a rounded rectangle with a bright top edge highlight and bottom shadow
/// to give blocks a 3D appearance. Includes a subtle dark outline for definition.
/// Self-initializing singleton.
/// </summary>
public class BlockTextureGen : MonoBehaviour
{
    private static BlockTextureGen _instance;
    public static BlockTextureGen Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<BlockTextureGen>();
                if (_instance == null)
                {
                    var go = new GameObject("BlockTextureGen");
                    _instance = go.AddComponent<BlockTextureGen>();
                }
            }
            return _instance;
        }
    }

    private Dictionary<Color, Sprite> _cache = new();
    private const int TEX_W = 256;
    private const int TEX_H = 80; // Taller to fit outline + better gradient zone

    /// <summary>
    /// Get (or generate) a sprite for the given color.
    /// PPU is set so 1 world unit = 100 pixels — blocks won't stretch the texture.
    /// </summary>
    public Sprite GetBlockSprite(Color baseColor)
    {
        // Quantize color to reduce cache misses
        Color key = new Color(
            Mathf.Round(baseColor.r * 10f) / 10f,
            Mathf.Round(baseColor.g * 10f) / 10f,
            Mathf.Round(baseColor.b * 10f) / 10f,
            1f);

        if (_cache.TryGetValue(key, out var s)) return s;

        var tex = GenerateBlockTexture(key);
        // PPU=100: a 4.5-wide block uses 450 texture pixels across a 256-wide tex, so it's still
        // slightly stretched but much better than PPU=64. The bilinear filter will handle it.
        s = Sprite.Create(tex, new Rect(0, 0, TEX_W, TEX_H), new Vector2(0.5f, 0.5f), 100);
        s.name = "Block_" + ColorUtility.ToHtmlStringRGB(key);
        _cache[key] = s;
        return s;
    }

    private Texture2D GenerateBlockTexture(Color c)
    {
        int w = TEX_W, h = TEX_H;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // Outline color — slightly darkened base
        Color outlineColor = Color.Lerp(c, Color.black, 0.35f);
        // Compute color zones (adjusted for new texture height)
        Color topEdge = Color.Lerp(c, Color.white, 0.55f);  // bright top edge
        Color highlight = Color.Lerp(c, Color.white, 0.25f); // soft highlight below edge
        Color mid = c;
        Color shadow = Color.Lerp(c, Color.black, 0.20f);
        Color bottomEdge = Color.Lerp(c, Color.black, 0.40f); // dark bottom line

        int radius = 16; // Slightly smaller radius
        int outlineWidth = 2; // 2-pixel outline

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Rounded corner mask with smooth (distance-based) alpha
                float alpha = 1f;
                float cornerDist = 0f;
                bool inCorner = false;

                if (x < radius && y < radius)
                {
                    cornerDist = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius));
                    inCorner = true;
                }
                else if (x >= w - radius && y < radius)
                {
                    cornerDist = Mathf.Sqrt((x - (w - 1 - radius)) * (x - (w - 1 - radius)) + (y - radius) * (y - radius));
                    inCorner = true;
                }
                else if (x < radius && y >= h - radius)
                {
                    cornerDist = Mathf.Sqrt((x - radius) * (x - radius) + (y - (h - 1 - radius)) * (y - (h - 1 - radius)));
                    inCorner = true;
                }
                else if (x >= w - radius && y >= h - radius)
                {
                    cornerDist = Mathf.Sqrt((x - (w - 1 - radius)) * (x - (w - 1 - radius)) + (y - (h - 1 - radius)) * (y - (h - 1 - radius)));
                    inCorner = true;
                }

                if (inCorner)
                {
                    if (cornerDist > radius)
                        alpha = 0f;
                    else if (cornerDist > radius - 2f)
                        alpha = 1f - (cornerDist - (radius - 2f)) / 2f;
                }

                // Determine if this pixel is in the outline band (non-corner edges)
                bool isOutline = false;
                if (alpha > 0)
                {
                    // Check if we're in the 2px border from any edge
                    bool nearLeft = x < outlineWidth;
                    bool nearRight = x >= w - outlineWidth;
                    bool nearTop = y < outlineWidth;
                    bool nearBottom = y >= h - outlineWidth;

                    if (!inCorner)
                    {
                        isOutline = nearLeft || nearRight || nearTop || nearBottom;
                    }
                    else
                    {
                        // In corners: outline is where cornerDist > (radius - outlineWidth)
                        isOutline = cornerDist > (radius - outlineWidth) && cornerDist <= radius;
                    }
                }

                if (isOutline && alpha > 0)
                {
                    tex.SetPixel(x, y, outlineColor);
                    continue;
                }

                if (alpha <= 0)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Vertical color zones for 3D appearance
                Color pixel;
                float yNorm = (float)y / h;

                if (yNorm < 0.07f) // Top ~7% — bright edge highlight
                {
                    float t = yNorm / 0.07f;
                    pixel = Color.Lerp(topEdge, highlight, t);
                }
                else if (yNorm < 0.18f) // 7-18% — gradient from highlight to mid
                {
                    float t = (yNorm - 0.07f) / 0.11f;
                    pixel = Color.Lerp(highlight, mid, t);
                }
                else if (yNorm < 0.82f) // 18-82% — flat mid color
                {
                    pixel = mid;
                }
                else if (yNorm < 0.93f) // 82-93% — gradient to shadow
                {
                    float t = (yNorm - 0.82f) / 0.11f;
                    pixel = Color.Lerp(mid, shadow, t);
                }
                else // Bottom ~7% — dark edge
                {
                    float t = (yNorm - 0.93f) / 0.07f;
                    pixel = Color.Lerp(shadow, bottomEdge, t);
                }

                pixel.a = alpha;
                tex.SetPixel(x, y, pixel);
            }
        }

        // Bright top edge line (y = 3, just inside the outline)
        for (int x = radius; x < w - radius; x++)
        {
            var p = tex.GetPixel(x, 3);
            p = Color.Lerp(p, Color.white, 0.5f);
            p.a = 1f;
            tex.SetPixel(x, 3, p);
        }

        // Subtle shadow at bottom edge
        for (int x = radius; x < w - radius; x++)
        {
            var p = tex.GetPixel(x, h - (int)(radius * 0.5f));
            p = Color.Lerp(p, Color.black, 0.25f);
            p.a = 1f;
            tex.SetPixel(x, h - (int)(radius * 0.5f), p);
        }

        tex.Apply();
        return tex;
    }
}
