using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Combat
{
    #region Enums

    /// <summary>四种BOSS应对路径 (003-PATH-01).</summary>
    public enum BossPathType
    {
        Combat,
        Diplomacy,
        Stealth,
        Reinforcement
    }

    /// <summary>外交谈判结果.</summary>
    public enum DiplomacyResult
    {
        Pending,
        Accepted,
        Rejected,
        Betrayed,
        Expired
    }

    /// <summary>记仇等级 (003-GRUDGE-01). 等级越高后续交互越困难.</summary>
    public enum GrudgeLevel
    {
        None,       // 0 — 正常
        Annoyed,    // 1 — 不悦：外交-10%
        Angry,      // 2 — 愤怒：外交-20%，索敌+10%
        Furious,    // 3 — 暴怒：外交-30%，伤害+15%
        Vengeful    // 4 — 记仇：不可外交，伤害+30%，永久索敌
    }

    /// <summary>援军类型 (003-REIN-01).</summary>
    public enum ReinforcementType
    {
        NPC,           // 附近友好NPC
        SectDisciple,  // 消耗门派令
        Talisman,      // 消耗召唤符
        TempParty      // 临时组队（附近玩家）
    }

    #endregion

    #region Runtime Data

    /// <summary>单个BOSS的记仇数据，用于跨战斗持久化 (003-GRUDGE-02).</summary>
    [Serializable]
    public class BossGrudgeData
    {
        public string bossId;
        public string bossName;
        public GrudgeLevel level = GrudgeLevel.None;
        public int betrayalCount;                  // 背叛次数
        public float lastEncounterTime;            // 上次遭遇时间戳
        public bool hasBeenBetrayed;               // 是否曾被背叛过

        /// <summary>记仇等级倍率，影响外交成功率.</summary>
        public float DiplomacyPenalty => level switch
        {
            GrudgeLevel.None     => 1.0f,
            GrudgeLevel.Annoyed  => 0.9f,
            GrudgeLevel.Angry    => 0.8f,
            GrudgeLevel.Furious  => 0.7f,
            GrudgeLevel.Vengeful => 0.0f,           // 不可外交
            _ => 1.0f
        };

        /// <summary>记仇等级对隐身难度的加成（降低潜行成功率）.</summary>
        public float StealthPenalty => level switch
        {
            GrudgeLevel.None     => 0.00f,
            GrudgeLevel.Annoyed  => 0.05f,
            GrudgeLevel.Angry    => 0.10f,
            GrudgeLevel.Furious  => 0.15f,
            GrudgeLevel.Vengeful => 0.20f,
            _ => 0.00f
        };

        /// <summary>记仇等级对BOSS伤害的加成.</summary>
        public float DamageBonus => level switch
        {
            GrudgeLevel.None     => 1.0f,
            GrudgeLevel.Annoyed  => 1.0f,
            GrudgeLevel.Angry    => 1.10f,
            GrudgeLevel.Furious  => 1.15f,
            GrudgeLevel.Vengeful => 1.30f,
            _ => 1.0f
        };

        /// <summary>记仇等级对BOSS索敌范围的加成.</summary>
        public float AggroRangeMultiplier => level switch
        {
            GrudgeLevel.None     => 1.0f,
            GrudgeLevel.Annoyed  => 1.0f,
            GrudgeLevel.Angry    => 1.1f,
            GrudgeLevel.Furious  => 1.15f,
            GrudgeLevel.Vengeful => 2.0f,           // 见面就开打
            _ => 1.0f
        };

        /// <summary>是否允许外交.</summary>
        public bool CanDiplomacy => level < GrudgeLevel.Vengeful;

        /// <summary>BOSS好感度（0-100，用于UI显示）.</summary>
        public int Favorability
        {
            get
            {
                int baseFavor = 50;  // 默认中立
                int levelPenalty = (int)level * 20;
                int betrayPenalty = betrayalCount * 15;
                return Mathf.Clamp(baseFavor - levelPenalty - betrayPenalty, 0, 100);
            }
        }

        public BossGrudgeData(string id, string name)
        {
            bossId = id;
            bossName = name;
        }
    }

    #endregion

    #region Grudge Manager (Static)

    /// <summary>
    /// 记仇管理器——跨战斗持久化BOSS记仇状态。
    /// 所有BOSS的记仇数据在此统一管理，按bossId索引。
    /// 生产环境中需接入存档系统进行序列化。
    /// </summary>
    public static class GrudgeManager
    {
        private static readonly Dictionary<string, BossGrudgeData> _grudgeMap =
            new Dictionary<string, BossGrudgeData>();

        /// <summary>获取BOSS记仇数据，不存在则创建.</summary>
        public static BossGrudgeData GetOrCreate(string bossId, string bossName)
        {
            if (string.IsNullOrEmpty(bossId))
            {
                Debug.LogWarning("[GrudgeManager] bossId is null/empty, returning default.");
                return new BossGrudgeData("unknown", bossName ?? "BOSS");
            }

            if (!_grudgeMap.TryGetValue(bossId, out var data))
            {
                data = new BossGrudgeData(bossId, bossName);
                _grudgeMap[bossId] = data;
            }
            else if (data.bossName != bossName && !string.IsNullOrEmpty(bossName))
            {
                data.bossName = bossName;
            }

            return data;
        }

        /// <summary>提升记仇等级.</summary>
        public static void IncreaseGrudge(string bossId, int amount, string reason)
        {
            if (string.IsNullOrEmpty(bossId)) return;

            if (!_grudgeMap.TryGetValue(bossId, out var data))
            {
                Debug.LogWarning($"[GrudgeManager] No grudge data for '{bossId}'. Cannot increase.");
                return;
            }

            GrudgeLevel oldLevel = data.level;
            int newLevelVal = Mathf.Min((int)data.level + amount, (int)GrudgeLevel.Vengeful);
            data.level = (GrudgeLevel)newLevelVal;

            EventBus.Publish(new BossGrudgeUpdatedEvent
            {
                BossId = bossId,
                BossName = data.bossName,
                OldLevel = (int)oldLevel,
                NewLevel = (int)data.level,
                Reason = reason
            });

            Debug.Log($"[GrudgeManager] {data.bossName} 记仇等级: {oldLevel} -> {data.level} ({reason})");
        }

        /// <summary>降低记仇等级（随时间、送礼等）.</summary>
        public static void DecreaseGrudge(string bossId, int amount, string reason)
        {
            if (string.IsNullOrEmpty(bossId)) return;
            if (!_grudgeMap.TryGetValue(bossId, out var data)) return;

            GrudgeLevel oldLevel = data.level;
            int newLevelVal = Mathf.Max((int)data.level - amount, 0);
            data.level = (GrudgeLevel)newLevelVal;

            EventBus.Publish(new BossGrudgeUpdatedEvent
            {
                BossId = bossId,
                BossName = data.bossName,
                OldLevel = (int)oldLevel,
                NewLevel = (int)data.level,
                Reason = reason
            });
        }

        /// <summary>标记玩家背叛.</summary>
        public static void RecordBetrayal(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return;
            if (!_grudgeMap.TryGetValue(bossId, out var data)) return;

            data.betrayalCount++;
            data.hasBeenBetrayed = true;
            data.lastEncounterTime = Time.time;

            // 背叛直接拉满记仇 (003-BETRAY-01)
            IncreaseGrudge(bossId, 4, "战斗中背叛和约，记仇+4");
        }

        /// <summary>重置记仇数据.</summary>
        public static void ResetGrudge(string bossId)
        {
            if (!string.IsNullOrEmpty(bossId) && _grudgeMap.ContainsKey(bossId))
            {
                _grudgeMap.Remove(bossId);
            }
        }

        /// <summary>清除所有记仇数据（调试/新游戏用）.</summary>
        public static void ClearAll()
        {
            _grudgeMap.Clear();
        }

        /// <summary>检查BOSS是否记仇到不可外交.</summary>
        public static bool CanDiplomacy(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return true;
            return !_grudgeMap.TryGetValue(bossId, out var data) || data.CanDiplomacy;
        }

        /// <summary>获取BOSS记仇等级的伤害加成.</summary>
        public static float GetDamageBonus(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return 1.0f;
            return _grudgeMap.TryGetValue(bossId, out var data) ? data.DamageBonus : 1.0f;
        }

        /// <summary>获取BOSS记仇等级的索敌加成.</summary>
        public static float GetAggroRangeMultiplier(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return 1.0f;
            return _grudgeMap.TryGetValue(bossId, out var data) ? data.AggroRangeMultiplier : 1.0f;
        }

        /// <summary>获取BOSS当前好感度.</summary>
        public static int GetFavorability(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return 50;
            return _grudgeMap.TryGetValue(bossId, out var data) ? data.Favorability : 50;
        }

        /// <summary>调试：打印所有记仇数据.</summary>
        public static string DebugDump()
        {
            if (_grudgeMap.Count == 0) return "[GrudgeManager] 无记仇数据。";

            string result = $"=== 记仇管理器 ({_grudgeMap.Count} 条) ===\n";
            foreach (var kvp in _grudgeMap)
            {
                var d = kvp.Value;
                result += $"[{d.bossId}] {d.bossName}: 等级={d.level} 好感={d.Favorability} 背叛={d.betrayalCount}次\n";
            }
            return result;
        }
    }

    #endregion

    /// <summary>
    /// BOSS外交/潜行/援军系统 (Story 003)。
    ///
    /// 四种应对路径 (003-PATH-01~09):
    ///   Combat     — 正面战斗，完整掉落+修为+声望 (003-PATH-01)
    ///   Diplomacy  — 谈判，接受条件→和平通过；反悔→狂暴+记仇+4 (003-PATH-02~03)
    ///   Stealth    — 潜行绕过，成功率60%+技能修正-BOSS感知 (003-PATH-04~05)
    ///   Reinforcement — 援军NPC/弟子/符咒/组队 (003-PATH-06)
    ///
    /// 奖励梯度: Combat > Diplomacy > Reinforcement > Stealth (003-PATH-07)
    ///
    /// 通过 EventBus 与 UI / CombatSystem / 掉落系统 解耦通信。
    /// </summary>
    public class BossDiplomacy : MonoBehaviour
    {
        #region Constants

        // ─── 潜行公式 (003-STEALTH-01) ──────────────────────────────
        // StealthSuccess = 0.6 + Level×0.01 + EquipBonus - BossPerception×0.02

        private const float STEALTH_BASE_RATE         = 0.60f;
        private const float STEALTH_LEVEL_FACTOR      = 0.01f;
        private const float STEALTH_PERCEPTION_FACTOR = 0.02f;
        private const float STEALTH_FAVOR_PENALTY     = -30f;

        // ─── 外交 (003-DIPLO-01) ─────────────────────────────────────
        private const float PEACE_COLLIDER_REENABLE_DELAY = 2f;  // 和平破裂后重新激活碰撞体延迟

        // ─── 奖励梯度倍率 (003-REWARD-01) ───────────────────────────
        private static readonly RewardMultiplier REWARD_COMBAT = new RewardMultiplier
            { drop = 1.00f, cultivation = 1.00f, reputation = 1.00f, title = true };

        private static readonly RewardMultiplier REWARD_DIPLOMACY = new RewardMultiplier
            { drop = 0.55f, cultivation = 0.45f, reputation = 0.70f, title = false };

        private static readonly RewardMultiplier REWARD_REINFORCEMENT = new RewardMultiplier
            { drop = 0.40f, cultivation = 0.30f, reputation = 0.50f, title = false };

        private static readonly RewardMultiplier REWARD_STEALTH = new RewardMultiplier
            { drop = 0.15f, cultivation = 0.10f, reputation = 0.00f, title = false };

        #endregion

        #region Inspector Config

        [Header("-- BOSS 引用 --")]
        [Tooltip("关联的 BossAI 组件，为空则自动查找。")]
        public BossAI bossAI;

        [Tooltip("关联的 BossDef，为空则从 BossAI 读取。")]
        public BossDef bossDef;

        [Header("-- 外交参数 --")]
        [Tooltip("玩家每级对潜行成功率的加成。")]
        public float stealthLevelFactor = STEALTH_LEVEL_FACTOR;
        [Tooltip("BOSS每点感知对潜行的扣减系数。")]
        public float stealthPerceptionFactor = STEALTH_PERCEPTION_FACTOR;
        [Tooltip("BOSS基础感知值（每境界等级叠加）。")]
        public float bossBasePerceptionPerRealm = 5f;
        [Tooltip("潜行失败后BOSS好感度减少。")]
        public float stealthFailFavorPenalty = STEALTH_FAVOR_PENALTY;

        [Header("-- 援军(003-REIN-02): 消耗道具ID --")]
        public string sectTokenItemId = "item_sect_token";
        public string talismanItemId = "item_summon_talisman";
        public string tempPartyItemId = "item_temp_party_scroll";

        [Header("-- 调试 --")]
        public bool enableDebugLogs = true;

        #endregion

        #region Private State

        // 当前路径运行时状态
        private BossPathType _currentPath = BossPathType.Combat;
        private DiplomacyResult _diplomacyResult = DiplomacyResult.Pending;
        private bool _isPeaceActive;
        private float _peaceTimer;
        private float _peaceDuration;

        // 潜行相关
        private bool _stealthAttempted;

        // 组件引用
        private Collider _bossCollider;
        private Renderer _bossRenderer;
        private Color _originalBossColor;

        // 初始化标志
        private bool _initialized;

        // BOSS名缓存
        private string _bossNameCache = "BOSS";

        // 事件订阅状态
        private bool _eventsSubscribed;

        #endregion

        #region Public Properties

        /// <summary>玩家当前选择的路径.</summary>
        public BossPathType CurrentPath => _currentPath;

        /// <summary>外交谈判结果.</summary>
        public DiplomacyResult LastDiplomacyResult => _diplomacyResult;

        /// <summary>是否处于和平窗口期.</summary>
        public bool IsPeaceActive => _isPeaceActive;

        /// <summary>和平窗口剩余时间.</summary>
        public float PeaceTimeRemaining => _isPeaceActive ? Mathf.Max(0f, _peaceTimer) : 0f;

        /// <summary>和平窗口总时长.</summary>
        public float PeaceDuration => _peaceDuration;

        /// <summary>是否已完成初始化.</summary>
        public bool IsReady => _initialized;

        /// <summary>当前BOSS的记仇数据.</summary>
        public BossGrudgeData GrudgeData =>
            bossDef != null ? GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache) : null;

        /// <summary>当前BOSS好感度.</summary>
        public int Favorability =>
            bossDef != null ? GrudgeManager.GetFavorability(bossDef.bossId) : 50;

        /// <summary>外交是否可用（BOSS支持外交且记仇未满）. </summary>
        public bool CanDiplomacy =>
            bossDef != null
            && bossDef.diplomacy.hasDiplomacy
            && GrudgeManager.CanDiplomacy(bossDef.bossId);

        /// <summary>潜行是否可用.</summary>
        public bool CanStealth =>
            bossDef != null && bossDef.stealthVulnerable;

        /// <summary>援军是否可用（有道具库存）. </summary>
        public bool CanCallReinforcements =>
            HasItem(sectTokenItemId) || HasItem(talismanItemId) || HasItem(tempPartyItemId);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 自动获取引用
            if (bossAI == null)
                bossAI = GetComponent<BossAI>();

            if (bossAI != null && bossDef == null)
                bossDef = bossAI.bossDef;

            if (bossDef == null)
            {
                Debug.LogError("[BossDiplomacy] BossDef 未配置，系统禁用。");
                enabled = false;
                return;
            }

            _bossCollider = GetComponent<Collider>();
            _bossRenderer = GetComponentInChildren<Renderer>();
            _bossNameCache = bossDef.displayName;

            _initialized = true;
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            if (!_initialized) return;

            // 同步记仇数据
            var grudge = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache);
            grudge.lastEncounterTime = Time.time;

            if (enableDebugLogs)
                DebugLog($"初始化完成。记仇等级: {grudge.level} 好感: {grudge.Favorability}");
        }

        private void Update()
        {
            if (!_initialized || !_isPeaceActive) return;

            // 和平倒计时
            _peaceTimer -= Time.deltaTime;
            if (_peaceTimer <= 0f)
            {
                ExpirePeace();
            }
        }

        #endregion

        #region EventBus Subscription

        private void SubscribeEvents()
        {
            if (_eventsSubscribed) return;
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed) return;
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            _eventsSubscribed = false;
        }

        #endregion

        #region Public API — 路径选择

        /// <summary>
        /// 玩家选择正面战斗 (003-PATH-01)。
        /// 恢复BOSS AI正常运行，进入战斗流程。
        /// </summary>
        public void ChooseCombat()
        {
            if (!_initialized || !bossAI.IsAlive) return;

            _currentPath = BossPathType.Combat;
            _diplomacyResult = DiplomacyResult.Pending;
            EnsureCombatReady();

            EventBus.Publish(new BossPathSelectedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                PathType = "Combat",
                PlayerId = ""   // 由 PlayerSystem 填充
            });

            DebugLog($"玩家选择【正面战斗】路径。");
        }

        /// <summary>
        /// 玩家选择外交谈判 (003-PATH-02)。
        /// 检查BOSS是否可谈判，返回谈判条件。
        /// </summary>
        /// <returns>是否可发起谈判.</returns>
        public bool ChooseDiplomacy()
        {
            if (!_initialized || !bossAI.IsAlive) return false;
            if (!CanDiplomacy)
            {
                DebugLog($"外交不可用：hasDiplomacy={bossDef.diplomacy.hasDiplomacy}, grudge blocks={!GrudgeManager.CanDiplomacy(bossDef.bossId)}");
                return false;
            }

            _currentPath = BossPathType.Diplomacy;
            _diplomacyResult = DiplomacyResult.Pending;

            // 生成谈判条件 (003-DIPLO-02)
            float effectiveRate = CalculateEffectiveDiplomacyRate();

            EventBus.Publish(new BossDiplomacyOfferEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                ConditionDescription = bossDef.diplomacy.conditionDescription,
                RequiredItems = bossDef.diplomacy.requiredItems ?? Array.Empty<string>(),
                BaseSuccessRate = bossDef.diplomacy.baseSuccessRate,
                EffectiveSuccessRate = effectiveRate,
                PeaceWindowDuration = bossDef.diplomacy.peaceWindowDuration
            });

            EventBus.Publish(new BossPathSelectedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                PathType = "Diplomacy",
                PlayerId = ""
            });

            DebugLog($"玩家选择【外交谈判】。条件: {bossDef.diplomacy.conditionDescription} 成功率: {effectiveRate:P1}");
            return true;
        }

        /// <summary>
        /// 玩家选择潜行绕过 (003-PATH-04)。
        /// 计算潜行成功率并执行判定。
        /// </summary>
        /// <param name="playerLevel">玩家等级.</param>
        /// <param name="equipmentBonus">潜行装备加成 (0~1).</param>
        /// <returns>潜行结果: true=成功.</returns>
        public bool ChooseStealth(int playerLevel = 1, float equipmentBonus = 0f)
        {
            if (!_initialized || !bossAI.IsAlive) return false;
            if (!CanStealth)
            {
                DebugLog($"潜行不可用：stealthVulnerable={bossDef.stealthVulnerable}");
                return false;
            }

            _currentPath = BossPathType.Stealth;
            _stealthAttempted = true;

            float successRate = CalculateStealthSuccessRate(playerLevel, equipmentBonus);
            bool success = UnityEngine.Random.value < successRate;

            EventBus.Publish(new BossStealthEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                Success = success,
                SuccessRate = successRate,
                CombatFavorChange = success ? 0f : stealthFailFavorPenalty
            });

            EventBus.Publish(new BossPathSelectedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                PathType = "Stealth",
                PlayerId = ""
            });

            if (success)
            {
                DebugLog($"潜行成功！(成功率: {successRate:P1}) 绕过BOSS。");
                // 玩家绕过BOSS，给予潜行奖励
                DistributeRewards(BossPathType.Stealth);
            }
            else
            {
                DebugLog($"潜行失败！(成功率: {successRate:P1}) 进入战斗，好感-30。");
                // 潜行失败：进入战斗 + BOSS好感-30 (003-STEALTH-02)
                GrudgeManager.IncreaseGrudge(bossDef.bossId, 1, "潜行失败被发现");
                // favor penalty handled by consuming system via BossStealthEvent
                EnsureCombatReady();
            }

            return success;
        }

        /// <summary>
        /// 玩家选择召唤援军 (003-PATH-06)。
        /// </summary>
        /// <param name="type">援军类型.</param>
        /// <returns>是否成功召唤.</returns>
        public bool ChooseReinforcements(ReinforcementType type)
        {
            if (!_initialized || !bossAI.IsAlive) return false;

            _currentPath = BossPathType.Reinforcement;

            // 检查并消耗道具
            string itemId = GetItemIdForReinforcement(type);
            string[] consumedItems;
            int allyCount;
            string[] allyNames;

            if (!TryConsumeReinforcementItem(type, itemId, out consumedItems, out allyCount, out allyNames))
            {
                DebugLog($"援军{type}不可用：缺少必要道具。");
                return false;
            }

            // 召唤援军（实际项目中实例化NPC预制体）
            SpawnAllies(type, allyCount);

            EventBus.Publish(new BossReinforcementEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                ReinforcementType = type.ToString(),
                AllyCount = allyCount,
                AllyNames = allyNames,
                ConsumedItems = consumedItems
            });

            EventBus.Publish(new BossPathSelectedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                PathType = "Reinforcement",
                PlayerId = ""
            });

            DebugLog($"玩家召唤援军 [{type}]。盟友: {allyCount}人");
            EnsureCombatReady();
            return true;
        }

        /// <summary>
        /// 外交谈判——玩家接受条件 (003-PATH-02)。
        /// 根据有效成功率进行判定。
        /// </summary>
        /// <returns>谈判是否被接受.</returns>
        public bool AcceptDiplomacy()
        {
            if (!_initialized) return false;
            if (_currentPath != BossPathType.Diplomacy)
            {
                DebugLog("未处于外交路径，无法接受条件。");
                return false;
            }

            // 检查玩家是否有满足条件的道具
            if (!HasRequiredItems())
            {
                DebugLog($"条件不足：缺少{bossDef.diplomacy.conditionDescription}");
                return false;
            }

            float effectiveRate = CalculateEffectiveDiplomacyRate();
            bool accepted = UnityEngine.Random.value < effectiveRate;

            _diplomacyResult = accepted ? DiplomacyResult.Accepted : DiplomacyResult.Rejected;

            EventBus.Publish(new BossDiplomacyResultEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                Result = accepted ? "Accepted" : "Rejected",
                Dialogue = accepted ? "条件已接受……你可以通过了。" : "不够……这点诚意还不够。",
                GrudgeChange = accepted ? 0 : 1
            });

            if (accepted)
            {
                // 谈判成功→和平通过 (003-DIPLO-03)
                EnterPeaceState();
                DistributeRewards(BossPathType.Diplomacy);
                DebugLog($"谈判成功！和平通过。窗口期: {bossDef.diplomacy.peaceWindowDuration}s");
            }
            else
            {
                // 谈判失败→进入战斗 (003-DIPLO-04)
                GrudgeManager.IncreaseGrudge(bossDef.bossId, 1, "外交谈判失败");
                EnsureCombatReady();
                DebugLog($"谈判被拒绝，进入战斗。");
            }

            return accepted;
        }

        /// <summary>
        /// 玩家拒绝外交条件 → 直接进入战斗。
        /// </summary>
        public void RejectDiplomacy()
        {
            if (!_initialized) return;
            _diplomacyResult = DiplomacyResult.Rejected;

            EventBus.Publish(new BossDiplomacyResultEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                Result = "Rejected",
                Dialogue = bossDef.retreatDialogue,
                GrudgeChange = 0
            });

            EnsureCombatReady();
            DebugLog($"玩家拒绝了外交条件，进入战斗。");
        }

        /// <summary>
        /// 玩家在和平窗口期攻击BOSS → 背叛 (003-BETRAY-01)。
        /// 由 CombatSystem 在玩家造成伤害时调用。
        /// </summary>
        public void OnPlayerBetrayed()
        {
            if (!_initialized || !_isPeaceActive) return;

            _isPeaceActive = false;
            _diplomacyResult = DiplomacyResult.Betrayed;

            // 记仇系统：背叛→记仇+4 (003-BETRAY-01)
            GrudgeManager.RecordBetrayal(bossDef.bossId);

            EventBus.Publish(new BossDiplomacyResultEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                Result = "Betrayed",
                Dialogue = "你竟敢背叛我？！受死吧！！",
                GrudgeChange = 4
            });

            EventBus.Publish(new BossPeaceBrokenEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                GrudgeLevel = (int)GrudgeLevel.Vengeful,
                IsEnraged = true
            });

            // 恢复BOSS战斗能力（狂暴状态）
            RestoreBossCombatMode();

            DebugLog($"玩家背叛！BOSS狂暴+记仇+4 (003-BETRAY-01)");
        }

        #endregion

        #region Diplomacy System

        /// <summary>
        /// 计算有效外交成功率。
        /// 基础成功率 × 记仇等级修正 (003-DIPLO-05)。
        /// </summary>
        private float CalculateEffectiveDiplomacyRate()
        {
            float baseRate = bossDef.diplomacy.baseSuccessRate;
            float grudgePenalty = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache).DiplomacyPenalty;
            return Mathf.Clamp01(baseRate * grudgePenalty);
        }

        /// <summary>
        /// 检查玩家是否拥有所有必需道具 (003-DIPLO-06)。
        /// </summary>
        private bool HasRequiredItems()
        {
            if (bossDef.diplomacy.requiredItems == null || bossDef.diplomacy.requiredItems.Length == 0)
                return true;

            foreach (string itemId in bossDef.diplomacy.requiredItems)
            {
                if (!HasItem(itemId))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 占位方法：检查玩家背包是否有指定道具。
        /// 实际项目中接入 InventorySystem。
        /// </summary>
        private bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            // TODO: 接入 InventorySystem.HasItem(itemId)
            // 临时默认返回 true（仅用于测试）
            return true;
        }

        /// <summary>
        /// 消耗玩家持有的道具。
        /// </summary>
        private bool ConsumeItems(string[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0) return true;

            foreach (string id in itemIds)
            {
                if (!string.IsNullOrEmpty(id) && !ConsumeItem(id))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 占位方法：从玩家背包消耗一个道具。
        /// </summary>
        private bool ConsumeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return true;
            // TODO: 接入 InventorySystem.ConsumeItem(itemId)
            return true;
        }

        #endregion

        #region Stealth System

        /// <summary>
        /// 计算潜行成功率 (003-STEALTH-01)。
        /// 公式: StealthSuccess = 0.6 + Level×0.01 + EquipBonus - BossPerception×0.02 - 记仇修正
        /// </summary>
        /// <param name="playerLevel">玩家等级.</param>
        /// <param name="equipmentBonus">潜行装备加成 (0~1).</param>
        /// <returns>最终成功率 (0~1).</returns>
        public float CalculateStealthSuccessRate(int playerLevel, float equipmentBonus)
        {
            if (bossDef == null) return STEALTH_BASE_RATE;

            // BOSS感知 = 境界 × 每境界基础感知
            float bossPerception = bossDef.realm * bossBasePerceptionPerRealm;

            // 记仇修正
            float grudgePenalty = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache).StealthPenalty;

            float rate = STEALTH_BASE_RATE
                         + playerLevel * stealthLevelFactor
                         + equipmentBonus
                         - bossPerception * stealthPerceptionFactor
                         - grudgePenalty;

            return Mathf.Clamp01(rate);
        }

        #endregion

        #region Reinforcement System

        /// <summary>
        /// 获取援军类型对应的道具ID。
        /// </summary>
        private string GetItemIdForReinforcement(ReinforcementType type)
        {
            return type switch
            {
                ReinforcementType.SectDisciple => sectTokenItemId,
                ReinforcementType.Talisman     => talismanItemId,
                ReinforcementType.TempParty    => tempPartyItemId,
                _ => null
            };
        }

        /// <summary>
        /// 尝试消耗援军所需道具并返回援军信息。
        /// NPC类型不需要消耗道具（附近有友好NPC）。
        /// </summary>
        private bool TryConsumeReinforcementItem(ReinforcementType type, string itemId,
            out string[] consumedItems, out int allyCount, out string[] allyNames)
        {
            consumedItems = Array.Empty<string>();
            allyCount = 0;
            allyNames = Array.Empty<string>();

            if (type == ReinforcementType.NPC)
            {
                // NPC援军不需要消耗道具 (003-REIN-03)
                if (!TryFindNearbyNPC(out allyCount, out allyNames))
                {
                    DebugLog("附近没有可召唤的友好NPC。");
                    return false;
                }
                return true;
            }

            // 道具类援军：检查并消耗道具
            if (string.IsNullOrEmpty(itemId))
            {
                DebugLog($"援军类型 {type} 未配置道具ID。");
                return false;
            }

            if (!HasItem(itemId))
            {
                DebugLog($"缺少道具 {itemId}，无法召唤援军。");
                return false;
            }

            ConsumeItem(itemId);
            consumedItems = new[] { itemId };
            allyCount = GetAllyCountForType(type);
            allyNames = GenerateAllyNames(type, allyCount);
            return true;
        }

        /// <summary>
        /// 搜索附近友好NPC（占位实现）。
        /// 实际项目中应通过 NPCManager 或区域系统查询。
        /// </summary>
        private bool TryFindNearbyNPC(out int count, out string[] names)
        {
            // TODO: 接入 NPCManager
            count = 0;
            names = Array.Empty<string>();
            return false;
        }

        /// <summary>
        /// 根据援军类型获取盟友数量。
        /// </summary>
        private int GetAllyCountForType(ReinforcementType type)
        {
            return type switch
            {
                ReinforcementType.SectDisciple => 2,   // 同门弟子 2人
                ReinforcementType.Talisman     => 1,   // 召唤符灵 1个
                ReinforcementType.TempParty    => 3,   // 临时队伍 3人
                _ => 0
            };
        }

        /// <summary>
        /// 生成盟友名称用于事件。
        /// </summary>
        private string[] GenerateAllyNames(ReinforcementType type, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = type switch
                {
                    ReinforcementType.SectDisciple => $"同门弟子·{i + 1}",
                    ReinforcementType.Talisman     => $"召唤符灵·{i + 1}",
                    ReinforcementType.TempParty    => $"临时队友·{i + 1}",
                    _ => $"盟友·{i + 1}"
                };
            }
            return names;
        }

        /// <summary>
        /// 实例化援军预制体（占位实现）。
        /// 实际项目中实例化 NPC/召唤物 预制体并设置AI。
        /// </summary>
        private void SpawnAllies(ReinforcementType type, int count)
        {
            // TODO: 接入 ObjectPool / NPCManager 实例化盟友
            DebugLog($"援军 {type} x{count} 被召唤（实际项目中实例化预制体）。");
        }

        #endregion

        #region Reward System

        /// <summary>
        /// 奖励倍率配置。
        /// </summary>
        private struct RewardMultiplier
        {
            public float drop;
            public float cultivation;
            public float reputation;
            public bool title;
        }

        /// <summary>
        /// 根据路径类型发放奖励 (003-PATH-07)。
        /// 奖励梯度: Combat > Diplomacy > Reinforcement > Stealth
        /// </summary>
        public void DistributeRewards(BossPathType pathType)
        {
            if (bossDef == null) return;

            RewardMultiplier mult = pathType switch
            {
                BossPathType.Combat        => REWARD_COMBAT,
                BossPathType.Diplomacy     => REWARD_DIPLOMACY,
                BossPathType.Stealth       => REWARD_STEALTH,
                BossPathType.Reinforcement => REWARD_REINFORCEMENT,
                _ => REWARD_COMBAT
            };

            // 记仇系统影响：记仇越高，战斗奖励越低（BOSS携带重要宝物逃跑了）
            float grudgeRewardPenalty = 1.0f;
            if (pathType == BossPathType.Combat)
            {
                // 高记仇BOSS会摧毁部分宝物
                float grudgeMult = GrudgeManager.GetDamageBonus(bossDef.bossId);
                grudgeRewardPenalty = Mathf.Lerp(1.0f, 0.8f, (grudgeMult - 1.0f) / 0.3f);
            }

            float finalDropMult = mult.drop * grudgeRewardPenalty;
            float finalCultMult = mult.cultivation * grudgeRewardPenalty;
            float finalRepMult = mult.reputation * grudgeRewardPenalty;

            // 发布奖励事件（由 RewardSystem / DropSystem 消费）
            EventBus.Publish(new BossRewardDistributionEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                PathType = pathType.ToString(),
                DropMultiplier = finalDropMult,
                CultivationMultiplier = finalCultMult,
                ReputationMultiplier = finalRepMult,
                UnlocksTitle = mult.title && pathType == BossPathType.Combat
            });

            DebugLog($"奖励分发 [{pathType}]: 掉落×{finalDropMult:F2} 修为×{finalCultMult:F2} 声望×{finalRepMult:F2} 称号={mult.title}");
        }

        /// <summary>
        /// 获取指定路径的奖励倍率摘要（供UI显示）。
        /// </summary>
        public string GetRewardSummary(BossPathType pathType)
        {
            RewardMultiplier r = pathType switch
            {
                BossPathType.Combat        => REWARD_COMBAT,
                BossPathType.Diplomacy     => REWARD_DIPLOMACY,
                BossPathType.Stealth       => REWARD_STEALTH,
                BossPathType.Reinforcement => REWARD_REINFORCEMENT,
                _ => REWARD_COMBAT
            };

            return $"[{pathType}] 掉落×{r.drop:F0%} | 修为×{r.cultivation:F0%} | 声望×{r.reputation:F0%}" +
                   (r.title ? " | ★可获得称号" : "");
        }

        #endregion

        #region Peace State Management

        /// <summary>
        /// 进入和平状态：BOSS不攻击、可通行 (003-DIPLO-03)。
        /// </summary>
        private void EnterPeaceState()
        {
            _isPeaceActive = true;
            _peaceDuration = bossDef.diplomacy.peaceWindowDuration;
            _peaceTimer = _peaceDuration;

            // 禁用BOSS AI
            if (bossAI != null) bossAI.enabled = false;

            // 禁用碰撞体（玩家可通行）
            if (_bossCollider != null) _bossCollider.enabled = false;

            // 视觉指示：变绿表示和平
            if (_bossRenderer != null && _bossRenderer.material != null)
            {
                _originalBossColor = _bossRenderer.material.color;
                StartCoroutine(FlashColor(_bossRenderer.material, Color.green, 1.5f));
                _bossRenderer.material.color = new Color(0.5f, 1f, 0.5f); // 浅绿色
            }

            DebugLog($"和平窗口开启: {_peaceDuration}s");
        }

        /// <summary>
        /// 和平窗口到期。
        /// </summary>
        private void ExpirePeace()
        {
            _isPeaceActive = false;
            _diplomacyResult = DiplomacyResult.Expired;

            EventBus.Publish(new BossDiplomacyResultEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                Result = "Expired",
                Dialogue = "和平窗口已过……下次见面就是敌人了。",
                GrudgeChange = 0
            });

            // 恢复BOSS
            RestoreBossCombatMode();

            DebugLog("和平窗口到期，BOSS恢复敌对状态。");
        }

        /// <summary>
        /// 恢复BOSS交战模式。
        /// </summary>
        private void RestoreBossCombatMode()
        {
            _isPeaceActive = false;

            // 重新启用BOSS AI
            if (bossAI != null) bossAI.enabled = true;

            // 恢复碰撞体（延迟一小段时间让玩家有时间反应）
            if (_bossCollider != null)
                StartCoroutine(DelayedColliderReEnable());

            // 恢复颜色
            if (_bossRenderer != null && _bossRenderer.material != null)
            {
                _bossRenderer.material.color = _originalBossColor != default
                    ? _originalBossColor
                    : Color.white;
            }
        }

        private System.Collections.IEnumerator DelayedColliderReEnable()
        {
            yield return new WaitForSeconds(PEACE_COLLIDER_REENABLE_DELAY);
            if (_bossCollider != null) _bossCollider.enabled = true;
        }

        #endregion

        #region Combat Mode

        /// <summary>
        /// 确保BOSS处于战斗就绪状态：启用AI、碰撞体、必要组件。
        /// </summary>
        private void EnsureCombatReady()
        {
            _isPeaceActive = false;

            if (bossAI != null)
            {
                bossAI.enabled = true;

                // 如果BOSS尚未进入战斗，触发出场
                if (bossAI.CurrentState == "Idle" || bossAI.CurrentState == "Detecting")
                {
                    // 模拟进入战斗（实际项目中可能由DetectPlayer触发）
                }
            }

            if (_bossCollider != null && !_bossCollider.enabled)
            {
                _bossCollider.enabled = true;
            }

            // 还原颜色
            if (_bossRenderer != null && _bossRenderer.material != null
                && _originalBossColor != default)
            {
                _bossRenderer.material.color = _originalBossColor;
            }
        }

        #endregion

        #region EventBus Handlers

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            if (evt.BossId != bossDef?.bossId) return;

            // BOSS被击败：发放战斗奖励 (003-PATH-01)
            // 潜行绕过不触发Defeated事件，只有战斗击杀才发
            if (_currentPath == BossPathType.Combat || _currentPath == BossPathType.Reinforcement)
            {
                DistributeRewards(BossPathType.Combat);
            }

            // 清理和平状态
            _isPeaceActive = false;
        }

        #endregion

        #region Effect Helpers

        private System.Collections.IEnumerator FlashColor(Material material, Color targetColor, float duration)
        {
            Color originalColor = material.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                material.color = Color.Lerp(originalColor, targetColor, Mathf.PingPong(elapsed * 3f, 1f));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        #endregion

        #region Debug / Editor

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[BossDiplomacy] {_bossNameCache}: {message}");
        }

        /// <summary>
        /// 获取完整的调试状态。
        /// </summary>
        public string GetDebugStatus()
        {
            if (!_initialized) return "[BossDiplomacy] 未初始化";

            var grudge = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache);
            return $"=== BOSS外交系统: {_bossNameCache} ===\n" +
                   $"路径: {_currentPath} | 外交: {_diplomacyResult}\n" +
                   $"和平: {_isPeaceActive} ({PeaceTimeRemaining:F1}s)\n" +
                   $"记仇: {grudge.level} (好感={grudge.Favorability})\n" +
                   $"可外交: {CanDiplomacy} | 可潜行: {CanStealth} | 可援军: {CanCallReinforcements}\n" +
                   $"潜行公式: 0.6 + 等级×{stealthLevelFactor} + 装备 - 感知×{stealthPerceptionFactor} - 记仇\n" +
                   GetRewardSummary(BossPathType.Combat) + "\n" +
                   GetRewardSummary(BossPathType.Diplomacy) + "\n" +
                   GetRewardSummary(BossPathType.Reinforcement) + "\n" +
                   GetRewardSummary(BossPathType.Stealth);
        }

        [ContextMenu("Debug: 打印状态")]
        private void DebugPrintStatus()
        {
            Debug.Log(GetDebugStatus());
        }

        [ContextMenu("Debug: 模拟外交成功")]
        private void DebugDiplomacySuccess()
        {
            if (!Application.isPlaying) return;
            ChooseDiplomacy();
            AcceptDiplomacy();
        }

        [ContextMenu("Debug: 模拟潜行")]
        private void DebugStealth()
        {
            if (!Application.isPlaying) return;
            ChooseStealth(30, 0.2f);
        }

        [ContextMenu("Debug: 模拟背叛")]
        private void DebugBetray()
        {
            if (!Application.isPlaying) return;
            ChooseDiplomacy();
            AcceptDiplomacy();
            OnPlayerBetrayed();
        }

        [ContextMenu("Debug: 打印记仇管理器")]
        private void DebugPrintGrudgeManager()
        {
            Debug.Log(GrudgeManager.DebugDump());
        }

        [ContextMenu("Debug: 清除所有记仇")]
        private void DebugClearGrudge()
        {
            GrudgeManager.ClearAll();
            Debug.Log("[BossDiplomacy] 所有记仇数据已清除。");
        }

        #endregion
    }
}
