using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    #region Enums & Data Structures

    /// <summary>Fog visibility layers for the world map.</summary>
    public enum FogLayer
    {
        /// <summary>Unexplored — only terrain silhouette visible.</summary>
        Unexplored = 0,

        /// <summary>Lightly explored — terrain + major landmarks visible.</summary>
        LightlyExplored = 1,

        /// <summary>Deeply explored — full detail including resources, NPCs, interactive objects.</summary>
        DeeplyExplored = 2
    }

    /// <summary>Serializable cell data for save/load.</summary>
    [System.Serializable]
    public struct FogCellRecord
    {
        public int X;
        public int Y;
        public int Layer;
    }

    /// <summary>Serializable fog state snapshot for persistence.</summary>
    [System.Serializable]
    public class FogSaveData
    {
        public FogCellRecord[] Cells;
    }

    #endregion

    /// <summary>
    /// Three-layer fog of war system for EarthOnline.
    ///
    /// Layer 0 (Unexplored):     Full fog — only terrain silhouette visible.
    /// Layer 1 (LightlyExplored): Terrain + major landmarks (cities, roads, rivers).
    /// Layer 2 (DeeplyExplored):  Full detail — resources, NPCs, interactive objects.
    ///
    /// Grid-based with configurable cell size. Uses EventBus for decoupled
    /// communication with minimap, world map, and other systems.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        #region Constants

        private const float DEFAULT_CELL_SIZE = 2f;             // meters per cell
        private const float DEFAULT_BASE_REVEAL_RADIUS = 15f;   // BaseReveal(15m)
        private const float DEFAULT_HEIGHT_MULTIPLIER = 3f;     // High ground: 3x radius
        private const float DEFAULT_AERIAL_REVEAL_DURATION = 30f; // 30-second temporary vision
        private const float PERCEPTION_BONUS_STEP = 0.1f;       // +10% per perception level

        #endregion

        #region Singleton

        public static FogOfWar Instance { get; private set; }

        #endregion

        #region Inspector Config

        [Header("Grid Settings")]
        [SerializeField] private float _cellSize = DEFAULT_CELL_SIZE;

        [Header("Reveal Settings")]
        [SerializeField] private float _baseRevealRadius = DEFAULT_BASE_REVEAL_RADIUS;
        [SerializeField] private float _heightMultiplier = DEFAULT_HEIGHT_MULTIPLIER;
        [SerializeField] private float _aerialRevealDuration = DEFAULT_AERIAL_REVEAL_DURATION;
        [SerializeField, Range(0f, 1f)] private float _perceptionBonusPerLevel = PERCEPTION_BONUS_STEP;

        #endregion

        #region Private State

        // Explored cells: key = cellCoord (x,y), value = FogLayer (byte).
        // Cells at Unexplored (0) are NOT stored (default).
        private Dictionary<Vector2Int, byte> _exploredCells = new Dictionary<Vector2Int, byte>();

        // High-ground vision state.
        private bool _highGroundActive;
        private float _highGroundTimer;
        private float _currentHeightMultiplier; // cached during high-ground

        // Player tracking.
        private Vector3 _lastPlayerPosition;
        private bool _hasLastPosition;
        private float _revealCooldown;
        private const float REVEAL_INTERVAL = 0.5f; // re-check every 0.5s while moving

        #endregion

        #region Public Properties

        /// <summary>Size of each fog cell in world units.</summary>
        public float CellSize => _cellSize;

        /// <summary>Base reveal radius without modifiers (meters).</summary>
        public float BaseRevealRadius => _baseRevealRadius;

        /// <summary>Current effective reveal radius accounting for height multiplier.</summary>
        public float CurrentRevealRadius
        {
            get
            {
                float radius = _baseRevealRadius;
                if (_highGroundActive)
                    radius *= _currentHeightMultiplier;
                return radius;
            }
        }

        /// <summary>Whether high-ground vision is currently active.</summary>
        public bool IsHighGroundActive => _highGroundActive;

        /// <summary>Remaining high-ground duration in seconds.</summary>
        public float HighGroundRemaining => Mathf.Max(0f, _highGroundTimer);

        /// <summary>Total explored cell count (all layers).</summary>
        public int ExploredCellCount => _exploredCells.Count;

        /// <summary>Read-only view of explored cells for external queries.</summary>
        public IReadOnlyDictionary<Vector2Int, byte> ExploredCells => _exploredCells;

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

            _hasLastPosition = false;
            _currentHeightMultiplier = _heightMultiplier;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // Decay high-ground timer.
            if (_highGroundActive)
            {
                _highGroundTimer -= Time.deltaTime;
                if (_highGroundTimer <= 0f)
                {
                    DeactivateHighGroundVision();
                }
            }

            // Automatic reveal around player (FOG-02).
            UpdatePlayerReveal();
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>Convert a world position to cell coordinates.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / _cellSize),
                Mathf.FloorToInt(worldPosition.z / _cellSize)
            );
        }

        /// <summary>Get the world-space center of a cell.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                cell.x * _cellSize + _cellSize * 0.5f,
                0f,
                cell.y * _cellSize + _cellSize * 0.5f
            );
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the fog layer at a world position.
        /// Returns FogLayer.Unexplored for cells that have never been explored.
        /// </summary>
        public FogLayer GetFogLayer(Vector3 worldPosition)
        {
            return GetFogLayerAtCell(WorldToCell(worldPosition));
        }

        /// <summary>
        /// Get the fog layer for a specific cell coordinate.
        /// Returns FogLayer.Unexplored for cells that have never been explored.
        /// </summary>
        public FogLayer GetFogLayerAtCell(Vector2Int cellCoord)
        {
            if (_exploredCells.TryGetValue(cellCoord, out byte layer))
            {
                return (FogLayer)layer;
            }
            return FogLayer.Unexplored;
        }

        /// <summary>
        /// Reveal all cells within a radius around a world position.
        /// Newly revealed cells are set to FogLayer.DeeplyExplored (Layer 2).
        /// Previously explored cells are upgraded to Layer 2.
        /// Processes immediately — does not use deferred queue.
        /// </summary>
        public void RevealAround(Vector3 worldPosition, float radius)
        {
            Vector2Int center = WorldToCell(worldPosition);
            int cellRadius = Mathf.CeilToInt(radius / _cellSize);
            float radiusSq = radius * radius;
            int cellsChanged = 0;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dy);

                    // Check if within radius.
                    Vector3 cellWorld = CellToWorld(cell);
                    float distSq = (cellWorld - worldPosition).sqrMagnitude;
                    if (distSq > radiusSq)
                        continue;

                    if (TrySetCellLayer(cell, (byte)FogLayer.DeeplyExplored))
                    {
                        cellsChanged++;
                    }
                }
            }

            if (cellsChanged > 0)
            {
                EventBus.Publish(new FogBatchRevealedEvent
                {
                    RegionId = "player_reveal",
                    CellsChanged = cellsChanged
                });
            }
        }

        /// <summary>
        /// Reveal a single cell to a specific fog layer.
        /// Used by path tracking (FOG-03) and detailed map item (FOG-06).
        /// Processes immediately.
        /// </summary>
        public void RevealCell(Vector2Int cellCoord, FogLayer layer)
        {
            TrySetCellLayer(cellCoord, (byte)layer);
        }

        /// <summary>
        /// Activate high-ground vision: multiplies reveal radius by _heightMultiplier
        /// for _aerialRevealDuration seconds (FOG-04, FOG-05).
        /// </summary>
        public void SetHighGroundVision(bool active)
        {
            if (active)
            {
                ActivateHighGroundVision();
            }
            else
            {
                DeactivateHighGroundVision();
            }
        }

        /// <summary>
        /// Reveal all cells within a region to a specific fog layer.
        /// Used by the "detailed map" consumable item (FOG-06).
        /// Region boundaries are defined externally and passed as cell bounds.
        /// </summary>
        public void RevealRegion(Vector2Int regionMinCell, Vector2Int regionMaxCell, FogLayer layer)
        {
            int cellsChanged = 0;

            for (int x = regionMinCell.x; x <= regionMaxCell.x; x++)
            {
                for (int y = regionMinCell.y; y <= regionMaxCell.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (TrySetCellLayer(cell, (byte)layer))
                    {
                        cellsChanged++;
                    }
                }
            }

            if (cellsChanged > 0)
            {
                EventBus.Publish(new FogBatchRevealedEvent
                {
                    RegionId = $"cell_{regionMinCell.x}_{regionMinCell.y}",
                    CellsChanged = cellsChanged
                });
            }
        }

        /// <summary>
        /// Check if a world position has been explored (Layer >= 1).
        /// </summary>
        public bool IsExplored(Vector3 worldPosition)
        {
            return GetFogLayer(worldPosition) >= FogLayer.LightlyExplored;
        }

        /// <summary>
        /// Get all cells at a specific fog layer.
        /// </summary>
        public List<Vector2Int> GetCellsAtLayer(FogLayer layer)
        {
            var results = new List<Vector2Int>();
            byte targetByte = (byte)layer;

            foreach (var kvp in _exploredCells)
            {
                if (kvp.Value == targetByte)
                {
                    results.Add(kvp.Key);
                }
            }

            return results;
        }

        /// <summary>
        /// Get a flat array of explored cells for save/load serialization.
        /// </summary>
        public FogCellRecord[] GetExploredCells()
        {
            var records = new FogCellRecord[_exploredCells.Count];
            int index = 0;
            foreach (var kvp in _exploredCells)
            {
                records[index] = new FogCellRecord
                {
                    X = kvp.Key.x,
                    Y = kvp.Key.y,
                    Layer = kvp.Value
                };
                index++;
            }
            return records;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Capture the current fog state as serializable data.
        /// </summary>
        public FogSaveData GetSaveData()
        {
            return new FogSaveData
            {
                Cells = GetExploredCells()
            };
        }

        /// <summary>
        /// Restore fog state from previously saved data.
        /// (FOG-07: Death does not reset fog — reload saved state on respawn.)
        /// </summary>
        public void LoadSaveData(FogSaveData data)
        {
            if (data?.Cells == null)
            {
                Debug.LogWarning("[FogOfWar] LoadSaveData: null or empty data.");
                return;
            }

            _exploredCells.Clear();

            foreach (var record in data.Cells)
            {
                var cell = new Vector2Int(record.X, record.Y);
                byte layer = (byte)Mathf.Clamp(record.Layer, (int)FogLayer.Unexplored, (int)FogLayer.DeeplyExplored);

                // Skip Unexplored (Layer 0) — not stored.
                if (layer == (byte)FogLayer.Unexplored)
                    continue;

                _exploredCells[cell] = layer;
            }

            Debug.Log($"[FogOfWar] Loaded fog data: {_exploredCells.Count} cells restored.");
        }

        /// <summary>
        /// Clear all fog data (for new game).
        /// </summary>
        public void ClearAll()
        {
            _exploredCells.Clear();
            _highGroundActive = false;
            _highGroundTimer = 0f;
            _hasLastPosition = false;

            Debug.Log("[FogOfWar] All fog data cleared.");
        }

        #endregion

        #region Internal: Reveal Processing

        /// <summary>
        /// Try to set a cell's fog layer. Returns true if the layer actually changed.
        /// Only upgrades to higher layers — never downgrades (FOG-07).
        /// </summary>
        private bool TrySetCellLayer(Vector2Int cell, byte newLayer)
        {
            byte oldLayer = 0; // Default: Unexplored.

            if (_exploredCells.TryGetValue(cell, out byte existingLayer))
            {
                oldLayer = existingLayer;

                // Never downgrade — once explored, stays explored (FOG-07).
                if (existingLayer >= newLayer)
                    return false;
            }

            _exploredCells[cell] = newLayer;

            // Publish per-cell event for fine-grained listeners.
            EventBus.Publish(new FogCellRevealedEvent
            {
                CellX = cell.x,
                CellY = cell.y,
                OldLayer = oldLayer,
                NewLayer = newLayer
            });

            return true;
        }

        #endregion

        #region Internal: Player Reveal

        /// <summary>Automatically reveal fog around the player while moving.</summary>
        private void UpdatePlayerReveal()
        {
            _revealCooldown -= Time.deltaTime;
            if (_revealCooldown > 0f) return;

            // Find the player GameObject (by tag or via a manager).
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                // Player not yet spawned — skip.
                return;
            }

            Vector3 playerPos = player.transform.position;

            // Only re-reveal if player has moved meaningfully.
            if (_hasLastPosition)
            {
                float moved = Vector3.Distance(playerPos, _lastPlayerPosition);
                if (moved < _cellSize * 0.5f)
                    return;
            }

            _lastPlayerPosition = playerPos;
            _hasLastPosition = true;
            _revealCooldown = REVEAL_INTERVAL;

            // Calculate effective radius with optional perception bonus.
            float radius = CurrentRevealRadius;

            // Apply perception bonus if available (placeholder for cultivation system).
            float perceptionBonus = GetPerceptionBonus();
            radius *= (1f + perceptionBonus);

            RevealAround(playerPos, radius);
        }

        /// <summary>
        /// Get perception bonus from the cultivation system.
        /// Formula: perceptionBonus = perceptionLevel x _perceptionBonusPerLevel.
        /// Currently returns 0 pending CultivationManager integration (perceptionLevel = 0).
        /// </summary>
        private float GetPerceptionBonus()
        {
            // TODO: Integrate with CultivationManager:
            // int perceptionLevel = CultivationManager.Instance?.GetPerceptionLevel() ?? 0;
            int perceptionLevel = 0;
            return perceptionLevel * _perceptionBonusPerLevel;
        }

        #endregion

        #region Internal: High Ground Vision

        /// <summary>Activate high-ground vision multiplier (FOG-04).</summary>
        private void ActivateHighGroundVision()
        {
            _highGroundActive = true;
            _highGroundTimer = _aerialRevealDuration;
            _currentHeightMultiplier = _heightMultiplier;

            // Immediately reveal around player with the expanded radius.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                RevealAround(player.transform.position, CurrentRevealRadius);
            }

            EventBus.Publish(new FogHighGroundVisionEvent
            {
                IsActive = "true",
                CurrentRadius = CurrentRevealRadius,
                RemainingDuration = _highGroundTimer
            });

            Debug.Log($"[FogOfWar] High-ground vision activated. Radius: {CurrentRevealRadius}m, Duration: {_highGroundTimer}s");
        }

        /// <summary>Deactivate high-ground vision (FOG-05).</summary>
        private void DeactivateHighGroundVision()
        {
            _highGroundActive = false;
            _highGroundTimer = 0f;

            EventBus.Publish(new FogHighGroundVisionEvent
            {
                IsActive = "false",
                CurrentRadius = CurrentRevealRadius,
                RemainingDuration = 0f
            });

            Debug.Log("[FogOfWar] High-ground vision deactivated. Radius returned to base.");
        }

        /// <summary>Refresh high-ground timer (e.g., player on another vantage point).</summary>
        public void RefreshHighGroundVision()
        {
            if (_highGroundActive)
            {
                _highGroundTimer = _aerialRevealDuration;
            }
        }

        /// <summary>Extend or set high-ground duration by a custom amount.</summary>
        public void ExtendHighGroundVision(float extraSeconds)
        {
            if (_highGroundActive)
            {
                _highGroundTimer += extraSeconds;
            }
            else
            {
                _highGroundTimer = extraSeconds;
                _highGroundActive = true;
                _currentHeightMultiplier = _heightMultiplier;
            }

            EventBus.Publish(new FogHighGroundVisionEvent
            {
                IsActive = "true",
                CurrentRadius = CurrentRevealRadius,
                RemainingDuration = _highGroundTimer
            });
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            return $"=== Fog of War Status ===\n" +
                   $"Explored Cells: {_exploredCells.Count}\n" +
                   $"Layer 2 (Deep): {GetCellsAtLayer(FogLayer.DeeplyExplored).Count}\n" +
                   $"Layer 1 (Light): {GetCellsAtLayer(FogLayer.LightlyExplored).Count}\n" +
                   $"High Ground: {(_highGroundActive ? $"ACTIVE ({_highGroundTimer:F1}s)" : "OFF")}\n" +
                   $"Base Radius: {_baseRevealRadius}m | Current: {CurrentRevealRadius:F1}m";
        }

        #endregion
    }
}
