using System;
using System.Collections;
using System.Collections.Generic;
using EarthOnline.Core;
using EarthOnline.Framework;
using EarthOnline.World;
using UnityEngine;
using UnityEngine.UI;

namespace EarthOnline.UI
{
    #region UI Event Structs

    /// <summary>Published when the crafting panel is opened/closed.</summary>
    public struct CraftingPanelToggleEvent
    {
        public bool IsOpen;
        public bool IsAlchemyMode; // true=炼丹, false=炼器
    }

    /// <summary>Published when a heat button is pressed.</summary>
    public struct HeatButtonPressedEvent
    {
        public HeatLevel Level;
        public bool OnCooldown;
    }

    /// <summary>Published when a material slot is clicked in crafting UI.</summary>
    public struct MaterialSlotClickedEvent
    {
        public int SlotIndex;
        public bool IsOccupied;
        public string MaterialId;
    }

    /// <summary>Published when forge QTE strike button is pressed.</summary>
    public struct ForgeStrikeInputEvent
    {
        public float AppliedForce; // 0~1
    }

    /// <summary>Published when quenching liquid is selected in UI.</summary>
    public struct QuenchingLiquidSelectedEvent
    {
        public QuenchingLiquid LiquidType;
    }

    /// <summary>Published for gathering perception updates.</summary>
    public struct GatheringPerceptionUpdateEvent
    {
        public int NearbyNodes;
        public bool IsInGatheringRange;
    }

    /// <summary>Published when a recipe is selected in the list.</summary>
    public struct RecipeSelectedEvent
    {
        public string RecipeId;
        public string DisplayName;
        public bool IsAlchemyRecipe;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    //  CraftingUI — Combined Alchemy + Forging UI Controller
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 炼制UI界面 (Story 008)
    ///
    /// 炼丹UI:
    ///   - 丹炉面板 (cauldron status, durability)
    ///   - 火候切换按钮 (三档: 大火/中火/小火 + CD指示器)
    ///   - 温度条 (实时温度 + 最佳温度范围)
    ///   - 药液颜色变化 (四阶段颜色过渡)
    ///   - 投料槽 (材料格子 + 顺序提示)
    ///
    /// 炼器UI:
    ///   - 四步进度条 (熔炼→塑形→淬火→开光)
    ///   - 锤击力度指示器 (QTE力度条动画)
    ///   - 淬火液选择面板
    ///   - 灵力注入条
    ///
    /// 额外:
    ///   - 采集感知HUD (灵材光点 + 边界范围可视化)
    ///   - 配方界面 (列表/搜索/收藏/自创配方标记)
    ///   - 熟练度显示 (等级+称号+进度条)
    ///   - 设备耐久度条 + 维修按钮
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        #region Constants

        private const float STRIKE_FORCE_ANIMATION_SPEED = 2f;    // 力度指示器摆动速度
        private const float MAX_STRIKE_HOLD_TIME = 2f;             // 最大蓄力时间
        private const float TEMP_BAR_LERP_SPEED = 5f;              // 温度条平滑速度
        private const float LIQUID_COLOR_LERP_SPEED = 3f;          // 药液颜色变化速度
        private const float HEAT_BUTTON_CD_RADIUS = 360f;          // CD圆形遮罩角度
        private const int MAX_INGREDIENT_SLOTS = 8;                // 最大投料槽数

        #endregion

        #region Panel References

        [Header("面板根对象")]
        [SerializeField] private GameObject _craftingPanel;         // 主面板
        [SerializeField] private GameObject _alchemyPanel;          // 炼丹子面板
        [SerializeField] private GameObject _forgePanel;            // 炼器子面板
        [SerializeField] private GameObject _gatheringHud;          // 采集感知HUD
        [SerializeField] private GameObject _recipePanel;           // 配方界面
        [SerializeField] private GameObject _proficiencyPanel;      // 熟练度面板

        [Header("模式切换")]
        [SerializeField] private Button _alchemyModeButton;
        [SerializeField] private Button _forgeModeButton;
        [SerializeField] private Button _recipeToggleButton;
        [SerializeField] private Button _closeButton;

        #endregion

        #region Alchemy UI — 丹炉面板

        [Header("炼丹 → 丹炉面板")]
        [SerializeField] private Image _cauldronDurabilityFill;
        [SerializeField] private Text _cauldronDurabilityText;
        [SerializeField] private Text _cauldronNameText;
        [SerializeField] private Button _repairCauldronButton;
        [SerializeField] private Text _repairCostText;

        [Header("炼丹 → 火候按钮 (三档)")]
        [SerializeField] private Button _highHeatButton;            // 大火
        [SerializeField] private Button _mediumHeatButton;          // 中火
        [SerializeField] private Button _lowHeatButton;            // 小火
        [SerializeField] private Image _highHeatCdFill;
        [SerializeField] private Image _mediumHeatCdFill;
        [SerializeField] private Image _lowHeatCdFill;
        [SerializeField] private Color _highHeatActiveColor = new Color(1f, 0.3f, 0.1f);
        [SerializeField] private Color _highHeatInactiveColor = new Color(0.6f, 0.2f, 0.1f);
        [SerializeField] private Color _mediumHeatActiveColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] private Color _mediumHeatInactiveColor = new Color(0.6f, 0.4f, 0.1f);
        [SerializeField] private Color _lowHeatActiveColor = new Color(0.3f, 0.6f, 1.0f);
        [SerializeField] private Color _lowHeatInactiveColor = new Color(0.2f, 0.3f, 0.6f);

        [Header("炼丹 → 温度条")]
        [SerializeField] private Image _temperatureFillBar;
        [SerializeField] private Text _temperatureText;
        [SerializeField] private RectTransform _optimalTempIndicator;
        [SerializeField] private Image _optimalRangeHighlight;

        [Header("炼丹 → 药液颜色")]
        [SerializeField] private Image _cauldronLiquidImage;        // 丹炉中液体颜色显示
        [SerializeField] private Color _boilingColor = new Color(0.3f, 0.5f, 0.2f);     // 沸腾期 — 青绿
        [SerializeField] private Color _fusionColor = new Color(0.5f, 0.3f, 0.6f);       // 融合期 — 紫
        [SerializeField] private Color _purificationColor = new Color(0.2f, 0.7f, 0.8f); // 提纯期 — 蓝
        [SerializeField] private Color _finishingColor = new Color(1.0f, 0.8f, 0.2f);    // 收丹期 — 金黄
        [SerializeField] private Color _idleLiquidColor = new Color(0.4f, 0.4f, 0.4f);   // 待机 — 灰

