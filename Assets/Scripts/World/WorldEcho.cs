using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.1 世界回响系统 —— 踏入重要地点时触发3-8秒的记忆闪回。
    /// 叙事总监建议的#1叙事系统：让玩家"感受到"故事而非"读到"。
    /// </summary>
    public class WorldEcho : MonoBehaviour
    {
        public string echoId;
        public string echoTitle;
        [TextArea(3, 10)]
        public string echoText;           // 回响的文本描述
        public string connectedGiftId;    // 关联的金手指ID
        public string connectedNpcId;     // 关联的NPC ID
        public float triggerRadius = 4f;
        public bool oneShot = true;       // 只触发一次

        private bool _triggered;
        private Transform _player;
        private ParticleSystem _echoParticles;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            CreateEchoVisual();
        }

        void CreateEchoVisual()
        {
            // 淡淡的光晕标记回响位置
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "EchoGlow"; glow.transform.SetParent(transform);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * 0.5f;
            var r = glow.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.3f, 0.6f, 0.9f, 0.2f);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 0.9f) * 0.5f);
                r.material = m;
            }
            glow.GetComponent<Collider>().isTrigger = true;
        }

        void Update()
        {
            if (_triggered && oneShot) return;
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= triggerRadius && !_triggered)
            {
                TriggerEcho();
            }
        }

        void TriggerEcho()
        {
            _triggered = true;

            Debug.Log($"🌌 ═══════════════════════════════");
            Debug.Log($"🌌 【世界回响】{echoTitle}");
            Debug.Log($"🌌 ═══════════════════════════════");
            Debug.Log($"🌌 {echoText}");
            Debug.Log($"🌌 ═══════════════════════════════");

            // 给一点修为奖励——探索世界应该得到回馈
            PlayerStats.Instance?.AddCultivation(20);

            EventBus.Publish("OnEchoTriggered", new Dictionary<string, object> {
                {"echoId", echoId}, {"title", echoTitle},
                {"connectedGiftId", connectedGiftId}, {"connectedNpcId", connectedNpcId}
            });
        }
    }
}
