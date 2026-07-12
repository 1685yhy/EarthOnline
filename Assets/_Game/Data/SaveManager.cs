using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public Dictionary<int, int> levelStars = new();
    public Dictionary<int, int> levelAttempts = new();
    public int totalGames;
    public int totalLayers;
    public int bestComboEver;
    public int totalPerfects;
    public List<string> unlockedSkins = new() { "candy" };
    public int dailyStreak;
    public string lastPlayDate = "";
    public List<string> unlockedAchievements = new();
    public Dictionary<string, int> dailyBest = new();
    public Dictionary<string, int> tools = new()
        { {"slow", 1}, {"widen", 1}, {"reverse", 1} };
    public int shareRefills;
    public string currentTheme = "candy";

    public int GetLevelStars(int id) => levelStars.GetValueOrDefault(id, 0);
    public void SetLevelStars(int id, int s)
    {
        if (s > GetLevelStars(id)) levelStars[id] = s;
    }
    public int MaxUnlockedLevel()
    {
        for (int i = 1; i <= 30; i++)
            if (GetLevelStars(i) < 1) return i;
        return 30;
    }
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private const string SAVE_KEY = "bounce_tower_save_v1";
    public SaveData Current { get; private set; }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json))
            Current = new SaveData();
        else
        {
            try { Current = JsonUtility.FromJson<SaveData>(json); }
            catch { Current = new SaveData(); }
        }
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(Current);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }
}