        [Header("炼丹 → 投料槽")]
        [SerializeField] private GameObject _ingredientSlotPrefab;
        [SerializeField] private Transform _ingredientSlotContainer;
        [SerializeField] private Text _orderHintText;
        [SerializeField] private Button _addIngredientButton;

        [Header("炼丹 → 阶段进度")]
        [SerializeField] private Image _craftProgressFill;
        [SerializeField] private Text _stageNameText;
        [SerializeField] private Text _progressText;

        #endregion

        #region Forge UI — 炼器面板

        [Header("炼器 → 四步进度条")]
        [SerializeField] private Image[] _stageProgressBars;        // [0]=熔炼, [1]=塑形, [2]=淬火, [3]=开光
        [SerializeField] private Text[] _stageNameLabels;           // 各阶段名
        [SerializeField] private Image[] _stageCheckmarks;          // 完成勾选

        [Header("炼器 → 锤击QTE")]
        [SerializeField] private RectTransform _forceIndicatorPivot; // 力度指示器指针
        [SerializeField] private Image _targetForceZone;             // 目标力道范围指示
        [SerializeField] private Image _forceBarFill;                // 力度蓄力条
        [SerializeField] private Text _strikeCountText;              // "第X/5锤"
        [SerializeField] private Button _strikeButton;               // 锤击按钮
        [SerializeField] private Text _strikeFeedbackText;           // "完美!" / "好!" / "偏了"

        [Header("炼器 → 淬火液选择")]
        [SerializeField] private Button _spiritSpringButton;         // 灵泉
        [SerializeField] private Button _beastBloodButton;           // 妖兽血
        [SerializeField] private Image _quenchLiquidPreview;         // 淬火液颜色预览

        [Header("炼器 → 灵力注入条")]
        [SerializeField] private Image _spiritualPowerFill;
        [SerializeField] private Image _affinityFill;
        [SerializeField] private Button _injectPowerButton;          // 按住注入灵力
        [SerializeField] private Text _affinityText;

        #endregion

        #region Gathering HUD — 采集感知

        [Header("采集感知HUD")]
        [SerializeField] private Image _resourceRadarIcon;           // 灵材光点指示
        [SerializeField] private Text _gatheringHintText;
        [SerializeField] private RectTransform _miniRadar;           // 迷你雷达
        [SerializeField] private GameObject _resourceNodeIndicatorPrefab;
        [SerializeField] private float _maxDetectionRange = 50f;
        [SerializeField] private Color _rareResourceColor = Color.magenta;

        #endregion

        #region Recipe Panel — 配方界面

        [Header("配方界面")]
        [SerializeField] private RectTransform _recipeListContainer;
        [SerializeField] private GameObject _recipeItemPrefab;
        [SerializeField] private InputField _recipeSearchInput;
        [SerializeField] private Button _recipeSortButton;
        [SerializeField] private Toggle _showFavoritesOnlyToggle;
        [SerializeField] private Toggle _showCustomOnlyToggle;
        [SerializeField] private Text _recipeDetailText;

        #endregion

        #region Proficiency Display — 熟练度

        [Header("熟练度显示")]
        [SerializeField] private Text _proficiencyLevelText;
        [SerializeField] private Text _proficiencyTitleText;
        [SerializeField] private Image _proficiencyExpFill;
        [SerializeField] private Text _proficiencyExpText;

        #endregion

        #region Private State

        // ─── Mode ───
        private bool _isOpen;
        private bool _isAlchemyMode = true;

        // ─── Heat button images ───
        private Image _highHeatImage;
        private Image _mediumHeatImage;
        private Image _lowHeatImage;

        // ─── Heat button cooldown tracking ───
        private bool _highHeatCooldown;
        private bool _mediumHeatCooldown;
        private bool _lowHeatCooldown;

        // ─── Temperature bar ───
        private float _displayedTemperature;

        // ─── Liquid color ───
        private Color _currentLiquidColor;
        private Color _targetLiquidColor;

        // ─── Ingredient slots ───
        private List<CraftingIngredientSlot> _ingredientSlots = new List<CraftingIngredientSlot>();

        // ─── Forge QTE ───
        private bool _isChargingStrike;
        private float _strikeChargeTime;
        private float _currentForceValue;
        private Coroutine _forceAnimCoroutine;

        // ─── Spiritual power injection ───
        private bool _isInjecting;
        private float _injectAccumulator;

        // ─── Event subscriptions ───
        private Action<AlchemyProgressEvent> _onAlchemyProgress;
        private Action<AlchemyStageChangedEvent> _onAlchemyStageChanged;
        private Action<TemperatureChangedEvent> _onTemperatureChanged;
        private Action<HeatSwitchedEvent> _onHeatSwitched;
        private Action<AlchemyCompletedEvent> _onAlchemyCompleted;
        private Action<AlchemyExplodedEvent> _onAlchemyExploded;
        private Action<MaterialInputEvent> _onMaterialInput;
        private Action<AlchemyProficiencyChangedEvent> _onAlchemyProfChanged;

        private Action<ForgeStageChangedEvent> _onForgeStageChanged;
        private Action<SmeltingTemperatureEvent> _onSmeltingTempChanged;
        private Action<ShapingStrikeEvent> _onShapingStrike;
        private Action<EnlighteningProgressEvent> _onEnlighteningProgress;
        private Action<ForgeCompletedEvent> _onForgeCompleted;
        private Action<ForgeProficiencyChangedEvent> _onForgeProfChanged;

        private Action<CauldronData> _onCauldronChanged;

        #endregion

        #region Helper Class

        /// <summary>单个投料槽UI数据</summary>
        private class CraftingIngredientSlot
        {
            public GameObject GameObject;
            public Image IconImage;
            public Text NameText;
            public int SlotIndex;
            public bool IsOccupied;
            public string MaterialId;
            public Button Button;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CacheHeatButtonImages();
            InitializeIngredientSlots();
            SubscribeToEvents();

            if (_craftingPanel != null)
                _craftingPanel.SetActive(false);
        }

