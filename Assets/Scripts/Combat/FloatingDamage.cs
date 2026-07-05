using UnityEngine;
using System.Collections;
using EarthOnline.NPC;

namespace EarthOnline.Combat
{
    /// <summary>
    /// 浮动伤害/治疗数字 —— 世界空间弹出后上升淡出。
    /// </summary>
    public class FloatingDamage : MonoBehaviour
    {
        public static void Spawn(Vector3 worldPos, string text, Color color, float duration = 1.5f)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = worldPos + Vector3.up * 2f + Random.insideUnitSphere * 0.3f;

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 36;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.color = color;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.15f;

            go.AddComponent<Billboard>();
            go.AddComponent<FloatingAnim>().Init(duration);
        }

        private class FloatingAnim : MonoBehaviour
        {
            private float _duration;
            private float _elapsed;
            private TextMesh _text;

            public void Init(float duration)
            {
                _duration = duration;
                _text = GetComponent<TextMesh>();
                StartCoroutine(Animate());
            }

            IEnumerator Animate()
            {
                Vector3 start = transform.position;
                float elapsed = 0;
                while (elapsed < _duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / _duration;
                    transform.position = start + Vector3.up * (t * 2f);
                    if (_text != null)
                        _text.color = new Color(_text.color.r, _text.color.g, _text.color.b, 1f - t);
                    yield return null;
                }
                Destroy(gameObject);
            }
        }
    }
}
