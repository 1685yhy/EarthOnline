using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 称号系统 —— 基于成就和等级解锁称号，显示在HUD上。
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        public static TitleManager Instance { get; private set; }
        public string CurrentTitle { get; private set; } = "初来乍到的穿越者";

        private Dictionary<string, string> _titles = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _titles["lv5"] = "初窥门径的修士";
            _titles["lv10"] = "小有所成的冒险者";
            _titles["boss_kill"] = "弑神者";
            _titles["rich"] = "富甲一方的商人";
            _titles["collector"] = "收藏家";
            _titles["quest_all"] = "冒险王";

            EventBus.Subscribe("OnPlayerLevelUp", OnLevelUp);
            EventBus.Subscribe("OnAchievementUnlocked", OnAchievement);
        }

        void OnLevelUp(Dictionary<string, object> data)
        {
            int lv = (int)data["level"];
            if (lv >= 10) SetTitle("lv10");
            else if (lv >= 5) SetTitle("lv5");
        }

        void OnAchievement(Dictionary<string, object> data)
        {
            string title = data["title"]?.ToString();
            string mapped = title switch
            {
                "弑神者" => "boss_kill",
                "富甲一方" => "rich",
                "收藏家" => "collector",
                "冒险家" => "quest_all",
                _ => null
            };
            if (mapped != null && _titles.ContainsKey(mapped))
                SetTitle(mapped);
        }

        void SetTitle(string key)
        {
            if (_titles.ContainsKey(key))
            {
                CurrentTitle = _titles[key];
                Debug.Log($"[称号] 🏅 获得称号: {CurrentTitle}");
                EventBus.Publish("OnTitleChanged", new Dictionary<string, object> {
                    {"title", CurrentTitle}
                });
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnPlayerLevelUp", OnLevelUp);
            EventBus.Unsubscribe("OnAchievementUnlocked", OnAchievement);
        }
    }
}