        private void Start()
        {
            SetupButtonListeners();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (_isAlchemyMode)
            {
                UpdateAlchemyUI();
                UpdateHeatCooldownVisuals();
            }
            else
            {
                UpdateForgeUI();
            }

            UpdateProficiencyPanel();
        }

        #endregion

        #region Initialization

        /// <summary>Cache references to heat button Image components.</summary>
        private void CacheHeatButtonImages()
        {
            if (_highHeatButton != null) _highHeatImage = _highHeatButton.GetComponent<Image>();
            if (_mediumHeatButton != null) _mediumHeatImage = _mediumHeatButton.GetComponent<Image>();
            if (_lowHeatButton != null) _lowHeatImage = _lowHeatButton.GetComponent<Image>();
        }

        /// <summary>Initialize ingredient slots.</summary>
        private void InitializeIngredientSlots()
        {
            if (_ingredientSlotPrefab == null || _ingredientSlotContainer == null) return;

            for (int i = 0; i < MAX_INGREDIENT_SLOTS; i++)
            {
                GameObject slotGO = Instantiate(_ingredientSlotPrefab, _ingredientSlotContainer);
                slotGO.name = $"IngredientSlot_{i}";

                var slot = new CraftingIngredientSlot
                {
                    GameObject = slotGO,
                    IconImage = slotGO.GetComponentInChildren<Image>(),
                    NameText = slotGO.GetComponentInChildren<Text>(),
                    SlotIndex = i,
                    IsOccupied = "false",
                    MaterialId = null,
                    Button = slotGO.GetComponent<Button>()
                };

                int capturedIndex = i;
                if (slot.Button != null)
                {
                    slot.Button.onClick.AddListener(() => OnIngredientSlotClicked(capturedIndex));
                }

                _ingredientSlots.Add(slot);
                slotGO.SetActive(false);
            }
        }

