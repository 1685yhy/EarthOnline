using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AchievementDef
{
    public string id;
    public string name;
    public string description;
    public string icon;
    public enum ConditionType { TotalGames, LevelPassed, ComboReached, TotalLayers, AllSkins, LayerInOneGame }
    public ConditionType condition;
    public int threshold;
}

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    [SerializeField] private List<AchievementDef> allAchievements;
    private HashSet<string> unlocked = new();

    private void Awake() => Instance = this;

    private void Start()
    {
        if (allAchievements == null || allAchievements.Count == 0)
        {
            allAchievements = new List<AchievementDef>
            {
                new() { id="first_game", name="初次尝试", description="完成第一局", icon="🎮", condition=AchievementDef.ConditionType.TotalGames, threshold=1 },
                new() { id="level_5", name="小有所成", description="通过第5关", icon="⭐", condition=AchievementDef.ConditionType.LevelPassed, threshold=5 },
                new() { id="level_10", name="渐入佳境", description="通过第10关", icon="🌟", condition=AchievementDef.ConditionType.LevelPassed, threshold=10 },
                new() { id="level_15", name="势不可挡", description="通过第15关", icon="💫", condition=AchievementDef.ConditionType.LevelPassed, threshold=15 },
                new() { id="level_20", name="大师风范", description="通过第20关", icon="🏆", condition=AchievementDef.ConditionType.LevelPassed, threshold=20 },
                new() { id="level_25", name="登峰造极", description="通过第25关", icon="👑", condition=AchievementDef.ConditionType.LevelPassed, threshold=25 },
                new() { id="level_30", name="弹弹之神", description="通关30关", icon="🗼", condition=AchievementDef.ConditionType.LevelPassed, threshold=30 },
                new() { id="combo_5", name="手感火热", description="达成5连击", icon="🔥", condition=AchievementDef.ConditionType.ComboReached, threshold=5 },
                new() { id="combo_10", name="人机合一", description="达成10连击", icon="⚡", condition=AchievementDef.ConditionType.ComboReached, threshold=10 },
                new() { id="combo_20", name="神之手", description="达成20连击", icon="👑", condition=AchievementDef.ConditionType.ComboReached, threshold=20 },
                new() { id="layer_50", name="摩天大楼", description="单局50层", icon="🏢", condition=AchievementDef.ConditionType.LayerInOneGame, threshold=50 },
                new() { id="layer_100", name="通天塔", description="单局100层", icon="🗼", condition=AchievementDef.ConditionType.LayerInOneGame, threshold=100 },
                new() { id="games_50", name="铁粉", description="累计玩50局", icon="💎", condition=AchievementDef.ConditionType.TotalGames, threshold=50 },
                new() { id="total_1000", name="堆叠大师", description="累计1000层", icon="🏆", condition=AchievementDef.ConditionType.TotalLayers, threshold=1000 },
                new() { id="skins_all", name="皮肤收藏家", description="解锁全部皮肤", icon="🌈", condition=AchievementDef.ConditionType.AllSkins, threshold=6 },
            };
        }
        // Load unlocked from SaveManager
        var save = SaveManager.Instance?.Current;
        if (save != null)
            foreach (var id in save.unlockedAchievements)
                unlocked.Add(id);
    }

    public List<AchievementDef> CheckAndUnlock()
    {
        var newly = new List<AchievementDef>();
        var save = SaveManager.Instance?.Current;
        if (save == null) return newly;

        foreach (var a in allAchievements)
        {
            if (unlocked.Contains(a.id)) continue;
            bool met = false;
            switch (a.condition)
            {
                case AchievementDef.ConditionType.TotalGames:
                    met = save.totalGames >= a.threshold; break;
                case AchievementDef.ConditionType.LevelPassed:
                    met = save.MaxUnlockedLevel() >= a.threshold; break;
                case AchievementDef.ConditionType.ComboReached:
                    met = save.bestComboEver >= a.threshold; break;
                case AchievementDef.ConditionType.TotalLayers:
                    met = save.totalLayers >= a.threshold; break;
                case AchievementDef.ConditionType.LayerInOneGame:
                    met = GameManager.Instance.CurrentScore >= a.threshold; break;
                case AchievementDef.ConditionType.AllSkins:
                    met = save.unlockedSkins.Count >= a.threshold; break;
            }
            if (met)
            {
                unlocked.Add(a.id);
                save.unlockedAchievements.Add(a.id);
                newly.Add(a);
                // Show achievement toast
                UIManager.Instance?.HUD?.ShowAchievement(a.name);
            }
        }
        if (newly.Count > 0) SaveManager.Instance.Save();
        return newly;
    }
}
