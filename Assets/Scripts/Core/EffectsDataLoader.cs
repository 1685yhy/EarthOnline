using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthOnline.Data
{
    /// <summary>
    /// 单个特效/音频定义。
    /// type: "VFX" 或 "Audio"
    /// spawnPosition: "origin"(施放者), "target"(命中点), "center"(自身中心), "screen"(屏幕空间UI)
    /// loop: true 表示持续播放直到手动停止（环境/修炼光环/BGM）
    /// </summary>
    [System.Serializable]
    public class EffectDefinition
    {
        public string id;
        public string displayName;
        public string type;
        public string prefabPath;
        public string colorHint;
        public float duration;
        public bool loop;
        public string spawnPosition;
        public string triggerEvent;
    }

    /// <summary>
    /// EffectsConfig.json 根容器
    /// </summary>
    [System.Serializable]
    public class EffectsConfigData
    {
        public List<EffectDefinition> effects;
    }

    /// <summary>
    /// 特效/音频配置数据加载器。
    /// 从 Resources/Data/EffectsConfig.json 读取所有 VFX/Audio 定义，
    /// 提供按 ID、类别前缀、类型、事件名查询的 API。
    ///
    /// 使用方式:
    ///   var effect = EffectsDataLoader.GetEffect("combat_bolt_fire");
    ///   var combatVFX = EffectsDataLoader.GetEffectsByCategory("combat");
    ///   var sfx = EffectsDataLoader.GetEffectsByType("Audio");
    ///   var hits = EffectsDataLoader.GetEffectsByEvent("OnHitMedium");
    /// </summary>
    public static class EffectsDataLoader
    {
        private static Dictionary<string, EffectDefinition> _effectMap;
        private static bool _loaded;

        private const string ResourcePath = "Data/EffectsConfig";

        /// <summary>
        /// 从 Resources 加载 JSON 并构建索引。
        /// 可多次调用，每次会重新加载最新数据。
        /// </summary>
        public static void Load()
        {
            var data = ConfigLoader.Load<EffectsConfigData>(ResourcePath);
            if (data?.effects == null || data.effects.Count == 0)
            {
                Debug.LogError("[EffectsDataLoader] 加载失败: EffectsConfig.json 为空或格式错误");
                _effectMap = new Dictionary<string, EffectDefinition>(0);
                _loaded = true;
                return;
            }

            _effectMap = new Dictionary<string, EffectDefinition>(data.effects.Count);
            foreach (var effect in data.effects)
            {
                if (string.IsNullOrEmpty(effect.id))
                {
                    Debug.LogWarning("[EffectsDataLoader] 跳过空ID项");
                    continue;
                }
                if (!_effectMap.ContainsKey(effect.id))
                {
                    _effectMap.Add(effect.id, effect);
                }
                else
                {
                    Debug.LogWarning($"[EffectsDataLoader] 跳过重复ID: {effect.id}");
                }
            }

            _loaded = true;
            Debug.Log($"[EffectsDataLoader] 加载完成: {_effectMap.Count} 个定义 (共{data.effects.Count}项)");
        }

        /// <summary>确保数据已加载（首次访问自动加载）</summary>
        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        /// <summary>
        /// 按 ID 获取单个特效/音频定义。
        /// 未找到时返回 null 并输出警告。
        /// </summary>
        public static EffectDefinition GetEffect(string id)
        {
            EnsureLoaded();
            if (_effectMap != null && _effectMap.TryGetValue(id, out var effect))
                return effect;

            Debug.LogWarning($"[EffectsDataLoader] 未找到定义: {id}");
            return null;
        }

        /// <summary>
        /// 按 ID 前缀匹配获取一组特效。
        /// 例如: GetEffectsByCategory("combat") 返回所有战斗类 VFX。
        ///        GetEffectsByCategory("audio_sfx") 返回所有 SFX。
        /// </summary>
        public static List<EffectDefinition> GetEffectsByCategory(string categoryPrefix)
        {
            EnsureLoaded();
            if (_effectMap == null) return new List<EffectDefinition>();

            return _effectMap.Values
                .Where(e => !string.IsNullOrEmpty(e.id) && e.id.StartsWith(categoryPrefix))
                .ToList();
        }

        /// <summary>
        /// 按类型过滤: "VFX" 或 "Audio"
        /// </summary>
        public static List<EffectDefinition> GetEffectsByType(string type)
        {
            EnsureLoaded();
            if (_effectMap == null) return new List<EffectDefinition>();

            return _effectMap.Values
                .Where(e => e.type == type)
                .ToList();
        }

        /// <summary>
        /// 返回全部已加载的定义
        /// </summary>
        public static List<EffectDefinition> GetAllEffects()
        {
            EnsureLoaded();
            return _effectMap?.Values.ToList() ?? new List<EffectDefinition>();
        }

        /// <summary>
        /// 根据 triggerEvent 名称查找匹配的特效。
        /// 例如: GetEffectsByEvent("OnPlayerAttack") 返回所有响应攻击事件的 VFX/Audio。
        /// </summary>
        public static List<EffectDefinition> GetEffectsByEvent(string eventName)
        {
            EnsureLoaded();
            if (_effectMap == null) return new List<EffectDefinition>();

            return _effectMap.Values
                .Where(e => e.triggerEvent == eventName)
                .ToList();
        }

        /// <summary>
        /// 检查指定 ID 是否存在
        /// </summary>
        public static bool HasEffect(string id)
        {
            EnsureLoaded();
            return _effectMap != null && _effectMap.ContainsKey(id);
        }

        /// <summary>
        /// 获取已加载的定义总数
        /// </summary>
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _effectMap?.Count ?? 0;
            }
        }
    }
}
