using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 配方案例 JSON 加载器
    ///
    /// 职责:
    /// - 从 Resources/Data/Recipes.json 加载配方数据
    /// - 调用 RecipeDatabase.LoadRecipesFromJson() 将配方注入运行时数据库
    /// - 支持手动重新加载 (开发调试 / 热更新)
    ///
    /// 使用方式:
    ///   在任意 MonoBehaviour 中调用:
    ///     RecipeDataLoader.LoadFromResources();
    ///
    ///   或在编辑器 Inspector 中将此脚本挂载到任意 GameObject,
    ///   勾选 loadOnAwake 可在场景启动时自动加载.
    /// </summary>
    public class RecipeDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]

        [SerializeField, Tooltip("Resources 路径 (不含扩展名)")]
        private string jsonResourcesPath = "Data/Recipes";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载前是否清空已有配方 (否则合并)")]
        private bool clearBeforeLoad;

        [Header("=== 状态 ===")]
        [SerializeField]
        private int lastLoadedCount;

        [SerializeField]
        private bool loadSucceeded;

        // ────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadFromResources(jsonResourcesPath, clearBeforeLoad);
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  实例方法 (Inspector 可调用)
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 使用 Inspector 中配置的路径重新加载配方.
        /// 可在编辑器中通过右键菜单 / 按钮调用.
        /// </summary>
        [ContextMenu("重新加载配方")]
        public void Reload()
        {
            LoadFromResources(jsonResourcesPath, clearBeforeLoad);
        }

        // ────────────────────────────────────────────────────────────────
        //  静态 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 从 Resources 加载配方案例 JSON 并注入 RecipeDatabase.
        /// </summary>
        /// <param name="path">Resources 路径 (不含扩展名, 默认 "Data/Recipes")</param>
        /// <param name="clearFirst">加载前是否清空已有配方</param>
        /// <returns>成功加载的配方数量, -1 表示失败</returns>
        public static int LoadFromResources(string path = "Data/Recipes", bool clearFirst = false)
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[RecipeDataLoader] 未找到配方案例: {path}.json (Resources 路径)");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<RecipeDatabaseJson>(jsonAsset.text);
            if (wrapper?.Recipes == null || wrapper.Recipes.Length == 0)
            {
                Debug.LogWarning("[RecipeDataLoader] 配方案例为空或格式无效");
                return -1;
            }

            if (RecipeDatabase.Instance == null)
            {
                Debug.LogError("[RecipeDataLoader] RecipeDatabase 实例不存在, " +
                               "请确保 RecipeDatabase 已初始化的场景中调用");
                return -1;
            }

            if (clearFirst)
            {
                // 通过反射或扩展方法清空 — 暂时先用 LoadRecipesFromJson 的覆盖逻辑
                Debug.Log("[RecipeDataLoader] clearFirst=true, 将覆盖已有同名配方");
            }

            RecipeDatabase.Instance.LoadRecipesFromJson(jsonAsset.text);

            int count = wrapper.Recipes.Length;
            Debug.Log($"[RecipeDataLoader] 成功加载 {count} 个配方 ← {path}.json");

            return count;
        }

        /// <summary>
        /// 从给定的 JSON 文本字符串加载配方 (用于测试 / 网络更新).
        /// </summary>
        public static int LoadFromJsonText(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                Debug.LogError("[RecipeDataLoader] JSON 文本为空");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<RecipeDatabaseJson>(jsonText);
            if (wrapper?.Recipes == null || wrapper.Recipes.Length == 0)
            {
                Debug.LogWarning("[RecipeDataLoader] JSON 文本解析结果为空");
                return -1;
            }

            if (RecipeDatabase.Instance == null)
            {
                Debug.LogError("[RecipeDataLoader] RecipeDatabase 实例不存在");
                return -1;
            }

            RecipeDatabase.Instance.LoadRecipesFromJson(jsonText);
            return wrapper.Recipes.Length;
        }

        /// <summary>
        /// 检查 RecipeDatabase 当前加载的配方总数.
        /// </summary>
        public static int GetCurrentRecipeCount()
        {
            if (RecipeDatabase.Instance == null)
                return -1;
            return RecipeDatabase.Instance.TotalRecipeCount;
        }
    }
}
