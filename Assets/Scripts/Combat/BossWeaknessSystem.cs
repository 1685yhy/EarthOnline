using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Combat
{
    #region Enums

    /// <summary>运行时侦察状态，每个弱点独立追踪。</summary>
    public enum WeaknessReconState
    {
        Unknown,    // 未发现 — 不可见
        Discovered, // 已侦察 — 显示在UI，可用
        Exploited   // 已利用 — 本次战斗已触发过
    }

    /// <summary>四种侦察方式 (WEAK-02)。</summary>
    public enum ReconMethod
    {
        Observation, // 观察 — 自动免费，战斗中进行
        SpiritGaze,  // 望气术 — 消耗灵力，主动使用
        NPCIntel,    // NPC情报 — 消耗灵石，100%成功率
        BattleProbe  // 战斗试探 — BOSS攻击玩家时概率触发
    }

    #endregion

    #region Runtime Weakness State

    /// <summary>
    /// 单个弱点的运行时状态包装。
    /// 关联 BossDef.weaknesses[] 中的定义，加上运行时字段。
    /// </summary>
    [Serializable]
    public class WeaknessRuntimeState
    {
        public WeaknessDef definition;        // 原始定义引用（只读数据）
        public WeaknessReconState reconState; // 当前侦察状态
        public float lastExposureTime;        // 上次暴露时间戳（秒）
        public int exploitCount;              // 本场战斗利用次数

        /// <summary>是否已暴露给玩家（已发现或已利用）。</summary>
        public bool IsExposed => reconState == WeaknessReconState.Discovered ||
                                 reconState == WeaknessReconState.Exploited;

        /// <summary>是否可被利用（已暴露且未被标记为已利用）。</summary>
        public bool IsAvailable => reconState == WeaknessReconState.Discovered;

        /// <summary>弱点的伤害倍率（利用时生效）。</summary>
        public float DamageMultiplier => definition.damageMultiplier;

        /// <summary>弱点类型名称。</summary>
        public string WeaknessTypeName => definition.weaknessType.ToString();

        /// <summary>弱点显示名。</summary>
        public string DisplayName => definition.displayName;
    }

    #endregion

    #region Recon Strategy

    /// <summary>
    /// 侦察策略结果，封装一次侦察尝试的结果。
    /// </summary>
    public struct ReconResult
    {
        public ReconMethod method;
        public int weaknessesRevealed;          // 本次新发现的弱点数
        public WeaknessRuntimeState[] revealed; // 新发现的弱点列表
        public bool success;                    // 是否成功（NPCIntel永远true）
        public string failReason;               // 失败原因
    }

    #endregion

    /// <summary>
    /// BOSS弱点侦察系统 (WEAK-01 ~ WEAK-05)。
    ///
    /// 管理6种弱点类型的侦察/利用生命周期：
    ///   Unknown -> Discovered -> Exploited
    ///
    /// 四种侦察方式：
    ///   观察(Observation)  — 自动，战斗中定期免费触发，概率发现
    ///   望气术(SpiritGaze) — 主动技能，消耗灵力，立即发现所有未知弱点
    ///   NPC情报(NPCIntel)  — 主动技能，消耗灵石，100%发现2个弱点
    ///   战斗试探(BattleProbe) — BOSS命中玩家时概率触发，发现相关弱点
    ///
    /// 完美狩猎：一场战斗内利用所有弱点 => 掉落品质+1 (WEAK-05)
    /// 依赖 BossDef.weaknesses[] 中配置的弱点定义。
    ///
    /// 通过 EventBus 与 UI、VFX、掉落系统解耦通信。
    /// </summary>
    public class BossWeaknessSystem : MonoBehaviour
    {
        #region Constants

        private const float OBSERVATION_INTERVAL = 8f;       // 观察间隔（秒）
        private const float OBSERVATION_CHANCE = 0.3f;       // 每次观察发现概率
        private const float SPIRIT_GAZE_COST = 50f;           // 望气术灵力消耗
        private const float NPC_INTEL_COST = 200f;            // NPC情报灵石消耗
        private const float BATTLE_PROBE_CHANCE = 0.25f;      // 战斗试探触发概率
        private const int NPC_INTEL_REVEAL_COUNT = 2;         // NPC情报固定发现2个
        private const float EXPOSURE_COOLDOWN = 300f;         // 5分钟冷却后弱点可重新暴露

        // 侦察方式评估描述
        private static readonly string[] OBSERVATION_MESSAGES = new[]
        {
            "观察到{BOSS}的{弱点}有些薄弱……",
            "通过观察动作轨迹，发现{弱点}似乎是可以利用的突破口！",
            "仔细观察后，注意到{弱点}",
            "发现{BOSS}在{弱点}方面有明显破绽！"
        };

        private static readonly string[] SPIRIT_GAZE_MESSAGES = new[]
        {
            "运起望气术，洞察到{BOSS}在{弱点}处气机紊乱！",
            "灵气之眼看到了{BOSS}的灵气流转异常——{弱点}！",
            "望气术探测到{弱点}！灵力波动暴露了一切。"
        };

        private static readonly string[] NPC_INTEL_MESSAGES = new[]
        {
            "根据情报贩子的线索，{BOSS}的弱点是{弱点}。",
            "情报显示——攻击{弱点}会事半功倍！",
            "灵石没有白花，情报指出{弱点}是{BOSS}的死穴。"
        };

        private static readonly string[] BATTLE_PROBE_MESSAGES = new[]
        {
            "硬抗了这一击后，你意识到{弱点}可能是突破口。",
            "实战中发现了{BOSS}的{弱点}！记下这个发现。",
            "吃痛之下你敏锐地捕捉到了{弱点}的情报。"
        };

        #endregion

        #region Inspector Config

        [Header("-- BOSS 引用 --")]
        [Tooltip("关联的 BossAI 组件。为空则自动查找。")]
        public BossAI bossAI;
        [Tooltip("关联的 BossDef 定义。为空则从 BossAI 读取。")]
        public BossDef bossDef;

        [Header("-- 侦察参数 --")]
        [Tooltip("观察间隔（秒），战斗中每隔X秒尝试一次自动观察。")]
        public float observationInterval = OBSERVATION_INTERVAL;
        [Range(0f, 1f)]
        [Tooltip("每次观察发现弱点的基础概率。")]
        public float observationChance = OBSERVATION_CHANCE;
        [Tooltip("望气术消耗的灵力值。")]
        public float spiritGazeCost = SPIRIT_GAZE_COST;
        [Tooltip("NPC情报消耗的灵石数量。")]
        public float npcIntelCost = NPC_INTEL_COST;
        [Tooltip("BOSS命中玩家时触发试探的概率。")]
        public float battleProbeChance = BATTLE_PROBE_CHANCE;

        [Header("-- 视觉特效引用 (可选) --")]
        [Tooltip("放置在弱点攻击时的特效预制体。可以为空，由 VFX 系统监听事件响应。")]
        public GameObject weaknessVFXPrefab;
        [Tooltip("完美狩猎庆祝特效预制体。")]
        public GameObject perfectHuntVFXPrefab;

        #endregion

        #region Private State

        // 每个弱点的运行时状态，按 bossDef.weaknesses 顺序排列
        private WeaknessRuntimeState[] _weaknessStates;

        // 观察计时器
        private float _observationTimer;

        // 计数
        private int _totalWeaknesses;
        private int _discoveredCount;
        private int _exploitedCount;

        // 完美狩猎状态
        private bool _perfectHuntAchieved;

        // 初始化标志
        private bool _initialized;
        private bool _isReady;

        // 缓存消息模板变量
        private string _bossNameCache = "BOSS";

        #endregion

        #region Public Properties

        /// <summary>本场战斗是否达成完美狩猎。</summary>
        public bool PerfectHuntAchieved => _perfectHuntAchieved;

        /// <summary>已发现的弱点数量。</summary>
        public int DiscoveredCount => _discoveredCount;

        /// <summary>已利用的弱点数量。</summary>
        public int ExploitedCount => _exploitedCount;

        /// <summary>BOSS总弱点数。</summary>
        public int TotalWeaknesses => _totalWeaknesses;

        /// <summary>是否已完成初始化。</summary>
        public bool IsReady => _isReady;

        /// <summary>获取运行时状态的只读视图。</summary>
        public IReadOnlyList<WeaknessRuntimeState> WeaknessStates => _weaknessStates;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 尝试自动获取引用
            if (bossAI == null)
                bossAI = GetComponent<BossAI>();

            if (bossAI != null && bossDef == null)
                bossDef = bossAI.bossDef;

            if (bossDef == null)
            {
                Debug.LogError("[BossWeaknessSystem] BossDef 无法解析。系统禁用。");
                enabled = false;
                return;
            }

            InitializeWeaknessStates();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossBreathingWindowEvent>(OnBreathingWindow);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossBreathingWindowEvent>(OnBreathingWindow);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void Start()
        {
            _bossNameCache = bossDef != null ? bossDef.displayName : "BOSS";

            // 将所有未初始化的弱点标记为 Unknown
            foreach (var ws in _weaknessStates)
            {
                if (ws.reconState == WeaknessReconState.Unknown)
                    continue;
                ws.reconState = WeaknessReconState.Unknown;
            }

            _discoveredCount = 0;
            _exploitedCount = 0;
            _perfectHuntAchieved = false;
            _observationTimer = observationInterval; // 战斗开始后观察

            // 存在弱点才启用系统
            _isReady = _totalWeaknesses > 0;

            if (!_isReady)
            {
                Debug.Log($"[BossWeaknessSystem] {_bossNameCache} 没有配置弱点，系统空转。");
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || !_isReady || !enabled) return;

            // 仅在 Combat 状态下进行自动观察
            if (bossAI != null && bossAI.CurrentState == "Combat")
            {
                UpdateObservation();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 从 BossDef 创建运行时弱点状态数组。
        /// 如果 BossDef 没有配置弱点，创建空数组。
        /// </summary>
        private void InitializeWeaknessStates()
        {
            if (bossDef.weaknesses == null || bossDef.weaknesses.Length == 0)
            {
                _weaknessStates = Array.Empty<WeaknessRuntimeState>();
                _totalWeaknesses = 0;
                return;
            }

            _totalWeaknesses = bossDef.weaknesses.Length;
            _weaknessStates = new WeaknessRuntimeState[_totalWeaknesses];

            for (int i = 0; i < _totalWeaknesses; i++)
            {
                _weaknessStates[i] = new WeaknessRuntimeState
                {
                    definition = bossDef.weaknesses[i],
                    reconState = WeaknessReconState.Unknown,
                    lastExposureTime = -EXPOSURE_COOLDOWN, // 初始无冷却
                    exploitCount = 0
                };
            }

            Debug.Log($"[BossWeaknessSystem] 初始化完成：{_totalWeaknesses} 个弱点。");
        }

        #endregion

        #region Public API — Recon Methods

        /// <summary>
        /// 观察：自动侦察，战斗中周期性触发（WEAK-02）。
        /// 免费，概率发现1个随机弱点。
        /// </summary>
        /// <returns>侦察结果。</returns>
        public ReconResult TryObservation()
        {
            if (!_isReady) return FailedResult(ReconMethod.Observation, "无弱点可侦察。");

            // 检查是否有尚未发现的弱点
            var unknown = GetWeaknessesInState(WeaknessReconState.Unknown);
            if (unknown.Count == 0)
            {
                return FailedResult(ReconMethod.Observation, "所有弱点已发现。");
            }

            // 概率判定
            if (UnityEngine.Random.value > observationChance)
            {
                return FailedResult(ReconMethod.Observation, "观察未发现异常。");
            }

            // 随机发现1个未知弱点
            var target = unknown[UnityEngine.Random.Range(0, unknown.Count)];
            return RevealWeakness(target, ReconMethod.Observation);
        }

        /// <summary>
        /// 望气术：消耗灵力发现所有未知弱点（WEAK-02）。
        /// </summary>
        /// <param name="availableMana">当前可用灵力值。</param>
        /// <returns>侦察结果。success=false 表示灵力不足。</returns>
        public ReconResult TrySpiritGaze(float availableMana)
        {
            if (!_isReady) return FailedResult(ReconMethod.SpiritGaze, "无弱点可侦察。");

            if (availableMana < spiritGazeCost)
            {
                return new ReconResult
                {
                    method = ReconMethod.SpiritGaze,
                    success = false,
                    weaknessesRevealed = 0,
                    revealed = Array.Empty<WeaknessRuntimeState>(),
                    failReason = $"灵力不足（需要{spiritGazeCost}，当前{availableMana}）。"
                };
            }

            var unknown = GetWeaknessesInState(WeaknessReconState.Unknown);
            if (unknown.Count == 0)
            {
                return FailedResult(ReconMethod.SpiritGaze, "所有弱点已发现。");
            }

            // 望气术发现所有未知弱点
            int count = unknown.Count;
            List<WeaknessRuntimeState> revealed = new List<WeaknessRuntimeState>();

            for (int i = 0; i < count; i++)
            {
                var result = RevealWeakness(unknown[i], ReconMethod.SpiritGaze);
                if (result.success)
                    revealed.Add(unknown[i]);
            }

            return new ReconResult
            {
                method = ReconMethod.SpiritGaze,
                success = revealed.Count > 0,
                weaknessesRevealed = revealed.Count,
                revealed = revealed.ToArray(),
                failReason = revealed.Count > 0 ? "" : "所有弱点已发现。"
            };
        }

        /// <summary>
        /// NPC情报：消耗灵石，100%概率发现最多2个未知弱点（WEAK-04）。
        /// </summary>
        /// <param name="availableCurrency">当前可用灵石数量。</param>
        /// <returns>侦察结果。success=false 表示灵石不足。</returns>
        public ReconResult TryNPCIntel(float availableCurrency)
        {
            if (!_isReady) return FailedResult(ReconMethod.NPCIntel, "无弱点可侦察。");

            if (availableCurrency < npcIntelCost)
            {
                return new ReconResult
                {
                    method = ReconMethod.NPCIntel,
                    success = false,
                    weaknessesRevealed = 0,
                    revealed = Array.Empty<WeaknessRuntimeState>(),
                    failReason = $"灵石不足（需要{npcIntelCost}，当前{availableCurrency}）。"
                };
            }

            var unknown = GetWeaknessesInState(WeaknessReconState.Unknown);
            if (unknown.Count == 0)
            {
                return FailedResult(ReconMethod.NPCIntel, "所有弱点已发现。");
            }

            // NPC情报固定发现NPC_INTEL_REVEAL_COUNT个（不超过剩余数）
            int revealCount = Mathf.Min(NPC_INTEL_REVEAL_COUNT, unknown.Count);
            List<WeaknessRuntimeState> revealed = new List<WeaknessRuntimeState>();

            // 打乱后取前N个（模拟情报贩子知道的部分弱点）
            ShuffleList(unknown);
            for (int i = 0; i < revealCount; i++)
            {
                var result = RevealWeakness(unknown[i], ReconMethod.NPCIntel);
                if (result.success)
                    revealed.Add(unknown[i]);
            }

            return new ReconResult
            {
                method = ReconMethod.NPCIntel,
                success = revealed.Count > 0,
                weaknessesRevealed = revealed.Count,
                revealed = revealed.ToArray(),
                failReason = revealed.Count > 0 ? "" : "所有弱点已发现。"
            };
        }

        /// <summary>
        /// 战斗试探：BOSS命中玩家时触发（WEAK-02）。
        /// 概率发现1个与此次伤害类型相关的弱点。
        /// </summary>
        /// <param name="damageType">伤害类型（如 "Physical", "Fire", "Water" 等）。</param>
        /// <returns>侦察结果。</returns>
        public ReconResult TryBattleProbe(string damageType)
        {
            if (!_isReady) return FailedResult(ReconMethod.BattleProbe, "无弱点可侦察。");

            // 概率判定
            if (UnityEngine.Random.value > battleProbeChance)
            {
                return FailedResult(ReconMethod.BattleProbe, "未能从攻击中获取有效信息。");
            }

            var unknown = GetWeaknessesInState(WeaknessReconState.Unknown);
            if (unknown.Count == 0)
            {
                return FailedResult(ReconMethod.BattleProbe, "所有弱点已发现。");
            }

            // 优先发现与伤害类型相关的弱点（属性相克 + 环境利用）
            WeaknessRuntimeState priorityTarget = null;
            foreach (var ws in unknown)
            {
                if (ws.definition.weaknessType == WeaknessType.Element ||
                    ws.definition.weaknessType == WeaknessType.Environment)
                {
                    // 如果 elementType 与伤害类型相关联
                    if (!string.IsNullOrEmpty(ws.definition.elementType) &&
                        ws.definition.elementType.Equals(damageType, StringComparison.OrdinalIgnoreCase))
                    {
                        priorityTarget = ws;
                        break;
                    }
                }
            }

            var target = priorityTarget ?? unknown[UnityEngine.Random.Range(0, unknown.Count)];
            return RevealWeakness(target, ReconMethod.BattleProbe);
        }

        #endregion

        #region Public API — Weakness Exploitation

        /// <summary>
        /// 标记一个弱点已被利用（在攻击造成伤害后调用）。
        /// 发布 WeaknessExploitEvent，如果达成完美狩猎则发布 PerfectHuntEvent。
        /// </summary>
        /// <param name="weaknessTypeName">弱点类型名称（与 WeaknessType.ToString() 匹配）。</param>
        /// <returns>该弱点的伤害倍率；如果弱点未暴露则返回 1.0。</returns>
        public float MarkWeaknessExploited(string weaknessTypeName)
        {
            if (!_isReady || string.IsNullOrEmpty(weaknessTypeName))
                return 1f;

            var ws = FindWeaknessByTypeName(weaknessTypeName);
            if (ws == null || !ws.IsAvailable)
                return 1f;

            // 标记为已利用
            ws.reconState = WeaknessReconState.Exploited;
            ws.exploitCount++;
            ws.lastExposureTime = Time.time;

            _exploitedCount++;

            float multiplier = ws.DamageMultiplier;

            // 发布弱点利用事件
            EventBus.Publish(new WeaknessExploitEvent
            {
                BossId = bossDef.bossId,
                WeaknessType = ws.WeaknessTypeName,
                DamageMultiplier = multiplier,
                IsPerfectHunt = false // 稍后检查
            });

            // 发布 VFX 事件
            EventBus.Publish(new WeaknessVFXEvent
            {
                BossId = bossDef.bossId,
                WeaknessType = ws.WeaknessTypeName,
                ElementType = ws.definition.elementType,
                DamageMultiplier = multiplier
            });

            // 检查完美狩猎
            CheckPerfectHunt();

            Debug.Log($"[BossWeaknessSystem] 弱点利用: {ws.DisplayName} (×{multiplier})");

            return multiplier;
        }

        /// <summary>
        /// 根据弱点类型枚举值标记利用（由 CombatSystem 调用更方便的接口）。
        /// </summary>
        public float MarkWeaknessExploited(WeaknessType weaknessType)
        {
            return MarkWeaknessExploited(weaknessType.ToString());
        }

        /// <summary>
        /// 获取指定弱点的当前伤害倍率。
        /// 如果弱点尚未暴露或已被利用，返回 1.0（无加成）。
        /// </summary>
        public float GetWeaknessMultiplier(string weaknessTypeName)
        {
            var ws = FindWeaknessByTypeName(weaknessTypeName);
            if (ws != null && ws.IsAvailable)
                return ws.DamageMultiplier;
            return 1f;
        }

        /// <summary>
        /// 获取指定弱点类型的运行时状态。
        /// </summary>
        public WeaknessRuntimeState GetWeaknessState(WeaknessType type)
        {
            return FindWeaknessByTypeName(type.ToString());
        }

        /// <summary>
        /// 获取所有已发现弱点的伤害倍率字典（供 UI 显示）。
        /// </summary>
        public Dictionary<string, float> GetDiscoveredMultipliers()
        {
            var result = new Dictionary<string, float>();
            if (!_isReady) return result;

            foreach (var ws in _weaknessStates)
            {
                if (ws.IsExposed)
                {
                    result[ws.DisplayName] = ws.DamageMultiplier;
                }
            }
            return result;
        }

        /// <summary>
        /// 获取侦察进度信息（供 UI 显示）。
        /// </summary>
        public string GetReconProgress()
        {
            if (!_isReady)
                return "该BOSS没有弱点";

            return $"侦察进度: {_discoveredCount}/{_totalWeaknesses} | " +
                   $"利用进度: {_exploitedCount}/{_totalWeaknesses} | " +
                   (_perfectHuntAchieved ? "★ 完美狩猎达成！" : "");
        }

        #endregion

        #region EventBus Handlers

        private void OnBreathingWindow(BossBreathingWindowEvent evt)
        {
            // 喘息窗口期间不进行观察（BOSS不可攻击）
            // 仅记录状态，观察在 Update 中通过 bossAI.CurrentState 判断
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            // 如果 BOSS 被击败时尚未达成完美狩猎，标记失败
            if (!_perfectHuntAchieved && _discoveredCount > 0 && _exploitedCount < _totalWeaknesses)
            {
                Debug.Log($"[BossWeaknessSystem] {_bossNameCache} 被击败，未完满狩猎。" +
                          $"利用: {_exploitedCount}/{_totalWeaknesses}");
            }
        }

        #endregion

        #region Internal: Recon Processing

        /// <summary>
        /// 更新自动观察计时器。
        /// </summary>
        private void UpdateObservation()
        {
            _observationTimer -= Time.deltaTime;
            if (_observationTimer <= 0f)
            {
                _observationTimer = observationInterval;
                TryObservation();
            }
        }

        /// <summary>
        /// 将指定弱点从未知状态转换为已发现状态，并发布事件。
        /// </summary>
        private ReconResult RevealWeakness(WeaknessRuntimeState target, ReconMethod method)
        {
            if (target.reconState != WeaknessReconState.Unknown)
            {
                return FailedResult(method, "该弱点已被发现。");
            }

            // 检查暴露冷却
            if (Time.time - target.lastExposureTime < EXPOSURE_COOLDOWN)
            {
                return FailedResult(method, "弱点暴露冷却中。");
            }

            target.reconState = WeaknessReconState.Discovered;
            target.lastExposureTime = Time.time;
            _discoveredCount++;

            // 生成发现消息
            string message = ComposeReconMessage(method, target);

            // 发布弱点发现事件
            EventBus.Publish(new WeaknessDiscoveredEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                WeaknessType = target.WeaknessTypeName,
                DisplayName = target.DisplayName,
                DamageMultiplier = target.DamageMultiplier,
                ReconMethod = method.ToString()
            });

            // 发布 UI 更新事件
            PublishUIUpdate();

            Debug.Log($"[BossWeaknessSystem] {message}");

            return new ReconResult
            {
                method = method,
                success = true,
                weaknessesRevealed = 1,
                revealed = new[] { target },
                failReason = ""
            };
        }

        /// <summary>
        /// 检查是否达成完美狩猎：所有弱点均已利用。
        /// 如果达成，发布 PerfectHuntEvent。
        /// </summary>
        private void CheckPerfectHunt()
        {
            if (_perfectHuntAchieved) return;

            // 检查所有弱点是否均已被利用
            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].reconState != WeaknessReconState.Exploited)
                    return; // 还有未利用的弱点
            }

            // 完美狩猎达成！
            _perfectHuntAchieved = true;

            EventBus.Publish(new PerfectHuntEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                TotalWeaknesses = _totalWeaknesses
            });

            // 同步更新 WeaknessExploitEvent 的 IsPerfectHunt 标记
            // 发布一个额外的 UI 事件刷新
            PublishUIUpdate();

            Debug.Log($"[BossWeaknessSystem] ★★★ 完美狩猎达成！{_bossNameCache} 的 {_totalWeaknesses} 个弱点全部被利用！★★★");

            // 触发完美狩猎 VFX
            if (perfectHuntVFXPrefab != null)
            {
                Instantiate(perfectHuntVFXPrefab, transform.position, Quaternion.identity, transform);
            }
        }

        /// <summary>
        /// 发布 UI 刷新事件，通知弱点面板更新。
        /// </summary>
        private void PublishUIUpdate()
        {
            // 收集已发现弱点的类型和名称
            List<string> discoveredTypes = new List<string>();
            List<string> discoveredNames = new List<string>();

            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].IsExposed)
                {
                    discoveredTypes.Add(_weaknessStates[i].WeaknessTypeName);
                    discoveredNames.Add(_weaknessStates[i].DisplayName);
                }
            }

            EventBus.Publish(new WeaknessUIUpdateEvent
            {
                BossId = bossDef.bossId,
                WeaknessesDiscovered = _discoveredCount,
                TotalWeaknesses = _totalWeaknesses,
                DiscoveredTypes = discoveredTypes.ToArray(),
                DiscoveredNames = discoveredNames.ToArray()
            });
        }

        #endregion

        #region Internal: Query Helpers

        /// <summary>
        /// 获取所有处于指定状态的弱点。
        /// </summary>
        private List<WeaknessRuntimeState> GetWeaknessesInState(WeaknessReconState state)
        {
            var results = new List<WeaknessRuntimeState>();
            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].reconState == state)
                    results.Add(_weaknessStates[i]);
            }
            return results;
        }

        /// <summary>
        /// 根据弱点类型名称查找运行时状态。
        /// </summary>
        private WeaknessRuntimeState FindWeaknessByTypeName(string typeName)
        {
            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].WeaknessTypeName == typeName)
                    return _weaknessStates[i];
            }
            return null;
        }

        #endregion

        #region Internal: Messaging

        /// <summary>
        /// 根据侦察方式和目标弱点组合发现消息。
        /// </summary>
        private string ComposeReconMessage(ReconMethod method, WeaknessRuntimeState target)
        {
            string[] templates;

            switch (method)
            {
                case ReconMethod.Observation:
                    templates = OBSERVATION_MESSAGES;
                    break;
                case ReconMethod.SpiritGaze:
                    templates = SPIRIT_GAZE_MESSAGES;
                    break;
                case ReconMethod.NPCIntel:
                    templates = NPC_INTEL_MESSAGES;
                    break;
                case ReconMethod.BattleProbe:
                    templates = BATTLE_PROBE_MESSAGES;
                    break;
                default:
                    return $"发现在 {_bossNameCache} 身上有弱点：{target.DisplayName} (×{target.DamageMultiplier})";
            }

            string template = templates[UnityEngine.Random.Range(0, templates.Length)];
            string message = template
                .Replace("{BOSS}", _bossNameCache)
                .Replace("{弱点}", target.DisplayName);

            return message;
        }

        /// <summary>
        /// 创建一个失败的侦察结果。
        /// </summary>
        private static ReconResult FailedResult(ReconMethod method, string reason)
        {
            return new ReconResult
            {
                method = method,
                success = false,
                weaknessesRevealed = 0,
                revealed = Array.Empty<WeaknessRuntimeState>(),
                failReason = reason
            };
        }

        /// <summary>
        /// Fisher-Yates 洗牌算法，用于随机化列表。
        /// </summary>
        private static void ShuffleList<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        #endregion

        #region Editor / Debug Helpers

        /// <summary>
        /// 获取完整的调试状态字符串。
        /// </summary>
        public string GetDebugStatus()
        {
            if (!_initialized)
                return "[BossWeaknessSystem] 未初始化";

            string result = $"=== BOSS弱点侦察系统: {_bossNameCache} ===\n" +
                            $"总弱点: {_totalWeaknesses} | 已发现: {_discoveredCount} | 已利用: {_exploitedCount}\n" +
                            $"完美狩猎: {(_perfectHuntAchieved ? "★ 已达成" : "未达成")}\n" +
                            $"观察计时器: {_observationTimer:F1}s\n\n";

            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                var ws = _weaknessStates[i];
                string stateStr = ws.reconState switch
                {
                    WeaknessReconState.Unknown => "???",
                    WeaknessReconState.Discovered => $"已发现 (×{ws.DamageMultiplier})",
                    WeaknessReconState.Exploited => $"已利用 (×{ws.DamageMultiplier}, {ws.exploitCount}次)",
                    _ => "未知"
                };
                result += $"[{ws.WeaknessTypeName}] {ws.DisplayName}: {stateStr}\n";
            }

            return result;
        }

        /// <summary>
        /// 调试：手动强制发现所有弱点（仅编辑器/测试用）。
        /// </summary>
        [ContextMenu("Debug: 强制发现所有弱点")]
        private void DebugRevealAll()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].reconState == WeaknessReconState.Unknown)
                {
                    RevealWeakness(_weaknessStates[i], ReconMethod.SpiritGaze);
                }
            }
            Debug.Log("[BossWeaknessSystem] [Debug] 所有弱点已强制发现。");
        }

        /// <summary>
        /// 调试：强制标记所有弱点已被利用（仅编辑器/测试用）。
        /// </summary>
        [ContextMenu("Debug: 强制利用所有弱点")]
        private void DebugExploitAll()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < _weaknessStates.Length; i++)
            {
                if (_weaknessStates[i].IsAvailable)
                {
                    MarkWeaknessExploited(_weaknessStates[i].WeaknessTypeName);
                }
            }
            Debug.Log("[BossWeaknessSystem] [Debug] 所有弱点已强制利用。");
        }

        /// <summary>
        /// 调试：打印当前状态到控制台。
        /// </summary>
        [ContextMenu("Debug: 打印状态")]
        private void DebugPrintStatus()
        {
            Debug.Log(GetDebugStatus());
        }

        #endregion
    }
}
