using System;
using System.Collections.Generic;
using System.Reflection;
using EarthOnline.World;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── JSON Data Contract ──────────────────────────────────────────────

    /// <summary>Color data matching Unity's Color serialization format.</summary>
    [Serializable]
    public struct ColorData
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public ColorData(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static implicit operator Color(ColorData cd) => new Color(cd.r, cd.g, cd.b, cd.a);
        public static implicit operator ColorData(Color c) => new ColorData(c.r, c.g, c.b, c.a);
    }

    /// <summary>Flat JSON data for one sect, matching SectConfigs.json structure.</summary>
    [Serializable]
    public class SectConfigData
    {
        public string sectId;
        public string displayName;
        public string description;
        public bool isFormal;
        public int requiredRealm;
        public int requiredReputation;
        public string trialDescription;
        public ColorData sectColor;
        public List<string> extraConditions;
        public int trialCooldownDays;
        public int leaveCooldownDays;
        public int peacefulLeaveRepPenalty;
        public float contributionRetentionOnLeave;
        public int expulsionContributionThreshold;
    }

    /// <summary>Top-level JSON wrapper for JsonUtility deserialization.</summary>
    [Serializable]
    public class SectConfigsWrapper
    {
        public List<SectConfigData> sects;
    }

    // ─── Config Loader ───────────────────────────────────────────────────

    /// <summary>
    /// Loads sect configuration from Resources/Data/SectConfigs.json into
    /// SectManager at runtime, replacing the hardcoded defaults.
    ///
    /// Also exposes extended data not present in SectConfig (sectColor, extraConditions)
    /// via static accessors.
    ///
    /// Place this on the same GameObject as SectManager, or call LoadConfigs() manually.
    /// </summary>
    public class SectConfigLoader : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Path under Resources/ (without extension). Default: Data/SectConfigs")]
        [SerializeField] private string _configPath = "Data/SectConfigs";

        [Tooltip("If true, logs all loaded config values at startup.")]
        [SerializeField] private bool _verboseLogging;

        // ─── Extended Data Stores ────────────────────────────────────────

        private static readonly Dictionary<SectType, Color> SectColors = new();
        private static readonly Dictionary<SectType, List<string>> ExtraConditions = new();

        // ─── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            LoadConfigs();
        }

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Get the display color associated with a sect.</summary>
        public static Color GetSectColor(SectType sect)
        {
            return SectColors.TryGetValue(sect, out var color) ? color : Color.white;
        }

        /// <summary>Get the list of extra join conditions for a sect (returns a copy).</summary>
        public static List<string> GetExtraConditions(SectType sect)
        {
            return ExtraConditions.TryGetValue(sect, out var conditions)
                ? new List<string>(conditions)
                : new List<string>();
        }

        /// <summary>
        /// Trigger a (re)load from the JSON resource path.
        /// Call this if you need to reload at runtime (e.g., after hot-reload).
        /// </summary>
        public void LoadConfigs()
        {
            var jsonAsset = Resources.Load<TextAsset>(_configPath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[SectConfigLoader] 未找到 {_configPath}.json，跳过配置加载");
                return;
            }

            SectConfigsWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<SectConfigsWrapper>(jsonAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SectConfigLoader] JSON 解析失败: {ex.Message}");
                return;
            }

            if (wrapper?.sects == null || wrapper.sects.Count == 0)
            {
                Debug.LogError("[SectConfigLoader] SectConfigs.json 格式错误或无数据");
                return;
            }

            // Clear existing extended data
            SectColors.Clear();
            ExtraConditions.Clear();

            // Build config dictionary from JSON data
            var configs = new Dictionary<SectType, SectConfig>();
            foreach (var data in wrapper.sects)
            {
                if (!Enum.TryParse<SectType>(data.sectId, out var sectType))
                {
                    Debug.LogWarning($"[SectConfigLoader] 未知的 sectId: \"{data.sectId}\"，跳过");
                    continue;
                }

                configs[sectType] = new SectConfig
                {
                    DisplayName = data.displayName,
                    Description = data.description,
                    IsFormal = data.isFormal,
                    RequiredRealmLevel = data.requiredRealm,
                    RequiredReputation = data.requiredReputation,
                    ExtraConditionDesc = data.trialDescription,
                    TrialCooldownDays = data.trialCooldownDays,
                    LeaveCooldownDays = data.leaveCooldownDays,
                    PeacefulLeaveRepPenalty = data.peacefulLeaveRepPenalty,
                    ContributionRetentionOnLeave = data.contributionRetentionOnLeave,
                    ExpulsionContributionThreshold = data.expulsionContributionThreshold,
                };

                // Store extended data for public API access
                SectColors[sectType] = data.sectColor;
                ExtraConditions[sectType] = data.extraConditions ?? new List<string>();

                if (_verboseLogging)
                {
                    Debug.Log($"[SectConfigLoader]   {sectType} → {data.displayName} (境界要求:{data.requiredRealm}, 声望:{data.requiredReputation}, 正式:{data.isFormal})");
                }
            }

            // Inject into SectManager's private static DefaultConfigs
            if (SectManager.Instance != null)
            {
                InjectIntoDefaultConfigs(configs);
            }
            else
            {
                Debug.LogWarning("[SectConfigLoader] SectManager.Instance 为 null，配置将在 SectManager 初始化后生效吗？请确保 SectManager 先于本加载器创建。");
            }

            Debug.Log($"[SectConfigLoader] 成功加载 {configs.Count} 个门派配置");
        }

        // ─── Reflection Injection ────────────────────────────────────────

        /// <summary>
        /// Replaces the contents of SectManager.DefaultConfigs in-place.
        /// Uses reflection because DefaultConfigs is private static readonly.
        /// The dictionary reference cannot be reassigned (readonly), but its
        /// contents can be cleared and repopulated.
        /// </summary>
        private static void InjectIntoDefaultConfigs(Dictionary<SectType, SectConfig> configs)
        {
            var field = typeof(SectManager).GetField("DefaultConfigs",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError("[SectConfigLoader] 无法反射获取 SectManager.DefaultConfigs 字段（可能已被 IL2CPP 剥离）");
                return;
            }

            var dict = field.GetValue(null) as Dictionary<SectType, SectConfig>;
            if (dict == null)
            {
                Debug.LogError("[SectConfigLoader] SectManager.DefaultConfigs 字段值为 null");
                return;
            }

            dict.Clear();
            foreach (var kvp in configs)
            {
                dict[kvp.Key] = kvp.Value;
            }

            Debug.Log($"[SectConfigLoader] 已注入 {configs.Count} 条门派配置到 SectManager.DefaultConfigs");
        }

        // ─── Editor Helper ───────────────────────────────────────────────

        /// <summary>
        /// In the Unity Editor, call this from the Inspector context menu
        /// or from [MenuItem] to validate the JSON without entering Play Mode.
        /// </summary>
        [ContextMenu("Reload Sect Configs (Editor)")]
        private void EditorReload()
        {
            LoadConfigs();
        }
    }
}
