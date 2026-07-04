using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// JSON配置加载器。所有游戏内容从JSON读取，改数据不动代码。
    /// </summary>
    public static class ConfigLoader
    {
        public static T Load<T>(string resourcePath)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                Debug.LogError($"[ConfigLoader] Resource not found: {resourcePath}");
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(textAsset.text);
        }

        public static T LoadFromFile<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[ConfigLoader] File not found: {filePath}");
                return default(T);
            }
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void SaveToFile<T>(string filePath, T data)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
            Debug.Log($"[ConfigLoader] Saved to: {filePath}");
        }
    }
}
