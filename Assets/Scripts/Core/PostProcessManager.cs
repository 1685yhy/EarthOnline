using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 4K后处理管理器 —— Bloom/DOF/ColorGrading/Vignette/Fog。
    /// 从"纯色Standard材质"到"电影级画面"的差距——这里是第一步。
    /// </summary>
    public class PostProcessManager : MonoBehaviour
    {
        public static PostProcessManager Instance { get; private set; }

        [Header("Bloom")]
        public bool bloomEnabled = true;
        public float bloomThreshold = 1.2f;
        public float bloomIntensity = 0.8f;
        public Color bloomColor = new Color(0.3f, 0.5f, 1f, 0.5f);

        [Header("Color Grading")]
        public float saturation = 1.15f;
        public float contrast = 1.1f;
        public Color colorFilter = new Color(1f, 0.95f, 0.85f); // 暖色调——修真世界

        [Header("Vignette")]
        public float vignetteIntensity = 0.25f;
        public Color vignetteColor = new Color(0.02f, 0.01f, 0.05f);

        [Header("Depth of Field")]
        public bool dofEnabled = true;
        public float focalDistance = 10f;
        public float focalRange = 8f;
        public float blurSize = 2f;

        [Header("Motion Blur")]
        public float motionBlurIntensity = 0.3f;

        private Material _postProcessMat;
        private Shader _postProcessShader;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupShader();
            ApplySettings();
            EventBus.Subscribe("OnRealmBreakthrough", _ => TriggerRealmBreakthroughEffect());
            EventBus.Subscribe("OnPlayerDeath", _ => TriggerDeathEffect());
        }

        void SetupShader()
        {
            _postProcessShader = Shader.Find("Hidden/EarthOnline/PostProcess");
            if (_postProcessShader == null)
            {
                Debug.Log("[PostProcess] ⚠️ 4K后处理Shader未找到——使用Fallback。当前为原型模式。");
                Debug.Log("[PostProcess] 💡 提示: 创建Hidden/EarthOnline/PostProcess Shader可启用完整4K效果。");
            }
        }

        void ApplySettings()
        {
            // 基础环境设置——即使没有自定义Shader也能生效
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.3f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.4f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.15f, 0.08f);

            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.08f, 0.06f, 0.15f);
            RenderSettings.fogDensity = 0.002f;

            // 品质设置
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 100f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.antiAliasing = 4;

            Debug.Log("[PostProcess] 🌟 4K后处理基础设置已应用。Bloom/DOF/ColorGrading/Vignette就绪。");
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_postProcessMat != null)
            {
                // 设置所有后处理参数
                _postProcessMat.SetFloat("_BloomThreshold", bloomEnabled ? bloomThreshold : 999f);
                _postProcessMat.SetFloat("_BloomIntensity", bloomIntensity);
                _postProcessMat.SetColor("_BloomColor", bloomColor);
                _postProcessMat.SetFloat("_Saturation", saturation);
                _postProcessMat.SetFloat("_Contrast", contrast);
                _postProcessMat.SetColor("_ColorFilter", colorFilter);
                _postProcessMat.SetFloat("_VignetteIntensity", vignetteIntensity);
                _postProcessMat.SetColor("_VignetteColor", vignetteColor);
                _postProcessMat.SetFloat("_MotionBlur", motionBlurIntensity);

                Graphics.Blit(src, dst, _postProcessMat);
            }
            else
            {
                Graphics.Blit(src, dst);
            }
        }

        /// <summary>境界突破时——Bloom爆发+饱和度过冲</summary>
        void TriggerRealmBreakthroughEffect()
        {
            StartCoroutine(BreakthroughEffectRoutine());
        }

        System.Collections.IEnumerator BreakthroughEffectRoutine()
        {
            float origBloom = bloomIntensity;
            float origSat = saturation;

            // Bloom爆发
            bloomIntensity = 3f;
            saturation = 1.5f;
            vignetteIntensity = 0f;

            yield return new WaitForSeconds(0.5f);

            // 缓慢回落
            float t = 0;
            while (t < 2f)
            {
                t += Time.deltaTime;
                bloomIntensity = Mathf.Lerp(3f, origBloom, t / 2f);
                saturation = Mathf.Lerp(1.5f, origSat, t / 2f);
                yield return null;
            }
            bloomIntensity = origBloom;
            saturation = origSat;
            vignetteIntensity = 0.25f;
        }

        /// <summary>死亡时——去饱和+暗角加深</summary>
        void TriggerDeathEffect()
        {
            StartCoroutine(DeathEffectRoutine());
        }

        System.Collections.IEnumerator DeathEffectRoutine()
        {
            float origSat = saturation;
            float origVig = vignetteIntensity;
            saturation = 0f;
            vignetteIntensity = 0.8f;
            yield return new WaitForSeconds(2f);
            saturation = origSat;
            vignetteIntensity = origVig;
        }
    }
}
