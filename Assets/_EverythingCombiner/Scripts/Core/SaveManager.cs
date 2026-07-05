using System;
using System.IO;
using UnityEngine;

namespace EverythingCombiner
{
    /// <summary>
    /// 存档管理器 - 本地JSON存档
    /// 负责：保存、加载、自动存档、跨平台路径处理
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public PlayerData CurrentData { get; private set; }

        private string saveFilePath;
        private const string SAVE_FILE_NAME = "everything_combiner_save.json";

        // 存档事件
        public event Action OnDataLoaded;
        public event Action OnDataSaved;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                saveFilePath = GetSavePath();
                LoadGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 获取跨平台存档路径
        /// </summary>
        private string GetSavePath()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL/小游戏：使用PlayerPrefs
            return "";
#else
            return Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
#endif
        }

        /// <summary>
        /// 加载存档
        /// </summary>
        public void LoadGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string json = PlayerPrefs.GetString("save_data", "");
            if (string.IsNullOrEmpty(json))
            {
                CurrentData = PlayerData.CreateDefault();
            }
            else
            {
                CurrentData = JsonUtility.FromJson<PlayerData>(json);
            }
#else
            if (File.Exists(saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(saveFilePath);
                    CurrentData = JsonUtility.FromJson<PlayerData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"存档加载失败: {e.Message}，创建新存档");
                    CurrentData = PlayerData.CreateDefault();
                }
            }
            else
            {
                CurrentData = PlayerData.CreateDefault();
            }
#endif
            OnDataLoaded?.Invoke();
            Debug.Log($"存档加载完成 - 已发现 {CurrentData.totalDiscoveries} 个元素");
        }

        /// <summary>
        /// 保存存档
        /// </summary>
        public void SaveGame()
        {
            if (CurrentData == null) return;

            string json = JsonUtility.ToJson(CurrentData, true);

#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString("save_data", json);
            PlayerPrefs.Save();
#else
            try
            {
                File.WriteAllText(saveFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"存档保存失败: {e.Message}");
                return;
            }
#endif
            OnDataSaved?.Invoke();
        }

        /// <summary>
        /// 自动存档（在合成成功后调用）
        /// </summary>
        public void AutoSave()
        {
            SaveGame();
        }

        /// <summary>
        /// 删除存档（重置游戏）
        /// </summary>
        public void DeleteSave()
        {
            CurrentData = PlayerData.CreateDefault();

#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey("save_data");
#else
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }
#endif
            SaveGame();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                SaveGame();
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}