        /// <summary>Setup button click listeners.</summary>
        private void SetupButtonListeners()
        {
            if (_alchemyModeButton != null)
                _alchemyModeButton.onClick.AddListener(() => SwitchMode(true));

            if (_forgeModeButton != null)
                _forgeModeButton.onClick.AddListener(() => SwitchMode(false));

            if (_closeButton != null)
                _closeButton.onClick.AddListener(ClosePanel);

            if (_highHeatButton != null)
                _highHeatButton.onClick.AddListener(() => OnHeatButtonClicked(HeatLevel.High));

            if (_mediumHeatButton != null)
                _mediumHeatButton.onClick.AddListener(() => OnHeatButtonClicked(HeatLevel.Medium));

            if (_lowHeatButton != null)
                _lowHeatButton.onClick.AddListener(() => OnHeatButtonClicked(HeatLevel.Low));

            if (_recipeToggleButton != null)
                _recipeToggleButton.onClick.AddListener(ToggleRecipePanel);

            if (_strikeButton != null)
            {
                _strikeButton.onClick.AddListener(OnStrikeButtonPressed);
                // Also support hold.
            }

            if (_spiritSpringButton != null)
                _spiritSpringButton.onClick.AddListener(() => OnQuenchingSelected(QuenchingLiquid.SpiritSpring));

            if (_beastBloodButton != null)
                _beastBloodButton.onClick.AddListener(() => OnQuenchingSelected(QuenchingLiquid.BeastBlood));

            if (_injectPowerButton != null)
            {
                // For simplicity, click-based injection (can be changed to hold).
                _injectPowerButton.onClick.AddListener(OnInjectPowerPressed);
            }

            if (_recipeSearchInput != null)
            {
                _recipeSearchInput.onValueChanged.AddListener(OnRecipeSearchChanged);
            }

            if (_repairCauldronButton != null)
                _repairCauldronButton.onClick.AddListener(OnRepairCauldronClicked);

            if (_addIngredientButton != null)
                _addIngredientButton.onClick.AddListener(OnAddIngredientClicked);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  PANEL OPEN / CLOSE
        // ═══════════════════════════════════════════════════════════════════

        #region Panel Open/Close

        /// <summary>Open the crafting panel in alchemy or forge mode.</summary>
        public void OpenPanel(bool alchemyMode)
        {
            if (_craftingPanel == null) return;

            _isOpen = true;
            _isAlchemyMode = alchemyMode;
            _craftingPanel.SetActive(true);

            _alchemyPanel?.SetActive(alchemyMode);
            _forgePanel?.SetActive(!alchemyMode);
            _gatheringHud?.SetActive(true);   // gathering HUD always visible during crafting
            _proficiencyPanel?.SetActive(true);
            _recipePanel?.SetActive(false);

            ResetUIState();

            EventBus.Publish(new CraftingPanelToggleEvent
            {
                IsOpen = "true",
                IsAlchemyMode = alchemyMode
            });

            Debug.Log($"[CraftingUI] 打开{(alchemyMode ? "炼丹" : "炼器")}面板");
        }

        /// <summary>Close the crafting panel.</summary>
        public void ClosePanel()
        {
            if (_craftingPanel == null) return;

            _isOpen = false;
            _craftingPanel.SetActive(false);
            _gatheringHud?.SetActive(false);

            EventBus.Publish(new CraftingPanelToggleEvent
            {
                IsOpen = "false",
                IsAlchemyMode = _isAlchemyMode
            });
        }

        /// <summary>Toggle open/close.</summary>
        public void TogglePanel(bool alchemyMode)
        {
            if (_isOpen)
                ClosePanel();
            else
                OpenPanel(alchemyMode);
        }

        /// <summary>Switch between alchemy and forge mode.</summary>
        private void SwitchMode(bool toAlchemy)
        {
            if (_isAlchemyMode == toAlchemy) return;

            _isAlchemyMode = toAlchemy;
            _alchemyPanel?.SetActive(toAlchemy);
            _forgePanel?.SetActive(!toAlchemy);

            ResetUIState();
        }

        /// <summary>Reset all dynamic UI elements.</summary>
        private void ResetUIState()
        {
            _displayedTemperature = 25f;
            _currentLiquidColor = _idleLiquidColor;
            _targetLiquidColor = _idleLiquidColor;

            if (_cauldronLiquidImage != null)
                _cauldronLiquidImage.color = _idleLiquidColor;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  ALCHEMY UI UPDATE
        // ═══════════════════════════════════════════════════════════════════

        #region Alchemy UI Update

        /// <summary>Update alchemy UI elements each frame.</summary>
        private void UpdateAlchemyUI()
        {
            if (!_isAlchemyMode) return;

            AlchemyController ctrl = AlchemyController.Instance;
            if (ctrl == null) return;

            // Temperature bar smoothing
            _displayedTemperature = Mathf.Lerp(
                _displayedTemperature, ctrl.CurrentTemperature,
                TEMP_BAR_LERP_SPEED * Time.deltaTime);

            if (_temperatureFillBar != null)
            {
                float fill = Mathf.Clamp01(_displayedTemperature / 500f);
                _temperatureFillBar.fillAmount = fill;
            }

            if (_temperatureText != null)
            {
                _temperatureText.text = $"{_displayedTemperature:F0}°C" +
                    (ctrl.IsCrafting
                        ? $" / {ctrl.CurrentRecipe.OptimalTemperature:F0}°C"
                        : "");
            }

            // Optimal temperature indicator position
            if (_optimalTempIndicator != null && ctrl.IsCrafting)
            {
                float optimalPos = Mathf.Clamp01(ctrl.CurrentRecipe.OptimalTemperature / 500f);
                _optimalTempIndicator.anchorMin = new Vector2(optimalPos, 0f);
                _optimalTempIndicator.anchorMax = new Vector2(optimalPos, 1f);
            }

            // Stage name & progress
            if (_stageNameText != null)
            {
                _stageNameText.text = AlchemyController.GetStageDisplayName(ctrl.CurrentStage);
            }

            if (_craftProgressFill != null)
            {
                _craftProgressFill.fillAmount = ctrl.Progress;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{ctrl.Progress * 100:F1}%";
            }

            // Cauldron durability
            UpdateCauldronUI(ctrl);

            // Liquid color transition
            UpdateLiquidColor(ctrl);

            // Heat button active states
            UpdateHeatButtonVisuals(ctrl.CurrentHeat);

            // Ingredient slots
            UpdateIngredientSlots(ctrl);
        }

        /// <summary>Update cauldron durability bar and repair button.</summary>
        private void UpdateCauldronUI(AlchemyController ctrl)
        {
            if (ctrl.ActiveCauldron.DisplayName == null) return;

            if (_cauldronNameText != null)
                _cauldronNameText.text = ctrl.ActiveCauldron.DisplayName;

            if (_cauldronDurabilityFill != null)
            {
                float fill = ctrl.ActiveCauldron.MaxDurability > 0
                    ? ctrl.ActiveCauldron.CurrentDurability / ctrl.ActiveCauldron.MaxDurability
                    : 1f;
                _cauldronDurabilityFill.fillAmount = fill;
                _cauldronDurabilityFill.color = fill > 0.5f ? Color.green
                    : fill > 0.25f ? Color.yellow : Color.red;
            }

            if (_cauldronDurabilityText != null)
            {
                _cauldronDurabilityText.text =
                    $"{ctrl.ActiveCauldron.CurrentDurability:F0}/{ctrl.ActiveCauldron.MaxDurability:F0}";
            }

            if (_repairCauldronButton != null)
            {
                bool needsRepair = ctrl.ActiveCauldron.CurrentDurability < ctrl.ActiveCauldron.MaxDurability;
                _repairCauldronButton.gameObject.SetActive(needsRepair);
            }
        }

        /// <summary>Update the liquid color to reflect current alchemy stage.</summary>
        private void UpdateLiquidColor(AlchemyController ctrl)
        {
            _targetLiquidColor = ctrl.CurrentStage switch
            {
                AlchemyStage.Boiling      => _boilingColor,
                AlchemyStage.Fusion       => _fusionColor,
                AlchemyStage.Purification => _purificationColor,
                AlchemyStage.Finishing    => _finishingColor,
                AlchemyStage.Complete     => ctrl.LastResult.QualityColor,
                AlchemyStage.Exploded     => new Color(0.3f, 0.1f, 0.05f), // 焦黑
                _                         => _idleLiquidColor
            };

            _currentLiquidColor = Color.Lerp(
                _currentLiquidColor, _targetLiquidColor,
                LIQUID_COLOR_LERP_SPEED * Time.deltaTime);

            if (_cauldronLiquidImage != null)
            {
                _cauldronLiquidImage.color = _currentLiquidColor;
            }
        }

        /// <summary>Update heat button visual feedback (color + cooldown).</summary>
        private void UpdateHeatButtonVisuals(HeatLevel currentHeat)
        {
            SetHeatButtonState(_highHeatImage, _highHeatActiveColor, _highHeatInactiveColor,
                currentHeat == HeatLevel.High);
            SetHeatButtonState(_mediumHeatImage, _mediumHeatActiveColor, _mediumHeatInactiveColor,
                currentHeat == HeatLevel.Medium);
            SetHeatButtonState(_lowHeatImage, _lowHeatActiveColor, _lowHeatInactiveColor,
                currentHeat == HeatLevel.Low);
        }

        private void SetHeatButtonState(Image btnImage, Color activeColor, Color inactiveColor, bool isActive)
        {
            if (btnImage == null) return;
            btnImage.color = isActive ? activeColor : inactiveColor;
        }

        /// <summary>Update heat button cooldown overlays.</summary>
        private void UpdateHeatCooldownVisuals()
        {
            AlchemyController ctrl = AlchemyController.Instance;
            if (ctrl == null) return;

            float cdProgress = ctrl.HeatCooldownProgress;

            UpdateCdFill(_highHeatCdFill, cdProgress);
            UpdateCdFill(_mediumHeatCdFill, cdProgress);
            UpdateCdFill(_lowHeatCdFill, cdProgress);
        }

        private void UpdateCdFill(Image cdFill, float progress)
        {
            if (cdFill == null) return;
            cdFill.fillAmount = 1f - progress;
            cdFill.gameObject.SetActive(progress < 1f);
        }

        /// <summary>Update ingredient slot display.</summary>
        private void UpdateIngredientSlots(AlchemyController ctrl)
        {
            for (int i = 0; i < _ingredientSlots.Count; i++)
            {
                var slot = _ingredientSlots[i];

                if (i < ctrl.InputMaterialCount)
                {
                    // This would need access to the actual material list.
                    // For now, show occupied state.
                    slot.GameObject.SetActive(true);
                    slot.IsOccupied = true;
                    if (slot.NameText != null)
                        slot.NameText.text = $"材料 {i + 1}";
                }
                else
                {
                    bool isNextSlot = i == ctrl.InputMaterialCount && ctrl.IsCrafting && !ctrl.AllMaterialsInput;
                    slot.GameObject.SetActive(isNextSlot);
                    slot.IsOccupied = false;

                    if (isNextSlot && slot.NameText != null)
                    {
                        string[] order = ctrl.CurrentRecipe.RecommendedOrder;
                        if (order != null && i < order.Length)
                        {
                            string hintText = i < order.Length
                                ? $"放入: {order[i]}"
                                : "可选材料";
                            if (slot.NameText != null)
                                slot.NameText.text = hintText;

                            if (_orderHintText != null && i == ctrl.InputMaterialCount)
                                _orderHintText.text = $"{hintText}";
                        }
                    }
                }
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  ALCHEMY — INPUT HANDLERS
        // ═══════════════════════════════════════════════════════════════════

        #region Alchemy Input Handlers

        /// <summary>Called when a heat button is clicked.</summary>
        private void OnHeatButtonClicked(HeatLevel targetHeat)
        {
            AlchemyController ctrl = AlchemyController.Instance;
            if (ctrl == null) return;

            bool applied = ctrl.SwitchHeat(targetHeat);

            EventBus.Publish(new HeatButtonPressedEvent
            {
                Level = targetHeat,
                OnCooldown = !applied
            });
        }

        /// <summary>Called when an ingredient slot is clicked.</summary>
        private void OnIngredientSlotClicked(int slotIndex)
        {
            // Open material selection panel (integration with inventory system).
            EventBus.Publish(new MaterialSlotClickedEvent
            {
                SlotIndex = slotIndex,
                IsOccupied = _ingredientSlots[slotIndex].IsOccupied,
                MaterialId = _ingredientSlots[slotIndex].MaterialId
            });

            Debug.Log($"[CraftingUI] 投料槽 #{slotIndex + 1} 点击");
        }

        /// <summary>Called when add ingredient button is pressed.</summary>
        private void OnAddIngredientClicked()
        {
            AlchemyController ctrl = AlchemyController.Instance;
            if (ctrl == null || !ctrl.IsCrafting || ctrl.AllMaterialsInput) return;

            // Placeholder: prompt inventory system for material selection.
            AlchemyMaterialInput testMaterial = new AlchemyMaterialInput
            {
                ItemId = "mat_herb_01",
                DisplayName = "灵草",
                QualityCoefficient = "0.85f",
                InputOrderIndex = ctrl.InputMaterialCount
            };

            ctrl.InputMaterial(testMaterial);
        }

        /// <summary>Called when repair cauldron button is clicked.</summary>
        private void OnRepairCauldronClicked()
        {
            AlchemyController ctrl = AlchemyController.Instance;
            if (ctrl == null) return;

            float repairAmount = ctrl.ActiveCauldron.MaxDurability - ctrl.ActiveCauldron.CurrentDurability;
            if (repairAmount > 0f)
            {
                ctrl.RepairCauldron(repairAmount);
                Debug.Log($"[CraftingUI] 丹炉已维修: +{repairAmount:F0}耐久");
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  FORGE UI UPDATE
        // ═══════════════════════════════════════════════════════════════════

        #region Forge UI Update

        /// <summary>Update forge UI elements each frame.</summary>
        private void UpdateForgeUI()
        {
            ForgeController ctrl = ForgeController.Instance;
            if (ctrl == null) return;

            // Update stage progress bars.
            UpdateForgeStageBars(ctrl);

            // Update QTE force indicator.
            UpdateForceIndicator(ctrl);

            // Update spiritual power injection.
            UpdateSpiritualPowerUI(ctrl);
        }

        /// <summary>Update the four stage progress bars with color coding.</summary>
        private void UpdateForgeStageBars(ForgeController ctrl)
        {
            ForgeStage[] stages = { ForgeStage.Smelting, ForgeStage.Shaping,
                                    ForgeStage.Quenching, ForgeStage.Enlightening };

            for (int i = 0; i < Mathf.Min(stages.Length, _stageProgressBars.Length); i++)
            {
                if (_stageProgressBars[i] == null) continue;

                bool isCurrentStage = ctrl.CurrentStage == stages[i];
                bool isPastStage = GetStageIndex(ctrl.CurrentStage) > i;
                bool isCompleted = ctrl.IsCompleted;

                float fill = 0f;
                if (isCurrentStage)
                {
                    fill = ctrl.StageProgress;
                }
                else if (isPastStage || isCompleted)
                {
                    fill = 1f;
                }

                _stageProgressBars[i].fillAmount = fill;
                _stageProgressBars[i].color = isPastStage || (isCompleted && fill >= 1f)
                    ? Color.green : isCurrentStage ? Color.yellow : Color.gray;

                // Checkmark
                if (i < _stageCheckmarks.Length && _stageCheckmarks[i] != null)
                {
                    _stageCheckmarks[i].gameObject.SetActive(isPastStage || (isCompleted && fill >= 1f));
                }

                // Label
                if (i < _stageNameLabels.Length && _stageNameLabels[i] != null)
                {
                    if (isCurrentStage)
                        _stageNameLabels[i].text = $"< {ForgeController.GetStageDisplayName(stages[i])} >";
                    else
                        _stageNameLabels[i].text = ForgeController.GetStageDisplayName(stages[i]);
                }
            }
        }

        /// <summary>Get numeric index of a forge stage.</summary>
        private int GetStageIndex(ForgeStage stage)
        {
            return stage switch
            {
                ForgeStage.Idle          => -1,
                ForgeStage.Smelting      => 0,
                ForgeStage.Shaping       => 1,
                ForgeStage.Quenching     => 2,
                ForgeStage.Enlightening  => 3,
                ForgeStage.Complete      => 4,
                ForgeStage.Failed        => 4,
                _                        => -1
            };
        }

        /// <summary>Update the hammer strike force indicator QTE bar.</summary>
        private void UpdateForceIndicator(ForgeController ctrl)
        {
            if (ctrl.CurrentStage != ForgeStage.Shaping) return;

            if (_strikeCountText != null)
            {
                _strikeCountText.text = $"第 {ctrl.CurrentStrike + 1}/{ctrl.TotalStrikes} 锤";
            }

            // Target force zone visual.
            if (_targetForceZone != null)
            {
                float target = ctrl.CurrentTargetForce;
                float range = 0.15f; // visual range
                _targetForceZone.rectTransform.anchorMin = new Vector2(
                    Mathf.Clamp01(target - range), 0f);
                _targetForceZone.rectTransform.anchorMax = new Vector2(
                    Mathf.Clamp01(target + range), 1f);
            }
        }

        /// <summary>Toggle the force animation for QTE.</summary>
        private IEnumerator ForceBarAnimation()
        {
            float time = 0f;
            while (!_isChargingStrike && time < MAX_STRIKE_HOLD_TIME)
            {
                // Oscillating force indicator.
                _currentForceValue = (Mathf.Sin(time * STRIKE_FORCE_ANIMATION_SPEED) + 1f) * 0.5f;

                if (_forceIndicatorPivot != null)
                {
                    _forceIndicatorPivot.anchorMin = new Vector2(_currentForceValue, 0f);
                    _forceIndicatorPivot.anchorMax = new Vector2(_currentForceValue, 1f);
                }

                if (_forceBarFill != null)
                {
                    _forceBarFill.fillAmount = _currentForceValue;
                }

                time += Time.deltaTime;
                yield return null;
            }

            _forceAnimCoroutine = null;
        }

        /// <summary>Update spiritual power injection UI.</summary>
        private void UpdateSpiritualPowerUI(ForgeController ctrl)
        {
            if (ctrl.CurrentStage != ForgeStage.Enlightening) return;

            if (_spiritualPowerFill != null)
            {
                _spiritualPowerFill.fillAmount = Mathf.Clamp01(ctrl.InjectedPower / ctrl.MaxSpiritualPower);
            }

            if (_affinityFill != null)
            {
                _affinityFill.fillAmount = ctrl.CurrentAffinity;
            }

            if (_affinityText != null)
            {
                _affinityText.text = $"亲和度: {ctrl.CurrentAffinity * 100:F1}%";
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  FORGE — INPUT HANDLERS
        // ═══════════════════════════════════════════════════════════════════

        #region Forge Input Handlers

        /// <summary>Called when the player presses the strike button.</summary>
        private void OnStrikeButtonPressed()
        {
            ForgeController ctrl = ForgeController.Instance;
            if (ctrl == null || ctrl.CurrentStage != ForgeStage.Shaping) return;

            // Use the current force value from the animation.
            float appliedForce = _currentForceValue;
            float accuracy = ctrl.PerformStrike(appliedForce);

            // Show feedback.
            if (_strikeFeedbackText != null)
            {
                string feedback = accuracy >= 0.95f ? "完美!" :
                    accuracy >= 0.7f ? "不错!" :
                    accuracy >= 0.4f ? "还行" : "偏了!";
                _strikeFeedbackText.text = feedback;

                _strikeFeedbackText.color = accuracy >= 0.95f ? Color.yellow :
                    accuracy >= 0.7f ? Color.green :
                    accuracy >= 0.4f ? Color.white : Color.red;

                // Auto-hide feedback after a short delay.
                if (gameObject.activeInHierarchy)
                    StartCoroutine(ClearFeedbackDelayed());
            }

            EventBus.Publish(new ForgeStrikeInputEvent
            {
                AppliedForce = appliedForce
            });
        }

        private IEnumerator ClearFeedbackDelayed()
        {
            yield return new WaitForSeconds(1.5f);
            if (_strikeFeedbackText != null)
                _strikeFeedbackText.text = "";
        }

        /// <summary>Called when a quenching liquid is selected.</summary>
        private void OnQuenchingSelected(QuenchingLiquid liquidType)
        {
            ForgeController ctrl = ForgeController.Instance;
            if (ctrl == null) return;

            bool success = ctrl.SelectQuenchingLiquid(liquidType);
            if (success && _quenchLiquidPreview != null)
            {
                if (ForgeController.QuenchingLiquidDataMap.TryGetValue(liquidType, out var data))
                {
                    _quenchLiquidPreview.color = data.LiquidColor;
                }
            }

            EventBus.Publish(new QuenchingLiquidSelectedEvent
            {
                LiquidType = liquidType
            });
        }

        /// <summary>Called when the player presses the inject power button.</summary>
        private void OnInjectPowerPressed()
        {
            ForgeController ctrl = ForgeController.Instance;
            if (ctrl == null || ctrl.CurrentStage != ForgeStage.Enlightening) return;

            // Inject a fixed amount per press.
            float injected = ctrl.InjectSpiritualPower(15f);
            Debug.Log($"[CraftingUI] 灵力注入: {injected:F1}% 亲和度");
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  RECIPE PANEL
        // ═══════════════════════════════════════════════════════════════════

        #region Recipe Panel

        /// <summary>Toggle the recipe panel.</summary>
        private void ToggleRecipePanel()
        {
            if (_recipePanel == null) return;
            bool isActive = !_recipePanel.activeSelf;
            _recipePanel.SetActive(isActive);

            if (isActive)
            {
                RefreshRecipeList();
            }
        }

        /// <summary>Refresh the recipe list (placeholder).</summary>
        private void RefreshRecipeList()
        {
            // Clear existing items.
            if (_recipeListContainer == null) return;

            foreach (Transform child in _recipeListContainer)
            {
                Destroy(child.gameObject);
            }

            // Placeholder — would load from recipe database.
            // Add sample recipe entries for now.
            AddRecipeListItem("聚气丹", "炼丹", true, false);
            AddRecipeListItem("筑基丹", "炼丹", false, false);
            AddRecipeListItem("青锋剑", "炼器", true, true);

            if (_recipeDetailText != null)
            {
                _recipeDetailText.text = "选择一个配方查看详情";
            }
        }

        /// <summary>Add a single recipe to the list.</summary>
        private void AddRecipeListItem(string name, string type, bool isFavorite, bool isCustom)
        {
            if (_recipeItemPrefab == null || _recipeListContainer == null) return;

            GameObject item = Instantiate(_recipeItemPrefab, _recipeListContainer);
            Text textComp = item.GetComponentInChildren<Text>();
            if (textComp != null)
            {
                string fav = isFavorite ? " ★" : "";
                string custom = isCustom ? " [自创]" : "";
                textComp.text = $"[{type}] {name}{fav}{custom}";
            }

            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                string capturedName = name;
                string capturedType = type;
                btn.onClick.AddListener(() =>
                {
                    SelectRecipe(capturedName, capturedType == "炼丹");
                });
            }
        }

        /// <summary>Handle recipe selection.</summary>
        private void SelectRecipe(string displayName, bool isAlchemy)
        {
            if (_recipeDetailText != null)
            {
                _recipeDetailText.text = $"已选择: {displayName}\n" +
                    $"{(!isAlchemy ? "炼器" : "炼丹")}配方\n" +
                    "材料需求: xxx\n" +
                    "难度: ★★☆☆☆";
            }

            EventBus.Publish(new RecipeSelectedEvent
            {
                RecipeId = displayName,
                DisplayName = displayName,
                IsAlchemyRecipe = isAlchemy
            });
        }

        /// <summary>Handle recipe search input.</summary>
        private void OnRecipeSearchChanged(string searchText)
        {
            // Filter recipe list.
            if (_recipeListContainer == null) return;

            foreach (Transform child in _recipeListContainer)
            {
                Text textComp = child.GetComponentInChildren<Text>();
                if (textComp != null)
                {
                    bool visible = string.IsNullOrEmpty(searchText) ||
                                   textComp.text.Contains(searchText);
                    child.gameObject.SetActive(visible);
                }
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  PROFICIENCY PANEL
        // ═══════════════════════════════════════════════════════════════════

        #region Proficiency Display

        /// <summary>Update proficiency display for active crafting mode.</summary>
        private void UpdateProficiencyPanel()
        {
            if (_proficiencyPanel == null || !_proficiencyPanel.activeSelf) return;

            if (_isAlchemyMode)
            {
                AlchemyController ctrl = AlchemyController.Instance;
                if (ctrl?.Proficiency == null) return;
                UpdateProficiencyDisplay(
                    ctrl.Proficiency.Level,
                    ctrl.Proficiency.GetTitle(),
                    ctrl.Proficiency.CurrentExp,
                    ctrl.Proficiency.ExpToNext);
            }
            else
            {
                ForgeController ctrl = ForgeController.Instance;
                if (ctrl?.Proficiency == null) return;
                UpdateProficiencyDisplay(
                    ctrl.Proficiency.Level,
                    ctrl.Proficiency.GetTitle(),
                    ctrl.Proficiency.CurrentExp,
                    ctrl.Proficiency.ExpToNext);
            }
        }

        /// <summary>Set proficiency UI values.</summary>
        private void UpdateProficiencyDisplay(int level, string title, float currentExp, float expToNext)
        {
            if (_proficiencyLevelText != null)
                _proficiencyLevelText.text = $"Lv.{level}";

            if (_proficiencyTitleText != null)
                _proficiencyTitleText.text = title;

            if (_proficiencyExpFill != null)
            {
                _proficiencyExpFill.fillAmount = expToNext > 0
                    ? Mathf.Clamp01(currentExp / expToNext) : 0f;
            }

            if (_proficiencyExpText != null)
            {
                _proficiencyExpText.text = $"{currentExp:F0} / {expToNext:F0}";
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT BUS INTEGRATION
        // ═══════════════════════════════════════════════════════════════════

        #region EventBus Subscriptions

        private void SubscribeToEvents()
        {
            _onAlchemyProgress = OnAlchemyProgress;
            _onAlchemyStageChanged = OnAlchemyStageChanged;
            _onTemperatureChanged = OnTemperatureChanged;
            _onHeatSwitched = OnHeatSwitched;
            _onAlchemyCompleted = OnAlchemyCompleted;
            _onAlchemyExploded = OnAlchemyExploded;
            _onMaterialInput = OnMaterialInput;
            _onAlchemyProfChanged = OnAlchemyProficiencyChanged;

            _onForgeStageChanged = OnForgeStageChanged;
            _onSmeltingTempChanged = OnSmeltingTempChanged;
            _onShapingStrike = OnShapingStrike;
            _onEnlighteningProgress = OnEnlighteningProgress;
            _onForgeCompleted = OnForgeCompleted;
            _onForgeProfChanged = OnForgeProficiencyChanged;

            EventBus.Subscribe<AlchemyProgressEvent>(_onAlchemyProgress);
            EventBus.Subscribe<AlchemyStageChangedEvent>(_onAlchemyStageChanged);
            EventBus.Subscribe<TemperatureChangedEvent>(_onTemperatureChanged);
            EventBus.Subscribe<HeatSwitchedEvent>(_onHeatSwitched);
            EventBus.Subscribe<AlchemyCompletedEvent>(_onAlchemyCompleted);
            EventBus.Subscribe<AlchemyExplodedEvent>(_onAlchemyExploded);
            EventBus.Subscribe<MaterialInputEvent>(_onMaterialInput);
            EventBus.Subscribe<AlchemyProficiencyChangedEvent>(_onAlchemyProfChanged);

            EventBus.Subscribe<ForgeStageChangedEvent>(_onForgeStageChanged);
            EventBus.Subscribe<SmeltingTemperatureEvent>(_onSmeltingTempChanged);
            EventBus.Subscribe<ShapingStrikeEvent>(_onShapingStrike);
            EventBus.Subscribe<EnlighteningProgressEvent>(_onEnlighteningProgress);
            EventBus.Subscribe<ForgeCompletedEvent>(_onForgeCompleted);
            EventBus.Subscribe<ForgeProficiencyChangedEvent>(_onForgeProfChanged);
        }

        private void UnsubscribeFromEvents()
        {
            if (_onAlchemyProgress != null) EventBus.Unsubscribe<AlchemyProgressEvent>(_onAlchemyProgress);
            if (_onAlchemyStageChanged != null) EventBus.Unsubscribe<AlchemyStageChangedEvent>(_onAlchemyStageChanged);
            if (_onTemperatureChanged != null) EventBus.Unsubscribe<TemperatureChangedEvent>(_onTemperatureChanged);
            if (_onHeatSwitched != null) EventBus.Unsubscribe<HeatSwitchedEvent>(_onHeatSwitched);
            if (_onAlchemyCompleted != null) EventBus.Unsubscribe<AlchemyCompletedEvent>(_onAlchemyCompleted);
            if (_onAlchemyExploded != null) EventBus.Unsubscribe<AlchemyExplodedEvent>(_onAlchemyExploded);
            if (_onMaterialInput != null) EventBus.Unsubscribe<MaterialInputEvent>(_onMaterialInput);
            if (_onAlchemyProfChanged != null) EventBus.Unsubscribe<AlchemyProficiencyChangedEvent>(_onAlchemyProfChanged);

            if (_onForgeStageChanged != null) EventBus.Unsubscribe<ForgeStageChangedEvent>(_onForgeStageChanged);
            if (_onSmeltingTempChanged != null) EventBus.Unsubscribe<SmeltingTemperatureEvent>(_onSmeltingTempChanged);
            if (_onShapingStrike != null) EventBus.Unsubscribe<ShapingStrikeEvent>(_onShapingStrike);
            if (_onEnlighteningProgress != null) EventBus.Unsubscribe<EnlighteningProgressEvent>(_onEnlighteningProgress);
            if (_onForgeCompleted != null) EventBus.Unsubscribe<ForgeCompletedEvent>(_onForgeCompleted);
            if (_onForgeProfChanged != null) EventBus.Unsubscribe<ForgeProficiencyChangedEvent>(_onForgeProfChanged);
        }

        private void OnAlchemyProgress(AlchemyProgressEvent evt)
        {
            // UI updated in Update loop.
        }

        private void OnAlchemyStageChanged(AlchemyStageChangedEvent evt)
        {
            if (_stageNameText != null)
                _stageNameText.text = AlchemyController.GetStageDisplayName(evt.NewStage);
        }

        private void OnTemperatureChanged(TemperatureChangedEvent evt)
        {
            // Handled in UpdateAlchemyUI.
        }

        private void OnHeatSwitched(HeatSwitchedEvent evt)
        {
            UpdateHeatButtonVisuals(evt.NewLevel);
        }

        private void OnAlchemyCompleted(AlchemyCompletedEvent evt)
        {
            Debug.Log($"[CraftingUI] 炼丹完成: {evt.PillName} ({AlchemyController.GetQualityDisplayName(evt.Quality)})");
        }

        private void OnAlchemyExploded(AlchemyExplodedEvent evt)
        {
            Debug.LogWarning($"[CraftingUI] 炸炉! 伤害: {evt.PlayerDamage:F0}, 材料损失: {evt.MaterialsLostPercent * 100:F0}%");
        }

        private void OnMaterialInput(MaterialInputEvent evt)
        {
            // Update ingredient slots.
            for (int i = 0; i < _ingredientSlots.Count; i++)
            {
                if (i < evt.TotalInputs)
                {
                    _ingredientSlots[i].GameObject.SetActive(true);
                    _ingredientSlots[i].IsOccupied = true;
                    if (evt.InputIndex == i && _ingredientSlots[i].NameText != null)
                    {
                        _ingredientSlots[i].NameText.text = evt.MaterialName;
                    }
                }
            }
        }

        private void OnAlchemyProficiencyChanged(AlchemyProficiencyChangedEvent evt)
        {
            UpdateProficiencyDisplay(evt.Level, evt.Title, evt.CurrentExp, evt.ExpToNext);
        }

        private void OnForgeStageChanged(ForgeStageChangedEvent evt)
        {
            Debug.Log($"[CraftingUI] 炼器阶段: {ForgeController.GetStageDisplayName(evt.NewStage)}");
        }

        private void OnSmeltingTempChanged(SmeltingTemperatureEvent evt)
        {
            // Handled in UpdateForgeUI.
        }

        private void OnShapingStrike(ShapingStrikeEvent evt)
        {
            if (_strikeCountText != null)
                _strikeCountText.text = $"第 {evt.CurrentStrike}/{evt.TotalStrikes} 锤";
        }

        private void OnEnlighteningProgress(EnlighteningProgressEvent evt)
        {
            // Handled in UpdateForgeUI.
        }

        private void OnForgeCompleted(ForgeCompletedEvent evt)
        {
            Debug.Log($"[CraftingUI] 炼器完成: {evt.EquipmentName} ({ForgeController.GetQualityDisplayName(evt.Quality)})");
        }

        private void OnForgeProficiencyChanged(ForgeProficiencyChangedEvent evt)
        {
            UpdateProficiencyDisplay(evt.Level, evt.Title, evt.CurrentExp, evt.ExpToNext);
        }

        #endregion

        #region Gathering Perception HUD

        /// <summary>Update resource detection indicators on the mini-radar.</summary>
        public void UpdateGatheringPerception(int nearbyNodes, bool inRange)
        {
            if (_gatheringHud == null || !_gatheringHud.activeSelf) return;

            if (_gatheringHintText != null)
            {
                _gatheringHintText.text = nearbyNodes > 0
                    ? $"感知到 {nearbyNodes} 处灵材"
                    : "附近无灵材";
                _gatheringHintText.color = nearbyNodes > 0 ? Color.green : Color.gray;
            }

            if (_resourceRadarIcon != null)
            {
                _resourceRadarIcon.gameObject.SetActive(nearbyNodes > 0);
                _resourceRadarIcon.color = inRange ? Color.green : _rareResourceColor;
            }

            EventBus.Publish(new GatheringPerceptionUpdateEvent
            {
                NearbyNodes = nearbyNodes,
                IsInGatheringRange = inRange
            });
        }

        #endregion

        #region Editor/Debug Helpers

        /// <summary>Get a debug status string.</summary>
        public string GetDebugStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== CraftingUI Status ===");
            sb.AppendLine($"Open: {_isOpen}");
            sb.AppendLine($"Mode: {(_isAlchemyMode ? "炼丹" : "炼器")}");
            sb.AppendLine($"Ingredient Slots: {_ingredientSlots.Count}");
            sb.AppendLine($"Temp Display: {_displayedTemperature:F1}°C");

            if (_isAlchemyMode)
            {
                var ctrl = AlchemyController.Instance;
                if (ctrl != null)
                    sb.AppendLine($"Alchemy Stage: {AlchemyController.GetStageDisplayName(ctrl.CurrentStage)}");
            }
            else
            {
                var ctrl = ForgeController.Instance;
                if (ctrl != null)
                    sb.AppendLine($"Forge Stage: {ForgeController.GetStageDisplayName(ctrl.CurrentStage)}");
            }

            return sb.ToString();
        }

        #endregion
    }
}
