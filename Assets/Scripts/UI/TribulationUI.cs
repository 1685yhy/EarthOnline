using System;
using System.Collections;
using System.Collections.Generic;
using EarthOnline.Core;
using EarthOnline.Framework;
using UnityEngine;
using UnityEngine.UI;
using CultivationRealm = EarthOnline.CultivationManager.Realm;

namespace EarthOnline.UI
{
    // ═══════════════════════════════════════════════════════════════════════
    //  UI-Specific Event Data Structs
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Published when a realm breakthrough occurs after successful tribulation.</summary>
    public struct RealmBreakthroughEvent
    {
        public CultivationManager.Realm PreviousRealm;
        public CultivationManager.Realm NewRealm;
        public string BodyTypeName;
        public int BodyQuality;
    }

    /// <summary>Published when cultivation falls back after tribulation failure.</summary>
    public struct BreakthroughFallbackEvent
    {
        public CultivationManager.Realm PreviousRealm;
        public CultivationManager.Realm FallbackRealm;
        public int FailureCount;
        public float ExperienceBonus;
        public string Reason;
    }

    /// <summary>Published when disconnect protection timer state changes.</summary>
    public struct DisconnectProtectionTimerEvent
    {
        public bool IsActive;
        public float RemainingTime;
        public bool IsExpired;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Tribulation UI Controller
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages all tribulation (渡劫) UI presented to the player:
    ///
    /// 1) 渡劫确认面板 — readiness scores, success rate estimation, suggestion list
    /// 2) 雷劫HUD — lightning strike counter, sequence indicator, dodge feedback
    /// 3) 心魔UI — willpower bar, demon display, resolution method panel
    /// 4) 天道问心 — question text, answer button grid
    /// 5) 道体面板 — quality, traits, appearance description
    /// 6) 断线保护 — 5-minute disconnect protection overlay (daily once)
    /// 7) PVP结界 — barrier durability display, PVP attack indicator
    /// 8) 成功→OnRealmBreakthrough — publishes RealmBreakthroughEvent on success
    /// 9) 失败→CultivationManager回退 — displays fallback info, publishes BreakthroughFallbackEvent
    ///
    /// Communication via EventBus (typed structs). UGUI SerializeFields for
    /// all visual references — drag-and-drop wiring in the Unity Editor.
    /// </summary>
    public class TribulationUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        //  PANEL REFERENCES
        // ═══════════════════════════════════════════════════════════════════

        [Header("Panel Roots (assigned in Editor)")]
        [SerializeField] private GameObject _confirmationPanel;
        [SerializeField] private GameObject _thunderHudPanel;
        [SerializeField] private GameObject _heartDemonPanel;
        [SerializeField] private GameObject _daoQuestioningPanel;
        [SerializeField] private GameObject _daoBodyPanel;
        [SerializeField] private GameObject _disconnectOverlay;
        [SerializeField] private GameObject _pvpBarrierIndicator;

        // ═══════════════════════════════════════════════════════════════════
        //  CONFIRMATION PANEL (渡劫确认面板)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Confirmation Panel")]
        [SerializeField] private Text _qualityLabelText;
        [SerializeField] private Image _qualityColorImage;

        [SerializeField] private Text _pillScoreLabel;
        [SerializeField] private Slider _pillBar;
        [SerializeField] private Text _pillScoreText;

        [SerializeField] private Text _equipScoreLabel;
        [SerializeField] private Slider _equipBar;
        [SerializeField] private Text _equipScoreText;

        [SerializeField] private Text _formScoreLabel;
        [SerializeField] private Slider _formBar;
        [SerializeField] private Text _formScoreText;

        [SerializeField] private Text _escortScoreLabel;
        [SerializeField] private Slider _escortBar;
        [SerializeField] private Text _escortScoreText;

        [SerializeField] private Text _totalReadinessLabel;
        [SerializeField] private Slider _totalReadinessBar;
        [SerializeField] private Text _totalReadinessText;

        [SerializeField] private Text _successRateLabel;
        [SerializeField] private Slider _successRateBar;
        [SerializeField] private Text _successRateText;

        [SerializeField] private Text _daoBodyWarningText;
        [SerializeField] private Text _suggestionListText;

        [SerializeField] private Button _startTribulationButton;
        [SerializeField] private Button _cancelConfirmationButton;

        [Header("Confirmation Colors")]
        [SerializeField] private Color _qualityNormalColor = new Color(0.6f, 0.6f, 0.8f);
        [SerializeField] private Color _qualityAncientColor = new Color(0.8f, 0.6f, 0.2f);
        [SerializeField] private Color _qualitySecretColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] private Color _scoreGoodColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color _scoreWarnColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color _scoreBadColor = new Color(0.9f, 0.3f, 0.3f);

        // ═══════════════════════════════════════════════════════════════════
        //  THUNDER HUD (雷劫HUD)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Thunder HUD")]
        [SerializeField] private Text _strikeCounterText;
        [SerializeField] private Slider _strikeProgressBar;
        [SerializeField] private Text _strikeSequenceText;
        [SerializeField] private Text _dodgeCountText;
        [SerializeField] private Text _warningActiveText;
        [SerializeField] private GameObject _warningFlashObject;
        [SerializeField] private Text _barrierDurabilityText;
        [SerializeField] private Slider _barrierDurabilityBar;

        // ═══════════════════════════════════════════════════════════════════
        //  HEART DEMON PANEL (心魔UI)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Heart Demon Panel")]
        [SerializeField] private Slider _willpowerBar;
        [SerializeField] private Image _willpowerFillImage;
        [SerializeField] private Text _willpowerText;
        [SerializeField] private Text _demonNameText;
        [SerializeField] private Text _demonDescriptionText;
        [SerializeField] private Text _resolutionHintText;
        [SerializeField] private Text _demonCounterText;

        [SerializeField] private Button _confrontButton;
        [SerializeField] private Button _reflectButton;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _suppressButton;

        [SerializeField] private Text _resolutionFeedbackText;
        [SerializeField] private float _feedbackDisplayDuration = 2f;

        [Header("Willpower Colors")]
        [SerializeField] private Color _willpowerHighColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color _willpowerMidColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color _willpowerLowColor = new Color(0.9f, 0.3f, 0.2f);

        // ═══════════════════════════════════════════════════════════════════
        //  DAO QUESTIONING PANEL (天道问心)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Dao Questioning Panel")]
        [SerializeField] private Text _questionCounterText;
        [SerializeField] private Text _questionText;
        [SerializeField] private Button[] _answerButtons;

        // ═══════════════════════════════════════════════════════════════════
        //  DAO BODY PANEL (道体面板)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Dao Body Panel")]
        [SerializeField] private Text _bodyQualityNameText;
        [SerializeField] private Text _bodyTypeNameText;
        [SerializeField] private Text _bodyStatsText;
        [SerializeField] private Text _bodyAppearanceDescText;
        [SerializeField] private Text _formationResultText;
        [SerializeField] private Image _formationResultIcon;
        [SerializeField] private GameObject _formationSuccessGroup;
        [SerializeField] private GameObject _formationFailureGroup;
        [SerializeField] private Button _bodyConfirmButton;

