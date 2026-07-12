using System;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Rating Enum ───────────────────────────────────────────────────

    /// <summary>通关评价五级评分 / Five-tier dungeon completion rating.</summary>
    public enum DungeonRating
    {
        D = 0, // 险胜 / Narrow victory
        C = 1, // 合格 / Passable
        B = 2, // 良好 / Good
        A = 3, // 优秀 / Excellent
        S = 4  // 完美 / Perfect
    }

    // ─── Progress Data ─────────────────────────────────────────────────

    /// <summary>Serializable data for a dungeon run progress snapshot.</summary>
    [Serializable]
    public struct DungeonProgressData
    {
        public string DungeonId;
        public string PlayerId;
        public DungeonDifficulty Difficulty;
        public int CurrentRoomIndex;
        public int RoomsCleared;
        public DungeonState State;
        public int Seed;
        public int VisitCount;
        public double SaveTimestamp; // Unix timestamp (seconds)
        public float ElapsedTime;    // seconds spent in dungeon
        public int EnemiesDefeated;
        public int DamageTaken;
        public int CollectiblesFound;
        public bool BossDefeated;

        /// <summary>Check if this save is still within the valid 48-hour window.</summary>
        public bool IsValid
        {
            get
            {
                if (SaveTimestamp <= 0) return false;
                double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                return (now - SaveTimestamp) < 172800d; // 48h in seconds
            }
        }
    }

    // ─── Event Data ────────────────────────────────────────────────────

    /// <summary>Published when dungeon progress is saved.</summary>
    public struct DungeonProgressSavedEvent
    {
        public string DungeonId;
        public string PlayerId;
        public double SaveTimestamp;
    }

    /// <summary>Published when a saved dungeon is successfully restored.</summary>
    public struct DungeonProgressRestoredEvent
    {
        public string DungeonId;
        public string PlayerId;
        public DungeonProgressData Progress;
    }

    /// <summary>Published when a saved progress has expired (>48h).</summary>
    public struct DungeonProgressExpiredEvent
    {
        public string DungeonId;
        public string PlayerId;
        public double AgeHours;
    }

    /// <summary>Published when the dungeon run receives its final rating.</summary>
    public struct DungeonRatingEvent
    {
        public string DungeonId;
        public DungeonDifficulty Difficulty;
        public DungeonRating Rating;
        public int Score;
        public float BonusMultiplier;
    }

    // ─── Progress Manager ──────────────────────────────────────────────

    /// <summary>
    /// Manages dungeon progress persistence with 48-hour expiry.
    /// Supports save, restore, and rating evaluation.
    /// Uses PlayerPrefs as backing store for simplicity.
    /// </summary>
    public class DungeonProgress : MonoBehaviour
    {
        private const string SAVE_KEY_PREFIX = "DungeonProgress_";
        private const double EXPIRY_SECONDS = 172800d; // 48 hours

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Header("Config")]
        [SerializeField] private string _playerId = "player_default";

        // Runtime
        private DungeonProgressData _currentProgress;
        private DungeonInstance _dungeonInstance;

        // ─── Properties ──────────────────────────────────────────────────

        public bool HasSavedProgress { get; private set; }
        public DungeonProgressData CurrentProgress => _currentProgress;

        // ─── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _dungeonInstance = GetComponent<DungeonInstance>();
        }

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Save current dungeon progress.</summary>
        public void SaveProgress()
        {
            if (_dungeonInstance == null) return;

            _currentProgress = new DungeonProgressData
            {
                DungeonId = _dungeonInstance.DungeonId,
                PlayerId = _playerId,
                Difficulty = _dungeonInstance.Difficulty,
                CurrentRoomIndex = _dungeonInstance.CurrentRoomIndex,
                RoomsCleared = _dungeonInstance.RoomsCleared,
                State = _dungeonInstance.State,
                Seed = _dungeonInstance.Seed,
                VisitCount = _currentProgress.VisitCount,
                SaveTimestamp = (DateTime.UtcNow - UnixEpoch).TotalSeconds,
                ElapsedTime = _currentProgress.ElapsedTime,
                EnemiesDefeated = _currentProgress.EnemiesDefeated,
                DamageTaken = _currentProgress.DamageTaken,
                CollectiblesFound = _currentProgress.CollectiblesFound,
                BossDefeated = _currentProgress.BossDefeated
            };

            string key = GetSaveKey(_dungeonInstance.DungeonId, _playerId);
            string json = JsonUtility.ToJson(_currentProgress);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();

            HasSavedProgress = true;

            EventBus.Publish(new DungeonProgressSavedEvent
            {
                DungeonId = _dungeonInstance.DungeonId,
                PlayerId = _playerId,
                SaveTimestamp = _currentProgress.SaveTimestamp
            });

            Debug.Log($"[DungeonProgress] Progress saved for '{_dungeonInstance.DungeonId}' at room {_dungeonInstance.CurrentRoomIndex}");
        }

        /// <summary>Restore previously saved progress. Returns true if successful.</summary>
        public bool RestoreProgress(string dungeonId)
        {
            string key = GetSaveKey(dungeonId, _playerId);
            if (!PlayerPrefs.HasKey(key))
            {
                Debug.Log($"[DungeonProgress] No saved progress found for '{dungeonId}'");
                return false;
            }

            string json = PlayerPrefs.GetString(key);
            var data = JsonUtility.FromJson<DungeonProgressData>(json);

            // Check 48h expiry
            if (!data.IsValid)
            {
                double ageHours = ((DateTime.UtcNow - UnixEpoch).TotalSeconds - data.SaveTimestamp) / 3600d;
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();

                EventBus.Publish(new DungeonProgressExpiredEvent
                {
                    DungeonId = dungeonId,
                    PlayerId = _playerId,
                    AgeHours = ageHours
                });

                Debug.Log($"[DungeonProgress] Progress for '{dungeonId}' has expired ({ageHours:F1}h > 48h). Cleared.");
                return false;
            }

            _currentProgress = data;
            HasSavedProgress = true;

            EventBus.Publish(new DungeonProgressRestoredEvent
            {
                DungeonId = dungeonId,
                PlayerId = _playerId,
                Progress = data
            });

            Debug.Log($"[DungeonProgress] Progress restored for '{dungeonId}' — Room {data.CurrentRoomIndex}, {data.RoomsCleared} cleared.");
            return true;
        }

        /// <summary>Clear saved progress for a dungeon.</summary>
        public void ClearProgress(string dungeonId)
        {
            string key = GetSaveKey(dungeonId, _playerId);
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            HasSavedProgress = false;
            _currentProgress = default;
        }

        /// <summary>Record an enemy defeated during the run.</summary>
        public void RecordEnemyDefeated()
        {
            _currentProgress.EnemiesDefeated++;
        }

        /// <summary>Record damage taken during the run.</summary>
        public void RecordDamageTaken(int amount)
        {
            _currentProgress.DamageTaken += amount;
        }

        /// <summary>Record a collectible found.</summary>
        public void RecordCollectibleFound()
        {
            _currentProgress.CollectiblesFound++;
        }

        /// <summary>Mark boss as defeated.</summary>
        public void MarkBossDefeated()
        {
            _currentProgress.BossDefeated = true;
        }

        /// <summary>Elapsed play time tracking.</summary>
        public void AddElapsedTime(float delta)
        {
            _currentProgress.ElapsedTime += delta;
        }

        /// <summary>
        /// Calculate final rating (S/A/B/C/D) based on performance metrics.
        /// Score thresholds: S>=90, A>=75, B>=55, C>=35, D<35.
        /// Includes difficulty multiplier. Publishes DungeonRatingEvent.
        /// </summary>
        public DungeonRating CalculateRating()
        {
            var data = _currentProgress;
            int score = 0;

            // Base score from rooms cleared (max 30)
            score += Mathf.Min(data.RoomsCleared * 5, 30);

            // Boss defeat bonus (30)
            if (data.BossDefeated)
                score += 30;

            // Efficiency: fewer enemies defeated = more efficient (max 15)
            score += Mathf.Max(0, 15 - data.EnemiesDefeated);

            // Survivability: less damage taken = better (max 15)
            score += Mathf.Max(0, 15 - Mathf.FloorToInt(data.DamageTaken / 50f));

            // Collectibles found (max 10)
            score += Mathf.Min(data.CollectiblesFound * 2, 10);

            // Difficulty multiplier
            float diffMultiplier = data.Difficulty switch
            {
                DungeonDifficulty.Easy => 0.8f,
                DungeonDifficulty.Normal => 1.0f,
                DungeonDifficulty.Hard => 1.2f,
                DungeonDifficulty.Nightmare => 1.5f,
                _ => 1.0f
            };
            score = Mathf.RoundToInt(score * diffMultiplier);

            // Map score to rating
            DungeonRating rating;
            if (score >= 90) rating = DungeonRating.S;
            else if (score >= 75) rating = DungeonRating.A;
            else if (score >= 55) rating = DungeonRating.B;
            else if (score >= 35) rating = DungeonRating.C;
            else rating = DungeonRating.D;

            // Bonus multiplier for rewards
            float bonusMultiplier = rating switch
            {
                DungeonRating.S => 2.0f,
                DungeonRating.A => 1.5f,
                DungeonRating.B => 1.2f,
                DungeonRating.C => 1.0f,
                DungeonRating.D => 0.5f,
                _ => 1.0f
            };

            EventBus.Publish(new DungeonRatingEvent
            {
                DungeonId = data.DungeonId,
                Difficulty = data.Difficulty,
                Rating = rating,
                Score = score,
                BonusMultiplier = bonusMultiplier
            });

            Debug.Log($"[DungeonProgress] Rating: {rating} (Score: {score}, Bonus: x{bonusMultiplier:F1})");
            return rating;
        }

        // ─── Internal ────────────────────────────────────────────────────

        private static string GetSaveKey(string dungeonId, string playerId)
        {
            return $"{SAVE_KEY_PREFIX}{playerId}_{dungeonId}";
        }
    }
}
