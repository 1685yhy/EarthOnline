using System;
using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ─── Event Data ────────────────────────────────────────────────────

    /// <summary>Published when a dungeon reward drop rate is calculated.</summary>
    public struct DungeonRewardCalculatedEvent
    {
        public string DungeonId;
        public string PlayerId;
        public float BaseRate;
        public float AdjustedRate;
        public int ConsecutiveDays;
        public bool IsFloored;
    }

    /// <summary>Published when a daily record is reset for a player.</summary>
    public struct DungeonDailyResetEvent
    {
        public string PlayerId;
        public int DungeonsReset;
    }

    // ─── Reward Manager ────────────────────────────────────────────────

    /// <summary>
    /// Manages diminishing returns for dungeon rewards to prevent farming.
    /// Formula: DropRate = BaseRate * (1 - min(ConsecutiveDays, 7) * 0.13)
    /// Floor: 10% of base rate after 7 consecutive days.
    ///
    /// Tracks consecutive daily runs per dungeon per player.
    /// Consecutive counter resets if more than one day is skipped.
    /// Uses PlayerPrefs for persistence with in-memory cache.
    /// </summary>
    public class DungeonReward : MonoBehaviour
    {
        private const string RECORD_KEY_PREFIX = "DungeonReward_";
        private const double DAY_SECONDS = 86400d;
        private const int MAX_CONSECUTIVE = 7;
        private const float DECAY_PER_DAY = 0.13f;
        private const float FLOOR_RATE = 0.10f;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Header("Config")]
        [SerializeField] private string _playerId = "player_default";

        // Runtime cache: dungeonId -> record
        private readonly Dictionary<string, DailyRecord> _cache = new Dictionary<string, DailyRecord>();

        // ─── Types ───────────────────────────────────────────────────────

        [Serializable]
        private struct DailyRecord
        {
            public string DungeonId;
            public int ConsecutiveDays;
            public int TotalRuns;
            public double LastRunTimestamp;
        }

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Get the effective drop rate for a dungeon after applying diminishing returns.
        /// </summary>
        /// <param name="dungeonId">Dungeon identifier.</param>
        /// <param name="baseRate">Base drop rate (0-1).</param>
        /// <returns>Adjusted drop rate between floor and base.</returns>
        public float GetAdjustedDropRate(string dungeonId, float baseRate)
        {
            var record = LoadRecord(dungeonId);
            int consecutiveDays = record.ConsecutiveDays;

            // Check if last run was more than a day ago → reset streak for calculation
            double now = (DateTime.UtcNow - UnixEpoch).TotalSeconds;
            if (record.LastRunTimestamp > 0 && (now - record.LastRunTimestamp) > DAY_SECONDS)
            {
                consecutiveDays = 0;
            }

            // Apply diminishing formula: DropRate = Base * (1 - min(ConsecutiveDays, 7) * 0.13)
            int clampedDays = Mathf.Min(consecutiveDays, MAX_CONSECUTIVE);
            float decayFactor = clampedDays * DECAY_PER_DAY;
            float adjustedRate = baseRate * (1f - decayFactor);

            // Floor: never below 10% of base rate
            float floor = baseRate * FLOOR_RATE;
            adjustedRate = Mathf.Max(adjustedRate, floor);
            bool isFloored = adjustedRate <= floor + 0.001f;

            EventBus.Publish(new DungeonRewardCalculatedEvent
            {
                DungeonId = dungeonId,
                PlayerId = _playerId,
                BaseRate = baseRate,
                AdjustedRate = adjustedRate,
                ConsecutiveDays = consecutiveDays,
                IsFloored = isFloored
            });

            Debug.Log($"[DungeonReward] '{dungeonId}': base={baseRate:P0} adjusted={adjustedRate:P0} (consecutive={consecutiveDays}d, floored={isFloored})");
            return adjustedRate;
        }

        /// <summary>Record a completed run and update consecutive-day counter.</summary>
        public void RecordRun(string dungeonId)
        {
            var record = LoadRecord(dungeonId);
            double now = (DateTime.UtcNow - UnixEpoch).TotalSeconds;

            if (record.LastRunTimestamp > 0)
            {
                double hoursSinceLastRun = (now - record.LastRunTimestamp) / 3600d;

                if (hoursSinceLastRun < 24d)
                {
                    // Same calendar day window — don't double-count
                    Debug.Log($"[DungeonReward] Same-day run for '{dungeonId}', streak unchanged ({record.ConsecutiveDays}d)");
                }
                else if (hoursSinceLastRun < 48d)
                {
                    // Next day within window — increment streak
                    record.ConsecutiveDays++;
                    Debug.Log($"[DungeonReward] Consecutive day +1 for '{dungeonId}' -> {record.ConsecutiveDays}d");
                }
                else
                {
                    // Gap > 48h — streak broken, reset to 1
                    record.ConsecutiveDays = 1;
                    Debug.Log($"[DungeonReward] Streak broken for '{dungeonId}' (gap > 48h). Reset to 1d.");
                }
            }
            else
            {
                // First run ever
                record.ConsecutiveDays = 1;
            }

            record.LastRunTimestamp = now;
            record.TotalRuns++;
            record.DungeonId = dungeonId;

            SaveRecord(dungeonId, record);

            Debug.Log($"[DungeonReward] Run recorded for '{dungeonId}'. Consecutive: {record.ConsecutiveDays}d, total runs: {record.TotalRuns}");
        }

        /// <summary>Get the consecutive-day streak for a dungeon.</summary>
        public int GetConsecutiveDays(string dungeonId)
        {
            return LoadRecord(dungeonId).ConsecutiveDays;
        }

        /// <summary>Get total lifetime runs for a dungeon.</summary>
        public int GetTotalRuns(string dungeonId)
        {
            return LoadRecord(dungeonId).TotalRuns;
        }

        /// <summary>
        /// Get the effective reward multiplier combining diminishing-return factor
        /// with rating-based bonus multiplier.
        /// </summary>
        public float GetEffectiveMultiplier(string dungeonId, float ratingBonusMultiplier)
        {
            var record = LoadRecord(dungeonId);
            double now = (DateTime.UtcNow - UnixEpoch).TotalSeconds;

            int consecutiveDays = record.ConsecutiveDays;
            if (record.LastRunTimestamp > 0 && (now - record.LastRunTimestamp) > DAY_SECONDS)
                consecutiveDays = 0;

            int clampedDays = Mathf.Min(consecutiveDays, MAX_CONSECUTIVE);
            float diminishingFactor = 1f - clampedDays * DECAY_PER_DAY;
            diminishingFactor = Mathf.Max(diminishingFactor, FLOOR_RATE);

            float combined = diminishingFactor * ratingBonusMultiplier;
            Debug.Log($"[DungeonReward] Effective multiplier for '{dungeonId}': diminishing={diminishingFactor:F2} ratingBonus={ratingBonusMultiplier:F2} combined={combined:F2}");
            return combined;
        }

        /// <summary>Reset all reward records for current player (admin/debug).</summary>
        public void ResetAllRecords()
        {
            _cache.Clear();
            Debug.Log($"[DungeonReward] Cache cleared for player '{_playerId}'.");
        }

        /// <summary>Reset reward record for a specific dungeon.</summary>
        public void ResetRecord(string dungeonId)
        {
            _cache.Remove(dungeonId);
            string key = GetRecordKey(dungeonId);
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Debug.Log($"[DungeonReward] Record reset for '{dungeonId}'.");
        }

        // ─── Internal ────────────────────────────────────────────────────

        private DailyRecord LoadRecord(string dungeonId)
        {
            if (_cache.TryGetValue(dungeonId, out var cached))
                return cached;

            string key = GetRecordKey(dungeonId);
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                var record = JsonUtility.FromJson<DailyRecord>(json);
                _cache[dungeonId] = record;
                return record;
            }

            return new DailyRecord { DungeonId = dungeonId };
        }

        private void SaveRecord(string dungeonId, DailyRecord record)
        {
            _cache[dungeonId] = record;
            string key = GetRecordKey(dungeonId);
            string json = JsonUtility.ToJson(record);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        private string GetRecordKey(string dungeonId)
        {
            return $"{RECORD_KEY_PREFIX}{_playerId}_{dungeonId}";
        }
    }
}