        // ═══════════════════════════════════════════════════════════════════
        //  DISCONNECT PROTECTION (断线保护)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Disconnect Protection")]
        [SerializeField] private Text _protectionTimerText;
        [SerializeField] private Text _protectionStatusText;
        [SerializeField] private Image _protectionTimerRing;
        [SerializeField] private GameObject _protectionActiveGroup;
        [SerializeField] private GameObject _protectionExpiredGroup;

        // ═══════════════════════════════════════════════════════════════════
        //  PVP BARRIER INDICATOR
        // ═══════════════════════════════════════════════════════════════════

        [Header("PVP Barrier")]
        [SerializeField] private Text _pvpStatusText;
        [SerializeField] private Text _pvpBarrierHealthText;

        // ═══════════════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ═══════════════════════════════════════════════════════════════════

        private bool _isConfirmationVisible;
        private string _currentPlatformId;
        private TribulationQuality _currentQuality;
        private ReadinessScores _currentScores;
        private float _currentDaoBodyPenalty;

        // Thunder state
        private int _totalStrikes;
        private int _currentStrikeIndex;
        private int _perfectDodgeCount;
        private Coroutine _warningFlashCoroutine;

        // Heart demon state
        private float _lastWillpower;
        private Coroutine _feedbackCoroutine;

        // Disconnect protection state
        private bool _disconnectProtectionUsedToday;
        private bool _isDisconnected;
        private float _disconnectTimer;
        private Coroutine _disconnectTimerCoroutine;
        private const float DISCONNECT_PROTECTION_DURATION = 300f; // 5 minutes
        private const string DISCONNECT_DAILY_KEY = "TribulationDisconnectUsed";
        private const string TRIBULATION_BACKUP_KEY = "TribulationBackupState";
        private const string TRIBULATION_BACKUP_DATE_KEY = "TribulationBackupDate";

        // PVP state
        private bool _isPvPZone;
        private float _lastBarrierAttackTime;
        private const float PVP_ATTACK_COOLDOWN = 0.5f;

        // ═══════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            LoadDailyDisconnectState();
            SetAllPanelsActive(false);
            if (_protectionActiveGroup != null) _protectionActiveGroup.SetActive(false);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(false);
            if (_formationSuccessGroup != null) _formationSuccessGroup.SetActive(false);
            if (_formationFailureGroup != null) _formationFailureGroup.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TribulationConfirmationEvent>(OnTribulationConfirmation);
            EventBus.Subscribe<TribulationStartedEvent>(OnTribulationStarted);
            EventBus.Subscribe<TribulationCompletedEvent>(OnTribulationCompleted);
            EventBus.Subscribe<TribulationBarrierCreatedEvent>(OnBarrierCreated);
            EventBus.Subscribe<TribulationBarrierDamagedEvent>(OnBarrierDamaged);
            EventBus.Subscribe<ThunderStrikeWarningEvent>(OnThunderStrikeWarning);
            EventBus.Subscribe<ThunderStrikeStruckEvent>(OnThunderStrikeStruck);
            EventBus.Subscribe<ThunderStrikeDodgedEvent>(OnThunderStrikeDodged);
            EventBus.Subscribe<ThunderTribulationCompletedEvent>(OnThunderTribulationCompleted);
            EventBus.Subscribe<HeartDemonStageStartedEvent>(OnHeartDemonStageStarted);
            EventBus.Subscribe<HeartDemonSpawnedEvent>(OnHeartDemonSpawned);
            EventBus.Subscribe<HeartDemonWillPowerChangedEvent>(OnHeartDemonWillpowerChanged);
            EventBus.Subscribe<HeartDemonResolvedEvent>(OnHeartDemonResolved);
            EventBus.Subscribe<HeartDemonFailedEvent>(OnHeartDemonFailed);
            EventBus.Subscribe<HeartDemonAllClearedEvent>(OnHeartDemonAllCleared);
            EventBus.Subscribe<DaoQuestioningStartedEvent>(OnDaoQuestioningStarted);
            EventBus.Subscribe<DaoQuestionPresentedEvent>(OnDaoQuestionPresented);
            EventBus.Subscribe<DaoQuestioningCompletedEvent>(OnDaoQuestioningCompleted);
            EventBus.Subscribe<DaoBodyFormedEvent>(OnDaoBodyFormed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TribulationConfirmationEvent>(OnTribulationConfirmation);
            EventBus.Unsubscribe<TribulationStartedEvent>(OnTribulationStarted);
            EventBus.Unsubscribe<TribulationCompletedEvent>(OnTribulationCompleted);
            EventBus.Unsubscribe<TribulationBarrierCreatedEvent>(OnBarrierCreated);
            EventBus.Unsubscribe<TribulationBarrierDamagedEvent>(OnBarrierDamaged);
            EventBus.Unsubscribe<ThunderStrikeWarningEvent>(OnThunderStrikeWarning);
            EventBus.Unsubscribe<ThunderStrikeStruckEvent>(OnThunderStrikeStruck);
            EventBus.Unsubscribe<ThunderStrikeDodgedEvent>(OnThunderStrikeDodged);
            EventBus.Unsubscribe<ThunderTribulationCompletedEvent>(OnThunderTribulationCompleted);
            EventBus.Unsubscribe<HeartDemonStageStartedEvent>(OnHeartDemonStageStarted);
            EventBus.Unsubscribe<HeartDemonSpawnedEvent>(OnHeartDemonSpawned);
            EventBus.Unsubscribe<HeartDemonWillPowerChangedEvent>(OnHeartDemonWillpowerChanged);
            EventBus.Unsubscribe<HeartDemonResolvedEvent>(OnHeartDemonResolved);
            EventBus.Unsubscribe<HeartDemonFailedEvent>(OnHeartDemonFailed);
            EventBus.Unsubscribe<HeartDemonAllClearedEvent>(OnHeartDemonAllCleared);
            EventBus.Unsubscribe<DaoQuestioningStartedEvent>(OnDaoQuestioningStarted);
            EventBus.Unsubscribe<DaoQuestionPresentedEvent>(OnDaoQuestionPresented);
            EventBus.Unsubscribe<DaoQuestioningCompletedEvent>(OnDaoQuestioningCompleted);
            EventBus.Unsubscribe<DaoBodyFormedEvent>(OnDaoBodyFormed);
        }

