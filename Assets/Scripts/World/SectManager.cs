using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>The 5 sect types in the Spirit Continent. 散修联盟 is not a formal sect.</summary>
    public enum SectType
    {
        TianYuanZong,     // 天元宗 — orthodox swordsmanship
        QingYunMen,       // 青云门 — alchemy & herbalism
        ShangMeng,        // 商盟 — trade & commerce
        YuShouYiZu,       // 御兽遗族 — beast taming
        SanXiuLianMeng,   // 散修联盟 — rogue cultivator alliance (not formal)
    }

    /// <summary>Result of a join-sect attempt.</summary>
    public enum JoinResult
    {
        Success,
        RealmTooLow,
        ReputationTooLow,
        AlreadyInFormalSect,
        TrialFailed,
        OnLeaveCooldown,
        OnTrialCooldown,
        AlreadyInThisSect,
    }

    /// <summary>Result of a leave-sect attempt.</summary>
    public enum LeaveResult
    {
        Success,
        NotInAnySect,
        ContributionNegative,       // Cannot peaceful-leave with negative contribution
        IsBetrayalState,
        CannotLeaveSanctionAlliance, // 散修联盟 has no formal leave
    }

    /// <summary>Type of departure from a sect.</summary>
    public enum LeaveType
    {
        Peaceful,
        Forced,     // expelled by reaching -100 contribution
        Betrayal,   // reserved for Story 002
    }

    /// <summary>Internal rank levels within a formal sect (1-5).</summary>
    public enum SectRank
    {
        OuterDisciple = 1,  // 外门弟子
        InnerDisciple = 2,  // 内门弟子
        CoreDisciple = 3,   // 核心弟子/真传弟子
        Elder = 4,          // 长老
        Leader = 5,         // 掌门
    }

    // ─── Event Data (EventBus) ────────────────────────────────────────

    /// <summary>Published when a player successfully joins a sect.</summary>
    public struct SectJoinedEvent
    {
        public string PlayerId;
        public SectType Sect;
        public string TokenItemId;
    }

    /// <summary>Published when trial results are in (pass or fail).</summary>
    public struct SectTrialCompletedEvent
    {
        public string PlayerId;
        public SectType Sect;
        public bool Passed;
        /// <summary>Human-readable reason for pass/fail.</summary>
        public string Detail;
    }

    /// <summary>Published when a player leaves a sect (any leave type).</summary>
    public struct SectLeftEvent
    {
        public string PlayerId;
        public SectType PreviousSect;
        public LeaveType LeaveType;
        public int RetainedContribution;
    }

    /// <summary>Published when a player is expelled due to low contribution.</summary>
    public struct SectExpelledEvent
    {
        public string PlayerId;
        public SectType Sect;
        public int FinalContribution;
    }

    /// <summary>Published when a player enters or leaves the sanction alliance.</summary>
    public struct SanctionAllianceEvent
    {
        public string PlayerId;
        public bool Joined; // true=joined, false=left
    }

    // ─── Config Data Classes ──────────────────────────────────────────

    /// <summary>Configuration for one sect's join/leave rules.</summary>
    [Serializable]
    public class SectConfig
    {
        [Header("Identity")]
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public bool IsFormal = true;

        [Header("Join Requirements")]
        public int RequiredRealmLevel;          // numeric realm threshold
        public int RequiredReputation;          // minimum reputation with this sect
        [TextArea(1, 2)] public string ExtraConditionDesc;  // shown as trial description

        [Header("Cooldowns (days)")]
        public int TrialCooldownDays = 7;        // retry after failed trial
        public int LeaveCooldownDays = 7;        // rejoin after peaceful leave

        [Header("Leave Penalties")]
        public int PeacefulLeaveRepPenalty = 30;
        [Range(0f, 1f)] public float ContributionRetentionOnLeave = 0.5f;  // 50%
        public int ExpulsionContributionThreshold = -100;
    }

    // ─── Player-State Data ───────────────────────────────────────────

    /// <summary>Runtime player state for sect membership.</summary>
    [Serializable]
    public class PlayerSectState
    {
        public string PlayerId;
        public SectType? CurrentFormalSect;   // null = no formal sect
        public bool IsInSanctionAlliance = true; // 散修联盟 — default true
        public int Contribution;
        public SectRank Rank = SectRank.OuterDisciple;
        public double LeaveCooldownTimestamp;  // Unix timestamp (seconds)
        public double TrialCooldownTimestamp;  // Unix timestamp (seconds)
    }

    // ─── Sect Manager ─────────────────────────────────────────────────

    /// <summary>
    /// Manages sect join/leave lifecycle for EarthOnline.
    /// Handles requirement checks, trial flow, peaceful/forced leave,
    /// contribution tracking, and cooldowns.
    /// 
    /// Core invariants:
    /// - A player cannot be in two formal sects simultaneously.
    /// - 散修联盟 is not a formal sect — all players are members by default.
    /// - Contribution dropping to -100 triggers forced expulsion.
    /// </summary>
    public class SectManager : MonoBehaviour
    {
        // ─── Singleton ─────────────────────────────────────────────────

        public static SectManager Instance { get; private set; }

        // ─── Serialized Config Overrides ────────────────────────────────

        [Header("Sect Configurations")]
        [SerializeField] private bool _overrideDefaultConfigs;
        [SerializeField] private SectConfig[] _configOverrides;

        // ─── Runtime State ──────────────────────────────────────────────

        private Dictionary<string, PlayerSectState> _playerStates = new Dictionary<string, PlayerSectState>();

        // ─── Default Configuration ───────────────────────────────────────

        private static readonly Dictionary<SectType, SectConfig> DefaultConfigs = new Dictionary<SectType, SectConfig>
        {
            { SectType.TianYuanZong, new SectConfig {
                DisplayName = "天元宗",
                Description = "正道之首，以剑法和符箓见长。门规森严，讲究以正制邪。",
                IsFormal = true,
                RequiredRealmLevel = 5,       // 筑基期
                RequiredReputation = 0,
                ExtraConditionDesc = "通过入门考核（战斗试炼）",
                TrialCooldownDays = 7,
                LeaveCooldownDays = 7,
                PeacefulLeaveRepPenalty = 30,
                ContributionRetentionOnLeave = 0.5f,
                ExpulsionContributionThreshold = -100,
            }},
            { SectType.QingYunMen, new SectConfig {
                DisplayName = "青云门",
                Description = "以丹道和采药闻名于世。门人善用草木灵材，济世救人。",
                IsFormal = true,
                RequiredRealmLevel = 5,       // 筑基期
                RequiredReputation = 0,
                ExtraConditionDesc = "通过入门考核（采集+炼丹试炼）",
                TrialCooldownDays = 7,
                LeaveCooldownDays = 7,
                PeacefulLeaveRepPenalty = 30,
                ContributionRetentionOnLeave = 0.5f,
                ExpulsionContributionThreshold = -100,
            }},
            { SectType.ShangMeng, new SectConfig {
                DisplayName = "商盟",
                Description = "以经商贸易为核心的松散商会。灵石即力量，契约即法律。",
                IsFormal = true,
                RequiredRealmLevel = 3,       // 练气期
                RequiredReputation = 10,
                ExtraConditionDesc = "完成'投名状'任务（上缴一批指定材料）",
                TrialCooldownDays = 7,
                LeaveCooldownDays = 7,
                PeacefulLeaveRepPenalty = 30,
                ContributionRetentionOnLeave = 0.5f,
                ExpulsionContributionThreshold = -100,
            }},
            { SectType.YuShouYiZu, new SectConfig {
                DisplayName = "御兽遗族",
                Description = "上古御兽传承的遗族。与灵兽共生，以驯化和驾驭妖兽为荣。",
                IsFormal = true,
                RequiredRealmLevel = 3,       // 练气期
                RequiredReputation = 0,
                ExtraConditionDesc = "通过灵兽亲和测试",
                TrialCooldownDays = 7,
                LeaveCooldownDays = 7,
                PeacefulLeaveRepPenalty = 30,
                ContributionRetentionOnLeave = 0.5f,
                ExpulsionContributionThreshold = -100,
            }},
            { SectType.SanXiuLianMeng, new SectConfig {
                DisplayName = "散修联盟",
                Description = "松散互助组织，不设门槛。提供悬赏榜、坊市摊位、情报交换。",
                IsFormal = false,
                RequiredRealmLevel = 0,
                RequiredReputation = 0,
                ExtraConditionDesc = "默认身份，无需加入流程",
                TrialCooldownDays = 0,
                LeaveCooldownDays = 0,
                PeacefulLeaveRepPenalty = 0,
                ContributionRetentionOnLeave = 1.0f,
                ExpulsionContributionThreshold = -100,
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
        }

        // ─── Public API: Queries ───────────────────────────────────────

        /// <summary>Get the config for a sect (respects overrides).</summary>
        public SectConfig GetConfig(SectType type)
        {
            if (_overrideDefaultConfigs && _configOverrides != null)
            {
                for (int i = 0; i < _configOverrides.Length; i++)
                {
                    // Match by enum value assigned to the override slot
                }
            }
            return DefaultConfigs[type];
        }

        /// <summary>Check if a player is a member of any formal sect.</summary>
        public bool IsInFormalSect(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var s)
                && s.CurrentFormalSect.HasValue;
        }

        /// <summary>Get the player's current formal sect, if any.</summary>
        public SectType? GetCurrentSect(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var s)
                ? s.CurrentFormalSect
                : null;
        }

        /// <summary>Check if a player is in the sanction alliance.</summary>
        public bool IsInSanctionAlliance(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var s)
                && s.IsInSanctionAlliance;
        }

        /// <summary>Get the player's contribution in their current sect.</summary>
        public int GetContribution(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var s) ? s.Contribution : 0;
        }

        /// <summary>Get the player's current rank.</summary>
        public SectRank GetRank(string playerId)
        {
            return _playerStates.TryGetValue(playerId, out var s) ? s.Rank : SectRank.OuterDisciple;
        }

        /// <summary>Get full mutable state for serialization (save system).</summary>
        public PlayerSectState GetPlayerState(string playerId)
        {
            _playerStates.TryGetValue(playerId, out var state);
            return state;
        }

        /// <summary>Restore a saved player state (e.g., on load).</summary>
        public void RestorePlayerState(PlayerSectState state)
        {
            if (state != null)
                _playerStates[state.PlayerId] = state;
        }

        // ─── Public API: Join Flow ─────────────────────────────────────

        /// <summary>
        /// Check all join requirements and return a detailed list of missing items
        /// for UI display. Returns empty list if all requirements are met.
        /// </summary>
        public List<string> GetMissingRequirements(string playerId, SectType sect, int playerRealmLevel, int sectReputation)
        {
            var config = GetConfig(sect);
            var missing = new List<string>();

            // 散修联盟 has no requirements
            if (!config.IsFormal) return missing;

            // Realm check
            if (playerRealmLevel < config.RequiredRealmLevel)
            {
                missing.Add($"境界不足：需要达到【{config.RequiredRealmLevel}级】，当前 {playerRealmLevel} 级");
            }

            // Reputation check
            if (sectReputation < config.RequiredReputation)
            {
                missing.Add($"声望不足：需要【{config.DisplayName}声望 ≥ {config.RequiredReputation}】，当前 {sectReputation}");
            }

            // Extra condition display
            if (!string.IsNullOrEmpty(config.ExtraConditionDesc))
            {
                missing.Add($"额外考核：{config.ExtraConditionDesc}");
            }

            // Already in another formal sect
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                if (state.CurrentFormalSect.HasValue && state.CurrentFormalSect.Value != sect)
                {
                    missing.Add("你已加入其他正式门派，需先退出当前门派");
                }

                // Leave cooldown
                if (state.LeaveCooldownTimestamp > 0)
                {
                    var remaining = GetCooldownRemaining(state.LeaveCooldownTimestamp);
                    if (remaining > 0)
                        missing.Add($"退出冷却中：还需 {remaining:F1} 天才能重新加入");
                }

                // Trial cooldown
                if (state.TrialCooldownTimestamp > 0)
                {
                    var remaining = GetCooldownRemaining(state.TrialCooldownTimestamp);
                    if (remaining > 0)
                        missing.Add($"考核冷却中：还需 {remaining:F1} 天才能再次申请");
                }
            }

            return missing;
        }

        /// <summary>
        /// Check all requirements for joining a sect.
        /// Returns Success if the player can proceed to the trial phase.
        /// </summary>
        public JoinResult CheckJoinRequirements(string playerId, SectType sect, int playerRealmLevel, int sectReputation)
        {
            var config = GetConfig(sect);

            // 散修联盟: always joinable, no checks
            if (!config.IsFormal)
                return JoinResult.Success;

            // Already in THIS sect
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                if (state.CurrentFormalSect == sect)
                    return JoinResult.AlreadyInThisSect;

                // Already in another formal sect
                if (state.CurrentFormalSect.HasValue)
                    return JoinResult.AlreadyInFormalSect;

                // Leave cooldown
                if (state.LeaveCooldownTimestamp > 0 && GetCooldownRemaining(state.LeaveCooldownTimestamp) > 0)
                    return JoinResult.OnLeaveCooldown;

                // Trial cooldown
                if (state.TrialCooldownTimestamp > 0 && GetCooldownRemaining(state.TrialCooldownTimestamp) > 0)
                    return JoinResult.OnTrialCooldown;
            }

            // Realm check
            if (playerRealmLevel < config.RequiredRealmLevel)
                return JoinResult.RealmTooLow;

            // Reputation check
            if (sectReputation < config.RequiredReputation)
                return JoinResult.ReputationTooLow;

            return JoinResult.Success;
        }

        /// <summary>
        /// Initiate the join process. Checks requirements, and if met,
        /// returns Success — the calling code should then start the trial scene.
        /// For 散修联盟, this auto-joins immediately.
        /// </summary>
        public JoinResult RequestJoin(string playerId, SectType sect, int playerRealmLevel, int sectReputation)
        {
            var config = GetConfig(sect);

            // 散修联盟: automatic join, no conditions
            if (!config.IsFormal)
            {
                var state = EnsurePlayerState(playerId);
                state.IsInSanctionAlliance = true;
                EventBus.Publish(new SanctionAllianceEvent { PlayerId = playerId, Joined = true });
                return JoinResult.Success;
            }

            // Run standard checks
            var result = CheckJoinRequirements(playerId, sect, playerRealmLevel, sectReputation);
            if (result != JoinResult.Success)
                return result;

            // All checks passed → proceed to trial (called via OnTrialCompleted)
            return JoinResult.Success;
        }

        /// <summary>
        /// Called when the trial scene finishes. If passed, finalizes membership;
        /// if failed, sets trial cooldown.
        /// </summary>
        public void OnTrialCompleted(string playerId, SectType sect, bool passed)
        {
            var config = GetConfig(sect);

            if (passed)
            {
                FinalizeJoin(playerId, sect);
            }
            else
            {
                // Set trial cooldown
                var state = EnsurePlayerState(playerId);
                state.TrialCooldownTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    + config.TrialCooldownDays * 86400L;
            }

            EventBus.Publish(new SectTrialCompletedEvent
            {
                PlayerId = playerId,
                Sect = sect,
                Passed = passed,
                Detail = passed
                    ? $"考核通过！获得【{config.DisplayName}】令牌。"
                    : $"考核未通过，{config.TrialCooldownDays}天后可再次申请。"
            });
        }

        // ─── Public API: Leave Flow ────────────────────────────────────

        /// <summary>
        /// Peaceful leave: player voluntarily exits current formal sect.
        /// Conditions: contribution >= 0, no active missions flag here (caller checks).
        /// Penalties: retain 50% contribution (configurable), reputation -30, 7-day cooldown.
        /// </summary>
        public LeaveResult PeacefulLeave(string playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
                return LeaveResult.NotInAnySect;

            if (!state.CurrentFormalSect.HasValue)
                return LeaveResult.NotInAnySect;

            var sect = state.CurrentFormalSect.Value;
            var config = GetConfig(sect);

            // Can't "leave" 散修联盟 via this path
            if (!config.IsFormal)
                return LeaveResult.CannotLeaveSanctionAlliance;

            // Must not have negative contribution for peaceful leave
            if (state.Contribution < 0)
                return LeaveResult.ContributionNegative;

            // Calculate retained contribution
            int retainedContribution = Mathf.RoundToInt(state.Contribution * config.ContributionRetentionOnLeave);

            // Apply penalties
            int originalContribution = state.Contribution;
            SectType previousSect = sect;

            state.CurrentFormalSect = null;
            state.Contribution = retainedContribution;
            state.Rank = SectRank.OuterDisciple;
            state.LeaveCooldownTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + config.LeaveCooldownDays * 86400L;

            Debug.Log($"[SectManager] 和平退出: {playerId} 离开 {config.DisplayName}，贡献 {originalContribution}→{retainedContribution}，声望 -{config.PeacefulLeaveRepPenalty}，冷却 {config.LeaveCooldownDays}天");

            EventBus.Publish(new SectLeftEvent
            {
                PlayerId = playerId,
                PreviousSect = previousSect,
                LeaveType = LeaveType.Peaceful,
                RetainedContribution = retainedContribution,
            });

            return LeaveResult.Success;
        }

        /// <summary>
        /// Modify a player's contribution by delta. If contribution drops to
        /// or below the expulsion threshold, triggers forced leave.
        /// Returns true if still in sect, false if expelled.
        /// </summary>
        public bool ModifyContribution(string playerId, int delta)
        {
            var state = EnsurePlayerState(playerId);
            if (!state.CurrentFormalSect.HasValue)
                return false;

            state.Contribution += delta;
            var config = GetConfig(state.CurrentFormalSect.Value);

            Debug.Log($"[SectManager] 贡献变化: {playerId} {delta:+0;-0} → {state.Contribution}");

            if (state.Contribution <= config.ExpulsionContributionThreshold)
            {
                ForceLeave(playerId);
                return false; // expelled
            }

            return true; // still in sect
        }

        // ─── Internal ──────────────────────────────────────────────────

        /// <summary>Force leave when contribution drops to expulsion threshold.</summary>
        private void ForceLeave(string playerId)
        {
            var state = _playerStates[playerId];
            var sect = state.CurrentFormalSect.Value;
            var config = GetConfig(sect);
            int finalContribution = state.Contribution;

            state.CurrentFormalSect = null;
            state.Contribution = 0;
            state.Rank = SectRank.OuterDisciple;

            Debug.Log($"[SectManager] 强制逐出: {playerId} 被 {config.DisplayName} 逐出（贡献 {finalContribution} ≤ {config.ExpulsionContributionThreshold}）");

            EventBus.Publish(new SectExpelledEvent
            {
                PlayerId = playerId,
                Sect = sect,
                FinalContribution = finalContribution,
            });

            EventBus.Publish(new SectLeftEvent
            {
                PlayerId = playerId,
                PreviousSect = sect,
                LeaveType = LeaveType.Forced,
                RetainedContribution = 0,
            });
        }

        /// <summary>Finalize membership after trial pass.</summary>
        private void FinalizeJoin(string playerId, SectType sect)
        {
            var config = GetConfig(sect);
            var state = EnsurePlayerState(playerId);

            // If switching from another formal sect, auto-leave
            if (state.CurrentFormalSect.HasValue && state.CurrentFormalSect.Value != sect)
            {
                Debug.Log($"[SectManager] 自动退出: {playerId} 离开原门派 {GetConfig(state.CurrentFormalSect.Value).DisplayName}");
                // In auto-leave scenario, we don't trigger events for the previous sect
                // as this is a silent switch
            }

            state.CurrentFormalSect = sect;
            state.Rank = SectRank.OuterDisciple;
            state.Contribution = 0;
            state.LeaveCooldownTimestamp = 0;
            state.TrialCooldownTimestamp = 0;
            state.IsInSanctionAlliance = true; // All formal members remain in 散修联盟

            string tokenItemId = $"token_{sect.ToString().ToLowerInvariant()}";

            Debug.Log($"[SectManager] 加入门派: {playerId} 加入 {config.DisplayName}，获得令牌 {tokenItemId}");

            EventBus.Publish(new SectJoinedEvent
            {
                PlayerId = playerId,
                Sect = sect,
                TokenItemId = tokenItemId,
            });
        }

        /// <summary>Get or create a player state entry.</summary>
        private PlayerSectState EnsurePlayerState(string playerId)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                state = new PlayerSectState
                {
                    PlayerId = playerId,
                    CurrentFormalSect = null,
                    IsInSanctionAlliance = true,
                    Contribution = 0,
                    Rank = SectRank.OuterDisciple,
                };
                _playerStates[playerId] = state;
            }
            return state;
        }

        /// <summary>Get remaining cooldown in days from a Unix timestamp.</summary>
        private static double GetCooldownRemaining(double unixTimestamp)
        {
            double remainingSeconds = unixTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return remainingSeconds > 0 ? remainingSeconds / 86400.0 : 0.0;
        }
    }
}
