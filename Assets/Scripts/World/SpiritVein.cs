using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 灵脉节点 —— 特定位置的灵气浓度更高。站在上面修炼加速。
    /// 灵脉是有限的——控制灵脉=控制修炼资源。
    /// </summary>
    public class SpiritVein : MonoBehaviour
    {
        public string veinName = "小型灵脉";
        public float cultivationMultiplier = 1.5f;
        public float spiritRegenBonus = 3f;
        public float radius = 5f;
        public bool isOccupied = false;

        private Transform _player;
        private float _pulseTimer;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            CreateVisual();
        }

        void CreateVisual()
        {
            // Ground glow effect
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "VeinGlow"; glow.transform.SetParent(transform);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = new Vector3(radius * 0.3f, 0.05f, radius * 0.3f);
            var r = glow.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.2f, 0.5f, 0.9f, 0.5f);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.9f) * 0.3f);
                r.material = m;
            }
            glow.GetComponent<Collider>().isTrigger = true;
        }

        void Update()
        {
            if (_player == null) return;
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(_player.position.x, 0, _player.position.z));

            if (dist <= radius)
            {
                // 灵气加速
                var combat = EarthOnline.Combat.CombatSystem.Instance;
                if (combat != null)
                {
                    // Boost spirit regen via direct modification
                    var f = typeof(EarthOnline.Combat.CombatSystem).GetField("spiritRegenRate",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    // Simple approach: give cultivation boost
                }

                var stats = PlayerStats.Instance;
                if (stats != null && Time.frameCount % 300 == 0) // every ~5 seconds
                {
                    int boost = Mathf.RoundToInt(cultivationMultiplier);
                    if (boost > 1) stats.AddCultivation(1);
                }
            }

            // Visual pulse
            _pulseTimer += Time.deltaTime;
            var glowObj = transform.Find("VeinGlow");
            if (glowObj != null)
            {
                float pulse = 1f + Mathf.Sin(_pulseTimer * 2f) * 0.1f;
                glowObj.localScale = new Vector3(radius * 0.3f * pulse, 0.05f, radius * 0.3f * pulse);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 0.9f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
