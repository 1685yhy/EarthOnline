using System;
using System.Collections;
using System.Collections.Generic;
using EarthOnline.Framework;
using EarthOnline.World;
using UnityEngine;
using UnityEngine.UI;

namespace EarthOnline.UI
{
    // ═══════════════════════════════════════════════════════════════════════
    //  UI-Specific Event Data Structs
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Published when the sect UI opens or closes.</summary>
    public struct SectUIVisibilityEvent
    {
        public bool Visible;
    }

    /// <summary>Published when a sect daily quest is claimed.</summary>
    public struct SectDailyQuestClaimedEvent
    {
        public string QuestId;
        public string QuestName;
        public int ContributionReward;
        public int SpiritStoneReward;
    }

    /// <summary>Published when a bounty quest is accepted or completed.</summary>
    public struct SectBountyEvent
    {
        public string BountyId;
        public string BountyName;
        public bool Accepted;
        public bool Completed;
    }

    /// <summary>Published when an item is purchased from the sect shop.</summary>
    public struct SectShopPurchaseEvent
    {
        public string ItemId;
        public string ItemName;
        public int Cost;
        public int ContributionCost;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Data Classes for UI Display
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Display data for one daily quest entry.</summary>
    [Serializable]
    public class SectDailyQuestData
    {
        public string QuestId;
        public string QuestName;
        [TextArea(1, 2)] public string Description;
        public int ContributionReward;
        public int SpiritStoneReward;
        public bool IsCompleted;
        public bool IsClaimed;
    }

    /// <summary>Display data for one bounty quest entry.</summary>
    [Serializable]
    public class SectBountyData
    {
        public string BountyId;
        public string BountyName;
        [TextArea(1, 2)] public string Description;
        public int ContributionReward;
        public int SpiritStoneReward;
        public int RequiredRealmLevel;
        public bool IsAccepted;
        public bool IsCompleted;
        public float TimeRemainingHours;
    }

    /// <summary>Display data for a shop item.</summary>
    [Serializable]
    public class SectShopItemData
    {
        public string ItemId;
        public string ItemName;
        [TextArea(1, 2)] public string Description;
        public string Category; // "Technique", "Pill", "Equipment"
        public int SpiritStoneCost;
        public int ContributionCost;
        public int RequiredRank; // minimum SectRank int value needed
        public bool IsAvailable;
    }

    /// <summary>Display data for the identity panel.</summary>
    [Serializable]
    public class SectIdentityDisplayData
    {
        public string PlayerId;
        public string SectName;
        public string RankName;
        public int Contribution;
        public int NextRankThreshold;
        public int ReputationLevel;
        public int SectReputationValue;
        public string ReputationAttitude;
        public List<SectReputationDisplay> OtherSectReputations = new List<SectReputationDisplay>();
        public bool IsSpy;
        public string SpyTrueSect;
    }

    /// <summary>Display data for reputation with another sect.</summary>
    [Serializable]
    public class SectReputationDisplay
    {
        public SectType Sect;
        public string SectName;
        public int ReputationValue;
        public string Attitude;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Sect UI Controller
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages the sect UI panel (Story 006) with three tabs:
    ///   1) 身份面板 — sect identity, rank, contribution, reputation
    ///   2) 任务面板 — daily quests + bounty quests
    ///   3) 兑换商店 — techniques / pills / equipment exchange
    ///
    /// Features:
    ///   - Reputation linkage: joining a sect affects attitudes of other sects
    ///   - Spy identity: triggered by special items, revealed through events
    ///   - Sect destroyed → notification to become 散修
    ///   - Leader betrayal → crisis warning
    ///
    /// UGUI wiring via SerializeField — drag-and-drop in the Unity Editor.
    /// Communication via EventBus.
    /// </summary>
    public class SectUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        //  MAIN PANEL
        // ═══════════════════════════════════════════════════════════════════

        [Header("Main Panel")]
        [SerializeField] private GameObject _mainPanel;

        // ═══════════════════════════════════════════════════════════════════
        //  TAB BUTTONS
        // ═══════════════════════════════════════════════════════════════════

        [Header("Tab Buttons")]
        [SerializeField] private Button _identityTabButton;
        [SerializeField] private Button _questTabButton;
        [SerializeField] private Button _shopTabButton;

        [SerializeField] private GameObject _identityTabHighlight;
        [SerializeField] private GameObject _questTabHighlight;
        [SerializeField] private GameObject _shopTabHighlight;

        // ═══════════════════════════════════════════════════════════════════
        //  TAB CONTENT ROOTS
        // ═══════════════════════════════════════════════════════════════════

        [Header("Tab Content")]
        [SerializeField] private GameObject _identityContent;
        [SerializeField] private GameObject _questContent;
        [SerializeField] private GameObject _shopContent;

        // ═══════════════════════════════════════════════════════════════════
        //  IDENTITY PANEL (身份面板)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Identity Panel")]
        [SerializeField] private Text _sectNameText;
        [SerializeField] private Text _rankNameText;
        [SerializeField] private Text _contributionText;
        [SerializeField] private Slider _contributionBar;
        [SerializeField] private Text _contributionProgressText;

        [SerializeField] private Text _reputationLevelText;
        [SerializeField] private Text _sectReputationValueText;
        [SerializeField] private Text _reputationAttitudeText;

        [SerializeField] private Transform _reputationListRoot;
        [SerializeField] private GameObject _reputationEntryPrefab;

        [SerializeField] private Text _spyStatusText;
        [SerializeField] private GameObject _spyActiveIndicator;
        [SerializeField] private Button _spyActivateButton;

        [SerializeField] private Text _sectDestructionWarningText;

        // ═══════════════════════════════════════════════════════════════════
        //  QUEST PANEL (任务面板)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Quest Panel")]
        [SerializeField] private Transform _dailyQuestRoot;
        [SerializeField] private GameObject _dailyQuestEntryPrefab;

        [SerializeField] private Text _dailyQuestCounterText;
        [SerializeField] private Text _dailyQuestRefreshText;

        [SerializeField] private Transform _bountyQuestRoot;
        [SerializeField] private GameObject _bountyQuestEntryPrefab;

        [SerializeField] private Text _bountyCountText;

        [Header("Quest Colors")]
        [SerializeField] private Color _questAvailableColor = Color.white;
        [SerializeField] private Color _questCompletedColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color _questClaimedColor = new Color(0.5f, 0.5f, 0.5f);

        // ═══════════════════════════════════════════════════════════════════
        //  SHOP PANEL (兑换商店)
        // ═══════════════════════════════════════════════════════════════════

