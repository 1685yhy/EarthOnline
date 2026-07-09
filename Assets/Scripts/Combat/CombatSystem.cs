using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline.Combat
{
    /// <summary>
    /// V2.0 修真战斗 —— 掐诀→释放法诀→灵力消耗→境界压制。
    /// 不再左键平A。每击都是修真者的法诀。
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        [Header("基础攻击")]
        public float baseSpiritAttack = 15f;    // 基础灵击伤害
        public float spiritCostPerAttack = 5f;   // 每次灵击灵力消耗
        public float castTime = 0.4f;            // 掐诀前摇(秒)
        public float maxSpiritEnergy = 100f;     // 最大灵力值
        public float spiritRegenRate = 8f;       // 每秒灵力回复(加快，避免罚站)

        [Header("境界压制")]
        public float realmSuppressionRatio = 0.5f; // 高1境界→50%免伤

        private float _currentSpiritEnergy;
        private float _lastCastTime;
        private EnemyAI _lockedTarget;
        private Camera _cam;

        public float SpiritEnergy => _currentSpiritEnergy;
        public float SpiritPercent => _currentSpiritEnergy / maxSpiritEnergy;
        public EnemyAI LockedTarget => _lockedTarget;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _currentSpiritEnergy = maxSpiritEnergy;
            _cam = Camera.main;
        }

        void Update()
        {
            // 灵力自然回复
            if (_currentSpiritEnergy < maxSpiritEnergy)
                _currentSpiritEnergy = Mathf.Min(maxSpiritEnergy, _currentSpiritEnergy + spiritRegenRate * Time.deltaTime);

            // 左键点击选择目标
            if (Input.GetMouseButtonDown(0))
            {
                TrySelectTarget();
            }

            // 右键释放灵击（攻击当前锁定目标）
            if (Input.GetMouseButtonDown(1) && _lockedTarget != null && !_lockedTarget.IsDead)
            {
                CastSpiritAttack();
            }

            // Q键：主修功法技能
            if (Input.GetKeyDown(KeyCode.Q) && _lockedTarget != null)
            {
                CastTechnique();
            }

            // 高亮锁定目标
            if (_lockedTarget != null && _lockedTarget.IsDead)
                _lockedTarget = null;
        }

        /// <summary>
        /// 左键点击：选中+攻击一步完成
        /// </summary>
        void TrySelectTarget()
        {
            if (_cam == null) return;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 50f))
            {
                var enemy = hit.collider.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.IsDead)
                {
                    _lockedTarget = enemy;
                    if (_currentSpiritEnergy >= spiritCostPerAttack)
                        CastSpiritAttack();
                    else
                        CastBasicAttack();
                    return;
                }

                // V2.1: 点击了NPC→攻击NPC→触发犯罪系统
                var npc = hit.collider.GetComponent<EarthOnline.NPC.NPCBase>();
                if (npc != null)
                {
                    AttackNPC(npc);
                    return;
                }
            }
            if (_lockedTarget != null) { _lockedTarget = null; }
        }

        /// <summary>攻击NPC——触发犯罪</summary>
        void AttackNPC(EarthOnline.NPC.NPCBase npc)
        {
            int dmg = Mathf.RoundToInt(baseSpiritAttack * 0.5f);
            Debug.Log($"[Combat] ⚔️ 攻击了{npc.npcName}！");
            CrimeSystem.Instance?.ReportAssault(npc.npcName, npc.transform.position);
            CombatFeedback.Shake(0.1f);
            // VFX: 命中圆环特效
            CombatFeedback.SpawnHitVFX(_lockedTarget.transform.position);
        }

        /// <summary>
        /// 免费基础攻击（灵力不足时可用，伤害减半）
        /// </summary>
        void CastBasicAttack()
        {
            if (_lockedTarget == null || _lockedTarget.IsDead) return;
            int dmg = Mathf.RoundToInt(baseSpiritAttack * 0.4f);
            _lockedTarget.TakeDamage(dmg, false);
            FloatingDamage.Spawn(_lockedTarget.transform.position, $"-{dmg}", new Color(0.5f, 0.5f, 0.5f));
            VFXManager.Instance?.SpawnSpiritBolt(GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero, _lockedTarget.transform.position, crit);
        }

        /// <summary>
        /// 右键释放基础灵击
        /// </summary>
        void CastSpiritAttack()
        {
            if (Time.time - _lastCastTime < castTime) return; // 还在掐诀
            if (_currentSpiritEnergy < spiritCostPerAttack)
            {
                CombatFeedback.LowSpiritWarning();
                return;
            }

            _lastCastTime = Time.time;
            _currentSpiritEnergy -= spiritCostPerAttack;

            // 境界压制计算
            int playerRealm = GetPlayerRealm();
            float suppressionMult = 1f;
            if (playerRealm > 0) // 简单：玩家境界越高，伤害越高
                suppressionMult = 1f + playerRealm * 0.3f;

            // 装备加成
            int eqBonus = EquipmentManager.Instance?.AttackBonus ?? 0;

            // 伤害计算
            float totalAtk = (baseSpiritAttack + eqBonus) * suppressionMult;
            float comboMult = SkillComboSystem.Instance?.RegisterHit(_lockedTarget.enemyId) ?? 1f;
            totalAtk *= comboMult;
            float weatherMult = WeatherSystem.Instance?.WeatherAttackModifier ?? 1f;
            totalAtk *= weatherMult;

            bool crit = Random.value < 0.12f;
            int damage = crit ? Mathf.RoundToInt(totalAtk * 1.8f) : Mathf.RoundToInt(totalAtk);

            _lockedTarget.TakeDamage(damage, crit);
            CombatFeedback.Shake(crit ? 0.15f : 0.08f);
            // VFX: 命中圆环特效
            CombatFeedback.SpawnHitVFX(_lockedTarget.transform.position);

            string critText = crit ? " 暴击！" : "";
            Debug.Log($"[Combat] 灵击！{damage}伤害 → {_lockedTarget.enemyName}{critText} (灵力:{_currentSpiritEnergy:F0}/{maxSpiritEnergy})");

            VFXManager.Instance?.SpawnSpiritBolt(GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero, _lockedTarget.transform.position, crit);
            FloatingDamage.Spawn(_lockedTarget.transform.position,
                crit ? $"-{damage} 暴击!" : $"-{damage}",
                crit ? new Color(1f, 0.85f, 0f) : new Color(0.6f, 0.8f, 1f));

            float dist = Vector3.Distance(
                GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero,
                _lockedTarget.transform.position);
            Debug.Log($"[Combat] 距离:{dist:F1}m | 灵力:{_currentSpiritEnergy:F0} | 伤害加成:x{suppressionMult:F1}");
        }

        /// <summary>
        /// Q键释放功法技能（需修炼对应功法）
        /// </summary>
        void CastTechnique()
        {
            if (_currentSpiritEnergy < 15f)
            {
                Debug.Log("[Combat] 灵力不足！功法技能需要15灵力。");
                return;
            }
            _currentSpiritEnergy -= 15f;

            // 示例：剑气斩（剑修技能）
            int dmg = Mathf.RoundToInt(baseSpiritAttack * 2.5f);
            _lockedTarget.TakeDamage(dmg, false);
            FloatingDamage.Spawn(_lockedTarget.transform.position, $"-{dmg} 剑气!", new Color(0.3f, 0.7f, 1f));
            VFXManager.Instance?.SpawnSpiritBolt(GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero, _lockedTarget.transform.position, crit);
            Debug.Log($"[Combat] ⚔️ 剑气斩！{dmg}伤害 → {_lockedTarget.enemyName}");
        }

        int GetPlayerRealm()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return 0;
            int c = stats.cultivation;
            if (c >= 1500) return 5;
            if (c >= 1000) return 4;
            if (c >= 600) return 3;
            if (c >= 300) return 2;
            if (c >= 100) return 1;
            return 0;
        }

        public void CycleTarget()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var all = Object.FindObjectsOfType<EnemyAI>();
            var enemies = new List<EnemyAI>();
            foreach (var e in all) if (!e.IsDead) enemies.Add(e);
            enemies.Sort((a, b) =>
                Vector3.Distance(player.transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(player.transform.position, b.transform.position)));

            if (enemies.Count == 0) { Debug.Log("[Combat] 附近没有敌人。"); return; }

            int idx = _lockedTarget != null ? enemies.IndexOf(_lockedTarget) : -1;
            idx = (idx + 1) % enemies.Count;
            _lockedTarget = enemies[idx];
            Debug.Log($"[Combat] 🎯 目标切换: {_lockedTarget.enemyName} ({_lockedTarget.currentHP}/{_lockedTarget.maxHP}HP)");
        }

        void OnGUI()
        {
            // 简易灵力条（屏幕左上）
            if (_currentSpiritEnergy < maxSpiritEnergy * 0.3f)
            {
                GUI.color = Color.red;
            }
            else
            {
                GUI.color = new Color(0.3f, 0.6f, 1f);
            }
            GUI.Box(new Rect(10, 60, 150, 20), $"灵力: {_currentSpiritEnergy:F0}/{maxSpiritEnergy}");
        }
    }
}
