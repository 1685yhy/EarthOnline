using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 技能/功法数据加载器。
    ///
    /// 从 Resources/Data/Skills.json 加载完整技能库（30技能），
    /// 提供运行时查询 API 供 CombatSystem 等模块使用。
    ///
    /// JSON 是唯一天源——改数据不动代码。
    ///
    /// 使用方式：
    ///   任意 MonoBehaviour 中调用：
    ///     SkillDataLoader.LoadFromResources();
    ///
    ///   或将此脚本挂载到任意 GameObject 并勾选 loadOnAwake。
    /// </summary>
    public class SkillDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]
        [SerializeField, Tooltip("Resources 路径（不含扩展名）")]
        private string jsonResourcesPath = "Data/Skills";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载前是否清空已有技能数据")]
        private bool clearBeforeLoad;

        [Header("=== 状态 ===")]
        [SerializeField]
        private int lastLoadedCount;

        [SerializeField]
        private bool loadSucceeded;

        // ────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadFromResources(jsonResourcesPath, clearBeforeLoad);
            }
        }

        [ContextMenu("重新加载技能数据")]
        public void Reload()
        {
            LoadFromResources(jsonResourcesPath, clearBeforeLoad);
        }

        // ────────────────────────────────────────────────────────────────
        //  静态 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>所有技能定义的主字典（skillId -> SkillDef）</summary>
        public static Dictionary<string, SkillDef> AllSkills { get; private set; } = new();

        /// <summary>按分类分组的技能列表</summary>
        public static Dictionary<string, List<SkillDef>> SkillsByCategory { get; private set; } = new();

        /// <summary>非被动技能列表（可主动施放）</summary>
        public static List<SkillDef> ActiveSkills { get; private set; } = new();

        /// <summary>被动技能列表</summary>
        public static List<SkillDef> PassiveSkills { get; private set; } = new();

        /// <summary>
        /// 从 Resources 加载技能 JSON。
        /// </summary>
        /// <param name="path">Resources 路径（不含扩展名，默认 "Data/Skills"）</param>
        /// <param name="clearFirst">加载前是否清空已有数据</param>
        /// <returns>成功加载的技能数量，-1 表示失败</returns>
        public static int LoadFromResources(string path = "Data/Skills", bool clearFirst = false)
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[SkillDataLoader] 未找到技能数据: {path}.json (Resources 路径)");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<SkillDatabaseJson>(jsonAsset.text);
            if (wrapper?.skills == null || wrapper.skills.Length == 0)
            {
                Debug.LogWarning("[SkillDataLoader] 技能数据为空或格式无效");
                return -1;
            }

            if (clearFirst)
            {
                AllSkills.Clear();
                SkillsByCategory.Clear();
                ActiveSkills.Clear();
                PassiveSkills.Clear();
                Debug.Log("[SkillDataLoader] 已清空现有技能数据");
            }

            int loadedCount = 0;
            foreach (var def in wrapper.skills)
            {
                if (string.IsNullOrEmpty(def.skillId))
                {
                    Debug.LogWarning("[SkillDataLoader] 跳过空 skillId 的技能");
                    continue;
                }

                // 注入主字典
                AllSkills[def.skillId] = def;

                // 按分类索引
                if (!SkillsByCategory.ContainsKey(def.category))
                    SkillsByCategory[def.category] = new List<SkillDef>();
                SkillsByCategory[def.category].Add(def);

                // 主动/被动分类
                if (def.isPassive)
                    PassiveSkills.Add(def);
                else
                    ActiveSkills.Add(def);

                loadedCount++;
            }

            Debug.Log($"[SkillDataLoader] 成功加载 {loadedCount} 个技能 ← {path}.json" +
                      $" | 剑法:{CountByCategory("sword")} 法术:{CountByCategory("spell")}" +
                      $" 身法:{CountByCategory("movement")} 炼丹:{CountByCategory("alchemy")}" +
                      $" 炼器:{CountByCategory("forging")} 神识:{CountByCategory("perception")}" +
                      $" 特殊:{CountByCategory("special")}");

            return loadedCount;
        }

        // ────────────────────────────────────────────────────────────────
        //  查询 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>获取技能定义。</summary>
        public static SkillDef GetDef(string skillId)
        {
            return AllSkills.TryGetValue(skillId, out var def) ? def : null;
        }

        /// <summary>获取某分类的技能数量。</summary>
        public static int CountByCategory(string category)
        {
            return SkillsByCategory.TryGetValue(category, out var list) ? list.Count : 0;
        }

        /// <summary>获取某分类的所有技能。</summary>
        public static List<SkillDef> GetSkillsByCategory(string category)
        {
            return SkillsByCategory.TryGetValue(category, out var list)
                ? new List<SkillDef>(list)
                : new List<SkillDef>();
        }

        /// <summary>
        /// 获取玩家当前境界可用的技能列表。
        /// 境界索引: 0=凡人, 1=练气, 2=筑基, 3=金丹, 4=元婴, 5=化神, 6=渡劫, 7=大成
        /// </summary>
        public static List<SkillDef> GetAvailableSkills(int realmIndex, int layer)
        {
            var result = new List<SkillDef>();
            foreach (var def in AllSkills.Values)
            {
                if (def.requiredRealm < realmIndex ||
                    (def.requiredRealm == realmIndex && def.requiredLayer <= layer))
                {
                    result.Add(def);
                }
            }
            return result;
        }

        /// <summary>获取玩家当前可用的主动技能。</summary>
        public static List<SkillDef> GetAvailableActiveSkills(int realmIndex, int layer)
        {
            var allAvailable = GetAvailableSkills(realmIndex, layer);
            var result = new List<SkillDef>();
            foreach (var s in allAvailable)
            {
                if (!s.isPassive) result.Add(s);
            }
            return result;
        }

        /// <summary>获取可连击的目标技能（基于 comboInto 字段）。</summary>
        public static List<SkillDef> GetComboTargets(string currentSkillId)
        {
            var def = GetDef(currentSkillId);
            if (def?.comboInto == null || def.comboInto.Length == 0)
                return new List<SkillDef>();

            var result = new List<SkillDef>();
            foreach (var targetId in def.comboInto)
            {
                var target = GetDef(targetId);
                if (target != null) result.Add(target);
            }
            return result;
        }

        /// <summary>检查玩家是否满足技能解锁条件。</summary>
        public static bool IsUnlocked(SkillDef def, int realmIndex, int layer)
        {
            if (def.requiredRealm < realmIndex) return true;
            if (def.requiredRealm == realmIndex && def.requiredLayer <= layer) return true;
            return false;
        }

        /// <summary>获取所有分类名称列表。</summary>
        public static List<string> GetAllCategories()
        {
            return new List<string>(SkillsByCategory.Keys);
        }

        /// <summary>获取总技能数。</summary>
        public static int TotalCount => AllSkills.Count;
    }

    // ────────────────────────────────────────────────────────────────────
    //  JSON 数据模型 (与 Skills.json 严格对应)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>JSON 根容器。</summary>
    [System.Serializable]
    public class SkillDatabaseJson
    {
        public SkillDef[] skills;
    }

    /// <summary>技能定义——单个技能的完整数据。</summary>
    [System.Serializable]
    public class SkillDef
    {
        // ── 基础 ──
        public string skillId;           // 唯一标识（如 "sword_basic_slash"）
        public string displayName;       // 显示名称（如 "基础斩击"）
        public string description;       // 描述/叙事文本
        public string category;          // sword / spell / movement / alchemy / forging / perception / special

        // ── 解锁条件 ──
        public int requiredRealm;        // 所需境界（0=Mortal ... 7=GreatAscension）
        public int requiredLayer;        // 所需层数（0-based）
        public string unlockCondition;   // 解锁条件描述文本（供 UI/日志使用）

        // ── 战斗参数 ──
        public int spiritCost;           // 灵力消耗
        public float cooldown;           // 冷却时间（秒）
        public float damageMultiplier;   // 伤害倍率（基于 CombatSystem.baseSpiritAttack）

        // ── 效果 ──
        public SkillBuffEffect[] buffEffects;  // 施放时附加的 Buff 效果（可为 null）
        public string[] comboInto;             // 可连击进入的下一个技能 skillId 数组（可为 null）
        public bool isPassive;                 // 是否为被动技能（炼丹/炼器/神识类）
    }

    /// <summary>技能附加的 Buff 效果。</summary>
    [System.Serializable]
    public class SkillBuffEffect
    {
        public string buffType;   // BuffType 枚举名: AttackUp / DefenseUp / HealOverTime / SpeedUp / DamageOverTime
        public float value;       // Buff 数值（加算/减算）
        public float duration;    // 持续秒数
    }
}
