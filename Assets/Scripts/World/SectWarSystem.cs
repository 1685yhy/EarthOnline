using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Enums ──────────────────────────────────────────────────────────────

    /// <summary>Forms of war a sect can initiate.</summary>
    public enum WarForm
    {
        BattlefieldInstance,  // 战场副本 — independent scene instance
        ResourceContest,      // 资源点争夺 — map resource point contest
    }

    /// <summary>Current phase of a sect war.</summary>
    public enum WarPhase
    {
        Preparation,   // 宣战期 — first 24h, deployment
        Active,        // 交战期 — 72h main combat
        Settlement,    // 结算期 — after 72h, calculating results
        Concluded,     // 已结束
    }

    /// <summary>Result of a settled war.</summary>
    public enum WarResult
    {
        AttackerWin,
        DefenderWin,
        Draw,
        Cancelled,
    }

    /// <summary>Type of scoring action in a war.</summary>
    public enum WarScoreType
    {
        KillDisciple,   // +10
        DestroyFlag,    // +100
        KillLeader,     // +500
        CaptureResourcePoint, // +50
    }

    /// <summary>Sect attitude / diplomatic stance.</summary>
    public enum SectAttitude
    {
        Hostile,      // 敌对
        Unfriendly,   // 不友好
        Neutral,      // 中立
        Friendly,     // 友好
        Allied,       // 同盟
    }

    // ─── Config Data ────────────────────────────────────────────────────────

    /// <summary>Configurable constants for the sect war system.</summary>
    [Serializable]
    public class SectWarConfig
    {
        [Header("War Declaration")]
        [Tooltip("Minimum sect reputation level required to declare war.")]
        public int MinReputationLevel = 3;
        [Tooltip("Spirit stones cost to declare war.")]
        public int DeclarationCost = 10000;

        [Header("Duration")]
        [Tooltip("Preparation phase duration in hours before combat begins.")]
        public float PreparationHours = 24f;
        [Tooltip("Main combat phase duration in hours.")]
        public float CombatHours = 72f;

        [Header("Scoring")]
        public int ScoreKillDisciple = 10;
        public int ScoreDestroyFlag = 100;
        public int ScoreKillLeader = 500;
        public int ScoreCaptureResourcePoint = 50;

        [Header("Settlement")]
        [Tooltip("Base compensation multiplier: loser pays (scoreDiff * this) spirit stones.")]
        public float CompensationMultiplier = 10f;
        [Tooltip("Minimum compensation paid by loser (spirit stones).")]
        public int MinCompensation = 5000;
        [Tooltip("Number of territories loser forfeits to winner (clamped to available).")]
        public int TerritoryTransferCount = 1;
        [Tooltip("Risk rating increase during war (percentage points).")]
        public float WarRiskIncrease = 25f;

        [Header("Cooldown")]
        [Tooltip("Days a sect must wait after a war before declaring again.")]
        public int WarCooldownDays = 14;
    }

    /// <summary>Data for a territory / resource point that can be contested.</summary>
    [Serializable]
    public class SectTerritory
    {
        public string TerritoryId;
        public string DisplayName;
        public string RegionId;
        public Vector3 WorldPosition;
        public SectType Owner;

        [TextArea(1, 3)]
        public string Description;
        public float ResourceRichness; // 0~1, affects resource output
    }

    /// <summary>Reputation standing between two sects.</summary>
    [Serializable]
    public class SectReputationEntry
    {
        public SectType Target;
        public int ReputationValue;   // -100 ~ 100
        public SectAttitude Attitude;
    }

    /// <summary>Per-player war score tracking.</summary>
    [Serializable]
    public class PlayerWarScore
    {
        public string PlayerId;
        public int TotalScore;
        public int DisciplesKilled;
        public int FlagsDestroyed;
        public int LeadersKilled;
        public int ResourcePointsCaptured;
    }

    // ─── Runtime State ──────────────────────────────────────────────────────

    /// <summary>Runtime state of a single war.</summary>
    [Serializable]
    public class SectWarState
    {
        public string WarId;
        public SectType Attacker;
        public SectType Defender;
        public WarForm Form;
        public WarPhase Phase;

        public double DeclarationTimestamp;    // Unix seconds
        public double CombatStartTimestamp;
        public double SettlementTimestamp;

        public int AttackerScore;
        public int DefenderScore;

        public List<string> ContestedTerritoryIds = new List<string>();
        public List<string> AttackerRiskZoneIds = new List<string>();
        public List<string> DefenderRiskZoneIds = new List<string>();

        public Dictionary<string, PlayerWarScore> PlayerScores = new Dictionary<string, PlayerWarScore>();

        public bool AttackerPaid;
        public bool SettlementDone;

        /// <summary>Get the remaining combat time in hours (0 if not in combat).</summary>
        public float GetRemainingCombatHours(float combatHours)
        {
            if (Phase != WarPhase.Active) return 0f;
            double elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - CombatStartTimestamp;
            double remainingSeconds = (combatHours * 3600.0) - elapsed;
            return Mathf.Max(0f, (float)(remainingSeconds / 3600.0));
        }

        /// <summary>Check if the combat timer has expired.</summary>
        public bool IsCombatTimeExpired(float combatHours)
        {
            if (Phase != WarPhase.Active) return false;
            double elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - CombatStartTimestamp;
            return elapsed >= (combatHours * 3600.0);
        }
    }

    /// <summary>Overall reputation state between all sect pairs.</summary>
    [Serializable]
    public class SectDiplomacyState
    {
        /// <summary>Reputation values keyed by "SectType->SectType".</summary>
        public Dictionary<string, int> ReputationMap = new Dictionary<string, int>();

        public int GetReputation(SectType a, SectType b)
        {
            if (a == b) return 100;
            string key = GetKey(a, b);
            return ReputationMap.TryGetValue(key, out var val) ? val : 0;
        }

        public void AddReputation(SectType a, SectType b, int delta)
        {
            if (a == b) return;
            string key = GetKey(a, b);
            ReputationMap.TryGetValue(key, out var current);
            ReputationMap[key] = Mathf.Clamp(current + delta, -100, 100);
        }

        public SectAttitude GetAttitude(SectType a, SectType b)
        {
            int rep = GetReputation(a, b);
            if (rep <= -60) return SectAttitude.Hostile;
            if (rep <= -20) return SectAttitude.Unfriendly;
            if (rep >= 60) return SectAttitude.Allied;
            if (rep >= 20) return SectAttitude.Friendly;
            return SectAttitude.Neutral;
        }

        private static string GetKey(SectType a, SectType b)
        {
            // Deterministic ordering
            if (a < b) return $"{a}->{b}";
            return $"{b}->{a}";
        }
    }

    // ─── EventBus Events ────────────────────────────────────────────────────

    /// <summary>Published when a sect declares war on another sect.</summary>
    public struct WarDeclaredEvent
    {
        public string WarId;
        public SectType Attacker;
        public SectType Defender;
        public WarForm Form;
        public string AttackerDisplayName;
        public string DefenderDisplayName;
        public double PreparationEndTime;
    }

    /// <summary>Published when war enters the active combat phase.</summary>
    public struct WarCombatStartedEvent
    {
        public string WarId;
        public SectType Attacker;
        public SectType Defender;
        public double CombatEndTime;
    }

    /// <summary>Published when a player scores points in a war.</summary>
    public struct WarScoreChangedEvent
    {
        public string WarId;
        public string PlayerId;
        public WarScoreType ScoreType;
        public int PointsGained;
        public int AttackerTotal;
        public int DefenderTotal;
    }

    /// <summary>Published when a war is settled with results.</summary>
    public struct WarSettledEvent
    {
        public string WarId;
        public SectType Attacker;
        public SectType Defender;
        public WarResult Result;
        public int AttackerFinalScore;
        public int DefenderFinalScore;
        public int CompensationAmount;
        public List<string> TerritoriesTransferred;
        public string Summary;
    }

    /// <summary>Published when a resource point is captured during war.</summary>
    public struct ResourcePointCapturedEvent
    {
        public string TerritoryId;
        public string TerritoryName;
        public SectType NewOwner;
        public string CapturingPlayerId;
    }

    /// <summary>Published when war zone risk level changes.</summary>
    public struct WarZoneRiskChangedEvent
    {
        public string ZoneId;
        public RiskLevel PreviousLevel;
        public RiskLevel CurrentLevel;
        public bool IsWarActive;
    }

    /// <summary>Published when a sect is destroyed (all territories lost, members scattered).</summary>
    public struct SectDestroyedEvent
    {
        public SectType Sect;
        public string DisplayName;
        public SectType VictorSect;
        public List<string> AffectedPlayerIds;
    }

    /// <summary>Published when a sect leader betrays / defects.</summary>
    public struct LeaderBetrayalEvent
    {
        public SectType Sect;
        public string LeaderPlayerId;
        public string LeaderPlayerName;
        public string Reason;
        public int CrisisDurationDays;
    }

    /// <summary>Published when a player's spy identity is triggered.</summary>
    public struct SpyIdentityTriggeredEvent
    {
        public string PlayerId;
        public SectType CoverSect;
        public SectType TrueSect;
        public bool IsExposed;
    }

    /// <summary>Published when sect reputation changes between two sects.</summary>
    public struct SectReputationChangedEvent
    {
        public SectType SectA;
        public SectType SectB;
        public int OldValue;
        public int NewValue;
        public SectAttitude CurrentAttitude;
        public string Reason;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Sect War System
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages sect wars between formal sects (Story 005).
    ///
    /// Responsibilities:
    ///   - War declaration: reputation ≥ 3, 10000 spirit stones cost
    ///   - Two war forms: battlefield instance + resource point contest
    ///   - 72h combat timer with scoring system
    ///   - Scoring: kill disciple +10, destroy flag +100, kill leader +500
    ///   - Settlement: winner gets compensation + territory, loser loses territory + pays
    ///   - Temporary war zone risk level changes
    ///   - Sect diplomacy reputation tracking
    ///   - Sect destruction → members become 散修
    ///   - Leader betrayal → sect crisis
    ///   - Spy identity mechanics
    ///
    /// Depends on <see cref="SectManager"/> for player-sect-state access.
    /// </summary>
    public class SectWarSystem : MonoBehaviour
    {
        // ─── Singleton ─────────────────────────────────────────────────

        public static SectWarSystem Instance { get; private set; }

        // ─── Serialized Configuration ──────────────────────────────────

        [Header("War Configuration")]
        [SerializeField] private SectWarConfig _config;

        [Header("Default Territories (populated in Editor)")]
        [SerializeField] private List<SectTerritory> _defaultTerritories = new List<SectTerritory>();

        // ─── Runtime State ─────────────────────────────────────────────

        private List<SectWarState> _activeWars = new List<SectWarState>();
        private List<SectWarState> _warHistory = new List<SectWarState>();
        private SectDiplomacyState _diplomacy = new SectDiplomacyState();
        private Dictionary<string, SectTerritory> _territories = new Dictionary<string, SectTerritory>();
        private Dictionary<SectType, List<string>> _sectTerritories = new Dictionary<SectType, List<string>>();
        private Dictionary<SectType, double> _warCooldowns = new Dictionary<SectType, double>();
        private Dictionary<string, bool> _spyActiveMap = new Dictionary<string, bool>(); // playerId -> isSpy
        private Dictionary<string, SectType> _spyTrueSectMap = new Dictionary<string, SectType>(); // playerId -> true sect

        // ─── Default Config ───────────────────────────────────────────

        private static SectWarConfig DefaultConfig => new SectWarConfig();

        // ─── Unity Lifecycle ──────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_config == null)
                _config = DefaultConfig;

            InitializeDefaultTerritories();
            InitializeDefaultDiplomacy();
        }

        private void Update()
        {
            // Tick active wars every frame to check phase transitions
            float unscaled = Time.unscaledTime;
            if (unscaled - _lastWarTick >= _warTickInterval)
            {
                _lastWarTick = unscaled;
                TickWars();
            }
        }

        private float _lastWarTick;
        private const float _warTickInterval = 60f; // check every 60 seconds

        // ─── Initialization ────────────────────────────────────────────

        /// <summary>Set up default territory assignments per sect.</summary>
        private void InitializeDefaultTerritories()
        {
            if (_defaultTerritories.Count == 0)
            {
                // Create some default territories if none assigned in Editor
                _defaultTerritories = new List<SectTerritory>
                {
                    new SectTerritory { TerritoryId = "terr_tianyuan_peak", DisplayName = "天元主峰", RegionId = "region_central", Owner = SectType.TianYuanZong, Description = "天元宗山门所在", ResourceRichness = 0.8f },
                    new SectTerritory { TerritoryId = "terr_qingyun_valley", DisplayName = "青云谷", RegionId = "region_east", Owner = SectType.QingYunMen, Description = "青云门灵药园", ResourceRichness = 0.7f },
                    new SectTerritory { TerritoryId = "terr_shangmeng_hub", DisplayName = "商贸枢纽", RegionId = "region_central", Owner = SectType.ShangMeng, Description = "商盟贸易中心", ResourceRichness = 0.9f },
                    new SectTerritory { TerritoryId = "terr_yushou_wilds", DisplayName = "御兽原", RegionId = "region_west", Owner = SectType.YuShouYiZu, Description = "御兽族灵兽牧场", ResourceRichness = 0.6f },
                };
            }

            foreach (var t in _defaultTerritories)
            {
                _territories[t.TerritoryId] = t;
                if (!_sectTerritories.ContainsKey(t.Owner))
                    _sectTerritories[t.Owner] = new List<string>();
                _sectTerritories[t.Owner].Add(t.TerritoryId);
            }
        }

        /// <summary>Set initial diplomacy between all sect pairs.</summary>
        private void InitializeDefaultDiplomacy()
        {
            var sects = (SectType[])Enum.GetValues(typeof(SectType));
            foreach (SectType a in sects)
            {
                if (a == SectType.SanXiuLianMeng) continue;
                foreach (SectType b in sects)
                {
                    if (b == SectType.SanXiuLianMeng) continue;
                    if (a >= b) continue;

                    // Default relationships
                    if ((a == SectType.TianYuanZong && b == SectType.QingYunMen) ||
                        (a == SectType.ShangMeng && b == SectType.YuShouYiZu))
                    {
                        _diplomacy.AddReputation(a, b, 40); // Friendly
                    }
                    else if ((a == SectType.TianYuanZong && b == SectType.ShangMeng))
                    {
                        _diplomacy.AddReputation(a, b, 10); // Slightly friendly
                    }
                    else
                    {
                        _diplomacy.AddReputation(a, b, 0); // Neutral
                    }
                }
            }
        }

        // ─── Public API: War Declaration ──────────────────────────────

        /// <summary>
        /// Check if a sect can declare war on another sect.
        /// Requirements:
        ///   - Attacker reputation level ≥ MinReputationLevel
        ///   - Attacker has 10000 spirit stones
        ///   - Attacker is not already at war
        ///   - Target is a valid formal sect (not 散修联盟)
        ///   - Attacker not on war cooldown
        /// </summary>
        public bool CanDeclareWar(SectType attacker, int attackerRepLevel, int attackerSpiritStones, out string failReason)
        {
            failReason = "";

            if (attacker == SectType.SanXiuLianMeng)
            {
                failReason = "散修联盟无法对其他门派宣战。";
                return false;
            }

            if (attackerRepLevel < _config.MinReputationLevel)
            {
                failReason = $"声望等级不足：需要 {_config.MinReputationLevel} 级，当前 {attackerRepLevel} 级。";
                return false;
            }

            if (attackerSpiritStones < _config.DeclarationCost)
            {
                failReason = $"灵石不足：需要 {_config.DeclarationCost} 灵石，当前 {attackerSpiritStones}。";
                return false;
            }

            // Check if already at war
            foreach (var war in _activeWars)
            {
                if (war.Phase == WarPhase.Active || war.Phase == WarPhase.Preparation)
                {
                    if (war.Attacker == attacker || war.Defender == attacker)
                    {
                        failReason = "该门派已在战争中，无法重复宣战。";
                        return false;
                    }
                }
            }

            // Check cooldown
            if (_warCooldowns.TryGetValue(attacker, out double cdEnd))
            {
                double remaining = cdEnd - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (remaining > 0)
                {
                    failReason = $"战争冷却中：还需 {remaining / 86400:F1} 天。";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Declare war on a target sect. Returns the war ID on success.
        /// Attacker must pass CanDeclareWar checks first.
        /// </summary>
        public string DeclareWar(SectType attacker, SectType defender, WarForm form, int attackerSpiritStones)
        {
            if (!CanDeclareWar(attacker, GetSectRepLevel(attacker), attackerSpiritStones, out _))
                return null;

            if (defender == SectType.SanXiuLianMeng)
                return null;

            if (attacker == defender)
                return null;

            string warId = $"war_{attacker}_{defender}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var war = new SectWarState
            {
                WarId = warId,
                Attacker = attacker,
                Defender = defender,
                Form = form,
                Phase = WarPhase.Preparation,
                DeclarationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CombatStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)(_config.PreparationHours * 3600),
                AttackerPaid = true,
                SettlementDone = false,
            };

            // Determine contested territories (border territories or random)
            war.ContestedTerritoryIds = GetBorderTerritories(attacker, defender);

            // Mark risk zones
            war.AttackerRiskZoneIds = GetSectZoneIds(attacker);
            war.DefenderRiskZoneIds = GetSectZoneIds(defender);

            _activeWars.Add(war);

            // Apply risk level changes
            ApplyWarRiskChanges(war, true);

            Debug.Log($"[SectWarSystem] 宣战: {GetDisplayName(attacker)} 向 {GetDisplayName(defender)} 宣战！形式: {form}，战争ID: {warId}");

            EventBus.Publish(new WarDeclaredEvent
            {
                WarId = warId,
                Attacker = attacker,
                Defender = defender,
                Form = form,
                AttackerDisplayName = GetDisplayName(attacker),
                DefenderDisplayName = GetDisplayName(defender),
                PreparationEndTime = war.CombatStartTimestamp,
            });

            return warId;
        }

        // ─── Public API: Scoring ──────────────────────────────────────

        /// <summary>
        /// Record a scoring action by a player during an active war.
        /// </summary>
        public void RecordScore(string warId, string playerId, WarScoreType scoreType)
        {
            var war = FindActiveWar(warId);
            if (war == null || war.Phase != WarPhase.Active) return;

            int points = scoreType switch
            {
                WarScoreType.KillDisciple => _config.ScoreKillDisciple,
                WarScoreType.DestroyFlag => _config.ScoreDestroyFlag,
                WarScoreType.KillLeader => _config.ScoreKillLeader,
                WarScoreType.CaptureResourcePoint => _config.ScoreCaptureResourcePoint,
                _ => 0,
            };

            // Determine which side the player is on
            var manager = SectManager.Instance;
            var playerSect = manager.GetCurrentSect(playerId);
            bool isAttacker = false;

            if (!playerSect.HasValue) return;

            if (playerSect.Value == war.Attacker)
                isAttacker = true;
            else if (playerSect.Value == war.Defender)
                isAttacker = false;
            else
                return; // player not part of this war

            // Track per-player score
            if (!war.PlayerScores.TryGetValue(playerId, out var ps))
            {
                ps = new PlayerWarScore { PlayerId = playerId };
                war.PlayerScores[playerId] = ps;
            }

            ps.TotalScore += points;
            switch (scoreType)
            {
                case WarScoreType.KillDisciple: ps.DisciplesKilled++; break;
                case WarScoreType.DestroyFlag: ps.FlagsDestroyed++; break;
                case WarScoreType.KillLeader: ps.LeadersKilled++; break;
                case WarScoreType.CaptureResourcePoint: ps.ResourcePointsCaptured++; break;
            }

            // Update team totals
            if (isAttacker)
                war.AttackerScore += points;
            else
                war.DefenderScore += points;

            Debug.Log($"[SectWarSystem] 战功: {playerId} 获得 {points} 战功 ({scoreType})，当前比分 {war.AttackerScore}:{war.DefenderScore}");

            EventBus.Publish(new WarScoreChangedEvent
            {
                WarId = warId,
                PlayerId = playerId,
                ScoreType = scoreType,
                PointsGained = points,
                AttackerTotal = war.AttackerScore,
                DefenderTotal = war.DefenderScore,
            });
        }

        // ─── Public API: Resource Point Capture ───────────────────────

        /// <summary>
        /// Capture a resource point / territory during a war.
        /// The territory changes ownership only after the war is settled.
        /// </summary>
        public void CaptureResourcePoint(string warId, string territoryId, string capturingPlayerId)
        {
            var war = FindActiveWar(warId);
            if (war == null || war.Phase != WarPhase.Active) return;

            if (!_territories.TryGetValue(territoryId, out var territory))
                return;

            // Check territory belongs to defender or is contested
            if (territory.Owner != war.Defender && territory.Owner != war.Attacker)
                return;

            // Record score for the capture
            RecordScore(warId, capturingPlayerId, WarScoreType.CaptureResourcePoint);

            // Mark territory as contested - will transfer on settlement
            if (!war.ContestedTerritoryIds.Contains(territoryId))
                war.ContestedTerritoryIds.Add(territoryId);

            EventBus.Publish(new ResourcePointCapturedEvent
            {
                TerritoryId = territoryId,
                TerritoryName = territory.DisplayName,
                NewOwner = war.Attacker, // provisional — finalized at settlement
                CapturingPlayerId = capturingPlayerId,
            });

            Debug.Log($"[SectWarSystem] 资源点争夺: {capturingPlayerId} 夺取了 {territory.DisplayName}");
        }

        // ─── Public API: War Tick / Phase Management ─────────────────

        /// <summary>Called periodically to advance war phases.</summary>
        public void TickWars()
        {
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (int i = _activeWars.Count - 1; i >= 0; i--)
            {
                var war = _activeWars[i];

                switch (war.Phase)
                {
                    case WarPhase.Preparation:
                        if (now >= war.CombatStartTimestamp)
                        {
                            EnterCombatPhase(war);
                        }
                        break;

                    case WarPhase.Active:
                        if (war.IsCombatTimeExpired(_config.CombatHours))
                        {
                            EnterSettlementPhase(war);
                        }
                        break;

                    case WarPhase.Settlement:
                        if (!war.SettlementDone)
                        {
                            SettleWar(war);
                        }
                        break;
                }
            }

            // Clean up concluded wars older than 7 days from history
            _warHistory.RemoveAll(w => w.Phase == WarPhase.Concluded &&
                (now - w.SettlementTimestamp) > 7 * 86400);
        }

        /// <summary>Transition from preparation to active combat.</summary>
        private void EnterCombatPhase(SectWarState war)
        {
            war.Phase = WarPhase.Active;
            war.CombatStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Debug.Log($"[SectWarSystem] 交战开始: {GetDisplayName(war.Attacker)} vs {GetDisplayName(war.Defender)}");

            EventBus.Publish(new WarCombatStartedEvent
            {
                WarId = war.WarId,
                Attacker = war.Attacker,
                Defender = war.Defender,
                CombatEndTime = war.CombatStartTimestamp + (long)(_config.CombatHours * 3600),
            });
        }

        /// <summary>Transition from active to settlement phase.</summary>
        private void EnterSettlementPhase(SectWarState war)
        {
            war.Phase = WarPhase.Settlement;
            war.SettlementTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Debug.Log($"[SectWarSystem] 战争结束，进入结算: {GetDisplayName(war.Attacker)} vs {GetDisplayName(war.Defender)}");
        }

        // ─── Public API: Settlement ───────────────────────────────────

        /// <summary>Calculate the result of a war and distribute consequences.</summary>
        public void SettleWar(SectWarState war)
        {
            if (war.SettlementDone) return;

            WarResult result;
            int scoreDiff = war.AttackerScore - war.DefenderScore;

            if (Mathf.Abs(scoreDiff) < 10)
                result = WarResult.Draw;
            else if (scoreDiff > 0)
                result = WarResult.AttackerWin;
            else
                result = WarResult.DefenderWin;

            int compensation = 0;
            var transferredTerritories = new List<string>();

            if (result == WarResult.AttackerWin)
            {
                // Attacker wins: defender pays compensation + loses territories
                compensation = Mathf.Max(
                    _config.MinCompensation,
                    scoreDiff * (int)_config.CompensationMultiplier);

                // Transfer up to TerritoryTransferCount territories from defender to attacker
                if (_sectTerritories.TryGetValue(war.Defender, out var defTerrs))
                {
                    // Try to transfer contested territories first
                    int transferred = 0;
                    foreach (var tId in war.ContestedTerritoryIds)
                    {
                        if (transferred >= _config.TerritoryTransferCount) break;
                        if (_territories.TryGetValue(tId, out var t) && t.Owner == war.Defender)
                        {
                            t.Owner = war.Attacker;
                            transferredTerritories.Add(tId);
                            transferred++;
                        }
                    }

                    // If still need more, take from defender's territories
                    for (int i = defTerrs.Count - 1; i >= 0 && transferred < _config.TerritoryTransferCount; i--)
                    {
                        string tId = defTerrs[i];
                        if (!transferredTerritories.Contains(tId))
                        {
                            if (_territories.TryGetValue(tId, out var t))
                            {
                                t.Owner = war.Attacker;
                                transferredTerritories.Add(tId);
                                transferred++;
                            }
                        }
                    }

                    // Update ownership lists
                    _sectTerritories[war.Attacker].AddRange(transferredTerritories);
                    defTerrs.RemoveAll(t => transferredTerritories.Contains(t));
                }

                // Check if defender has lost all territories → sect destroyed
                if (!_sectTerritories.TryGetValue(war.Defender, out var remaining) || remaining.Count == 0)
                {
                    TriggerSectDestruction(war.Defender, war.Attacker);
                }
            }
            else if (result == WarResult.DefenderWin)
            {
                // Defender wins: attacker pays compensation + loses territories
                compensation = Mathf.Max(
                    _config.MinCompensation,
                    Mathf.Abs(scoreDiff) * (int)_config.CompensationMultiplier);

                if (_sectTerritories.TryGetValue(war.Attacker, out var atkTerrs))
                {
                    int transferred = 0;
                    for (int i = atkTerrs.Count - 1; i >= 0 && transferred < _config.TerritoryTransferCount; i--)
                    {
                        string tId = atkTerrs[i];
                        if (_territories.TryGetValue(tId, out var t))
                        {
                            t.Owner = war.Defender;
                            transferredTerritories.Add(tId);
                            transferred++;
                        }
                    }

                    _sectTerritories[war.Defender].AddRange(transferredTerritories);
                    atkTerrs.RemoveAll(t => transferredTerritories.Contains(t));

                    if (atkTerrs.Count == 0)
                    {
                        TriggerSectDestruction(war.Attacker, war.Defender);
                    }
                }
            }
            else
            {
                // Draw: no compensation, no territory change
                compensation = 0;
            }

            war.SettlementDone = true;
            war.Phase = WarPhase.Concluded;

            // Set cooldown for attacker
            _warCooldowns[war.Attacker] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + _config.WarCooldownDays * 86400L;

            // Remove risk changes
            ApplyWarRiskChanges(war, false);

            // Move to history
            _activeWars.Remove(war);
            _warHistory.Add(war);

            string summary = BuildSettlementSummary(war, result, compensation, transferredTerritories);
            Debug.Log($"[SectWarSystem] {summary}");

            EventBus.Publish(new WarSettledEvent
            {
                WarId = war.WarId,
                Attacker = war.Attacker,
                Defender = war.Defender,
                Result = result,
                AttackerFinalScore = war.AttackerScore,
                DefenderFinalScore = war.DefenderScore,
                CompensationAmount = compensation,
                TerritoriesTransferred = transferredTerritories,
                Summary = summary,
            });
        }

        /// <summary>Trigger the destruction of a sect, scattering its members.</summary>
        private void TriggerSectDestruction(SectType defeated, SectType victor)
        {
            string displayName = GetDisplayName(defeated);

            // Find all members of this sect and scatter them
            var affectedPlayers = new List<string>();
            // NOTE: In a full implementation, iterate over SectManager's player states
            // For now, publish event and let other systems handle it

            Debug.Log($"[SectWarSystem] ⚠ 灭门: {displayName} 被 {GetDisplayName(victor)} 灭门！");

            EventBus.Publish(new SectDestroyedEvent
            {
                Sect = defeated,
                DisplayName = displayName,
                VictorSect = victor,
                AffectedPlayerIds = affectedPlayers,
            });
        }

        // ─── Public API: Diplomacy/Reputation ─────────────────────────

        /// <summary>
        /// Get the reputation value between two sects.
        /// </summary>
        public int GetSectReputation(SectType a, SectType b)
        {
            return _diplomacy.GetReputation(a, b);
        }

        /// <summary>
        /// Get the diplomatic attitude between two sects.
        /// </summary>
        public SectAttitude GetSectAttitude(SectType a, SectType b)
        {
            return _diplomacy.GetAttitude(a, b);
        }

        /// <summary>
        /// Add reputation between two sects. Also applies allied/ enemy reputation linkage.
        /// When sect A gains reputation with sect B, A's allies gain +10, A's enemies gain -20.
        /// </summary>
        public void AddSectReputation(SectType a, SectType b, int delta, string reason = "")
        {
            int oldValue = _diplomacy.GetReputation(a, b);
            _diplomacy.AddReputation(a, b, delta);
            int newValue = _diplomacy.GetReputation(a, b);

            // Reputation linkage: allies of A gain half, enemies of A lose double
            foreach (SectType other in Enum.GetValues(typeof(SectType)))
            {
                if (other == a || other == b || other == SectType.SanXiuLianMeng) continue;

                SectAttitude attitudeToA = _diplomacy.GetAttitude(other, a);
                if (attitudeToA == SectAttitude.Allied)
                {
                    int allyBonus = delta / 2;
                    if (allyBonus != 0)
                    {
                        int oldAlly = _diplomacy.GetReputation(other, b);
                        _diplomacy.AddReputation(other, b, allyBonus);
                        EventBus.Publish(new SectReputationChangedEvent
                        {
                            SectA = other, SectB = b,
                            OldValue = oldAlly, NewValue = _diplomacy.GetReputation(other, b),
                            CurrentAttitude = _diplomacy.GetAttitude(other, b),
                            Reason = $"同盟联动: {GetDisplayName(a)}",
                        });
                    }
                }
                else if (attitudeToA == SectAttitude.Hostile)
                {
                    int enemyPenalty = delta * 2;
                    if (enemyPenalty != 0)
                    {
                        int oldEnemy = _diplomacy.GetReputation(other, b);
                        _diplomacy.AddReputation(other, b, -enemyPenalty);
                        EventBus.Publish(new SectReputationChangedEvent
                        {
                            SectA = other, SectB = b,
                            OldValue = oldEnemy, NewValue = _diplomacy.GetReputation(other, b),
                            CurrentAttitude = _diplomacy.GetAttitude(other, b),
                            Reason = $"敌对联动: {GetDisplayName(a)}",
                        });
                    }
                }
            }

            EventBus.Publish(new SectReputationChangedEvent
            {
                SectA = a, SectB = b,
                OldValue = oldValue, NewValue = newValue,
                CurrentAttitude = _diplomacy.GetAttitude(a, b),
                Reason = reason,
            });

            Debug.Log($"[SectWarSystem] 声望变化: {GetDisplayName(a)} ↔ {GetDisplayName(b)}: {oldValue} → {newValue} ({reason})");
        }

        /// <summary>
        /// When a player joins a sect, apply reputation linkage:
        /// allies of new sect gain +10 rep with player, enemies of new sect lose -20.
        /// </summary>
        public void OnPlayerJoinedSect(string playerId, SectType joinedSect)
        {
            foreach (SectType other in Enum.GetValues(typeof(SectType)))
            {
                if (other == joinedSect || other == SectType.SanXiuLianMeng) continue;

                SectAttitude attitude = _diplomacy.GetAttitude(other, joinedSect);
                if (attitude == SectAttitude.Allied)
                {
                    AddSectReputation(other, joinedSect, 10, $"玩家 {playerId} 加入 {GetDisplayName(joinedSect)}，同盟加成");
                }
                else if (attitude == SectAttitude.Hostile)
                {
                    AddSectReputation(other, joinedSect, -20, $"玩家 {playerId} 加入 {GetDisplayName(joinedSect)}，敌对惩罚");
                }
            }
        }

        // ─── Public API: Spy / Dual Identity ─────────────────────────

        /// <summary>
        /// Activate spy identity for a player. The player appears to be in the cover sect
        /// but is actually loyal to the true sect. Requires a special item (令牌).
        /// </summary>
        public bool ActivateSpyIdentity(string playerId, SectType coverSect, SectType trueSect)
        {
            if (!SectManager.Instance.IsInFormalSect(playerId))
                return false;

            var currentSect = SectManager.Instance.GetCurrentSect(playerId);
            if (!currentSect.HasValue || currentSect.Value != coverSect)
                return false;

            if (coverSect == trueSect)
                return false;

            _spyActiveMap[playerId] = true;
            _spyTrueSectMap[playerId] = trueSect;

            Debug.Log($"[SectWarSystem] 卧底: {playerId} 以 {GetDisplayName(coverSect)} 身份潜伏，实际属于 {GetDisplayName(trueSect)}");

            EventBus.Publish(new SpyIdentityTriggeredEvent
            {
                PlayerId = playerId,
                CoverSect = coverSect,
                TrueSect = trueSect,
                IsExposed = false,
            });

            return true;
        }

        /// <summary>Check if a player is actively a spy.</summary>
        public bool IsSpy(string playerId)
        {
            return _spyActiveMap.TryGetValue(playerId, out var active) && active;
        }

        /// <summary>Get the player's true sect if they are a spy.</summary>
        public SectType? GetSpyTrueSect(string playerId)
        {
            if (IsSpy(playerId) && _spyTrueSectMap.TryGetValue(playerId, out var trueSect))
                return trueSect;
            return null;
        }

        /// <summary>Expose a spy, triggering consequences.</summary>
        public void ExposeSpy(string playerId)
        {
            if (!IsSpy(playerId)) return;

            SectType coverSect = SectManager.Instance.GetCurrentSect(playerId) ?? SectType.SanXiuLianMeng;
            SectType trueSect = _spyTrueSectMap[playerId];

            _spyActiveMap[playerId] = false;

            // Expel from cover sect
            SectManager.Instance.ModifyContribution(playerId, -1000); // force expulsion

            Debug.Log($"[SectWarSystem] 卧底暴露: {playerId} 在 {GetDisplayName(coverSect)} 的卧底身份被揭穿！");

            EventBus.Publish(new SpyIdentityTriggeredEvent
            {
                PlayerId = playerId,
                CoverSect = coverSect,
                TrueSect = trueSect,
                IsExposed = true,
            });
        }

        /// <summary>
        /// Trigger a leader betrayal event. The leader defects to another sect,
        /// causing a sect-wide crisis.
        /// </summary>
        public void TriggerLeaderBetrayal(SectType sect, string leaderPlayerId, string leaderName, string reason)
        {
            Debug.Log($"[SectWarSystem] ⚠ 掌门叛逃: {leaderName} ({GetDisplayName(sect)}) 叛逃！原因: {reason}");

            EventBus.Publish(new LeaderBetrayalEvent
            {
                Sect = sect,
                LeaderPlayerId = leaderPlayerId,
                LeaderPlayerName = leaderName,
                Reason = reason,
                CrisisDurationDays = 7,
            });

            // Auto-scatter members if leader betrayal + no replacement
            // Full implementation would check if there's a successor
        }

        /// <summary>
        /// Handle sect destruction → affected players become 散修.
        /// Called by external systems when processing SectDestroyedEvent.
        /// </summary>
        public void HandleSectDestructionPlayer(string playerId, SectType destroyedSect)
        {
            var manager = SectManager.Instance;
            var state = manager.GetPlayerState(playerId);
            if (state == null) return;

            if (state.CurrentFormalSect == destroyedSect)
            {
                // Player becomes 散修
                state.CurrentFormalSect = null;
                state.Contribution = 0;
                state.Rank = SectRank.OuterDisciple;
                state.IsInSanctionAlliance = true;

                Debug.Log($"[SectWarSystem] 门派被灭: {playerId} 被迫转为散修。");

                EventBus.Publish(new SectLeftEvent
                {
                    PlayerId = playerId,
                    PreviousSect = destroyedSect,
                    LeaveType = LeaveType.Forced,
                    RetainedContribution = 0,
                });
            }
        }

        // ─── Public API: Queries ─────────────────────────────────────

        /// <summary>Get all active wars.</summary>
        public List<SectWarState> GetActiveWars()
        {
            return new List<SectWarState>(_activeWars);
        }

        /// <summary>Get war history (completed wars).</summary>
        public List<SectWarState> GetWarHistory()
        {
            return new List<SectWarState>(_warHistory);
        }

        /// <summary>Find an active war by ID.</summary>
        public SectWarState FindActiveWar(string warId)
        {
            return _activeWars.Find(w => w.WarId == warId);
        }

        /// <summary>Find an active war involving a specific sect.</summary>
        public SectWarState FindWarBySect(SectType sect)
        {
            return _activeWars.Find(w =>
                (w.Attacker == sect || w.Defender == sect) &&
                (w.Phase == WarPhase.Active || w.Phase == WarPhase.Preparation));
        }

        /// <summary>Check if a sect is currently in an active war.</summary>
        public bool IsSectAtWar(SectType sect)
        {
            return FindWarBySect(sect) != null;
        }

        /// <summary>Get all territories owned by a sect.</summary>
        public List<SectTerritory> GetSectTerritories(SectType sect)
        {
            var result = new List<SectTerritory>();
            if (_sectTerritories.TryGetValue(sect, out var ids))
            {
                foreach (var id in ids)
                {
                    if (_territories.TryGetValue(id, out var t))
                        result.Add(t);
                }
            }
            return result;
        }

        /// <summary>Get the reputation level of a sect (for war declaration check).</summary>
        public int GetSectRepLevel(SectType sect)
        {
            // This would be calculated from overall sect standing
            // For now, return a basic estimate based on territories + diplomacy
            if (_sectTerritories.TryGetValue(sect, out var terr))
                return Mathf.Clamp(terr.Count + 1, 1, 5);
            return 1;
        }

        /// <summary>Get the risk level for a zone, factoring in active wars.</summary>
        public RiskLevel GetEffectiveRiskLevel(string zoneId, RiskLevel baseLevel)
        {
            foreach (var war in _activeWars)
            {
                if (war.Phase != WarPhase.Active && war.Phase != WarPhase.Preparation)
                    continue;

                if (war.AttackerRiskZoneIds.Contains(zoneId) || war.DefenderRiskZoneIds.Contains(zoneId))
                {
                    // Shift risk by one level during war
                    int baseInt = (int)baseLevel;
                    int warInt = Mathf.Min(baseInt + 1, (int)RiskLevel.Extreme);
                    return (RiskLevel)warInt;
                }
            }
            return baseLevel;
        }

        // ─── Internal Helpers ────────────────────────────────────────

        /// <summary>Get territories on the border between two sects.</summary>
        private List<string> GetBorderTerritories(SectType a, SectType b)
        {
            // Simplified: return territories of the defender that are "at risk"
            var result = new List<string>();
            if (_sectTerritories.TryGetValue(b, out var defTerrs))
            {
                result.AddRange(defTerrs);
            }
            return result;
        }

        /// <summary>Get zone IDs belonging to a sect's territories.</summary>
        private List<string> GetSectZoneIds(SectType sect)
        {
            var zones = new List<string>();
            if (_sectTerritories.TryGetValue(sect, out var terrIds))
            {
                foreach (var tid in terrIds)
                {
                    if (_territories.TryGetValue(tid, out var terr))
                    {
                        if (!zones.Contains(terr.RegionId))
                            zones.Add(terr.RegionId);
                    }
                }
            }
            return zones;
        }

        /// <summary>Apply or remove war risk level modifiers.</summary>
        private void ApplyWarRiskChanges(SectWarState war, bool applying)
        {
            foreach (var zoneId in war.AttackerRiskZoneIds)
            {
                EventBus.Publish(new WarZoneRiskChangedEvent
                {
                    ZoneId = zoneId,
                    IsWarActive = applying,
                });
            }
            foreach (var zoneId in war.DefenderRiskZoneIds)
            {
                EventBus.Publish(new WarZoneRiskChangedEvent
                {
                    ZoneId = zoneId,
                    IsWarActive = applying,
                });
            }
        }

        /// <summary>Build a Chinese settlement summary string.</summary>
        private string BuildSettlementSummary(SectWarState war, WarResult result, int compensation, List<string> transferred)
        {
            string atkName = GetDisplayName(war.Attacker);
            string defName = GetDisplayName(war.Defender);

            string resultStr = result switch
            {
                WarResult.AttackerWin => $"{atkName} 获胜",
                WarResult.DefenderWin => $"{defName} 获胜",
                WarResult.Draw => "平局",
                _ => "取消",
            };

            string compStr = compensation > 0
                ? $"赔偿 {compensation} 灵石"
                : "无需赔偿";

            string terrStr = transferred.Count > 0
                ? $"，领地转移: {string.Join(", ", transferred.ConvertAll(t => _territories.TryGetValue(t, out var terr) ? terr.DisplayName : t))}"
                : "，无领地变更";

            return $"战争结算: {atkName} vs {defName} — {resultStr}，比分 {war.AttackerScore}:{war.DefenderScore}，{compStr}{terrStr}";
        }

        /// <summary>Get the Chinese display name for a sect type.</summary>
        public string GetDisplayName(SectType sect)
        {
            return sect switch
            {
                SectType.TianYuanZong => "天元宗",
                SectType.QingYunMen => "青云门",
                SectType.ShangMeng => "商盟",
                SectType.YuShouYiZu => "御兽遗族",
                SectType.SanXiuLianMeng => "散修联盟",
                _ => sect.ToString(),
            };
        }

        // ─── Save / Load Support ──────────────────────────────────────

        /// <summary>Get serializable war state for save system.</summary>
        public List<SectWarState> GetWarsForSave()
        {
            return _activeWars;
        }

        /// <summary>Restore war state from save data.</summary>
        public void RestoreWars(List<SectWarState> wars)
        {
            if (wars != null)
                _activeWars = wars;
        }

        /// <summary>Get diplomacy state for save system.</summary>
        public SectDiplomacyState GetDiplomacyState()
        {
            return _diplomacy;
        }

        /// <summary>Restore diplomacy from save data.</summary>
        public void RestoreDiplomacy(SectDiplomacyState state)
        {
            if (state != null)
                _diplomacy = state;
        }

        /// <summary>Get territory ownership for save system.</summary>
        public Dictionary<SectType, List<string>> GetTerritoryOwnership()
        {
            return _sectTerritories;
        }

        /// <summary>Restore territory ownership from save data.</summary>
        public void RestoreTerritoryOwnership(Dictionary<SectType, List<string>> ownership)
        {
            if (ownership != null)
                _sectTerritories = ownership;
        }
    }
}
