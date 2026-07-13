using UnityEngine;
using UnityEngine.SceneManagement;
using EarthOnline;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.World
{
    /// <summary>
    /// 三层发现系统 —— 管理 Landmark/POI/Hidden 三种探索发现。
    /// Landmark: 15m 自动触发 + 永久地图标记
    /// POI:      10m + 迷雾消散 + 问号标记
    /// Hidden:    6m + 条件检测 + 奖励 + 不自动标记
    ///
    /// 与原有的 HiddenDiscovery 组件配合使用 ——
    /// DiscoverySystem 取得控制权并进行三层逻辑分发，
    /// HiddenDiscovery 作为数据容器保留（其自身 Update 被禁用）。
    ///
    /// 通信：所有事件通过 EventBus 字符串 API 广播。
    /// </summary>
    public class DiscoverySystem : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════
        //  Singleton
        // ════════════════════════════════════════════════════════════════

        public static DiscoverySystem Instance { get; private set; }

        // ════════════════════════════════════════════════════════════════
        //  Inspector — 发现参数
        // ════════════════════════════════════════════════════════════════

        [Header("Detection Ranges")]
        [SerializeField] private float landmarkRange = 15f;
        [SerializeField] private float poiRange = 10f;
        [SerializeField] private float hiddenRange = 6f;

        [Header("Hidden Discovery — Detection Probability")]
        [SerializeField] private float baseDetectionChance = 0.6f;

        [Header("Discovery Type Mapping")]
        [Tooltip("将 discoveryId 映射为 Landmark/POI/Hidden 及触发条件。"
               + "未在此列表中的发现默认视为 Landmark。")]
        [SerializeField] private DiscoveryConfigEntry[] discoveryConfigs;

        // ════════════════════════════════════════════════════════════════
        //  Runtime State
        // ════════════════════════════════════════════════════════════════

        private Dictionary<string, DiscoveryState> _discoveries;
        private Dictionary<string, DiscoveryConfigEntry> _configMap;
        private Transform _player;

        // ── Lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _discoveries = new Dictionary<string, DiscoveryState>();
            _configMap = new Dictionary<string, DiscoveryConfigEntry>();
        }

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[DiscoverySystem] 场景中未找到 Tag=Player 的对象。发现检测将暂停。");

            BuildConfigMap();
            ScanDiscoveriesInScene();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Update()
        {
            if (_player == null) return;

            foreach (var kvp in _discoveries)
            {
                DiscoveryState state = kvp.Value;
                if (state.discovered) continue;
                if (state.component == null) continue;

                float dist = Vector3.Distance(
                    state.component.transform.position, _player.position);

                if (TryDiscover(state, dist))
                    state.discovered = true;
            }
        }

        // ── Scene Loading ─────────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            ScanDiscoveriesInScene();
        }

        private void BuildConfigMap()
        {
            _configMap.Clear();
            if (discoveryConfigs == null) return;

            foreach (var entry in discoveryConfigs)
            {
                if (!string.IsNullOrEmpty(entry.discoveryId) && !_configMap.ContainsKey(entry.discoveryId))
                    _configMap[entry.discoveryId] = entry;
            }
        }

        private void ScanDiscoveriesInScene()
        {
            // Keep already-discovered entries so we don't lose state across scene loads
            var alreadyDiscovered = new HashSet<string>();
            foreach (var kvp in _discoveries)
                if (kvp.Value.discovered)
                    alreadyDiscovered.Add(kvp.Key);

            _discoveries.Clear();

            var components = FindObjectsOfType<HiddenDiscovery>(true);
            foreach (var hd in components)
            {
                string id = hd.discoveryId;

                var state = new DiscoveryState
                {
                    component = hd,
                    type = DiscoveryType.Landmark, // default
                    discovered = alreadyDiscovered.Contains(id)
                };

                if (_configMap.TryGetValue(id, out var config))
                    state.type = config.type;

                _discoveries[id] = state;

                // 禁用原始 HiddenDiscovery 的自动检测，避免重复触发
                hd.enabled = false;
            }

            Debug.Log($"[DiscoverySystem] 扫描完成：{_discoveries.Count} 个发现点已注册");
        }

        // ════════════════════════════════════════════════════════════════
        //  三层发现逻辑
        // ════════════════════════════════════════════════════════════════

        private bool TryDiscover(DiscoveryState state, float distance)
        {
            switch (state.type)
            {
                case DiscoveryType.Landmark:
                    return TryDiscoverLandmark(state, distance);
                case DiscoveryType.POI:
                    return TryDiscoverPOI(state, distance);
                case DiscoveryType.Hidden:
                    return TryDiscoverHidden(state, distance);
                default:
                    return false;
            }
        }

        private float RangeForType(DiscoveryType type) => type switch
        {
            DiscoveryType.Landmark => landmarkRange,
            DiscoveryType.POI => poiRange,
            DiscoveryType.Hidden => hiddenRange,
            _ => landmarkRange
        };

        // ── Landmark ──────────────────────────────────────────────────

        private bool TryDiscoverLandmark(DiscoveryState state, float distance)
        {
            if (distance > landmarkRange) return false;

            var hd = state.component;
            Debug.Log($"[探索] 发现地标 —— {hd.discoveryName}");
            if (!string.IsNullOrEmpty(hd.discoveryText))
                Debug.Log($"  {hd.discoveryText}");

            EventBus.Publish("OnDiscoveryFound", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName},
                {"type", "Landmark"},
                {"text", hd.discoveryText ?? ""},
                {"position", hd.transform.position}
            });

            EventBus.Publish("OnLandmarkDiscovered", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName}
            });

            return true;
        }

        // ── POI ───────────────────────────────────────────────────────

        private bool TryDiscoverPOI(DiscoveryState state, float distance)
        {
            if (distance > poiRange) return false;
            if (!IsFogCleared(state.component.transform.position)) return false;

            var hd = state.component;
            Debug.Log($"[探索] 发现 POI —— {hd.discoveryName}");
            if (!string.IsNullOrEmpty(hd.discoveryText))
                Debug.Log($"  {hd.discoveryText}");

            EventBus.Publish("OnDiscoveryFound", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName},
                {"type", "POI"},
                {"text", hd.discoveryText ?? ""},
                {"position", hd.transform.position}
            });

            EventBus.Publish("OnPOIDiscovered", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName}
            });

            return true;
        }

        // ── Hidden ────────────────────────────────────────────────────

        private bool TryDiscoverHidden(DiscoveryState state, float distance)
        {
            if (distance > hiddenRange) return false;
            if (!CheckConditions(state.component.discoveryId)) return false;

            var hd = state.component;
            Debug.Log($"[探索] 发现隐藏 —— {hd.discoveryName}");
            if (!string.IsNullOrEmpty(hd.discoveryText))
                Debug.Log($"  {hd.discoveryText}");

            // 发放奖励
            GiveRewards(hd);

            // 只播事件，不自动标记地图
            EventBus.Publish("OnDiscoveryFound", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName},
                {"type", "Hidden"},
                {"text", hd.discoveryText ?? ""},
                {"position", hd.transform.position}
            });

            EventBus.Publish("OnHiddenDiscovered", new Dictionary<string, object>
            {
                {"id", hd.discoveryId},
                {"name", hd.discoveryName}
            });

            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助逻辑
        // ════════════════════════════════════════════════════════════════

        /// <summary> 检查该位置迷雾是否已消散（与 FogOfWar 系统集成）。</summary>
        private bool IsFogCleared(Vector3 position)
        {
            if (FogOfWar.Instance == null)
            {
                // 迷雾系统不存在时默认允许 POI 发现
                return true;
            }
            return FogOfWar.Instance.IsExplored(position);
        }

        /// <summary> 检查隐藏发现的触发条件是否全部满足。</summary>
        private bool CheckConditions(string discoveryId)
        {
            if (!_configMap.TryGetValue(discoveryId, out var config))
                return false;
            if (config.conditions == null || config.conditions.Length == 0)
                return false;

            foreach (var cond in config.conditions)
            {
                if (!cond.IsMet())
                    return false;
            }
            return true;
        }

        private void GiveRewards(HiddenDiscovery hd)
        {
            // 道具奖励
            if (!string.IsNullOrEmpty(hd.rewardItemId) && InventoryManager.Instance != null)
            {
                string itemName = string.IsNullOrEmpty(hd.rewardItemName)
                    ? hd.rewardItemId
                    : hd.rewardItemName;

                InventoryManager.Instance.AddItem(new Item
                {
                    id = hd.rewardItemId,
                    name = itemName,
                    quantity = hd.rewardQuantity,
                    value = 50
                });

                Debug.Log($"  [奖励] 道具: {itemName} x{hd.rewardQuantity}");
            }

            // 修为奖励
            if (hd.rewardCultivation > 0 && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddCultivation(hd.rewardCultivation);
                Debug.Log($"  [奖励] 修为: +{hd.rewardCultivation}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  公开接口 —— 供技能/UI 调用
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 神识探查 —— 检测周围可发现的隐藏点。
        /// 返回所有在 detectionRange 内且满足条件的 Hidden discoveryId 列表。
        /// </summary>
        public List<string> DetectHiddenInRange(float detectionRange)
        {
            var results = new List<string>();
            if (_player == null) return results;

            foreach (var kvp in _discoveries)
            {
                var state = kvp.Value;
                if (state.discovered) continue;
                if (state.type != DiscoveryType.Hidden) continue;
                if (state.component == null) continue;

                float dist = Vector3.Distance(
                    state.component.transform.position, _player.position);

                if (dist <= detectionRange && CheckConditions(state.component.discoveryId))
                    results.Add(state.component.discoveryId);
            }

            return results;
        }

        /// <summary>
        /// 计算特定发现的当前发现概率。
        /// 公式: DetectionChance = 0.6 / (1 + (Dist / IdealRadius)^2)
        /// </summary>
        public float GetDetectionChance(string discoveryId)
        {
            if (!_discoveries.TryGetValue(discoveryId, out var state)) return 0f;
            if (state.component == null || _player == null) return 0f;

            float dist = Vector3.Distance(
                state.component.transform.position, _player.position);
            float ideal = RangeForType(state.type);
            return baseDetectionChance / (1f + (dist / ideal) * (dist / ideal));
        }

        /// <summary> 外部查询指定发现是否已被记录。 </summary>
        public bool IsDiscovered(string discoveryId)
        {
            return _discoveries.TryGetValue(discoveryId, out var state) && state.discovered;
        }

        /// <summary> 外部强制标记一个发现为已发现（用于存档载入等场景）。 </summary>
        public void MarkDiscovered(string discoveryId)
        {
            if (_discoveries.TryGetValue(discoveryId, out var state))
                state.discovered = true;
        }

        /// <summary> 获取当前层的发现数量。 </summary>
        public int GetDiscoveryCount(DiscoveryType type)
        {
            int count = 0;
            foreach (var kvp in _discoveries)
                if (kvp.Value.type == type && kvp.Value.discovered)
                    count++;
            return count;
        }

        // ════════════════════════════════════════════════════════════════
        //  Gizmos（Scene 视图调试）
        // ════════════════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // In edit mode _discoveries may be null; scan manually for gizmo display.
            if (_discoveries == null)
            {
                DrawEditTimeGizmos();
                return;
            }

            foreach (var kvp in _discoveries)
            {
                var state = kvp.Value;
                if (state.component == null) continue;

                float range = RangeForType(state.type);

                switch (state.type)
                {
                    case DiscoveryType.Landmark:
                        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.3f);
                        break;
                    case DiscoveryType.POI:
                        Gizmos.color = new Color(0.2f, 0.5f, 1.0f, 0.3f);
                        break;
                    case DiscoveryType.Hidden:
                        Gizmos.color = new Color(1.0f, 0.8f, 0.0f, 0.4f);
                        break;
                }

                Gizmos.DrawWireSphere(state.component.transform.position, range);

                if (state.discovered)
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
                    Gizmos.DrawSphere(state.component.transform.position, 0.3f);
                }
            }
        }

        /// <summary>
        /// Edit-mode gizmo fallback: scans HiddenDiscovery components directly
        /// so scene view visualisation works without entering Play Mode.
        /// </summary>
        private void DrawEditTimeGizmos()
        {
            var components = FindObjectsOfType<HiddenDiscovery>(true);
            foreach (var hd in components)
            {
                // Default range and colour in edit mode (Landmark).
                float range = landmarkRange;
                Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.3f);

                // Try to read config type if available.
                if (discoveryConfigs != null)
                {
                    foreach (var entry in discoveryConfigs)
                    {
                        if (entry.discoveryId == hd.discoveryId)
                        {
                            range = RangeForType(entry.type);

                            switch (entry.type)
                            {
                                case DiscoveryType.Landmark:
                                    Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.3f);
                                    break;
                                case DiscoveryType.POI:
                                    Gizmos.color = new Color(0.2f, 0.5f, 1.0f, 0.3f);
                                    break;
                                case DiscoveryType.Hidden:
                                    Gizmos.color = new Color(1.0f, 0.8f, 0.0f, 0.4f);
                                    break;
                            }
                            break;
                        }
                    }
                }

                Gizmos.DrawWireSphere(hd.transform.position, range);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  内部数据类型
        // ════════════════════════════════════════════════════════════════

        [System.Serializable]
        public class DiscoveryConfigEntry
        {
            public string discoveryId;
            public DiscoveryType type = DiscoveryType.Landmark;
            public DiscoveryCondition[] conditions;
        }

        [System.Serializable]
        public class DiscoveryCondition
        {
            public ConditionType type = ConditionType.AlwaysTrue;

            [Tooltip("条件参数：\n"
                   + "AlwaysTrue → 不读取\n"
                   + "CultivationAbove → 最低修为等级\n"
                   + "TimeOfDay → 时间区间 \"HH:mm-HH:mm\"\n"
                   + "HasItem → 物品 ID")]
            public string value;

            public bool IsMet()
            {
                switch (type)
                {
                    case ConditionType.AlwaysTrue:
                        return true;

                    case ConditionType.CultivationAbove:
                        if (PlayerStats.Instance == null) return false;
                        if (int.TryParse(value, out int minCultivation))
                            return PlayerStats.Instance.cultivation >= minCultivation;
                        return true;

                    case ConditionType.TimeOfDay:
                        // TODO: 接入时间系统
                        return true;

                    case ConditionType.HasItem:
                        if (InventoryManager.Instance != null)
                            return InventoryManager.Instance.HasItem(value);
                        return false;

                    default:
                        return true;
                }
            }
        }

        public enum ConditionType
        {
            AlwaysTrue,
            CultivationAbove,
            TimeOfDay,
            HasItem
        }

        private class DiscoveryState
        {
            public HiddenDiscovery component;
            public DiscoveryType type;
            public bool discovered;
        }
    }
}
