using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPCScheduleLoader —— 从 Resources/Data/NPCSchedules.json 加载行程数据，
    /// 自动为场景中所有 NPCBase 配置 NPCSchedule 和 NPCNaturalSchedule。
    /// </summary>
    public class NPCScheduleLoader : MonoBehaviour
    {
        [System.Serializable]
        public class TimeBlock
        {
            public int startHour;
            public int endHour;
            public string activityName;
            public string activityType; // Work / Rest / Social / Travel / Eat / Training
            public Vector3 location;
            public string animationHint;
            public bool isInterruptible;
        }

        [System.Serializable]
        public class NpcScheduleEntry
        {
            public string npcId;
            public string npcName;
            public TimeBlock[] timeBlocks;
        }

        [System.Serializable]
        public class ScheduleRoot
        {
            public string version;
            public string generatedForScene;
            public string description;
            public NpcScheduleEntry[] npcSchedules;
        }

        private Dictionary<string, NpcScheduleEntry> _scheduleMap;
        private bool _loaded = false;

        void Start()
        {
            LoadScheduleData();
            ApplyAllSchedules();
        }

        void LoadScheduleData()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/NPCSchedules");
            if (jsonAsset == null)
            {
                Debug.LogError("[NPCScheduleLoader] 找不到 NPCSchedules.json！请确保文件在 Resources/Data/ 目录下。");
                return;
            }

            try
            {
                ScheduleRoot root = JsonUtility.FromJson<ScheduleRoot>(jsonAsset.text);
                if (root == null || root.npcSchedules == null)
                {
                    Debug.LogError("[NPCScheduleLoader] JSON 解析失败：格式无效。");
                    return;
                }

                _scheduleMap = new Dictionary<string, NpcScheduleEntry>();
                foreach (var entry in root.npcSchedules)
                {
                    if (!string.IsNullOrEmpty(entry.npcId))
                    {
                        _scheduleMap[entry.npcId] = entry;
                    }
                }

                _loaded = true;
                Debug.Log($"[NPCScheduleLoader] 成功加载 {_scheduleMap.Count} 个NPC的行程数据。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCScheduleLoader] JSON 解析异常：{e.Message}");
            }
        }

        void ApplyAllSchedules()
        {
            if (!_loaded || _scheduleMap == null) return;

            NPCBase[] allNpcs = FindObjectsOfType<NPCBase>(true);
            int appliedCount = 0;

            foreach (var npc in allNpcs)
            {
                if (string.IsNullOrEmpty(npc.npcId) || !_scheduleMap.ContainsKey(npc.npcId))
                    continue;

                NpcScheduleEntry entry = _scheduleMap[npc.npcId];
                ApplyToNPCSchedule(npc, entry);
                ApplyToNPCNaturalSchedule(npc, entry);
                appliedCount++;
            }

            Debug.Log($"[NPCScheduleLoader] 已为 {appliedCount}/{allNpcs.Length} 个NPC配置行程。");
            if (appliedCount < allNpcs.Length)
            {
                Debug.LogWarning($"[NPCScheduleLoader] {(allNpcs.Length - appliedCount)} 个NPC未找到匹配行程数据。");
            }
        }

        void ApplyToNPCSchedule(NPCBase npc, NpcScheduleEntry entry)
        {
            NPCSchedule schedule = npc.GetComponent<NPCSchedule>();
            if (schedule == null) return;

            var slots = new List<NPCSchedule.TimeSlot>();
            foreach (var block in entry.timeBlocks)
            {
                slots.Add(new NPCSchedule.TimeSlot
                {
                    startHour = block.startHour,
                    endHour = block.endHour,
                    position = block.location,
                    activity = block.activityName
                });
            }
            schedule.schedule = slots.ToArray();
        }

        void ApplyToNPCNaturalSchedule(NPCBase npc, NpcScheduleEntry entry)
        {
            NPCNaturalSchedule naturalSchedule = npc.GetComponent<NPCNaturalSchedule>();
            if (naturalSchedule == null) return;

            var dailySlots = new List<NPCNaturalSchedule.DailySlot>();
            foreach (var block in entry.timeBlocks)
            {
                float speed = block.activityType switch
                {
                    "Travel" => 2.5f,
                    "Work" => 1.5f,
                    "Social" => 1.8f,
                    "Training" => 2.0f,
                    "Eat" => 1.5f,
                    "Rest" => 1.0f,
                    _ => 1.5f
                };

                dailySlots.Add(new NPCNaturalSchedule.DailySlot
                {
                    startHour = block.startHour,
                    endHour = block.endHour,
                    destination = block.location,
                    activity = block.activityName,
                    speed = speed
                });
            }
            naturalSchedule.schedule = dailySlots.ToArray();
        }

        /// <summary>
        /// 运行时重新加载行程数据（用于调试或动态更新）。
        /// </summary>
        public void Reload()
        {
            _loaded = false;
            LoadScheduleData();
            ApplyAllSchedules();
        }
    }
}
