using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// Buff/Debuff 数据加载器。
    ///
    /// 从 Resources/Data/Buffs.json 加载完整 Buff 库（35定义），
    /// 提供运行时查询 API 供 BuffSystem / CombatSystem 等模块使用。
    ///
    /// JSON 是唯一天源——改数据不动代码。
    ///
    /// 使用方式：
    ///   任意 MonoBehaviour 中调用：
    ///     BuffDataLoader.LoadFromResources();
    ///
    ///   或将此脚本挂载到任意 GameObject 并勾选 loadOnAwake。
    /// </summary>
    public class BuffDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]
        [SerializeField, Tooltip("Resources 路径（不含扩展名）")]
        private string jsonResourcesPath = "Data/Buffs";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载前是否清空已有 Buff 数据")]
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

        [ContextMenu("重新加载 Buff 数据")]
        public void Reload()
        {
            LoadFromResources(jsonResourcesPath, clearBeforeLoad);
        }

        // ────────────────────────────────────────────────────────────────
        //  静态 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>所有 Buff 定义的主字典（buffId -> BuffDef）</summary>
        public static Dictionary<string, BuffDef> AllBuffs { get; private set; } = new();

        /// <summary>按类型分组的 Buff 列表</summary>
        public static Dictionary<string, List<BuffDef>> BuffsByType { get; private set; } = new();

        /// <summary>按类别分组的 Buff 列表</summary>
        public static Dictionary<string, List<BuffDef>> BuffsByCategory { get; private set; } = new();

        /// <summary>Buff 列表（可驱散）</summary>
        public static List<BuffDef> DispellableBuffs { get; private set; } = new();

        /// <summary>Debuff 列表（可驱散）</summary>
        public static List<BuffDef> DispellableDebuffs { get; private set; } = new();

        /// <summary>永久性 Buff 列表</summary>
        public static List<BuffDef> PermanentBuffs { get; private set; } = new();

        /// <summary>
        /// 从 Resources 加载 Buff JSON。
        /// </summary>
        /// <param name="path">Resources 路径（不含扩展名，默认 "Data/Buffs"）</param>
        /// <param name="clearFirst">加载前是否清空已有数据</param>
        /// <returns>成功加载的 Buff 数量，-1 表示失败</returns>
        public static int LoadFromResources(string path = "Data/Buffs", bool clearFirst = false)
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[BuffDataLoader] 未找到 Buff 数据: {path}.json (Resources 路径)");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<BuffDatabaseJson>(jsonAsset.text);
            if (wrapper?.buffs == null || wrapper.buffs.Length == 0)
            {
                Debug.LogWarning("[BuffDataLoader] Buff 数据为空或格式无效");
                return -1;
            }

            if (clearFirst)
            {
                AllBuffs.Clear();
                BuffsByType.Clear();
                BuffsByCategory.Clear();
                DispellableBuffs.Clear();
                DispellableDebuffs.Clear();
                PermanentBuffs.Clear();
                Debug.Log("[BuffDataLoader] 已清空现有 Buff 数据");
            }

            int loadedCount = 0;
            foreach (var def in wrapper.buffs)
            {
                if (string.IsNullOrEmpty(def.buffId))
                {
                    Debug.LogWarning("[BuffDataLoader] 跳过空 buffId 的 Buff");
                    continue;
                }

                // 注入主字典
                AllBuffs[def.buffId] = def;

                // 按 buffType 索引 (Buff / Debuff / Special)
                if (!BuffsByType.ContainsKey(def.buffType))
                    BuffsByType[def.buffType] = new List<BuffDef>();
                BuffsByType[def.buffType].Add(def);

                // 按 category 索引
                if (!BuffsByCategory.ContainsKey(def.category))
                    BuffsByCategory[def.category] = new List<BuffDef>();
                BuffsByCategory[def.category].Add(def);

                // 可驱散 Buff / Debuff 列表
                if (def.isDispellable)
                {
                    if (def.buffType == "Buff" || def.buffType == "Special")
                        DispellableBuffs.Add(def);
                    else if (def.buffType == "Debuff")
                        DispellableDebuffs.Add(def);
                }

                // 永久性 Buff
                if (def.durationType == "Permanent")
                    PermanentBuffs.Add(def);

                loadedCount++;
            }

            Debug.Log($"[BuffDataLoader] 成功加载 {loadedCount} 个 Buff ← {path}.json" +
                      $" | 增益:{CountByType("Buff")} 减益:{CountByType("Debuff")}" +
                      $" 特殊:{CountByType("Special")} 可驱散:{DispellableBuffs.Count + DispellableDebuffs.Count}");

            return loadedCount;
        }

        // ────────────────────────────────────────────────────────────────
        //  查询 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>获取 Buff 定义。</summary>
        public static BuffDef GetDef(string buffId)
        {
            return AllBuffs.TryGetValue(buffId, out var def) ? def : null;
        }

        /// <summary>获取某类型的 Buff 数量。</summary>
        public static int CountByType(string buffType)
        {
            return BuffsByType.TryGetValue(buffType, out var list) ? list.Count : 0;
        }

        /// <summary>获取某类别的 Buff 数量。</summary>
        public static int CountByCategory(string category)
        {
            return BuffsByCategory.TryGetValue(category, out var list) ? list.Count : 0;
        }

        /// <summary>获取某类型的所有 Buff。</summary>
        public static List<BuffDef> GetBuffsByType(string buffType)
        {
            return BuffsByType.TryGetValue(buffType, out var list)
                ? new List<BuffDef>(list)
                : new List<BuffDef>();
        }

        /// <summary>获取某类别的所有 Buff。</summary>
        public static List<BuffDef> GetBuffsByCategory(string category)
        {
            return BuffsByCategory.TryGetValue(category, out var list)
                ? new List<BuffDef>(list)
                : new List<BuffDef>();
        }

        /// <summary>
        /// 创建运行时 Buff 实例（用于 BuffManager）。
        /// 从定义创建可被 BuffSystem 处理的运行时对象。
        /// </summary>
        public static Buff CreateRuntimeBuff(BuffDef def, float durationOverride = -1f, string sourceName = "")
        {
            return new Buff
            {
                type = ParseBuffType(def.effectStat),
                duration = durationOverride >= 0 ? durationOverride : def.baseDuration,
                value = def.effectValue,
                remaining = durationOverride >= 0 ? durationOverride : def.baseDuration,
                sourceName = sourceName
            };
        }

        /// <summary>按 buffId 创建运行时 Buff 对象。</summary>
        public static Buff CreateRuntimeBuffById(string buffId, float durationOverride = -1f, string sourceName = "")
        {
            var def = GetDef(buffId);
            if (def == null)
            {
                Debug.LogError($"[BuffDataLoader] 未找到 Buff 定义: {buffId}");
                return null;
            }
            return CreateRuntimeBuff(def, durationOverride, sourceName);
        }

        /// <summary>获取指定类别所有 Buff 的运行时实例列表。</summary>
        public static List<Buff> CreateRuntimeBuffsByCategory(string category, string sourceName = "")
        {
            var defs = GetBuffsByCategory(category);
            var list = new List<Buff>(defs.Count);
            foreach (var def in defs)
            {
                list.Add(CreateRuntimeBuff(def, -1f, sourceName));
            }
            return list;
        }

        /// <summary>获取总 Buff 数。</summary>
        public static int TotalCount => AllBuffs.Count;

        // ────────────────────────────────────────────────────────────────
        //  内部映射
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 将 JSON 中的 effectStat 字符串映射回 BuffType 枚举。
        /// </summary>
        private static BuffType ParseBuffType(string effectStat)
        {
            return effectStat switch
            {
                "ATK"             => BuffType.AttackUp,
                "DEF"             => BuffType.DefenseUp,
                "SPD"             => BuffType.SpeedUp,
                "SpiritRegen"     => BuffType.HealOverTime,
                "PoisonDOT"       => BuffType.DamageOverTime,
                "BurnDOT"         => BuffType.DamageOverTime,
                "CritRate"        => BuffType.AttackUp,
                "LifeSteal"       => BuffType.AttackUp,
                "Thorns"          => BuffType.DefenseUp,
                "Invincible"      => BuffType.DefenseUp,
                "ComboRate"       => BuffType.AttackUp,
                "Stun"            => BuffType.SpeedUp,
                "Silence"         => BuffType.SpeedUp,
                "Freeze"          => BuffType.SpeedUp,
                "MoveSpeed"       => BuffType.SpeedUp,
                "AllStats"        => BuffType.AttackUp,
                "CultivationSpeed"=> BuffType.HealOverTime,
                _                 => BuffType.AttackUp
            };
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  JSON 数据模型 (与 Buffs.json 严格对应)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>JSON 根容器。</summary>
    [System.Serializable]
    public class BuffDatabaseJson
    {
        public BuffDef[] buffs;
    }

    /// <summary>Buff 定义——单个 Buff 的全部数据。</summary>
    [System.Serializable]
    public class BuffDef
    {
        // ── 基础 ──
        public string buffId;             // 唯一标识（如 "buff_atk_up"）
        public string displayName;        // 显示名称（如 "攻击强化"）
        public string description;        // 描述/叙事文本
        public string buffType;           // Buff / Debuff / Special
        public string category;           // combat_buff / combat_debuff / cultivation / exploration / special / environment

        // ── 持续时间 ──
        public string durationType;       // Timed / Permanent / Conditional
        public float baseDuration;        // 基础持续秒数（Permanent/Conditional 填 0）
        public int maxStacks;             // 最大叠加层数（1 = 不可叠加）

        // ── 效果参数 ──
        public string effectType;         // Additive / Multiplicative
        public float effectValue;         // 效果数值（加算值/倍率）
        public string effectStat;         // ATK / DEF / SPD / CritRate / SpiritRegen / LifeSteal / Thorns / Invincible / ComboRate / PoisonDOT / BurnDOT / Stun / Silence / Freeze / MoveSpeed / Stealth / Gathering / DiscoveryRate / CultivationSpeed / BreakthroughRate / PillEffectiveness / SpiritStoneDrop / EXP / AllStats / VoidDamage

        // ── 表现 ──
        public string iconHint;           // 图标路径提示
        public float[] particleColor;     // 粒子颜色 [r, g, b] (0~1)

        // ── 交互规则 ──
        public string stackBehavior;      // Refresh / Independent / Replace
        public bool isDispellable;        // 是否可被驱散

        /// <summary>此 Buff 可治愈/移除的其他 Buff ID 列表（用于解毒/净化）</summary>
        public string[] curesBuffs;
    }
}
