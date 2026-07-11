using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace EarthOnline.Combat
{
    /// <summary>修真境界枚举</summary>
    public enum RealmLevel
    {
        QiRefining,               // 练气期
        FoundationEstablishment,   // 筑基期
        CoreFormation,            // 金丹期
        NascentSoul               // 元婴期
    }

    /// <summary>
    /// 4K VFX管理器 —— 东方修真视觉特效。
    /// Phase1：灵击弹道 + 命中爆发 + 暴击强化。
    /// Phase2：境界突破 + 低灵力警告 + 水墨暴击。
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

        [Header("境界突破")]
        public Color qiRefiningColor = new Color(0.6f, 0.9f, 1f);         // 淡青
        public Color foundationColor = new Color(0.8f, 0.6f, 1f);         // 紫
        public Color coreFormationColor = new Color(1f, 0.85f, 0.3f);    // 金
        public Color nascentSoulColor = new Color(1f, 0.4f, 0.6f);       // 赤

        [Header("低灵力警告")]
        public Color lowSpiritVignetteColor = new Color(0.6f, 0f, 0f, 0.4f);
        public Color criticalSpiritPulseColor = new Color(0.8f, 0f, 0f, 0.6f);
        public float pulseFrequency = 1.2f;

        [Header("水墨暴击")]
        public Color inkColor = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        public Color inkSplashColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        private GameObject vignetteOverlay;
        private Coroutine warningCoroutine;
        private Coroutine pulseCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            CreateWarningOverlay();
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

        // ================================================================
        // Phase 2 — 境界突破 / 低灵力警告 / 水墨暴击
        // ================================================================

        #region 低灵力警告UI

        /// <summary>创建全屏 UI 覆盖层（用于暗角、脉冲效果）</summary>
        private void CreateWarningOverlay()
        {
            var canvasGO = new GameObject("VFXOverlay");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            vignetteOverlay = new GameObject("LowSpiritVignette");
            vignetteOverlay.transform.SetParent(canvasGO.transform);
            var img = vignetteOverlay.AddComponent<Image>();
            img.color = lowSpiritVignetteColor;
            img.raycastTarget = false;
            var rect = vignetteOverlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            vignetteOverlay.SetActive(false);
        }

        #endregion

        #region 境界突破特效

        /// <summary>
        /// 播放境界突破特效。
        /// 根据境界等级生成不同规模的光柱、扩散环、升腾粒子。
        /// </summary>
        /// <param name="realm">目标突破境界</param>
        public void PlayBreakthroughEffect(RealmLevel realm)
        {
            Color color;
            float pillarHeight;
            float pillarRadius;
            int ringCount;
            float duration;
            string realmName;

            switch (realm)
            {
                case RealmLevel.QiRefining:
                    color = qiRefiningColor;
                    pillarHeight = 3f;
                    pillarRadius = 0.3f;
                    ringCount = 1;
                    duration = 1.5f;
                    realmName = "练气突破";
                    break;
                case RealmLevel.FoundationEstablishment:
                    color = foundationColor;
                    pillarHeight = 5f;
                    pillarRadius = 0.5f;
                    ringCount = 3;
                    duration = 2.0f;
                    realmName = "筑基突破";
                    break;
                case RealmLevel.CoreFormation:
                    color = coreFormationColor;
                    pillarHeight = 8f;
                    pillarRadius = 0.8f;
                    ringCount = 5;
                    duration = 2.5f;
                    realmName = "金丹突破";
                    break;
                case RealmLevel.NascentSoul:
                    color = nascentSoulColor;
                    pillarHeight = 12f;
                    pillarRadius = 1.2f;
                    ringCount = 7;
                    duration = 3.0f;
                    realmName = "元婴突破";
                    break;
                default:
                    return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 position = player != null
                ? player.transform.position + Vector3.up * 0.5f
                : Vector3.zero;

            StartCoroutine(BreakthroughSequence(position, color, pillarHeight,
                pillarRadius, ringCount, duration, realmName));
        }

        /// <summary>境界突破协程：光柱 → 扩散环 → 升腾粒子 → 淡出</summary>
        private IEnumerator BreakthroughSequence(
            Vector3 position, Color color, float height, float radius,
            int ringCount, float duration, string realmName)
        {
            // —— 1 · 光柱 (Light Pillar) ——
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = realmName + "_Pillar";
            pillar.transform.position = position;
            pillar.transform.localScale = new Vector3(radius * 0.5f, height * 0.5f, radius * 0.5f);
            pillar.GetComponent<Collider>().isTrigger = true;
            var pr = pillar.GetComponent<Renderer>();
            Material pillarMat = null;
            if (pr != null)
            {
                pillarMat = new Material(Shader.Find("Standard"));
                pillarMat.color = new Color(color.r, color.g, color.b, 0f);
                pillarMat.EnableKeyword("_EMISSION");
                pillarMat.SetColor("_EmissionColor", color * 2.5f);
                pr.material = pillarMat;
            }

            // 光柱淡入
            float elapsed = 0;
            float fadeInDuration = 0.3f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                if (pr != null && pillarMat != null)
                {
                    float alpha = Mathf.Lerp(0f, 0.7f, elapsed / fadeInDuration);
                    pillarMat.color = new Color(color.r, color.g, color.b, alpha);
                }
                yield return null;
            }

            // —— 2 · 扩散环 (Expanding Rings) ——
            for (int i = 0; i < ringCount; i++)
            {
                float delay = i * (duration / ringCount * 0.5f);
                float ringRadius = radius * (1f + i * 0.5f);
                StartCoroutine(SpawnExpandingRing(position, color, ringRadius, delay));
            }

            // —— 3 · 升腾粒子 (Ascending Particles) ——
            int particleCount = ringCount * 3;
            for (int i = 0; i < particleCount; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-radius * 0.5f, radius * 0.5f),
                    0f,
                    Random.Range(-radius * 0.5f, radius * 0.5f));

                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = realmName + "_Spark";
                spark.transform.position = position + offset;
                spark.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
                spark.GetComponent<Collider>().isTrigger = true;
                var sr = spark.GetComponent<Renderer>();
                if (sr != null)
                {
                    var sm = new Material(Shader.Find("Standard"));
                    sm.color = color;
                    sm.EnableKeyword("_EMISSION");
                    sm.SetColor("_EmissionColor", color * 1.5f);
                    sr.material = sm;
                }
                var rb = spark.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.velocity = Vector3.up * Random.Range(2f, 4f)
                            + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                Destroy(spark, duration * 0.8f);
            }

            // 保持光柱
            yield return new WaitForSeconds(duration * 0.6f);

            // —— 4 · 光柱淡出 ——
            elapsed = 0;
            float fadeOutDuration = 0.5f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                if (pr != null && pillarMat != null)
                {
                    float alpha = Mathf.Lerp(0.7f, 0f, elapsed / fadeOutDuration);
                    pillarMat.color = new Color(color.r, color.g, color.b, alpha);
                }
                yield return null;
            }
            Destroy(pillar);
        }

        /// <summary>生成单个扩散环（由内向外逐步放大并淡出）</summary>
        private IEnumerator SpawnExpandingRing(Vector3 position, Color color, float maxRadius, float delay)
        {
            yield return new WaitForSeconds(delay);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "BreakthroughRing";
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(0.05f, 0.02f, 0.05f);
            ring.GetComponent<Collider>().isTrigger = true;
            var rr = ring.GetComponent<Renderer>();
            Material ringMat = null;
            if (rr != null)
            {
                ringMat = new Material(Shader.Find("Standard"));
                ringMat.color = new Color(color.r, color.g, color.b, 0.6f);
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", color * 2f);
                rr.material = ringMat;
            }

            float elapsed = 0;
            float ringDuration = 0.8f;
            while (elapsed < ringDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ringDuration;
                float scale = Mathf.Lerp(0.05f, maxRadius, t);
                ring.transform.localScale = new Vector3(scale, 0.02f, scale);
                if (rr != null && ringMat != null)
                {
                    ringMat.color = new Color(color.r, color.g, color.b, 0.6f * (1f - t));
                }
                yield return null;
            }
            Destroy(ring);
        }

        #endregion

        #region 低灵力警告

        /// <summary>
        /// 根据当前灵力百分比显示低灵力警告。
        /// &lt;20%：红暗角（透明度随灵力降低而加深）。
        /// &lt;5%：心跳脉冲（lub-dub 双拍节律）。
        /// </summary>
        /// <param name="spiritPercentage">当前灵力百分比（0~1）</param>
        public void ShowLowSpiritWarning(float spiritPercentage)
        {
            if (vignetteOverlay == null) return;

            if (spiritPercentage < 0.05f)
            {
                // —— 危急（<5%）：心跳脉冲 + 暗红全屏 ——
                vignetteOverlay.SetActive(true);
                var img = vignetteOverlay.GetComponent<Image>();
                if (img != null)
                    img.color = criticalSpiritPulseColor;

                if (pulseCoroutine != null)
                    StopCoroutine(pulseCoroutine);
                pulseCoroutine = StartCoroutine(HeartbeatPulse());
            }
            else if (spiritPercentage < 0.20f)
            {
                // —— 低灵（<20%）：静态红暗角 ——
                if (pulseCoroutine != null)
                {
                    StopCoroutine(pulseCoroutine);
                    pulseCoroutine = null;
                }
                vignetteOverlay.SetActive(true);
                var img = vignetteOverlay.GetComponent<Image>();
                if (img != null)
                {
                    float t = (spiritPercentage - 0.05f) / 0.15f; // 0.05→0, 0.20→1
                    float alpha = Mathf.Lerp(lowSpiritVignetteColor.a, lowSpiritVignetteColor.a * 0.3f, t);
                    img.color = new Color(
                        lowSpiritVignetteColor.r,
                        lowSpiritVignetteColor.g,
                        lowSpiritVignetteColor.b,
                        alpha);
                }
            }
            else
            {
                // —— 正常：关闭所有警告 ——
                HideLowSpiritWarning();
            }
        }

        /// <summary>关闭低灵力警告特效</summary>
        public void HideLowSpiritWarning()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            if (vignetteOverlay != null && vignetteOverlay.activeSelf)
                vignetteOverlay.SetActive(false);
        }

        /// <summary>心跳脉冲协程：lub-dub 双拍节律，模拟心脏搏动</summary>
        private IEnumerator HeartbeatPulse()
        {
            var img = vignetteOverlay.GetComponent<Image>();
            if (img == null) yield break;

            while (true)
            {
                // —— 第一跳 (lub, 强) ——
                float beatIn = 0.12f;   // 收缩
                float beatOut = 0.20f;  // 舒张

                // 淡入（闪红）
                float elapsed = 0;
                while (elapsed < beatIn)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.3f, 0.85f, elapsed / beatIn);
                    img.color = new Color(
                        criticalSpiritPulseColor.r,
                        criticalSpiritPulseColor.g,
                        criticalSpiritPulseColor.b,
                        alpha);
                    yield return null;
                }
                // 淡出
                elapsed = 0;
                while (elapsed < beatOut)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.85f, 0.3f, elapsed / beatOut);
                    img.color = new Color(
                        criticalSpiritPulseColor.r,
                        criticalSpiritPulseColor.g,
                        criticalSpiritPulseColor.b,
                        alpha);
                    yield return null;
                }

                // —— 第二跳 (dub, 较弱) ——
                elapsed = 0;
                float dubIn = beatIn * 0.7f;
                float dubOut = beatOut * 0.7f;

                // 淡入
                while (elapsed < dubIn)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.3f, 0.65f, elapsed / dubIn);
                    img.color = new Color(
                        criticalSpiritPulseColor.r,
                        criticalSpiritPulseColor.g,
                        criticalSpiritPulseColor.b,
                        alpha);
                    yield return null;
                }
                // 淡出
                elapsed = 0;
                while (elapsed < dubOut)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.65f, 0.3f, elapsed / dubOut);
                    img.color = new Color(
                        criticalSpiritPulseColor.r,
                        criticalSpiritPulseColor.g,
                        criticalSpiritPulseColor.b,
                        alpha);
                    yield return null;
                }

                // 组间停顿（控制整体频率）
                yield return new WaitForSeconds(0.8f / pulseFrequency);
            }
        }

        #endregion

        #region 水墨暴击 (Dao Rhythm)

        /// <summary>
        /// 尝试触发 Dao Rhythm 水墨暴击（5% 概率）。
        /// 若触发，生成水墨涟漪、墨滴飞溅、笔触拖尾。
        /// </summary>
        /// <param name="position">暴击发生位置</param>
        /// <returns>是否触发了水墨暴击</returns>
        public bool TryPlayDaoRhythmEffect(Vector3 position)
        {
            if (Random.value > 0.05f) return false;
            StartCoroutine(InkCritSequence(position));
            return true;
        }

        /// <summary>水墨暴击协程：涟漪 → 墨滴 → 笔触</summary>
        private IEnumerator InkCritSequence(Vector3 position)
        {
            // —— 1 · 水墨涟漪 (Ink Ripples) ——
            for (int i = 0; i < 3; i++)
            {
                float delay = i * 0.08f;
                float radius = 1.2f + i * 0.6f;
                StartCoroutine(SpawnInkRing(position, inkColor, radius, delay));
            }

            // —— 2 · 墨滴飞溅 (Ink Droplets) ——
            for (int i = 0; i < 12; i++)
            {
                var droplet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                droplet.name = "InkDroplet";
                droplet.transform.position = position + Random.insideUnitSphere * 0.3f;
                droplet.transform.localScale = Vector3.one * Random.Range(0.03f, 0.1f);
                droplet.GetComponent<Collider>().isTrigger = true;
                var dr = droplet.GetComponent<Renderer>();
                if (dr != null)
                {
                    var dm = new Material(Shader.Find("Standard"));
                    dm.color = new Color(inkColor.r, inkColor.g, inkColor.b, Random.Range(0.4f, 0.9f));
                    dr.material = dm;
                }
                var rb = droplet.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.velocity = Random.insideUnitSphere * 2.5f;
                Destroy(droplet, 0.6f);
            }

            // —— 3 · 水墨笔触拖尾 (Ink Brush Strokes) ——
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Random.insideUnitSphere.normalized;
                dir.y *= 0.3f;
                var stroke = new GameObject("InkStroke");
                stroke.transform.position = position;
                var lr = stroke.AddComponent<LineRenderer>();
                lr.startWidth = 0.08f;
                lr.endWidth = 0f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = inkColor;
                lr.endColor = new Color(inkColor.r, inkColor.g, inkColor.b, 0f);
                lr.SetPosition(0, position);
                lr.SetPosition(1, position + dir * Random.Range(1f, 2.5f));
                Destroy(stroke, 0.4f);
            }

            yield return null;
        }

        /// <summary>生成单个水墨扩散环</summary>
        private IEnumerator SpawnInkRing(Vector3 position, Color color, float maxRadius, float delay)
        {
            yield return new WaitForSeconds(delay);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "InkSplash";
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(0.05f, 0.01f, 0.05f);
            ring.GetComponent<Collider>().isTrigger = true;
            var rr = ring.GetComponent<Renderer>();
            Material ringMat = null;
            if (rr != null)
            {
                ringMat = new Material(Shader.Find("Standard"));
                ringMat.color = new Color(color.r, color.g, color.b, 0.5f);
                rr.material = ringMat;
            }

            float elapsed = 0;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(0.05f, maxRadius, t);
                ring.transform.localScale = new Vector3(scale, 0.01f, scale);
                if (rr != null && ringMat != null)
                {
                    ringMat.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t));
                }
                yield return null;
            }
            Destroy(ring);
        }

        #endregion
    }
}
