using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 天气系统 —— 晴/阴/雨/雾，影响战斗和可见度。
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public enum Weather { Sunny, Cloudy, Rain, Fog }
        public Weather CurrentWeather { get; private set; } = Weather.Sunny;

        [Header("天气概率")]
        public float rainChance = 0.25f;
        public float fogChance = 0.15f;
        public float cloudyChance = 0.3f;

        public float WeatherAttackModifier => CurrentWeather switch
        {
            Weather.Rain => 0.9f,
            Weather.Fog => 0.85f,
            _ => 1f
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnDayPassed", OnNewDay);
            UpdateVisuals();
        }

        void OnNewDay(Dictionary<string, object> data)
        {
            float roll = Random.value;
            Weather old = CurrentWeather;

            if (roll < fogChance) CurrentWeather = Weather.Fog;
            else if (roll < fogChance + rainChance) CurrentWeather = Weather.Rain;
            else if (roll < fogChance + rainChance + cloudyChance) CurrentWeather = Weather.Cloudy;
            else CurrentWeather = Weather.Sunny;

            if (CurrentWeather != old)
            {
                Debug.Log($"[Weather] 天气变化: {old} → {CurrentWeather}");
                UpdateVisuals();
                EventBus.Publish("OnWeatherChanged", new Dictionary<string, object> {
                    {"weather", CurrentWeather.ToString()}
                });
            }
        }

        void UpdateVisuals()
        {
            switch (CurrentWeather)
            {
                case Weather.Rain:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.3f, 0.35f, 0.4f);
                    RenderSettings.fogDensity = 0.015f;
                    RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);
                    break;
                case Weather.Fog:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.6f, 0.6f, 0.55f);
                    RenderSettings.fogDensity = 0.03f;
                    RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.45f);
                    break;
                case Weather.Cloudy:
                    RenderSettings.fog = false;
                    RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
                    break;
                default:
                    RenderSettings.fog = false;
                    break;
            }
        }

        public string GetWeatherEmoji() => CurrentWeather switch
        {
            Weather.Sunny => "☀️", Weather.Cloudy => "☁️",
            Weather.Rain => "🌧️", Weather.Fog => "🌫️", _ => "☀️"
        };

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnDayPassed", OnNewDay);
            RenderSettings.fog = false;
        }
    }
}
