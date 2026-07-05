using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 快速旅行点 —— 站上去按F传送到其他已发现的旅行点。
    /// </summary>
    public class FastTravel : MonoBehaviour
    {
        public string pointName;
        public string pointId;
        public float activateRange = 3f;

        private static Dictionary<string, Vector3> _discovered = new();
        private Transform _player;
        private bool _inRange;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            CreateVisual();
            // 起始点自动发现
            if (pointId == "village_center") _discovered[pointId] = transform.position;
        }

        void CreateVisual()
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(transform); pillar.transform.localPosition = Vector3.zero;
            pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            var r = pillar.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.2f, 0.6f, 0.9f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.2f,0.6f,0.9f)*0.3f); r.material = m; }

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere); glow.transform.SetParent(transform);
            glow.transform.localPosition = Vector3.up * 1f; glow.transform.localScale = Vector3.one * 0.3f;
            glow.GetComponent<Collider>().isTrigger = true;
            var gr = glow.GetComponent<Renderer>();
            if (gr != null) { var gm = new Material(Shader.Find("Standard")); gm.color = new Color(0.3f,0.7f,1f); gm.EnableKeyword("_EMISSION"); gm.SetColor("_EmissionColor", new Color(0.3f,0.7f,1f)*0.5f); gr.material = gm; }
        }

        void Update()
        {
            if (_player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            _inRange = dist <= activateRange;

            if (_inRange)
            {
                // 发现新旅行点
                if (!_discovered.ContainsKey(pointId))
                {
                    _discovered[pointId] = transform.position;
                    Debug.Log($"[Travel] 发现旅行点: {pointName}");
                    EventBus.Publish("OnTravelPointDiscovered", new Dictionary<string, object> {
                        {"name", pointName}, {"id", pointId}
                    });
                }

                if (Input.GetKeyDown(KeyCode.F))
                {
                    ShowTravelMenu();
                }
            }
        }

        void ShowTravelMenu()
        {
            Debug.Log($"═══════ 快速旅行 ═══════");
            int idx = 1;
            foreach (var kv in _discovered)
            {
                if (kv.Key == pointId) continue; // Skip current
                float dist = Vector3.Distance(transform.position, kv.Value);
                Debug.Log($"  {idx}. {kv.Key} ({dist:F0}m)");
                idx++;
            }
            if (idx == 1) Debug.Log("  尚未发现其他旅行点。");
            Debug.Log($"  按F+数字传送 (如F1=传送到第1个)");
        }

        public static bool Travel(string targetId)
        {
            if (!_discovered.ContainsKey(targetId)) return false;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return false;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = _discovered[targetId] + Vector3.up * 1.5f;
            if (cc != null) cc.enabled = true;

            Debug.Log($"[Travel] 传送到 {targetId}");
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, activateRange);
        }
    }
}
