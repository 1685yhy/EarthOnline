using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Room Type Definitions ──────────────────────────────────────────

    /// <summary>The 6 types of rooms in a dungeon.</summary>
    public enum RoomType
    {
        Combat,     // 战斗 — enemy encounter
        Treasure,   // 宝藏 — loot / rewards
        Trap,       // 陷阱 — hazard challenge
        Merchant,   // 商人 — shop NPC
        Rest,       // 休息 — heal / recovery
        Boss        // BOSS — end-of-dungeon boss fight
    }

    /// <summary>The 4 ways a player can pass through / handle a room.</summary>
    public enum PassageMethod
    {
        Combat,         // 战斗 — fight through
        Stealth,        // 潜行 — sneak past
        Negotiate,      // 谈判 — talk through
        Environmental   // 环境利用 — use environment
    }

    // ─── Room Data ──────────────────────────────────────────────────────

    /// <summary>A single room in the generated dungeon layout.</summary>
    [Serializable]
    public class DungeonRoom
    {
        public RoomType RoomType;
        public List<PassageMethod> PassageMethods;
        public List<int> Branches; // indices of next rooms this branch leads to
        public int Depth;          // how deep from start (0-based)
        public string Description;

        public DungeonRoom(RoomType roomType, int depth)
        {
            RoomType = roomType;
            PassageMethods = new List<PassageMethod>();
            Branches = new List<int>();
            Depth = depth;
            Description = GenerateDescription(roomType);
        }

        private static string GenerateDescription(RoomType type)
        {
            return type switch
            {
                RoomType.Combat => "A battle arena echoes with the clash of weapons.",
                RoomType.Treasure => "Glittering treasure awaits the bold.",
                RoomType.Trap => "The air feels dangerous — hidden mechanisms lie ahead.",
                RoomType.Merchant => "A mysterious merchant sets up shop in the shadows.",
                RoomType.Rest => "A quiet sanctuary offers a moment of respite.",
                RoomType.Boss => "An overwhelming presence looms ahead — the floor guardian.",
                _ => "An empty chamber stretches before you."
            };
        }
    }

    // ─── Full Dungeon Layout ────────────────────────────────────────────

    /// <summary>Complete layout of a generated dungeon instance.</summary>
    public class DungeonLayout
    {
        private List<DungeonRoom> _rooms;

        public int RoomCount => _rooms.Count;
        public IReadOnlyList<DungeonRoom> Rooms => _rooms.AsReadOnly();

        public DungeonLayout()
        {
            _rooms = new List<DungeonRoom>();
        }

        public void AddRoom(DungeonRoom room) => _rooms.Add(room);

        public DungeonRoom GetRoom(int index)
        {
            if (index < 0 || index >= _rooms.Count)
                return null;
            return _rooms[index];
        }
    }

    // ─── Room Generator ─────────────────────────────────────────────────

    /// <summary>
    /// Seed-based procedural dungeon room generator.
    /// Produces a deterministic branching layout given a seed and difficulty.
    /// Each intersection offers 2-3 branches. Room types are distributed
    /// according to difficulty-weighted probabilities.
    /// </summary>
    public class DungeonRoomGenerator
    {
        // Difficulty config: { roomCount, combatWeight, treasureWeight, trapWeight, merchantWeight, restWeight }
        private static readonly (int rooms, int[] weights)[] DifficultyConfig = new (int, int[])[]
        {
            // Easy:    fewer rooms, more combat/treasure, fewer traps
            (5,  new[] { 35, 25, 10, 10, 15, 5 }),
            // Normal:  balanced
            (7,  new[] { 30, 20, 15, 10, 10, 15 }),
            // Hard:    more rooms, more traps, less rest
            (10, new[] { 30, 15, 20, 10, 5, 20 }),
            // Nightmare: most rooms, heavy combat/traps, rare rest
            (12, new[] { 35, 10, 25, 10, 3, 17 })
        };

        private const int ROOM_TYPES = 6; // Combat(0) through Boss(5)

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Generate a full dungeon layout from seed and difficulty.</summary>
        public DungeonLayout Generate(int seed, DungeonDifficulty difficulty)
        {
            var rng = new System.Random(seed);
            var layout = new DungeonLayout();
            int diffIndex = (int)difficulty;
            var (roomCount, weights) = DifficultyConfig[diffIndex];

            // Clone weights for mutation
            var workingWeights = new int[ROOM_TYPES];
            Array.Copy(weights, workingWeights, ROOM_TYPES);

            // Allocate room slots (excluding start and boss ends)
            // Room 0 = start room; last room = Boss.
            int nonBossCount = Mathf.Max(1, roomCount - 2); // rooms between start and boss

            // Generate rooms layer by layer
            BuildLayers(layout, nonBossCount, rng, workingWeights, diffIndex);

            // ── Add boss room at end ──
            var bossRoom = new DungeonRoom(RoomType.Boss, layout.RoomCount > 0 ? layout.GetRoom(layout.RoomCount - 1).Depth + 1 : 0);
            bossRoom.PassageMethods.Add(PassageMethod.Combat);
            bossRoom.PassageMethods.Add(PassageMethod.Environmental);
            layout.AddRoom(bossRoom);

            // Wire boss into deepest rooms
            WireBossRoom(layout);

            // Fill passage methods for each room
            AssignPassageMethods(layout, rng);

            return layout;
        }

        // ─── Layer Building ──────────────────────────────────────────────

        private void BuildLayers(DungeonLayout layout, int roomCount, System.Random rng, int[] weights, int diffIndex)
        {
            var startRoom = new DungeonRoom(RoomType.Combat, 0);
            startRoom.PassageMethods.Add(PassageMethod.Combat);
            startRoom.PassageMethods.Add(PassageMethod.Environmental);
            layout.AddRoom(startRoom);

            if (roomCount <= 0)
                return;

            int layers = Mathf.Max(1, Mathf.CeilToInt(roomCount / 2.5f));
            int roomsPerLayer = Mathf.Max(1, roomCount / layers);

            int roomsPlaced = 0;
            int currentDepth = 1;

            while (roomsPlaced < roomCount)
            {
                int batchSize = Mathf.Min(roomsPerLayer + rng.Next(-1, 2), roomCount - roomsPlaced);
                batchSize = Mathf.Max(1, batchSize);

                var roomsThisLayer = new List<int>();

                for (int i = 0; i < batchSize && roomsPlaced < roomCount; i++)
                {
                    var roomType = RollRoomType(rng, weights, diffIndex, roomsPlaced, roomCount);
                    var room = new DungeonRoom(roomType, currentDepth);
                    layout.AddRoom(room);
                    roomsThisLayer.Add(layout.RoomCount - 1);
                    roomsPlaced++;
                }

                var prevLayerRooms = GetRoomsAtDepth(layout, currentDepth - 1);
                ConnectLayers(layout, prevLayerRooms, roomsThisLayer, rng);

                currentDepth++;
            }

            EnsureConnectivity(layout, rng);
        }

        private List<int> GetRoomsAtDepth(DungeonLayout layout, int depth)
        {
            var result = new List<int>();
            for (int i = 0; i < layout.RoomCount; i++)
            {
                if (layout.GetRoom(i).Depth == depth)
                    result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// Connect each room in prevLayer to 2-3 rooms in currLayer.
        /// Handles edge cases by adding lateral/back branches when a layer has
        /// fewer rooms than the target branch count.
        /// </summary>
        private void ConnectLayers(DungeonLayout layout, List<int> prevLayer, List<int> currLayer, System.Random rng)
        {
            if (prevLayer.Count == 0 || currLayer.Count == 0) return;

            foreach (int prevIdx in prevLayer)
            {
                var room = layout.GetRoom(prevIdx);
                int target = rng.Next(2, 4);
                var targets = new HashSet<int>();
                int toConnect = Mathf.Min(target, currLayer.Count);
                toConnect = Mathf.Max(1, toConnect);

                int attempts = 0;
                while (targets.Count < toConnect && attempts < 100)
                {
                    targets.Add(currLayer[rng.Next(0, currLayer.Count)]);
                    attempts++;
                }
                foreach (int t in targets)
                    if (!room.Branches.Contains(t)) room.Branches.Add(t);

                // If only 1 connection, add lateral/back branches to siblings
                if (room.Branches.Count < 2)
                {
                    foreach (int sibling in prevLayer)
                    {
                        if (sibling != prevIdx && !room.Branches.Contains(sibling))
                        {
                            room.Branches.Add(sibling);
                            if (room.Branches.Count >= 2) break;
                        }
                    }
                }

                // Singleton currLayer: duplicate the only target
                if (room.Branches.Count < 2 && currLayer.Count == 1)
                    room.Branches.Add(currLayer[0]);

                // Cap at 3
                while (room.Branches.Count > 3)
                    room.Branches.RemoveAt(room.Branches.Count - 1);
            }
        }

        private void EnsureConnectivity(DungeonLayout layout, System.Random rng)
        {
            // Ensure all rooms have at least one incoming connection
            for (int i = 1; i < layout.RoomCount; i++)
            {
                bool hasIncoming = false;
                for (int j = 0; j < i; j++)
                {
                    if (layout.GetRoom(j).Branches.Contains(i))
                    {
                        hasIncoming = true;
                        break;
                    }
                }

                if (!hasIncoming)
                {
                    // Find closest room at depth-1
                    var room = layout.GetRoom(i);
                    var candidates = GetRoomsAtDepth(layout, room.Depth - 1);
                    if (candidates.Count > 0)
                    {
                        int source = candidates[rng.Next(0, candidates.Count)];
                        layout.GetRoom(source).Branches.Add(i);
                    }
                }
            }
        }

        /// <summary>
        /// Connect the boss room from all deepest-layer rooms.
        /// Ensures each pre-boss room satisfies the 2-3 branch rule.
        /// </summary>
        private void WireBossRoom(DungeonLayout layout)
        {
            int bossIndex = layout.RoomCount - 1;
            int bossDepth = layout.GetRoom(bossIndex).Depth;
            var deepestRooms = GetRoomsAtDepth(layout, bossDepth - 1);

            if (deepestRooms.Count == 0)
            {
                int finalDepth = 0;
                for (int i = 0; i < layout.RoomCount - 1; i++)
                    finalDepth = Mathf.Max(finalDepth, layout.GetRoom(i).Depth);
                deepestRooms = GetRoomsAtDepth(layout, finalDepth);
            }

            foreach (int roomIdx in deepestRooms)
            {
                var room = layout.GetRoom(roomIdx);
                if (!room.Branches.Contains(bossIndex))
                    room.Branches.Add(bossIndex);

                // Ensure 2+ branches: add lateral connections to siblings
                if (room.Branches.Count < 2 && deepestRooms.Count > 1)
                {
                    foreach (int siblingIdx in deepestRooms)
                    {
                        if (siblingIdx != roomIdx && !room.Branches.Contains(siblingIdx))
                        {
                            room.Branches.Insert(0, siblingIdx);
                            if (room.Branches.Count >= 2) break;
                        }
                    }
                }

                // Singleton deep layer: duplicate boss branch
                if (room.Branches.Count < 2)
                    room.Branches.Add(bossIndex);

                // Cap at 3
                while (room.Branches.Count > 3)
                    room.Branches.RemoveAt(room.Branches.Count - 1);
            }
        }

        // ─── Room Type Selection ─────────────────────────────────────────

        private RoomType RollRoomType(System.Random rng, int[] weights, int diffIndex, int roomsPlaced, int totalRooms)
        {
            // Guarantee a rest room near the midpoint
            float progress = totalRooms > 0 ? (float)roomsPlaced / totalRooms : 0f;

            if (roomsPlaced > 0 && (progress >= 0.45f && progress <= 0.55f))
            {
                return RoomType.Rest;
            }

            // Weighted random
            int totalWeight = 0;
            for (int i = 0; i < ROOM_TYPES; i++)
                totalWeight += weights[i];

            int roll = rng.Next(0, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < ROOM_TYPES; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return (RoomType)i;
            }

            return RoomType.Combat; // fallback
        }

        // ─── Passage Method Assignment ───────────────────────────────────

        private void AssignPassageMethods(DungeonLayout layout, System.Random rng)
        {
            var allMethods = new[] { PassageMethod.Combat, PassageMethod.Stealth, PassageMethod.Negotiate, PassageMethod.Environmental };

            for (int i = 0; i < layout.RoomCount; i++)
            {
                var room = layout.GetRoom(i);

                // Already assigned via special cases
                if (room.PassageMethods.Count > 0)
                    continue;

                int methodCount = rng.Next(2, 5); // 2 to 4 methods per room
                var selected = new HashSet<PassageMethod>();

                // Boss room always has combat
                if (room.RoomType == RoomType.Boss)
                {
                    selected.Add(PassageMethod.Combat);
                }

                // Rest room always has negotiate (rest)
                if (room.RoomType == RoomType.Rest)
                {
                    selected.Add(PassageMethod.Negotiate);
                }

                // Fill remaining
                while (selected.Count < methodCount && selected.Count < allMethods.Length)
                {
                    var method = allMethods[rng.Next(0, allMethods.Length)];
                    selected.Add(method);
                }

                room.PassageMethods = new List<PassageMethod>(selected);
            }
        }
    }
}
