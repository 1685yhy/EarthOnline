using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 存档管理器。JSON格式存档，版本兼容，存在 persistentDataPath。
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SAVE_DIR = "saves";
        private const string SAVE_FILE = "save_001.json";
        private const int CURRENT_SAVE_VERSION = 1;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private string GetSavePath()
        {
            string dir = Path.Combine(Application.persistentDataPath, SAVE_DIR);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, SAVE_FILE);
        }

        public bool Save(SaveData data)
        {
            try
            {
                data.version = CURRENT_SAVE_VERSION;
                data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetSavePath(), json);
                Debug.Log($"[SaveManager] Game saved. Version: {data.version}");
                EventBus.Publish("OnGameSaved");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                return false;
            }
        }

        public SaveData Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data.version < CURRENT_SAVE_VERSION)
                    data = UpgradeSaveData(data);

                Debug.Log($"[SaveManager] Game loaded. Version: {data.version}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        public bool HasSave()
        {
            return File.Exists(GetSavePath());
        }

        public void DeleteSave()
        {
            string path = GetSavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                EventBus.Publish("OnSaveDeleted");
            }
        }

        private SaveData UpgradeSaveData(SaveData oldData)
        {
            Debug.Log($"[SaveManager] Upgrading save from v{oldData.version} to v{CURRENT_SAVE_VERSION}");
            oldData.version = CURRENT_SAVE_VERSION;
            return oldData;
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string saveTime = "";
        public string playerName = "穿越者";
        public string currentWorldId = "";
        public string currentSceneName = "";
        public float playerPosX = 0f;
        public float playerPosY = 0f;
        public float playerPosZ = 0f;
        public List<string> activeGiftIds = new List<string>();
        public int gameDay = 1;
        public int gameHour = 8;
        public int gameMinute = 0;
        public int playerLevel = 1;
        public long playerSpiritStones = 0;
        public List<NPCProgressData> npcProgress = new List<NPCProgressData>();
        public List<StringPair> extraData = new List<StringPair>();
    }

    [System.Serializable]
    public class NPCProgressData
    {
        public string npcId;
        public int affinity;
        public int relationship;
        public List<string> memories = new List<string>();
    }

    [System.Serializable]
    public class StringPair
    {
        public string key;
        public string value;
    }
}
