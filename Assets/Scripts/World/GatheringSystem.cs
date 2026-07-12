using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.World
{
    #region Enums & Data Structures

    /// <summary>Types of resource nodes in the world.</summary>
    public enum ResourceNodeType
    {
        Common,         // 普通灵材
        Rare,           // 稀有灵材
        SpiritSpring,   // 灵泉
        MineralVein,    // 矿脉
        CelestialHerb   // 天材地宝
    }

    /// <summary>Categories for tool-material matching.</summary>
    public enum HerbCategory
    {
        Herb,     // 草本类 (草药、灵花)
        Woody,    // 木本类 (灵木、树皮)
        Mineral,  // 矿物类 (矿石、结晶)
        Liquid,   // 液体类 (灵泉、露水)
        Special   // 特殊类 (兽骨、龙鳞、古物)
    }

    /// <summary>Quality tiers for gathering tools.</summary>
    public enum ToolQuality
    {
        Basic,     // 基础 — 无加成
        Fine,      // 精良 — 速度+30%, 暴击率+5%
        Legendary  // 传说 — 速度+60%, 暴击率+15%, 稀有产出率+10%
    }

    /// <summary>Perception visual level for resource nodes.</summary>
    public enum PerceptionLevel
    {
        None,       // 不可感知
        FaintGlow,  // 微弱绿色光点 (低级)
        StableGlow, // 稳定绿色光晕 (中级)
        PulsingGlow,// 强烈脉动绿光 (高级)
        LightPillar // 冲天光柱 (天材地宝)
    }

    /// <summary>Tool data for a specific herb category.</summary>
    [Serializable]
    public struct GatheringToolEntry
    {
        public HerbCategory Category;
        public string ToolName;            // e.g., "玉锄头", "寒铁镐"
        public bool IsRequired;            // true = cannot gather without this tool
        public float SpeedMultiplier;      // 1.0 = base
        public float CritBonus;            // added to crit chance
        public float SuccessBonus;         // added to success chance
    }

    /// <summary>A resource node in the game world.</summary>
    [Serializable]
    public class ResourceNode
    {
        public string Id;
        public string DisplayName;
        public ResourceNodeType NodeType;
        public HerbCategory Category;
        public Vector3 Position;
        public string RegionId;
        public int StageRequired;          // minimum cultivation stage to perceive
        public int MaxGatherCount;         // how many times before depletion
        public int CurrentGatherCount;
        public float GatherDifficulty;     // 0.0 (easy) to 1.0 (extremely hard)
        public float BaseQuantity;         // base output per gather
        public string RequiredToolName;    // tool name if any
        public bool HasGuardian;           // has guardian beast
        public float RespawnTimeDays;      // current respawn timer in game days
        public float BaseRespawnTimeMin;   // minimum respawn range
        public float BaseRespawnTimeMax;   // maximum respawn range
        public bool IsDepleted;
        public float RespawnTimer;          // current respawn countdown accumulator
        public bool RequiresActivePerception; // rare nodes need active scan
        public float PerceptionRadiusOverride; // 0 = use default
        
        /// <summary>How the node appears to the player's perception.</summary>
        public PerceptionLevel GetPerceptionLevel()
        {
            switch (NodeType)
            {
                case ResourceNodeType.Common:       return PerceptionLevel.FaintGlow;
                case ResourceNodeType.Rare:         return PerceptionLevel.PulsingGlow;
                case ResourceNodeType.SpiritSpring: return PerceptionLevel.StableGlow;
                case ResourceNodeType.MineralVein:  return PerceptionLevel.StableGlow;
                case ResourceNodeType.CelestialHerb:return PerceptionLevel.LightPillar;
                default: return PerceptionLevel.None;
            }
        }
    }

    /// <summary>Player's gathering proficiency snapshot.</summary>
    [Serializable]
    public class GatheringProficiency
    {
        public int Level = 1;
        public float CurrentExp;
        public float ExpToNext = 100f;

        public float SpeedBonus => 1.0f + (Level - 1) * 0.02f;     // +2% per level
        public float QuantityBonus => 1.0f + (Level - 1) * 0.01f;   // +1% per level
        public float SuccessBonus => Mathf.Min((Level - 1) * 0.005f, 0.3f); // +0.5% per level, cap 30%
        public float CritBonus => Mathf.Min((Level - 1) * 0.002f, 0.15f);   // +0.2% per level, cap 15%

        public void AddExp(float amount)
        {
            CurrentExp += amount;
            while (CurrentExp >= ExpToNext && Level < 100)
            {
                CurrentExp -= ExpToNext;
                Level++;
                ExpToNext = 100f + Level * 20f; // scaling exp curve
                OnLevelUp?.Invoke(Level);
            }
        }

        public System.Action<int> OnLevelUp;
    }

    #endregion

    /// <summary>
    /// Main gathering system for EarthOnline.
    /// Handles perception, gathering progress, tool matching, respawn, proficiency, and announcements.
    /// </summary>
    public class GatheringSystem : MonoBehaviour
    {
        #region Singleton

        public static GatheringSystem Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Perception Settings")]
        [SerializeField] private float basePerceptionRadius = 10f;
        [SerializeField] private float perceptionBonusPerStage = 5f;
        [SerializeField] private float passivePerceptionInterval = 2f;

        [Header("Gathering Settings")]
        [SerializeField] private float gatherBaseDuration = 3f;          // seconds for basic gather
        [SerializeField] private float gatherBaseChance = 0.9f;
        [SerializeField] private float gatherCritChance = 0.1f;
        [SerializeField] private float gatherCritMultiplier = 2f;
        [SerializeField] private int maxGatherPerNode = 5;

        [Header("Respawn Settings (game days)")]
        [SerializeField] private float commonRespawnMin = 1f;
        [SerializeField] private float commonRespawnMax = 3f;
        [SerializeField] private float rareRespawnMin = 3f;
        [SerializeField] private float rareRespawnMax = 7f;

        [Header("Tool Matching Table")]
        [SerializeField] private List<GatheringToolEntry> toolTable;

        #endregion

        #region Private State

        // All active resource nodes in the world.
        private Dictionary<string, ResourceNode> _activeNodes = new Dictionary<string, ResourceNode>();

        // Depleted nodes awaiting respawn.
        private List<ResourceNode> _depletedNodes = new List<ResourceNode>();

        // Currently selected target for gathering.
        private ResourceNode _currentTarget;

        // Gathering progress.
        private float _gatherProgress;
        private float _gatherDuration;
        private bool _isGathering;

        // Perception.
        private bool _isPerceptionActive;
        private float _perceptionTimer;
        private List<ResourceNode> _perceivedNodes = new List<ResourceNode>();

        // Proficiency.
        private GatheringProficiency _proficiency = new GatheringProficiency();

        // Player state (to be hooked up to player system later).
        private int _playerStage = 1;           // current cultivation stage
        private string _equippedToolName = "";
        private ToolQuality _equippedToolQuality = ToolQuality.Basic;
        private Vector3 _playerPosition;
        private string _playerRegion = "default";
        private string _playerName = "Player";

        // Game time tracking (simplified — real game time integration later).
        private float _gameDaysElapsed;

        #endregion

        #region Public Properties

        public bool IsPerceptionActive => _isPerceptionActive;
        public bool IsGathering => _isGathering;
        public float GatherProgress => _gatherProgress;
        public ResourceNode CurrentTarget => _currentTarget;
        public GatheringProficiency Proficiency => _proficiency;
        public int PlayerStage { get => _playerStage; set => _playerStage = Mathf.Max(1, value); }
        public string EquippedTool { get => _equippedToolName; set => _equippedToolName = value; }
        public ToolQuality EquippedToolQuality { get => _equippedToolQuality; set => _equippedToolQuality = value; }
        public Vector3 PlayerPosition { get => _playerPosition; set => _playerPosition = value; }
        public string PlayerRegion { get => _playerRegion; set => _playerRegion = value; }
        public string PlayerName { get => _playerName; set => _playerName = value; }
        public IReadOnlyDictionary<string, ResourceNode> ActiveNodes => _activeNodes;
        public IReadOnlyList<ResourceNode> PerceivedNodes => _perceivedNodes;

        /// <summary>Current perception radius based on stage.</summary>
        public float PerceptionRadius => basePerceptionRadius + _playerStage * perceptionBonusPerStage;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize default tool table if not set in inspector.
            if (toolTable == null || toolTable.Count == 0)
            {
                toolTable = new List<GatheringToolEntry>
                {
                    new GatheringToolEntry { Category = HerbCategory.Herb,    ToolName = "玉锄头",   IsRequired = "false", SpeedMultiplier = "1.0f", CritBonus = "0",    SuccessBonus = 0 },
                    new GatheringToolEntry { Category = HerbCategory.Woody,   ToolName = "采木锯",   IsRequired = "true",  SpeedMultiplier = "1.0f", CritBonus = "0",    SuccessBonus = 0 },
                    new GatheringToolEntry { Category = HerbCategory.Mineral, ToolName = "寒铁镐",   IsRequired = "true",  SpeedMultiplier = "1.0f", CritBonus = "0",    SuccessBonus = 0 },
                    new GatheringToolEntry { Category = HerbCategory.Liquid,  ToolName = "玉瓶",     IsRequired = "true",  SpeedMultiplier = "1.0f", CritBonus = "0",    SuccessBonus = 0 },
                    new GatheringToolEntry { Category = HerbCategory.Special, ToolName = "",         IsRequired = "false", SpeedMultiplier = "1.0f", CritBonus = "0",    SuccessBonus = 0 },
                };
            }

            // Subscribe to events this system cares about.
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
                Instance = null;
            }
        }

        private void Update()
        {
            // Advance game time (simplified: real-time simulation).
            // In production, this would be driven by the server/game-time system.
            _gameDaysElapsed += Time.deltaTime / 86400f;

            // Update perception detection.
            UpdatePerception();

            // Update respawn timers.
            UpdateRespawnTimers();

            // Update active gathering progress.
            if (_isGathering)
            {
                UpdateGatheringProgress();
            }
        }

        #endregion

        #region Perception

        /// <summary>
        /// Activate or deactivate the perception (cultivation method running).
        /// </summary>
        public void SetPerceptionActive(bool active)
        {
            if (_isPerceptionActive == active) return;

            _isPerceptionActive = active;
            _perceivedNodes.Clear();

            if (_isPerceptionActive)
            {
                // Immediate scan on activation.
                ScanForResources();
            }

            EventBus.Publish(new PerceptionStateChangedEvent
            {
                IsActive = _isPerceptionActive,
                CurrentRadius = PerceptionRadius,
                ResourcesDetected = _perceivedNodes.Count
            });

            Debug.Log($"[GatheringSystem] Perception {(active ? "activated" : "deactivated")}. " +
                      $"Radius: {PerceptionRadius}m, Detected: {_perceivedNodes.Count}");
        }

        /// <summary>
        /// Force a perception scan (for active skill "神识探查").
        /// </summary>
        public void ActivePerceptionScan()
        {
            ScanForResources();

            EventBus.Publish(new PerceptionStateChangedEvent
            {
                IsActive = _isPerceptionActive,
                CurrentRadius = PerceptionRadius,
                ResourcesDetected = _perceivedNodes.Count
            });

            Debug.Log($"[GatheringSystem] Active perception scan complete. Found {_perceivedNodes.Count} resources.");
        }

        private void UpdatePerception()
        {
            if (!_isPerceptionActive) return;

            // Periodic passive re-scan every _passivePerceptionInterval seconds.
            _perceptionTimer += Time.deltaTime;
            if (_perceptionTimer >= passivePerceptionInterval)
            {
                _perceptionTimer = 0f;
                ScanForResources();
            }
        }

        private void ScanForResources()
        {
            _perceivedNodes.Clear();

            float radius = PerceptionRadius;
            float radiusSq = radius * radius;

            foreach (var kvp in _activeNodes)
            {
                ResourceNode node = kvp.Value;
                if (node.IsDepleted) continue;

                // Check distance.
                float distSq = (node.Position - _playerPosition).sqrMagnitude;
                if (distSq > radiusSq) continue;

                // Check stage requirement.
                if (_playerStage < node.StageRequired) continue;

                // Check if node requires active perception and is currently not active-detected.
                if (node.RequiresActivePerception && !_isPerceptionActive) continue;

                // Check perception level - faint nodes may be missed in passive scan.
                PerceptionLevel level = node.GetPerceptionLevel();
                if (!_isPerceptionActive && level == PerceptionLevel.FaintGlow)
                {
                    // Passive scan has a chance to miss faint nodes.
                    if (UnityEngine.Random.value > 0.7f) continue;
                }

                _perceivedNodes.Add(node);

                // Notify discovery if this is a new detection.
                bool isRare = node.NodeType == ResourceNodeType.Rare ||
                              node.NodeType == ResourceNodeType.CelestialHerb;

                EventBus.Publish(new ResourceDiscoveredEvent
                {
                    NodeId = node.Id,
                    NodeName = node.DisplayName,
                    RegionId = node.RegionId,
                    IsRare = isRare
                });
            }
        }

        /// <summary>
        /// Get the perception visual level for a specific node from the player's perspective.
        /// </summary>
        public PerceptionLevel GetNodePerceptionLevel(string nodeId)
        {
            if (_activeNodes.TryGetValue(nodeId, out ResourceNode node) && !node.IsDepleted)
            {
                return node.GetPerceptionLevel();
            }
            return PerceptionLevel.None;
        }

        #endregion

        #region Gathering

        /// <summary>
        /// Player pressed the interact key (F) near a resource node.
        /// Returns true if gathering started.
        /// </summary>
        public bool TryStartGather(ResourceNode node)
        {
            if (node == null || node.IsDepleted)
            {
                Debug.LogWarning("[GatheringSystem] Cannot gather: node is depleted or null.");
                return false;
            }

            if (_isGathering)
            {
                Debug.LogWarning("[GatheringSystem] Already gathering.");
                return false;
            }

            // Check tool requirement.
            if (!string.IsNullOrEmpty(node.RequiredToolName))
            {
                if (!_equippedToolName.Contains(node.RequiredToolName) &&
                    !node.RequiredToolName.Contains(_equippedToolName))
                {
                    string failReason = $"需要{node.RequiredToolName}";
                    EventBus.Publish(new GatheringFailedEvent
                    {
                        NodeId = node.Id,
                        NodeName = node.DisplayName,
                        FailReason = failReason
                    });
                    Debug.Log($"[GatheringSystem] {failReason} for {node.DisplayName}");
                    return false;
                }
            }

            // Check general tool matching from tool table.
            GatheringToolEntry toolEntry = GetToolEntry(node.Category);
            if (toolEntry.IsRequired)
            {
                bool hasTool = _equippedToolName == toolEntry.ToolName;
                if (!hasTool)
                {
                    string failReason = $"需要{toolEntry.ToolName}";
                    EventBus.Publish(new GatheringFailedEvent
                    {
                        NodeId = node.Id,
                        NodeName = node.DisplayName,
                        FailReason = failReason
                    });
                    Debug.Log($"[GatheringSystem] {failReason} for {node.DisplayName}");
                    return false;
                }
            }

            // Check if node has guardian and player hasn't defeated it yet.
            if (node.HasGuardian)
            {
                string failReason = "有守护妖兽，需先击败";
                EventBus.Publish(new GatheringFailedEvent
                {
                    NodeId = node.Id,
                    NodeName = node.DisplayName,
                    FailReason = failReason
                });
                Debug.Log($"[GatheringSystem] {failReason} before gathering {node.DisplayName}");
                return false;
            }

            // Start gathering.
            _currentTarget = node;
            _gatherProgress = 0f;

            // Calculate gather duration based on tools and proficiency.
            float speedMultiplier = 1f;
            if (toolEntry.IsRequired || !string.IsNullOrEmpty(toolEntry.ToolName))
            {
                speedMultiplier = toolEntry.SpeedMultiplier;
                if (_equippedToolName == toolEntry.ToolName)
                {
                    // Apply tool quality bonuses.
                    switch (_equippedToolQuality)
                    {
                        case ToolQuality.Fine:      speedMultiplier *= 1.3f; break;
                        case ToolQuality.Legendary: speedMultiplier *= 1.6f; break;
                    }
                }
            }
            // Proficiency speed bonus.
            speedMultiplier *= _proficiency.SpeedBonus;

            _gatherDuration = gatherBaseDuration / speedMultiplier;
            _gatherDuration = Mathf.Max(_gatherDuration, 0.5f); // minimum 0.5s

            _isGathering = true;

            EventBus.Publish(new GatheringStartedEvent
            {
                NodeId = node.Id,
                NodeName = node.DisplayName,
                RegionId = node.RegionId,
                TotalDuration = _gatherDuration
            });

            Debug.Log($"[GatheringSystem] Started gathering {node.DisplayName}. Duration: {_gatherDuration:F1}s");
            return true;
        }

        /// <summary>
        /// Try to start gathering by node ID.
        /// </summary>
        public bool TryStartGatherById(string nodeId)
        {
            if (_activeNodes.TryGetValue(nodeId, out ResourceNode node))
                return TryStartGather(node);
            return false;
        }

        /// <summary>
        /// Interrupt the current gathering action (e.g., player takes damage).
        /// </summary>
        public void InterruptGathering(string reason = "受攻击打断")
        {
            if (!_isGathering || _currentTarget == null) return;

            _isGathering = false;
            _gatherProgress = 0f;

            EventBus.Publish(new GatheringInterruptedEvent
            {
                NodeId = _currentTarget.Id,
                Reason = reason
            });

            Debug.Log($"[GatheringSystem] Gathering interrupted: {reason}");
            _currentTarget = null;
        }

        private void UpdateGatheringProgress()
        {
            _gatherProgress += Time.deltaTime / _gatherDuration;

            EventBus.Publish(new GatheringProgressEvent
            {
                NodeId = _currentTarget.Id,
                Progress = Mathf.Clamp01(_gatherProgress),
                Elapsed = _gatherProgress * _gatherDuration
            });

            if (_gatherProgress >= 1f)
            {
                CompleteGathering();
            }
        }

        private void CompleteGathering()
        {
            if (_currentTarget == null) return;

            ResourceNode node = _currentTarget;
            _isGathering = false;

            // --- Success/Failure Calculation ---

            // Determine tool entry.
            GatheringToolEntry toolEntry = GetToolEntry(node.Category);
            bool hasCorrectTool = _equippedToolName == toolEntry.ToolName;

            // Calculate roll bonuses.
            float toolBonus = hasCorrectTool ? toolEntry.SuccessBonus : 0f;
            if (hasCorrectTool)
            {
                switch (_equippedToolQuality)
                {
                    case ToolQuality.Fine:      toolBonus += 0.05f; break;
                    case ToolQuality.Legendary: toolBonus += 0.10f; break;
                }
            }
            else
            {
                // Penalty for not using the right tool on herbaceous.
                if (node.Category == HerbCategory.Herb && !hasCorrectTool)
                {
                    toolBonus = -0.3f; // speed penalty is handled separately, this is success penalty
                }
            }

            float successChance = gatherBaseChance +
                                  toolBonus +
                                  _proficiency.SuccessBonus -
                                  node.GatherDifficulty;

            // Hard block if success chance too low.
            if (successChance < 0f)
            {
                EventBus.Publish(new GatheringFailedEvent
                {
                    NodeId = node.Id,
                    NodeName = node.DisplayName,
                    FailReason = "采集难度过高，需提升工具或境界"
                });
                Debug.Log($"[GatheringSystem] Cannot gather {node.DisplayName}: success chance {successChance:F2} < 0");
                _currentTarget = null;
                return;
            }

            bool success = UnityEngine.Random.value < successChance;

            if (!success)
            {
                // Failed — get half or nothing.
                int partialQty = node.BaseQuantity > 1 ? Mathf.CeilToInt(node.BaseQuantity * 0.5f) : 0;

                EventBus.Publish(new GatheringFailedEvent
                {
                    NodeId = node.Id,
                    NodeName = node.DisplayName,
                    FailReason = partialQty > 0 ? "采集部分成功" : "采集失败"
                });

                if (partialQty > 0)
                {
                    // Still consumed the node usage.
                    ConsumeNodeGather(node, partialQty, false);
                }

                _proficiency.AddExp(2f);
                _currentTarget = null;
                return;
            }

            // --- Success ---

            // Calculate output quantity.
            float toolMult = hasCorrectTool ? 1.0f : 0.5f; // halved without correct tool
            float baseQty = node.BaseQuantity * toolMult * _proficiency.QuantityBonus;
            int quantity = Mathf.Max(1, Mathf.RoundToInt(baseQty));

            // Crit check.
            float critChance = gatherCritChance + toolEntry.CritBonus + _proficiency.CritBonus;
            if (hasCorrectTool)
            {
                switch (_equippedToolQuality)
                {
                    case ToolQuality.Fine:      critChance += 0.05f; break;
                    case ToolQuality.Legendary: critChance += 0.15f; break;
                }
            }

            bool isCrit = UnityEngine.Random.value < critChance;
            if (isCrit)
            {
                quantity = Mathf.RoundToInt(quantity * gatherCritMultiplier);
            }

            // Consume one gather from the node.
            bool depleted = ConsumeNodeGather(node, quantity, true);

            // Proficiency gain.
            float profGain = 5f;
            if (isCrit) profGain += 3f;
            if (node.NodeType == ResourceNodeType.Rare || node.NodeType == ResourceNodeType.CelestialHerb)
                profGain += 5f;
            _proficiency.AddExp(profGain);

            // Publish completion event.
            EventBus.Publish(new GatheringCompletedEvent
            {
                NodeId = node.Id,
                NodeName = node.DisplayName,
                Quantity = quantity,
                IsCrit = isCrit,
                ProficiencyGained = profGain
            });

            Debug.Log($"[GatheringSystem] Gathered {node.DisplayName} x{quantity}" +
                      (isCrit ? " (暴击!)" : "") +
                      (depleted ? " [资源枯竭]" : ""));

            // Celestial herb — area broadcast.
            if (node.NodeType == ResourceNodeType.CelestialHerb)
            {
                EventBus.Publish(new CelestialHerbGatheredEvent
                {
                    NodeName = node.DisplayName,
                    PlayerName = _playerName,
                    RegionId = node.RegionId
                });
                Debug.Log($"[GatheringSystem] !! 天材地宝公告: {_playerName} 在 {node.RegionId} 采集了 {node.DisplayName} !!");
            }

            _currentTarget = null;
        }

        /// <summary>
        /// Consume one gather from a node. Returns true if node is now depleted.
        /// </summary>
        private bool ConsumeNodeGather(ResourceNode node, int quantity, bool success)
        {
            node.CurrentGatherCount++;

            // In production, this would add items to the player's inventory.
            // For now we just track the resource usage.
            Debug.Log($"[GatheringSystem] {(success ? "Obtained" : "Partial")} {node.DisplayName} x{quantity} (gather {node.CurrentGatherCount}/{node.MaxGatherCount})");

            if (node.CurrentGatherCount >= node.MaxGatherCount)
            {
                DepleteNode(node);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Deplete a resource node and start its respawn timer.
        /// </summary>
        private void DepleteNode(ResourceNode node)
        {
            node.IsDepleted = true;
            _depletedNodes.Add(node);

            // Set respawn time based on node type.
            float respawnDays;
            if (node.NodeType == ResourceNodeType.Common)
            {
                respawnDays = UnityEngine.Random.Range(commonRespawnMin, commonRespawnMax);
            }
            else
            {
                respawnDays = UnityEngine.Random.Range(rareRespawnMin, rareRespawnMax);
            }
            node.RespawnTimeDays = respawnDays;
            node.RespawnTimer = 0f;

            EventBus.Publish(new ResourceDepletedEvent
            {
                NodeId = node.Id,
                NodeName = node.DisplayName,
                RespawnTimeDays = respawnDays
            });

            Debug.Log($"[GatheringSystem] Node {node.DisplayName} depleted. Respawn in {respawnDays:F1} game days.");
        }

        #endregion

        #region Respawn

        private void UpdateRespawnTimers()
        {
            // Game time delta per frame.
            float deltaDays = Time.deltaTime / 86400f;

            for (int i = _depletedNodes.Count - 1; i >= 0; i--)
            {
                ResourceNode node = _depletedNodes[i];
                node.RespawnTimer += deltaDays;

                if (node.RespawnTimer >= node.RespawnTimeDays)
                {
                    // Respawn the node.
                    node.IsDepleted = false;
                    node.CurrentGatherCount = 0;
                    node.RespawnTimer = 0f;
                    _depletedNodes.RemoveAt(i);

                    EventBus.Publish(new ResourceRespawnedEvent
                    {
                        NodeId = node.Id,
                        NodeName = node.DisplayName,
                        RegionId = node.RegionId
                    });

                    Debug.Log($"[GatheringSystem] Node {node.DisplayName} respawned in {node.RegionId}.");
                }
            }
        }

        #endregion

        #region Node Management

        /// <summary>
        /// Register a resource node in the world.
        /// </summary>
        public string RegisterNode(ResourceNode node)
        {
            if (string.IsNullOrEmpty(node.Id))
            {
                node.Id = $"node_{node.RegionId}_{node.DisplayName}_{Guid.NewGuid():N}";
            }

            _activeNodes[node.Id] = node;
            return node.Id;
        }

        /// <summary>
        /// Unregister and remove a node completely.
        /// </summary>
        public void UnregisterNode(string nodeId)
        {
            _activeNodes.Remove(nodeId);
            _depletedNodes.RemoveAll(n => n.Id == nodeId);
        }

        /// <summary>
        /// Get a node by ID.
        /// </summary>
        public ResourceNode GetNode(string nodeId)
        {
            _activeNodes.TryGetValue(nodeId, out ResourceNode node);
            return node;
        }

        /// <summary>
        /// Get all nodes in a specific region.
        /// </summary>
        public List<ResourceNode> GetNodesInRegion(string regionId)
        {
            List<ResourceNode> results = new List<ResourceNode>();
            foreach (var kvp in _activeNodes)
            {
                if (kvp.Value.RegionId == regionId && !kvp.Value.IsDepleted)
                    results.Add(kvp.Value);
            }
            return results;
        }

        /// <summary>
        /// Find the nearest resource node to a position that the player can interact with.
        /// </summary>
        public ResourceNode FindNearestInteractable(Vector3 position, float maxDistance)
        {
            ResourceNode nearest = null;
            float nearestDist = maxDistance * maxDistance;

            foreach (var kvp in _activeNodes)
            {
                ResourceNode node = kvp.Value;
                if (node.IsDepleted) continue;

                float distSq = (node.Position - position).sqrMagnitude;
                if (distSq < nearestDist)
                {
                    nearestDist = distSq;
                    nearest = node;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Mark a guardian as defeated for a specific node, allowing gathering.
        /// </summary>
        public bool DefeatGuardian(string nodeId)
        {
            if (_activeNodes.TryGetValue(nodeId, out ResourceNode node))
            {
                node.HasGuardian = false;
                Debug.Log($"[GatheringSystem] Guardian defeated for {node.DisplayName}. Gathering now possible.");
                return true;
            }
            return false;
        }

        #endregion

        #region Tool Matching

        /// <summary>
        /// Get the tool entry for a given herb category.
        /// </summary>
        public GatheringToolEntry GetToolEntry(HerbCategory category)
        {
            foreach (var entry in toolTable)
            {
                if (entry.Category == category)
                    return entry;
            }
            return default;
        }

        /// <summary>
        /// Check if the player's equipped tool matches the required tool for a category.
        /// Returns (hasTool, toolName, isRequired) tuple.
        /// </summary>
        public (bool hasTool, string toolName, bool isRequired) CheckToolRequirement(HerbCategory category)
        {
            GatheringToolEntry entry = GetToolEntry(category);
            bool hasTool = string.IsNullOrEmpty(entry.ToolName) || _equippedToolName == entry.ToolName;
            return (hasTool, entry.ToolName, entry.IsRequired);
        }

        #endregion

        #region Proficiency

        /// <summary>
        /// Get the current proficiency level.
        /// </summary>
        public int GetProficiencyLevel() => _proficiency.Level;

        /// <summary>
        /// Get the proficiency title based on level.
        /// </summary>
        public string GetProficiencyTitle()
        {
            if (_proficiency.Level <= 10)   return "采集学徒";
            if (_proficiency.Level <= 25)   return "采集工匠";
            if (_proficiency.Level <= 45)   return "采集大师";
            if (_proficiency.Level <= 70)   return "采集宗师";
            if (_proficiency.Level <= 90)   return "采集圣手";
            return "采集传说";
        }

        /// <summary>
        /// Get the exp progress as a 0-1 value.
        /// </summary>
        public float GetProficiencyProgress() => _proficiency.CurrentExp / _proficiency.ExpToNext;

        /// <summary>
        /// Register a callback for proficiency level up.
        /// </summary>
        public void OnProficiencyLevelUp(Action<int> callback)
        {
            _proficiency.OnLevelUp += callback;
        }

        #endregion

        #region Event Handlers

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            // Boss defeat could potentially spawn new rare resources or respawn celestial herbs.
            // Placeholder for future integration.
            Debug.Log($"[GatheringSystem] Boss {evt.BossName} defeated. Potential resource impact.");
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>
        /// Create a test resource node (for debug/editor use).
        /// </summary>
        public string CreateTestNode(string name, ResourceNodeType type, Vector3 position, string region = "test_region")
        {
            var node = new ResourceNode
            {
                DisplayName = name,
                NodeType = type,
                Category = type == ResourceNodeType.MineralVein ? HerbCategory.Mineral :
                           type == ResourceNodeType.SpiritSpring ? HerbCategory.Liquid :
                           type == ResourceNodeType.CelestialHerb ? HerbCategory.Herb :
                           HerbCategory.Herb,
                Position = position,
                RegionId = region,
                StageRequired = type == ResourceNodeType.CelestialHerb ? 5 : 1,
                MaxGatherCount = type == ResourceNodeType.CelestialHerb ? 1 : maxGatherPerNode,
                CurrentGatherCount = "0",
                GatherDifficulty = type == ResourceNodeType.Common ? 0.1f :
                                   type == ResourceNodeType.Rare ? 0.3f :
                                   type == ResourceNodeType.CelestialHerb ? 0.5f : 0.2f,
                BaseQuantity = type == ResourceNodeType.MineralVein ? 3 :
                               type == ResourceNodeType.CelestialHerb ? 1 : 2,
                HasGuardian = UnityEngine.Random.value > 0.7f,
                BaseRespawnTimeMin = type == ResourceNodeType.Common ? commonRespawnMin : rareRespawnMin,
                BaseRespawnTimeMax = type == ResourceNodeType.Common ? commonRespawnMax : rareRespawnMax,
                RequiresActivePerception = type == ResourceNodeType.Rare || type == ResourceNodeType.CelestialHerb,
                IsDepleted = false
            };

            if (node.NodeType == ResourceNodeType.CelestialHerb)
                node.RequiredToolName = "玉锄头";

            return RegisterNode(node);
        }

        /// <summary>
        /// Get a debug status string.
        /// </summary>
        public string GetDebugStatus()
        {
            int activeCount = 0;
            int depletedCount = _depletedNodes.Count;
            foreach (var kvp in _activeNodes)
            {
                if (!kvp.Value.IsDepleted) activeCount++;
            }

            return $"=== GatheringSystem Status ===\n" +
                   $"Perception: {(_isPerceptionActive ? "ON" : "OFF")} (radius: {PerceptionRadius}m)\n" +
                   $"Gathering: {(_isGathering ? $"active ({_gatherProgress:P1})" : "idle")}\n" +
                   $"Active Nodes: {activeCount}, Depleted: {depletedCount}\n" +
                   $"Proficiency: Lv.{_proficiency.Level} ({GetProficiencyTitle()}) [{GetProficiencyProgress():P1}]\n" +
                   $"Player Stage: {_playerStage}, Tool: {_equippedToolName} ({_equippedToolQuality})";
        }

        #endregion
    }
}
