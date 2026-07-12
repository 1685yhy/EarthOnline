using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ThemeDef
{
    public string id;
    public string name;
    public string emoji;
    public Color[] bgColors = new Color[3];
    public Color[][] blockPalettes;
    public int unlockLevel;
    public int unlockShares;
}

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    [SerializeField] private List<ThemeDef> themes;
    private string currentThemeId = "candy";

    public ThemeDef CurrentTheme => themes.Find(t => t.id == currentThemeId);
    public List<ThemeDef> AllThemes => themes;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (themes == null || themes.Count == 0)
        {
            themes = new List<ThemeDef>
            {
                new() { id="candy", name="糖果乐园", emoji="🍬", bgColors=new[]{new Color(1f,0.973f,0.941f),new Color(0.996f,0.965f,0.906f),new Color(0.996f,0.976f,0.941f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(1f,0.329f,0.439f),new Color(0f,0.808f,0.788f),new Color(0.424f,0.361f,0.906f),new Color(0.992f,0.796f,0.431f),new Color(0f,0.722f,0.580f),new Color(0.992f,0.475f,0.659f)}},
                    unlockLevel=0 },
                new() { id="ocean", name="晴空万里", emoji="🌊", bgColors=new[]{new Color(0.91f,0.96f,0.99f),new Color(0.86f,0.93f,0.98f),new Color(0.91f,0.96f,0.99f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(0.2f,0.6f,1f),new Color(0f,0.4f,0.8f),new Color(0.4f,0.8f,1f),new Color(0.1f,0.5f,0.9f),new Color(0.3f,0.7f,1f),new Color(0f,0.6f,0.9f)}},
                    unlockLevel=5 },
                new() { id="sunset", name="橘子汽水", emoji="🍊", bgColors=new[]{new Color(1f,0.96f,0.94f),new Color(1f,0.91f,0.86f),new Color(1f,0.96f,0.94f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(1f,0.6f,0.2f),new Color(1f,0.4f,0.1f),new Color(1f,0.7f,0.4f),new Color(0.9f,0.3f,0f),new Color(1f,0.5f,0.3f),new Color(1f,0.8f,0.5f)}},
                    unlockLevel=10 },
                new() { id="forest", name="抹茶森林", emoji="🍵", bgColors=new[]{new Color(0.94f,0.98f,0.95f),new Color(0.89f,0.96f,0.91f),new Color(0.94f,0.98f,0.95f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(0.3f,0.7f,0.3f),new Color(0.2f,0.6f,0.2f),new Color(0.4f,0.8f,0.4f),new Color(0.1f,0.5f,0.1f),new Color(0.5f,0.9f,0.5f),new Color(0.3f,0.6f,0.3f)}},
                    unlockLevel=15 },
                new() { id="neon", name="赛博乐园", emoji="🌃", bgColors=new[]{new Color(0.96f,0.94f,1f),new Color(0.93f,0.88f,1f),new Color(0.96f,0.94f,1f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(1f,0.1f,0.6f),new Color(0f,1f,0.8f),new Color(0.6f,0.2f,1f),new Color(0f,0.8f,1f),new Color(1f,0.4f,0.7f),new Color(0.3f,1f,0.6f)}},
                    unlockLevel=20 },
                new() { id="gold", name="奶油爆米花", emoji="🍿", bgColors=new[]{new Color(1f,0.996f,0.96f),new Color(1f,0.97f,0.88f),new Color(1f,0.996f,0.96f)},
                    blockPalettes=new Color[][]{new Color[]{new Color(1f,0.84f,0f),new Color(1f,0.76f,0f),new Color(1f,0.9f,0.4f),new Color(0.9f,0.7f,0f),new Color(1f,0.8f,0.2f),new Color(0.95f,0.88f,0.5f)}},
                    unlockLevel=30 },
            };
        }
        // Load saved theme
        var save = SaveManager.Instance?.Current;
        if (save != null && !string.IsNullOrEmpty(save.currentTheme))
            currentThemeId = save.currentTheme;
        ApplyTheme(currentThemeId);
    }

    public void SetTheme(string id)
    {
        if (!IsUnlocked(id)) return;
        currentThemeId = id;
        ApplyTheme(id);
        var save = SaveManager.Instance?.Current;
        if (save != null) { save.currentTheme = id; SaveManager.Instance.Save(); }
    }

    private void ApplyTheme(string id)
    {
        var theme = themes.Find(t => t.id == id);
        if (theme == null) return;
        Camera.main.backgroundColor = theme.bgColors[0];

        // Update CameraController's gradient background if available
        var camCtrl = Camera.main.GetComponent<CameraController>();
        if (camCtrl != null)
        {
            // Background applied via Camera.main.backgroundColor
            if (theme.bgColors.Length > 0) Camera.main.backgroundColor = theme.bgColors[0];
            // Update the gradient background quad
            camCtrl.UpdateBackgroundGradient();
        }

        // Update BlockSpawner's block colors to match theme
        if (theme.blockPalettes != null && theme.blockPalettes.Length > 0 && theme.blockPalettes[0] != null)
        {
            var spawner = FindObjectOfType<BlockSpawner>();
            if (spawner != null)
                spawner.SetBlockPalette(theme.blockPalettes[0]);
        }
    }

    public bool IsUnlocked(string id)
    {
        var theme = themes.Find(t => t.id == id);
        if (theme == null) return false;
        if (theme.unlockLevel == 0) return true;
        return SaveManager.Instance?.Current.MaxUnlockedLevel() >= theme.unlockLevel;
    }

    public Color GetRandomBlockColor()
    {
        // Return a random color — use the same palette as BlockSpawner for consistency
        Color[] palette = new Color[] {
            new Color(1f, 0.329f, 0.439f),  // #FF5470 coral
            new Color(0f, 0.808f, 0.788f),  // #00CEC9 teal
            new Color(0.424f, 0.361f, 0.906f), // #6C5CE7 violet
            new Color(0.992f, 0.796f, 0.431f), // #FDCB6E gold
            new Color(0f, 0.722f, 0.580f),  // #00B894 emerald
            new Color(0.992f, 0.475f, 0.659f), // #FD79A8 pink
        };
        return palette[Random.Range(0, palette.Length)];
    }
}
