using UnityEngine;
using UnityEngine.UI;

namespace EarthOnline.UI
{
    /// <summary>
    /// 简易小地图 —— 俯视摄像机+RenderTexture显示在右下角。
    /// </summary>
    public class MiniMap : MonoBehaviour
    {
        public float mapSize = 180f;
        public float worldViewSize = 40f;
        public LayerMask cullingMask = -1;

        private Camera _mapCam;
        private RenderTexture _rt;

        void Start()
        {
            CreateMiniMap();
        }

        void CreateMiniMap()
        {
            // Create render texture
            _rt = new RenderTexture(256, 256, 16);
            _rt.name = "MiniMapRT";

            // Create minimap camera
            var camGo = new GameObject("MiniMapCamera");
            camGo.transform.SetParent(transform);
            _mapCam = camGo.AddComponent<Camera>();
            _mapCam.orthographic = true;
            _mapCam.orthographicSize = worldViewSize / 2f;
            _mapCam.cullingMask = cullingMask;
            _mapCam.clearFlags = CameraClearFlags.SolidColor;
            _mapCam.backgroundColor = new Color(0.1f, 0.15f, 0.1f, 0.8f);
            _mapCam.targetTexture = _rt;
            _mapCam.depth = 10; // render after main camera

            // Point down
            camGo.transform.rotation = Quaternion.Euler(90, 0, 0);

            // Create UI image
            var canvas = GetComponent<Canvas>();
            if (canvas == null) { canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; }

            var mapImg = new GameObject("MapImage");
            mapImg.transform.SetParent(transform);
            var rawImg = mapImg.AddComponent<RawImage>();
            rawImg.texture = _rt;
            var rect = mapImg.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0); // bottom-right
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(mapSize, mapSize);
            rect.anchoredPosition = new Vector2(-20, 20);

            // Border
            var border = new GameObject("MapBorder"); border.transform.SetParent(mapImg.transform);
            var bImg = border.AddComponent<Image>(); bImg.color = new Color(0, 0, 0, 0);
            var outline = border.AddComponent<Outline>(); outline.effectColor = new Color(1, 1, 1, 0.5f); outline.effectDistance = new Vector2(2, 2);
            var bRect = border.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one; bRect.sizeDelta = Vector2.zero;

            Debug.Log("[MiniMap] 小地图已创建 (右下角)");
        }

        void LateUpdate()
        {
            if (_mapCam == null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var pos = player.transform.position;
                _mapCam.transform.position = new Vector3(pos.x, pos.y + worldViewSize / 2f, pos.z);
            }
        }

        void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
