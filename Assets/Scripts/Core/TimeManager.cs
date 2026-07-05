using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 游戏内时间系统 —— 24分钟=1游戏天，驱动昼夜循环。
    /// 日出06:00 → 正午12:00 → 日落18:00 → 深夜00:00。
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        // 1现实秒 = 1游戏分钟 (24分钟=1游戏天)
        public float timeScale = 60f;

        public int GameDay { get; private set; } = 1;
        public int GameHour { get; private set; } = 8;
        public int GameMinute { get; private set; } = 0;
        public float GameSecond { get; private set; } = 0;

        public Light SunLight { get; private set; }
        [Header("昼夜")]
        public Gradient dayNightColor;
        public AnimationCurve sunIntensity;

        public bool IsDaytime => GameHour >= 6 && GameHour < 18;
        public bool IsNight => !IsDaytime;
        public string TimeString => $"{GameHour:D2}:{GameMinute:D2}";

        public event System.Action OnHourChanged;
        public event System.Action OnDayChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            SunLight = FindObjectOfType<Light>();
        }

        void Start()
        {
            if (SunLight == null) SunLight = FindObjectOfType<Light>();
            // 初始化昼夜预设(防止AddComponent时未序列化)
            if (dayNightColor == null || dayNightColor.colorKeys.Length == 0)
            {
                dayNightColor = new Gradient();
                var colors = new GradientColorKey[] {
                    new(new Color(0.1f,0.1f,0.3f), 0f),     // 午夜深蓝
                    new(new Color(1f,0.6f,0.2f), 0.25f),     // 日出橙色
                    new(new Color(1f,0.95f,0.8f), 0.5f),     // 正午白黄
                    new(new Color(1f,0.4f,0.1f), 0.75f),     // 日落红橙
                    new(new Color(0.1f,0.1f,0.3f), 1f),      // 午夜深蓝
                };
                dayNightColor.colorKeys = colors;
            }
            if (sunIntensity == null || sunIntensity.keys.Length == 0)
            {
                sunIntensity = new AnimationCurve(
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.25f, 0.8f),
                    new Keyframe(0.5f, 1.2f),
                    new Keyframe(0.75f, 0.6f),
                    new Keyframe(1f, 0.1f)
                );
            }
        }

        void Update()
        {
            GameSecond += Time.deltaTime * timeScale;
            while (GameSecond >= 60f) { GameSecond -= 60f; GameMinute++; }
            while (GameMinute >= 60) { GameMinute -= 60; int prevHour = GameHour; GameHour++; OnHourChanged?.Invoke(); }
            while (GameHour >= 24) { GameHour -= 24; GameDay++; OnDayChanged?.Invoke();
                EventBus.Publish("OnDayPassed", new Dictionary<string, object>{{"day", GameDay}});
            }

            UpdateSunLight();
        }

        void UpdateSunLight()
        {
            if (SunLight == null || SunLight.transform == null)
            {
                SunLight = FindObjectOfType<Light>();
                if (SunLight == null) return;
            }

            try
            {
                float dayProgress = (GameHour + GameMinute / 60f) / 24f;
                float sunAngle = dayProgress * 360f - 90f;
                SunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0);
                if (dayNightColor != null) SunLight.color = dayNightColor.Evaluate(dayProgress);
                if (sunIntensity != null) SunLight.intensity = sunIntensity.Evaluate(dayProgress);
                RenderSettings.ambientLight = SunLight.color * 0.3f;
            }
            catch (System.Exception) { /* silently handle */ }
        }

        // 用于存档的摘要
        public Dictionary<string, object> GetSaveData()
        {
            return new Dictionary<string, object> {
                {"day", GameDay}, {"hour", GameHour}, {"minute", GameMinute}
            };
        }
    }
}
