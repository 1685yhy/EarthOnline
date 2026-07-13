using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Combat
{
    // ─── JSON 配置数据类 ──────────────────────────────────────────────

    /// <summary>
    /// 与 BossConfigs.json 中单个 BOSS 条目对应的可序列化数据结构。
    /// </summary>
    [Serializable]
    public class BossConfigEntry
    {
        // 基本信息（必填）
        public string bossId;
        public string displayName;
        public string title;
        public string bossType;          // 枚举名称字符串，运行时转为 BossType
        public int realm;

        // 基础属性（JSON中使用 baseHP 以符合配置习惯）
        public float baseHP;
        public float baseAttack;
        public float baseDefense;
        public float baseCritRate = 0.05f;

        // 索敌范围
        public float detectRange = 30f;
        public float aggroRange = 20f;
        public float leashRange = 50f;

        // 阶段转换（可选）
        public PhaseTransitionConfig[] phases;

        // 狂暴时间限制（秒）
        public float enrageTimeLimit = 300f;

        // 弱点（可选）
        public WeaknessConfig[] weaknesses;

        // 独特机制（可选）
        public MechanicConfig[] mechanics;

        // 攻击库（可选）
        public AttackConfig[] attacks;

        // 外交（可选）
        public DiplomacyConfig diplomacy;

        // 是否可绕过（潜行）
        public bool stealthVulnerable = true;

        // 掉落（可选）
        public DropTableConfig dropTable;

        // 刷新天数
        public int respawnTimeDays = 3;

        // 背景故事
        public string storyContext;

        // 出场对话
        public string entranceDialogue = "你终于来了……我等这一刻很久了。";
        public string defeatDialogue = "不……可……能……";
        public string retreatDialogue = "逃吧……但下次不会这么幸运了。";
    }

    /// <summary>JSON 阶段转换定义（对应 PhaseTransitionDef）</summary>
    [Serializable]
    public class PhaseTransitionConfig
    {
        public string triggerType;       // HP / Time / Behavior / Environment
        public float triggerValue;
        public string[] newMechanics;
        public string[] removedMechanics;
        public string[] newAttacks;
        public string dialogue;
        public ColorConfig visualColor;
        public string visualEffectName;
        public bool dropCheckpoint;
    }

    /// <summary>JSON 弱点定义（对应 WeaknessDef）</summary>
    [Serializable]
    public class WeaknessConfig
    {
        public string weaknessType;      // Element / Timing / PartBreak / ItemCounter / Environment / Fear
        public string displayName;
        public string description;
        public float damageMultiplier = 1.5f;
        public string elementType;
        public float exposureDuration;
    }

    /// <summary>JSON 机制定义（对应 MechanicDef）</summary>
    [Serializable]
    public class MechanicConfig
    {
        public string mechanicName;
        public string description;
        public string counterMethod;
    }

    /// <summary>JSON 攻击定义（对应 AttackDef）</summary>
    [Serializable]
    public class AttackConfig
    {
        public string attackName;
        public float damageMultiplier = 1.0f;
        public float cooldown;
        public string animationTrigger;
        public string effectPrefabPath;
    }

    /// <summary>JSON 外交选项（对应 DiplomacyDef）</summary>
    [Serializable]
    public class DiplomacyConfig
    {
        public bool hasDiplomacy;
        public string conditionDescription;
        public string[] requiredItems;
        public float baseSuccessRate;
        public float peaceWindowDuration;
    }

    /// <summary>JSON 颜色配置</summary>
    [Serializable]
    public class ColorConfig
    {
        public float r = 1f;
        public float g = 1f;
        public float b = 1f;
        public float a = 1f;

        public Color ToColor() => new Color(r, g, b, a);
    }

    /// <summary>JSON 掉落条目（对应 DropEntry）</summary>
    [Serializable]
    public class DropEntryConfig
    {
        public string itemId;
        public string itemName;
        public float dropChance = 1.0f;
        public int minCount = 1;
        public int maxCount = 1;
    }

    /// <summary>JSON 掉落表（对应 DropTable）</summary>
    [Serializable]
    public class DropTableConfig
    {
        public DropEntryConfig[] guaranteed;
        public DropEntryConfig[] normal;
        public DropEntryConfig[] conditional;
    }

    /// <summary>JSON 根数据（包装数组以兼容 JsonUtility）</summary>
    [Serializable]
    public class BossConfigData
    {
        public BossConfigEntry[] bosses;
    }

    // ─── BOSS 配置加载器 ──────────────────────────────────────────────

    /// <summary>
    /// 从 Resources/Data/BossConfigs.json 加载 BOSS 配置，
    /// 解析后创建运行时 BossDef 实例。
    ///
    /// 自动装载机制：
    /// - [BeforeSceneLoad] 预加载全部 BossDef 到缓存
    /// - [AfterSceneLoad] 扫描场景中所有 BossAI 组件并自动分配
    /// - BossAI.Awake() 也可通过 LoadBossForGameObject() 按需加载
    /// </summary>
    public static class BossConfigLoader
    {
        private const string ConfigResourcePath = "Data/BossConfigs";

        // 枚举缓存，避免重复解析
        private static readonly Dictionary<string, BossType> BossTypeMap
            = new Dictionary<string, BossType>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, PhaseTriggerType> PhaseTriggerMap
            = new Dictionary<string, PhaseTriggerType>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, WeaknessType> WeaknessTypeMap
            = new Dictionary<string, WeaknessType>(StringComparer.OrdinalIgnoreCase);

        /// <summary>运行时 BossDef 实例缓存，避免重复加载。</summary>
        private static BossDef[] _cachedBosses;

        /// <summary>
        /// GameObject 名称到 bossId 的显式映射。
        /// 在 Inspector 中通过 BossAI.bossIdOverride 或脚本调用 RegisterNameMapping() 设置。
        /// 优先级高于 LoadBossForGameObject 的模糊匹配。
        /// </summary>
        private static readonly Dictionary<string, string> _nameToBossIdMapping
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册一个 GameObject 名称到 bossId 的显式映射。
        /// 此后 LoadBossForGameObject 会优先使用此映射查找。
        /// </summary>
        public static void RegisterNameMapping(string gameObjectName, string bossId)
        {
            if (!string.IsNullOrEmpty(gameObjectName) && !string.IsNullOrEmpty(bossId))
                _nameToBossIdMapping[gameObjectName] = bossId;
        }

        static BossConfigLoader()
        {
            // 初始化枚举映射
            foreach (BossType val in Enum.GetValues(typeof(BossType)))
                BossTypeMap[val.ToString()] = val;
            foreach (PhaseTriggerType val in Enum.GetValues(typeof(PhaseTriggerType)))
                PhaseTriggerMap[val.ToString()] = val;
            foreach (WeaknessType val in Enum.GetValues(typeof(WeaknessType)))
                WeaknessTypeMap[val.ToString()] = val;
        }

        /// <summary>
        /// 场景加载前预加载 BOSS 配置到缓存，使 Awake() 阶段即可获取 BossDef。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreloadCache()
        {
            // 预加载并写入缓存，使 Awake() 阶段即可命中
            _cachedBosses = LoadInternal();
            if (_cachedBosses != null && _cachedBosses.Length > 0)
            {
                Debug.Log($"[BossConfigLoader] 预加载完成: {_cachedBosses.Length} 个 BOSS 定义已缓存");
            }
        }

        /// <summary>
        /// 场景加载完成后自动扫描所有 BossAI 组件并分配 BossDef。
        /// 已分配的或已禁用的跳过（由 BossAI.Awake 自身负责加载）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAssignAfterSceneLoad()
        {
            AutoAssignAllBossDefs();
        }

        // ── 公开 API ──────────────────────────────────────────────────

        /// <summary>
        /// 加载全部 BOSS 配置并创建运行时实例。
        /// 返回 BossDef 数组，加载失败时返回 null 并打印错误。
        /// 结果会被缓存，重复调用不会重新加载。
        /// </summary>
        public static BossDef[] LoadAllBosses()
        {
            if (_cachedBosses != null)
                return _cachedBosses;

            BossDef[] result = LoadInternal();
            _cachedBosses = result;
            return result;
        }

        /// <summary>
        /// 按 bossId 查找单个 BOSS。
        /// </summary>
        public static BossDef LoadBoss(string bossId)
        {
            BossDef[] all = LoadAllBosses();
            if (all == null) return null;

            foreach (BossDef boss in all)
            {
                if (boss != null && boss.bossId == bossId)
                    return boss;
            }

            Debug.LogWarning($"[BossConfigLoader] 未找到 bossId: {bossId}");
            return null;
        }

        /// <summary>
        /// 从 Resources 同步重新加载（先清空缓存再加载）。
        /// </summary>
        public static void ReloadAll()
        {
            _cachedBosses = null;
            Resources.UnloadUnusedAssets();
            LoadAllBosses();
        }

        // ─── 场景 BOSS 自动分配 ─────────────────────────────────────────

        /// <summary>
        /// 根据 GameObject 名称自动匹配并返回最合适的 BossDef。
        /// 匹配策略：
        ///   1. GameObject 名称包含 bossId（不区分大小写）
        ///   2. GameObject 名称包含 displayName（不区分大小写）
        ///   3. 返回第一个 BossDef 作为兜底
        /// </summary>
        /// <param name="gameObject">需要分配 BossDef 的 BOSS GameObject</param>
        /// <returns>匹配到的 BossDef，无可用数据时返回 null</returns>
        public static BossDef LoadBossForGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            BossDef[] all = LoadAllBosses();
            if (all == null || all.Length == 0)
                return null;

            string lowerName = gameObject.name.ToLowerInvariant();

            // 策略 0：显式映射查找 — 由 Inspector 或 RegisterNameMapping() 设置
            if (_nameToBossIdMapping.TryGetValue(gameObject.name, out string mappedBossId))
            {
                foreach (BossDef def in all)
                {
                    if (def != null && def.bossId == mappedBossId)
                        return def;
                }
                Debug.LogWarning($"[BossConfigLoader] 显式映射 '{gameObject.name}' -> '{mappedBossId}' " +
                                 $"但缓存中未找到该 bossId，降级到模糊匹配。", gameObject);
            }

            // 策略 1：按 bossId 匹配
            foreach (BossDef def in all)
            {
                if (def != null && !string.IsNullOrEmpty(def.bossId) &&
                    lowerName.Contains(def.bossId.ToLowerInvariant()))
                {
                    return def;
                }
            }

            // 策略 2：按 displayName 匹配
            foreach (BossDef def in all)
            {
                if (def != null && !string.IsNullOrEmpty(def.displayName) &&
                    lowerName.Contains(def.displayName.ToLowerInvariant()))
                {
                    return def;
                }
            }

            // 策略 3：兜底——返回第一个可用 BossDef
            Debug.LogWarning(
                $"[BossConfigLoader] 无法为 '{gameObject.name}' 找到匹配的 BossDef，已分配 '{all[0]?.bossId}' 作为兜底",
                gameObject);
            return all[0];
        }

        /// <summary>
        /// 扫描当前场景中所有 BossAI 组件，为未分配 BossDef 的组件自动分配。
        /// 可在 Start() 之前被调用，与 BossAI.Awake() 的自动加载形成互为备份。
        /// </summary>
        public static void AutoAssignAllBossDefs()
        {
            BossDef[] allBosses = LoadAllBosses();
            if (allBosses == null || allBosses.Length == 0)
            {
                Debug.LogWarning("[BossConfigLoader] 无法自动分配：未加载到任何 BOSS 定义");
                return;
            }

            BossAI[] allBossAIs = UnityEngine.Object.FindObjectsOfType<BossAI>();
            if (allBossAIs.Length == 0)
                return;

            int assignedCount = 0;
            foreach (BossAI ai in allBossAIs)
            {
                if (ai.bossDef != null)
                    continue; // 已在 Inspector 或 Awake 中分配

                BossDef def = LoadBossForGameObject(ai.gameObject);
                if (def != null)
                {
                    ai.bossDef = def;
                    assignedCount++;
                    Debug.Log($"[BossConfigLoader] 自动分配 '{def.bossId}' → '{ai.gameObject.name}'", ai.gameObject);
                }
            }

            if (assignedCount > 0)
            {
                Debug.Log($"[BossConfigLoader] 场景 BOSS 自动分配完成: {assignedCount} 个 BossAI 已赋值");
            }
        }

        // ── 内部实现 ──────────────────────────────────────────────────

        /// <summary>
        /// 内部加载方法，不从缓存读取，由 LoadAllBosses 调用。
        /// </summary>
        private static BossDef[] LoadInternal()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[BossConfigLoader] 配置文件未找到: Resources/{ConfigResourcePath}.json");
                return null;
            }

            BossConfigData configData;
            try
            {
                configData = JsonUtility.FromJson<BossConfigData>(jsonAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BossConfigLoader] JSON 解析失败: {ex.Message}");
                return null;
            }

            if (configData == null || configData.bosses == null || configData.bosses.Length == 0)
            {
                Debug.LogError("[BossConfigLoader] 配置数据为空，请检查 BossConfigs.json");
                return null;
            }

            BossDef[] bosses = new BossDef[configData.bosses.Length];
            for (int i = 0; i < configData.bosses.Length; i++)
            {
                try
                {
                    bosses[i] = CreateBossFromConfig(configData.bosses[i]);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BossConfigLoader] 创建 BOSS [{configData.bosses[i].bossId}] 失败: {ex.Message}");
                }
            }

            return bosses;
        }

        // ── 内部转换 ──────────────────────────────────────────────────

        private static BossDef CreateBossFromConfig(BossConfigEntry entry)
        {
            if (string.IsNullOrEmpty(entry.bossId))
                throw new ArgumentException("bossId 不能为空");

            BossDef boss = ScriptableObject.CreateInstance<BossDef>();
            boss.hideFlags = HideFlags.DontSave; // runtime-only, never persist to disk

            // 基本信息
            boss.bossId = entry.bossId;
            boss.displayName = entry.displayName ?? "未命名BOSS";
            boss.title = entry.title ?? "";
            boss.realm = Mathf.Clamp(entry.realm, 1, 10);

            // BOSS 类型
            if (!string.IsNullOrEmpty(entry.bossType) && BossTypeMap.TryGetValue(entry.bossType, out BossType parsedType))
                boss.bossType = parsedType;
            else
                Debug.LogWarning($"[BossConfigLoader] BOSS [{entry.bossId}] bossType 无效或为空: \"{entry.bossType}\"，使用默认值 AreaLord");

            // 基础属性（JSON 中使用 baseHP，映射到 baseMaxHP）
            boss.baseMaxHP = Mathf.Max(1f, entry.baseHP);
            boss.baseAttack = Mathf.Max(0f, entry.baseAttack);
            boss.baseDefense = Mathf.Max(0f, entry.baseDefense);
            boss.baseCritRate = Mathf.Clamp01(entry.baseCritRate);

            // 索敌范围
            boss.detectRange = Mathf.Max(0f, entry.detectRange);
            boss.aggroRange = Mathf.Max(0f, entry.aggroRange);
            boss.leashRange = Mathf.Max(0f, entry.leashRange);

            // 狂暴
            boss.enrageTimeLimit = Mathf.Max(0f, entry.enrageTimeLimit);

            // 阶段转换
            if (entry.phases != null && entry.phases.Length > 0)
            {
                boss.phases = new PhaseTransitionDef[entry.phases.Length];
                for (int i = 0; i < entry.phases.Length; i++)
                    boss.phases[i] = ConvertPhase(entry.phases[i]);
            }

            // 弱点
            if (entry.weaknesses != null && entry.weaknesses.Length > 0)
            {
                boss.weaknesses = new WeaknessDef[entry.weaknesses.Length];
                for (int i = 0; i < entry.weaknesses.Length; i++)
                    boss.weaknesses[i] = ConvertWeakness(entry.weaknesses[i]);
            }

            // 独特机制
            if (entry.mechanics != null && entry.mechanics.Length > 0)
            {
                boss.mechanics = new MechanicDef[entry.mechanics.Length];
                for (int i = 0; i < entry.mechanics.Length; i++)
                    boss.mechanics[i] = ConvertMechanic(entry.mechanics[i]);
            }

            // 攻击库
            if (entry.attacks != null && entry.attacks.Length > 0)
            {
                boss.attacks = new AttackDef[entry.attacks.Length];
                for (int i = 0; i < entry.attacks.Length; i++)
                    boss.attacks[i] = ConvertAttack(entry.attacks[i]);
            }

            // 外交
            boss.diplomacy = ConvertDiplomacy(entry.diplomacy);

            // 可绕过
            boss.stealthVulnerable = entry.stealthVulnerable;

            // 掉落
            boss.dropTable = ConvertDropTable(entry.dropTable);

            // 刷新
            boss.respawnTimeDays = Mathf.Max(1, entry.respawnTimeDays);

            // 文本
            boss.storyContext = entry.storyContext ?? "";
            boss.entranceDialogue = entry.entranceDialogue ?? "";
            boss.defeatDialogue = entry.defeatDialogue ?? "";
            boss.retreatDialogue = entry.retreatDialogue ?? "";

            return boss;
        }

        private static PhaseTransitionDef ConvertPhase(PhaseTransitionConfig c)
        {
            PhaseTransitionDef def = new PhaseTransitionDef();

            if (!string.IsNullOrEmpty(c.triggerType) && PhaseTriggerMap.TryGetValue(c.triggerType, out PhaseTriggerType parsed))
                def.triggerType = parsed;
            else
                def.triggerType = PhaseTriggerType.HP;

            def.triggerValue = c.triggerValue;
            def.newMechanics = c.newMechanics ?? Array.Empty<string>();
            def.removedMechanics = c.removedMechanics ?? Array.Empty<string>();
            def.newAttacks = c.newAttacks ?? Array.Empty<string>();
            def.dialogue = c.dialogue ?? "";
            def.visualColor = c.visualColor?.ToColor() ?? Color.white;
            def.visualEffectName = c.visualEffectName ?? "";
            def.dropCheckpoint = c.dropCheckpoint;

            return def;
        }

        private static WeaknessDef ConvertWeakness(WeaknessConfig c)
        {
            WeaknessDef def = new WeaknessDef();

            if (!string.IsNullOrEmpty(c.weaknessType) && WeaknessTypeMap.TryGetValue(c.weaknessType, out WeaknessType parsed))
                def.weaknessType = parsed;
            else
                def.weaknessType = WeaknessType.Element;

            def.displayName = c.displayName ?? "";
            def.description = c.description ?? "";
            def.damageMultiplier = Mathf.Max(1f, c.damageMultiplier);
            def.elementType = c.elementType ?? "";
            def.exposureDuration = Mathf.Max(0f, c.exposureDuration);

            return def;
        }

        private static MechanicDef ConvertMechanic(MechanicConfig c)
        {
            return new MechanicDef
            {
                mechanicName = c.mechanicName ?? "",
                description = c.description ?? "",
                counterMethod = c.counterMethod ?? ""
            };
        }

        private static AttackDef ConvertAttack(AttackConfig c)
        {
            return new AttackDef
            {
                attackName = c.attackName ?? "",
                damageMultiplier = Mathf.Max(0f, c.damageMultiplier),
                cooldown = Mathf.Max(0f, c.cooldown),
                animationTrigger = c.animationTrigger ?? "",
                effectPrefabPath = c.effectPrefabPath ?? ""
            };
        }

        private static DiplomacyDef ConvertDiplomacy(DiplomacyConfig c)
        {
            if (c.requiredItems == null)
                c.requiredItems = Array.Empty<string>();

            return new DiplomacyDef
            {
                hasDiplomacy = c.hasDiplomacy,
                conditionDescription = c.conditionDescription ?? "",
                requiredItems = c.requiredItems,
                baseSuccessRate = Mathf.Clamp01(c.baseSuccessRate),
                peaceWindowDuration = Mathf.Max(0f, c.peaceWindowDuration)
            };
        }

        private static DropTable ConvertDropTable(DropTableConfig c)
        {
            DropTable table = new DropTable
            {
                guaranteed = ConvertDropEntries(c.guaranteed),
                normal = ConvertDropEntries(c.normal),
                conditional = ConvertDropEntries(c.conditional)
            };
            return table;
        }

        private static DropEntry[] ConvertDropEntries(DropEntryConfig[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<DropEntry>();

            DropEntry[] result = new DropEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                result[i] = new DropEntry
                {
                    itemId = entries[i].itemId ?? "",
                    itemName = entries[i].itemName ?? "",
                    dropChance = Mathf.Clamp01(entries[i].dropChance),
                    minCount = Mathf.Max(1, entries[i].minCount),
                    maxCount = Mathf.Max(entries[i].minCount, entries[i].maxCount)
                };
            }
            return result;
        }
    }
}
