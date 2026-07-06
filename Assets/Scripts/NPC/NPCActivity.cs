using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC活动系统 —— NPC不只是站着。他们做自己的事：打铁、采药、喝酒、看书。
    /// 玩家观察到的NPC行为反映他们的性格和秘密。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCActivity : MonoBehaviour
    {
        public enum Activity { Idle, Working, Resting, Walking, Training, Hiding, Praying }

        public Activity currentActivity = Activity.Idle;
        public string[] workLines; // 工作时说的话
        public float activityChangeInterval = 60f;

        private NPCBase _npc;
        private float _nextChange;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _nextChange = Time.time + Random.Range(30f, 90f);
            PickActivity();
        }

        void Update()
        {
            if (Time.time >= _nextChange)
            {
                _nextChange = Time.time + Random.Range(activityChangeInterval * 0.5f, activityChangeInterval * 1.5f);
                PickActivity();
            }

            // 偶尔自言自语
            if (Random.value < 0.001f && workLines != null && workLines.Length > 0)
            {
                string line = workLines[Random.Range(0, workLines.Length)];
                Debug.Log($"[{_npc.npcName}] 💭 '{line}'");
            }
        }

        void PickActivity()
        {
            var hour = TimeManager.Instance?.GameHour ?? 12;

            currentActivity = _npc.npcId switch
            {
                "npc_wang_001" => hour switch { >= 6 and < 18 => Activity.Working, _ => Activity.Resting },
                "npc_li_001" => hour switch { >= 8 and < 16 => Activity.Working, >= 20 => Activity.Praying, _ => Activity.Idle },
                "npc_zhang_001" => hour switch { >= 20 or < 4 => Activity.Praying, >= 10 and < 16 => Activity.Training, _ => Activity.Resting },
                "npc_chen_001" => hour switch { >= 6 and < 18 => Activity.Working, _ => Activity.Walking },
                "npc_zhao_001" => hour switch { >= 6 and < 22 => Activity.Working, _ => Activity.Resting },
                _ => Activity.Idle
            };
        }

        public string GetActivityDescription()
        {
            return currentActivity switch
            {
                Activity.Working => "正在忙碌",
                Activity.Resting => "正在休息",
                Activity.Walking => "正在散步",
                Activity.Training => "正在修炼",
                Activity.Praying => "正在祈祷...",
                Activity.Hiding => "躲藏着什么",
                _ => ""
            };
        }
    }
}