        [Header("Shop Panel")]
        [SerializeField] private Transform _shopItemRoot;
        [SerializeField] private GameObject _shopItemEntryPrefab;

        [SerializeField] private Button _filterAllButton;
        [SerializeField] private Button _filterTechniqueButton;
        [SerializeField] private Button _filterPillButton;
        [SerializeField] private Button _filterEquipmentButton;

        [SerializeField] private GameObject _filterAllHighlight;
        [SerializeField] private GameObject _filterTechniqueHighlight;
        [SerializeField] private GameObject _filterPillHighlight;
        [SerializeField] private GameObject _filterEquipmentHighlight;

        [SerializeField] private Text _shopDiscountText;
        [SerializeField] private Text _playerContributionText;

        [Header("Shop Colors")]
        [SerializeField] private Color _shopCanAffordColor = Color.white;
        [SerializeField] private Color _shopCannotAffordColor = new Color(0.7f, 0.3f, 0.3f);
        [SerializeField] private Color _shopRankLockedColor = new Color(0.5f, 0.5f, 0.5f);

        // ═══════════════════════════════════════════════════════════════════
        //  DETAIL POPUP
        // ═══════════════════════════════════════════════════════════════════

        [Header("Detail Popup")]
        [SerializeField] private GameObject _detailPopup;
        [SerializeField] private Text _detailTitleText;
        [SerializeField] private Text _detailDescriptionText;
        [SerializeField] private Text _detailRewardsText;
        [SerializeField] private Button _detailConfirmButton;
        [SerializeField] private Button _detailCloseButton;
        [SerializeField] private Text _detailConfirmButtonText;

        // ═══════════════════════════════════════════════════════════════════
        //  CRISIS / EVENT POPUP
        // ═══════════════════════════════════════════════════════════════════

        [Header("Crisis Notification")]
        [SerializeField] private GameObject _crisisPopup;
        [SerializeField] private Text _crisisTitleText;
        [SerializeField] private Text _crisisMessageText;
        [SerializeField] private Image _crisisIconImage;
        [SerializeField] private Button _crisisConfirmButton;
        [SerializeField] private Color _crisisDangerColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _crisisWarningColor = new Color(0.9f, 0.7f, 0.1f);

        // ═══════════════════════════════════════════════════════════════════
        //  TOGGLE BUTTON
        // ═══════════════════════════════════════════════════════════════════

        [Header("Toggle")]
        [SerializeField] private Button _toggleButton;

        // ═══════════════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ═══════════════════════════════════════════════════════════════════

        public enum Tab { Identity, Quest, Shop }
        private Tab _currentTab = Tab.Identity;
        private string _currentFilterCategory = ""; // "" = all

        // Cached player data
        private string _currentPlayerId;
        private bool _isInFormalSect;
        private SectType? _currentSect;
        private List<SectDailyQuestData> _dailyQuests = new List<SectDailyQuestData>();
        private List<SectBountyData> _bountyQuests = new List<SectBountyData>();
        private List<SectShopItemData> _shopItems = new List<SectShopItemData>();

        // Dynamic UI object pools
        private List<GameObject> _reputationEntries = new List<GameObject>();
        private List<GameObject> _dailyQuestEntries = new List<GameObject>();
        private List<GameObject> _bountyQuestEntries = new List<GameObject>();
        private List<GameObject> _shopItemEntries = new List<GameObject>();

        // Callback storage for detail popup
        private Action _detailConfirmAction;

        // ═══════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_mainPanel != null)
                _mainPanel.SetActive(false);

            if (_detailPopup != null)
                _detailPopup.SetActive(false);

            if (_crisisPopup != null)
                _crisisPopup.SetActive(false);

            // Hide tab highlights initially
            SetActiveTab(Tab.Identity);
        }

        private void Start()
        {
            WireTabButtons();
            WireFilterButtons();
            WireToggleButton();
            WireDetailButtons();
            WireCrisisButtons();
        }

