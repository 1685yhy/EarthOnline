using UnityEngine;
using System.Collections;

namespace StackingCute
{
    public class PerfectEffect : MonoBehaviour
    {
        [SerializeField] private Color _flashColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float _flashDuration = 0.3f;
        [SerializeField] private float _bounceScale = 1.2f;
        [SerializeField] private float _bounceDuration = 0.25f;
        private SpriteRenderer _sr; private Color _oc;

        private void Awake() { _sr = GetComponent<SpriteRenderer>(); if (_sr) _oc = _sr.color; }

        public void PlayPerfect(int combo)
        {
            StartCoroutine(Flash()); StartCoroutine(Bounce());
            if (combo >= 2) StartCoroutine(Text(combo));
        }

        private IEnumerator Flash() {
            if (!_sr) yield break; float e = 0f;
            while (e < _flashDuration) { e += Time.deltaTime; _sr.color = Color.Lerp(_oc, _flashColor, 1f - e / _flashDuration); yield return null; }
            _sr.color = _oc;
        }

        private IEnumerator Bounce() {
            var os = transform.localScale; float e = 0f;
            while (e < _bounceDuration) { e += Time.deltaTime; float t = e / _bounceDuration; transform.localScale = os * (1f + (_bounceScale - 1f) * Mathf.Exp(-t * 8f) * Mathf.Cos(t * 12f)); yield return null; }
            transform.localScale = os;
        }

        private IEnumerator Text(int c) {
            var go = new GameObject("CT"); go.transform.position = transform.position + Vector3.up * 0.8f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = c >= 5 ? "PERFECT x" + c + "!!" : "PERFECT x" + c + "!";
            tm.fontSize = 48; tm.color = c >= 5 ? new Color(1f, 0.3f, 0.3f) : Color.yellow;
            tm.anchor = TextAnchor.MiddleCenter; tm.fontStyle = FontStyle.Bold;
            go.transform.localScale = Vector3.one * 0.3f;
            float e = 0f; var sp = go.transform.position;
            while (e < 1f) { e += Time.deltaTime; go.transform.position = sp + Vector3.up * e * 1.5f; tm.color = new Color(tm.color.r, tm.color.g, tm.color.b, 1f - e); yield return null; }
            Destroy(go);
        }
    }
}