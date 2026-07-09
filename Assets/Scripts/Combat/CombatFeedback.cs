using UnityEngine;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 战斗反馈增强 —— 屏幕震动、击中标记、灵力不足提示。
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance { get; private set; }

        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector3 _originalCamPos;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (Camera.main != null) _originalCamPos = Camera.main.transform.localPosition;
        }

        void Update()
        {
            if (_shakeDuration > 0)
            {
                _shakeDuration -= Time.deltaTime;
                if (Camera.main != null)
                {
                    float x = Random.Range(-1f, 1f) * _shakeIntensity;
                    float y = Random.Range(-1f, 1f) * _shakeIntensity;
                    Camera.main.transform.localPosition = _originalCamPos + new Vector3(x, y, 0);
                }
            }
            else if (Camera.main != null)
            {
                Camera.main.transform.localPosition = _originalCamPos;
            }
        }

        /// <summary>屏幕震动——击中/被击中时调用</summary>
        public static void Shake(float intensity = 0.1f, float duration = 0.15f)
        {
            if (Instance == null) return;
            Instance._shakeIntensity = intensity;
            Instance._shakeDuration = duration;
        }

        /// <summary>灵力不足时红色闪烁提示</summary>
        public static void LowSpiritWarning()
        {
            Debug.Log("[Combat] ⚠️ 灵力不足！等待回复或使用基础攻击。");
        }

        /// <summary>命中VFX：冲击波圆环</summary>
        public static void SpawnHitVFX(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "HitVFX"; go.transform.position = position;
            go.transform.localScale = new Vector3(1.5f, 0.05f, 1.5f);
            var r = go.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.3f, 0.6f, 1f, 0.5f); r.material = m; }
            go.GetComponent<Collider>().isTrigger = true;
            Object.Destroy(go, 0.5f);
        }
    }
}
