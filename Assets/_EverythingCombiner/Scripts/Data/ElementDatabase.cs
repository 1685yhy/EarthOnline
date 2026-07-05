using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverythingCombiner
{
    /// <summary>
    /// 元素数据库
    /// 中央数据仓库：存储所有元素和配方，提供查询接口
    /// 运行时从Resources或Addressables加载ElementData和SynthesisRecipe资产
    /// </summary>
    [CreateAssetMenu(fileName = "ElementDatabase", menuName = "万物合成师/元素数据库")]
    public class ElementDatabase : ScriptableObject
    {
        [Header("所有元素")]
        [SerializeField] private List<ElementData> allElements = new List<ElementData>();

        [Header("所有配方")]
        [SerializeField] private List<SynthesisRecipe> allRecipes = new List<SynthesisRecipe>();

        // 快速查找字典
        private Dictionary<string, ElementData> elementById;
        private Dictionary<string, List<SynthesisRecipe>> recipesByElementId;

        /// <summary>
        /// 初始化数据库（在游戏启动时调用一次）
        /// </summary>
        public void Initialize()
        {
            BuildElementIndex();
            BuildRecipeIndex();
        }

        private void BuildElementIndex()
        {
            elementById = new Dictionary<string, ElementData>();
            foreach (var element in allElements)
            {
                if (element != null && !string.IsNullOrEmpty(element.elementId))
                {
                    if (!elementById.ContainsKey(element.elementId))
                        elementById[element.elementId] = element;
                }
            }
            Debug.Log($"[ElementDatabase] 加载了 {elementById.Count} 个元素");
        }

        private void BuildRecipeIndex()
        {
            recipesByElementId = new Dictionary<string, List<SynthesisRecipe>>();
            foreach (var recipe in allRecipes)
            {
                if (recipe == null || recipe.elementA == null || recipe.elementB == null) continue;

                // 按元素A索引
                AddRecipeToIndex(recipe.elementA.elementId, recipe);
                // 按元素B索引
                AddRecipeToIndex(recipe.elementB.elementId, recipe);
            }
            Debug.Log($"[ElementDatabase] 加载了 {allRecipes.Count} 条配方");
        }

        private void AddRecipeToIndex(string elementId, SynthesisRecipe recipe)
        {
            if (string.IsNullOrEmpty(elementId)) return;

            if (!recipesByElementId.ContainsKey(elementId))
                recipesByElementId[elementId] = new List<SynthesisRecipe>();

            if (!recipesByElementId[elementId].Contains(recipe))
                recipesByElementId[elementId].Add(recipe);
        }

        // ── 查询接口 ──

        /// <summary>
        /// 根据ID获取元素
        /// </summary>
        public ElementData GetElementById(string id)
        {
            if (elementById == null) BuildElementIndex();
            return elementById.TryGetValue(id, out var element) ? element : null;
        }

        /// <summary>
        /// 根据名称获取元素
        /// </summary>
        public ElementData GetElementByName(string name)
        {
            if (elementById == null) BuildElementIndex();
            return allElements.FirstOrDefault(e => e.elementName == name);
        }

        /// <summary>
        /// 获取所有元素
        /// </summary>
        public List<ElementData> GetAllElements()
        {
            return allElements;
        }

        /// <summary>
        /// 获取指定类别的所有元素
        /// </summary>
        public List<ElementData> GetElementsByCategory(ElementCategory category)
        {
            return allElements.Where(e => e.category == category).ToList();
        }

        /// <summary>
        /// 获取指定稀有度的所有元素
        /// </summary>
        public List<ElementData> GetElementsByRarity(ElementRarity rarity)
        {
            return allElements.Where(e => e.rarity == rarity).ToList();
        }

        /// <summary>
        /// 获取基础元素（火、水、土、风）
        /// </summary>
        public List<ElementData> GetBaseElements()
        {
            return allElements.Where(e => e.isBaseElement).ToList();
        }

        /// <summary>
        /// 获取涉及指定元素的所有配方
        /// </summary>
        public List<SynthesisRecipe> GetRecipesInvolving(string elementId)
        {
            if (recipesByElementId == null) BuildRecipeIndex();
            return recipesByElementId.TryGetValue(elementId, out var recipes)
                ? new List<SynthesisRecipe>(recipes)
                : new List<SynthesisRecipe>();
        }

        /// <summary>
        /// 获取涉及指定元素且结果未发现的配方
        /// </summary>
        public List<SynthesisRecipe> GetUndiscoveredRecipes(string elementId, HashSet<string> discoveredIds)
        {
            return GetRecipesInvolving(elementId)
                .Where(r => r.result != null && !discoveredIds.Contains(r.result.elementId))
                .ToList();
        }

        /// <summary>
        /// 获取所有配方
        /// </summary>
        public List<SynthesisRecipe> GetAllRecipes()
        {
            return allRecipes;
        }

        /// <summary>
        /// 获取总元素数
        /// </summary>
        public int TotalElementCount => allElements.Count;

        /// <summary>
        /// 获取总配方数
        /// </summary>
        public int TotalRecipeCount => allRecipes.Count;

        /// <summary>
        /// 查找配方（A+B → ?）
        /// </summary>
        public SynthesisRecipe FindRecipe(string elementAId, string elementBId)
        {
            return allRecipes.FirstOrDefault(r =>
                (r.elementA.elementId == elementAId && r.elementB.elementId == elementBId) ||
                (r.elementA.elementId == elementBId && r.elementB.elementId == elementAId));
        }

        /// <summary>
        /// 获取各稀有度统计
        /// </summary>
        public Dictionary<ElementRarity, int> GetRarityStats()
        {
            var stats = new Dictionary<ElementRarity, int>();
            foreach (ElementRarity rarity in System.Enum.GetValues(typeof(ElementRarity)))
            {
                stats[rarity] = allElements.Count(e => e.rarity == rarity);
            }
            return stats;
        }
    }
}
