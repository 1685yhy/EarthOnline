using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EverythingCombiner
{
    /// <summary>
    /// 合成管理器 - 核心合成逻辑
    /// 负责：验证配方、执行合成、稀有度判定、合成历史记录
    /// </summary>
    public class SynthesisManager : MonoBehaviour
    {
        public static SynthesisManager Instance { get; private set; }

        [Header("配方数据库")]
        [SerializeField] private List<SynthesisRecipe> allRecipes = new List<SynthesisRecipe>();

        // 快速查找字典：key = "elementA_id|elementB_id"
        private Dictionary<string, SynthesisRecipe> recipeLookup;

        // 合成事件
        public event Action<ElementData, ElementData, ElementData, ElementRarity> OnSynthesisSuccess;
        public event Action<ElementData, ElementData> OnSynthesisFail;
        public event Action<ElementData> OnNewElementDiscovered;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildRecipeLookup();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 构建快速查找字典
        /// </summary>
        private void BuildRecipeLookup()
        {
            recipeLookup = new Dictionary<string, SynthesisRecipe>();
            foreach (var recipe in allRecipes)
            {
                if (recipe == null || recipe.elementA == null || recipe.elementB == null) continue;

                // 配方双向存储（A+B 和 B+A 都能匹配）
                string key1 = $"{recipe.elementA.elementId}|{recipe.elementB.elementId}";
                string key2 = $"{recipe.elementB.elementId}|{recipe.elementA.elementId}";

                if (!recipeLookup.ContainsKey(key1))
                    recipeLookup[key1] = recipe;
                if (!recipeLookup.ContainsKey(key2))
                    recipeLookup[key2] = recipe;
            }
        }

        /// <summary>
        /// 尝试合成两个元素
        /// </summary>
        /// <returns>合成结果元素（失败返回null）</returns>
        public ElementData TrySynthesize(ElementData a, ElementData b)
        {
            if (a == null || b == null) return null;

            string lookupKey = $"{a.elementId}|{b.elementId}";
            if (!recipeLookup.TryGetValue(lookupKey, out var recipe))
            {
                OnSynthesisFail?.Invoke(a, b);
                return null;
            }

            // 成功率判定
            float roll = Random.Range(0f, 100f);
            if (roll > recipe.successRate)
            {
                OnSynthesisFail?.Invoke(a, b);
                return null;
            }

            var result = recipe.result;
            if (result == null) return null;

            // 稀有度覆盖判定（部分配方可能有概率出更高稀有度）
            ElementRarity finalRarity = RollRarityOverride(result.rarity);

            // 触发事件
            OnSynthesisSuccess?.Invoke(a, b, result, finalRarity);

            // 检查是否新发现
            var playerData = SaveManager.Instance?.CurrentData;
            if (playerData != null && !playerData.discoveredElementIds.Contains(result.elementId))
            {
                playerData.discoveredElementIds.Add(result.elementId);
                playerData.totalDiscoveries++;
                OnNewElementDiscovered?.Invoke(result);
            }

            // 更新统计
            if (playerData != null)
            {
                playerData.totalSynthesisCount++;
            }

            return result;
        }

        /// <summary>
        /// 稀有度覆盖判定
        /// 基础稀有度有概率提升一档
        /// </summary>
        private ElementRarity RollRarityOverride(ElementRarity baseRarity)
        {
            // 神话级不再提升
            if (baseRarity == ElementRarity.Mythic) return baseRarity;

            // 稀有度提升概率：普通5%、稀有3%、史诗2%、传说1%
            float upgradeChance = baseRarity switch
            {
                ElementRarity.Common => 5f,
                ElementRarity.Rare => 3f,
                ElementRarity.Epic => 2f,
                ElementRarity.Legend => 1f,
                _ => 0f
            };

            if (Random.Range(0f, 100f) < upgradeChance)
            {
                return baseRarity + 1; // 提升一档
            }

            return baseRarity;
        }

        /// <summary>
        /// 获取可以与指定元素合成的所有可能配方
        /// </summary>
        public List<SynthesisRecipe> GetPossibleRecipes(ElementData element)
        {
            var results = new List<SynthesisRecipe>();
            foreach (var recipe in allRecipes)
            {
                if (recipe.elementA == element || recipe.elementB == element)
                {
                    // 检查结果是否已发现（用于提示系统）
                    results.Add(recipe);
                }
            }
            return results;
        }

        /// <summary>
        /// 获取未发现的合成提示（用于广告提示系统）
        /// </summary>
        public SynthesisRecipe GetUndiscoveredHint()
        {
            var playerData = SaveManager.Instance?.CurrentData;
            if (playerData == null) return null;

            var undiscovered = allRecipes.Where(r =>
                r.result != null &&
                playerData.discoveredElementIds.Contains(r.elementA.elementId) &&
                playerData.discoveredElementIds.Contains(r.elementB.elementId) &&
                !playerData.discoveredElementIds.Contains(r.result.elementId)
            ).ToList();

            if (undiscovered.Count == 0) return null;

            // 优先给稀有度高的提示
            undiscovered.Sort((a, b) => b.result.rarity.CompareTo(a.result.rarity));
            return undiscovered[Random.Range(0, Math.Min(3, undiscovered.Count))];
        }

        /// <summary>
        /// 获取合成统计
        /// </summary>
        public (int total, int discovered) GetProgress()
        {
            int total = allRecipes.Count;
            int discovered = SaveManager.Instance?.CurrentData?.totalDiscoveries ?? 0;
            return (total, discovered);
        }
    }
}
