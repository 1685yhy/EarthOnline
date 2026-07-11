using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EarthOnline.Framework;

namespace EarthOnline.UI
{
    /// <summary>
    /// M2 UI重写 —— 从Debug.Log升级到真正的Canvas UI。
    /// HUD: 血条+灵力条+境界+灵石+灵韵+小地图
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("HUD元素")]
        public GameObject hudRoot;
        public TextMeshProUGUI realmText;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI spiritText;
        public TextMeshProUGUI stonesText;
        public TextMeshProUGUI essenceText;
        public TextMeshProUGUI timeText;
        public TextMeshProUGUI eventText;
        public RawImage minimapImage;
        public GameObject dialoguePanel;
        public TextMeshProUGUI dialogueText;
        public GameObject choicePanel;
        public List<TextMeshProUGUI> choiceTexts;

        private float _eventDisplayTimer;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        void Start()
        {
            // 监听游戏事件，实时显示事件文字
            EventBus.Subscribe("OnRealmBreakthrough", data => {
                ShowEvent($"突破! {data["realm"]}");
            });
            EventBus.Subscribe("OnPlayerDeath", _ => {
                ShowEvent("你已死亡，3秒后重生...");
            });
            EventBus.Subscribe("OnDayPassed", data => {
                ShowEvent($"第{data["day"]}天");
            });
            EventBus.Subscribe("OnGiftAwakened", data => {
                ShowEvent($"金手指觉醒: {data["giftName"]}");
            });
            EventBus.Subscribe("OnGameStarted", data => {
                ShowEvent($"出身 {data["origin"]} | {data["realm"]}");
            });
        }

        void BuildUI()
        {
            // Canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            // --- HUD Bottom-Left ---
            var hudPanel = CreatePanel("HUD_Panel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(400, 120), new Vector2(20, 20));
            hudPanel.transform.SetParent(transform);
            var hudBg = hudPanel.AddComponent<Image>();
            hudBg.color = new Color(0, 0, 0, 0.5f);

            realmText = CreateTMPText("Realm", hudPanel.transform, "练气期 第1层", 22, TextAlignmentOptions.TopLeft, Color.white, new Vector2(10, -10));
            hpText = CreateTMPText("HP", hudPanel.transform, "❤️ 100/100", 18, TextAlignmentOptions.TopLeft, new Color(1f,0.3f,0.3f), new Vector2(10, -40));
            spiritText = CreateTMPText("Spirit", hudPanel.transform, "🔵 灵力:100", 14, TextAlignmentOptions.TopLeft, new Color(0.3f,0.6f,1f), new Vector2(10, -62));
            stonesText = CreateTMPText("Stones", hudPanel.transform, "💎 灵石:0", 14, TextAlignmentOptions.TopLeft, new Color(0.3f,0.8f,1f), new Vector2(200, -40));
            essenceText = CreateTMPText("Essence", hudPanel.transform, "✨ 灵韵:0", 14, TextAlignmentOptions.TopLeft, new Color(0.8f,0.8f,0.3f), new Vector2(200, -62));
            timeText = CreateTMPText("Time", hudPanel.transform, "🕐 08:00 第1天 ☀️", 14, TextAlignmentOptions.TopLeft, Color.white, new Vector2(10, -84));

            // --- Event Text Top-Center ---
            eventText = CreateTMPText("Event", transform, "", 16, TextAlignmentOptions.Top, new Color(1f,0.85f,0.2f), new Vector2(0, -80));
            eventText.rectTransform.anchorMin = eventText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            eventText.rectTransform.sizeDelta = new Vector2(800, 60);

            // --- Dialogue Panel Bottom-Center ---
            dialoguePanel = CreatePanel("Dialogue", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 120), new Vector2(0, 150));
            dialoguePanel.transform.SetParent(transform);
            dialoguePanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            dialogueText = CreateTMPText("DlgText", dialoguePanel.transform, "", 18, TextAlignmentOptions.TopLeft, Color.white, new Vector2(10, -10));
            dialogueText.rectTransform.sizeDelta = new Vector2(580, 100);
            dialoguePanel.SetActive(false);

            // --- Choice Panel Above Dialogue ---
            choicePanel = CreatePanel("Choices", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 80), new Vector2(0, 280));
            choicePanel.transform.SetParent(transform);
            choicePanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            choiceTexts = new List<TextMeshProUGUI>();
            for (int i = 0; i < 4; i++)
            {
                var ct = CreateTMPText($"Choice{i}", choicePanel.transform, "", 16, TextAlignmentOptions.Left, Color.white, new Vector2(10, -10 - i*20));
                choiceTexts.Add(ct);
            }
            choicePanel.SetActive(false);
        }

        GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return go;
        }

        TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions align, Color color, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize;
            tmp.alignment = align; tmp.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(380, 30);
            rt.anchoredPosition = pos;
            return tmp;
        }

        void Update()
        {
            if (TimeManager.Instance == null) return;

            // Update HUD
            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                realmText.text = $"{(CultivationManager.Instance?.FullTitle ?? "凡人")}";
                hpText.text = $"❤️ {stats.currentHP}/{stats.maxHP}";
                spiritText.text = $"🔵 灵力:{EarthOnline.Combat.CombatSystem.Instance?.SpiritEnergy ?? 0:F0}";
                stonesText.text = $"💎 灵石:{stats.spiritStones}";
                essenceText.text = $"✨ 灵韵:{stats.spiritEssence}";
                timeText.text = $"🕐 {TimeManager.Instance.TimeString} 第{TimeManager.Instance.GameDay}天 {WeatherSystem.Instance?.GetWeatherEmoji() ?? "☀️"}";
            }

            // Event text fade
            if (_eventDisplayTimer > 0)
            {
                _eventDisplayTimer -= Time.deltaTime;
                if (_eventDisplayTimer <= 0) eventText.text = "";
            }
        }

        public void ShowEvent(string msg)
        {
            eventText.text = msg;
            _eventDisplayTimer = 5f;
        }

        public void ShowDialogue(string speaker, string text, string[] choices = null)
        {
            dialoguePanel.SetActive(true);
            dialogueText.text = $"<b>{speaker}</b>\n{text}";

            if (choices != null && choices.Length > 0)
            {
                choicePanel.SetActive(true);
                for (int i = 0; i < choiceTexts.Count; i++)
                {
                    if (i < choices.Length)
                    {
                        choiceTexts[i].text = $"[{i+1}] {choices[i]}";
                        choiceTexts[i].gameObject.SetActive(true);
                    }
                    else
                        choiceTexts[i].gameObject.SetActive(false);
                }
            }
            else
                choicePanel.SetActive(false);
        }

        public void HideDialogue()
        {
            dialoguePanel.SetActive(false);
            choicePanel.SetActive(false);
        }
    }
}
