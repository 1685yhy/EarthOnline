using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// V2.3 技能连击系统 —— 连续击中同一目标→伤害递增。
    /// 网文里"连斩十三剑"的感觉。
    /// </summary>
    public class SkillComboSystem : MonoBehaviour
    {
        public static SkillComboSystem Instance { get; private set; }

        private Dictionary<string, int> _comboCounters = new(); // enemyId→combo
        private Dictionary<string, float> _comboTimers = new();
        public float comboTimout = 3f; // 3秒内不攻击→连击中断

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            var keys = new List<string>(_comboTimers.Keys);
            foreach (var k in keys)
            {
                _comboTimers[k] -= Time.deltaTime;
                if (_comboTimers[k] <= 0)
                {
                    if (_comboCounters.ContainsKey(k) && _comboCounters[k] >= 5)
                        Debug.Log($"[连击] ⚡ 连击中断！({_comboCounters[k]}连击)");
                    _comboCounters.Remove(k);
                    _comboTimers.Remove(k);
                }
            }
        }

        /// <summary>命中目标→增加连击数，返回伤害加成倍率</summary>
        public float RegisterHit(string enemyId)
        {
            if (!_comboCounters.ContainsKey(enemyId))
                _comboCounters[enemyId] = 0;
            _comboCounters[enemyId]++;
            _comboTimers[enemyId] = comboTimout;

            int combo = _comboCounters[enemyId];
            float bonus = 1f + combo * 0.05f; // 每连击+5%伤害

            if (combo == 5) Debug.Log($"[连击] ⚡ 5连击！");
            if (combo == 10) Debug.Log($"[连击] 🔥 10连击！伤害+50%！");
            if (combo == 15) Debug.Log($"[连击] 💥 15连击！！伤害翻倍！");

            return bonus;
        }

        public int GetCombo(string enemyId) =>
            _comboCounters.ContainsKey(enemyId) ? _comboCounters[enemyId] : 0;
    }
}
