using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarthOnline.Framework;

namespace EarthOnline.UI
{
    /// <summary>
    /// Tribulation (渡劫) UI using string-based EventBus.
    ///
    /// Panels:
    ///   1. Confirmation Panel — shows readiness scores, confirm button
    ///   2. Thunder HUD       — strike counter, warning indicator
    ///   3. Heart Demon Panel — willpower bar, demon info, resolution buttons
    ///   4. Dao Body Panel    — formation result display
    ///
    /// String Events Subscribed:
    ///   TribulationConfirmation, TribulationStarted,
    ///   ThunderStrikeWarning, ThunderStrikeStruck, ThunderTribulationCompleted,
    ///   HeartDemonStageStarted, HeartDemonSpawned, HeartDemonWillPowerChanged,
    ///   HeartDemonResolved, HeartDemonAllCleared, HeartDemonFailed,
    ///   DaoBodyFormed, TribulationCompleted
    /// </summary>
    public class TribulationUI : MonoBehaviour
    {
        // ── Stored handler references (for clean unsubscribe) ─────────────
        private readonly Dictionary<string, System.Action<Dictionary<string, object>>> _handlers = new();

        // ── Root Canvas ──────────────────────────────────────────────────
        private GameObject _canvas;

        // ── Confirmation Panel ───────────────────────────────────────────
        private GameObject _confirmPanel;
        private Text _confirmTitleText;
        private Text _readinessPillText;
        private Text _readinessEquipText;
        private Text _readinessFormText;
        private Text _readinessEscortText;
        private Text _readinessTotalText;
        private Text _successRateText;
        private Button _confirmBtn;
        private Button _cancelBtn;

        // ── Thunder HUD ──────────────────────────────────────────────────
        private GameObject _thunderHUD;
        private Text _strikeCounterText;
        private Text _strikeWarningText;
        private Image _warningFlashImage;

        // ── Heart Demon Panel ────────────────────────────────────────────
        private GameObject _heartPanel;
        private Image _willpowerBar;
        private Text _willpowerText;
        private Text _demonInfoText;
        private Text _demonDescriptionText;
        private Text _resolutionHintText;
        private GameObject _resolutionButtons;
        private Button[] _resolveBtns;

        // ── Dao Body Result Panel ────────────────────────────────────────
        private GameObject _resultPanel;
        private Text _resultTitleText;
        private Text _resultBodyText;
        private Text _resultDetailText;
        private Button _resultCloseBtn;

        // ── Barrier HUD ──────────────────────────────────────────────────
        private GameObject _barrierHUD;
        private Image _barrierHealthBar;
        private Text _barrierHealthText;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildCanvas();
            BuildConfirmationPanel();
            BuildThunderHUD();
            BuildHeartDemonPanel();
            BuildResultPanel();
            BuildBarrierHUD();
        }

        private void OnEnable()
        {
            BindEvent("TribulationConfirmation",     OnTribulationConfirmation);
            BindEvent("TribulationStarted",          OnTribulationStarted);
            BindEvent("ThunderStrikeWarning",        OnThunderStrikeWarning);
            BindEvent("ThunderStrikeStruck",         OnThunderStrikeStruck);
            BindEvent("ThunderTribulationCompleted", OnThunderTribulationCompleted);
            BindEvent("HeartDemonStageStarted",      OnHeartDemonStageStarted);
            BindEvent("HeartDemonSpawned",           OnHeartDemonSpawned);
            BindEvent("HeartDemonWillPowerChanged",  OnHeartDemonWillPowerChanged);
            BindEvent("HeartDemonResolved",          OnHeartDemonResolved);
            BindEvent("HeartDemonAllCleared",        OnHeartDemonAllCleared);
            BindEvent("HeartDemonFailed",            OnHeartDemonFailed);
            BindEvent("DaoBodyFormed",               OnDaoBodyFormed);
            BindEvent("TribulationCompleted",        OnTribulationCompleted);
        }

