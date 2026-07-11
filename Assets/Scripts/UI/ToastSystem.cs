using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EarthOnline.Framework;

namespace EarthOnline.UI
{
    /// <summary>
    /// P0 Toast通知系统 —— 替代Debug.Log的游戏内反馈。
    /// 战斗红/修炼蓝/灵石金/事件绿。右上角滑入，2秒消失。
    /// </summary>
    public class ToastSystem : MonoBehaviour
    {
        public static ToastSystem Instance { get; private set; }

        public enum ToastType { Combat, Cultivation, Currency, Event }

        [System.Serializable]
        public class ToastConfig
        {
            public ToastType type;
            public Color mainColor;
            public Color bgColor;
            public string icon;
        }

        private List<ToastConfig> _configs = new()
        {
            new() { type=ToastType.Combat, mainColor=new Color(0.94f,0.27f,0.27f), bgColor=new Color(0.13f,0.05f,0.05f), icon="⚔️" },
            new() { type=ToastType.Cultivation, mainColor=new Color(0.23f,0.51f,0.96f), bgColor=new Color(0.05f,0.08f,0.2f), icon="🧘" },
            new() { type=ToastType.Currency, mainColor=new Color(0.96f,0.62f,0.04f), bgColor=new Color(0.2f,0.13f,0.02f), icon="💎" },
            new() { type=ToastType.Event, mainColor=new Color(0.13f,0.77f,0.37f), bgColor=new Color(0.05f,0.18f,0.08f), icon="📋" },
        };

        private Queue<(ToastType type, string msg)> _queue = new();
        private bool _showing;
        private Canvas _toastCanvas;
        private GameObject _toastTemplate;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            CreateToastCanvas();
        }

        void CreateToastCanvas()
        {
            _toastCanvas = gameObject.AddComponent<Canvas>();
            _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _toastCanvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
        }

        void Start()
        {
            // 监听所有需要Toast的事件
            EventBus.Subscribe("OnItemAdded", d => Show(ToastType.Currency, $"获得 {d["itemName"]} x{d["quantity"]}"));
            EventBus.Subscribe("OnEnemyKilled", d => Show(ToastType.Combat, $"击杀 {d["enemyName"]}"));
            EventBus.Subscribe("OnPlayerAttack", d => Show(ToastType.Combat, $"{(d.ContainsKey("crit")&&(bool)d["crit"]?"暴击!":"")} -{d["damage"]}HP"));
            EventBus.Subscribe("OnCultivationBoost", d => Show(ToastType.Cultivation, $"+{d["amount"]}修为"));
            EventBus.Subscribe("OnQuestCompleted", d => Show(ToastType.Event, $"任务完成: {d["title"]}"));
            EventBus.Subscribe("OnSignInComplete", d => Show(ToastType.Currency, $"签到+{d["reward"]}灵石"));
            EventBus.Subscribe("OnRealmBreakthrough", d => Show(ToastType.Cultivation, $"突破! {d["realm"]}"));
            EventBus.Subscribe("OnAchievementUnlocked", d => Show(ToastType.Event, $"🏆 成就: {d["title"]}"));
        }

        public void Show(ToastType type, string message)
        {
            _queue.Enqueue((type, message));
            if (!_showing) StartCoroutine(ProcessQueue());
        }

        IEnumerator ProcessQueue()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                var (type, msg) = _queue.Dequeue();
                var config = _configs.Find(c => c.type == type);
                if (config == null) config = _configs[0];

                var toast = CreateToastElement(config, msg);
                // 入场动画
                yield return AnimateIn(toast);
                // 驻留
                yield return new WaitForSeconds(2f);
                // 消失
                yield return AnimateOut(toast);
                Destroy(toast);
            }
            _showing = false;
        }

        GameObject CreateToastElement(ToastConfig config, string message)
        {
            var go = new GameObject("Toast");
            go.transform.SetParent(transform);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(360, 56);
            rt.anchoredPosition = new Vector2(-20, -20);

            // 背景
            var bg = go.AddComponent<Image>();
            bg.color = config.bgColor;

            // 左边色条
            var leftBar = new GameObject("LeftBar"); leftBar.transform.SetParent(go.transform);
            var lb = leftBar.AddComponent<Image>(); lb.color = config.mainColor;
            var lr = leftBar.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1);
            lr.sizeDelta = new Vector2(3, 0); lr.anchoredPosition = Vector2.zero;

            // 图标+文字
            var textGo = new GameObject("Text"); textGo.transform.SetParent(go.transform);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{config.icon} {message}";
            tmp.fontSize = 14; tmp.color = Color.white;
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0, 0);
            tr.sizeDelta = new Vector2(340, 46); tr.anchoredPosition = new Vector2(10, 0);

            return go;
        }

        IEnumerator AnimateIn(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(400, rt.anchoredPosition.y); // start off-screen
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = Vector2.Lerp(new Vector2(400, rt.anchoredPosition.y), new Vector2(-20, rt.anchoredPosition.y), t / 0.3f);
                yield return null;
            }
        }

        IEnumerator AnimateOut(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            var cg = go.AddComponent<CanvasGroup>();
            float t = 0;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - t / 0.25f;
                rt.anchoredPosition += Vector2.right * 2f;
                yield return null;
            }
        }
    }
}
