using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    public enum BuffType { AttackUp, DefenseUp, HealOverTime, SpeedUp, DamageOverTime }

    [System.Serializable]
    public class Buff
    {
        public BuffType type; public float duration; public float value;
        public float remaining; public string sourceName;
    }

    /// <summary>
    /// Buff/Debuff系统 —— 临时属性修改。
    /// </summary>
    public class BuffManager : MonoBehaviour
    {
        public static BuffManager Instance { get; private set; }
        private List<Buff> _activeBuffs = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                _activeBuffs[i].remaining -= Time.deltaTime;
                if (_activeBuffs[i].remaining <= 0)
                {
                    Debug.Log($"[Buff] {_activeBuffs[i].sourceName} 效果消失");
                    _activeBuffs.RemoveAt(i);
                }
            }
        }

        public void Apply(BuffType type, float value, float duration, string source)
        {
            // Remove existing same-type buff
            _activeBuffs.RemoveAll(b => b.type == type);
            _activeBuffs.Add(new Buff { type = type, value = value, duration = duration, remaining = duration, sourceName = source });

            string icon = type switch
            {
                BuffType.AttackUp => "⚔️", BuffType.DefenseUp => "🛡️",
                BuffType.HealOverTime => "💚", BuffType.SpeedUp => "💨",
                _ => "🔥"
            };
            Debug.Log($"[Buff] {icon} {source}: {type} {value:+0;-#} ({duration}s)");
        }

        public float GetMultiplier(BuffType type)
        {
            var buff = _activeBuffs.Find(b => b.type == type);
            return buff != null ? 1f + buff.value : 1f;
        }

        public bool HasBuff(BuffType type) => _activeBuffs.Exists(b => b.type == type);

        public string GetSummary()
        {
            if (_activeBuffs.Count == 0) return "";
            var parts = new List<string>();
            foreach (var b in _activeBuffs)
                parts.Add($"{b.type}({b.remaining:F0}s)");
            return "✨ " + string.Join(" ", parts);
        }
    }
}
