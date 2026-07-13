using UnityEngine;
using System.Collections;

namespace StackingCute
{
    public class DebrisEffect : MonoBehaviour
    {
        [SerializeField] private int _debrisCount = 10;
        [SerializeField] private float _fadeDuration = 0.6f;
        [SerializeField] private float _flySpeed = 3f;
        [SerializeField] private Color _debrisColor = new Color(1f, 0.75f, 0.8f, 1f);
        [SerializeField] private float _debrisSize = 0.08f;

        public void SpawnDebris(Vector3 pos, float w, float bw)
        {
            for (int i = 0; i < Mathf.Clamp(_debrisCount, 6, 20); i++)
                StartCoroutine(Fly(pos, w));
        }

        private IEnumerator Fly(Vector3 o, float ew)
        {
            var d = new GameObject("D" + Random.Range(0, 9999));
            d.transform.position = o + new Vector3(Random.Range(-ew * 0.5f, ew * 0.5f), Random.Range(-0.05f, 0.05f), 0);
            var sr = d.AddComponent<SpriteRenderer>();
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var p = new Color[16]; for (int i = 0; i < 16; i++) p[i] = _debrisColor;
            t.SetPixels(p); t.Apply(); t.filterMode = FilterMode.Point;
            sr.sprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sr.sortingOrder = 100;
            float s = Random.Range(_debrisSize * 0.5f, _debrisSize * 1.5f);
            d.transform.localScale = Vector3.one * s;
            var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1f), 0).normalized * _flySpeed;
            float e = 0f;
            while (e < _fadeDuration)
            {
                e += Time.deltaTime; float tt = e / _fadeDuration;
                d.transform.position += dir * Time.deltaTime; dir.y -= 2f * Time.deltaTime;
                sr.color = new Color(_debrisColor.r, _debrisColor.g, _debrisColor.b, 1f - tt);
                d.transform.localScale = Vector3.one * s * (1f - tt * 0.5f);
                yield return null;
            }
            Destroy(d);
        }
    }
}