        private void OnDisable()
        {
            foreach (var kvp in _handlers)
                EventBus.Unsubscribe(kvp.Key, kvp.Value);
            _handlers.Clear();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Binding Helper
        // ══════════════════════════════════════════════════════════════════

        private void BindEvent(string eventName, System.Action<Dictionary<string, object>> handler)
        {
            _handlers[eventName] = handler;
            EventBus.Subscribe(eventName, handler);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Confirmation Panel (TribulationConfirmation)
        // ══════════════════════════════════════════════════════════════════

        private void OnTribulationConfirmation(Dictionary<string, object> data)
        {
            HideAllPanels();

            string quality     = GetStr(data, "quality", "Normal");
            float pillScore    = GetFloat(data, "pillScore", 0f);
            float equipScore   = GetFloat(data, "equipScore", 0f);
            float formScore    = GetFloat(data, "formScore", 0f);
            float escortScore  = GetFloat(data, "escortScore", 0f);
            float totalScore   = GetFloat(data, "totalScore", 0f);
            float successRate  = GetFloat(data, "estimatedSuccessRate", 0f);

            _confirmTitleText.text = $"天劫确认 - {GetQualityName(quality)}";
            _readinessPillText.text  = $"丹药 {pillScore * 100f:F0}%";
            _readinessEquipText.text = $"装备 {equipScore * 100f:F0}%";
            _readinessFormText.text  = $"阵法 {formScore * 100f:F0}%";
            _readinessEscortText.text= $"护法 {escortScore * 100f:F0}%";
            _readinessTotalText.text = $"总准备度 {totalScore * 100f:F1}%";
            _successRateText.text    = $"预计成功率 {successRate * 100f:F1}%";

            _confirmPanel.SetActive(true);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Thunder HUD (TribulationStarted / ThunderStrikeWarning / etc.)
        // ══════════════════════════════════════════════════════════════════

        private void OnTribulationStarted(Dictionary<string, object> data)
        {
            HideAllPanels();

            string quality = GetStr(data, "quality", "Normal");
            float readiness = GetFloat(data, "readinessScore", 0f);
            float successRate = GetFloat(data, "estimatedSuccessRate", 0f);

            _strikeCounterText.text = "雷劫 准备中...";
            _strikeWarningText.text = "";
            _thunderHUD.SetActive(true);
        }

        private void OnThunderStrikeWarning(Dictionary<string, object> data)
        {
            int strikeIndex    = GetInt(data, "strikeIndex", 0);
            int totalStrikes   = GetInt(data, "totalStrikes", 9);
            float baseDamage   = GetFloat(data, "baseDamage", 30f);
            float timeUntil    = GetFloat(data, "timeUntilStrike", 1f);

            _strikeCounterText.text = $"天雷 {strikeIndex}/{totalStrikes}";
            _strikeWarningText.text = $"⚡ 警告！ 伤害: {baseDamage:F0} | 倒计时: {timeUntil:F1}s";
            _strikeWarningText.color = Color.red;

            // Pulse flash
            if (_warningFlashImage != null)
            {
                _warningFlashImage.gameObject.SetActive(true);
                var c = _warningFlashImage.color;
                c.a = 0.4f;
                _warningFlashImage.color = c;
            }
        }

        private void OnThunderStrikeStruck(Dictionary<string, object> data)
        {
            int strikeIndex = GetInt(data, "strikeIndex", 0);
            float damage    = GetFloat(data, "damage", 0f);
            bool hit        = GetBool(data, "playerHit", false);
            float dist      = GetFloat(data, "distanceFromPlayer", 999f);
            float splash    = GetFloat(data, "splashDamage", 0f);

            if (hit)
            {
                _strikeWarningText.text = $"✖ 击中！ 伤害: {damage:F1} (距离 {dist:F1}m)";
                _strikeWarningText.color = new Color(1f, 0.5f, 0f);
            }
            else
            {
                _strikeWarningText.text = $"✔ 完美闪避！";
                _strikeWarningText.color = Color.green;
            }

            // Flash effect decay
            if (_warningFlashImage != null)
            {
                var c = _warningFlashImage.color;
                c.a = 0.1f;
                _warningFlashImage.color = c;
            }
        }

        private void OnThunderTribulationCompleted(Dictionary<string, object> data)
        {
            int perfectDodges = GetInt(data, "perfectDodges", 0);
            int totalStrikes  = GetInt(data, "totalStrikes", 9);
            float diffMod     = GetFloat(data, "difficultyModifier", 0f);
            int daoBodyBonus  = GetInt(data, "daoBodyBonus", 0);

            _strikeCounterText.text = $"雷劫 完成！";
            _strikeWarningText.text = $"完美闪避: {perfectDodges}/{totalStrikes} | 心魔修正: {diffMod * 100f:F0}% | 道体 +{daoBodyBonus}";
            _strikeWarningText.color = new Color(0.3f, 0.8f, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Heart Demon Panel
        // ══════════════════════════════════════════════════════════════════

        private void OnHeartDemonStageStarted(Dictionary<string, object> data)
        {
            HideAllPanels();

            int demonCount      = GetInt(data, "demonCount", 3);
            float initialWill   = GetFloat(data, "initialWillpower", 100f);
            float diffMod       = GetFloat(data, "difficultyModifier", 0f);

            _willpowerBar.fillAmount = 1f;
            _willpowerText.text = $"道心 {initialWill:F0}/{initialWill:F0}";
            _demonInfoText.text = $"心魔劫开始 — {demonCount}个心魔";
            _demonDescriptionText.text = "";
            _resolutionHintText.text = "";
            _resolutionButtons.SetActive(false);

            _heartPanel.SetActive(true);
        }

        private void OnHeartDemonSpawned(Dictionary<string, object> data)
        {
            int demonIndex    = GetInt(data, "demonIndex", 0);
            int totalDemons   = GetInt(data, "totalDemons", 3);
            string demonType  = GetStr(data, "demonType", "?");
            string desc       = GetStr(data, "description", "");
            string hint       = GetStr(data, "resolutionHint", "");

            _demonInfoText.text = $"心魔 #{demonIndex}/{totalDemons} — {demonType}";
            _demonDescriptionText.text = desc;
            _resolutionHintText.text = $"💡 {hint}";
            _resolutionButtons.SetActive(true);

            // Update resolution button labels
            if (_resolveBtns != null && _resolveBtns.Length >= 4)
            {
                SetBtnLabel(_resolveBtns[0], "直面 (40%)");
                SetBtnLabel(_resolveBtns[1], "反思 (50%)");
                SetBtnLabel(_resolveBtns[2], "接纳 (60%)");
                SetBtnLabel(_resolveBtns[3], "压制 (30%)");
            }
        }

        private void OnHeartDemonWillPowerChanged(Dictionary<string, object> data)
        {
            float current = GetFloat(data, "currentWillpower", 100f);
            float max     = GetFloat(data, "maxWillpower", 100f);
            string reason = GetStr(data, "reason", "");

            float pct = max > 0f ? current / max : 0f;
            _willpowerBar.fillAmount = pct;

            // Color: green > yellow > red
            _willpowerBar.color = pct > 0.5f
                ? new Color(0.3f, 1f, 0.3f)
                : pct > 0.25f
                    ? new Color(1f, 0.8f, 0.2f)
                    : new Color(1f, 0.2f, 0.2f);

            string reasonLabel = reason switch
            {
                "time_drain" => "时间流逝",
                "resolve_failed" => "心魔反噬",
                _ => reason
            };
            _willpowerText.text = $"道心 {current:F0}/{max:F0} [{reasonLabel}]";
        }

        private void OnHeartDemonResolved(Dictionary<string, object> data)
        {
            bool success = GetBool(data, "success", false);
            string method = GetStr(data, "resolutionMethod", "?");
            float wpCost = GetFloat(data, "willpowerCost", 0f);

            if (success)
            {
                _demonDescriptionText.text = $"✔ 化解成功！({method})";
                _demonDescriptionText.color = Color.green;
            }
            else
            {
                _demonDescriptionText.text = $"✖ 化解失败！ 道心 -{wpCost:F0} ({method})";
                _demonDescriptionText.color = Color.red;
            }

            _resolutionButtons.SetActive(false);
        }

        private void OnHeartDemonAllCleared(Dictionary<string, object> data)
        {
            int resolved = GetInt(data, "resolvedCount", 0);
            int total    = GetInt(data, "totalDemons", 0);
            float remain = GetFloat(data, "remainingWillpower", 0f);

            _demonInfoText.text = $"所有心魔已清除！";
            _demonDescriptionText.text = $"化解: {resolved}/{total} | 剩余道心: {remain:F0}";
            _demonDescriptionText.color = new Color(0.3f, 1f, 0.8f);
            _resolutionButtons.SetActive(false);
        }

        private void OnHeartDemonFailed(Dictionary<string, object> data)
        {
            int remaining = GetInt(data, "demonsRemaining", 0);
            string lastType = GetStr(data, "lastDemonType", "?");

            _demonInfoText.text = "心魔劫 失败";
            _demonDescriptionText.text = $"道心崩坏！剩余 {remaining} 个心魔未化解。最后心魔: {lastType}";
            _demonDescriptionText.color = Color.red;
            _resolutionButtons.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Dao Body Result Panel
        // ══════════════════════════════════════════════════════════════════

        private void OnDaoBodyFormed(Dictionary<string, object> data)
        {
            HideAllPanels();

            bool success        = GetBool(data, "success", false);
            string bodyTypeName = GetStr(data, "bodyTypeName", "?");
            string qualityName  = GetStr(data, "qualityName", "?");
            int quality         = GetInt(data, "quality", 0);
            int failureCount    = GetInt(data, "failureCount", 0);

            if (success)
            {
                _resultTitleText.text = "道体凝聚成功！";
                _resultTitleText.color = new Color(1f, 0.84f, 0f); // Golden
                _resultBodyText.text = $"{qualityName} · {bodyTypeName}";
                _resultBodyText.color = quality >= 4
                    ? new Color(1f, 0.5f, 0f) // Orange for Saint+ level
                    : Color.white;
                _resultDetailText.text = $"品质等级: {quality}/5";
            }
            else
            {
                _resultTitleText.text = "道体凝聚失败";
                _resultTitleText.color = Color.red;
                _resultBodyText.text = $"{qualityName} · {bodyTypeName} (未成功)";
                _resultBodyText.color = Color.gray;
                _resultDetailText.text = $"失败次数: {failureCount}/4 (第4次必定成功)";
            }

            _resultPanel.SetActive(true);
        }

        private void OnTribulationCompleted(Dictionary<string, object> data)
        {
            bool success   = GetBool(data, "success", false);
            string quality = GetStr(data, "quality", "Normal");
            float readiness = GetFloat(data, "readinessScore", 0f);

            if (!_resultPanel.activeSelf)
            {
                // If DaoBodyFormed hasn't shown, show generic result
                HideAllPanels();
                _resultTitleText.text = success ? "渡劫成功" : "渡劫失败";
                _resultTitleText.color = success ? new Color(1f, 0.84f, 0f) : Color.red;
                _resultBodyText.text = $"{GetQualityName(quality)}平台 | 准备度 {readiness * 100f:F1}%";
                _resultDetailText.text = success
                    ? "天道认可，修为大进！"
                    : "天道无情，来日再战！";
                _resultPanel.SetActive(true);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Barrier HUD (optional sub-panel)
        // ══════════════════════════════════════════════════════════════════

        private void OnBarrierDamaged(Dictionary<string, object> data)
        {
            float remaining = GetFloat(data, "remainingDurability", 0f);
            float maxDura   = GetFloat(data, "maxDurability", 100f);

            _barrierHealthBar.fillAmount = maxDura > 0f ? remaining / maxDura : 0f;
            _barrierHealthText.text = $"护罩 {remaining:F0}/{maxDura:F0}";
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — publish resolution choice
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by the resolution buttons. Publishes "ResolveHeartDemon"
        /// with the chosen method so gameplay code can process it.
        /// </summary>
        public void OnResolutionClicked(int methodIndex)
        {
            string[] methods = { "confront", "reflect", "accept", "suppress" };
            if (methodIndex < 0 || methodIndex >= methods.Length) return;

            EventBus.Publish("ResolveHeartDemon", new Dictionary<string, object>
            {
                { "method", methods[methodIndex] }
            });
        }

        /// <summary>
        /// Called by the confirm button. Publishes "ConfirmTribulation"
        /// so TribulationManager can start the tribulation.
        /// </summary>
        public void OnConfirmClicked()
        {
            EventBus.Publish("ConfirmTribulation", new Dictionary<string, object>());
        }

        /// <summary>
        /// Called by the cancel button. Hides confirmation panel.
        /// </summary>
        public void OnCancelClicked()
        {
            _confirmPanel.SetActive(false);
        }

        /// <summary>
        /// Called by the close button on the result panel.
        /// </summary>
        public void OnResultClosed()
        {
            _resultPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private void HideAllPanels()
        {
            _confirmPanel?.SetActive(false);
            _thunderHUD?.SetActive(false);
            _heartPanel?.SetActive(false);
            _resultPanel?.SetActive(false);
            _barrierHUD?.SetActive(false);
        }

        private static string GetStr(Dictionary<string, object> d, string key, string fallback)
        {
            return d != null && d.ContainsKey(key) ? d[key]?.ToString() ?? fallback : fallback;
        }

        private static float GetFloat(Dictionary<string, object> d, string key, float fallback)
        {
            if (d != null && d.ContainsKey(key))
            {
                var v = d[key];
                if (v is float f) return f;
                if (v is int i) return i;
                if (float.TryParse(v?.ToString(), out float r)) return r;
            }
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int fallback)
        {
            if (d != null && d.ContainsKey(key))
            {
                var v = d[key];
                if (v is int i) return i;
                if (v is float f) return (int)f;
                if (int.TryParse(v?.ToString(), out int r)) return r;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
        {
            if (d != null && d.ContainsKey(key))
            {
                var v = d[key];
                if (v is bool b) return b;
                if (v is int i) return i != 0;
                if (bool.TryParse(v?.ToString(), out bool r)) return r;
            }
            return fallback;
        }

        private static string GetQualityName(string quality)
        {
            return quality switch
            {
                "Normal"  => "凡品",
                "Ancient" => "古品",
                "Secret"  => "秘品",
                _ => quality
            };
        }

        private static void SetBtnLabel(Button btn, string label)
        {
            var text = btn.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Canvas / Panel Construction (programmatic UI)
        // ══════════════════════════════════════════════════════════════════

        private void BuildCanvas()
        {
            _canvas = new GameObject("TribulationUICanvas");
            _canvas.transform.SetParent(transform);

            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // Above main HUD (10)

            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvas.AddComponent<GraphicRaycaster>();
        }

        // ── Confirmation Panel ───────────────────────────────────────────

        private void BuildConfirmationPanel()
        {
            _confirmPanel = CreatePanel("ConfirmPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(400, 320), Vector2.zero);
            _confirmPanel.transform.SetParent(_canvas.transform);

            var bg = _confirmPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            _confirmTitleText = MakeLabel("Title", _confirmPanel.transform,
                "天劫确认", 22, Color.white, new Vector2(0, -20), new Vector2(360, 30));

            _readinessPillText = MakeLabel("Pill", _confirmPanel.transform,
                "丹药 0%", 16, new Color(0.6f, 0.9f, 0.6f), new Vector2(0, -60), new Vector2(360, 24));
            _readinessEquipText = MakeLabel("Equip", _confirmPanel.transform,
                "装备 0%", 16, new Color(0.6f, 0.8f, 1f), new Vector2(0, -88), new Vector2(360, 24));
            _readinessFormText = MakeLabel("Form", _confirmPanel.transform,
                "阵法 0%", 16, new Color(0.9f, 0.7f, 0.5f), new Vector2(0, -116), new Vector2(360, 24));
            _readinessEscortText = MakeLabel("Escort", _confirmPanel.transform,
                "护法 0%", 16, new Color(0.8f, 0.6f, 0.9f), new Vector2(0, -144), new Vector2(360, 24));
            _readinessTotalText = MakeLabel("Total", _confirmPanel.transform,
                "总准备度 0%", 18, Color.white, new Vector2(0, -176), new Vector2(360, 26));
            _successRateText = MakeLabel("Rate", _confirmPanel.transform,
                "预计成功率 0%", 18, new Color(1f, 0.84f, 0f), new Vector2(0, -208), new Vector2(360, 26));

            // Buttons
            _confirmBtn = MakeButton("ConfirmBtn", _confirmPanel.transform,
                "开始渡劫", new Vector2(-80, -260), new Vector2(140, 36));
            _confirmBtn.onClick.AddListener(OnConfirmClicked);

            _cancelBtn = MakeButton("CancelBtn", _confirmPanel.transform,
                "取消", new Vector2(80, -260), new Vector2(140, 36));
            _cancelBtn.onClick.AddListener(OnCancelClicked);

            _confirmPanel.SetActive(false);
        }

        // ── Thunder HUD ──────────────────────────────────────────────────

        private void BuildThunderHUD()
        {
            _thunderHUD = CreatePanel("ThunderHUD",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(400, 120), new Vector2(0, -60));
            _thunderHUD.transform.SetParent(_canvas.transform);

            // Semi-transparent background
            var bg = _thunderHUD.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.55f);

            _strikeCounterText = MakeLabel("StrikeCounter", _thunderHUD.transform,
                "雷劫 准备中", 24, new Color(1f, 0.84f, 0f), new Vector2(0, -16), new Vector2(380, 30));

            _strikeWarningText = MakeLabel("Warning", _thunderHUD.transform,
                "", 18, Color.red, new Vector2(0, -50), new Vector2(380, 26));

            // Warning flash overlay
            var flashObj = CreatePanel("WarningFlash",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1920, 1080), Vector2.zero);
            flashObj.transform.SetParent(_canvas.transform);
            _warningFlashImage = flashObj.AddComponent<Image>();
            _warningFlashImage.color = new Color(1f, 0.4f, 0f, 0f);
            _warningFlashImage.raycastTarget = false;
            flashObj.SetActive(false);

            _thunderHUD.SetActive(false);
        }

        // ── Heart Demon Panel ────────────────────────────────────────────

        private void BuildHeartDemonPanel()
        {
            _heartPanel = CreatePanel("HeartDemonPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(500, 400), Vector2.zero);
            _heartPanel.transform.SetParent(_canvas.transform);

            var bg = _heartPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.12f, 0.92f);

            // Title
            MakeLabel("HDTitle", _heartPanel.transform,
                "心魔劫", 22, new Color(1f, 0.84f, 0f), new Vector2(0, -20), new Vector2(460, 30));

            // Willpower bar
            var barBg = CreatePanel("WillpowerBarBg",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(400, 24), new Vector2(0, -60));
            barBg.transform.SetParent(_heartPanel.transform);
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.2f, 0.2f, 0.2f);

            var barFill = CreatePanel("WillpowerFill",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(400, 24), Vector2.zero);
            barFill.transform.SetParent(barBg.transform);
            _willpowerBar = barFill.AddComponent<Image>();
            _willpowerBar.type = Image.Type.Filled;
            _willpowerBar.fillMethod = Image.FillMethod.Horizontal;
            _willpowerBar.fillAmount = 1f;
            _willpowerBar.color = new Color(0.3f, 1f, 0.3f);

            _willpowerText = MakeLabel("WillpowerText", _heartPanel.transform,
                "道心 100/100", 16, Color.white, new Vector2(0, -92), new Vector2(460, 22));

            // Demon info
            _demonInfoText = MakeLabel("DemonInfo", _heartPanel.transform,
                "心魔即将出现...", 18, Color.white, new Vector2(0, -130), new Vector2(460, 26));

            _demonDescriptionText = MakeLabel("DemonDesc", _heartPanel.transform,
                "", 15, new Color(0.8f, 0.8f, 0.8f), new Vector2(0, -170), new Vector2(460, 60));

            _resolutionHintText = MakeLabel("ResolveHint", _heartPanel.transform,
                "", 14, new Color(0.6f, 0.9f, 0.6f), new Vector2(0, -220), new Vector2(460, 22));

            // Resolution buttons
            _resolutionButtons = CreatePanel("ResolveBtns",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(460, 120), new Vector2(0, -310));
            _resolutionButtons.transform.SetParent(_heartPanel.transform);

            _resolveBtns = new Button[4];
            string[] labels = { "直面", "反思", "接纳", "压制" };
            int[] colors = { 0, 1, 2, 3 }; // index
            for (int i = 0; i < 4; i++)
            {
                int xPos = -180 + i * 120;
                int ci = i; // capture for closure
                _resolveBtns[i] = MakeButton($"Resolve{i}", _resolutionButtons.transform,
                    labels[i], new Vector2(xPos, 0), new Vector2(100, 30));
                _resolveBtns[i].onClick.AddListener(() => OnResolutionClicked(ci));

                // Color by type
                var btnColors = _resolveBtns[i].colors;
                btnColors.normalColor = i switch
                {
                    0 => new Color(0.4f, 0.3f, 0.8f), // 直面 purple
                    1 => new Color(0.3f, 0.6f, 0.8f), // 反思 blue
                    2 => new Color(0.3f, 0.8f, 0.4f), // 接纳 green
                    3 => new Color(0.8f, 0.3f, 0.3f), // 压制 red
                    _ => new Color(0.4f, 0.4f, 0.4f)
                };
                _resolveBtns[i].colors = btnColors;
            }

            _resolutionButtons.SetActive(false);
            _heartPanel.SetActive(false);
        }

        // ── Result Panel ─────────────────────────────────────────────────

        private void BuildResultPanel()
        {
            _resultPanel = CreatePanel("ResultPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(420, 280), Vector2.zero);
            _resultPanel.transform.SetParent(_canvas.transform);

            var bg = _resultPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            _resultTitleText = MakeLabel("ResultTitle", _resultPanel.transform,
                "渡劫完成", 24, new Color(1f, 0.84f, 0f), new Vector2(0, -24), new Vector2(400, 32));

            _resultBodyText = MakeLabel("ResultBody", _resultPanel.transform,
                "", 20, Color.white, new Vector2(0, -70), new Vector2(400, 28));

            _resultDetailText = MakeLabel("ResultDetail", _resultPanel.transform,
                "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(0, -110), new Vector2(400, 24));

            _resultCloseBtn = MakeButton("CloseBtn", _resultPanel.transform,
                "关闭", new Vector2(0, -170), new Vector2(160, 36));
            _resultCloseBtn.onClick.AddListener(OnResultClosed);

            _resultPanel.SetActive(false);
        }

        // ── Barrier HUD ──────────────────────────────────────────────────

        private void BuildBarrierHUD()
        {
            _barrierHUD = CreatePanel("BarrierHUD",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(200, 36), new Vector2(0, -200));
            _barrierHUD.transform.SetParent(_canvas.transform);

            var barBg = CreatePanel("BarrierBarBg",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(200, 20), Vector2.zero);
            barBg.transform.SetParent(_barrierHUD.transform);
            barBg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

            var barFill = CreatePanel("BarrierFill",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(200, 20), Vector2.zero);
            barFill.transform.SetParent(barBg.transform);
            _barrierHealthBar = barFill.AddComponent<Image>();
            _barrierHealthBar.type = Image.Type.Filled;
            _barrierHealthBar.fillMethod = Image.FillMethod.Horizontal;
            _barrierHealthBar.fillAmount = 1f;
            _barrierHealthBar.color = new Color(1f, 0.84f, 0f, 0.8f);

            _barrierHealthText = MakeLabel("BarrierText", _barrierHUD.transform,
                "护罩 100/100", 14, new Color(1f, 0.84f, 0f), new Vector2(0, -10), new Vector2(200, 16));

            _barrierHUD.SetActive(false);
        }

        // ── UI Primitive Builders ────────────────────────────────────────

        private static GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 sizeDelta, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            return go;
        }

        private static Text MakeLabel(string name, Transform parent, string text,
            int fontSize, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var tmp = go.AddComponent<Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAnchor.MiddleCenter;
            tmp.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return tmp;
        }

        private static Button MakeButton(string name, Transform parent, string label,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.4f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = image;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            // Button text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = size;

            return btn;
        }
    }
}
