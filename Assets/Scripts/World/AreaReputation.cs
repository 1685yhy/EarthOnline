using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seven-tier reputation level from 仇恨(-3) to 崇拜(3).
    /// Ordered so numeric comparison works intuitively: higher = better.
    /// </summary>
    public enum ReputationLevel
    {
        Hatred = -3,       // 仇恨     — triggers guard pursuit
        Hostile = -2,      // 敌对     — NPCs refuse trade
        Indifferent = -1,  // 冷淡
        Neutral = 0,       // 中立     — teleport allowed, fastest decay
        Friendly = 1,      // 友好
        Respect = 2,       // 尊敬     — 8折 shop discount
        Adoration = 3,     // 崇拜     — 8折 shop discount
    }

    /// <summary>Reason for a reputation value change.</summary>
    public enum ReputationChangeReason
    {
        TaskCompleted,       // 完成区域内任务
        KillFriendlyNpc,     // 击杀区域内友好NPC
        DailyDecay,          // 每日自然衰减
        Donation,            // 捐献资源
        Betrayal,            // 背叛行为
        EventOutcome,        // 动态事件结果
        DebugOverride,       // 调试覆盖
    }

    /// <summary>Types of spawnable resources in a region ecosystem.</summary>
    public enum EcosystemResourceType
    {
        SpiritualHerb,       // 灵材
        DemonBeast,          // 妖兽
        Mineral,             // 矿石
    }

    // ─── Event Data ─────────────────────────────────────────────────────

    /// <summary>Published when a player's reputation value changes in a region.</summary>
    public struct AreaReputationChangedEvent
    {
        public string PlayerId;
        public string RegionId;
        public int OldValue;
        public int NewValue;
        public int Delta;
        public ReputationChangeReason Reason;
    }

    /// <summary>Published when a player crosses a reputation tier boundary.</summary>
    public struct AreaReputationLevelChangedEvent
    {
        public string PlayerId;
        public string RegionId;
        public ReputationLevel OldLevel;
        public ReputationLevel NewLevel;
    }

    /// <summary>Published each time daily decay is applied to a reputation entry.</summary>
    public struct AreaReputationDecayedEvent
    {
        public string PlayerId;
        public string RegionId;
        public int DecayAmount;
        public int ResultValue;
    }

    /// <summary>
    /// Published when a player at 仇恨 (Hatred) level enters a region.
    /// The guard AI / pursuit system should subscribe to trigger NPC chase behavior.
    /// </summary>
    public struct AreaHostileEntryEvent
    {
        public string PlayerId;
        public string RegionId;
        public ReputationLevel CurrentLevel;
    }

    // ─── Config Data Classes ──────────────────────────────────────────

    /// <summary>Configuration for one reputation tier (级).</summary>
    [Serializable]
    public class ReputationTierConfig
    {
        [Header("Identity")]
        public string DisplayName;
        public ReputationLevel Level;

        [Header("Value Range (-1000 ~ 1000)")]
        public int MinValue;
        public int MaxValue;

        [Header("Decay")]
        [Range(0f, 1f)]
        [Tooltip("Decay = BaseDailyDecay * (1 - LevelFactor). Higher factor = slower decay.")]
        public float LevelFactor;

        [Header("Commerce")]
        [Range(0f, 1f)]
        [Tooltip("Price multiplier for shops: 1.0 = full price, 0.8 = 20%% off (Respect+).")]
        public float PriceMultiplier = 1f;
        public bool CanTrade = true;

        [Header("Restrictions")]
        public bool TeleportAllowed = true;
        public bool TriggersPursuit;
    }

    /// <summary>A single spawn entry within a region's ecosystem (灵材/妖兽/矿石).</summary>
    [Serializable]
    public class ResourceSpawnEntry
    {
        [Header("Identity")]
        public string ResourceId;
        public string DisplayName;
        public EcosystemResourceType ResourceType;

        [Header("Spawn Cycle")]
        public int RefreshIntervalHours = 24;
        public int MaxCount = 5;

        [Header("Quality")]
        [Range(1, 10)]
        public int Tier = 1;
    }

    /// <summary>
    /// Ecosystem definition for one region.
    /// Each region has independent resources (灵材/妖兽/NPC/势力) and refresh cycles.
    /// </summary>
    [Serializable]
    public class RegionEcosystemConfig
    {
        [Header("Identity")]
        public string RegionId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;

        [Header("Flora & Materials (灵材)")]
        public List<ResourceSpawnEntry> SpiritualHerbs = new();

        [Header("Fauna & Monsters (妖兽)")]
        public List<ResourceSpawnEntry> DemonBeasts = new();

        [Header("Minerals (矿石)")]
        public List<ResourceSpawnEntry> Minerals = new();

        [Header("NPC & Factions")]
        public List<string> NpcIds = new();
        /// <summary>Factions (e.g., sect IDs) that hold influence over this region.</summary>
        public List<string> ControllingFactions = new();

        [Header("Dynamic Events")]
        public int DynamicEventSlots = 2;
    }

    // ─── Player-State Data ───────────────────────────────────────────

    /// <summary>
    /// Per-player, per-region reputation state for save/load.
    /// Value range: -1000 ~ 1000, clamped.
    /// </summary>
    [Serializable]
    public class PlayerAreaReputation
    {
        public string PlayerId;
        public string RegionId;
        public int Value;
        /// <summary>Unix timestamp (seconds) of the last daily decay application.</summary>
        public double LastDecayTimestamp;
    }

    // ─── Area Reputation Manager ─────────────────────────────────────────

    /// <summary>
    /// Manages per-region reputation and ecosystem definitions for EarthOnline.
    ///
    /// Core features (Story 005):
    ///   REP-01: Task completion → reputation gain
    ///   REP-02: Killing friendly NPCs → reputation loss
    ///   REP-03: 尊敬 (Respect) and above → shop 20% discount
    ///   REP-04: 敌对 (Hostile) and below → NPCs refuse trade
    ///   REP-05: 仇恨 (Hatred) → entering region triggers guard pursuit
    ///   REP-06: Daily decay: 5 * (1 - LevelFactor), 中立 decays fastest at 5/day
    ///   REP-07: Teleport allowed within 中立 range (-200 ~ 200)
    ///   Each region has independent ecosystem (灵材/妖兽/NPC/势力)
    ///
    /// Core invariants:
    /// - Reputation is tracked per-player per-region, fully independent.
    /// - Range is clamped to [-1000, 1000].
    /// - Decay formula: BaseDecay(5) * (1 - LevelFactor)
    /// - Daily decay moves reputation toward 0 (positive decreases, negative increases).
    /// - Only active records decay; reputation at 0 skips decay.
    /// </summary>
    public class AreaReputation : MonoBehaviour
    {
        // ─── Singleton ─────────────────────────────────────────────────

        public static AreaReputation Instance { get; private set; }

        // ─── Serialized Config Overrides ────────────────────────────────

        [Header("Global Config")]
        [SerializeField]
        [Tooltip("Base daily decay amount before tier factor is applied.")]
        private int _baseDailyDecay = 5;

        [Header("Ecosystem Overrides")]
        [SerializeField] private bool _overrideDefaultEcosystems;
        [SerializeField] private RegionEcosystemConfig[] _ecosystemOverrides;

        // ─── Runtime State ──────────────────────────────────────────────

        // [playerId][regionId] -> PlayerAreaReputation
        private readonly Dictionary<string, Dictionary<string, PlayerAreaReputation>> _playerReputations
            = new Dictionary<string, Dictionary<string, PlayerAreaReputation>>();

        // ─── Default Tier Configuration ──────────────────────────────────

        private static readonly ReputationTierConfig[] DefaultTiers =
        {
            new ReputationTierConfig
            {
                DisplayName = "仇恨", Level = ReputationLevel.Hatred,
                MinValue = -1000, MaxValue = -801,
                LevelFactor = 0.8f,
                PriceMultiplier = 1f,
                CanTrade = false, TeleportAllowed = false, TriggersPursuit = true,
            },
            new ReputationTierConfig
            {
                DisplayName = "敌对", Level = ReputationLevel.Hostile,
                MinValue = -800, MaxValue = -601,
                LevelFactor = 0.6f,
                PriceMultiplier = 1f,
                CanTrade = false, TeleportAllowed = false, TriggersPursuit = false,
            },
            new ReputationTierConfig
            {
                DisplayName = "冷淡", Level = ReputationLevel.Indifferent,
                MinValue = -600, MaxValue = -201,
                LevelFactor = 0.3f,
                PriceMultiplier = 1f,
                CanTrade = true, TeleportAllowed = false, TriggersPursuit = false,
            },
            new ReputationTierConfig
            {
                DisplayName = "中立", Level = ReputationLevel.Neutral,
                MinValue = -200, MaxValue = 200,
                LevelFactor = 0.0f,               // fastest decay: 5 * 1.0 = 5/day
                PriceMultiplier = 1f,
                CanTrade = true, TeleportAllowed = true, TriggersPursuit = false,
            },
            new ReputationTierConfig
            {
                DisplayName = "友好", Level = ReputationLevel.Friendly,
                MinValue = 201, MaxValue = 600,
                LevelFactor = 0.3f,
                PriceMultiplier = 1f,
                CanTrade = true, TeleportAllowed = true, TriggersPursuit = false,
            },
            new ReputationTierConfig
            {
                DisplayName = "尊敬", Level = ReputationLevel.Respect,
                MinValue = 601, MaxValue = 800,
                LevelFactor = 0.6f,
                PriceMultiplier = 0.8f,            // 8折
                CanTrade = true, TeleportAllowed = true, TriggersPursuit = false,
            },
            new ReputationTierConfig
            {
                DisplayName = "崇拜", Level = ReputationLevel.Adoration,
                MinValue = 801, MaxValue = 1000,
                LevelFactor = 0.8f,                // slowest decay: 5 * 0.2 = 1/day
                PriceMultiplier = 0.8f,            // 8折
                CanTrade = true, TeleportAllowed = true, TriggersPursuit = false,
            },
        };

        // ─── Default Region Ecosystems ────────────────────────────────────

        private static readonly Dictionary<string, RegionEcosystemConfig> DefaultEcosystems
            = new Dictionary<string, RegionEcosystemConfig>
        {
            {
                "newbie_village",
                new RegionEcosystemConfig
                {
                    RegionId = "newbie_village",
                    DisplayName = "新手村",
                    Description = "灵气稀薄的边陲村落，适合初学者修炼。",
                    SpiritualHerbs = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "herb_spirit_grass", DisplayName = "灵草", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 4, MaxCount = 10, Tier = 1 },
                        new() { ResourceId = "herb_healing_root", DisplayName = "止血根", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 6, MaxCount = 8, Tier = 1 },
                    },
                    DemonBeasts = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "beast_wild_boar", DisplayName = "野猪", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 8, MaxCount = 5, Tier = 1 },
                    },
                    Minerals = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "ore_copper", DisplayName = "铜矿", ResourceType = EcosystemResourceType.Mineral, RefreshIntervalHours = 12, MaxCount = 3, Tier = 1 },
                    },
                    NpcIds = new List<string> { "npc_village_elder", "npc_trader_lin", "npc_blacksmith_wang" },
                    ControllingFactions = new List<string> { "sanction_alliance" },
                    DynamicEventSlots = 1,
                }
            },
            {
                "spirit_forest",
                new RegionEcosystemConfig
                {
                    RegionId = "spirit_forest",
                    DisplayName = "灵材森林",
                    Description = "灵气充沛的古森林，灵材丰富但妖兽横行。",
                    SpiritualHerbs = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "herb_spirit_grass", DisplayName = "灵草", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 3, MaxCount = 15, Tier = 2 },
                        new() { ResourceId = "herb_moon_flower", DisplayName = "月光花", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 8, MaxCount = 5, Tier = 3 },
                        new() { ResourceId = "herb_fire_lingzhi", DisplayName = "火灵芝", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 12, MaxCount = 3, Tier = 4 },
                    },
                    DemonBeasts = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "beast_shadow_wolf", DisplayName = "影狼", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 6, MaxCount = 8, Tier = 2 },
                        new() { ResourceId = "beast_iron_bear", DisplayName = "铁甲熊", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 10, MaxCount = 3, Tier = 3 },
                    },
                    Minerals = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "ore_iron", DisplayName = "铁矿", ResourceType = EcosystemResourceType.Mineral, RefreshIntervalHours = 10, MaxCount = 5, Tier = 2 },
                    },
                    NpcIds = new List<string> { "npc_herbalist_mei" },
                    ControllingFactions = new List<string> { "qingyun_men" },
                    DynamicEventSlots = 2,
                }
            },
            {
                "demon_abyss",
                new RegionEcosystemConfig
                {
                    RegionId = "demon_abyss",
                    DisplayName = "妖兽深渊",
                    Description = "妖兽聚集的险恶之地，危险与机遇并存。",
                    SpiritualHerbs = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "herb_demon_grass", DisplayName = "魔灵草", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 6, MaxCount = 6, Tier = 5 },
                    },
                    DemonBeasts = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "beast_flame_lion", DisplayName = "炎狮", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 8, MaxCount = 5, Tier = 4 },
                        new() { ResourceId = "beast_thunder_eagle", DisplayName = "雷鹰", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 6, MaxCount = 4, Tier = 5 },
                        new() { ResourceId = "beast_abyss_drake", DisplayName = "深渊龙蜥", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 24, MaxCount = 1, Tier = 7 },
                    },
                    Minerals = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "ore_dark_crystal", DisplayName = "暗晶", ResourceType = EcosystemResourceType.Mineral, RefreshIntervalHours = 16, MaxCount = 3, Tier = 4 },
                    },
                    NpcIds = new List<string>(),
                    ControllingFactions = new List<string> { "yushou_yizu" },
                    DynamicEventSlots = 3,
                }
            },
            {
                "trade_city",
                new RegionEcosystemConfig
                {
                    RegionId = "trade_city",
                    DisplayName = "坊市",
                    Description = "商盟管辖的贸易城市，各路修士聚集交易。",
                    SpiritualHerbs = new List<ResourceSpawnEntry>(),
                    DemonBeasts = new List<ResourceSpawnEntry>(),
                    Minerals = new List<ResourceSpawnEntry>(),
                    NpcIds = new List<string> { "npc_trader_zhao", "npc_auctioneer_li", "npc_bank_chen", "npc_alchemist_sun" },
                    ControllingFactions = new List<string> { "shang_meng" },
                    DynamicEventSlots = 4,
                }
            },
            {
                "snow_peak",
                new RegionEcosystemConfig
                {
                    RegionId = "snow_peak",
                    DisplayName = "雪峰",
                    Description = "终年积雪的高峰，天元宗山门所在。严寒中蕴藏稀世灵材。",
                    SpiritualHerbs = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "herb_snow_lotus", DisplayName = "雪莲", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 12, MaxCount = 3, Tier = 5 },
                        new() { ResourceId = "herb_ice_crystal", DisplayName = "冰晶草", ResourceType = EcosystemResourceType.SpiritualHerb, RefreshIntervalHours = 8, MaxCount = 5, Tier = 4 },
                    },
                    DemonBeasts = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "beast_frost_wolf", DisplayName = "霜狼", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 8, MaxCount = 6, Tier = 3 },
                        new() { ResourceId = "beast_ice_serpent", DisplayName = "冰蟒", ResourceType = EcosystemResourceType.DemonBeast, RefreshIntervalHours = 14, MaxCount = 2, Tier = 5 },
                    },
                    Minerals = new List<ResourceSpawnEntry>
                    {
                        new() { ResourceId = "ore_frost_iron", DisplayName = "霜铁矿", ResourceType = EcosystemResourceType.Mineral, RefreshIntervalHours = 12, MaxCount = 4, Tier = 3 },
                        new() { ResourceId = "ore_spirit_jade", DisplayName = "灵玉", ResourceType = EcosystemResourceType.Mineral, RefreshIntervalHours = 24, MaxCount = 2, Tier = 6 },
                    },
                    NpcIds = new List<string> { "npc_tianyuan_guard", "npc_tianyuan_elder" },
                    ControllingFactions = new List<string> { "tianyuan_zong" },
                    DynamicEventSlots = 2,
                }
            },
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

        // ─── Public API: Tier & Ecosystem Queries ──────────────────────

        /// <summary>Get the tier config for a given reputation level.</summary>
        public ReputationTierConfig GetTierConfig(ReputationLevel level)
        {
            for (int i = 0; i < DefaultTiers.Length; i++)
            {
                if (DefaultTiers[i].Level == level)
                    return DefaultTiers[i];
            }
            return DefaultTiers[3]; // fallback: 中立
        }

        /// <summary>
        /// Get the ecosystem config for a region.
        /// Returns null if the region ID is not recognized.
        /// </summary>
        public RegionEcosystemConfig GetEcosystem(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return null;

            if (_overrideDefaultEcosystems && _ecosystemOverrides != null)
            {
                for (int i = 0; i < _ecosystemOverrides.Length; i++)
                {
                    if (_ecosystemOverrides[i] != null && _ecosystemOverrides[i].RegionId == regionId)
                        return _ecosystemOverrides[i];
                }
            }
            return DefaultEcosystems.TryGetValue(regionId, out var eco) ? eco : null;
        }

        /// <summary>Enumerate all known region IDs (overrides + defaults).</summary>
        public IEnumerable<string> GetAllRegionIds()
        {
            var ids = new HashSet<string>();
            if (_overrideDefaultEcosystems && _ecosystemOverrides != null)
            {
                foreach (var eco in _ecosystemOverrides)
                {
                    if (eco != null) ids.Add(eco.RegionId);
                }
            }
            foreach (var kv in DefaultEcosystems)
                ids.Add(kv.Key);
            return ids;
        }

        // ─── Public API: Reputation Queries ────────────────────────────

        /// <summary>
        /// Get raw reputation value for a player in a region.
        /// Returns 0 (neutral) if no record exists yet.
        /// </summary>
        public int GetReputation(string playerId, string regionId)
        {
            if (_playerReputations.TryGetValue(playerId, out var regions)
                && regions.TryGetValue(regionId, out var rep))
            {
                return rep.Value;
            }
            return 0;
        }

        /// <summary>Calculate the reputation level for any raw value in [-1000, 1000].</summary>
        public ReputationLevel CalculateLevel(int value)
        {
            value = Mathf.Clamp(value, -1000, 1000);
            for (int i = DefaultTiers.Length - 1; i >= 0; i--)
            {
                var tier = DefaultTiers[i];
                if (value >= tier.MinValue && value <= tier.MaxValue)
                    return tier.Level;
            }
            return ReputationLevel.Neutral;
        }

        /// <summary>Get the reputation level for a player in a region (derived from value).</summary>
        public ReputationLevel GetLevel(string playerId, string regionId)
        {
            return CalculateLevel(GetReputation(playerId, regionId));
        }

        /// <summary>Get the full tier config for the player's current level in a region.</summary>
        public ReputationTierConfig GetCurrentTier(string playerId, string regionId)
        {
            return GetTierConfig(GetLevel(playerId, regionId));
        }

        /// <summary>Can this player trade with NPCs in this region? (REP-04)</summary>
        public bool CanTrade(string playerId, string regionId)
        {
            return GetCurrentTier(playerId, regionId).CanTrade;
        }

        /// <summary>Can this player use teleportation in this region? (REP-07)</summary>
        public bool CanTeleport(string playerId, string regionId)
        {
            return GetCurrentTier(playerId, regionId).TeleportAllowed;
        }

        /// <summary>
        /// Get the shop price multiplier for this player in this region.
        /// 1.0 = full price, 0.8 = 20% discount (REP-03).
        /// </summary>
        public float GetPriceMultiplier(string playerId, string regionId)
        {
            return GetCurrentTier(playerId, regionId).PriceMultiplier;
        }

        /// <summary>
        /// Does this player trigger guard pursuit when entering this region?
        /// True only at 仇恨 (Hatred) level. (REP-05)
        /// </summary>
        public bool ShouldTriggerPursuit(string playerId, string regionId)
        {
            return GetCurrentTier(playerId, regionId).TriggersPursuit;
        }

        /// <summary>
        /// Get the mutable reputation state for serialization (save system).
        /// Returns null if no record exists.
        /// </summary>
        public PlayerAreaReputation GetPlayerReputation(string playerId, string regionId)
        {
            if (_playerReputations.TryGetValue(playerId, out var regions))
            {
                regions.TryGetValue(regionId, out var rep);
                return rep;
            }
            return null;
        }

        /// <summary>Restore a saved reputation entry (e.g., on game load).</summary>
        public void RestorePlayerReputation(PlayerAreaReputation state)
        {
            if (state == null) return;
            if (!_playerReputations.TryGetValue(state.PlayerId, out var regions))
            {
                regions = new Dictionary<string, PlayerAreaReputation>();
                _playerReputations[state.PlayerId] = regions;
            }
            regions[state.RegionId] = state;
        }

        /// <summary>Get all reputation data for a player (for full save snapshot).</summary>
        public IReadOnlyCollection<PlayerAreaReputation> GetAllPlayerReputations(string playerId)
        {
            if (_playerReputations.TryGetValue(playerId, out var regions))
                return regions.Values;
            return Array.Empty<PlayerAreaReputation>();
        }

        // ─── Public API: Reputation Mutations ──────────────────────────

        /// <summary>
        /// Modify a player's reputation in a region by a delta.
        /// Clamps result to [-1000, 1000].
        /// Publishes <see cref="AreaReputationChangedEvent"/> and, if the
        /// tier boundary is crossed, <see cref="AreaReputationLevelChangedEvent"/>.
        ///
        /// Call this for:
        ///   - REP-01: Task completion (positive delta)
        ///   - REP-02: Killing friendly NPCs (negative delta)
        /// </summary>
        public void ModifyReputation(string playerId, string regionId, int delta, ReputationChangeReason reason)
        {
            if (delta == 0) return;

            var rep = EnsureReputation(playerId, regionId);
            int oldValue = rep.Value;
            var oldLevel = CalculateLevel(oldValue);

            rep.Value = Mathf.Clamp(rep.Value + delta, -1000, 1000);
            int newValue = rep.Value;
            int actualDelta = newValue - oldValue;

            Debug.Log($"[AreaReputation] {playerId} @ {regionId}: {oldValue:+0;-0} -> {newValue:+0;-0} ({reason})");

            EventBus.Publish(new AreaReputationChangedEvent
            {
                PlayerId = playerId,
                RegionId = regionId,
                OldValue = oldValue,
                NewValue = newValue,
                Delta = actualDelta,
                Reason = reason,
            });

            // Publish level-change event if tier boundary was crossed
            var newLevel = CalculateLevel(newValue);
            if (newLevel != oldLevel)
            {
                Debug.Log($"[AreaReputation] 等级变化: {playerId} @ {regionId}: {GetTierConfig(oldLevel).DisplayName} -> {GetTierConfig(newLevel).DisplayName}");
                EventBus.Publish(new AreaReputationLevelChangedEvent
                {
                    PlayerId = playerId,
                    RegionId = regionId,
                    OldLevel = oldLevel,
                    NewLevel = newLevel,
                });
            }
        }

        /// <summary>
        /// Apply a single daily decay tick for a specific player-region pair.
        /// Decay formula: <c>BaseDailyDecay * (1 - LevelFactor)</c>
        ///
        /// Decay always moves reputation toward 0:
        ///   - Positive values decrease; negative values increase.
        ///   - Reputation at exactly 0 is skipped.
        ///   - No auto-creation — if no record exists, nothing happens.
        ///
        /// REP-06: 中立 decays fastest (LevelFactor=0.0 → decay = 5/day).
        /// 崇拜/仇恨 decay slowest (LevelFactor=0.8 → decay = 1/day).
        /// </summary>
        public void ApplyDailyDecay(string playerId, string regionId)
        {
            if (!_playerReputations.TryGetValue(playerId, out var regions)
                || !regions.TryGetValue(regionId, out var rep))
            {
                return; // No record = nothing to decay
            }

            if (rep.Value == 0) return; // Already neutral, skip

            var tier = GetTierConfig(CalculateLevel(rep.Value));
            int decayAmount = Mathf.RoundToInt(_baseDailyDecay * (1f - tier.LevelFactor));
            if (decayAmount <= 0) return;

            int oldValue = rep.Value;

            // Decay toward 0
            if (rep.Value > 0)
            {
                rep.Value = Mathf.Max(0, rep.Value - decayAmount);
            }
            else
            {
                rep.Value = Mathf.Min(0, rep.Value + decayAmount);
            }

            rep.LastDecayTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Debug.Log($"[AreaReputation] 每日衰减: {playerId} @ {regionId}: {oldValue:+0;-0} -> {rep.Value:+0;-0} (-{decayAmount})");

            EventBus.Publish(new AreaReputationDecayedEvent
            {
                PlayerId = playerId,
                RegionId = regionId,
                DecayAmount = decayAmount,
                ResultValue = rep.Value,
            });
        }

        /// <summary>Apply daily decay for all regions the given player has reputation in.</summary>
        public void ApplyDailyDecayAll(string playerId)
        {
            if (!_playerReputations.TryGetValue(playerId, out var regions))
                return;

            // Snapshot keys to avoid mutation during iteration
            var keys = new List<string>(regions.Keys);
            foreach (var regionId in keys)
            {
                ApplyDailyDecay(playerId, regionId);
            }
        }

        /// <summary>
        /// Called when a player enters a region.
        /// If the player's reputation is at 仇恨 (Hatred), publishes
        /// <see cref="AreaHostileEntryEvent"/> so the guard AI system can respond.
        /// (REP-05)
        /// </summary>
        public void OnPlayerEnterRegion(string playerId, string regionId)
        {
            EnsureReputation(playerId, regionId); // Touch record so decay history starts

            var level = GetLevel(playerId, regionId);
            if (level == ReputationLevel.Hatred)
            {
                Debug.Log($"[AreaReputation] 仇恨触发: {playerId} 踏入 {regionId}，守卫追杀启动！");
                EventBus.Publish(new AreaHostileEntryEvent
                {
                    PlayerId = playerId,
                    RegionId = regionId,
                    CurrentLevel = level,
                });
            }
        }

        /// <summary>
        /// Directly set reputation (for save restoration, admin, or debug).
        /// Prefer <see cref="ModifyReputation"/> for game-logic changes so events fire properly.
        /// </summary>
        public void SetReputationRaw(string playerId, string regionId, int value)
        {
            var rep = EnsureReputation(playerId, regionId);
            rep.Value = Mathf.Clamp(value, -1000, 1000);
        }

        // ─── Internal ──────────────────────────────────────────────────

        /// <summary>Get or create a reputation record.</summary>
        private PlayerAreaReputation EnsureReputation(string playerId, string regionId)
        {
            if (!_playerReputations.TryGetValue(playerId, out var regions))
            {
                regions = new Dictionary<string, PlayerAreaReputation>();
                _playerReputations[playerId] = regions;
            }

            if (!regions.TryGetValue(regionId, out var rep))
            {
                rep = new PlayerAreaReputation
                {
                    PlayerId = playerId,
                    RegionId = regionId,
                    Value = 0,
                    LastDecayTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                regions[regionId] = rep;
            }

            return rep;
        }

        // ─── Editor Helpers ─────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Debug: Print All Reputations")]
        private void DebugPrintAll()
        {
            foreach (var playerKv in _playerReputations)
            {
                foreach (var regionKv in playerKv.Value)
                {
                    var rep = regionKv.Value;
                    var tier = GetTierConfig(CalculateLevel(rep.Value));
                    Debug.Log($"[AreaReputation Debug] Player={rep.PlayerId} Region={rep.RegionId} Value={rep.Value} Level={tier.DisplayName} LastDecay={rep.LastDecayTimestamp}");
                }
            }
        }
#endif
    }
}
