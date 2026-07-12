using System.Collections;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Combat
{
    /// <summary>
    /// BOSS记仇系统 (Story 004) — 记仇值追踪 + 等级判定 + 撤退处理。
    ///
    /// 记仇等级4级 (004-GRUDGE-01):
    ///   警惕(Annoyed) → 记恨(Angry) → 仇恨(Furious) → 宿敌(Vengeful)
    ///
    /// 记仇触发:
    ///   逃跑(+1)    — 战斗中跑出 leashRange
    ///   反悔(+2)    — 外交和平窗口期攻击或拒绝已接受的条件
    ///   击杀同族(+3) — 在BOSS区域击杀同种类生物
    ///
    /// 记仇下降:
    ///   30天不进区域 — 降低1级 (004-DECAY-01)
    ///   击杀BOSS    — 清零 (004-RESET-01)
    ///
    /// 撤退惩罚 (004-RETREAT-01):
    ///   BOSS位置重置 + 区域怪物攻击性+20%
    ///
    /// 依赖 GrudgeManager (BossDiplomacy.cs) 做持久化存储。
    /// 依赖 BossDef.leashRange 做撤退距离判定。
    /// </summary>
    public class BossGrudgeSystem : MonoBehaviour
    {
        #region Constants

        private const float REAL_DAY_IN_SECONDS = 86400f;

        #endregion

        #region Inspector Config

        [Header("-- BOSS 引用 --")]
        [Tooltip("关联的 BossAI 组件，为空则自动查找。")]
        public BossAI bossAI;

        [Tooltip("关联的 BossDef，为空则从 BossAI 读取。")]
        public BossDef bossDef;

        [Header("-- 记仇参数 (004-GRUDGE-02) --")]
        [Tooltip("逃跑增加的记仇值")]
        public int grudgeEscapeAmount = 1;

        [Tooltip("反悔（外交期间攻击）增加的记仇值")]
        public int grudgeRenegeAmount = 2;

        [Tooltip("击杀同族生物增加的记仇值")]
        public int grudgeKillSameSpeciesAmount = 3;

        [Tooltip("记仇自然衰减所需天数 (004-DECAY-02)")]
        public int decayDays = 30;

        [Tooltip("每次衰减降低的记仇等级数")]
        public int decayAmount = 1;

        [Header("-- 撤退参数 (004-RETREAT-02) --")]
        [Tooltip("撤退后区域怪物攻击性增加百分比 (0.20 = +20%)")]
        public float retreatAggressionIncrease = 0.20f;

        [Tooltip("撤退后BOSS重置的等待时间（秒，播放撤退对话用）")]
        public float retreatResetDelay = 3f;

        [Header("-- 区域与同族 (004-SPECIES-01) --")]
        [Tooltip("BOSS所在区域ID，用于衰减判定和怪物攻击性增益。")]
        public string regionId;

        [Tooltip("与BOSS同族的物种ID列表。击杀列表中任一物种会触发记仇+3。")]
        public string[] sameSpeciesIds;

        [Header("-- 调试 --")]
        public bool enableDebugLogs = true;

        #endregion

        #region Private State

        private BossGrudgeData _grudgeData;
        private bool _initialized;
        private string _bossNameCache = "BOSS";
        private Transform _cachedTransform;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _isRetreating;

        // 撤退协程引用
        private Coroutine _retreatCoroutine;

        #endregion

        #region Public Properties

        /// <summary>当前BOSS的记仇数据。null 表示未初始化。</summary>
        public BossGrudgeData GrudgeData => _grudgeData;

        /// <summary>当前记仇等级。未初始化时返回 None。</summary>
        public GrudgeLevel CurrentGrudgeLevel => _grudgeData?.level ?? GrudgeLevel.None;

        /// <summary>当前好感度 (0-100)。</summary>
        public int Favorability => _grudgeData?.Favorability ?? 50;

        /// <summary>是否正在撤退流程中。</summary>
        public bool IsRetreating => _isRetreating;

        /// <summary>系统是否已完成初始化。</summary>
        public bool IsReady => _initialized;

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
                Debug.LogError("[BossGrudgeSystem] BossDef 未配置，系统禁用。");
                enabled = false;
                return;
            }

            _bossNameCache = bossDef.displayName;
            _cachedTransform = transform;
            _originalPosition = _cachedTransform.position;
            _originalRotation = _cachedTransform.rotation;

            _initialized = true;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void Start()
        {
            if (!_initialized) return;

            // 获取或创建记仇数据（与 BossDiplomacy 共享 GrudgeManager）
            _grudgeData = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache);

            // 检查是否满足时间衰减条件
            CheckAndApplyDecay();

            // 更新最后遭遇时间
            _grudgeData.lastEncounterTime = Time.time;

            if (enableDebugLogs)
                DebugLog($"初始化完成。记仇等级: {GetLevelName(_grudgeData.level)} 好感: {_grudgeData.Favorability}");
        }

        private void Update()
        {
            if (!_initialized || !bossAI.IsAlive || _isRetreating) return;

            // 战斗中检查是否超出 leashRange（逃跑/撤退判定）
            if (bossAI.CurrentState == "Combat")
            {
                CheckLeashRange();
            }
        }

        #endregion

        #region Public API — 记仇触发 (004-GRUDGE-03)

        /// <summary>
        /// 记录逃跑行为：记仇+1。
        /// 由撤退系统自动调用，也可由 CombatSystem 在玩家逃离战斗时调用。
        /// </summary>
        public void RecordEscape()
        {
            if (!_initialized || _grudgeData == null) return;

            int oldLevel = (int)_grudgeData.level;
            GrudgeManager.IncreaseGrudge(bossDef.bossId, grudgeEscapeAmount, "玩家逃跑触怒BOSS");

            EventBus.Publish(new BossEscapeEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                GrudgeIncrease = grudgeEscapeAmount
            });

            DebugLog($"逃跑！记仇+{grudgeEscapeAmount} " +
                     $"[{oldLevel}→{(int)_grudgeData.level}]");
        }

        /// <summary>
        /// 记录反悔行为：记仇+2。
        /// 玩家在和平窗口期攻击或拒绝已接受的外交条件时调用。
        /// 注意：这与 BossDiplomacy.OnPlayerBetrayed() 不同，后者记仇+4。
        /// 这是更轻度的"反悔"（如拒绝已接受的条件而非直接攻击）。
        /// </summary>
        public void RecordReneged()
        {
            if (!_initialized || _grudgeData == null) return;

            int oldLevel = (int)_grudgeData.level;
            GrudgeManager.IncreaseGrudge(bossDef.bossId, grudgeRenegeAmount, "玩家反悔和约条件");

            EventBus.Publish(new BossRenegedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                GrudgeIncrease = grudgeRenegeAmount
            });

            DebugLog($"反悔！记仇+{grudgeRenegeAmount} " +
                     $"[{oldLevel}→{(int)_grudgeData.level}]");
        }

        /// <summary>
        /// 记录击杀同族行为：记仇+3。
        /// 玩家在BOSS区域内击杀与BOSS同族的生物时调用。
        /// </summary>
        /// <param name="speciesId">被击杀的物种ID。必须存在于 sameSpeciesIds 列表中。</param>
        /// <returns>true 表示物种匹配且记仇已增加；false 表示未匹配或未初始化。</returns>
        public bool RecordSameSpeciesKill(string speciesId)
        {
            if (!_initialized || _grudgeData == null || string.IsNullOrEmpty(speciesId))
                return false;

            // 检查是否属于同族列表
            bool isMatch = false;
            foreach (string id in sameSpeciesIds)
            {
                if (id == speciesId)
                {
                    isMatch = true;
                    break;
                }
            }

            if (!isMatch) return false;

            int oldLevel = (int)_grudgeData.level;
            GrudgeManager.IncreaseGrudge(bossDef.bossId, grudgeKillSameSpeciesAmount, $"击杀同族({speciesId})");

            EventBus.Publish(new BossSameSpeciesKillEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                SpeciesId = speciesId,
                GrudgeIncrease = grudgeKillSameSpeciesAmount
            });

            DebugLog($"击杀同族 [{speciesId}]！记仇+{grudgeKillSameSpeciesAmount} " +
                     $"[{oldLevel}→{(int)_grudgeData.level}]");
            return true;
        }

        #endregion

        #region Public API — 衰减检测 (004-DECAY-03)

        /// <summary>
        /// 检查是否满足时间衰减条件。
        /// 如果距离上次遭遇超过 decayDays 天，降低记仇等级。
        /// 在 Start() 和区域入口处调用。
        /// </summary>
        public void CheckAndApplyDecay()
        {
            if (_grudgeData == null || _grudgeData.level == GrudgeLevel.None)
                return;

            float currentTime = Time.time;
            float elapsed = currentTime - _grudgeData.lastEncounterTime;

            // 计算经过的天数
            float daysPassed = elapsed / REAL_DAY_IN_SECONDS;

            if (daysPassed < decayDays)
            {
                if (enableDebugLogs)
                    DebugLog($"衰减检查: 经过 {daysPassed:F1} 天，未达阈值 {decayDays} 天。");
                return;
            }

            int oldLevel = (int)_grudgeData.level;

            // 每次衰减降低1级
            GrudgeManager.DecreaseGrudge(bossDef.bossId, decayAmount,
                $"超过 {decayDays} 天未进入 [{regionId}] 区域");

            int newLevel = (int)_grudgeData.level;

            EventBus.Publish(new BossGrudgeDecayedEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                OldLevel = oldLevel,
                NewLevel = newLevel,
                DaysSinceLastEntry = daysPassed
            });

            DebugLog($"记仇自然衰减: {GetLevelName((GrudgeLevel)oldLevel)}→{GetLevelName((GrudgeLevel)newLevel)} " +
                     $"(超过 {decayDays} 天未进入区域 [{regionId}])");
        }

        /// <summary>
        /// 在玩家进入BOSS区域时调用，更新最后遭遇时间并检查衰减。
        /// 由 AreaTrigger 或 RegionSystem 在玩家进入区域时调用。
        /// </summary>
        public void OnPlayerEnterArea()
        {
            if (!_initialized || _grudgeData == null) return;

            CheckAndApplyDecay();

            // 无论是否衰减，更新最后遭遇时间
            _grudgeData.lastEncounterTime = Time.time;

            DebugLog($"玩家进入区域 [{regionId}]，更新遭遇时间。");
        }

        #endregion

        #region Public API — 撤退处理 (004-RETREAT-03)

        /// <summary>
        /// 手动触发撤退流程。可由外部系统（如区域管理器）调用。
        /// </summary>
        public void TriggerRetreat()
        {
            if (_isRetreating || !_initialized) return;

            if (_retreatCoroutine != null)
                StopCoroutine(_retreatCoroutine);

            _retreatCoroutine = StartCoroutine(RetreatResetRoutine());
        }

        #endregion

        #region Internal — Leash 检查

        /// <summary>
        /// 检查玩家是否超出 leashRange。
        /// 如果在战斗中且超出距离，自动触发撤退流程。
        /// </summary>
        private void CheckLeashRange()
        {
            if (bossDef == null) return;

            float distance = GetPlayerDistance();
            if (distance < 0f) return; // 找不到玩家

            if (distance > bossDef.leashRange)
            {
                DebugLog($"玩家超出 leash 范围 ({distance:F1} > {bossDef.leashRange})。触发撤退。");
                TriggerRetreat();
            }
        }

        /// <summary>
        /// 撤退重置协程。
        /// 1. 播放撤退对话
        /// 2. 记录逃跑记仇
        /// 3. 重置BOSS位置和状态
        /// 4. 发布撤退事件（区域系统监听以增加怪物攻击性）
        /// </summary>
        private IEnumerator RetreatResetRoutine()
        {
            if (_isRetreating) yield break;
            _isRetreating = true;

            DebugLog($"=== 撤退流程开始 ===");

            // 1. 播放撤退对话
            EventBus.Publish(new BossDialogueEvent
            {
                Speaker = _bossNameCache,
                Line = bossDef.retreatDialogue,
                DisplayDuration = 2f
            });

            // 2. 记录逃跑记仇
            RecordEscape();

            yield return new WaitForSeconds(retreatResetDelay);

            // 3. 重置BOSS位置和状态
            _cachedTransform.position = _originalPosition;
            _cachedTransform.rotation = _originalRotation;

            if (bossAI != null)
            {
                bossAI.InitializeBoss();
            }

            _isRetreating = false;

            // 4. 发布撤退事件（区域系统监听以增加怪物攻击性）
            EventBus.Publish(new BossRetreatEvent
            {
                BossId = bossDef.bossId,
                BossName = _bossNameCache,
                AggressionIncrease = retreatAggressionIncrease,
                RegionId = regionId
            });

            DebugLog($"撤退完成。BOSS已重置。区域 [{regionId}] 怪物攻击性 +{retreatAggressionIncrease * 100}%");

            _retreatCoroutine = null;
        }

        #endregion

        #region EventBus Handlers

        /// <summary>
        /// BOSS被击杀 → 记仇清零 (004-RESET-02)。
        /// </summary>
        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            if (evt.BossId != bossDef?.bossId) return;

            if (_grudgeData != null && _grudgeData.level != GrudgeLevel.None)
            {
                GrudgeLevel oldLevel = _grudgeData.level;
                GrudgeManager.ResetGrudge(bossDef.bossId);
                _grudgeData = GrudgeManager.GetOrCreate(bossDef.bossId, _bossNameCache);

                DebugLog($"BOSS被击杀！记仇等级 {GetLevelName(oldLevel)} → {GetLevelName(_grudgeData.level)} (清零)");
            }
        }

        #endregion

        #region Tools

        /// <summary>
        /// 获取玩家与BOSS的距离。
        /// 通过标签查找"Player"对象；-1 表示找不到玩家。
        /// </summary>
        private float GetPlayerDistance()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return -1f;
            return Vector3.Distance(_cachedTransform.position, player.transform.position);
        }

        /// <summary>
        /// 获取记仇等级的中文描述。
        /// </summary>
        public static string GetLevelName(GrudgeLevel level)
        {
            return level switch
            {
                GrudgeLevel.None     => "正常",
                GrudgeLevel.Annoyed  => "警惕",
                GrudgeLevel.Angry    => "记恨",
                GrudgeLevel.Furious  => "仇恨",
                GrudgeLevel.Vengeful => "宿敌",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取完整的调试状态字符串。
        /// </summary>
        public string GetDebugStatus()
        {
            if (!_initialized)
                return "[BossGrudgeSystem] 未初始化";

            string grudgeInfo = _grudgeData != null
                ? $"等级: {GetLevelName(_grudgeData.level)} ({_grudgeData.level})\n" +
                  $"好感: {_grudgeData.Favorability}\n" +
                  $"背叛: {_grudgeData.betrayalCount}次\n" +
                  $"上次遭遇: {(Time.time - _grudgeData.lastEncounterTime) / REAL_DAY_IN_SECONDS:F1} 天前"
                : "无记仇数据";

            return $"=== BOSS记仇系统: {_bossNameCache} ===\n" +
                   $"{grudgeInfo}\n" +
                   $"区域: {regionId} | 同族物种: {(sameSpeciesIds.Length > 0 ? string.Join(", ", sameSpeciesIds) : "无")}\n" +
                   $"来袭参数: 逃跑+{grudgeEscapeAmount} / 反悔+{grudgeRenegeAmount} / 同族击杀+{grudgeKillSameSpeciesAmount}\n" +
                   $"衰减: {decayDays}天降{decayAmount}级 | 撤退中: {_isRetreating}";
        }

        #endregion

        #region Debug

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[BossGrudgeSystem] {_bossNameCache}: {message}");
        }

        [ContextMenu("Debug: 打印状态")]
        private void DebugPrintStatus()
        {
            Debug.Log(GetDebugStatus());
        }

        [ContextMenu("Debug: 模拟逃跑 (+1)")]
        private void DebugEscape()
        {
            if (!Application.isPlaying) return;
            RecordEscape();
        }

        [ContextMenu("Debug: 模拟反悔 (+2)")]
        private void DebugReneged()
        {
            if (!Application.isPlaying) return;
            RecordReneged();
        }

        [ContextMenu("Debug: 模拟击杀同族 (+3)")]
        private void DebugSameSpeciesKill()
        {
            if (!Application.isPlaying) return;
            if (sameSpeciesIds.Length > 0)
                RecordSameSpeciesKill(sameSpeciesIds[0]);
        }

        [ContextMenu("Debug: 检查衰减")]
        private void DebugCheckDecay()
        {
            if (!Application.isPlaying) return;
            CheckAndApplyDecay();
        }

        [ContextMenu("Debug: 强制撤退")]
        private void DebugForceRetreat()
        {
            if (!Application.isPlaying) return;
            TriggerRetreat();
        }

        [ContextMenu("Debug: 模拟进入区域")]
        private void DebugEnterArea()
        {
            if (!Application.isPlaying) return;
            OnPlayerEnterArea();
        }

        #endregion
    }
}
