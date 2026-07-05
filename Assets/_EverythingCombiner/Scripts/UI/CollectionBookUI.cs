using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EverythingCombiner
{
    /// <summary>
    /// 图鉴UI面板
    /// 展示所有已发现/未发现的元素，按类别分组
    /// </summary>
    public class CollectionBookUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Transform categoryTabsContainer;   // 类别标签容器
        [SerializeField] private Transform elementsGrid;            // 元素网格
        [SerializeField] private GameObject elementCellPrefab;      // 元素格子预制体
        [SerializeField] private GameObject categoryTabPrefab;      // 类别标签预制体
        [SerializeField] private TextMeshProUGUI progressText;      // 收集进度
        [SerializeField] private TextMeshProUGUI progressPercentText; // 收集百分比
        [SerializeField] private Slider progressBar;                // 进度条
        [SerializeField] private Button closeButton;

        [Header("稀有度筛选")]
        [SerializeField] private Toggle showAllToggle;
        [SerializeField] private Toggle showCommonToggle;
        [SerializeField] private Toggle showRareToggle;
        [SerializeField] private Toggle showEpicToggle;
        [SerializeField] private Toggle showLegendToggle;
        [SerializeField] private Toggle showMythicToggle;

        private ElementCategory currentCategory = ElementCategory.Basic;
        private ElementRarity? rarityFilter = null;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void Refresh()
        {
            UpdateProgress();
            RenderElements();
        }

        private void UpdateProgress()
        {
            var data = GameManager.Instance?.GetPlayerData();
            if (data == null) return;

            // TODO: 从ElementDatabase获取总元素数
            int total = 100; // 占位
            int discovered = data.totalDiscoveries;
            float percent = total > 0 ? (float)discovered / total : 0f;

            if (progressText != null)
                progressText.text = $"{discovered} / {total}";
            if (progressPercentText != null)
                progressPercentText.text = $"{percent:P1}";
            if (progressBar != null)
                progressBar.value = percent;
        }

        private void RenderElements()
        {
            if (elementsGrid == null || elementCellPrefab == null) return;

            // 清除现有
            foreach (Transform child in elementsGrid)
            {
                Destroy(child.gameObject);
            }

            // TODO: 从ElementDatabase获取所有元素，按类别和稀有度筛选
            // var elements = ElementDatabase.GetElementsByCategory(currentCategory);
            // 暂时演示
        }

        public void SelectCategory(int categoryIndex)
        {
            currentCategory = (ElementCategory)categoryIndex;
            Refresh();
        }

        public void SetRarityFilter(int rarityIndex)
        {
            if (rarityIndex < 0) rarityFilter = null;
            else rarityFilter = (ElementRarity)rarityIndex;
            Refresh();
        }
    }
}
