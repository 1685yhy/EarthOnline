using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 成就数据加载器 —— 从 Resources/Data/Achievements.json 读取成就定义,
    /// 在运行时注册到 AchievementManager 中。
    /// </summary>
    public class AchievementDataLoader : MonoBehaviour
    {
        [Header("JSON 资源路径（不含扩展名）")]
        [SerializeField] private string jsonResourcePath = "Data/Achievements";

        private void Start()
        {
            // 使用 Start() 确保 AchievementManager.Awake() 已执行完毕，单例可用
            LoadAndRegister();
        }

        /// <summary>
        /// 加载 JSON 并注册所有成就。
        /// 可被外部调用（如初始化管理器自行触发）。
        /// </summary>
        public void LoadAndRegister()
        {
            // 1. 加载 JSON
            TextAsset jsonAsset = Resources.Load<TextAsset>(jsonResourcePath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[AchievementDataLoader] 找不到资源: Resources/{jsonResourcePath}.json");
                return;
            }

            // 2. 解析
            AchievementListWrapper wrapper = JsonUtility.FromJson<AchievementListWrapper>(jsonAsset.text);
            if (wrapper?.achievements == null || wrapper.achievements.Length == 0)
            {
                Debug.LogWarning("[AchievementDataLoader] JSON 中无成就数据");
                return;
            }

            // 3. 检查 AchievementManager 是否就绪
            if (AchievementManager.Instance == null)
            {
                Debug.LogError("[AchievementDataLoader] AchievementManager 实例不存在，无法注册成就");
                return;
            }

            // 4. 批量注册
            AchievementManager.Instance.RegisterFromLoaderBatch(wrapper.achievements);

            Debug.Log($"[AchievementDataLoader] 成功加载 {wrapper.achievements.Length} 项成就");
        }

        /// <summary>
        /// 手动指定 JSON 路径后重新加载
        /// </summary>
        public void LoadFromPath(string resourcePath)
        {
            jsonResourcePath = resourcePath;
            LoadAndRegister();
        }

        /// <summary>
        /// 验证 JSON 是否可正确解析（编辑器调试用）
        /// </summary>
        public int ValidateJson()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(jsonResourcePath);
            if (jsonAsset == null) return -1;

            AchievementListWrapper wrapper = JsonUtility.FromJson<AchievementListWrapper>(jsonAsset.text);
            return wrapper?.achievements?.Length ?? 0;
        }
    }

    /// <summary>
    /// JSON 根容器
    /// </summary>
    [System.Serializable]
    internal class AchievementListWrapper
    {
        public AchievementDefinition[] achievements;
    }
}
