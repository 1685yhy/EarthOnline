using UnityEngine;

namespace EarthOnline
{
    /// <summary>
    /// 修炼调优数据加载器。
    /// 从 Resources/Data/CultivationTuning.json 加载完整境界曲线数据，
    /// 在运行时配置 CultivationManager 和 PlayerStats 的基础数值。
    ///
    /// 数据驱动而非硬编码，允许策划通过修改 JSON 直接调整平衡性。
    /// 调用 CultivationDataLoader.Apply() 在游戏启动时加载配置。
    /// </summary>
    public static class CultivationDataLoader
    {
        // ──────────── JSON 数据容器类 ────────────

        [System.Serializable]
        public class CultivationTuningData
        {
            public string version;
            public string lastUpdated;
            public RealmData[] realms;
            public BreakthroughMultipliers breakthroughMultipliers;
            public RealmUnlockThreshold[] realmUnlockThresholds;
            public GlobalTuning globalTuning;
        }

        [System.Serializable]
        public class RealmData
        {
            public int realmIndex;
            public string realmName;
            public string displayName;
            public int realmPower;
            public BaseStats baseStats;
            public LayerCurve layerCurve;
            public Breakthrough breakthrough;
            public UnlockCondition unlock;
            public Unlocks unlocks;
        }

        [System.Serializable]
        public class BaseStats
        {
            public int maxHP;
            public int attack;
            public int defense;
            public int spiritCapacity;
        }

        [System.Serializable]
        public class LayerCurve
        {
            /// <summary>该境第1层所需修为</summary>
            public int baseCultivation;
            /// <summary>该境第13层所需修为（上限）</summary>
            public int maxCultivation;
            /// <summary>每层递增倍率（指数曲线）</summary>
            public float perLayerMultiplier;
        }

        [System.Serializable]
        public class Breakthrough
        {
            /// <summary>突破到大境界下一境的基础成功率</summary>
            public float baseChance;
            /// <summary>失败时扣除当前修为的比例(0-1)</summary>
            public float failurePenaltyRate;
        }

        [System.Serializable]
        public class UnlockCondition
        {
            public int requiredCultivation;
            public int requiredLayer;
            public float nextRealmBreakthroughChance;
        }

        [System.Serializable]
        public class Unlocks
        {
            public string[] skills;
            public string[] areas;
            public string equipmentTier;
            public string[] features;
        }

        [System.Serializable]
        public class BreakthroughMultipliers
        {
            public string description;
            public float perLayerBonusAboveMax;
            public int perLayerBonusMax;
            public float perLayerBonusCap;
            public RealmFactorEntry[] realmFactor;
            public float pillBonus;
            public float formationBonus;
            public float escortBonus;
            public float soloPenalty;
            public float maxBonusRate;
            public float minSuccessRate;
        }

        [System.Serializable]
        public class RealmFactorEntry
        {
            public string sourceRealm;
            public string targetRealm;
            public float factor;
        }

        [System.Serializable]
        public class RealmUnlockThreshold
        {
            public string realm;
            public int requiredCultivation;
            public int requiredLayer;
            public string unlockMessage;
            public Unlocks unlocks;
        }

        [System.Serializable]
        public class GlobalTuning
        {
            public int maxLayerPlayer;
            public int maxLayerNPC;
            public int spiritStoneToEssenceRate;
            public int essenceToCultivationRate;
            public float baseExpToLevelMultiplier;
            public int hpPerLevel;
            public int levelBonusInterval;
            public int levelBonusGoldPerLevel;
            public float deathGoldLossRate;
            public int newbieProtectionDays;
        }

        // ──────────── 解析后暴露的运行时数据 ────────────

        /// <summary>原生 JSON 数据（保留完整引用）</summary>
        public static CultivationTuningData RawData { get; private set; }

        /// <summary>是否已成功加载</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>每境每层所需修为表 [realmIndex][layerIndex(0-based)]</summary>
        private static int[][] _layerCultivationTable;

        /// <summary>每境每层突破成功率 [realmIndex][layerIndex]</summary>
        private static float[][] _layerBreakthroughTable;

        /// <summary>每境突破失败惩罚率 [realmIndex]</summary>
        private static float[] _realmFailurePenalty;

        /// <summary>每境基础属性 [realmIndex] → BaseStats</summary>
        private static BaseStats[] _realmBaseStats;

        // ──────────── 公共查询 API ────────────

