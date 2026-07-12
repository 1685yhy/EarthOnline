using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private List<LevelData> allLevels = new();

    [System.Serializable]
    public class LevelData
    {
        public int levelId;
        public string levelName;
        public int targetLayers;
        public float baseSpeed = 3f;
        public float initialBlockWidth = 4.5f;
        public string description;
        public float speedMul = 1f;
        public float zoneExtraMul = 2.5f;
        public bool useInternalCurve;
        public AnimationCurve speedCurve = AnimationCurve.Constant(0, 1, 1);
    }

    private void Awake() => Instance = this;

    public LevelData GetLevel(int id)
        => allLevels.Find(l => l.levelId == id);

    public int MaxLevel => allLevels.Count;

    // Exact 30-level definitions from original H5 game-engine.js
    public void GenerateDefaultLevels()
    {
        if (allLevels == null) allLevels = new List<LevelData>();
        allLevels.Clear();

        float configBaseSpeed = GameManager.Instance?.Config?.baseSpeed ?? 5f;
        float configBaseWidth = GameManager.Instance?.Config?.initialBlockWidth ?? 2.8f;

        var rawLevels = new (int id, string name, int target, float speedMul, string desc)[]
        {
            (1,  "你好世界",    2,  0.3f, "闭着眼都能过"),
            (2,  "地狱之门",    5,  1.8f, "第2关 · 90%死在这"),
            (3,  "才刚开始",    5,  1.4f, "继续继续"),
            (4,  "升温",        6,  1.6f, "手开始冒汗"),
            (5,  "别放松",      6,  1.3f, "下一关更难"),
            (6,  "步步惊心",    8,  1.7f, "集中注意力"),
            (7,  "稍微缓缓",    8,  1.4f, "别被骗了"),
            (8,  "加速",       10,  1.8f, "手指在发抖"),
            (9,  "稳住",       10,  1.5f, "你能行的"),
            (10, "分水岭",     12,  1.9f, "⭐ 半数玩家在此止步"),
            (11, "悬崖勒马",   12,  1.5f, "快用道具"),
            (12, "真刀真枪",   15,  1.9f, "超过70%玩家"),
            (13, "喘息之间",   15,  1.6f, "让你歇一秒"),
            (14, "地狱深处",   18,  2.0f, "只有30%能过"),
            (15, "里程碑",     18,  1.7f, "⭐ 你已经很强了"),
            (16, "精英门槛",   20,  2.0f, "前20%玩家"),
            (17, "暴风雨前",   20,  1.7f, "给你歇口气"),
            (18, "极限挑战",   24,  2.2f, "只有10%能看到这"),
            (19, "绿洲",       24,  1.8f, "最后的温柔"),
            (20, "生死线",     28,  2.4f, "⭐ 前5%·传说门槛"),
            (21, "大师入门",   28,  2.0f, "大师联赛"),
            (22, "狂风暴雨",   32,  2.5f, "确定还要继续？"),
            (23, "休整",       32,  2.1f, "最后休息站"),
            (24, "不归路",     38,  2.7f, "只有1%能看到这"),
            (25, "传奇",       38,  2.3f, "⭐ 前1%·炫耀资格"),
            (26, "神之试炼",   45,  2.8f, "0.1%通关率"),
            (27, "天堑",       45,  2.5f, "难以置信"),
            (28, "登天之路",   55,  3.0f, "传奇诞生中"),
            (29, "最后之门",   55,  2.7f, "没有人相信"),
            (30, "弹弹之神",   65,  3.5f, "👑 0.001%·你是神"),
        };

        // IBW formula: max(70, min(W*0.5, 200) - (levelId-1)*5) in pixels
        // Converted to world units: IBW_wu = max(0.98, 2.8 - (id-1) * 0.07)
        float GetIBW(int levelId)
        {
            if (levelId == 1) return configBaseWidth;
            return Mathf.Max(0.98f, configBaseWidth - (levelId - 1) * 0.07f);
        }

        foreach (var r in rawLevels)
        {
            float zoneMul = r.id == 1 ? 0.05f : 2.5f;
            allLevels.Add(new LevelData
            {
                levelId = r.id,
                levelName = r.name,
                targetLayers = r.target,
                baseSpeed = configBaseSpeed * r.speedMul,
                initialBlockWidth = GetIBW(r.id),
                description = r.desc,
                speedMul = r.speedMul,
                zoneExtraMul = zoneMul,
                useInternalCurve = false,
                speedCurve = AnimationCurve.Constant(0, 1, 1),
            });
        }
    }
}
