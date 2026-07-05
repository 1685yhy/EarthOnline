using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC日常行程 —— 不同时间在不同位置，让村子更生动。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCSchedule : MonoBehaviour
    {
        [System.Serializable]
        public class TimeSlot
        {
            public int startHour; public int endHour;
            public Vector3 position; public string activity;
        }

        public TimeSlot[] schedule;
        private NPCBase _npc;
        private CharacterController _cc;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _cc = GetComponent<CharacterController>();
            EventBus.Subscribe("OnHourChanged", OnHourChanged);
            ApplySchedule();
        }

        void OnHourChanged(Dictionary<string, object> _)
        {
            ApplySchedule();
        }

        void ApplySchedule()
        {
            if (TimeManager.Instance == null || schedule == null || schedule.Length == 0) return;

            int hour = TimeManager.Instance.GameHour;
            foreach (var slot in schedule)
            {
                if (hour >= slot.startHour && hour < slot.endHour)
                {
                    if (Vector3.Distance(transform.position, slot.position) > 0.5f)
                    {
                        // Teleport to schedule position
                        if (_cc != null) _cc.enabled = false;
                        transform.position = slot.position;
                        if (_cc != null) _cc.enabled = true;
                        Debug.Log($"[{_npc.npcName}] {slot.activity} ({slot.startHour}:00-{slot.endHour}:00)");
                    }
                    return;
                }
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnHourChanged", OnHourChanged);
        }
    }
}