        /// <summary>获取指定境界指定层的所需修为</summary>
        public static int GetCultivationForLayer(int realmIndex, int layer)
        {
            if (!IsLoaded || _layerCultivationTable == null) return 100;
            if (realmIndex < 0 || realmIndex >= _layerCultivationTable.Length) return 100;
            var layers = _layerCultivationTable[realmIndex];
            if (layer < 0 || layer >= layers.Length) return layers[^1];
            return layers[layer];
        }

        /// <summary>获取指定境界第 layerIndex 层（0-based）的突破成功率</summary>
        public static float GetBreakthroughChance(int realmIndex, int layerIndex)
        {
            if (!IsLoaded || _layerBreakthroughTable == null) return 0.7f;
            if (realmIndex < 0 || realmIndex >= _layerBreakthroughTable.Length) return 0.7f;
            var layers = _layerBreakthroughTable[realmIndex];
            if (layerIndex < 0 || layerIndex >= layers.Length) return layers[^1];
            return layers[layerIndex];
        }

        /// <summary>获取指定境界突破失败惩罚率(0-1)</summary>
        public static float GetFailurePenalty(int realmIndex)
        {
            if (!IsLoaded || _realmFailurePenalty == null) return 0.15f;
            if (realmIndex < 0 || realmIndex >= _realmFailurePenalty.Length) return 0.15f;
            return _realmFailurePenalty[realmIndex];
        }

        /// <summary>获取指定境界的基础属性</summary>
        public static BaseStats GetBaseStats(int realmIndex)
        {
            if (!IsLoaded || _realmBaseStats == null)
                return new BaseStats { maxHP = 100, attack = 5, defense = 2, spiritCapacity = 50 };
            if (realmIndex < 0 || realmIndex >= _realmBaseStats.Length)
                return _realmBaseStats[^1];
            return _realmBaseStats[realmIndex];
        }

        /// <summary>获取境界名称（中文）</summary>
        public static string GetRealmDisplayName(int realmIndex)
        {
            if (RawData?.realms == null || realmIndex < 0 || realmIndex >= RawData.realms.Length)
                return "未知";
            return RawData.realms[realmIndex].displayName;
        }

        /// <summary>获取境界战力值</summary>
        public static int GetRealmPower(int realmIndex)
        {
            if (RawData?.realms == null || realmIndex < 0 || realmIndex >= RawData.realms.Length)
                return 1;
            return RawData.realms[realmIndex].realmPower;
        }

        // ──────────── 加载与应用 ────────────

        /// <summary>
        /// 从 Resources 加载 CultivationTuning.json 并应用到运行时系统。
        /// 建议在 GameManager Awake 或 SceneBootstrap 中调用一次。
        /// </summary>
        public static void Load()
        {
            var textAsset = Resources.Load<TextAsset>("Data/CultivationTuning");
            if (textAsset == null)
            {
                Debug.LogError("[CultivationDataLoader] 找不到 Resources/Data/CultivationTuning.json！");
                return;
            }

            var data = JsonUtility.FromJson<CultivationTuningData>(textAsset.text);
            if (data == null || data.realms == null || data.realms.Length == 0)
            {
                Debug.LogError("[CultivationDataLoader] JSON 解析失败或数据为空！");
                return;
            }

            RawData = data;
            BuildTables(data);
            IsLoaded = true;

            Debug.Log($"[CultivationDataLoader] 加载完成：{data.realms.Length} 境界，版本 {data.version}");
        }

        /// <summary>
        /// 一次性加载+应用。等同于 Load()。
        /// </summary>
        public static void Apply()
        {
            Load();
        }

        // ──────────── 内部表构建 ────────────

