using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace EverythingCombiner
{
    /// <summary>
    /// 主界面UI管理器
    /// 管理：合成区域、元素列表、顶部状态栏、底部菜单
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("顶部状态栏")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI gemsText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI discoveryCountText;

        [Header("合成区域")]
        [SerializeField] private Transform synthesisArea;       // 合成台
        [SerializeField] private Image elementASlot;            // 元素A槽位
        [SerializeField] private Image elementBSlot;            // 元素B槽位
        [SerializeField] private Image resultSlot;              // 结果展示
        [SerializeField] private GameObject synthesizingEffect; // 合成中特效
        [SerializeField] private TextMeshProUGUI resultText;    // 结果文字

        [Header("元素网格")]
        [SerializeField] private Transform elementGrid;         // 已解锁元素网格
        [SerializeField] private GameObject elementButtonPrefab; // 元素按钮预制体

        [Header("弹窗预制体")]
        [SerializeField] private GameObject discoveryPopupPrefab;  // 新发现弹窗
        [SerializeField] private GameObject synthesisFailPopup;    // 合成失败提示
        [SerializeField] private GameObject shopPanel;             // 商店面板
        [SerializeField] private GameObject collectionBookPanel;   // 图鉴面板
        [SerializeField] private GameObject settingsPanel;         // 设置面板

        [Header("底部菜单")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button settingsButton;

        // 元素按钮缓存
        private Dictionary<string, ElementDragHandler> elementButtons = new Dictionary<string, ElementDragHandler>();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            // 绑定按钮事件
            if (shopButton != null) shopButton.onClick.AddListener(ToggleShop);
            if (collectionButton != null) collectionButton.onClick.AddListener(ToggleCollectionBook);
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
        }

        private void Start()
        {
            // 订阅游戏事件
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
                GameManager.Instance.OnGoldChanged.AddListener(UpdateGoldDisplay);
                GameManager.Instance.OnGemsChanged.AddListener(UpdateGemsDisplay);
                GameManager.Instance.OnElementDiscovered.AddListener(ShowDiscoveryPopup);
            }

            // 初始刷新
            RefreshAll();
        }

        // ── 状态变化处理 ──

        private void OnGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Idle:
                    ClearSynthesisSlots();
                    break;
                case GameState.Selecting:
                    // 元素A已选择，高亮
                    break;
                case GameState.ResultSuccess:
                    StartCoroutine(ShowSuccessResult());
                    break;
                case GameState.ResultFail:
                    StartCoroutine(ShowFailResult());
                    break;
            }
        }

        // ── 合成结果展示 ──

        private System.Collections.IEnumerator ShowSuccessResult()
        {
            if (synthesizingEffect != null)
                synthesizingEffect.SetActive(true);

            yield return new WaitForSeconds(0.5f);

            if (synthesizingEffect != null)
                synthesizingEffect.SetActive(false);

            RefreshUI();
        }

        private System.Collections.IEnumerator ShowFailResult()
        {
            if (synthesisFailPopup != null)
            {
                synthesisFailPopup.SetActive(true);
                yield return new WaitForSeconds(1.5f);
                synthesisFailPopup.SetActive(false);
            }
        }

        private void ClearSynthesisSlots()
        {
            if (elementASlot != null) elementASlot.sprite = null;
            if (elementBSlot != null) elementBSlot.sprite = null;
            if (resultSlot != null) resultSlot.sprite = null;
            if (resultText != null) resultText.text = "";
            if (synthesizingEffect != null) synthesizingEffect.SetActive(false);
        }

        // ── 发现弹窗 ──

        private void ShowDiscoveryPopup(ElementData element)
        {
            if (discoveryPopupPrefab == null) return;

            var popup = Instantiate(discoveryPopupPrefab, transform);
            var popupScript = popup.GetComponent<DiscoveryPopup>();
            if (popupScript != null)
            {
                popupScript.Show(element);
            }

            // 检查是否需要刷新元素网格
            RefreshElementGrid();
        }

        // ── UI刷新 ──

        public void RefreshAll()
        {
            UpdateCurrencyDisplay();
            UpdateEnergyDisplay();
            UpdateDiscoveryCount();
            RefreshElementGrid();
        }

        public void RefreshUI()
        {
            UpdateCurrencyDisplay();
            UpdateEnergyDisplay();
            UpdateDiscoveryCount();
        }

        private void UpdateCurrencyDisplay()
        {
            var data = GameManager.Instance?.GetPlayerData();
            if (data == null) return;
            UpdateGoldDisplay(data.gold);
            UpdateGemsDisplay(data.gems);
        }

        private void UpdateGoldDisplay(int gold)
        {
            if (goldText != null)
                goldText.text = $"🪙 {gold:N0}";
        }

        private void UpdateGemsDisplay(int gems)
        {
            if (gemsText != null)
                gemsText.text = $"💎 {gems}";
        }

        private void UpdateEnergyDisplay()
        {
            var data = GameManager.Instance?.GetPlayerData();
            if (data == null || energyText == null) return;
            energyText.text = $"⚡ {data.energy}/{data.maxEnergy}";
        }

        private void UpdateDiscoveryCount()
        {
            var data = GameManager.Instance?.GetPlayerData();
            if (data == null || discoveryCountText == null) return;
            discoveryCountText.text = $"📖 {data.totalDiscoveries}";
        }

        // ── 元素网格 ──

        private void RefreshElementGrid()
        {
            var data = GameManager.Instance?.GetPlayerData();
            if (data == null || elementGrid == null || elementButtonPrefab == null) return;

            // 清除已移除的元素
            var toRemove = new List<string>();
            foreach (var kvp in elementButtons)
            {
                if (!data.discoveredElementIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                Destroy(elementButtons[id].gameObject);
                elementButtons.Remove(id);
            }

            // 添加新发现的元素
            foreach (var elementId in data.discoveredElementIds)
            {
                if (!elementButtons.ContainsKey(elementId))
                {
                    // TODO: 从ElementDatabase加载ElementData，创建按钮
                    CreateElementButton(elementId);
                }
            }
        }

        private void CreateElementButton(string elementId)
        {
            var buttonObj = Instantiate(elementButtonPrefab, elementGrid);
            var handler = buttonObj.GetComponent<ElementDragHandler>();

            // TODO: 从数据库加载ElementData并初始化
            // var elementData = ElementDatabase.GetElement(elementId);
            // var icon = ElementDatabase.GetIcon(elementId);
            // handler.Initialize(elementData, icon);

            elementButtons[elementId] = handler;
        }

        // ── 面板切换 ──

        public void ToggleShop()
        {
            if (shopPanel != null)
                shopPanel.SetActive(!shopPanel.activeSelf);
        }

        public void ToggleCollectionBook()
        {
            if (collectionBookPanel != null)
            {
                bool active = !collectionBookPanel.activeSelf;
                collectionBookPanel.SetActive(active);
                if (active)
                {
                    var bookUI = collectionBookPanel.GetComponent<CollectionBookUI>();
                    bookUI?.Refresh();
                }
            }
        }

        public void ToggleSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        // ── 广告入口 ──

        public void OnWatchAdForHint()
        {
            AdManager.Instance?.ShowRewardedAd("hint", () =>
            {
                // 广告看完，给提示
                var recipe = SynthesisManager.Instance?.GetUndiscoveredHint();
                if (recipe != null)
                {
                    ShowHintPopup(recipe);
                }
            });
        }

        private void ShowHintPopup(SynthesisRecipe recipe)
        {
            if (resultText != null)
            {
                resultText.text = $"💡 试试: {recipe.elementA.elementName} + {recipe.elementB.elementName}";
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged.RemoveListener(OnGameStateChanged);
                GameManager.Instance.OnGoldChanged.RemoveListener(UpdateGoldDisplay);
                GameManager.Instance.OnGemsChanged.RemoveListener(UpdateGemsDisplay);
                GameManager.Instance.OnElementDiscovered.RemoveListener(ShowDiscoveryPopup);
            }
        }
    }
}