        private void Update()
        {
            // Update disconnect protection timer UI
            if (_isDisconnected)
            {
                UpdateDisconnectTimerDisplay();
            }

            // Update barrier flash animation if warning is active
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — CONFIRMATION PANEL (渡劫确认)
        // ═══════════════════════════════════════════════════════════════════

        private void OnTribulationConfirmation(TribulationConfirmationEvent evt)
        {
            if (evt.Show)
            {
                _isConfirmationVisible = true;
                _currentPlatformId = evt.PlatformId;
                _currentQuality = ParseQuality(evt.Quality);
                _currentDaoBodyPenalty = evt.DaoBodyPenalty;

                // Store individual scores
                _currentScores = new ReadinessScores
                {
                    pill = evt.PillScore,
                    equip = evt.EquipScore,
                    form = evt.FormScore,
                    escort = evt.EscortScore
                };

                ShowConfirmationPanel(evt);
            }
            else
            {
                _isConfirmationVisible = false;
                _confirmationPanel.SetActive(false);
            }
        }

        /// <summary>Display the full confirmation panel with scores, rate, and suggestions.</summary>
        private void ShowConfirmationPanel(TribulationConfirmationEvent evt)
        {
            SetAllPanelsActive(false);
            _confirmationPanel.SetActive(true);

            // ── Quality label + color ──
            if (_qualityLabelText != null)
            {
                _qualityLabelText.text = GetQualityDisplayName(evt.Quality);
            }
            if (_qualityColorImage != null)
            {
                _qualityColorImage.color = GetQualityColor(evt.Quality);
            }

            // ── Individual readiness bars ──
            SetReadinessBar(_pillBar, _pillScoreLabel, _pillScoreText, "丹药", evt.PillScore);
            SetReadinessBar(_equipBar, _equipScoreLabel, _equipScoreText, "装备", evt.EquipScore);
            SetReadinessBar(_formBar, _formScoreLabel, _formScoreText, "阵法", evt.FormScore);
            SetReadinessBar(_escortBar, _escortScoreLabel, _escortScoreText, "护法", evt.EscortScore);

            // ── Total readiness ──
            if (_totalReadinessBar != null)
            {
                _totalReadinessBar.value = evt.ReadinessScore;
                var fill = _totalReadinessBar.fillRect?.GetComponentInChildren<Image>(true);
                if (fill != null) fill.color = GetScoreColor(evt.ReadinessScore);
            }
            if (_totalReadinessText != null)
                _totalReadinessText.text = $"{evt.ReadinessScore * 100f:F0}%";

            // ── Success rate ──
            if (_successRateBar != null)
            {
                _successRateBar.value = evt.EstimatedSuccessRate;
                var fill = _successRateBar.fillRect?.GetComponentInChildren<Image>(true);
                if (fill != null) fill.color = GetScoreColor(evt.EstimatedSuccessRate);
            }
            if (_successRateText != null)
                _successRateText.text = $"{evt.EstimatedSuccessRate * 100f:F0}%";

            // ── Dao Body warning ──
            if (_daoBodyWarningText != null)
            {
                if (evt.DaoBodyPenalty > 0f)
                {
                    _daoBodyWarningText.text = $"<color=#FF6666>⚠ 天劫台品阶高于当前道体，道体将提升1级，但失败率 +{evt.DaoBodyPenalty * 100f:F0}%</color>";
                    _daoBodyWarningText.gameObject.SetActive(true);
                }
                else
                {
                    _daoBodyWarningText.gameObject.SetActive(false);
                }
            }

            // ── Suggestion list ──
            if (_suggestionListText != null)
            {
                string[] suggestions = GenerateSuggestions(evt);
                _suggestionListText.text = "建议清单:\n" + string.Join("\n", suggestions);
            }

            // ── Buttons ──
            WireConfirmationButtons();
        }

        /// <summary>Wire the start and cancel confirmation buttons.</summary>
        private void WireConfirmationButtons()
        {
            if (_startTribulationButton != null)
            {
                _startTribulationButton.onClick.RemoveAllListeners();
                _startTribulationButton.onClick.AddListener(OnStartTribulationClicked);
            }
            if (_cancelConfirmationButton != null)
            {
                _cancelConfirmationButton.onClick.RemoveAllListeners();
                _cancelConfirmationButton.onClick.AddListener(OnCancelConfirmationClicked);
            }
        }

        /// <summary>Called when the player confirms and starts the tribulation.</summary>
        public void OnStartTribulationClicked()
        {
            // Delegate to the platform's ConfirmTribulation method
            // Find the platform by ID or use the current one
            var platforms = FindObjectsByType<TribulationPlatform>(FindObjectsSortMode.None);
            foreach (var p in platforms)
            {
                if (p.PlatformId == _currentPlatformId && p.CanInteract)
                {
                    // Use current scores (from event) and default dao body quality
                    p.ConfirmTribulation(
                        _currentScores.pill,
                        _currentScores.equip,
                        _currentScores.form,
                        _currentScores.escort,
                        0, // escort count — will be replaced with actual count
                        0  // dao body quality — will be replaced with actual quality
                    );
                    break;
                }
            }

            _confirmationPanel.SetActive(false);
            _isConfirmationVisible = false;
        }

        /// <summary>Called when the player cancels the tribulation confirmation.</summary>
        public void OnCancelConfirmationClicked()
        {
            _confirmationPanel.SetActive(false);
            _isConfirmationVisible = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — TRIBULATION STARTED
        // ═══════════════════════════════════════════════════════════════════

        private void OnTribulationStarted(TribulationStartedEvent evt)
        {
            // Hide confirmation, show thunder HUD
            _confirmationPanel.SetActive(false);
            _thunderHudPanel.SetActive(true);

            // Reset thunder state
            _perfectDodgeCount = 0;
            _currentStrikeIndex = 0;
            _totalStrikes = 0;

            // Barrier indicators
            UpdateBarrierDurability(evt.BarrierMaxDurability, evt.BarrierMaxDurability);

            // Publish realm breakthrough event placeholder — will fire on completion
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — BARRIER
        // ═══════════════════════════════════════════════════════════════════

        private void OnBarrierCreated(TribulationBarrierCreatedEvent evt)
        {
            UpdateBarrierDurability(evt.MaxDurability, evt.MaxDurability);
        }

        private void OnBarrierDamaged(TribulationBarrierDamagedEvent evt)
        {
            if (TribulationManager.Instance == null) return;
            UpdateBarrierDurability(evt.RemainingDurability, TribulationManager.Instance.BarrierMaxDurability);
        }

        private void UpdateBarrierDurability(float current, float max)
        {
            if (_barrierDurabilityBar != null)
            {
                _barrierDurabilityBar.maxValue = max;
                _barrierDurabilityBar.value = current;
            }
            if (_barrierDurabilityText != null)
                _barrierDurabilityText.text = $"结界: {current:F0}/{max:F0}";
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — THUNDER STRIKES (雷劫)
        // ═══════════════════════════════════════════════════════════════════

        private void OnThunderStrikeWarning(ThunderStrikeWarningEvent evt)
        {
            _currentStrikeIndex = evt.StrikeIndex;
            _totalStrikes = evt.TotalStrikes;

            // Update strike counter
            if (_strikeCounterText != null)
                _strikeCounterText.text = $"第 {evt.StrikeIndex}/{evt.TotalStrikes} 道天雷";

            // Update progress bar
            if (_strikeProgressBar != null)
            {
                _strikeProgressBar.maxValue = evt.TotalStrikes;
                _strikeProgressBar.value = evt.StrikeIndex - 1;
            }

            // Update sequence indicator
            if (_strikeSequenceText != null)
            {
                _strikeSequenceText.text = BuildStrikeSequenceString(evt.StrikeIndex, evt.TotalStrikes);
            }

            // Show warning flash
            ShowWarningFlash(true);
            if (_warningActiveText != null)
            {
                _warningActiveText.text = $"<color=#FF4444>⚡ 天雷将至！{evt.TimeUntilStrike:F1}s</color>";
                _warningActiveText.gameObject.SetActive(true);
            }
        }

        private void OnThunderStrikeStruck(ThunderStrikeStruckEvent evt)
        {
            // Hide warning
            ShowWarningFlash(false);
            if (_warningActiveText != null)
                _warningActiveText.gameObject.SetActive(false);

            // Update progress bar
            if (_strikeProgressBar != null)
                _strikeProgressBar.value = _currentStrikeIndex;
        }

        private void OnThunderStrikeDodged(ThunderStrikeDodgedEvent evt)
        {
            _perfectDodgeCount = evt.TotalPerfectDodges;

            // Update dodge counter
            if (_dodgeCountText != null)
                _dodgeCountText.text = $"完美闪避: {evt.TotalPerfectDodges}";

            // Update progress bar
            if (_strikeProgressBar != null)
                _strikeProgressBar.value = _currentStrikeIndex;

            // Hide warning
            ShowWarningFlash(false);
            if (_warningActiveText != null)
                _warningActiveText.gameObject.SetActive(false);
        }

        private void OnThunderTribulationCompleted(ThunderTribulationCompletedEvent evt)
        {
            // Final update to dodge counter
            if (_dodgeCountText != null)
                _dodgeCountText.text = $"完美闪避: {evt.PerfectDodges}/{evt.TotalStrikes}";

            // Complete strike progress
            if (_strikeProgressBar != null)
                _strikeProgressBar.value = evt.TotalStrikes;

            if (_strikeCounterText != null)
                _strikeCounterText.text = $"雷劫完成 — 闪避 {evt.PerfectDodges}/{evt.TotalStrikes}";

            // Hide warning
            ShowWarningFlash(false);
            if (_warningActiveText != null)
                _warningActiveText.gameObject.SetActive(false);

            // Keep HUD visible during heart demon transition
        }

        /// <summary>Show/hide the warning flash indicator (world-space or screen-space).</summary>
        private void ShowWarningFlash(bool active)
        {
            if (_warningFlashObject != null)
                _warningFlashObject.SetActive(active);

            if (active)
            {
                if (_warningFlashCoroutine != null) StopCoroutine(_warningFlashCoroutine);
                _warningFlashCoroutine = StartCoroutine(WarningFlashAnim());
            }
        }

        private IEnumerator WarningFlashAnim()
        {
            if (_warningFlashObject == null) yield break;

            float duration = 1f;
            float elapsed = 0f;
            CanvasGroup cg = _warningFlashObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = _warningFlashObject.AddComponent<CanvasGroup>();

            while (_warningFlashObject.activeSelf)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 0.3f + Mathf.PingPong(elapsed * 4f, 0.7f);
                yield return null;
            }

            cg.alpha = 0f;
        }

        /// <summary>Build a strike sequence string showing past/current/upcoming strikes.</summary>
        private static string BuildStrikeSequenceString(int currentIndex, int totalStrikes)
        {
            char[] chars = new char[Mathf.Min(totalStrikes, 18)];
            int displayCount = chars.Length;

            for (int i = 0; i < displayCount; i++)
            {
                int strikeNum = i + 1;
                if (strikeNum < currentIndex)
                    chars[i] = '●'; // past
                else if (strikeNum == currentIndex)
                    chars[i] = '◆'; // current
                else
                    chars[i] = '○'; // upcoming
            }

            return new string(chars);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — HEART DEMON (心魔)
        // ═══════════════════════════════════════════════════════════════════

        private void OnHeartDemonStageStarted(HeartDemonStageStartedEvent evt)
        {
            _heartDemonPanel.SetActive(true);

            // Initialize willpower
            _lastWillpower = evt.InitialWillpower;
            UpdateWillpowerDisplay(evt.InitialWillpower, evt.InitialWillpower, evt.InitialWillpower);

            if (_demonCounterText != null)
                _demonCounterText.text = $"心魔 0/{evt.DemonCount}";

            // Wire resolution buttons
            WireResolutionButtons();
        }

        private void OnHeartDemonSpawned(HeartDemonSpawnedEvent evt)
        {
            if (_demonNameText != null)
                _demonNameText.text = $"心魔·{evt.DemonType}";

            if (_demonDescriptionText != null)
                _demonDescriptionText.text = evt.Description;

            if (_resolutionHintText != null)
                _resolutionHintText.text = $"<color=#AAAA88>提示: {evt.ResolutionHint}</color>";

            if (_demonCounterText != null)
                _demonCounterText.text = $"心魔 {evt.DemonIndex}/{evt.TotalDemons}";

            // Enable resolution buttons
            SetResolutionButtonsEnabled(true);
            if (_resolutionFeedbackText != null)
                _resolutionFeedbackText.gameObject.SetActive(false);
        }

        private void OnHeartDemonWillpowerChanged(HeartDemonWillPowerChangedEvent evt)
        {
            UpdateWillpowerDisplay(evt.PreviousWillpower, evt.CurrentWillpower, evt.MaxWillpower);

            // Show drain feedback
            if (evt.Reason == "time_drain" && _resolutionFeedbackText != null)
            {
                // Don't show time drain as popup — it's continuous
            }
        }

        private void OnHeartDemonResolved(HeartDemonResolvedEvent evt)
        {
            SetResolutionButtonsEnabled(false);

            string methodName = evt.ResolutionMethod switch
            {
                "confront" => "直面",
                "reflect" => "反思",
                "accept" => "接纳",
                "suppress" => "压制",
                _ => evt.ResolutionMethod
            };

            if (_resolutionFeedbackText != null)
            {
                if (evt.Success)
                {
                    _resolutionFeedbackText.text = $"<color=#66FF66>{methodName}成功！</color>";
                    _resolutionFeedbackText.color = _scoreGoodColor;
                }
                else
                {
                    _resolutionFeedbackText.text = $"<color=#FF6666>{methodName}失败！意志 -{evt.WillpowerCost:F0}</color>";
                    _resolutionFeedbackText.color = _scoreBadColor;
                }
                _resolutionFeedbackText.gameObject.SetActive(true);
            }

            // Auto-hide feedback after delay
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        private IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(_feedbackDisplayDuration);
            if (_resolutionFeedbackText != null)
                _resolutionFeedbackText.gameObject.SetActive(false);
            _feedbackCoroutine = null;
        }

        private void OnHeartDemonFailed(HeartDemonFailedEvent evt)
        {
            // Show failure state
            if (_demonDescriptionText != null)
                _demonDescriptionText.text = "心魔侵蚀了你的意志...渡劫失败。";

            SetResolutionButtonsEnabled(false);

            // Keep panel visible for a moment, then hide
            StartCoroutine(DelayedPanelHide(_heartDemonPanel, 3f));
        }

        private void OnHeartDemonAllCleared(HeartDemonAllClearedEvent evt)
        {
            if (_demonDescriptionText != null)
                _demonDescriptionText.text = "所有心魔已被清除！道心坚定。";

            SetResolutionButtonsEnabled(false);
        }

        /// <summary>Update the willpower bar and text with color transitions.</summary>
        private void UpdateWillpowerDisplay(float previous, float current, float max)
        {
            if (_willpowerBar != null)
            {
                _willpowerBar.maxValue = max;
                _willpowerBar.value = current;
            }

            if (_willpowerText != null)
                _willpowerText.text = $"意志: {current:F0}/{max:F0}";

            // Color based on percentage
            float pct = max > 0f ? current / max : 0f;
            Color willColor = pct > 0.5f ? _willpowerHighColor
                            : pct > 0.25f ? _willpowerMidColor
                            : _willpowerLowColor;

            if (_willpowerFillImage != null)
                _willpowerFillImage.color = willColor;

            if (_willpowerText != null)
                _willpowerText.color = willColor;
        }

        /// <summary>Wire the four resolution method buttons.</summary>
        private void WireResolutionButtons()
        {
            WireResButton(_confrontButton, HeartDemonTribulation.ResolutionMethod.Confront, "直面");
            WireResButton(_reflectButton, HeartDemonTribulation.ResolutionMethod.Reflect, "反思");
            WireResButton(_acceptButton, HeartDemonTribulation.ResolutionMethod.Accept, "接纳");
            WireResButton(_suppressButton, HeartDemonTribulation.ResolutionMethod.Suppress, "压制");
        }

        private void WireResButton(Button btn, HeartDemonTribulation.ResolutionMethod method, string label)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnResolutionMethodClicked(method));
        }

        /// <summary>Called when the player clicks a resolution method button.</summary>
        public void OnResolutionMethodClicked(HeartDemonTribulation.ResolutionMethod method)
        {
            var heartDemon = FindFirstObjectByType<HeartDemonTribulation>();
            if (heartDemon != null)
            {
                heartDemon.AttemptResolution(method);
            }
        }

        private void SetResolutionButtonsEnabled(bool enabled)
        {
            if (_confrontButton != null) _confrontButton.interactable = enabled;
            if (_reflectButton != null) _reflectButton.interactable = enabled;
            if (_acceptButton != null) _acceptButton.interactable = enabled;
            if (_suppressButton != null) _suppressButton.interactable = enabled;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — DAO QUESTIONING (天道问心)
        // ═══════════════════════════════════════════════════════════════════

        private void OnDaoQuestioningStarted(DaoQuestioningStartedEvent evt)
        {
            _daoQuestioningPanel.SetActive(true);

            if (_questionCounterText != null)
                _questionCounterText.text = $"天道问心 — 共 {evt.TotalQuestions} 问";
        }

        private void OnDaoQuestionPresented(DaoQuestionPresentedEvent evt)
        {
            if (_questionCounterText != null)
                _questionCounterText.text = $"第 {evt.QuestionIndex}/{evt.TotalQuestions} 问";

            if (_questionText != null)
                _questionText.text = evt.QuestionText;

            // Set up answer buttons
            for (int i = 0; i < _answerButtons.Length; i++)
            {
                if (_answerButtons[i] == null) continue;

                if (i < evt.AnswerTexts.Length && !string.IsNullOrEmpty(evt.AnswerTexts[i]))
                {
                    _answerButtons[i].gameObject.SetActive(true);
                    var textComp = _answerButtons[i].GetComponentInChildren<Text>();
                    if (textComp != null) textComp.text = evt.AnswerTexts[i];

                    int capturedIndex = i;
                    _answerButtons[i].onClick.RemoveAllListeners();
                    _answerButtons[i].onClick.AddListener(() => OnAnswerClicked(capturedIndex));
                    _answerButtons[i].interactable = true;
                }
                else
                {
                    _answerButtons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Called when the player clicks an answer button during Dao Questioning.</summary>
        public void OnAnswerClicked(int answerIndex)
        {
            // Disable all answer buttons to prevent double-clicks
            foreach (var btn in _answerButtons)
            {
                if (btn != null) btn.interactable = false;
            }

            // Submit the answer to DaoQuestioning
            var daoQuestioning = FindFirstObjectByType<DaoQuestioning>();
            if (daoQuestioning != null)
            {
                daoQuestioning.SubmitAnswer(answerIndex);
            }
        }

        private void OnDaoQuestioningCompleted(DaoQuestioningCompletedEvent evt)
        {
            // Keep panel visible briefly, then transition to Dao Body panel
            StartCoroutine(DelayedPanelHide(_daoQuestioningPanel, 1.5f));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — DAO BODY FORMATION (道体)
        // ═══════════════════════════════════════════════════════════════════

        private void OnDaoBodyFormed(DaoBodyFormedEvent evt)
        {
            _daoBodyPanel.SetActive(true);

            // Quality name
            if (_bodyQualityNameText != null)
                _bodyQualityNameText.text = evt.QualityName;

            // Body type name
            if (_bodyTypeNameText != null)
                _bodyTypeNameText.text = evt.BodyTypeName;

            // Stats display
            if (_bodyStatsText != null)
            {
                DaoBodyType bodyType = (DaoBodyType)evt.BodyType;
                _bodyStatsText.text = BuildBodyStatsDisplay(bodyType, evt.Quality);
            }

            // Appearance description
            if (_bodyAppearanceDescText != null)
                _bodyAppearanceDescText.text = GetBodyAppearanceDescription((DaoBodyType)evt.BodyType, evt.Quality);

            // Success / Failure display
            if (evt.Success)
            {
                if (_formationSuccessGroup != null) _formationSuccessGroup.SetActive(true);
                if (_formationFailureGroup != null) _formationFailureGroup.SetActive(false);

                if (_formationResultText != null)
                {
                    _formationResultText.text = $"<color=#66FF66>道体凝聚成功！</color>\n" +
                                                $"{evt.QualityName}·{evt.BodyTypeName}";
                }
            }
            else
            {
                if (_formationSuccessGroup != null) _formationSuccessGroup.SetActive(false);
                if (_formationFailureGroup != null) _formationFailureGroup.SetActive(true);

                if (_formationResultText != null)
                {
                    string expBonus = Mathf.Min(evt.FailureCount * 0.05f, 0.25f) * 100f + "%";
                    _formationResultText.text = $"<color=#FF6666>道体凝聚失败...</color>\n" +
                                                $"第 {evt.FailureCount} 次失败\n" +
                                                $"下次成功率: +{evt.FailureCount * 10}%\n" +
                                                $"累计经验加成: {expBonus}";
                }
            }

            // Confirm button → publish breakthrough event and clean up
            if (_bodyConfirmButton != null)
            {
                _bodyConfirmButton.onClick.RemoveAllListeners();
                _bodyConfirmButton.onClick.AddListener(() =>
                {
                    _daoBodyPanel.SetActive(false);
                    OnTribulationFullyComplete(evt.Success);
                });
            }
        }

        /// <summary>Called after the player dismisses the Dao Body panel.</summary>
        private void OnTribulationFullyComplete(bool success)
        {
            // Hide all tribulation panels
            SetAllPanelsActive(false);

            if (success)
            {
                // ── Publish RealmBreakthroughEvent ──
                CultivationManager.Realm previous = CultivationManager.Realm.GreatPerfection;
                CultivationManager.Realm newRealm = CultivationManager.Realm.TribulationPassed;
                if (CultivationManager.Instance != null)
                    newRealm = CultivationManager.Instance.CurrentRealm;

                EventBus.Publish(new RealmBreakthroughEvent
                {
                    PreviousRealm = previous,
                    NewRealm = newRealm,
                    BodyTypeName = TribulationBody.Instance != null
                        ? TribulationBody.Instance.GetDisplayString() : "未知",
                    BodyQuality = TribulationBody.Instance != null
                        ? TribulationBody.Instance.Quality : 0
                });

                Debug.Log($"[TribulationUI] RealmBreakthroughEvent published: {previous} → {newRealm}");
            }
            else
            {
                // ── Publish BreakthroughFallbackEvent ──
                EventBus.Publish(new BreakthroughFallbackEvent
                {
                    PreviousRealm = CultivationManager.Realm.TribulationPassed,
                    FallbackRealm = CultivationManager.Realm.GreatPerfection,
                    FailureCount = TribulationBody.Instance?.FailureCount ?? 1,
                    ExperienceBonus = Mathf.Min(TribulationBody.Instance?.FailureCount ?? 1 * 0.05f, 0.25f),
                    Reason = "道体凝聚失败"
                });

                Debug.Log($"[TribulationUI] BreakthroughFallbackEvent published: reverted to GreatPerfection");
            }

            // Clear any disconnect protection state
            ClearDisconnectProtection();
        }

        /// <summary>Build a stat display string for the body type and quality.</summary>
        private static string BuildBodyStatsDisplay(DaoBodyType bodyType, int quality)
        {
            float scale = 1f + (quality - 1) * 0.05f;

            string atkBonus = bodyType switch
            {
                DaoBodyType.Breaker => $"+{0.30f * scale * 100f:F0}%",
                DaoBodyType.Transcendent => $"+{0.10f * scale * 100f:F0}%",
                _ => "无加成"
            };

            string defBonus = bodyType switch
            {
                DaoBodyType.Guardian => $"+{0.30f * scale * 100f:F0}%",
                DaoBodyType.Transcendent => $"+{0.10f * scale * 100f:F0}%",
                _ => "无加成"
            };

            string spdBonus = bodyType == DaoBodyType.Transcendent
                ? $"+{0.10f * scale * 100f:F0}%"
                : "无加成";

            string fuseNote = bodyType == DaoBodyType.Mortal
                ? "可与其他道体融合"
                : "不可融合";

            return $"攻击: {atkBonus}\n防御: {defBonus}\n速度: {spdBonus}\n特性: {fuseNote}";
        }

        /// <summary>Generate appearance description text for the Dao Body.</summary>
        private static string GetBodyAppearanceDescription(DaoBodyType bodyType, int quality)
        {
            string qualityDesc = quality switch
            {
                1 => "周身笼罩着极淡的灵光，若隐若现。",
                2 => "体表流转着莹白光泽，灵气自毛孔中缓缓溢出。",
                3 => "道韵自然而然地环绕周身，每一步都引动天地灵气共鸣。",
                4 => "圣光普照，周遭灵草因你而加速生长，天地为之色变。",
                5 => "混沌之气缠绕，虚空为之扭曲，万法不侵，诸邪避易。",
                _ => "灵气环绕，道韵天成。"
            };

            string typeDesc = bodyType switch
            {
                DaoBodyType.Guardian =>
                    "身形如山岳般沉稳，厚重的大地气息自足下蔓延。" +
                    "肌肤上隐现古老的防御符文，每一次呼吸都与地脉共振。",
                DaoBodyType.Breaker =>
                    "双瞳如利刃般锐利，周身弥漫着凌厉的破灭气息。" +
                    "举手投足间，虚空为之震颤，仿佛能撕裂一切阻碍。",
                DaoBodyType.Transcendent =>
                    "身形若隐若现，仿佛随时会融于天地之间。" +
                    "周身流转着淡金色的道韵之光，超然物外，不染尘埃。",
                DaoBodyType.Mortal =>
                    "外表与凡人无异，返璞归真。" +
                    "但若细察，可发现其眼中偶尔闪过深邃的道蕴，内含无限可能。",
                _ => "道体初成，气息内敛。"
            };

            return $"外观: {typeDesc}\n{qualityDesc}";
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLER — TRIBULATION COMPLETED (final cleanup)
        // ═══════════════════════════════════════════════════════════════════

        private void OnTribulationCompleted(TribulationCompletedEvent evt)
        {
            // Hide thunder HUD and heart demon panel (they may still be visible)
            _thunderHudPanel.SetActive(false);
            _heartDemonPanel.SetActive(false);

            // If Dao Body formation hasn't been shown yet (e.g. barrier broke),
            // show a direct failure panel
            if (!_daoBodyPanel.activeSelf && !evt.Success)
            {
                // Barrier was destroyed or other premature failure
                _formationSuccessGroup?.SetActive(false);
                _formationFailureGroup?.SetActive(true);

                if (_formationResultText != null)
                {
                    _formationResultText.text = "<color=#FF6666>渡劫失败</color>\n天劫结界已被击破。";
                }

                _daoBodyPanel.SetActive(true);

                if (_bodyConfirmButton != null)
                {
                    _bodyConfirmButton.onClick.RemoveAllListeners();
                    _bodyConfirmButton.onClick.AddListener(() =>
                    {
                        _daoBodyPanel.SetActive(false);
                        OnTribulationFullyComplete(false);
                    });
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SUGGESTION GENERATION (建议清单)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Generate a list of suggestions based on readiness scores and quality.</summary>
        private string[] GenerateSuggestions(TribulationConfirmationEvent evt)
        {
            List<string> suggestions = new List<string>();

            // Quality info
            string qualityName = GetQualityDisplayName(evt.Quality);
            suggestions.Add($"天劫台: {qualityName}");

            // Individual dimension suggestions
            if (evt.PillScore < 0.3f)
                suggestions.Add("- 丹药: 准备严重不足 → 建议至少准备筑基丹、凝心丹等丹药");
            else if (evt.PillScore < 0.6f)
                suggestions.Add("- 丹药: 准备不足 → 建议补充丹药（权重25%）");

            if (evt.EquipScore < 0.3f)
                suggestions.Add("- 装备: 准备严重不足 → 建议至少准备抗雷甲、辟邪佩");
            else if (evt.EquipScore < 0.6f)
                suggestions.Add("- 装备: 准备不足 → 建议强化装备（权重30%）");

            if (evt.FormScore < 0.3f)
                suggestions.Add("- 阵法: 准备严重不足 → 建议布置防御阵法");
            else if (evt.FormScore < 0.6f)
                suggestions.Add("- 阵法: 准备不足 → 建议准备阵法（权重20%）");

            if (evt.EscortScore < 0.3f)
                suggestions.Add("- 护法: 准备严重不足 → 建议邀请护法道友");
            else if (evt.EscortScore < 0.6f)
                suggestions.Add("- 护法: 准备不足 → 建议增加护法（权重25%，散修上限3人）");

            // Quality-specific bonuses
            if (evt.Quality == "Ancient")
                suggestions.Add("- 古品天劫台: +10% 成功率加成");
            else if (evt.Quality == "Secret")
                suggestions.Add("- 秘品天劫台: +20% 成功率加成");

            // Dao Body overreach warning
            if (evt.DaoBodyPenalty > 0f)
            {
                suggestions.Add($"- ⚠ 道体品阶低于天劫台: 道体+1级，但失败率 +{evt.DaoBodyPenalty * 100f:F0}%");
            }

            // Scattered cultivator note
            if (TribulationBody.Instance != null && TribulationBody.Instance.IsScatteredCultivator)
            {
                suggestions.Add("- 散修: 道体基础品质+1，护法上限3人");
            }

            if (suggestions.Count <= 1)
            {
                suggestions.Add("- 准备充分，可以尝试渡劫！");
            }

            // Overall readiness suggestion
            if (evt.ReadinessScore < 0.3f)
                suggestions.Add("\n⚠ 综合准备评分过低，强烈建议提升后再尝试");
            else if (evt.ReadinessScore < 0.6f)
                suggestions.Add("\n⚠ 综合准备一般，建议提升至60%以上再尝试");

            return suggestions.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DISCONNECT PROTECTION (断线5分钟保护)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Call this when a network disconnect is detected during tribulation.</summary>
        public void OnDisconnectDetected()
        {
            if (_disconnectProtectionUsedToday)
            {
                Debug.Log("[TribulationUI] Disconnect protection already used today. Tribulation will fail.");
                if (TribulationManager.Instance != null && TribulationManager.Instance.IsTribulationActive)
                {
                    TribulationManager.Instance.EndTribulation(false);
                }
                return;
            }

            if (!TribulationManager.Instance.IsTribulationActive)
                return;

            _isDisconnected = true;
            _disconnectProtectionUsedToday = true;
            _disconnectTimer = DISCONNECT_PROTECTION_DURATION;
            SaveDailyDisconnectState();

            // Save backup state
            SaveBackupState();

            // Show protection overlay
            if (_disconnectOverlay != null)
            {
                _disconnectOverlay.SetActive(true);
            }
            if (_protectionActiveGroup != null) _protectionActiveGroup.SetActive(true);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(false);
            if (_protectionStatusText != null)
                _protectionStatusText.text = "断线保护已激活 — 请在5分钟内重新连接";

            // Start timer
            if (_disconnectTimerCoroutine != null) StopCoroutine(_disconnectTimerCoroutine);
            _disconnectTimerCoroutine = StartCoroutine(DisconnectProtectionTimer());

            // Publish timer event
            EventBus.Publish(new DisconnectProtectionTimerEvent
            {
                IsActive = "true",
                RemainingTime = _disconnectTimer,
                IsExpired = false
            });

            Debug.Log($"[TribulationUI] Disconnect protection activated. {DISCONNECT_PROTECTION_DURATION}s window.");
        }

        /// <summary>Call this when the player reconnects within the protection window.</summary>
        public void OnReconnected()
        {
            if (!_isDisconnected) return;

            _isDisconnected = false;

            if (_disconnectTimerCoroutine != null)
            {
                StopCoroutine(_disconnectTimerCoroutine);
                _disconnectTimerCoroutine = null;
            }

            // Restore backup state
            bool restored = TryRestoreBackupState();

            // Hide protection overlay
            if (_disconnectOverlay != null)
                _disconnectOverlay.SetActive(false);
            if (_protectionActiveGroup != null) _protectionActiveGroup.SetActive(false);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(false);

            if (_protectionStatusText != null)
                _protectionStatusText.text = restored ? "连接已恢复，渡劫继续。" : "状态恢复失败。";

            EventBus.Publish(new DisconnectProtectionTimerEvent
            {
                IsActive = "false",
                RemainingTime = "0f",
                IsExpired = false
            });

            Debug.Log($"[TribulationUI] Reconnected. Restoration: {(restored ? "success" : "failed")}");
        }

        /// <summary>Save the current tribulation state for disconnect recovery.</summary>
        private void SaveBackupState()
        {
            if (TribulationManager.Instance == null) return;

            var backup = new TribulationBackupData
            {
                quality = TribulationManager.Instance.CurrentQuality,
                isActive = TribulationManager.Instance.IsTribulationActive,
                barrierDurability = TribulationManager.Instance.BarrierDurability,
                timestamp = DateTime.Now.Ticks
            };

            string json = JsonUtility.ToJson(backup);
            PlayerPrefs.SetString(TRIBULATION_BACKUP_KEY, json);
            PlayerPrefs.Save();

            Debug.Log("[TribulationUI] Tribulation state backed up for disconnect recovery.");
        }

        /// <summary>Try to restore tribulation state from backup after reconnect.</summary>
        private bool TryRestoreBackupState()
        {
            if (!PlayerPrefs.HasKey(TRIBULATION_BACKUP_KEY))
                return false;

            string json = PlayerPrefs.GetString(TRIBULATION_BACKUP_KEY);
            var backup = JsonUtility.FromJson<TribulationBackupData>(json);

            // Check timestamp — if more than 5 minutes have passed, treat as expired
            long elapsed = DateTime.Now.Ticks - backup.timestamp;
            float elapsedSeconds = (float)new TimeSpan(elapsed).TotalSeconds;

            if (elapsedSeconds > DISCONNECT_PROTECTION_DURATION)
            {
                Debug.Log("[TribulationUI] Backup expired. Cannot restore.");
                PlayerPrefs.DeleteKey(TRIBULATION_BACKUP_KEY);
                return false;
            }

            // Restore — the tribulation is still active in the manager
            // (it wasn't ended), so no explicit restoration needed beyond
            // updating the UI state
            if (backup.isActive && TribulationManager.Instance != null)
            {
                // Re-show appropriate UI panels based on current phase
                if (!_thunderHudPanel.activeSelf &&
                    !_heartDemonPanel.activeSelf &&
                    !_daoQuestioningPanel.activeSelf)
                {
                    // Default to showing thunder HUD as the tribulation continues
                    _thunderHudPanel.SetActive(true);
                }
            }

            // Clear backup
            PlayerPrefs.DeleteKey(TRIBULATION_BACKUP_KEY);

            return true;
        }

        /// <summary>Timer coroutine for disconnect protection countdown.</summary>
        private IEnumerator DisconnectProtectionTimer()
        {
            while (_disconnectTimer > 0f)
            {
                yield return new WaitForSeconds(1f);
                _disconnectTimer -= 1f;

                // Update ring fill
                if (_protectionTimerRing != null)
                {
                    _protectionTimerRing.fillAmount = _disconnectTimer / DISCONNECT_PROTECTION_DURATION;
                }
            }

            // Timer expired — end tribulation as failure
            _isDisconnected = false;
            _disconnectProtectionUsedToday = true;
            SaveDailyDisconnectState();

            if (_protectionActiveGroup != null) _protectionActiveGroup.SetActive(false);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(true);
            if (_protectionStatusText != null)
                _protectionStatusText.text = "<color=#FF6666>保护时间已过，渡劫失败。</color>";

            EventBus.Publish(new DisconnectProtectionTimerEvent
            {
                IsActive = "false",
                RemainingTime = "0f",
                IsExpired = true
            });

            if (TribulationManager.Instance != null && TribulationManager.Instance.IsTribulationActive)
            {
                TribulationManager.Instance.EndTribulation(false);
            }

            // Auto-hide after delay
            yield return new WaitForSeconds(3f);
            if (_disconnectOverlay != null)
                _disconnectOverlay.SetActive(false);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(false);

            Debug.Log("[TribulationUI] Disconnect protection expired. Tribulation failed.");
        }

        /// <summary>Update the disconnect timer text display each frame.</summary>
        private void UpdateDisconnectTimerDisplay()
        {
            if (_protectionTimerText == null) return;

            int minutes = Mathf.FloorToInt(_disconnectTimer / 60f);
            int seconds = Mathf.FloorToInt(_disconnectTimer % 60f);
            _protectionTimerText.text = $"{minutes:00}:{seconds:00}";
        }

        /// <summary>Clear all disconnect protection state.</summary>
        private void ClearDisconnectProtection()
        {
            _isDisconnected = false;
            _disconnectTimer = 0f;

            if (_disconnectTimerCoroutine != null)
            {
                StopCoroutine(_disconnectTimerCoroutine);
                _disconnectTimerCoroutine = null;
            }

            if (_disconnectOverlay != null)
                _disconnectOverlay.SetActive(false);
            if (_protectionActiveGroup != null) _protectionActiveGroup.SetActive(false);
            if (_protectionExpiredGroup != null) _protectionExpiredGroup.SetActive(false);

            // Clear backup
            PlayerPrefs.DeleteKey(TRIBULATION_BACKUP_KEY);
        }

        /// <summary>Load daily disconnect protection usage state.</summary>
        private void LoadDailyDisconnectState()
        {
            string today = DateTime.Now.Date.ToString("yyyy-MM-dd");
            string savedDate = PlayerPrefs.GetString(TRIBULATION_BACKUP_DATE_KEY, "");
            _disconnectProtectionUsedToday = (savedDate == today) &&
                                              PlayerPrefs.GetInt(DISCONNECT_DAILY_KEY, 0) == 1;
        }

        /// <summary>Save daily disconnect protection usage state.</summary>
        private void SaveDailyDisconnectState()
        {
            string today = DateTime.Now.Date.ToString("yyyy-MM-dd");
            PlayerPrefs.SetString(TRIBULATION_BACKUP_DATE_KEY, today);
            PlayerPrefs.SetInt(DISCONNECT_DAILY_KEY, _disconnectProtectionUsedToday ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PVP BARRIER ATTACK (PVP区结界可被攻击)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set the current zone as PVP-capable. When true, other players can
        /// damage the tribulation barrier. Call this from zone detection logic.
        /// </summary>
        public void SetPvPZone(bool isPvP)
        {
            _isPvPZone = isPvP;

            if (_pvpStatusText != null)
                _pvpStatusText.text = isPvP ? "<color=#FF6666>⚔ PVP区域 — 结界可被攻击</color>" : "";

            if (_pvpBarrierIndicator != null)
                _pvpBarrierIndicator.SetActive(isPvP);

            if (_pvpBarrierHealthText != null && !isPvP)
                _pvpBarrierHealthText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Called by other players' attack systems to damage the tribulation barrier
        /// in PVP zones. Respects cooldown to prevent spam.
        /// </summary>
        /// <param name="attackerId">ID of the attacking player.</param>
        /// <param name="damage">Amount of damage to deal (default 5 per hit).</param>
        /// <returns>True if the attack was applied.</returns>
        public bool AttackBarrierInPvP(string attackerId, float damage = 5f)
        {
            if (!_isPvPZone) return false;
            if (TribulationManager.Instance == null || !TribulationManager.Instance.IsTribulationActive)
                return false;

            // Cooldown check
            if (Time.time - _lastBarrierAttackTime < PVP_ATTACK_COOLDOWN)
                return false;

            _lastBarrierAttackTime = Time.time;

            // Apply damage to barrier
            TribulationManager.Instance.DamageBarrier(damage);

            Debug.Log($"[TribulationUI] PVP attack on barrier by {attackerId}: {damage} damage.");

            // Update PVP barrier health display
            if (_pvpBarrierHealthText != null)
            {
                float remaining = TribulationManager.Instance.BarrierDurability;
                float maxVal = TribulationManager.Instance.BarrierMaxDurability;
                _pvpBarrierHealthText.text = $"结界耐久: {remaining:F0}/{maxVal:F0}";
                _pvpBarrierHealthText.gameObject.SetActive(true);
            }

            return true;
        }

        /// <summary>Whether the current zone has PVP-enabled barrier attacks.</summary>
        public bool IsPvPZone => _isPvPZone;

        // ═══════════════════════════════════════════════════════════════════
        //  DISPLAY HELPERS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Parse a quality string to enum.</summary>
        private static TribulationQuality ParseQuality(string quality)
        {
            return quality switch
            {
                "Ancient" => TribulationQuality.Ancient,
                "Secret" => TribulationQuality.Secret,
                _ => TribulationQuality.Normal
            };
        }

        /// <summary>Get Chinese display name for a quality tier.</summary>
        private static string GetQualityDisplayName(string quality)
        {
            return quality switch
            {
                "Normal" => "凡品天劫台",
                "Ancient" => "古品天劫台",
                "Secret" => "秘品天劫台",
                _ => quality
            };
        }

        /// <summary>Get color for a quality tier.</summary>
        private Color GetQualityColor(string quality)
        {
            return quality switch
            {
                "Normal" => _qualityNormalColor,
                "Ancient" => _qualityAncientColor,
                "Secret" => _qualitySecretColor,
                _ => Color.white
            };
        }

        /// <summary>Get color for a score value (good/warn/bad).</summary>
        private Color GetScoreColor(float value)
        {
            return value >= 0.6f ? _scoreGoodColor
                 : value >= 0.3f ? _scoreWarnColor
                 : _scoreBadColor;
        }

        /// <summary>Set a single readiness bar's value, label, and color.</summary>
        private void SetReadinessBar(Slider bar, Text label, Text valueText, string name, float score)
        {
            if (bar != null)
            {
                bar.value = score;
                var fill = bar.fillRect?.GetComponentInChildren<Image>(true);
                if (fill != null) fill.color = GetScoreColor(score);
            }
            if (label != null)
                label.text = $"{name}: {score * 100f:F0}%";
            if (valueText != null)
            {
                valueText.text = $"{score * 100f:F0}%";
                valueText.color = GetScoreColor(score);
            }
        }

        /// <summary>Helper to hide a panel after a delay.</summary>
        private IEnumerator DelayedPanelHide(GameObject panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel != null)
                panel.SetActive(false);
        }

        /// <summary>Set all panel root GameObjects active/inactive.</summary>
        private void SetAllPanelsActive(bool active)
        {
            if (_confirmationPanel != null) _confirmationPanel.SetActive(active);
            if (_thunderHudPanel != null) _thunderHudPanel.SetActive(active);
            if (_heartDemonPanel != null) _heartDemonPanel.SetActive(active);
            if (_daoQuestioningPanel != null) _daoQuestioningPanel.SetActive(active);
            if (_daoBodyPanel != null) _daoBodyPanel.SetActive(active);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BACKUP DATA STRUCT
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Serializable backup data for disconnect protection.</summary>
        [Serializable]
        private struct TribulationBackupData
        {
            public TribulationQuality quality;
            public bool isActive;
            public float barrierDurability;
            public long timestamp;
        }
    }
}
