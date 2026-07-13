using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EarthOnline.Framework
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  JSON Serialization Types
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>JSON 根结构: { "quests": [...] }</summary>
    [Serializable]
    public class QuestDatabaseJson
    {
        public QuestJsonEntry[] quests;
    }

    /// <summary>JSON 单条任务条目</summary>
    [Serializable]
    public class QuestJsonEntry
    {
        public string questId;
        public string title;
        public string description;
        public string chapter;
        public string[] prerequisites;
        public QuestObjectiveJson[] objectives;
        public QuestRewardJson rewards;
        public bool isMainQuest;
        public string unlockCondition;
        public string giverNpcId;
        public string giverName;
        public string completionText;

        /// <summary>
        /// 转换为运行时 QuestData 并注册到 QuestManager.
        /// 将 JSON 中的扩展字段映射为 QuestData 的窄字段:
        ///   - 解析第一个 objective 的 completionCondition DSL → (type, targetId, targetCount)
        ///   - rewards → (rewardSpiritStones, rewardCultivation, rewardItemId)
        /// </summary>
        public QuestData ToQuestData()
        {
            var qd = new QuestData
            {
                id = questId,
                title = title,
                description = description,
                giverNpcId = giverNpcId ?? "",
                giverName = giverName ?? giverNpcId ?? "",
                completionText = completionText ?? "",
                rewardSpiritStones = rewards?.spiritStones ?? 0,
                rewardCultivation = rewards?.cultivationXP ?? 0
            };

            // Single item reward (QuestData only supports one)
            if (rewards?.itemIds != null && rewards.itemIds.Length > 0)
                qd.rewardItemId = rewards.itemIds[0];

            // Parse first objective for QuestManager tracking fields
            if (objectives != null && objectives.Length > 0)
            {
                var parsed = ParseCompletionCondition(objectives[0].completionCondition);
                qd.type = parsed.type;
                qd.targetId = parsed.targetId;
                qd.targetCount = parsed.targetCount;
            }

            return qd;
        }

        /// <summary>
        /// 完成条件 DSL 解析器.
        /// 支持的指令:
        ///   talk_npc:&lt;npcId&gt;           → Talk,   targetId=npcId,   count=1
        ///   kill:&lt;enemyId&gt;:&lt;count&gt;      → Combat, targetId=enemyId, count=N
        ///   collect:&lt;itemId&gt;:&lt;count&gt;    → Collect,targetId=itemId, count=N
        ///   explore:&lt;location&gt;          → Explore,targetId=location,count=1
        ///   boss:&lt;bossId&gt;               → Boss,   targetId=bossId,  count=1
        /// </summary>
        private static (QuestType type, string targetId, int targetCount) ParseCompletionCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                Debug.LogWarning("[QuestDataLoader] completionCondition 为空, 默认 Guidance");
                return (QuestType.Guidance, "", 1);
            }

            // talk_npc:<npcId>
            var talkMatch = Regex.Match(condition, @"^talk_npc:(.+)$");
            if (talkMatch.Success)
                return (QuestType.Talk, talkMatch.Groups[1].Value, 1);

            // kill:<enemyId>:<count>
            var killMatch = Regex.Match(condition, @"^kill:(.+):(\d+)$");
            if (killMatch.Success)
                return (QuestType.Combat, killMatch.Groups[1].Value,
                    int.Parse(killMatch.Groups[2].Value));

            // collect:<itemId>:<count>
            var collectMatch = Regex.Match(condition, @"^collect:(.+):(\d+)$");
            if (collectMatch.Success)
                return (QuestType.Collect, collectMatch.Groups[1].Value,
                    int.Parse(collectMatch.Groups[2].Value));

            // explore:<location>
            var exploreMatch = Regex.Match(condition, @"^explore:(.+)$");
            if (exploreMatch.Success)
                return (QuestType.Explore, exploreMatch.Groups[1].Value, 1);

            // boss:<bossId>
            var bossMatch = Regex.Match(condition, @"^boss:(.+)$");
            if (bossMatch.Success)
                return (QuestType.Boss, bossMatch.Groups[1].Value, 1);

            Debug.LogWarning($"[QuestDataLoader] 无法解析 completionCondition: \"{condition}\", 默认 Guidance");
            return (QuestType.Guidance, condition, 1);
        }
    }

    /// <summary>JSON 任务目标</summary>
    [Serializable]
    public class QuestObjectiveJson
    {
        public string description;
        public string completionCondition;
    }

    /// <summary>JSON 奖励</summary>
    [Serializable]
    public class QuestRewardJson
    {
        public int cultivationXP;
        public int spiritStones;
        public string[] itemIds;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  QuestDataLoader — 从 JSON 加载并注入 QuestManager
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 任务 JSON 数据加载器.
    ///
    /// 职责:
    /// - 从 Resources/Data/Quests.json 加载任务定义
    /// - 将每个 JSON 条目转换为 QuestData 并调用 QuestManager.AddQuest() 注册
    /// - 保留完整 JSON 条目字典供外部系统查询 (getQuestEntry)
    /// - 支持手动重新加载 (开发调试 / 热更新)
    ///
    /// 使用方式:
    ///   在任意需要访问任务扩展数据的场景中挂载此 MonoBehaviour,
    ///   勾选 loadOnAwake 可在场景启动时自动加载.
    /// </summary>
    public class QuestDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]

        [SerializeField, Tooltip("Resources 路径 (不含扩展名)")]
        private string jsonResourcesPath = "Data/Quests";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载前是否清空已有数据 (否则覆盖同名)")]
        private bool clearBeforeLoad;

        [Header("=== 状态 ===")]

        [SerializeField]
        private int lastLoadedCount;

        [SerializeField]
        private int totalQuestCount;

        // ─── Singleton ───

        public static QuestDataLoader Instance { get; private set; }

        // ─── 内部存储 ───

        /// <summary>完整 JSON 条目 (按 questId 索引), 供外部系统查询扩展字段</summary>
        private readonly Dictionary<string, QuestJsonEntry> _questEntries
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>已同步到 QuestManager 的 questId 集合</summary>
        private readonly HashSet<string> _syncedIds
            = new(StringComparer.OrdinalIgnoreCase);

        // ────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (loadOnAwake)
            {
                LoadFromResources(jsonResourcesPath, clearBeforeLoad);
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 使用 Inspector 中配置的路径重新加载任务数据.
        /// 可在编辑器中通过右键菜单调用.
        /// </summary>
        [ContextMenu("重新加载任务数据")]
        public void Reload()
        {
            LoadFromResources(jsonResourcesPath, clearBeforeLoad);
        }

        /// <summary>
        /// 从 Resources 加载任务 JSON 数据并注册到 QuestManager.
        /// </summary>
        /// <param name="path">Resources 路径 (不含扩展名, 默认 "Data/Quests")</param>
        /// <param name="clearFirst">加载前是否清空已有数据</param>
        /// <returns>成功加载的任务数量, -1 表示失败</returns>
        public int LoadFromResources(string path = "Data/Quests", bool clearFirst = false)
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[QuestDataLoader] 未找到任务数据: {path}.json (Resources 路径)");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<QuestDatabaseJson>(jsonAsset.text);
            if (wrapper?.quests == null || wrapper.quests.Length == 0)
            {
                Debug.LogWarning("[QuestDataLoader] 任务数据为空或格式无效");
                return -1;
            }

            if (clearFirst)
            {
                _questEntries.Clear();
                _syncedIds.Clear();
            }

            int count = 0;

            foreach (var entry in wrapper.quests)
            {
                if (string.IsNullOrWhiteSpace(entry.questId))
                {
                    Debug.LogWarning("[QuestDataLoader] 发现 questId 为空的条目, 跳过");
                    continue;
                }

                // 存储完整条目 (覆盖同名)
                _questEntries[entry.questId] = entry;

                // 同步到 QuestManager
                if (!_syncedIds.Contains(entry.questId))
                {
                    if (QuestManager.Instance != null)
                    {
                        var qd = entry.ToQuestData();
                        QuestManager.Instance.AddQuest(qd);
                        _syncedIds.Add(entry.questId);
                    }
                    else
                    {
                        Debug.LogError("[QuestDataLoader] QuestManager.Instance 为 null, " +
                                       "请确保 QuestManager 已初始化的场景中调用");
                        return -1;
                    }
                }

                count++;
            }

            lastLoadedCount = count;
            totalQuestCount = _questEntries.Count;

            Debug.Log($"[QuestDataLoader] 成功加载 {count} 个任务 ← {path}.json " +
                      $"(库内总计 {totalQuestCount} 个)");

            return count;
        }

        // ────────────────────────────────────────────────────────────────
        //  外部查询接口
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 获取完整 JSON 条目 (含 objectives/chapter/prerequisites 等扩展字段).
        /// 如果只需基础 QuestData, 请通过 QuestManager 查询.
        /// </summary>
        public QuestJsonEntry GetQuestEntry(string questId)
        {
            _questEntries.TryGetValue(questId, out var entry);
            return entry;
        }

        /// <summary>检查任务是否已加载</summary>
        public bool HasQuest(string questId)
        {
            return _questEntries.ContainsKey(questId);
        }

        /// <summary>当前加载的任务总数</summary>
        public int LoadedQuestCount => _questEntries.Count;

        /// <summary>上次加载时成功注册的数量</summary>
        public int LastLoadedCount => lastLoadedCount;

        /// <summary>获取所有支持前置检查的 questId 列表</summary>
        public IEnumerable<string> GetAllQuestIds()
        {
            return _questEntries.Keys;
        }

        /// <summary>
        /// 获取某任务的前置任务列表 (JSON 原始定义).
        /// 注意: 实际 chaining 目前由 QuestManager.nextQuestId 机制处理.
        /// </summary>
        public string[] GetPrerequisites(string questId)
        {
            return _questEntries.TryGetValue(questId, out var entry)
                ? entry.prerequisites ?? Array.Empty<string>()
                : Array.Empty<string>();
        }
    }
}