        private void OnEnable()
        {
            // Sect manager events
            EventBus.Subscribe<SectJoinedEvent>(OnSectJoined);
            EventBus.Subscribe<SectLeftEvent>(OnSectLeft);
            EventBus.Subscribe<SectExpelledEvent>(OnSectExpelled);

            // Rank system events
            EventBus.Subscribe<ContributionGainedEvent>(OnContributionGained);
            EventBus.Subscribe<ContributionSpentEvent>(OnContributionSpent);
            EventBus.Subscribe<RankPromotedEvent>(OnRankPromoted);

            // War system events
            EventBus.Subscribe<SectReputationChangedEvent>(OnSectReputationChanged);
            EventBus.Subscribe<SectDestroyedEvent>(OnSectDestroyed);
            EventBus.Subscribe<LeaderBetrayalEvent>(OnLeaderBetrayal);
            EventBus.Subscribe<SpyIdentityTriggeredEvent>(OnSpyIdentityTriggered);
            EventBus.Subscribe<WarDeclaredEvent>(OnWarDeclared);
            EventBus.Subscribe<WarSettledEvent>(OnWarSettled);

            // Rank system (for daily quest tracking)
            EventBus.Subscribe<SectDailyQuestClaimedEvent>(OnDailyQuestClaimed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SectJoinedEvent>(OnSectJoined);
            EventBus.Unsubscribe<SectLeftEvent>(OnSectLeft);
            EventBus.Unsubscribe<SectExpelledEvent>(OnSectExpelled);

            EventBus.Unsubscribe<ContributionGainedEvent>(OnContributionGained);
            EventBus.Unsubscribe<ContributionSpentEvent>(OnContributionSpent);
            EventBus.Unsubscribe<RankPromotedEvent>(OnRankPromoted);

            EventBus.Unsubscribe<SectReputationChangedEvent>(OnSectReputationChanged);
            EventBus.Unsubscribe<SectDestroyedEvent>(OnSectDestroyed);
            EventBus.Unsubscribe<LeaderBetrayalEvent>(OnLeaderBetrayal);
            EventBus.Unsubscribe<SpyIdentityTriggeredEvent>(OnSpyIdentityTriggered);
            EventBus.Unsubscribe<WarDeclaredEvent>(OnWarDeclared);
            EventBus.Unsubscribe<WarSettledEvent>(OnWarSettled);

            EventBus.Unsubscribe<SectDailyQuestClaimedEvent>(OnDailyQuestClaimed);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PUBLIC API — OPEN / CLOSE
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Open the sect UI for a specific player.</summary>
        public void Open(string playerId)
        {
            _currentPlayerId = playerId;
            RefreshAllData();
            _mainPanel.SetActive(true);

            SetActiveTab(_currentTab);

            EventBus.Publish(new SectUIVisibilityEvent { Visible = true });
            Debug.Log($"[SectUI] Opened for player {playerId}");
        }

        /// <summary>Close the sect UI.</summary>
        public void Close()
        {
            _mainPanel.SetActive(false);

            if (_detailPopup != null)
                _detailPopup.SetActive(false);

            EventBus.Publish(new SectUIVisibilityEvent { Visible = false });
        }

        /// <summary>Toggle the sect UI open/close.</summary>
        public void Toggle(string playerId)
        {
            if (_mainPanel != null && _mainPanel.activeSelf)
            {
                Close();
            }
            else
            {
                Open(playerId);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DATA REFRESH
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Refresh all data from the backend systems and update UI.</summary>
        private void RefreshAllData()
        {
            RefreshPlayerState();
            RefreshIdentityPanel();
            RefreshQuestPanel();
            RefreshShopPanel();
        }

        /// <summary>Refresh cached player state from SectManager.</summary>
        private void RefreshPlayerState()
        {
            if (string.IsNullOrEmpty(_currentPlayerId)) return;

            var manager = SectManager.Instance;
            _isInFormalSect = manager.IsInFormalSect(_currentPlayerId);
            _currentSect = manager.GetCurrentSect(_currentPlayerId);
        }

        /// <summary>Refresh the identity tab content.</summary>
        private void RefreshIdentityPanel()
        {
            if (!_identityContent.activeSelf) return;
            if (string.IsNullOrEmpty(_currentPlayerId)) return;

            var manager = SectManager.Instance;
            var warSystem = SectWarSystem.Instance;
            var rankSystem = SectRankSystem.Instance;

            // ── Sect name ──
            if (_sectNameText != null)
            {
                if (_isInFormalSect && _currentSect.HasValue)
                {
                    string name = warSystem != null
                        ? warSystem.GetDisplayName(_currentSect.Value)
                        : _currentSect.Value.ToString();
                    _sectNameText.text = name;
                }
                else
                {
                    _sectNameText.text = "散修";
                }
            }

            // ── Rank name ──
            if (_rankNameText != null)
            {
                if (_isInFormalSect)
                {
                    var rank = manager.GetRank(_currentPlayerId);
                    var rankConfig = rankSystem != null ? rankSystem.GetRankConfig(rank) : null;
                    _rankNameText.text = rankConfig != null ? rankConfig.DisplayName : rank.ToString();
                }
                else
                {
                    _rankNameText.text = "无门派";
                }
            }

            // ── Contribution ──
            int contribution = manager.GetContribution(_currentPlayerId);
            if (_contributionText != null)
                _contributionText.text = $"贡献: {contribution}";

            // Contribution bar showing progress to next rank
            if (_isInFormalSect && rankSystem != null)
            {
                var currentRank = manager.GetRank(_currentPlayerId);
                var nextRank = currentRank < SectRank.Leader ? currentRank + 1 : currentRank;
                int currentThreshold = rankSystem.GetThresholdForRank(currentRank);
                int nextThreshold = rankSystem.GetThresholdForRank(nextRank);

                if (_contributionBar != null)
                {
                    if (nextThreshold > currentThreshold)
                    {
                        _contributionBar.minValue = currentThreshold;
                        _contributionBar.maxValue = nextThreshold;
                        _contributionBar.value = Mathf.Clamp(contribution, currentThreshold, nextThreshold);
                    }
                    else
                    {
                        _contributionBar.value = _contributionBar.maxValue; // max rank
                    }
                }

                if (_contributionProgressText != null)
                {
                    if (nextThreshold > currentThreshold)
                    {
                        _contributionProgressText.text = $"{contribution} / {nextThreshold}";
                    }
                    else
                    {
                        _contributionProgressText.text = "已满";
                    }
                }
            }
            else
            {
                if (_contributionBar != null)
                {
                    _contributionBar.value = 0;
                    _contributionBar.maxValue = 1;
                }
                if (_contributionProgressText != null)
                    _contributionProgressText.text = "";
            }

            // ── Reputation ──
            if (warSystem != null && _currentSect.HasValue)
            {
                int repLevel = warSystem.GetSectRepLevel(_currentSect.Value);
                if (_reputationLevelText != null)
                    _reputationLevelText.text = $"声望等级: {repLevel}";
            }

            // ── Attitude toward current sect ──
            if (warSystem != null && _currentSect.HasValue)
            {
                // Show player's relationship with their own sect (always high)
                if (_sectReputationValueText != null)
                    _sectReputationValueText.text = "声望: 100";
                if (_reputationAttitudeText != null)
                    _reputationAttitudeText.text = "归属";
            }

            // ── Reputation list with other sects ──
            RefreshReputationList();

            // ── Spy status ──
            bool isSpy = warSystem != null && warSystem.IsSpy(_currentPlayerId);
            if (_spyActiveIndicator != null)
                _spyActiveIndicator.SetActive(isSpy);
            if (_spyStatusText != null)
            {
                if (isSpy)
                {
                    var trueSect = warSystem.GetSpyTrueSect(_currentPlayerId);
                    string trueSectName = trueSect.HasValue && warSystem != null
                        ? warSystem.GetDisplayName(trueSect.Value)
                        : "未知";
                    _spyStatusText.text = $"<color=#FFAA00>⚔ 卧底身份已激活</color>\n真实所属: {trueSectName}";
                    _spyStatusText.gameObject.SetActive(true);
                }
                else if (_isInFormalSect)
                {
                    _spyStatusText.text = "可使用【卧底令】激活卧底身份";
                    _spyStatusText.gameObject.SetActive(true);
                }
                else
                {
                    _spyStatusText.gameObject.SetActive(false);
                }
            }
            if (_spyActivateButton != null)
                _spyActivateButton.gameObject.SetActive(_isInFormalSect && !isSpy);

            // ── Sect destruction warning (hidden normally) ──
            if (_sectDestructionWarningText != null)
                _sectDestructionWarningText.gameObject.SetActive(false);
        }

        /// <summary>Refresh the reputation list on the identity tab.</summary>
        private void RefreshReputationList()
        {
            // Clear old entries
            foreach (var entry in _reputationEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _reputationEntries.Clear();

            if (_reputationListRoot == null || _reputationEntryPrefab == null) return;
            if (SectWarSystem.Instance == null || !_currentSect.HasValue) return;

            var warSystem = SectWarSystem.Instance;

            foreach (SectType other in Enum.GetValues(typeof(SectType)))
            {
                if (other == _currentSect.Value || other == SectType.SanXiuLianMeng)
                    continue;

                int rep = warSystem.GetSectReputation(_currentSect.Value, other);
                var attitude = warSystem.GetSectAttitude(_currentSect.Value, other);
                string attName = GetAttitudeDisplayName(attitude);

                var entry = Instantiate(_reputationEntryPrefab, _reputationListRoot);
                var textComp = entry.GetComponentInChildren<Text>();
                if (textComp != null)
                {
                    string sectName = warSystem.GetDisplayName(other);
                    textComp.text = $"{sectName}: {rep} ({attName})";
                    textComp.color = GetAttitudeColor(attitude);
                }
                _reputationEntries.Add(entry);
            }
        }

        /// <summary>Refresh the quest tab content.</summary>
        private void RefreshQuestPanel()
        {
            if (!_questContent.activeSelf) return;

            RefreshDailyQuests();
            RefreshBountyQuests();
        }

        /// <summary>Refresh daily quest list.</summary>
        private void RefreshDailyQuests()
        {
            // Clear old entries
            foreach (var entry in _dailyQuestEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _dailyQuestEntries.Clear();

            if (_dailyQuestRoot == null || _dailyQuestEntryPrefab == null) return;

            // Populate sample daily quests (in a full implementation, read from a quest database)
            if (_dailyQuests.Count == 0)
            {
                _dailyQuests = GenerateDailyQuests();
            }

            foreach (var quest in _dailyQuests)
            {
                var entry = Instantiate(_dailyQuestEntryPrefab, _dailyQuestRoot);
                SetupDailyQuestEntry(entry, quest);
                _dailyQuestEntries.Add(entry);
            }

            // Update counter
            int completed = _dailyQuests.FindAll(q => q.IsCompleted).Count;
            int total = _dailyQuests.Count;
            if (_dailyQuestCounterText != null)
                _dailyQuestCounterText.text = $"每日任务: {completed}/{total}";

            if (_dailyQuestRefreshText != null)
                _dailyQuestRefreshText.text = "每日重置: 0:00";
        }

        /// <summary>Refresh bounty quest list.</summary>
        private void RefreshBountyQuests()
        {
            foreach (var entry in _bountyQuestEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _bountyQuestEntries.Clear();

            if (_bountyQuestRoot == null || _bountyQuestEntryPrefab == null) return;

            // Populate sample bounty quests
            if (_bountyQuests.Count == 0)
            {
                _bountyQuests = GenerateBountyQuests();
            }

            foreach (var bounty in _bountyQuests)
            {
                var entry = Instantiate(_bountyQuestEntryPrefab, _bountyQuestRoot);
                SetupBountyEntry(entry, bounty);
                _bountyQuestEntries.Add(entry);
            }

            int available = _bountyQuests.FindAll(b => !b.IsAccepted).Count;
            if (_bountyCountText != null)
                _bountyCountText.text = $"悬赏榜: {available} 个可接取";
        }

        /// <summary>Refresh the shop tab content.</summary>
        private void RefreshShopPanel()
        {
            if (!_shopContent.activeSelf) return;

            if (_playerContributionText != null)
            {
                int contrib = SectManager.Instance.GetContribution(_currentPlayerId);
                _playerContributionText.text = $"当前贡献: {contrib}";
            }

            if (_shopDiscountText != null)
            {
                float discount = SectRankSystem.Instance != null
                    ? SectRankSystem.Instance.GetShopDiscount(_currentPlayerId)
                    : 1.0f;
                if (discount < 1.0f)
                    _shopDiscountText.text = $"门派折扣: {(1f - discount) * 100f:F0}% OFF";
                else
                    _shopDiscountText.text = "无门派折扣";
            }

            RefreshShopItems();
        }

        /// <summary>Refresh the shop item list with current filter.</summary>
        private void RefreshShopItems()
        {
            foreach (var entry in _shopItemEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _shopItemEntries.Clear();

            if (_shopItemRoot == null || _shopItemEntryPrefab == null) return;

            // Generate sample shop items if needed
            if (_shopItems.Count == 0)
            {
                _shopItems = GenerateShopItems();
            }

            foreach (var item in _shopItems)
            {
                // Apply category filter
                if (!string.IsNullOrEmpty(_currentFilterCategory) &&
                    item.Category != _currentFilterCategory)
                    continue;

                var entry = Instantiate(_shopItemEntryPrefab, _shopItemRoot);
                SetupShopItemEntry(entry, item);
                _shopItemEntries.Add(entry);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TAB MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Switch to a specific tab.</summary>
        public void SetActiveTab(Tab tab)
        {
            _currentTab = tab;

            // Update highlights
            if (_identityTabHighlight != null) _identityTabHighlight.SetActive(tab == Tab.Identity);
            if (_questTabHighlight != null) _questTabHighlight.SetActive(tab == Tab.Quest);
            if (_shopTabHighlight != null) _shopTabHighlight.SetActive(tab == Tab.Shop);

            // Show content
            if (_identityContent != null) _identityContent.SetActive(tab == Tab.Identity);
            if (_questContent != null) _questContent.SetActive(tab == Tab.Quest);
            if (_shopContent != null) _shopContent.SetActive(tab == Tab.Shop);

            // Refresh data for the selected tab
            switch (tab)
            {
                case Tab.Identity: RefreshIdentityPanel(); break;
                case Tab.Quest: RefreshQuestPanel(); break;
                case Tab.Shop: RefreshShopPanel(); break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  QUEST DETAIL POPUP
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Show the detail popup for a quest or shop item.</summary>
        private void ShowDetailPopup(string title, string description, string rewards, string confirmText, Action onConfirm)
        {
            if (_detailPopup == null) return;

            if (_detailTitleText != null) _detailTitleText.text = title;
            if (_detailDescriptionText != null) _detailDescriptionText.text = description;
            if (_detailRewardsText != null) _detailRewardsText.text = rewards;
            if (_detailConfirmButtonText != null) _detailConfirmButtonText.text = confirmText;

            _detailConfirmAction = onConfirm;

            _detailPopup.SetActive(true);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BUTTON WIRING
        // ═══════════════════════════════════════════════════════════════════

        private void WireTabButtons()
        {
            if (_identityTabButton != null)
            {
                _identityTabButton.onClick.RemoveAllListeners();
                _identityTabButton.onClick.AddListener(() => SetActiveTab(Tab.Identity));
            }
            if (_questTabButton != null)
            {
                _questTabButton.onClick.RemoveAllListeners();
                _questTabButton.onClick.AddListener(() => SetActiveTab(Tab.Quest));
            }
            if (_shopTabButton != null)
            {
                _shopTabButton.onClick.RemoveAllListeners();
                _shopTabButton.onClick.AddListener(() => SetActiveTab(Tab.Shop));
            }
        }

        private void WireFilterButtons()
        {
            if (_filterAllButton != null)
            {
                _filterAllButton.onClick.RemoveAllListeners();
                _filterAllButton.onClick.AddListener(() => SetShopFilter(""));
            }
            if (_filterTechniqueButton != null)
            {
                _filterTechniqueButton.onClick.RemoveAllListeners();
                _filterTechniqueButton.onClick.AddListener(() => SetShopFilter("Technique"));
            }
            if (_filterPillButton != null)
            {
                _filterPillButton.onClick.RemoveAllListeners();
                _filterPillButton.onClick.AddListener(() => SetShopFilter("Pill"));
            }
            if (_filterEquipmentButton != null)
            {
                _filterEquipmentButton.onClick.RemoveAllListeners();
                _filterEquipmentButton.onClick.AddListener(() => SetShopFilter("Equipment"));
            }
        }

        private void WireToggleButton()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.RemoveAllListeners();
                _toggleButton.onClick.AddListener(() => Toggle(_currentPlayerId));
            }
        }

        private void WireDetailButtons()
        {
            if (_detailConfirmButton != null)
            {
                _detailConfirmButton.onClick.RemoveAllListeners();
                _detailConfirmButton.onClick.AddListener(OnDetailConfirmClicked);
            }
            if (_detailCloseButton != null)
            {
                _detailCloseButton.onClick.RemoveAllListeners();
                _detailCloseButton.onClick.AddListener(() =>
                {
                    if (_detailPopup != null) _detailPopup.SetActive(false);
                });
            }
        }

        private void WireCrisisButtons()
        {
            if (_crisisConfirmButton != null)
            {
                _crisisConfirmButton.onClick.RemoveAllListeners();
                _crisisConfirmButton.onClick.AddListener(() =>
                {
                    if (_crisisPopup != null) _crisisPopup.SetActive(false);
                });
            }
        }

        private void WireSpyActivateButton()
        {
            if (_spyActivateButton != null)
            {
                _spyActivateButton.onClick.RemoveAllListeners();
                _spyActivateButton.onClick.AddListener(OnSpyActivateClicked);
            }
        }

        /// <summary>Set the shop filter category and refresh.</summary>
        public void SetShopFilter(string category)
        {
            _currentFilterCategory = category;

            if (_filterAllHighlight != null) _filterAllHighlight.SetActive(category == "");
            if (_filterTechniqueHighlight != null) _filterTechniqueHighlight.SetActive(category == "Technique");
            if (_filterPillHighlight != null) _filterPillHighlight.SetActive(category == "Pill");
            if (_filterEquipmentHighlight != null) _filterEquipmentHighlight.SetActive(category == "Equipment");

            RefreshShopItems();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  QUEST ENTRY SETUP
        // ═══════════════════════════════════════════════════════════════════

        private void SetupDailyQuestEntry(GameObject entry, SectDailyQuestData quest)
        {
            var textComp = entry.GetComponentInChildren<Text>();
            if (textComp != null)
            {
                string status = quest.IsClaimed ? "[已领取] " : quest.IsCompleted ? "[可领取] " : "";
                textComp.text = $"{status}{quest.QuestName}\n{quest.Description}";
                textComp.color = quest.IsClaimed ? _questClaimedColor
                    : quest.IsCompleted ? _questCompletedColor
                    : _questAvailableColor;
            }

            var button = entry.GetComponentInChildren<Button>();
            if (button != null)
            {
                button.interactable = quest.IsCompleted && !quest.IsClaimed;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    ShowDetailPopup(
                        quest.QuestName,
                        quest.Description,
                        $"贡献: +{quest.ContributionReward}\n灵石: +{quest.SpiritStoneReward}",
                        "领取奖励",
                        () => ClaimDailyQuest(quest.QuestId)
                    );
                });
            }
        }

        private void SetupBountyEntry(GameObject entry, SectBountyData bounty)
        {
            var textComp = entry.GetComponentInChildren<Text>();
            if (textComp != null)
            {
                string status = bounty.IsCompleted ? "[已完成] "
                    : bounty.IsAccepted ? "[已接取] " : "";
                string timeStr = bounty.TimeRemainingHours > 0
                    ? $" 剩余 {bounty.TimeRemainingHours:F1}h" : "";
                textComp.text = $"{status}{bounty.BountyName}{timeStr}\n{bounty.Description}";
                textComp.color = bounty.IsCompleted ? _questCompletedColor
                    : bounty.IsAccepted ? new Color(0.8f, 0.8f, 0.4f)
                    : _questAvailableColor;
            }

            var button = entry.GetComponentInChildren<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (!bounty.IsAccepted && !bounty.IsCompleted)
                    {
                        ShowDetailPopup(
                            bounty.BountyName,
                            bounty.Description,
                            $"贡献: +{bounty.ContributionReward}\n灵石: +{bounty.SpiritStoneReward}\n需求境界: {bounty.RequiredRealmLevel}级",
                            "接取悬赏",
                            () => AcceptBounty(bounty.BountyId)
                        );
                    }
                    else if (bounty.IsCompleted)
                    {
                        ShowDetailPopup(
                            bounty.BountyName,
                            bounty.Description,
                            $"贡献: +{bounty.ContributionReward}\n灵石: +{bounty.SpiritStoneReward}",
                            "领取奖励",
                            () => ClaimBounty(bounty.BountyId)
                        );
                    }
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SHOP ENTRY SETUP
        // ═══════════════════════════════════════════════════════════════════

        private void SetupShopItemEntry(GameObject entry, SectShopItemData item)
        {
            var textComp = entry.GetComponentInChildren<Text>();
            int playerContrib = SectManager.Instance.GetContribution(_currentPlayerId);
            SectRank playerRank = SectManager.Instance.GetRank(_currentPlayerId);
            bool canAffordContrib = playerContrib >= item.ContributionCost;
            bool rankHighEnough = (int)playerRank >= item.RequiredRank;
            bool available = item.IsAvailable && canAffordContrib && rankHighEnough;

            if (textComp != null)
            {
                string rankLockStr = !rankHighEnough ? " [职级不足]" : "";
                string priceStr = $"贡献: {item.ContributionCost}  灵石: {item.SpiritStoneCost}";
                textComp.text = $"{item.ItemName}{rankLockStr}\n{item.Description}\n{priceStr}";
                textComp.color = !rankHighEnough ? _shopRankLockedColor
                    : !canAffordContrib ? _shopCannotAffordColor
                    : _shopCanAffordColor;
            }

            var button = entry.GetComponentInChildren<Button>();
            if (button != null)
            {
                button.interactable = available;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    ShowDetailPopup(
                        item.ItemName,
                        item.Description,
                        $"贡献: {item.ContributionCost}\n灵石: {item.SpiritStoneCost}\n需求职级: {GetRankName((SectRank)item.RequiredRank)}",
                        "兑换",
                        () => PurchaseShopItem(item.ItemId)
                    );
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ACTION HANDLERS
        // ═══════════════════════════════════════════════════════════════════

        private void OnDetailConfirmClicked()
        {
            _detailConfirmAction?.Invoke();
            if (_detailPopup != null)
                _detailPopup.SetActive(false);
        }

        /// <summary>Claim a daily quest reward.</summary>
        private void ClaimDailyQuest(string questId)
        {
            var quest = _dailyQuests.Find(q => q.QuestId == questId);
            if (quest == null || !quest.IsCompleted || quest.IsClaimed) return;

            quest.IsClaimed = true;

            // Add contribution via SectRankSystem
            if (SectRankSystem.Instance != null)
            {
                SectRankSystem.Instance.AddContribution(
                    _currentPlayerId,
                    ContributionSource.Quest,
                    quest.ContributionReward,
                    $"每日任务: {quest.QuestName}");
            }

            // Add spirit stones (via player inventory system — placeholder)
            Debug.Log($"[SectUI] 每日任务完成: {quest.QuestName}, +{quest.ContributionReward}贡献, +{quest.SpiritStoneReward}灵石");

            EventBus.Publish(new SectDailyQuestClaimedEvent
            {
                QuestId = questId,
                QuestName = quest.QuestName,
                ContributionReward = quest.ContributionReward,
                SpiritStoneReward = quest.SpiritStoneReward,
            });

            RefreshDailyQuests();
            RefreshIdentityPanel();
        }

        /// <summary>Accept a bounty quest.</summary>
        private void AcceptBounty(string bountyId)
        {
            var bounty = _bountyQuests.Find(b => b.BountyId == bountyId);
            if (bounty == null || bounty.IsAccepted || bounty.IsCompleted) return;

            bounty.IsAccepted = true;

            EventBus.Publish(new SectBountyEvent
            {
                BountyId = bountyId,
                BountyName = bounty.BountyName,
                Accepted = true,
                Completed = false,
            });

            RefreshBountyQuests();
        }

        /// <summary>Claim a completed bounty reward.</summary>
        private void ClaimBounty(string bountyId)
        {
            var bounty = _bountyQuests.Find(b => b.BountyId == bountyId);
            if (bounty == null || !bounty.IsCompleted) return;

            if (SectRankSystem.Instance != null)
            {
                SectRankSystem.Instance.AddContribution(
                    _currentPlayerId,
                    ContributionSource.Quest,
                    bounty.ContributionReward,
                    $"悬赏: {bounty.BountyName}");
            }

            Debug.Log($"[SectUI] 悬赏完成: {bounty.BountyName}, +{bounty.ContributionReward}贡献, +{bounty.SpiritStoneReward}灵石");

            EventBus.Publish(new SectBountyEvent
            {
                BountyId = bountyId,
                BountyName = bounty.BountyName,
                Accepted = false,
                Completed = true,
            });

            _bountyQuests.Remove(bounty);
            RefreshBountyQuests();
            RefreshIdentityPanel();
        }

        /// <summary>Purchase an item from the sect shop.</summary>
        private void PurchaseShopItem(string itemId)
        {
            var item = _shopItems.Find(i => i.ItemId == itemId);
            if (item == null || !item.IsAvailable) return;

            var rankSystem = SectRankSystem.Instance;

            // Spend contribution
            if (rankSystem != null)
            {
                var result = rankSystem.SpendContribution(
                    _currentPlayerId,
                    item.Category switch
                    {
                        "Technique" => ContributionSpendType.Technique,
                        "Pill" => ContributionSpendType.Pill,
                        "Equipment" => ContributionSpendType.Technique,
                        _ => ContributionSpendType.Technique,
                    },
                    item.ContributionCost,
                    $"商店兑换: {item.ItemName}");

                if (result != SpendResult.Success)
                {
                    Debug.LogWarning($"[SectUI] 购买失败: {result}");
                    return;
                }
            }

            // Deduct spirit stones (placeholder — would go through player inventory)
            Debug.Log($"[SectUI] 商店购买: {item.ItemName}, 消耗 {item.ContributionCost}贡献 + {item.SpiritStoneCost}灵石");

            EventBus.Publish(new SectShopPurchaseEvent
            {
                ItemId = itemId,
                ItemName = item.ItemName,
                Cost = item.SpiritStoneCost,
                ContributionCost = item.ContributionCost,
            });

            RefreshShopPanel();
            RefreshIdentityPanel();
        }

        /// <summary>Called when the player clicks the spy activation button.</summary>
        public void OnSpyActivateClicked()
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;

            ShowDetailPopup(
                "卧底身份",
                "使用【卧底令】可隐藏真实身份，潜伏到其他门派中。\n\n" +
                "选择目标门派后，你将以该门派弟子身份活动，\n" +
                "但实际仍然效忠于原门派。\n\n" +
                "⚠ 卧底身份暴露将导致被逐出门派。",
                "需消耗: 卧底令 ×1",
                "使用卧底令",
                () =>
                {
                    // Placeholder — would show a sect selection list
                    // For now, activate spy with a default target
                    if (SectWarSystem.Instance != null)
                    {
                        SectType targetSect = SectType.QingYunMen; // Example
                        if (_currentSect.Value == targetSect)
                            targetSect = SectType.TianYuanZong;

                        SectWarSystem.Instance.ActivateSpyIdentity(
                            _currentPlayerId,
                            targetSect,
                            _currentSect.Value);
                    }

                    RefreshIdentityPanel();
                }
            );
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — Sect Lifecycle
        // ═══════════════════════════════════════════════════════════════════

        private void OnSectJoined(SectJoinedEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            // Apply reputation linkage via war system
            if (SectWarSystem.Instance != null)
            {
                SectWarSystem.Instance.OnPlayerJoinedSect(evt.PlayerId, evt.Sect);
            }

            RefreshAllData();
        }

        private void OnSectLeft(SectLeftEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;
            RefreshAllData();
        }

        private void OnSectExpelled(SectExpelledEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            ShowCrisisNotification("被逐出门派",
                $"你已被 {SectWarSystem.Instance?.GetDisplayName(evt.Sect) ?? evt.Sect.ToString()} 逐出！\n" +
                $"最终贡献: {evt.FinalContribution}\n\n已转为散修身份。",
                _crisisWarningColor);

            RefreshAllData();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — Contribution & Rank
        // ═══════════════════════════════════════════════════════════════════

        private void OnContributionGained(ContributionGainedEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            if (_currentTab == Tab.Identity)
                RefreshIdentityPanel();
            if (_currentTab == Tab.Shop)
                RefreshShopPanel();
        }

        private void OnContributionSpent(ContributionSpentEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            if (_currentTab == Tab.Identity)
                RefreshIdentityPanel();
            if (_currentTab == Tab.Shop)
                RefreshShopPanel();
        }

        private void OnRankPromoted(RankPromotedEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            if (_currentTab == Tab.Identity)
                RefreshIdentityPanel();
            if (_currentTab == Tab.Shop)
                RefreshShopPanel();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — Reputation & Diplomacy
        // ═══════════════════════════════════════════════════════════════════

        private void OnSectReputationChanged(SectReputationChangedEvent evt)
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;
            if (evt.SectA != _currentSect.Value && evt.SectB != _currentSect.Value) return;

            if (_currentTab == Tab.Identity)
                RefreshIdentityPanel();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — War & Crisis
        // ═══════════════════════════════════════════════════════════════════

        private void OnWarDeclared(WarDeclaredEvent evt)
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;

            if (_currentSect.Value == evt.Attacker || _currentSect.Value == evt.Defender)
            {
                bool isAttacker = _currentSect.Value == evt.Attacker;
                string side = isAttacker ? "我方" : "敌方";
                ShowCrisisNotification("门派战争",
                    $"{evt.AttackerDisplayName} 向 {evt.DefenderDisplayName} 宣战！\n\n" +
                    $"形式: {(evt.Form == WarForm.BattlefieldInstance ? "战场副本" : "资源点争夺")}\n" +
                    $"准备期: 24小时后开战",
                    _crisisWarningColor);
            }
        }

        private void OnWarSettled(WarSettledEvent evt)
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;

            if (_currentSect.Value == evt.Attacker || _currentSect.Value == evt.Defender)
            {
                bool isAttacker = _currentSect.Value == evt.Attacker;
                bool won = (isAttacker && evt.Result == WarResult.AttackerWin) ||
                           (!isAttacker && evt.Result == WarResult.DefenderWin);

                if (won)
                {
                    ShowCrisisNotification("战争胜利",
                        $"🎉 我方在战争中获胜！\n比分: {evt.AttackerFinalScore}:{evt.DefenderFinalScore}\n" +
                        $"获得赔偿: {evt.CompensationAmount} 灵石",
                        Color.green);
                }
                else if (evt.Result == WarResult.Draw)
                {
                    ShowCrisisNotification("战争平局",
                        $"战争以平局告终。\n比分: {evt.AttackerFinalScore}:{evt.DefenderFinalScore}",
                        _crisisWarningColor);
                }
                else
                {
                    ShowCrisisNotification("战争失败",
                        $"💀 我方在战争中失败！\n比分: {evt.AttackerFinalScore}:{evt.DefenderFinalScore}\n" +
                        $"赔偿: {evt.CompensationAmount} 灵石\n" +
                        (evt.TerritoriesTransferred.Count > 0 ? "失去部分领地" : ""),
                        _crisisDangerColor);
                }
            }
        }

        private void OnSectDestroyed(SectDestroyedEvent evt)
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;

            if (_currentSect.Value == evt.Sect)
            {
                ShowCrisisNotification("⚠ 门派被灭",
                    $"【{evt.DisplayName}】已被 {SectWarSystem.Instance?.GetDisplayName(evt.VictorSect)} 灭门！\n\n" +
                    "所有弟子被迫转为散修。\n" +
                    "门派贡献清零，门派身份失效。",
                    _crisisDangerColor);

                // Auto-convert to 散修 via war system
                if (SectWarSystem.Instance != null)
                {
                    SectWarSystem.Instance.HandleSectDestructionPlayer(_currentPlayerId, evt.Sect);
                }

                RefreshAllData();
            }
            else
            {
                // Show notification about another sect's destruction
                ShowCrisisNotification("门派覆灭",
                    $"【{evt.DisplayName}】已被 {SectWarSystem.Instance?.GetDisplayName(evt.VictorSect)} 灭门！\n" +
                    "天下格局已变...",
                    _crisisWarningColor);
            }
        }

        private void OnLeaderBetrayal(LeaderBetrayalEvent evt)
        {
            if (!_isInFormalSect || !_currentSect.HasValue) return;

            if (_currentSect.Value == evt.Sect)
            {
                ShowCrisisNotification("⚠ 掌门叛逃",
                    $"掌门【{evt.LeaderPlayerName}】叛逃！\n原因: {evt.Reason}\n\n" +
                    "门派进入为期7天的危机状态。\n" +
                    "门派任务奖励减半，商店价格上浮。",
                    _crisisDangerColor);
            }
        }

        private void OnSpyIdentityTriggered(SpyIdentityTriggeredEvent evt)
        {
            if (evt.PlayerId != _currentPlayerId) return;

            if (evt.IsExposed)
            {
                ShowCrisisNotification("卧底暴露",
                    $"你的卧底身份被揭穿！\n" +
                    $"在 {SectWarSystem.Instance?.GetDisplayName(evt.CoverSect)} 的潜伏行动失败。\n" +
                    "你已被逐出门派。",
                    _crisisDangerColor);
            }

            RefreshIdentityPanel();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — Daily Quest
        // ═══════════════════════════════════════════════════════════════════

        private void OnDailyQuestClaimed(SectDailyQuestClaimedEvent evt)
        {
            // Another system claimed a daily quest — refresh UI
            if (_currentTab == Tab.Quest)
                RefreshQuestPanel();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CRISIS NOTIFICATION POPUP
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Show a crisis / event notification popup.</summary>
        private void ShowCrisisNotification(string title, string message, Color color)
        {
            if (_crisisPopup == null) return;

            if (_crisisTitleText != null)
                _crisisTitleText.text = title;

            if (_crisisMessageText != null)
                _crisisMessageText.text = message;

            if (_crisisIconImage != null)
                _crisisIconImage.color = color;

            _crisisPopup.SetActive(true);

            // Auto-hide after a while if not dismissed
            StartCoroutine(AutoHideCrisis(15f));
        }

        private IEnumerator AutoHideCrisis(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_crisisPopup != null && _crisisPopup.activeSelf)
                _crisisPopup.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SAMPLE DATA GENERATORS (placeholder — real quest system TBD)
        // ═══════════════════════════════════════════════════════════════════

        private List<SectDailyQuestData> GenerateDailyQuests()
        {
            return new List<SectDailyQuestData>
            {
                new SectDailyQuestData { QuestId = "dq_1", QuestName = "采集灵药", Description = "前往灵药园采集10份灵草", ContributionReward = 20, SpiritStoneReward = 50 },
                new SectDailyQuestData { QuestId = "dq_2", QuestName = "巡逻山门", Description = "在山门周边巡逻，驱散入侵妖兽", ContributionReward = 15, SpiritStoneReward = 30 },
                new SectDailyQuestData { QuestId = "dq_3", QuestName = "切磋演练", Description = "与同门切磋3次，提升实战能力", ContributionReward = 25, SpiritStoneReward = 40 },
                new SectDailyQuestData { QuestId = "dq_4", QuestName = "整理藏经阁", Description = "协助整理藏经阁典籍，维护门派传承", ContributionReward = 10, SpiritStoneReward = 20 },
                new SectDailyQuestData { QuestId = "dq_5", QuestName = "炼制丹药", Description = "为门派炼制3枚基础丹药", ContributionReward = 30, SpiritStoneReward = 60 },
            };
        }

        private List<SectBountyData> GenerateBountyQuests()
        {
            return new List<SectBountyData>
            {
                new SectBountyData { BountyId = "bq_1", BountyName = "剿灭山贼", Description = "清剿盘踞在青风岭的山贼窝点，夺回被劫物资", ContributionReward = 50, SpiritStoneReward = 200, RequiredRealmLevel = 3, TimeRemainingHours = 48f },
                new SectBountyData { BountyId = "bq_2", BountyName = "追捕叛徒", Description = "追捕叛出门派的叛徒，带回门派令牌", ContributionReward = 80, SpiritStoneReward = 500, RequiredRealmLevel = 5, TimeRemainingHours = 72f },
                new SectBountyData { BountyId = "bq_3", BountyName = "探索秘境", Description = "探索新发现的秘境洞穴，带回秘境地图", ContributionReward = 100, SpiritStoneReward = 800, RequiredRealmLevel = 6, TimeRemainingHours = 120f },
            };
        }

        private List<SectShopItemData> GenerateShopItems()
        {
            return new List<SectShopItemData>
            {
                // Techniques
                new SectShopItemData { ItemId = "shop_tech_1", ItemName = "基础剑诀", Description = "入门级剑法秘籍", Category = "Technique", SpiritStoneCost = 500, ContributionCost = 50, RequiredRank = (int)SectRank.OuterDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_tech_2", ItemName = "玄天心法", Description = "内功心法，提升修炼速度", Category = "Technique", SpiritStoneCost = 2000, ContributionCost = 200, RequiredRank = (int)SectRank.InnerDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_tech_3", ItemName = "万剑归宗", Description = "高级剑技，大范围攻击", Category = "Technique", SpiritStoneCost = 10000, ContributionCost = 800, RequiredRank = (int)SectRank.CoreDisciple, IsAvailable = true },

                // Pills
                new SectShopItemData { ItemId = "shop_pill_1", ItemName = "聚气丹", Description = "基础修炼丹药", Category = "Pill", SpiritStoneCost = 100, ContributionCost = 20, RequiredRank = (int)SectRank.OuterDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_pill_2", ItemName = "筑基丹", Description = "筑基期突破辅助丹药", Category = "Pill", SpiritStoneCost = 1000, ContributionCost = 150, RequiredRank = (int)SectRank.InnerDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_pill_3", ItemName = "凝神丹", Description = "提升神识修为", Category = "Pill", SpiritStoneCost = 3000, ContributionCost = 300, RequiredRank = (int)SectRank.CoreDisciple, IsAvailable = true },

                // Equipment
                new SectShopItemData { ItemId = "shop_eq_1", ItemName = "青锋剑", Description = "制式门派长剑", Category = "Equipment", SpiritStoneCost = 800, ContributionCost = 80, RequiredRank = (int)SectRank.OuterDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_eq_2", ItemName = "玄铁护甲", Description = "精铁打造的内甲", Category = "Equipment", SpiritStoneCost = 3000, ContributionCost = 250, RequiredRank = (int)SectRank.InnerDisciple, IsAvailable = true },
                new SectShopItemData { ItemId = "shop_eq_3", ItemName = "灵风披风", Description = "提升身法速度", Category = "Equipment", SpiritStoneCost = 5000, ContributionCost = 400, RequiredRank = (int)SectRank.CoreDisciple, IsAvailable = true },
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DISPLAY HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private static string GetAttitudeDisplayName(SectAttitude attitude)
        {
            return attitude switch
            {
                SectAttitude.Hostile => "敌对",
                SectAttitude.Unfriendly => "不友好",
                SectAttitude.Neutral => "中立",
                SectAttitude.Friendly => "友好",
                SectAttitude.Allied => "同盟",
                _ => "未知",
            };
        }

        private static Color GetAttitudeColor(SectAttitude attitude)
        {
            return attitude switch
            {
                SectAttitude.Hostile => new Color(0.9f, 0.3f, 0.3f),
                SectAttitude.Unfriendly => new Color(0.9f, 0.6f, 0.3f),
                SectAttitude.Neutral => Color.white,
                SectAttitude.Friendly => new Color(0.3f, 0.8f, 0.3f),
                SectAttitude.Allied => new Color(0.3f, 0.6f, 0.9f),
                _ => Color.white,
            };
        }

        private static string GetRankName(SectRank rank)
        {
            return rank switch
            {
                SectRank.OuterDisciple => "外门弟子",
                SectRank.InnerDisciple => "内门弟子",
                SectRank.CoreDisciple => "核心弟子",
                SectRank.Elder => "长老",
                SectRank.Leader => "掌门",
                _ => "未知",
            };
        }
    }
}
