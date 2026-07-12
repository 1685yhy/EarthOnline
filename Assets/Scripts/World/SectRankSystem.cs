using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Contribution Enums ──────────────────────────────────────────────

    /// <summary>Ways a player can earn sect contribution.</summary>
    public enum ContributionSource
    {
        Quest,        // 门派任务 — complete sect daily quests
        Tribulation,  // 上缴物资 — donate items / materials to the sect treasury
        Combat,       // 参战 — participate in sect wars / defense battles
        Mentoring,    // 指点 — teach lower-rank disciples
    }

    /// <summary>Ways a player can spend sect contribution.</summary>
    public enum ContributionSpendType
    {
        Technique,    // 功法 — exchange for cultivation techniques / manuals
        Pill,         // 丹药 — exchange for alchemy pills / elixirs
        Cave,         // 洞府 — rent a cultivation cave dwelling
        Protection,   // 庇护 — request sect protection or backing
    }

    /// <summary>Result of a promotion attempt.</summary>
    public enum PromotionResult
    {
        Success,
        ContributionTooLow,
        RealmTooLow,
        RequiresBattleExam,      // 2→3 needs battle + written exam
        RequiresSectQuest,       // 3→4 needs sect-level major quest
        RequiresSpecialEvent,    // 4→5 needs special event (leader abdication / death / abdication rite)
        MaxRankReached,
        NotInSect,
    }

    /// <summary>Result of spending contribution.</summary>
    public enum SpendResult
    {
        Success,
        InsufficientContribution,
        NotInSect,
    }

    // ─── Config Data ────────────────────────────────────────────────────

    /// <summary>Per-rank threshold and privilege configuration (tunable in Inspector).</summary>
    [Serializable]
    public class RankConfig
    {
        public SectRank Rank;
        public string DisplayName;
        [Tooltip("Minimum contribution needed to be eligible for this rank.")]
        public int ContributionThreshold;
        [Tooltip("Minimum numeric realm level required for this rank (0 = no requirement).")]
        public int RequiredRealmLevel;
        [Tooltip("藏经阁 — max accessible floor count.")]
        public int LibraryFloorAccess = 1;
        [Tooltip("商店折扣 multiplier (1.0 = full price, 0.9 = 10% off).")]
        [Range(0f, 1f)]
        public float ShopDiscount = 1.0f;
        [Tooltip("Does this rank have voting rights in sect decisions?")]
        public bool HasVotingRights;
        [TextArea(1, 2)]
        public string PromotionDescription;
    }

    /// <summary>Per-player daily activity record (transient, save-system managed).</summary>
    [Serializable]
    public class PlayerDailyRecord
    {
        public string PlayerId;
        public int QuestsCompletedToday;
        public string LastActiveDate;   // "yyyy-MM-dd" format
        public int ConsecutiveIdleDays;
    }

    // ─── EventBus Event Data ───────────────────────────────────────────

    /// <summary>Published when contribution is gained from any source.</summary>
    public struct ContributionGainedEvent
    {
        public string PlayerId;
        public ContributionSource Source;
        public int Amount;
        public int Total;
        public string Detail;
    }

    /// <summary>Published when contribution is spent.</summary>
    public struct ContributionSpentEvent
    {
        public string PlayerId;
        public ContributionSpendType SpendType;
        public int Amount;
        public int Remaining;
        public string Detail;
    }

    /// <summary>Published on successful rank promotion.</summary>
    public struct RankPromotedEvent
    {
        public string PlayerId;
        public SectRank PreviousRank;
        public SectRank NewRank;
    }

    /// <summary>Published when idle penalty is applied.</summary>
    public struct IdlePenaltyAppliedEvent
    {
        public string PlayerId;
        public int Penalty;
        public int ConsecutiveIdleDays;
    }

    /// <summary>Published when a promotion exam result is recorded.</summary>
    public struct PromotionExamEvent
    {
        public string PlayerId;
        /// <summary>"battle_written" | "sect_quest" | "special_event"</summary>
        public string ExamType;
        public bool Passed;
        public string Detail;
    }

    // ─── Sect Rank System ──────────────────────────────────────────────

    /// <summary>
    /// Manages the contribution & promotion lifecycle inside a formal sect.
    ///
    /// Responsibilities:
    ///   4 contribution gain pathways:  Quest / Tribulation / Combat / Mentoring
    ///   4 contribution spend pathways: Technique / Pill / Cave / Protection
    ///   5-rank ladder:  OuterDisciple → InnerDisciple → CoreDisciple → Elder → Leader
    ///   Rank privileges: library floors, shop discount, voting rights
    ///   Promotion exams: 2→3 (battle+written), 3→4 (sect quest), 4→5 (special event)
    ///   Daily quest cap (5) and idle penalty (-5/day after 7 consecutive idle days)
    ///
    /// Depends on <see cref="SectManager"/> for player-sect-state access.
    ///
    /// NOTE: The contribution economy handles only the currency side. Systems
    /// that exchange items or services (e.g. granting a technique manual after
    /// spending contribution on Technique) are the caller's responsibility.
    /// </summary>
    public class SectRankSystem : MonoBehaviour
    {
        // ─── Singleton ─────────────────────────────────────────────────

        public static SectRankSystem Instance { get; private set; }

        // ─── Serialized Configuration ──────────────────────────────────

        [Header("Contribution Thresholds")]
        [SerializeField, Tooltip("Contribution needed to reach InnerDisciple (rank 2).")]
        private int _innerThreshold = 200;
        [SerializeField, Tooltip("Contribution needed to reach CoreDisciple (rank 3).")]
        private int _coreThreshold = 500;
        [SerializeField, Tooltip("Contribution needed to reach Elder (rank 4).")]
        private int _elderThreshold = 2000;

        [Header("Realm Requirements per Rank")]
        [SerializeField, Tooltip("Minimum realm level for InnerDisciple.")]
        private int _innerRealm = 3;
        [SerializeField, Tooltip("Minimum realm level for CoreDisciple.")]
        private int _coreRealm = 6;
        [SerializeField, Tooltip("Minimum realm level for Elder.")]
        private int _elderRealm = 9;
        [SerializeField, Tooltip("Minimum realm level for Leader.")]
        private int _leaderRealm = 12;

        [Header("Daily Limits & Idle Penalty")]
        [SerializeField, Tooltip("Max sect daily quests a player can complete per day.")]
        private int _maxDailyQuests = 5;
        [SerializeField, Tooltip("Consecutive idle days before the daily penalty kicks in.")]
        private int _idleThresholdDays = 7;
        [SerializeField, Tooltip("Daily contribution penalty when idle beyond the threshold.")]
        private int _idleDailyPenalty = 5;

        // ─── Runtime State ─────────────────────────────────────────────

        private Dictionary<string, PlayerDailyRecord> _dailyRecords = new Dictionary<string, PlayerDailyRecord>();
        private string _todayDate; // cached "yyyy-MM-dd"

        // ─── Default Rank Configs ──────────────────────────────────────

        private static readonly Dictionary<SectRank, RankConfig> RankConfigs = new Dictionary<SectRank, RankConfig>
        {
            { SectRank.OuterDisciple, new RankConfig
            {
                Rank = SectRank.OuterDisciple,
                DisplayName = "外门弟子",
                ContributionThreshold = "0",
                RequiredRealmLevel = "0",
                LibraryFloorAccess = "1",
                ShopDiscount = "1.0f",
                HasVotingRights = "false",
                PromotionDescription = "初始身份。积累 200 贡献可晋升内门弟子。",
            }},
            { SectRank.InnerDisciple, new RankConfig
            {
                Rank = SectRank.InnerDisciple,
                DisplayName = "内门弟子",
                ContributionThreshold = "200",
                RequiredRealmLevel = "3",
                LibraryFloorAccess = "2",
                ShopDiscount = "1.0f",
                HasVotingRights = "false",
                PromotionDescription = "通过【战斗+笔试考核】可晋升核心弟子。",
            }},
            { SectRank.CoreDisciple, new RankConfig
            {
                Rank = SectRank.CoreDisciple,
                DisplayName = "核心弟子",
                ContributionThreshold = "500",
                RequiredRealmLevel = "6",
                LibraryFloorAccess = "3",
                ShopDiscount = "0.9f",
                HasVotingRights = "false",
                PromotionDescription = "完成【门派级大任务】可晋升长老。",
            }},
            { SectRank.Elder, new RankConfig
            {
                Rank = SectRank.Elder,
                DisplayName = "长老",
                ContributionThreshold = "2000",
                RequiredRealmLevel = "9",
                LibraryFloorAccess = "4",
                ShopDiscount = "0.8f",
                HasVotingRights = "true",
                PromotionDescription = "参与【特殊事件】（掌门退位/战死/禅让）可成为掌门。",
            }},
            { SectRank.Leader, new RankConfig
            {
                Rank = SectRank.Leader,
                DisplayName = "掌门",
                ContributionThreshold = "2000",
                RequiredRealmLevel = "12",
                LibraryFloorAccess = "5",
                ShopDiscount = "0.7f",
                HasVotingRights = "true",
                PromotionDescription = "门派最高领袖。",
            }},
        };

        // ─── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _todayDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        // ─── Config Accessors ───────────────────────────────────────────

        /// <summary>Get the static config for a given rank.</summary>
        public RankConfig GetRankConfig(SectRank rank)
        {
            RankConfigs.TryGetValue(rank, out var config);
            return config;
        }

        /// <summary>Get the contribution threshold needed for a target rank.</summary>
        public int GetThresholdForRank(SectRank targetRank)
        {
            return targetRank switch
            {
                SectRank.InnerDisciple => _innerThreshold,
                SectRank.CoreDisciple  => _coreThreshold,
                SectRank.Elder         => _elderThreshold,
                SectRank.Leader        => _elderThreshold, // contribution same as Elder; needs special event
                _                      => 0,
            };
        }

        /// <summary>Get the numeric realm level required for a target rank.</summary>
        public int GetRequiredRealm(SectRank targetRank)
        {
            return targetRank switch
            {
                SectRank.InnerDisciple => _innerRealm,
                SectRank.CoreDisciple  => _coreRealm,
                SectRank.Elder         => _elderRealm,
                SectRank.Leader        => _leaderRealm,
                _                      => 0,
            };
        }

        // ─── Privilege Queries ──────────────────────────────────────────

        /// <summary>
        /// Get the player's effective shop discount multiplier.
        /// (1.0 = full price, 0.9 = 10% off, etc.)
        /// </summary>
        public float GetShopDiscount(string playerId)
        {
            var config = GetRankConfig(SectManager.Instance.GetRank(playerId));
            return config != null ? config.ShopDiscount : 1.0f;
        }

        /// <summary>Get the max library (藏经阁) floor the player can access.</summary>
        public int GetLibraryFloorAccess(string playerId)
        {
            var config = GetRankConfig(SectManager.Instance.GetRank(playerId));
            return config != null ? config.LibraryFloorAccess : 1;
        }

        /// <summary>Does the player have voting rights in their sect?</summary>
        public bool HasVotingRights(string playerId)
        {
            var config = GetRankConfig(SectManager.Instance.GetRank(playerId));
            return config != null && config.HasVotingRights;
        }

        // ─── Public API: Add Contribution ──────────────────────────────

        /// <summary>
        /// Add contribution from a specific source.
        /// Quest source is subject to the daily limit (<see cref="_maxDailyQuests"/>).
        /// </summary>
        /// <returns>The new total contribution, or -1 if the player is not in a formal sect.</returns>
        public int AddContribution(string playerId, ContributionSource source, int amount, string detail = "")
        {
            var manager = SectManager.Instance;
            if (!manager.IsInFormalSect(playerId))
            {
                Debug.LogWarning($"[SectRankSystem] {playerId} 不在正式门派中，无法增加贡献。");
                return -1;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"[SectRankSystem] AddContribution called with non-positive amount {amount} for {playerId}.");
                return manager.GetContribution(playerId);
            }

            // ── Daily quest cap ──
            if (source == ContributionSource.Quest)
            {
                var record = EnsureDailyRecord(playerId);
                if (record.QuestsCompletedToday >= _maxDailyQuests)
                {
                    Debug.Log($"[SectRankSystem] 每日任务上限: {playerId} 今日已完成 {_maxDailyQuests}/{_maxDailyQuests}。");
                    return manager.GetContribution(playerId);
                }
                record.QuestsCompletedToday++;
            }

            // Mark activity (resets idle counter)
            RecordActivity(playerId);

            manager.ModifyContribution(playerId, amount);
            int total = manager.GetContribution(playerId);

            string sourceName = GetSourceDisplayName(source);
            Debug.Log($"[SectRankSystem] 贡献获取: {playerId} {sourceName} +{amount} (当前 {total})");

            EventBus.Publish(new ContributionGainedEvent
            {
                PlayerId = playerId,
                Source = source,
                Amount = amount,
                Total = total,
                Detail = string.IsNullOrEmpty(detail) ? $"{sourceName} +{amount}" : detail,
            });

            return total;
        }

        // ─── Public API: Spend Contribution ────────────────────────────

        /// <summary>
        /// Spend contribution. Checks sufficient balance and applies the player's
        /// rank discount before deducting.
        /// </summary>
        /// <returns><see cref="SpendResult"/> indicating success or failure.</returns>
        public SpendResult SpendContribution(string playerId, ContributionSpendType spendType, int baseCost, string detail = "")
        {
            var manager = SectManager.Instance;
            if (!manager.IsInFormalSect(playerId))
            {
                Debug.LogWarning($"[SectRankSystem] {playerId} 不在门派中，无法消耗贡献。");
                return SpendResult.NotInSect;
            }

            if (baseCost <= 0)
            {
                Debug.LogWarning($"[SectRankSystem] SpendContribution called with non-positive cost {baseCost} for {playerId}.");
                return SpendResult.Success; // nothing to spend
            }

            int current = manager.GetContribution(playerId);

            // Apply rank discount before checking affordability
            float discount = GetShopDiscount(playerId);
            int actualCost = Mathf.RoundToInt(baseCost * discount);
            if (actualCost < 1) actualCost = 1;

            if (current < actualCost)
            {
                Debug.Log($"[SectRankSystem] 贡献不足: {playerId} 需要 {actualCost}，当前 {current}。");
                return SpendResult.InsufficientContribution;
            }

            // Deduct (ModifyContribution returns false if the player gets expelled)
            manager.ModifyContribution(playerId, -actualCost);
            int remaining = manager.GetContribution(playerId);

            string typeName = GetSpendTypeDisplayName(spendType);
            Debug.Log($"[SectRankSystem] 贡献消耗: {playerId} {typeName} -{actualCost} (原价 {baseCost}，折扣 {discount:P0}) (剩余 {remaining})");

            EventBus.Publish(new ContributionSpentEvent
            {
                PlayerId = playerId,
                SpendType = spendType,
                Amount = actualCost,
                Remaining = remaining,
                Detail = string.IsNullOrEmpty(detail) ? $"{typeName} {actualCost}贡献" : detail,
            });

            return SpendResult.Success;
        }

        // ─── Public API: Promotion ──────────────────────────────────────

        /// <summary>
        /// Check promotion prerequisites and return what is needed next.
        ///
        /// Rules per rank transition:
        ///   1→2 (Outer → Inner) : contribution + realm check; auto-promote
        ///   2→3 (Inner → Core)  : contribution + realm + battle+written exam
        ///   3→4 (Core → Elder)  : contribution + realm + sect-level major quest
        ///   4→5 (Elder → Leader): contribution + realm + special event
        /// </summary>
        /// <param name="playerId">Target player.</param>
        /// <param name="playerRealmLevel">The player's current cultivation realm (numeric).</param>
        /// <returns>
        /// <see cref="PromotionResult.Success"/> if auto-promotion succeeded (1→2 only).
        /// <see cref="PromotionResult.RequiresBattleExam"/>, RequiresSectQuest,
        /// or RequiresSpecialEvent if an exam is blocking the promotion.
        /// Error codes if prerequisites are not met.
        /// </returns>
        public PromotionResult TryPromote(string playerId, int playerRealmLevel)
        {
            var manager = SectManager.Instance;
            if (!manager.IsInFormalSect(playerId))
                return PromotionResult.NotInSect;

            SectRank currentRank = manager.GetRank(playerId);
            SectRank nextRank = currentRank + 1;

            if (nextRank > SectRank.Leader)
                return PromotionResult.MaxRankReached;

            // ── Contribution check ──
            int threshold = GetThresholdForRank(nextRank);
            int contribution = manager.GetContribution(playerId);
            if (contribution < threshold)
                return PromotionResult.ContributionTooLow;

            // ── Realm check ──
            int requiredRealm = GetRequiredRealm(nextRank);
            if (playerRealmLevel < requiredRealm)
                return PromotionResult.RealmTooLow;

            // ── Determine exam gate ──
            // 1→2 (Outer→Inner) auto-promotes; no exam needed
            if (currentRank == SectRank.OuterDisciple)
            {
                return AutoPromote(playerId, currentRank, nextRank);
            }

            return currentRank switch
            {
                SectRank.InnerDisciple => PromotionResult.RequiresBattleExam,    // 2→3
                SectRank.CoreDisciple  => PromotionResult.RequiresSectQuest,     // 3→4
                SectRank.Elder         => PromotionResult.RequiresSpecialEvent,   // 4→5
                _                      => PromotionResult.Unknown,
            };
        }

        /// <summary>
        /// Complete a promotion exam and, if passed, promote the player.
        /// </summary>
        /// <param name="examType">
        /// One of: "battle_written" (2→3), "sect_quest" (3→4), "special_event" (4→5).
        /// </param>
        public PromotionResult CompletePromotionExam(
            string playerId,
            int playerRealmLevel,
            string examType,
            bool passed,
            string detail = "")
        {
            var manager = SectManager.Instance;
            if (!manager.IsInFormalSect(playerId))
                return PromotionResult.NotInSect;

            SectRank currentRank = manager.GetRank(playerId);
            SectRank nextRank = currentRank + 1;

            if (nextRank > SectRank.Leader)
                return PromotionResult.MaxRankReached;

            // ── Re-check prerequisites even on pass ──
            if (passed)
            {
                int threshold = GetThresholdForRank(nextRank);
                int contribution = manager.GetContribution(playerId);
                if (contribution < threshold)
                    return PromotionResult.ContributionTooLow;

                int requiredRealm = GetRequiredRealm(nextRank);
                if (playerRealmLevel < requiredRealm)
                    return PromotionResult.RealmTooLow;

                // Promote
                var state = manager.GetPlayerState(playerId);
                if (state == null)
                    return PromotionResult.NotInSect;

                SectRank previousRank = state.Rank;
                state.Rank = nextRank;

                var prevConfig = GetRankConfig(previousRank);
                var nextConfig = GetRankConfig(nextRank);
                Debug.Log($"[SectRankSystem] 晋升成功: {playerId} {prevConfig?.DisplayName} → {nextConfig?.DisplayName}");

                EventBus.Publish(new PromotionExamEvent
                {
                    PlayerId = playerId,
                    ExamType = examType,
                    Passed = "true",
                    Detail = string.IsNullOrEmpty(detail)
                        ? $"考核通过！晋升为【{nextConfig?.DisplayName}】！"
                        : detail,
                });

                EventBus.Publish(new RankPromotedEvent
                {
                    PlayerId = playerId,
                    PreviousRank = previousRank,
                    NewRank = nextRank,
                });

                return PromotionResult.Success;
            }
            else
            {
                // Publish failure event
                EventBus.Publish(new PromotionExamEvent
                {
                    PlayerId = playerId,
                    ExamType = examType,
                    Passed = "false",
                    Detail = string.IsNullOrEmpty(detail) ? "考核未通过，请继续积累实力。" : detail,
                });

                Debug.Log($"[SectRankSystem] 考核未通过: {playerId} {examType}");
                return PromotionResult.ContributionTooLow; // generic "not passed"
            }
        }

        // ─── Public API: Daily Activity ─────────────────────────────────

        /// <summary>Get the number of sect quests the player has completed today.</summary>
        public int GetDailyQuestCount(string playerId)
        {
            if (!_dailyRecords.TryGetValue(playerId, out var record))
                return 0;
            EnsureDateFreshness(record);
            return record.QuestsCompletedToday;
        }

        /// <summary>Get the number of sect quests the player can still complete today.</summary>
        public int GetRemainingDailyQuests(string playerId)
        {
            return Mathf.Max(0, _maxDailyQuests - GetDailyQuestCount(playerId));
        }

        /// <summary>Is the player idle (7+ consecutive days with no sect activity)?</summary>
        public bool IsPlayerIdle(string playerId)
        {
            if (!_dailyRecords.TryGetValue(playerId, out var record))
                return false;
            return CalculateIdleDays(record.LastActiveDate) >= _idleThresholdDays;
        }

        /// <summary>Get the player's consecutive idle day count.</summary>
        public int GetConsecutiveIdleDays(string playerId)
        {
            if (!_dailyRecords.TryGetValue(playerId, out var record))
                return 0;
            return CalculateIdleDays(record.LastActiveDate);
        }

        // ─── Public API: Day Tick ───────────────────────────────────────

        /// <summary>
        /// Called by the game's time / calendar system when a new in-game day begins.
        /// Resets daily quest counters and applies idle penalties to all sect members.
        /// </summary>
        public void OnNewDay()
        {
            _todayDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Reset stale daily counters
            foreach (var record in _dailyRecords.Values)
            {
                if (record.LastActiveDate != _todayDate)
                {
                    record.QuestsCompletedToday = 0;
                }
            }

            // Apply idle penalties
            ApplyIdlePenalties();
        }

        // ─── Public API: Save System ────────────────────────────────────

        /// <summary>Restore a player's daily record from save data.</summary>
        public void RestoreDailyRecord(PlayerDailyRecord record)
        {
            if (record != null && !string.IsNullOrEmpty(record.PlayerId))
                _dailyRecords[record.PlayerId] = record;
        }

        /// <summary>Get a player's daily record for serialization.</summary>
        public PlayerDailyRecord GetDailyRecord(string playerId)
        {
            _dailyRecords.TryGetValue(playerId, out var record);
            return record;
        }

        // ─── Internal: Promotion Helpers ───────────────────────────────

        /// <summary>Auto-promote OuterDisciple -> InnerDisciple with no exam gate.</summary>
        private PromotionResult AutoPromote(string playerId, SectRank currentRank, SectRank nextRank)
        {
            var manager = SectManager.Instance;
            var state = manager.GetPlayerState(playerId);
            if (state == null)
                return PromotionResult.NotInSect;

            state.Rank = nextRank;

            var prevConfig = GetRankConfig(currentRank);
            var nextConfig = GetRankConfig(nextRank);
            Debug.Log($"[SectRankSystem] 自动晋升: {playerId} {prevConfig?.DisplayName} → {nextConfig?.DisplayName}");

            EventBus.Publish(new RankPromotedEvent
            {
                PlayerId = playerId,
                PreviousRank = currentRank,
                NewRank = nextRank,
            });

            return PromotionResult.Success;
        }

        // ─── Internal: Display Names ────────────────────────────────────

        private string GetSourceDisplayName(ContributionSource source)
        {
            return source switch
            {
                ContributionSource.Quest       => "门派任务",
                ContributionSource.Tribulation => "上缴物资",
                ContributionSource.Combat      => "参战",
                ContributionSource.Mentoring   => "指点弟子",
                _                              => "未知",
            };
        }

        private string GetSpendTypeDisplayName(ContributionSpendType type)
        {
            return type switch
            {
                ContributionSpendType.Technique   => "功法兑换",
                ContributionSpendType.Pill        => "丹药兑换",
                ContributionSpendType.Cave        => "洞府租赁",
                ContributionSpendType.Protection  => "庇护申请",
                _                                 => "未知",
            };
        }

        // ─── Internal: Daily Record Management ────────────────────────

        private PlayerDailyRecord EnsureDailyRecord(string playerId)
        {
            if (!_dailyRecords.TryGetValue(playerId, out var record))
            {
                record = new PlayerDailyRecord
                {
                    PlayerId = playerId,
                    QuestsCompletedToday = "0",
                    LastActiveDate = _todayDate,
                    ConsecutiveIdleDays = "0",
                };
                _dailyRecords[playerId] = record;
            }
            EnsureDateFreshness(record);
            return record;
        }

        /// <summary>If the record's date is stale, reset the daily counter.</summary>
        private void EnsureDateFreshness(PlayerDailyRecord record)
        {
            if (record.LastActiveDate != _todayDate)
            {
                record.QuestsCompletedToday = 0;
            }
        }

        /// <summary>Mark the player as active today (resets idle counter).</summary>
        private void RecordActivity(string playerId)
        {
            var record = EnsureDailyRecord(playerId);

            if (record.LastActiveDate != _todayDate)
            {
                // Player was last active on a previous day — reset idle tracking
                record.ConsecutiveIdleDays = 0;
                record.LastActiveDate = _todayDate;
                record.QuestsCompletedToday = 0;
            }

            record.ConsecutiveIdleDays = 0;
        }

        /// <summary>
        /// Calculate the number of consecutive days the player has been idle
        /// (days since last activity that are fully past).
        /// </summary>
        private int CalculateIdleDays(string lastActiveDate)
        {
            if (string.IsNullOrEmpty(lastActiveDate))
                return 0;

            if (DateTime.TryParse(lastActiveDate, out var lastDate))
            {
                var today = DateTime.Now.Date;
                return Math.Max(0, (today - lastDate.Date).Days);
            }

            return 0;
        }

        // ─── Internal: Idle Penalty ─────────────────────────────────────

        /// <summary>
        /// Apply the idle daily penalty to all tracked players who have been
        /// idle for <see cref="_idleThresholdDays"/> or more consecutive days.
        /// </summary>
        private void ApplyIdlePenalties()
        {
            var manager = SectManager.Instance;

            foreach (var kvp in _dailyRecords)
            {
                string playerId = kvp.Key;
                var record = kvp.Value;

                // Only penalize current sect members
                if (!manager.IsInFormalSect(playerId))
                    continue;

                int idleDays = CalculateIdleDays(record.LastActiveDate);
                if (idleDays >= _idleThresholdDays)
                {
                    record.ConsecutiveIdleDays = idleDays;
                    manager.ModifyContribution(playerId, -_idleDailyPenalty);

                    Debug.Log($"[SectRankSystem] 怠惰惩罚: {playerId} 连续 {idleDays} 天未做任务，贡献 -{_idleDailyPenalty}。");

                    EventBus.Publish(new IdlePenaltyAppliedEvent
                    {
                        PlayerId = playerId,
                        Penalty = _idleDailyPenalty,
                        ConsecutiveIdleDays = idleDays,
                    });
                }
            }
        }
    }
}