        private static void BuildTables(CultivationTuningData data)
        {
            int realmCount = data.realms.Length;
            const int maxLayers = 13;
            int npcMaxLayer = data.globalTuning.maxLayerNPC;   // 9 (NPC 每境最多9层)
            int playerMaxLayer = data.globalTuning.maxLayerPlayer; // 13

            _layerCultivationTable = new int[realmCount][];
            _layerBreakthroughTable = new float[realmCount][];
            _realmFailurePenalty = new float[realmCount];
            _realmBaseStats = new BaseStats[realmCount];

            for (int r = 1; r < realmCount; r++) // 跳过 Mortal（索引0）
            {
                var realm = data.realms[r];
                var curve = realm.layerCurve;
                var bm = data.breakthroughMultipliers;

                _realmFailurePenalty[r] = realm.breakthrough.failurePenaltyRate;
                _realmBaseStats[r] = realm.baseStats;

                _layerCultivationTable[r] = new int[maxLayers];
                _layerBreakthroughTable[r] = new float[maxLayers];

                for (int layer = 0; layer < maxLayers; layer++)
                {
                    // 修为曲线：指数增长
                    // Layer N = baseCultivation * (perLayerMultiplier)^(N-1)
                    float cultivation = curve.baseCultivation *
                        Mathf.Pow(curve.perLayerMultiplier, layer);
                    _layerCultivationTable[r][layer] = Mathf.RoundToInt(cultivation);

                    // 突破成功率：
                    // - 非满层（layer < maxLayers-1）→ 2%~5% 提前突破小概率（机缘/顿悟）
                    // - 满层（layer == maxLayers-1）→ 基础成功率 + 超额层数补正
                    //
                    // 超额层数补正: 玩家可修炼到 13 层（NPC 仅 9 层），
                    // 每超出 NPC 上限 1 层 +perLayerBonusAboveMax 成功率。
                    // 原代码: successRate = 0.70f + (CurrentLayer - 9) * 0.03f
                    float chance;
                    if (layer < maxLayers - 1)
                    {
                        // 非满层——给小概率提前突破机会（"厚积薄发"）
                        chance = 0.03f;
                    }
                    else
                    {
                        // 满层——大境界突破判定
                        int extraLayers = playerMaxLayer - npcMaxLayer; // 13-9=4
                        float layerBonus = Mathf.Min(
                            extraLayers * bm.perLayerBonusAboveMax,
                            bm.perLayerBonusCap);
                        chance = realm.breakthrough.baseChance + layerBonus;
                    }

                    _layerBreakthroughTable[r][layer] = Mathf.Clamp(
                        chance, bm.minSuccessRate, bm.maxBonusRate);
                }
            }

            // Mortal (r=0) 特殊处理——凡人没有修炼层
            _layerCultivationTable[0] = new int[maxLayers];
            _layerBreakthroughTable[0] = new float[maxLayers];
            _realmFailurePenalty[0] = 0f;
            _realmBaseStats[0] = data.realms[0].baseStats;
        }

        // ──────────── 调试输出 ────────────

        /// <summary>打印完整境界曲线到控制台（用于验证）</summary>
        public static void DebugPrint()
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("[CultivationDataLoader] 未加载，无法打印。");
                return;
            }

            var data = RawData;
            System.Text.StringBuilder sb = new();
            sb.AppendLine("═══════════ 修炼境界曲线 ═══════════");

            for (int r = 1; r < data.realms.Length; r++)
            {
                var realm = data.realms[r];
                sb.AppendLine($"\n── {realm.displayName}（战力 {realm.realmPower}）──");
                sb.AppendLine($"  基础属性: HP={realm.baseStats.maxHP} ATK={realm.baseStats.attack} " +
                    $"DEF={realm.baseStats.defense} 灵力={realm.baseStats.spiritCapacity}");
                sb.AppendLine($"  突破基础成功率: {realm.breakthrough.baseChance * 100:F0}%  " +
                    $"失败惩罚: {realm.breakthrough.failurePenaltyRate * 100:F0}%");
                sb.AppendLine($"  解锁: {string.Join(", ", realm.unlocks.skills)}");
                sb.AppendLine($"  装备阶位: {realm.unlocks.equipmentTier}");
                sb.AppendLine($"  区域: {string.Join(", ", realm.unlocks.areas)}");

                sb.AppendLine("  层数 | 所需修为 | 突破成功率");
                for (int l = 0; l < 13; l++)
                {
                    sb.AppendLine($"  L{l + 1,2} | {GetCultivationForLayer(r, l),7}  | " +
                        $"{GetBreakthroughChance(r, l) * 100:F1}%");
                }
            }

            sb.AppendLine("\n── 突破修正系数 ──");
            var bm = data.breakthroughMultipliers;
            sb.AppendLine($"  丹药加成: +{bm.pillBonus * 100:F0}%");
            sb.AppendLine($"  阵法加成: +{bm.formationBonus * 100:F0}%");
            sb.AppendLine($"  护法加成: +{bm.escortBonus * 100:F0}%");
            sb.AppendLine($"  散修惩罚: {bm.soloPenalty * 100:F0}%");
            sb.AppendLine($"  层数超额加成: 每层+{bm.perLayerBonusAboveMax * 100:F0}% (上限+{bm.perLayerBonusCap * 100:F0}%)");

            sb.AppendLine("\n═══════════════════════════════════");
            Debug.Log(sb.ToString());
        }
    }
}
