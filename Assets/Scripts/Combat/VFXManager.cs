using UnityEngine;
using System.Collections;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 4K VFX管理器 —— 东方修真视觉特效。
    /// Phase1：灵击弹道 + 命中爆发 + 暴击强化。
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("弹道")]
        public Color spiritBoltColor = new Color(0.82f, 0.91f, 1f);
        public Color swordBoltColor = new Color(0.63f, 0.91f, 0.75f);

        [Header("命中爆发")]
        public Color hitFlashColor = Color.white;
        public Color hitRingColor = new Color(0.5f, 0.82f, 1f);
        public Color critBurstColor = new Color(1f, 0.82f, 0.5f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        /// <summary>灵击弹道：从玩家飞向目标</summary>
        public void SpawnSpiritBolt(Vector3 from, Vector3 to, bool isCrit = false)
        {
            StartCoroutine(BoltFlight(from, to, isCrit ? critBurstColor : spiritBoltColor, isCrit ? 1.5f : 1f));
        }

        IEnumerator BoltFlight(Vector3 from, Vector3 to, Color color, float speed)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "SpiritBolt";
            bolt.transform.position = from;
            bolt.transform.localScale = Vector3.one * 0.2f;
            var r = bolt.GetComponent<Renderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = color;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 2f);
                r.material = m;
            }
            bolt.GetComponent<Collider>().isTrigger = true;

            // 尾迹
            var trail = new GameObject("BoltTrail"); trail.transform.SetParent(bolt.transform);
            trail.transform.localPosition = Vector3.zero;
            var tr = trail.AddComponent<TrailRenderer>();
            tr.time = 0.15f; tr.startWidth = 0.1f; tr.endWidth = 0f;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            tr.startColor = color; tr.endColor = new Color(color.r, color.g, color.b, 0);

            float elapsed = 0;
            float duration = Vector3.Distance(from, to) / (10f * speed);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bolt.transform.position = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            // 命中爆发
            SpawnHitBurst(to, color);
            Destroy(bolt, 0.3f);
        }

        /// <summary>命中爆发：冲击波+灵屑</summary>
        public void SpawnHitBurst(Vector3 position, Color color)
        {
            // 冲击波环
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HitRing"; ring.transform.position = position;
            ring.transform.localScale = new Vector3(0.1f, 0.03f, 0.1f);
            var rr = ring.GetComponent<Renderer>();
            if (rr != null)
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(color.r, color.g, color.b, 0.5f);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.5f);
                rr.material = m;
            }
            ring.GetComponent<Collider>().isTrigger = true;
            StartCoroutine(ExpandAndFade(ring, 3f, 0.4f));

            // 灵屑粒子
            for (int i = 0; i < 15; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "Spark"; spark.transform.position = position;
                spark.transform.localScale = Vector3.one * 0.05f;
                var sr = spark.GetComponent<Renderer>();
                if (sr != null) { var sm = new Material(Shader.Find("Standard")); sm.color = color; sm.EnableKeyword("_EMISSION"); sm.SetColor("_EmissionColor", color); sr.material = sm; }
                spark.GetComponent<Collider>().isTrigger = true;
                var rb = spark.AddComponent<Rigidbody>();
                rb.useGravity = false; rb.velocity = Random.insideUnitSphere * 3f;
                Destroy(spark, 0.8f);
            }
        }

        IEnumerator ExpandAndFade(GameObject go, float maxScale, float duration)
        {
            float elapsed = 0;
            Vector3 start = go.transform.localScale;
            var r = go.GetComponent<Renderer>();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                go.transform.localScale = Vector3.Lerp(start, new Vector3(maxScale, 0.03f, maxScale), t);
                if (r != null) r.material.color = new Color(r.material.color.r, r.material.color.g, r.material.color.b, 1f - t);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>暴击强化：星芒爆发</summary>
        public void SpawnCritBurst(Vector3 position)
        {
            SpawnHitBurst(position, critBurstColor);
            // Extra golden star burst
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                var line = new GameObject("CritLine");
                line.transform.position = position;
                var lr = line.AddComponent<LineRenderer>();
                lr.startWidth = 0.05f; lr.endWidth = 0f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = critBurstColor; lr.endColor = new Color(1f, 0.82f, 0.5f, 0);
                lr.SetPosition(0, position);
                lr.SetPosition(1, position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2f);
                Destroy(line, 0.3f);
            }
        }
    }
}
