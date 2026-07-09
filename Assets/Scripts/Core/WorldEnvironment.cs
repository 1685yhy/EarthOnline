using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 4K级世界环境 —— 灵气粒子、体积雾、环境光、后处理氛围。
    /// 不是"绿色Plane"——是"灵气大陆"。
    /// </summary>
    public class WorldEnvironment : MonoBehaviour
    {
        public static WorldEnvironment Instance { get; private set; }

        [Header("灵气粒子")]
        public GameObject spiritParticlePrefab;
        public int particleCount = 200;
        public float particleSpreadRadius = 40f;
        public float particleHeight = 8f;
        public Color particleColor = new Color(0.3f, 0.6f, 1f, 0.3f);

        [Header("环境光")]
        public Color ambientColor = new Color(0.15f, 0.1f, 0.25f);
        public Color fogColor = new Color(0.1f, 0.08f, 0.2f);
        public float fogDensity = 0.003f;

        private GameObject[] _particles;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupEnvironment();
            SpawnSpiritParticles();
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            Debug.Log("[环境] 🌌 4K灵气大陆环境就绪：体积雾+环境光+灵气粒子");
        }

        void SpawnSpiritParticles()
        {
            _particles = new GameObject[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"SpiritParticle_{i}";
                go.transform.SetParent(transform);
                go.transform.position = new Vector3(
                    Random.Range(-particleSpreadRadius, particleSpreadRadius),
                    Random.Range(0.5f, particleHeight),
                    Random.Range(-particleSpreadRadius, particleSpreadRadius)
                );
                go.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var m = new Material(Shader.Find("Standard"));
                    m.color = particleColor;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", particleColor * Random.Range(0.3f, 0.8f));
                    r.material = m;
                }
                go.GetComponent<Collider>().isTrigger = true;
                _particles[i] = go;
            }
        }

        void Update()
        {
            // 灵气粒子缓慢飘动
            if (_particles == null) return;
            float t = Time.time;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;
                var p = _particles[i].transform;
                p.position += new Vector3(
                    Mathf.Sin(t * 0.3f + i) * 0.003f,
                    Mathf.Cos(t * 0.5f + i) * 0.002f,
                    Mathf.Cos(t * 0.4f + i) * 0.003f
                );
                // Bounds check
                if (p.position.y > particleHeight) p.position = new Vector3(p.position.x, 0.5f, p.position.z);
                if (p.position.y < 0.3f) p.position = new Vector3(p.position.x, particleHeight, p.position.z);
            }
        }
    }
}
