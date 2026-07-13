using UnityEngine;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 天气与时间周期配置加载器。
    /// 从 Resources/Data/WeatherTimeConfig.json 加载完整的天象、季节、天气、区域覆盖
    /// 以及特殊时间事件配置，在运行时供 TimeManager 和 WeatherSystem 查询。
    ///
    /// 数据驱动 —— 策划可通过修改 JSON 直接调整平衡性和天气表现。
    /// 调用 WeatherTimeDataLoader.Load() 在 SceneBootstrap 或 GameManager 启动时加载。
    /// </summary>
    public static class WeatherTimeDataLoader
    {
        // ──────────── JSON 根 ────────────

        [System.Serializable]
        public class WeatherTimeConfigData
        {
            public string version;
            public string lastUpdated;
            public string description;
            public DayCycleConfig dayCycle;
            public SeasonConfig[] seasons;
            public WeatherTypeConfig[] weatherTypes;
            public TimeEventConfig[] timeEvents;
            public ZoneWeatherOverride[] zoneWeatherOverrides;
        }

        // ──────────── 日周期 ────────────

        [System.Serializable]
        public class DayCycleConfig
        {
            public int dayLengthRealMinutes;
            public int dawnDurationSeconds;
            public int twilightDurationSeconds;
            public DayHours hours;
            public SunCurveKey[] sunCurveKeys;
        }

        [System.Serializable]
        public class DayHours
        {
            public int dawnStart;
            public int dawnEnd;
            public int dayStart;
            public int dayEnd;
            public int twilightStart;
            public int twilightEnd;
            public int nightStart;
            public int nightEnd;
        }

        [System.Serializable]
        public class SunCurveKey
        {
            public float time;
            public float intensity;
            public float[] color; // [r, g, b]
        }

        // ──────────── 季节 ────────────

        [System.Serializable]
        public class SeasonConfig
        {
            public string id;
            public string displayName;
            public int[] months;
            public string description;
            public float cultivationModifier;
            public float spawnRateModifier;
            public float resourceGatherModifier;
            public WeatherBias weatherBias;
        }

        [System.Serializable]
        public class WeatherBias
        {
            public float Clear;
            public float Cloudy;
            public float Rain;
            public float Storm;
            public float Snow;
            public float Fog;
            public float Sandstorm;
            public float SpiritTide;
        }

        // ──────────── 天气类型 ────────────

        [System.Serializable]
        public class WeatherTypeConfig
        {
            public string id;
            public string displayName;
            public string description;
            public int priority;
            public SeasonProbabilities probabilityBySeason;
            public DurationRange durationRangeMinutes;
            public int transitionTimeSeconds;
            public WeatherVisual visual;
            public WeatherGameplay gameplay;
        }

        [System.Serializable]
        public class SeasonProbabilities
        {
            public float spring;
            public float summer;
            public float autumn;
            public float winter;
        }

        [System.Serializable]
        public class DurationRange
        {
            public int min;
            public int max;
        }

        [System.Serializable]
        public class WeatherVisual
        {
            public bool fogEnabled;
            public float fogDensity;
            public float[] fogColor;
            public float[] ambientLight;
            public float sunMultiplier;
            public bool rainParticle;
            public bool snowParticle;
            public float windStrength;
            public float[] skyTint;
            public string specialParticle; // optional, e.g. "SpiritTide"
        }

        [System.Serializable]
        public class WeatherGameplay
        {
            public float cultivationModifier;
            public float movementSpeedModifier;
            public float spawnRateModifier;
            public float visibilityRange;
            public int spiritRegenBonus;
            public float fireAffinityBonus;
            public float woodAffinityBonus;
            public float waterAffinityBonus;
            public float thunderAffinityBonus;
            public float iceAffinityBonus;
            public float earthAffinityBonus;
            public float metalAffinityBonus;
            public float stealthBonus;
            public float rangedAccuracyPenalty;
            public float continuousDamagePerSecond;
            public float breakthroughRiskBonus;
            public float breakthroughChanceBonus;
            public float resourceGatherBonus;
            public float rareSpawnChanceBonus;
        }

        // ──────────── 时间事件 ────────────

        [System.Serializable]
        public class TimeEventConfig
        {
            public string id;
            public string displayName;
            public string description;
            public int hourStart;
            public int hourEnd;
            public string eventType; // Daily, Monthly, Rare
            public int requiredDayOfMonth;
            public float triggerChance;
            public int cooldownDays;
            public TimeEventEffects effects;
        }

        [System.Serializable]
        public class TimeEventEffects
        {
            public float cultivationModifier;
            public int spiritRegenBonus;
            public float breakthroughChanceBonus;
            public string elementAffinity;
            public float affinityBonus;
            public float alchemyQualityBonus;
            public float rareSpawnChanceBonus;
            public float transformationBeastChance;
            public float yinAffinityBonus;
            public float yangAffinityPenalty;
            public float rareItemDropBonus;
            public float hiddenRealmDiscoverChance;
            public float ancientRuinActivationChance;
        }

        // ──────────── 区域天气覆盖 ────────────

        [System.Serializable]
        public class StringFloatPair
        {
            public string key;
            public float value;
        }

        [System.Serializable]
        public class ZoneWeatherOverride
        {
            public string zoneId;
            public string zoneName;
            public string description;
            public string[] allowedWeathers;
            public string[] forcedWeathers;
            public ZoneWeatherModifiers weatherModifiers;
            public StringFloatPair[] seasonProbabilityOverrides; // JSON array, converted to dict at runtime

            /// <summary>运行时构建的查找表（由 BuildIndex 填充）</summary>
            [System.NonSerialized] public Dictionary<string, float> probabilityLookup;
        }

        [System.Serializable]
        public class ZoneWeatherModifiers
        {
            public float cultivationModifier;
            public float spawnRateModifier;
            public float visibilityRangeOverride; // -1 means use global
        }

        // ──────────── 运行时暴露 ────────────

        /// <summary>原始 JSON 数据（保留完整引用）</summary>
        public static WeatherTimeConfigData RawData { get; private set; }

        /// <summary>是否已成功加载</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>天气 ID 索引查找</summary>
        private static Dictionary<string, WeatherTypeConfig> _weatherIndex;

        /// <summary>季节 ID 索引查找</summary>
        private static Dictionary<string, SeasonConfig> _seasonIndex;

        /// <summary>区域 ID 索引查找</summary>
        private static Dictionary<string, ZoneWeatherOverride> _zoneIndex;

        // 天气枚举兼容 —— 将 JSON id 映射为现有 WeatherSystem.Weather 扩展
        private static readonly Dictionary<string, int> WeatherIdToEnum = new Dictionary<string, int>
        {
            { "Clear", 0 },
            { "Cloudy", 1 },
            { "Rain", 2 },
            { "Storm", 3 },
            { "Snow", 4 },
            { "Fog", 5 },
            { "Sandstorm", 6 },
            { "SpiritTide", 7 },
        };

        // ──────────── 公共查询 API ────────────

        /// <summary>获取天气配置（按 ID）</summary>
        public static WeatherTypeConfig GetWeather(string weatherId)
        {
            if (!IsLoaded || _weatherIndex == null) return null;
            _weatherIndex.TryGetValue(weatherId, out var config);
            return config;
        }

        /// <summary>获取所有天气配置</summary>
        public static WeatherTypeConfig[] GetAllWeathers()
        {
            return RawData?.weatherTypes;
        }

        /// <summary>获取季节配置</summary>
        public static SeasonConfig GetSeason(string seasonId)
        {
            if (!IsLoaded || _seasonIndex == null) return null;
            _seasonIndex.TryGetValue(seasonId, out var config);
            return config;
        }

        /// <summary>根据月份获取季节</summary>
        public static SeasonConfig GetSeasonByMonth(int month)
        {
            if (!IsLoaded || RawData?.seasons == null) return null;
            foreach (var s in RawData.seasons)
            {
                if (System.Array.IndexOf(s.months, month) >= 0)
                    return s;
            }
            return RawData.seasons.Length > 0 ? RawData.seasons[0] : null;
        }

        /// <summary>获取区域天气覆盖</summary>
        public static ZoneWeatherOverride GetZoneOverride(string zoneId)
        {
            if (!IsLoaded || _zoneIndex == null) return null;
            _zoneIndex.TryGetValue(zoneId, out var config);
            return config;
        }

        /// <summary>获取天气的权重值（JSON 中的概率 × 季节系数），用于随机选择</summary>
        public static float GetWeatherProbability(string weatherId, string seasonId)
        {
            var weather = GetWeather(weatherId);
            if (weather == null) return 0f;

            var p = weather.probabilityBySeason;
            return seasonId switch
            {
                "spring" => p.spring,
                "summer" => p.summer,
                "autumn" => p.autumn,
                "winter" => p.winter,
                _ => 0f
            };
        }

        /// <summary>获取区域覆盖下的天气权重</summary>
        public static float GetZoneWeatherProbability(string zoneId, string weatherId, string seasonId)
        {
            var zone = GetZoneOverride(zoneId);
            if (zone?.probabilityLookup != null &&
                zone.probabilityLookup.TryGetValue(weatherId, out float prob))
            {
                return prob;
            }
            return GetWeatherProbability(weatherId, seasonId);
        }

        /// <summary>随机选择一个天气（考虑季节概率）</summary>
        public static string RollWeather(string seasonId)
        {
            if (!IsLoaded || RawData?.weatherTypes == null) return "Clear";

            var weathers = RawData.weatherTypes;
            float totalWeight = 0f;
            foreach (var w in weathers)
            {
                totalWeight += GetWeatherProbability(w.id, seasonId);
            }

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var w in weathers)
            {
                cumulative += GetWeatherProbability(w.id, seasonId);
                if (roll <= cumulative) return w.id;
            }

            return "Clear";
        }

        /// <summary>随机选择一个区域天气（考虑区域覆盖和季节）</summary>
        public static string RollWeatherForZone(string zoneId, string seasonId)
        {
            var zone = GetZoneOverride(zoneId);
            if (zone == null) return RollWeather(seasonId);

            // 如果有强制天气，直接返回
            if (zone.forcedWeathers != null && zone.forcedWeathers.Length > 0)
            {
                return zone.forcedWeathers[Random.Range(0, zone.forcedWeathers.Length)];
            }

            var allowed = zone.allowedWeathers;
            if (allowed == null || allowed.Length == 0)
                return RollWeather(seasonId);

            float totalWeight = 0f;
            foreach (var wId in allowed)
            {
                totalWeight += GetZoneWeatherProbability(zoneId, wId, seasonId);
            }

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var wId in allowed)
            {
                cumulative += GetZoneWeatherProbability(zoneId, wId, seasonId);
                if (roll <= cumulative) return wId;
            }

            return allowed[0];
        }

        /// <summary>检查指定时间是否有活跃的时间事件</summary>
        public static List<TimeEventConfig> GetActiveTimeEvents(int gameDay, int gameHour, int gameMinute)
        {
            var result = new List<TimeEventConfig>();
            if (!IsLoaded || RawData?.timeEvents == null) return result;

            float currentMinute = gameHour * 60f + gameMinute;

            foreach (var evt in RawData.timeEvents)
            {
                bool isInHourWindow;
                if (evt.hourStart <= evt.hourEnd)
                {
                    // 同一天内，如 11-13
                    isInHourWindow = gameHour >= evt.hourStart && gameHour < evt.hourEnd;
                }
                else
                {
                    // 跨天，如 23-01
                    isInHourWindow = gameHour >= evt.hourStart || gameHour < evt.hourEnd;
                }

                if (!isInHourWindow) continue;

                // 月度事件：检查日期
                if (evt.eventType == "Monthly" && evt.requiredDayOfMonth > 0)
                {
                    int dayOfMonth = (gameDay - 1) % 30 + 1; // 30天一个月
                    if (dayOfMonth != evt.requiredDayOfMonth) continue;
                }

                // 稀有事件：概率触发
                if (evt.eventType == "Rare" && evt.triggerChance > 0f)
                {
                    // 首次触发检查（由外部系统在跨天时处理）
                    // 此处只标记窗口有效性，触发判定由外部做
                }

                result.Add(evt);
            }

            return result;
        }

        /// <summary>检查当前游戏时间是否为子时（23:00-01:00）</summary>
        public static bool IsZiShi(int gameHour) => gameHour >= 23 || gameHour < 1;

        /// <summary>检查当前游戏时间是否为午时（11:00-13:00）</summary>
        public static bool IsWuShi(int gameHour) => gameHour >= 11 && gameHour < 13;

        /// <summary>获取当前季节对某种效果的总修正</summary>
        public static float GetTotalCultivationModifier(string weatherId, string seasonId, int gameHour)
        {
            float modifier = 1.0f;

            // 季节修正
            var season = GetSeason(seasonId);
            if (season != null) modifier *= season.cultivationModifier;

            // 天气修正
            var weather = GetWeather(weatherId);
            if (weather != null) modifier *= weather.gameplay.cultivationModifier;

            // 时间事件修正
            if (IsZiShi(gameHour)) modifier *= 1.5f;
            else if (IsWuShi(gameHour)) modifier *= 1.3f;

            return modifier;
        }

        // ──────────── 加载与应用 ────────────

        /// <summary>
        /// 从 Resources 加载 WeatherTimeConfig.json。
        /// 建议在 SceneBootstrap 或 GameManager Awake 中调用一次。
        /// </summary>
        public static void Load()
        {
            var textAsset = Resources.Load<TextAsset>("Data/WeatherTimeConfig");
            if (textAsset == null)
            {
                Debug.LogError("[WeatherTimeDataLoader] 找不到 Resources/Data/WeatherTimeConfig.json！");
                return;
            }

            var data = JsonUtility.FromJson<WeatherTimeConfigData>(textAsset.text);
            if (data == null || data.weatherTypes == null || data.weatherTypes.Length == 0)
            {
                Debug.LogError("[WeatherTimeDataLoader] JSON 解析失败或数据为空！");
                return;
            }

            RawData = data;
            BuildIndex(data);
            IsLoaded = true;

            Debug.Log($"[WeatherTimeDataLoader] 加载完成：{data.weatherTypes.Length} 种天气，"
                + $"{data.seasons?.Length ?? 0} 个季节，"
                + $"{data.timeEvents?.Length ?? 0} 个时间事件，"
                + $"{data.zoneWeatherOverrides?.Length ?? 0} 个区域覆盖，"
                + $"版本 {data.version}");
        }

        /// <summary>Load() 的别名，匹配其他 Loader 的 Apply() 约定</summary>
        public static void Apply()
        {
            Load();
        }

        // ──────────── 内部索引构建 ────────────

        private static void BuildIndex(WeatherTimeConfigData data)
        {
            _weatherIndex = new Dictionary<string, WeatherTypeConfig>();
            if (data.weatherTypes != null)
            {
                foreach (var w in data.weatherTypes)
                    if (!string.IsNullOrEmpty(w.id))
                        _weatherIndex[w.id] = w;
            }

            _seasonIndex = new Dictionary<string, SeasonConfig>();
            if (data.seasons != null)
            {
                foreach (var s in data.seasons)
                    if (!string.IsNullOrEmpty(s.id))
                        _seasonIndex[s.id] = s;
            }

            _zoneIndex = new Dictionary<string, ZoneWeatherOverride>();
            if (data.zoneWeatherOverrides != null)
            {
                foreach (var z in data.zoneWeatherOverrides)
                {
                    if (!string.IsNullOrEmpty(z.zoneId))
                        _zoneIndex[z.zoneId] = z;

                    // 将序列化的键值对数组转换为运行时字典
                    if (z.seasonProbabilityOverrides != null && z.seasonProbabilityOverrides.Length > 0)
                    {
                        z.probabilityLookup = new Dictionary<string, float>(z.seasonProbabilityOverrides.Length);
                        foreach (var pair in z.seasonProbabilityOverrides)
                        {
                            if (!string.IsNullOrEmpty(pair.key))
                                z.probabilityLookup[pair.key] = pair.value;
                        }
                    }
                }
            }
        }

        // ──────────── 调试输出 ────────────

        /// <summary>打印完整天气时间配置到控制台（用于验证）</summary>
        public static void DebugPrint()
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("[WeatherTimeDataLoader] 未加载，无法打印。");
                return;
            }

            var data = RawData;
            System.Text.StringBuilder sb = new();
            sb.AppendLine("═══════════ 天气与时间周期配置 ═══════════");
            sb.AppendLine($"版本: {data.version}  最后更新: {data.lastUpdated}");
            sb.AppendLine($"日周期: {data.dayCycle?.dayLengthRealMinutes} 分钟/天");
            sb.AppendLine($"黎明时长: {data.dayCycle?.dawnDurationSeconds}s  黄昏时长: {data.dayCycle?.twilightDurationSeconds}s");

            sb.AppendLine("\n── 季节 ──");
            if (data.seasons != null)
            {
                foreach (var s in data.seasons)
                {
                    sb.AppendLine($"  {s.displayName} ({s.id}): 修炼x{s.cultivationModifier} "
                        + $"刷新x{s.spawnRateModifier} 采集x{s.resourceGatherModifier}");
                }
            }

            sb.AppendLine("\n── 天气类型 ──");
            if (data.weatherTypes != null)
            {
                foreach (var w in data.weatherTypes)
                {
                    sb.AppendLine($"  {w.displayName} ({w.id}): 优先级{w.priority} "
                        + $"修炼x{w.gameplay.cultivationModifier} "
                        + $"移速x{w.gameplay.movementSpeedModifier} "
                        + $"刷新x{w.gameplay.spawnRateModifier} "
                        + $"能见度{w.gameplay.visibilityRange}m");
                }
            }

            sb.AppendLine("\n── 时间事件 ──");
            if (data.timeEvents != null)
            {
                foreach (var evt in data.timeEvents)
                {
                    sb.AppendLine($"  {evt.displayName}: {evt.hourStart:D2}:00-{evt.hourEnd:D2}:00 "
                        + $"修炼x{evt.effects.cultivationModifier} "
                        + $"突破+{evt.effects.breakthroughChanceBonus * 100:F0}%");
                }
            }

            sb.AppendLine("\n── 区域天气覆盖 ──");
            if (data.zoneWeatherOverrides != null)
            {
                foreach (var z in data.zoneWeatherOverrides)
                {
                    string allowed = z.allowedWeathers != null ? string.Join(", ", z.allowedWeathers) : "无限制";
                    string forced = z.forcedWeathers != null && z.forcedWeathers.Length > 0
                        ? " 强制: " + string.Join(", ", z.forcedWeathers) : "";
                    sb.AppendLine($"  {z.zoneName}: 允许 [{allowed}]{forced}");
                }
            }

            sb.AppendLine("\n═══════════════════════════════════");
            Debug.Log(sb.ToString());
        }
    }
}
