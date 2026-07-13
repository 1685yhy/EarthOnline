using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>Dynamic difficulty levels for dungeon instances.</summary>
    public enum DungeonDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Nightmare = 3
    }

    /// <summary>Lifecycle state of a dungeon instance.</summary>
    public enum DungeonState
    {
        Idle,
        SelectingDifficulty,
        Generating,
        Exploring,
        BossFight,
        Completed,
        Failed
    }

    // ─── Event Data ─────────────────────────────────────────────────────

    /// <summary>Published when a dungeon instance is created and difficulty is set.</summary>
    public struct DungeonEnteredEvent
    {
        public string DungeonId;
        public string PlayerId;
        public DungeonDifficulty Difficulty;
        public int Seed;
        public int RoomCount;
    }

    /// <summary>Published when the player moves to a new room.</summary>
    public struct DungeonRoomChangedEvent
    {
        public int RoomIndex;
        public string RoomType;
        public int BranchCount;
        public string[] AvailablePassages;
    }

    /// <summary>Published when the dungeon is fully cleared.</summary>
    public struct DungeonCompletedEvent
    {
        public string DungeonId;
        public DungeonDifficulty Difficulty;
        public int RoomsCleared;
        public bool BossDefeated;
    }

    /// <summary>Published when the player fails or leaves the dungeon.</summary>
    public struct DungeonExitedEvent
    {
        public string DungeonId;
        public bool WasCompleted;
        public int RoomsCleared;
    }

    // ─── Dungeon Instance ───────────────────────────────────────────────

    /// <summary>
    /// Core dungeon instance controller.
    /// Replaces the legacy DungeonEntrance. Manages difficulty selection,
    /// seed generation, room traversal, and lifecycle events.
    /// </summary>
    public class DungeonInstance : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string _dungeonId = "default_dungeon";
        [SerializeField] private int _visitCount = 0;

        [Header("State")]
        [SerializeField] private DungeonState _state = DungeonState.Idle;
        [SerializeField] private DungeonDifficulty _difficulty = DungeonDifficulty.Normal;
        [SerializeField] private int _currentRoomIndex = 0;

        // Runtime data
        private int _seed;
        private string _playerId;
        private DungeonRoomGenerator _generator;
        private DungeonLayout _layout;
        private int _roomsCleared;

        // ─── Properties ──────────────────────────────────────────────────

        public DungeonState State => _state;
        public DungeonDifficulty Difficulty => _difficulty;
        public int CurrentRoomIndex => _currentRoomIndex;
        public int Seed => _seed;
        public int RoomsCleared => _roomsCleared;
        public DungeonLayout Layout => _layout;
        public string DungeonId => _dungeonId;
        public string PlayerId => _playerId;

        // ─── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _generator = new DungeonRoomGenerator();
        }

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Enter the dungeon with a player ID. Triggers difficulty selection phase.</summary>
        public void EnterDungeon(string playerId)
        {
            if (_state != DungeonState.Idle)
            {
                Debug.LogWarning($"[DungeonInstance] Cannot enter dungeon from state {_state}");
                return;
            }

            _playerId = playerId;
            _state = DungeonState.SelectingDifficulty;
            _currentRoomIndex = -1;
            _roomsCleared = 0;

            Debug.Log($"[DungeonInstance] Player {playerId} entering dungeon '{_dungeonId}'. Please select difficulty.");
        }

        /// <summary>Select difficulty and begin dungeon generation.</summary>
        public void SelectDifficulty(DungeonDifficulty difficulty)
        {
            if (_state != DungeonState.SelectingDifficulty)
            {
                Debug.LogWarning($"[DungeonInstance] Cannot select difficulty in state {_state}");
                return;
            }

            _difficulty = difficulty;
            _seed = CalculateSeed(_playerId, _dungeonId, _visitCount + 1);
            _visitCount++;

            _state = DungeonState.Generating;
            GenerateDungeon();

            _state = DungeonState.Exploring;
            _currentRoomIndex = 0;

            // Notify listeners
            var enteredEvt = new DungeonEnteredEvent
            {
                DungeonId = _dungeonId,
                PlayerId = _playerId,
                Difficulty = _difficulty,
                Seed = _seed,
                RoomCount = _layout.RoomCount
            };
            EventBus.Publish(enteredEvt);

            // Fire first room
            FireRoomChangedEvent();

            Debug.Log($"[DungeonInstance] Dungeon generated. Seed={_seed}, Difficulty={_difficulty}, Rooms={_layout.RoomCount}");
        }

        /// <summary>Move to the next room by choosing a branch index.</summary>
        public void MoveToRoom(int branchIndex)
        {
            if (_state != DungeonState.Exploring && _state != DungeonState.BossFight)
            {
                Debug.LogWarning($"[DungeonInstance] Cannot move rooms in state {_state}");
                return;
            }

            var currentRoom = _layout.GetRoom(_currentRoomIndex);
            if (branchIndex < 0 || branchIndex >= currentRoom.Branches.Count)
            {
                Debug.LogError($"[DungeonInstance] Invalid branch index {branchIndex} for room {_currentRoomIndex}");
                return;
            }

            var nextRoomIndex = currentRoom.Branches[branchIndex];
            if (nextRoomIndex < 0 || nextRoomIndex >= _layout.RoomCount)
            {
                Debug.LogError($"[DungeonInstance] Branch {branchIndex} leads to invalid room index {nextRoomIndex}");
                return;
            }

            _currentRoomIndex = nextRoomIndex;

            var nextRoom = _layout.GetRoom(_currentRoomIndex);
            _roomsCleared++;

            // Check for boss room
            if (nextRoom.RoomType == RoomType.Boss)
            {
                _state = DungeonState.BossFight;
            }

            FireRoomChangedEvent();

            // Check completion
            if (_state == DungeonState.BossFight && nextRoom.RoomType == RoomType.Boss)
            {
                // Boss not yet defeated; player still needs to beat it
            }
        }

        /// <summary>Call when the player defeats the boss to complete the dungeon.</summary>
        public void CompleteDungeon()
        {
            if (_state != DungeonState.BossFight)
            {
                Debug.LogWarning($"[DungeonInstance] Cannot complete dungeon in state {_state}");
                return;
            }

            _state = DungeonState.Completed;

            var completedEvt = new DungeonCompletedEvent
            {
                DungeonId = _dungeonId,
                Difficulty = _difficulty,
                RoomsCleared = _roomsCleared,
                BossDefeated = true
            };
            EventBus.Publish(completedEvt);

            Debug.Log($"[DungeonInstance] Dungeon '{_dungeonId}' completed! Rooms cleared: {_roomsCleared}");
        }

        /// <summary>Call when the player fails or abandons the dungeon.</summary>
        public void FailDungeon()
        {
            if (_state == DungeonState.Completed || _state == DungeonState.Idle)
                return;

            _state = DungeonState.Failed;

            var exitedEvt = new DungeonExitedEvent
            {
                DungeonId = _dungeonId,
                WasCompleted = false,
                RoomsCleared = _roomsCleared
            };
            EventBus.Publish(exitedEvt);

            Debug.Log($"[DungeonInstance] Dungeon '{_dungeonId}' failed after {_roomsCleared} rooms.");
        }

        /// <summary>Reset the instance for re-entry.</summary>
        public void ResetInstance()
        {
            _state = DungeonState.Idle;
            _currentRoomIndex = 0;
            _roomsCleared = 0;
            _layout = null;
            _playerId = null;
        }

        // ─── Internal ────────────────────────────────────────────────────

        private void GenerateDungeon()
        {
            _layout = _generator.Generate(_seed, _difficulty);
        }

        private void FireRoomChangedEvent()
        {
            var room = _layout.GetRoom(_currentRoomIndex);
            var branchCount = room.Branches.Count;
            var passages = new string[room.PassageMethods.Count];
            for (int i = 0; i < passages.Length; i++)
                passages[i] = room.PassageMethods[i].ToString();

            var roomEvt = new DungeonRoomChangedEvent
            {
                RoomIndex = _currentRoomIndex,
                RoomType = room.RoomType.ToString(),
                BranchCount = branchCount,
                AvailablePassages = passages
            };
            EventBus.Publish(roomEvt);
        }

        /// <summary>
        /// Deterministic seed = Hash(playerId + dungeonId + visitCount).
        /// </summary>
        private static int CalculateSeed(string playerId, string dungeonId, int visitCount)
        {
            unchecked
            {
                var input = $"{playerId}:{dungeonId}:{visitCount}";
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        // ─── Editor Helpers ──────────────────────────────────────────────

        private void OnGUI()
        {
            if (_state == DungeonState.SelectingDifficulty)
            {
                DrawDifficultySelector();
            }
        }

        private void DrawDifficultySelector()
        {
            var screenW = Screen.width;
            var screenH = Screen.height;

            // Semi-transparent overlay
            var overlay = new Rect(0, 0, screenW, screenH);
            GUI.Box(overlay, "");

            var title = "选择副本难度";
            var titleSize = GUI.skin.label.CalcSize(new GUIContent(title));
            var titleRect = new Rect(screenW / 2f - 100, screenH / 2f - 100, 200, 30);
            GUI.Label(titleRect, title, GUI.skin.label);

            string[] difficulties = { "简单 Easy", "普通 Normal", "困难 Hard", "噩梦 Nightmare" };
            for (int i = 0; i < difficulties.Length; i++)
            {
                var btnRect = new Rect(screenW / 2f - 80, screenH / 2f - 50 + i * 40, 160, 30);
                if (GUI.Button(btnRect, difficulties[i]))
                {
                    SelectDifficulty((DungeonDifficulty)i);
                }
            }
        }
    }
}
