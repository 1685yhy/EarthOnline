using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Combat
{
    /// <summary>
    /// BOSS 类型枚举
    /// </summary>
    public enum BossType
    {
        AreaLord,       // 区域领主
        Dungeon,        // 副本BOSS
        FieldRoaming,   // 野外游荡BOSS
        WorldBoss,      // 世界BOSS
        Hidden,         // 隐藏BOSS
        Quest           // 任务BOSS
    }

    /// <summary>
    /// 阶段转换触发类型
    /// </summary>
    public enum PhaseTriggerType
    {
        HP,         // 血量阈值触发
        Time,       // 时间触发（狂暴）
        Behavior,   // 行为触发
        Environment // 环境触发
    }

    /// <summary>
    /// 弱点类型
    /// </summary>
    public enum WeaknessType
    {
        Element,     // 属性相克
        Timing,      // 时机弱点
        PartBreak,   // 部位破坏
        ItemCounter, // 道具克制
        Environment, // 环境利用
        Fear         // 恐惧因素
    }

    // ─── 子数据结构 ──────────────────────────────────────────────────────

    /// <summary>阶段转换定义</summary>
    [Serializable]
    public struct PhaseTransitionDef
    {
        public PhaseTriggerType triggerType;
        public float triggerValue;                // HP阈值(0-1) 或 时间秒数
        public string[] newMechanics;             // 新增机制名称
        public string[] removedMechanics;         // 移除机制名称
        public string[] newAttacks;               // 新增攻击名称
        public string dialogue;                   // 阶段台词
        public Color visualColor;                 // 阶段主色调
        public string visualEffectName;           // 视觉特效名
        public bool dropCheckpoint;               // 是否触发阶段性掉落
    }

    /// <summary>弱点定义</summary>
    [Serializable]
    public struct WeaknessDef
    {
        public WeaknessType weaknessType;
        public string displayName;
        public string description;
        public float damageMultiplier;            // 伤害倍率
        public string elementType;                // 克制属性（属性相克时）
        public float exposureDuration;            // 暴露持续时间（秒）
    }

    /// <summary>独特机制定义</summary>
    [Serializable]
    public struct MechanicDef
    {
        public string mechanicName;
        public string description;
        public string counterMethod;              // 应对方式描述
    }

    /// <summary>基础攻击定义</summary>
    [Serializable]
    public struct AttackDef
    {
        public string attackName;
        public float damageMultiplier;            // 攻击倍率（基于BossAttack）
        public float cooldown;                    // 冷却时间
        public string animationTrigger;
        public string effectPrefabPath;
    }

    /// <summary>外交选项定义</summary>
    [Serializable]
    public struct DiplomacyDef
    {
        public bool hasDiplomacy;
        [TextArea(2, 4)] public string conditionDescription;
        public string[] requiredItems;
        public float baseSuccessRate;
        public float peaceWindowDuration;         // 和平窗口期（秒）
    }

    /// <summary>掉落条目</summary>
    [Serializable]
    public struct DropEntry
    {
        public string itemId;
        public string itemName;
        [Range(0f, 1f)] public float dropChance;
        public int minCount;
        public int maxCount;
    }

    /// <summary>掉落表</summary>
    [Serializable]
    public struct DropTable
    {
        public DropEntry[] guaranteed;
        public DropEntry[] normal;
        public DropEntry[] conditional;
    }

    // ─── BOSS定义 ScriptableObject ──────────────────────────────────────

    /// <summary>
    /// BOSS 数据结构 ScriptableObject。
    /// 所有 BOSS 属性在此定义，运行时通过 BossAI 读取并缩放。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBossDef", menuName = "地球Online/BOSS定义", order = 100)]
    public class BossDef : ScriptableObject
    {
        [Header("-- 基本信息 --")]
        public string bossId;
        public string displayName;
        public string title;                      // 称号
        [Range(1, 10)] public int realm = 1;     // 境界等级（1=练气...7=大成）

        public BossType bossType = BossType.AreaLord;

        [Header("-- 基础属性（练气期1人基准） --")]
        public float baseMaxHP = 1000f;
        public float baseAttack = 30f;
        public float baseDefense = 15f;
        [Range(0f, 1f)] public float baseCritRate = 0.05f;

        [Header("-- 索敌与范围 --")]
        public float detectRange = 30f;
        public float aggroRange = 20f;
        public float leashRange = 50f;

        [Header("-- 阶段转换 --")]
        public PhaseTransitionDef[] phases = new PhaseTransitionDef[]
        {
            new PhaseTransitionDef
            {
                triggerType = PhaseTriggerType.HP,
                triggerValue = 0.70f,
                dialogue = "哼，有点意思……看来需要认真一些了。",
                visualColor = new Color(1f, 0.5f, 0f),
                newAttacks = new[] { "阶段二·横斩", "阶段二·灵气爆发" },
                newMechanics = new[] { "灵气护盾" }
            },
            new PhaseTransitionDef
            {
                triggerType = PhaseTriggerType.HP,
                triggerValue = 0.35f,
                dialogue = "你已经激怒我了！感受真正的力量吧！",
                visualColor = new Color(1f, 0f, 0f),
                newAttacks = new[] { "阶段三·裂地斩", "阶段三·天雷引" },
                newMechanics = new[] { "召唤护卫" }
            }
        };

        [Header("-- 狂暴 --")]
        public float enrageTimeLimit = 300f;      // 秒

        [Header("-- 弱点 --")]
        public WeaknessDef[] weaknesses;

        [Header("-- 独特机制 --")]
        public MechanicDef[] mechanics;

        [Header("-- 攻击库 --")]
        public AttackDef[] attacks;

        [Header("-- 外交 --")]
        public DiplomacyDef diplomacy;

        [Header("-- 可绕过 --")]
        public bool stealthVulnerable = true;

        [Header("-- 掉落 --")]
        public DropTable dropTable;

        [Header("-- 刷新 --")]
        public int respawnTimeDays = 3;

        [Header("-- 背景 --")]
        [TextArea(3, 6)] public string storyContext;

        [Header("-- 出场设定 --")]
        public string entranceDialogue = "你终于来了……我等这一刻很久了。";
        public string defeatDialogue = "不……可……能……";
        public string retreatDialogue = "逃吧……但下次不会这么幸运了。";

        // ─── 运行时属性（非序列化，运行时填充） ────────────────────────

        [NonSerialized] public float ScaledMaxHP;
        [NonSerialized] public float ScaledAttack;
        [NonSerialized] public float ScaledDefense;

        // ─── 工具方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据境界等级计算境界系数
        /// 练气 = "1.0", 筑基 = "1.5", 金丹 = "2.5", 元婴 = "4.0", 化神 = "6.5", 渡劫 = "10.0", 大成=16.0
        /// </summary>
        public static float GetRealmMultiplier(int realmLevel)
        {
            float[] multipliers = { 0f, 1.0f, 1.5f, 2.5f, 4.0f, 6.5f, 10.0f, 16.0f };
            if (realmLevel < 1) return multipliers[1];
            if (realmLevel >= multipliers.Length) return multipliers[multipliers.Length - 1];
            return multipliers[realmLevel];
        }

        /// <summary>
        /// 根据组队人数计算组队系数
        /// 1人 = "1.0", 2人 = "1.4", 3人 = "1.8", 4人 = "2.2", 5人=2.6
        /// </summary>
        public static float GetPartySizeMultiplier(int partySize)
        {
            return 1.0f + (partySize - 1) * 0.4f;
        }

        /// <summary>
        /// 计算境界压制带来的伤害倍率
        /// 玩家境界 > BOSS境界：1.0 + 差 × 0.15
        /// 玩家境界 = BOSS境界：1.0
        /// 玩家境界 < BOSS境界：1.0 - 差 × 0.25（最低0.1）
        /// </summary>
        public static float CalculateRealmSuppression(int playerRealm, int bossRealm)
        {
            int diff = playerRealm - bossRealm;
            if (diff > 0)
                return Mathf.Min(1.0f + diff * 0.15f, 10.0f);  // 上限10倍
            if (diff < 0)
                return Mathf.Max(1.0f - (-diff) * 0.25f, 0.1f); // 最低10%
            return 1.0f;
        }

        /// <summary>
        /// 计算缩放后的BOSS属性
        /// </summary>
        public void CalculateScaledStats(int partySize)
        {
            float realmMult = GetRealmMultiplier(realm);
            float partyMult = GetPartySizeMultiplier(partySize);

            ScaledMaxHP = baseMaxHP * realmMult * partyMult;
            ScaledAttack = baseAttack * realmMult;
            ScaledDefense = baseDefense * realmMult;
        }

        /// <summary>
        /// 获取当前阶段索引（基于HP百分比）
        /// </summary>
        public int GetPhaseIndexForHP(float hpPercent)
        {
            int phase = 0;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].triggerType == PhaseTriggerType.HP && hpPercent <= phases[i].triggerValue)
                    phase = i + 1;
            }
            return phase;
        }
    }
}
