using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V2.1 NPC自然日程 —— 走路去目的地，不是瞬移。
    /// AI总监："小时定点传送到位置→玩家看到会出戏。Skyrim里NPC从家走到市场需要真实时间。"
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCNaturalSchedule : MonoBehaviour
    {
        [System.Serializable]
        public class DailySlot
        {
            public int startHour, endHour;
            public Vector3 destination;
            public string activity;
            public float speed = 1.5f;
        }

        public DailySlot[] schedule;
        private NPCBase _npc;
        private CharacterController _cc;
        private int _currentSlotIndex = -1;
        private bool _moving;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _cc = GetComponent<CharacterController>();
            if (_cc == null) { _cc = gameObject.AddComponent<CharacterController>(); _cc.center = new Vector3(0, 1, 0); _cc.height = 2f; _cc.radius = 0.5f; }

            if (schedule == null || schedule.Length == 0)
                schedule = GetDefaultSchedule();

            EventBus.Subscribe("OnHourChanged", OnHourChanged);
            ApplyCurrentSlot();
        }

        DailySlot[] GetDefaultSchedule()
        {
            return _npc.npcId switch
            {
                "npc_wang_001" => new[] {
                    new DailySlot { startHour=6, endHour=12, destination=new Vector3(-6,1.2f,7), activity="打铁", speed=1.5f },
                    new DailySlot { startHour=12, endHour=13, destination=new Vector3(-2,1.2f,-2), activity="去客栈吃午饭", speed=2f },
                    new DailySlot { startHour=13, endHour=18, destination=new Vector3(-6,1.2f,7), activity="打铁", speed=1.5f },
                    new DailySlot { startHour=18, endHour=6, destination=new Vector3(-6,1.2f,6), activity="休息", speed=1f },
                },
                _ => new[] { new DailySlot { startHour=0, endHour=24, destination=transform.position, activity="待机", speed=1f } }
            };
        }

        void OnHourChanged(Dictionary<string, object> _) => ApplyCurrentSlot();

        void ApplyCurrentSlot()
        {
            if (TimeManager.Instance == null || schedule == null) return;
            int hour = TimeManager.Instance.GameHour;

            for (int i = 0; i < schedule.Length; i++)
            {
                var slot = schedule[i];
                bool inSlot = slot.startHour <= hour && hour < slot.endHour;
                if (!inSlot) continue;
                if (i == _currentSlotIndex) return; // Same slot

                _currentSlotIndex = i;
                float dist = Vector3.Distance(transform.position, slot.destination);
                if (dist > 0.5f)
                {
                    _moving = true;
                    Debug.Log($"[{_npc.npcName}] 🚶 {slot.activity} — 走向目的地({dist:F0}m)");
                }
                break;
            }
        }

        void Update()
        {
            if (!_moving || _currentSlotIndex < 0) return;
            var slot = schedule[_currentSlotIndex];

            Vector3 target = slot.destination;
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0;

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(target.x, 0, target.z));

            if (dist <= 0.5f)
            {
                _moving = false;
                return;
            }

            transform.forward = Vector3.Lerp(transform.forward, dir, 2f * Time.deltaTime);
            if (_cc != null) _cc.SimpleMove(dir * slot.speed);
        }

        void OnDestroy() => EventBus.Unsubscribe("OnHourChanged", OnHourChanged);
    }
}
