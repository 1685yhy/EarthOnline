using UnityEngine;
using System.Collections.Generic;

public enum ToolType { Slow, Widen, Reverse, Undo, Freeze }

public class ToolSystem : MonoBehaviour
{
    public static ToolSystem Instance { get; private set; }

    [System.Serializable]
    public struct ToolConfig
    {
        public ToolType type;
        public string displayName;
        public string emoji;
        public int initialCount;
        public int maxPerGame;
        public int unlockLevel;
    }

    [SerializeField] private List<ToolConfig> toolConfigs;
    private Dictionary<ToolType, int> counts = new();
    private Dictionary<ToolType, int> usedThisGame = new();

    private void Awake() => Instance = this;

    private void Start()
    {
        // Default configs if not set in inspector
        if (toolConfigs == null || toolConfigs.Count == 0)
        {
            toolConfigs = new List<ToolConfig>
            {
                new ToolConfig { type = ToolType.Slow, displayName = "慢动作", emoji = "🐢", initialCount = 1, maxPerGame = 3, unlockLevel = 0 },
                new ToolConfig { type = ToolType.Widen, displayName = "加宽", emoji = "📏", initialCount = 1, maxPerGame = 3, unlockLevel = 0 },
                new ToolConfig { type = ToolType.Reverse, displayName = "反转", emoji = "🔄", initialCount = 1, maxPerGame = 5, unlockLevel = 0 },
            };
        }
        ResetForNewGame();
    }

    public void ResetForNewGame()
    {
        usedThisGame.Clear();
        counts.Clear();
        foreach (var tc in toolConfigs)
            counts[tc.type] = tc.initialCount;
    }

    public bool UseTool(ToolType type)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return false;
        if (counts.GetValueOrDefault(type, 0) <= 0) return false;
        if (usedThisGame.GetValueOrDefault(type, 0) >= GetConfig(type).maxPerGame) return false;
        int unlockLv = GetConfig(type).unlockLevel;
        if (unlockLv > 0 && GameManager.Instance.CurrentLevelId < unlockLv) return false;

        counts[type]--;
        usedThisGame[type] = usedThisGame.GetValueOrDefault(type, 0) + 1;
        ExecuteTool(type);
        AudioManager.Instance?.PlaySFX(SfxType.Tool);
        return true;
    }

    private Coroutine slowMotionCoroutine;

    private void ExecuteTool(ToolType type)
    {
        var bs = Object.FindObjectOfType<BlockSpawner>();
        var sm = Object.FindObjectOfType<StackManager>();

        switch (type)
        {
            case ToolType.Slow:
                bs?.SetSlowMotion(true);
                if (slowMotionCoroutine != null) StopCoroutine(slowMotionCoroutine);
                slowMotionCoroutine = StartCoroutine(EndSlowMotion());
                break;
            case ToolType.Widen:
                sm?.ResetTopWidth();
                break;
            case ToolType.Reverse:
                bs?.ReverseDirection();
                break;
        }
    }

    private System.Collections.IEnumerator EndSlowMotion()
    {
        yield return new WaitForSeconds(5f);
        var bs = Object.FindObjectOfType<BlockSpawner>();
        bs?.SetSlowMotion(false);
    }

    public int GetCount(ToolType type) => counts.GetValueOrDefault(type, 0);
    public void AddTool(ToolType type, int amount)
    {
        if (!counts.ContainsKey(type)) counts[type] = 0;
        counts[type] = Mathf.Min(counts[type] + amount, 99);
    }
    private ToolConfig GetConfig(ToolType type) => toolConfigs.Find(tc => tc.type == type);
}
