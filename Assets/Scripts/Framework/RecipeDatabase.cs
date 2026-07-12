using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EarthOnline.Core;

namespace EarthOnline.Framework
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Enums
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>配方获取途径 (5种)</summary>
    public enum RecipeSourceType
    {
        Basic,          // 基础 — 初始解锁
        Sect,           // 门派 — 门派贡献兑换
        Exploration,    // 探索 — 地图探索/秘境/奇遇
        Boss,           // BOSS — 首领掉落
        SelfCreated     // 自创 — 变异成功记录
    }

    /// <summary>配方类型分类</summary>
    public enum RecipeCategory
    {
        None,
        Pill,           // 丹药
        Elixir,         // 灵液
        Powder,         // 药散
        Balm,           // 药膏
        Special         // 特殊
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Events (Story 006: 配方系统)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Published when a new recipe is unlocked.</summary>
    public struct RecipeUnlockedEvent
    {
        public string RecipeId;
        public string RecipeName;
        public RecipeSourceType SourceType;
        public RecipeCategory Category;
        public bool IsSelfCreated;
    }

    /// <summary>Published when a mutation creates a new self-created recipe.</summary>
    public struct RecipeMutationCreatedEvent
    {
        public string OriginalRecipeId;
        public string OriginalRecipeName;
        public string NewRecipeId;
        public string NewRecipeName;
        public string Description;
    }

    /// <summary>Published when favorite state toggles.</summary>
    public struct RecipeFavoriteToggledEvent
    {
        public string RecipeId;
        public string RecipeName;
        public bool IsFavorite;
    }

    /// <summary>Published when searching/filtering recipes (UI refresh).</summary>
    public struct RecipeSearchResultEvent
    {
        public int TotalResults;
        public int KnownCount;
        public string Query;
    }

    /// <summary>Published when a recipe is shared or sold.</summary>
    public struct RecipeSharedEvent
    {
        public string RecipeId;
        public string RecipeName;
        public bool IsSold;         // false = free share, true = sold
        public int Price;
    }

    /// <summary>Published when first time crafting a recipe (bonus exp).</summary>
    public struct RecipeFirstCraftEvent
    {
        public string RecipeId;
        public string RecipeName;
        public float BonusProficiency;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Data Structures (JSON-serializable)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>配方所需材料</summary>
    [Serializable]
    public struct RecipeMaterial
    {
        public string ItemId;
        public string DisplayName;
        public int Quantity;            // 需要数量
        public bool IsConsumed;         // true=消耗, false=仅需要持有(如丹炉)
    }

    /// <summary>配方可能产出</summary>
    [Serializable]
    public struct RecipeResultEntry
    {
        public string ItemId;
        public string ItemName;
        public string Description;      // 物品描述
        public float Weight;            // 权重 (越高越可能产出)
        public PillQuality MinQuality;  // 最低品质
        public bool IsSpecial;          // 特殊产出 (隐藏/稀有)
    }

    /// <summary>变异可能产出</summary>
    [Serializable]
    public struct RecipeMutationEntry
    {
        public string ItemId;
        public string ItemName;
        public string Description;      // 变异描述文本
        public float Weight;            // 权重
    }

    /// <summary>
    /// 完整配方数据 (JSON 配置映射)
    /// 与 AlchemyController.AlchemyRecipeData 兼容,
    /// 但更完整, 包含获取途径/结果池/搜索标签等.
    /// </summary>
    [Serializable]
    public class RecipeEntry
    {
        // ─── 基础信息 ───
        public string Id;
        public string DisplayName;
        public string Description;
        public RecipeCategory Category;
        public RecipeSourceType SourceType;

        // ─── 炼制参数 (与 AlchemyRecipeData 对应) ───
        public float OptimalTemperature;
        public float Duration;
        public float BaseQualityMin;
        public float BaseQualityMax;
        public int Difficulty;
        public int RequiredProficiency;

        // ─── 材料 ───
        public RecipeMaterial[] Materials;
        public string[] RecommendedOrder;       // 推荐投料顺序

        // ─── 结果池 ───
        public RecipeResultEntry[] ResultPool;
        public RecipeMutationEntry[] MutationPool;   // 变异产出池

        // ─── 获取信息 ───
        public string SourceHint;               // 获取途径提示文本, e.g. "击杀烈焰蛟龙掉落"
        public string SourceDetail;             // 详细指引
        public string[] Tags;                   // 搜索标签 e.g. ["修炼", "突破", "回复"]

        // ─── 自创配方特有 ───
        public string CreatorPlayerId;          // 自创玩家的 ID
        public string CreatorPlayerName;        // 自创玩家名称
        public bool IsPublic;                   // 是否公开分享
        public int SalePrice;                   // 出售价格 (灵石)
        public bool IsOriginalRecipe;           // 是否是原始配方 (vs 变异衍生)

        // ─── 运行时状态 (不序列化到 JSON) ───
        [NonSerialized] public bool IsFavorite;
        [NonSerialized] public bool HasBeenCrafted;
        [NonSerialized] public int TimesCrafted;

        /// <summary>转换为 AlchemyRecipeData 供 AlchemyController 使用</summary>
        public AlchemyRecipeData ToAlchemyRecipeData()
        {
            return new AlchemyRecipeData
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                OptimalTemperature = OptimalTemperature,
                Duration = Duration,
                BaseQualityMin = BaseQualityMin,
                BaseQualityMax = BaseQualityMax,
                RecommendedOrder = RecommendedOrder ?? Array.Empty<string>(),
                Difficulty = Difficulty,
                RequiredProficiency = RequiredProficiency
            };
        }

        /// <summary>获取来源类型的显示名称</summary>
        public string GetSourceTypeDisplay()
        {
            return SourceType switch
            {
                RecipeSourceType.Basic => "基础配方",
                RecipeSourceType.Sect => "门派配方",
                RecipeSourceType.Exploration => "探索配方",
                RecipeSourceType.Boss => "首领配方",
                RecipeSourceType.SelfCreated => "自创配方",
                _ => "未知来源"
            };
        }
    }

    /// <summary>JSON 根结构</summary>
    [Serializable]
    public class RecipeDatabaseJson
    {
        public RecipeJsonEntry[] Recipes;
    }

    /// <summary>JSON 单条配方 (反序列化中间格式)</summary>
    [Serializable]
    public class RecipeJsonEntry
    {
        public string id;
        public string displayName;
        public string description;
        public string category;
        public string sourceType;
        public string creatorPlayerId;
        public string creatorPlayerName;
        public int salePrice;
        public bool isOriginalRecipe = true;

        public float optimalTemperature;
        public float duration;
        public float baseQualityMin;
        public float baseQualityMax;
        public int difficulty;
        public int requiredProficiency;

        public RecipeMaterialJson[] materials;
        public string[] recommendedOrder;

        public RecipeResultJson[] resultPool;
        public RecipeMutationJson[] mutationPool;

        public string sourceHint;
        public string sourceDetail;
        public string[] tags;

        /// <summary>反序列化为运行时 RecipeEntry</summary>
        public RecipeEntry ToRuntime()
        {
            var entry = new RecipeEntry
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Category = ParseCategory(category),
                SourceType = ParseSourceType(sourceType),
                OptimalTemperature = optimalTemperature,
                Duration = duration,
                BaseQualityMin = baseQualityMin,
                BaseQualityMax = baseQualityMax,
                Difficulty = difficulty,
                RequiredProficiency = requiredProficiency,
                SourceHint = sourceHint,
                SourceDetail = sourceDetail,
                Tags = tags ?? Array.Empty<string>(),
                CreatorPlayerId = creatorPlayerId,
                CreatorPlayerName = creatorPlayerName,
                IsPublic = "false",
                SalePrice = salePrice,
                IsOriginalRecipe = isOriginalRecipe
            };

            // Materials
            if (materials != null)
            {
                entry.Materials = materials.Select(m => new RecipeMaterial
                {
                    ItemId = m.itemId,
                    DisplayName = m.displayName,
                    Quantity = m.quantity,
                    IsConsumed = m.isConsumed
                }).ToArray();
            }
            else
            {
                entry.Materials = Array.Empty<RecipeMaterial>();
            }

            // Recommended order
            entry.RecommendedOrder = recommendedOrder ?? Array.Empty<string>();

            // Result pool
            if (resultPool != null)
            {
                entry.ResultPool = resultPool.Select(r => new RecipeResultEntry
                {
                    ItemId = r.itemId,
                    ItemName = r.itemName,
                    Description = r.description,
                    Weight = r.weight,
                    MinQuality = ParseQuality(r.minQuality),
                    IsSpecial = r.isSpecial
                }).ToArray();
            }
            else
            {
                entry.ResultPool = Array.Empty<RecipeResultEntry>();
            }

            // Mutation pool
            if (mutationPool != null)
            {
                entry.MutationPool = mutationPool.Select(m => new RecipeMutationEntry
                {
                    ItemId = m.itemId,
                    ItemName = m.itemName,
                    Description = m.description,
                    Weight = m.weight
                }).ToArray();
            }
            else
            {
                entry.MutationPool = Array.Empty<RecipeMutationEntry>();
            }

            return entry;
        }

        private static RecipeCategory ParseCategory(string s)
        {
            return s?.ToLowerInvariant() switch
            {
                "pill" => RecipeCategory.Pill,
                "elixir" => RecipeCategory.Elixir,
                "powder" => RecipeCategory.Powder,
                "balm" => RecipeCategory.Balm,
                "special" => RecipeCategory.Special,
                _ => RecipeCategory.None
            };
        }

        private static RecipeSourceType ParseSourceType(string s)
        {
            return s?.ToLowerInvariant() switch
            {
                "basic" => RecipeSourceType.Basic,
                "sect" => RecipeSourceType.Sect,
                "exploration" => RecipeSourceType.Exploration,
                "boss" => RecipeSourceType.Boss,
                "selfcreated" or "self_created" => RecipeSourceType.SelfCreated,
                _ => RecipeSourceType.Exploration
            };
        }

        private static PillQuality ParseQuality(string s)
        {
            return s?.ToLowerInvariant() switch
            {
                "fail" => PillQuality.Fail,
                "low" => PillQuality.Low,
                "mid" => PillQuality.Mid,
                "high" => PillQuality.High,
                "legendary" => PillQuality.Legendary,
                _ => PillQuality.Low
            };
        }
    }

    /// <summary>JSON 材料格式</summary>
    [Serializable]
    public class RecipeMaterialJson
    {
        public string itemId;
        public string displayName;
        public int quantity = 1;
        public bool isConsumed = true;
    }

    /// <summary>JSON 结果格式</summary>
    [Serializable]
    public class RecipeResultJson
    {
        public string itemId;
        public string itemName;
        public string description;
        public float weight = 1f;
        public string minQuality = "Low";
        public bool isSpecial;
    }

    /// <summary>JSON 变异产出格式</summary>
    [Serializable]
    public class RecipeMutationJson
    {
        public string itemId;
        public string itemName;
        public string description;
        public float weight = 1f;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Search / Filter Structs
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>配方搜索条件</summary>
    public struct RecipeSearchQuery
    {
        public string Keyword;              // 关键词 (匹配名称/描述/标签)
        public RecipeCategory? Category;    // null = 不限
        public RecipeSourceType? SourceType;// null = 不限
        public int MinDifficulty;           // 最小难度 (含)
        public int MaxDifficulty;           // 最大难度 (含)
        public bool OnlyKnown;              // 仅已解锁
        public bool OnlyFavorites;          // 仅收藏
        public bool OnlySelfCreated;        // 仅自创
        public bool IncludeUnknown;         // 包含未解锁 (和 OnlyKnown 互斥)
        public RecipeSortMode SortBy;       // 排序方式
        public bool Ascending;              // 升序

        public static RecipeSearchQuery Default => new RecipeSearchQuery
        {
            Keyword = "",
            Category = null,
            SourceType = null,
            MinDifficulty = 1,
            MaxDifficulty = 10,
            OnlyKnown = true,
            OnlyFavorites = "false",
            OnlySelfCreated = "false",
            IncludeUnknown = "false",
            SortBy = RecipeSortMode.Difficulty,
            Ascending = true
        };
    }

    /// <summary>配方排序方式</summary>
    public enum RecipeSortMode
    {
        Default,
        Name,
        Difficulty,
        Category,
        SourceType,
        DateUnlocked,
        TimesCrafted
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  RecipeDatabase — 配方管理核心
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Story 006: 配方系统 + 变异
    ///
    /// 功能:
    /// - 5种获取途径 (基础 / 门派 / 探索 / BOSS / 自创)
    /// - 从 JSON 配置文件加载配方
    /// - 变异判定: 非标准投料 => 变异可能
    /// - 自创配方记录 / 分享 / 出售
    /// - 首次炼制新配方 +10 额外熟练度
    /// - 配方搜索与收藏
    /// </summary>
    public class RecipeDatabase : MonoBehaviour
    {
        #region Singleton

        public static RecipeDatabase Instance { get; private set; }

        #endregion

        #region Inspector Configuration

        [Header("=== 配方案例配置 ===")]

        [Header("JSON 路径")]
        [SerializeField, Tooltip("Resources 路径 (不含扩展名)")]
        private string recipeJsonResourcesPath = "Data/Recipes/recipe_database";

        [Header("变异系统")]
        [SerializeField]
        [Tooltip("MutationChance = mutationBaseRate × (proficiencyLevel / 100)")]
        private float mutationBaseRate = 0.15f;

        [SerializeField, Tooltip("最小变异触发所需错误数")]
        private int minErrorsForMutation = 2;

        [Header("自创配方")]
        [SerializeField, Tooltip("自创配方默认售价")]
        private int defaultSelfCreatedPrice = 500;

        [Header("首次炼制奖励")]
        [SerializeField, Tooltip("首次炼制新配方额外熟练度")]
        private float firstCraftBonusProficiency = 10f;

        #endregion

        #region Private State

        // ─── 配方库 ───
        private Dictionary<string, RecipeEntry> _allRecipes = new(StringComparer.OrdinalIgnoreCase);
        private List<RecipeEntry> _recipeList = new();

        // ─── 玩家已解锁 ───
        private HashSet<string> _knownRecipeIds = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _favoriteRecipeIds = new(StringComparer.OrdinalIgnoreCase);

        // ─── 自创配方 ───
        private List<RecipeEntry> _selfCreatedRecipes = new();
        private int _nextSelfCreatedIndex = 1;

        // ─── UI 缓存 ───
        private RecipeSearchQuery _lastSearchQuery;
        private List<RecipeEntry> _lastSearchResults = new();

        // ─── JSON 加载状态 ───
        private bool _isLoaded;

        #endregion

        #region Public Properties

        public bool IsLoaded => _isLoaded;
        public int TotalRecipeCount => _allRecipes.Count;
        public int KnownRecipeCount => _knownRecipeIds.Count;
        public int SelfCreatedCount => _selfCreatedRecipes.Count;
        public int FavoriteCount => _favoriteRecipeIds.Count;
        public IReadOnlyList<RecipeEntry> SelfCreatedRecipes => _selfCreatedRecipes;
        public IReadOnlyCollection<string> KnownRecipeIds => _knownRecipeIds;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            LoadAllRecipes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════════
        //  配方加载
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 从 Resources 加载所有配方 JSON
        /// </summary>
        public void LoadAllRecipes()
        {
            try
            {
                TextAsset jsonAsset = Resources.Load<TextAsset>(recipeJsonResourcesPath);
                if (jsonAsset == null)
                {
                    Debug.LogWarning($"[RecipeDatabase] 未找到配方案例: {recipeJsonResourcesPath}");

                    // 加载内建默认配方 (防止空库)
                    LoadBuiltinRecipes();
                    return;
                }

                var wrapper = JsonUtility.FromJson<RecipeDatabaseJson>(jsonAsset.text);
                if (wrapper?.Recipes == null || wrapper.Recipes.Length == 0)
                {
                    Debug.LogWarning("[RecipeDatabase] 配方案例为空, 回退到内建配方");
                    LoadBuiltinRecipes();
                    return;
                }

                _allRecipes.Clear();
                _recipeList.Clear();

                foreach (var jsonEntry in wrapper.Recipes)
                {
                    var recipe = jsonEntry.ToRuntime();
                    _allRecipes[recipe.Id] = recipe;
                    _recipeList.Add(recipe);
                }

                _isLoaded = true;
                Debug.Log($"[RecipeDatabase] 从 JSON 加载 {_recipeList.Count} 个配方");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecipeDatabase] 加载配方案例失败: {ex.Message}");
                LoadBuiltinRecipes();
            }
        }

        /// <summary>
        /// 从 JSON 字符串加载 (测试 / 网络更新用)
        /// </summary>
        public void LoadRecipesFromJson(string jsonText)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<RecipeDatabaseJson>(jsonText);
                if (wrapper?.Recipes == null) return;

                foreach (var jsonEntry in wrapper.Recipes)
                {
                    var recipe = jsonEntry.ToRuntime();
                    _allRecipes[recipe.Id] = recipe;

                    // 替换已存在的
                    int existing = _recipeList.FindIndex(r => r.Id == recipe.Id);
                    if (existing >= 0)
                        _recipeList[existing] = recipe;
                    else
                        _recipeList.Add(recipe);
                }

                _isLoaded = true;
                Debug.Log($"[RecipeDatabase] 从 JSON 文本加载 {wrapper.Recipes.Length} 个配方");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecipeDatabase] JSON 解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 动态注册一个配方 (测试 / 自创配方)
        /// </summary>
        public void RegisterRecipe(RecipeEntry recipe)
        {
            if (_allRecipes.ContainsKey(recipe.Id))
            {
                Debug.LogWarning($"[RecipeDatabase] 配方已存在: {recipe.Id}, 将被覆盖");
            }

            _allRecipes[recipe.Id] = recipe;

            int idx = _recipeList.FindIndex(r => r.Id == recipe.Id);
            if (idx >= 0)
                _recipeList[idx] = recipe;
            else
                _recipeList.Add(recipe);
        }

        /// <summary>
        /// 内建默认配方 (当 JSON 缺失时的兜底)
        /// </summary>
        private void LoadBuiltinRecipes()
        {
            _allRecipes.Clear();
            _recipeList.Clear();

            AddBuiltinRecipe("recipe_juqi", "聚气丹", "基础修炼丹药，聚气凝神",
                RecipeCategory.Pill, RecipeSourceType.Basic, 150f, 50f, 0.3f, 0.8f,
                new[] { "mat_herb_01", "mat_root_02", "mat_essence_03" },
                new[] { new RecipeMaterial { ItemId = "mat_herb_01", DisplayName = "灵草", Quantity = 2 },
                        new RecipeMaterial { ItemId = "mat_root_02", DisplayName = "灵根", Quantity = 1 },
                        new RecipeMaterial { ItemId = "mat_essence_03", DisplayName = "灵粹", Quantity = 1 } },
                1, 1, "初始解锁");

            AddBuiltinRecipe("recipe_hunxue", "凝血丹", "疗伤止血丹药",
                RecipeCategory.Pill, RecipeSourceType.Basic, 120f, 40f, 0.3f, 0.75f,
                new[] { "mat_herb_01", "mat_blood_02", "mat_essence_03" },
                new[] { new RecipeMaterial { ItemId = "mat_herb_01", DisplayName = "灵草", Quantity = 1 },
                        new RecipeMaterial { ItemId = "mat_blood_02", DisplayName = "兽血", Quantity = 1 },
                        new RecipeMaterial { ItemId = "mat_essence_03", DisplayName = "灵粹", Quantity = 1 } },
                1, 1, "初始解锁");

            AddBuiltinRecipe("recipe_bigu", "辟谷丹", "三日不饥，适合远行",
                RecipeCategory.Pill, RecipeSourceType.Sect, 130f, 45f, 0.35f, 0.8f,
                new[] { "mat_grain_01", "mat_herb_01", "mat_root_02" },
                new[] { new RecipeMaterial { ItemId = "mat_grain_01", DisplayName = "灵谷", Quantity = 3 },
                        new RecipeMaterial { ItemId = "mat_herb_01", DisplayName = "灵草", Quantity = 1 } },
                2, 5, "门派贡献兑换 (500贡献)");

            AddBuiltinRecipe("recipe_jindan", "金元丹", "金丹期修炼圣药",
                RecipeCategory.Pill, RecipeSourceType.Exploration, 200f, 70f, 0.4f, 0.9f,
                new[] { "mat_gold_flower", "mat_spirit_essence", "mat_phoenix_feather" },
                new[] { new RecipeMaterial { ItemId = "mat_gold_flower", DisplayName = "金线花", Quantity = 2 },
                        new RecipeMaterial { ItemId = "mat_spirit_essence", DisplayName = "灵髓", Quantity = 1 } },
                5, 20, "探索秘境·幻灵谷深处");

            AddBuiltinRecipe("recipe_longli", "龙力丹", "大幅提升力量的远古丹药",
                RecipeCategory.Pill, RecipeSourceType.Boss, 280f, 90f, 0.5f, 1.0f,
                new[] { "mat_dragon_scale", "mat_phoenix_feather", "mat_spirit_essence", "mat_gold_flower" },
                new[] { new RecipeMaterial { ItemId = "mat_dragon_scale", DisplayName = "龙鳞", Quantity = 1 },
                        new RecipeMaterial { ItemId = "mat_phoenix_feather", DisplayName = "凤羽", Quantity = 1 },
                        new RecipeMaterial { ItemId = "mat_spirit_essence", DisplayName = "灵髓", Quantity = 2 } },
                8, 40, "击杀烈焰蛟龙 (20%掉落)");

            _isLoaded = true;
            Debug.Log($"[RecipeDatabase] 加载内建配方 {_recipeList.Count} 个");
        }

        private void AddBuiltinRecipe(string id, string name, string desc, RecipeCategory cat,
                                       RecipeSourceType src, float temp, float dur,
                                       float minQ, float maxQ, string[] order,
                                       RecipeMaterial[] materials, int diff, int reqProf,
                                       string hint)
        {
            var recipe = new RecipeEntry
            {
                Id = id,
                DisplayName = name,
                Description = desc,
                Category = cat,
                SourceType = src,
                OptimalTemperature = temp,
                Duration = dur,
                BaseQualityMin = minQ,
                BaseQualityMax = maxQ,
                Difficulty = diff,
                RequiredProficiency = reqProf,
                RecommendedOrder = order,
                Materials = materials,
                ResultPool = new[]
                {
                    new RecipeResultEntry
                    {
                        ItemId = id + "_result",
                        ItemName = name,
                        Description = desc,
                        Weight = "1f",
                        MinQuality = PillQuality.Low,
                        IsSpecial = false
                    }
                },
                SourceHint = hint,
                Tags = new[] { "修炼", cat == RecipeCategory.Pill ? "丹药" : "特殊" },
                IsOriginalRecipe = true
            };

            _allRecipes[id] = recipe;
            _recipeList.Add(recipe);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  配方查询
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>获取配方</summary>
        public RecipeEntry GetRecipe(string recipeId)
        {
            _allRecipes.TryGetValue(recipeId, out var recipe);
            return recipe;
        }

        /// <summary>获取所有配方 (只读)</summary>
        public List<RecipeEntry> GetAllRecipes()
        {
            return new List<RecipeEntry>(_recipeList);
        }

        /// <summary>配方是否已解锁</summary>
        public bool IsRecipeKnown(string recipeId)
        {
            return _knownRecipeIds.Contains(recipeId);
        }

        /// <summary>获取已解锁配方列表</summary>
        public List<RecipeEntry> GetKnownRecipes()
        {
            var result = new List<RecipeEntry>();
            foreach (var recipe in _recipeList)
            {
                if (_knownRecipeIds.Contains(recipe.Id))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>获取未解锁配方列表 (含获取途径提示)</summary>
        public List<RecipeEntry> GetUnknownRecipes()
        {
            var result = new List<RecipeEntry>();
            foreach (var recipe in _recipeList)
            {
                if (!_knownRecipeIds.Contains(recipe.Id) && recipe.SourceType != RecipeSourceType.SelfCreated)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>按获取途径筛选配方</summary>
        public List<RecipeEntry> GetRecipesBySource(RecipeSourceType sourceType)
        {
            var result = new List<RecipeEntry>();
            foreach (var recipe in _recipeList)
            {
                if (recipe.SourceType == sourceType)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>按品级筛选配方</summary>
        public List<RecipeEntry> GetRecipesByCategory(RecipeCategory category)
        {
            var result = new List<RecipeEntry>();
            foreach (var recipe in _recipeList)
            {
                if (recipe.Category == category)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>获取某一获取途径的已解锁数量</summary>
        public int GetKnownCountBySource(RecipeSourceType sourceType)
        {
            int count = 0;
            foreach (var id in _knownRecipeIds)
            {
                if (_allRecipes.TryGetValue(id, out var recipe) && recipe.SourceType == sourceType)
                    count++;
            }
            return count;
        }

        /// <summary>获取某一获取途径的配方总数</summary>
        public int GetTotalCountBySource(RecipeSourceType sourceType)
        {
            int count = 0;
            foreach (var recipe in _recipeList)
            {
                if (recipe.SourceType == sourceType)
                    count++;
            }
            return count;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  配方解锁
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 解锁配方, 返回是否成功 (已经解锁的不会重复触发)
        /// </summary>
        public bool UnlockRecipe(string recipeId, RecipeSourceType? overrideSource = null)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe))
            {
                Debug.LogWarning($"[RecipeDatabase] 配方不存在: {recipeId}");
                return false;
            }

            if (_knownRecipeIds.Contains(recipeId))
                return false; // 已解锁, 不重复触发

            _knownRecipeIds.Add(recipeId);
            recipe.HasBeenCrafted = false;

            // 如果外部指定了解锁来源, 覆盖
            if (overrideSource.HasValue)
                recipe.SourceType = overrideSource.Value;

            EventBus.Publish(new RecipeUnlockedEvent
            {
                RecipeId = recipeId,
                RecipeName = recipe.DisplayName,
                SourceType = recipe.SourceType,
                Category = recipe.Category,
                IsSelfCreated = recipe.SourceType == RecipeSourceType.SelfCreated
            });

            Debug.Log($"[RecipeDatabase] 解锁配方: {recipe.DisplayName} ({recipe.GetSourceTypeDisplay()})");
            return true;
        }

        /// <summary>批量解锁配方</summary>
        public int UnlockRecipes(IEnumerable<string> recipeIds)
        {
            int count = 0;
            foreach (var id in recipeIds)
            {
                if (UnlockRecipe(id))
                    count++;
            }
            return count;
        }

        /// <summary>重置所有解锁状态</summary>
        public void ResetAllUnlocked()
        {
            _knownRecipeIds.Clear();
            _favoriteRecipeIds.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  首次炼制
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 标记配方为 "已炼制", 返回首次炼制额外熟练度 (首次 = "10", 非首次=0)
        /// </summary>
        public float MarkRecipeCrafted(string recipeId)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe))
                return 0f;

            recipe.TimesCrafted++;

            if (!recipe.HasBeenCrafted)
            {
                recipe.HasBeenCrafted = true;

                EventBus.Publish(new RecipeFirstCraftEvent
                {
                    RecipeId = recipeId,
                    RecipeName = recipe.DisplayName,
                    BonusProficiency = firstCraftBonusProficiency
                });

                return firstCraftBonusProficiency;
            }

            return 0f;
        }

        /// <summary>
        /// 检查是否是首次炼制 (不修改状态)
        /// </summary>
        public bool IsFirstCraft(string recipeId)
        {
            return _allRecipes.TryGetValue(recipeId, out var recipe) && !recipe.HasBeenCrafted;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  变异系统
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 计算变异概率
        /// Formula: MutationChance = mutationBaseRate × (proficiencyLevel / 100)
        /// 投料顺序与标准不同时触发
        /// </summary>
        public float CalculateMutationChance(int proficiencyLevel, int orderErrors, bool hasExtraMaterials)
        {
            // 基础条件: 投料偏离标准
            if (orderErrors < minErrorsForMutation && !hasExtraMaterials)
                return 0f;

            // Chance = 0.15 × (proficiency / 100)
            float profFactor = Mathf.Clamp01(proficiencyLevel / 100f);
            float chance = mutationBaseRate * profFactor;

            // 额外系数: 错误越多, 概率略增
            float errorBonus = Mathf.Min(orderErrors * 0.02f, 0.1f);
            if (hasExtraMaterials)
                errorBonus += 0.05f;

            return Mathf.Clamp01(chance + errorBonus);
        }

        /// <summary>
        /// 触发变异判定, 返回变异是否成功及产出
        /// </summary>
        public (bool success, RecipeMutationEntry outcome, string newRecipeId)
            TryTriggerMutation(string recipeId, int proficiencyLevel, int orderErrors, bool hasExtraMaterials)
        {
            float chance = CalculateMutationChance(proficiencyLevel, orderErrors, hasExtraMaterials);
            bool success = UnityEngine.Random.value < chance;

            if (!success)
                return (false, default, null);

            // 获取变异产出池
            if (!_allRecipes.TryGetValue(recipeId, out var recipe))
                return (false, default, null);

            if (recipe.MutationPool == null || recipe.MutationPool.Length == 0)
            {
                // 没有配置变异池, 使用默认产出
                var defaultMutation = new RecipeMutationEntry
                {
                    ItemId = recipeId + "_mutant",
                    ItemName = recipe.DisplayName + "·异变",
                    Description = "非标准投料引发的变异产物",
                    Weight = 1f
                };
                return (true, defaultMutation, recipeId + "_mutant");
            }

            // 加权随机选取变异结果
            RecipeMutationEntry outcome = WeightedPickMutation(recipe.MutationPool);
            string newRecipeId = outcome.ItemId;

            // 如果是 Recipe 类型变异 (自创配方), 自动生成自创配方
            if (recipe.SourceType != RecipeSourceType.SelfCreated)
            {
                CreateSelfCreatedRecipeFromMutation(recipe, outcome);
            }

            return (true, outcome, newRecipeId);
        }

        /// <summary>从变异产出创建自创配方</summary>
        private RecipeEntry CreateSelfCreatedRecipeFromMutation(RecipeEntry original, RecipeMutationEntry mutation)
        {
            string newId = "self_" + Guid.NewGuid().ToString("N");

            var selfRecipe = new RecipeEntry
            {
                Id = newId,
                DisplayName = "自创·" + original.DisplayName,
                Description = mutation.Description ?? "通过变异实验自创的配方",
                Category = original.Category,
                SourceType = RecipeSourceType.SelfCreated,
                OptimalTemperature = original.OptimalTemperature + UnityEngine.Random.Range(-10f, 10f),
                Duration = original.Duration,
                BaseQualityMin = Mathf.Max(0.3f, original.BaseQualityMin - 0.1f),
                BaseQualityMax = Mathf.Min(1.0f, original.BaseQualityMax + 0.05f),
                Difficulty = Mathf.Min(10, original.Difficulty + 1),
                RequiredProficiency = original.RequiredProficiency,
                Materials = original.Materials,
                RecommendedOrder = original.RecommendedOrder,
                ResultPool = new[]
                {
                    new RecipeResultEntry
                    {
                        ItemId = mutation.ItemId,
                        ItemName = mutation.ItemName,
                        Description = mutation.Description,
                        Weight = "1f",
                        MinQuality = PillQuality.Low,
                        IsSpecial = false
                    }
                },
                SourceHint = "自创变异炼制",
                SourceDetail = $"由 {original.DisplayName} 变异所得",
                Tags = new[] { "自创", "变异", original.Category == RecipeCategory.Pill ? "丹药" : "特殊" },
                CreatorPlayerId = "",
                CreatorPlayerName = "",
                IsPublic = "false",
                SalePrice = defaultSelfCreatedPrice,
                IsOriginalRecipe = false
            };

            RegisterRecipe(selfRecipe);

            EventBus.Publish(new RecipeMutationCreatedEvent
            {
                OriginalRecipeId = original.Id,
                OriginalRecipeName = original.DisplayName,
                NewRecipeId = newId,
                NewRecipeName = selfRecipe.DisplayName,
                Description = mutation.Description
            });

            Debug.Log($"[RecipeDatabase] 自创配方生成: {selfRecipe.DisplayName} (来自 {original.DisplayName} 变异)");
            return selfRecipe;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  自创配方管理
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>自创配方列表</summary>
        public List<RecipeEntry> GetSelfCreatedRecipes()
        {
            var result = new List<RecipeEntry>();
            foreach (var recipe in _recipeList)
            {
                if (recipe.SourceType == RecipeSourceType.SelfCreated)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>设置自创配方的公开/分享状态</summary>
        public void SetRecipePublic(string recipeId, bool isPublic)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe)) return;
            if (recipe.SourceType != RecipeSourceType.SelfCreated) return;

            recipe.IsPublic = isPublic;

            EventBus.Publish(new RecipeSharedEvent
            {
                RecipeId = recipeId,
                RecipeName = recipe.DisplayName,
                IsSold = "false",
                Price = 0
            });
        }

        /// <summary>设置自创配方的出售价格 (0=不可出售)</summary>
        public void SetRecipeSalePrice(string recipeId, int price)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe)) return;
            if (recipe.SourceType != RecipeSourceType.SelfCreated) return;

            recipe.SalePrice = Mathf.Max(0, price);
        }

        /// <summary>出售自创配方, 返回售价</summary>
        public int SellSelfCreatedRecipe(string recipeId)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe)) return 0;
            if (recipe.SourceType != RecipeSourceType.SelfCreated) return 0;
            if (recipe.SalePrice <= 0) return 0;

            int price = recipe.SalePrice;

            EventBus.Publish(new RecipeSharedEvent
            {
                RecipeId = recipeId,
                RecipeName = recipe.DisplayName,
                IsSold = "true",
                Price = price
            });

            Debug.Log($"[RecipeDatabase] 出售自创配方: {recipe.DisplayName}, 售价 {price} 灵石");
            return price;
        }

        /// <summary>设置自创配方的创建者信息</summary>
        public void SetSelfCreatedCreator(string recipeId, string playerId, string playerName)
        {
            if (!_allRecipes.TryGetValue(recipeId, out var recipe)) return;
            if (recipe.SourceType != RecipeSourceType.SelfCreated) return;

            recipe.CreatorPlayerId = playerId;
            recipe.CreatorPlayerName = playerName;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  收藏
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>切换收藏状态</summary>
        public bool ToggleFavorite(string recipeId)
        {
            bool isFav;
            if (_favoriteRecipeIds.Contains(recipeId))
            {
                _favoriteRecipeIds.Remove(recipeId);
                isFav = false;
            }
            else
            {
                _favoriteRecipeIds.Add(recipeId);
                isFav = true;
            }

            // 同步到 RecipeEntry 实例
            if (_allRecipes.TryGetValue(recipeId, out var recipe))
                recipe.IsFavorite = isFav;

            EventBus.Publish(new RecipeFavoriteToggledEvent
            {
                RecipeId = recipeId,
                RecipeName = recipe?.DisplayName ?? recipeId,
                IsFavorite = isFav
            });

            return isFav;
        }

        /// <summary>是否已收藏</summary>
        public bool IsFavorite(string recipeId)
        {
            return _favoriteRecipeIds.Contains(recipeId);
        }

        /// <summary>获取收藏列表</summary>
        public List<RecipeEntry> GetFavoriteRecipes()
        {
            var result = new List<RecipeEntry>();
            foreach (var id in _favoriteRecipeIds)
            {
                if (_allRecipes.TryGetValue(id, out var recipe))
                    result.Add(recipe);
            }
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  搜索
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 搜索配方, 返回过滤后的列表
        /// </summary>
        public List<RecipeEntry> SearchRecipes(RecipeSearchQuery query)
        {
            IEnumerable<RecipeEntry> results = _recipeList;

            // ─── 已知/未知 ───
            if (query.OnlyKnown && !query.IncludeUnknown)
                results = results.Where(r => _knownRecipeIds.Contains(r.Id));
            else if (!query.OnlyKnown && query.IncludeUnknown)
                results = results.Where(r => !_knownRecipeIds.Contains(r.Id));

            // ─── 仅收藏 ───
            if (query.OnlyFavorites)
                results = results.Where(r => _favoriteRecipeIds.Contains(r.Id));

            // ─── 仅自创 ───
            if (query.OnlySelfCreated)
                results = results.Where(r => r.SourceType == RecipeSourceType.SelfCreated);

            // ─── 关键词 (名称 / 描述 / 标签) ───
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                string kw = query.Keyword.Trim().ToLowerInvariant();
                results = results.Where(r =>
                    r.DisplayName.ToLowerInvariant().Contains(kw) ||
                    r.Description.ToLowerInvariant().Contains(kw) ||
                    (r.Tags != null && r.Tags.Any(t => t.ToLowerInvariant().Contains(kw))));
            }

            // ─── 分类 ───
            if (query.Category.HasValue && query.Category.Value != RecipeCategory.None)
                results = results.Where(r => r.Category == query.Category.Value);

            // ─── 来源 ───
            if (query.SourceType.HasValue)
                results = results.Where(r => r.SourceType == query.SourceType.Value);

            // ─── 难度 ───
            results = results.Where(r =>
                r.Difficulty >= query.MinDifficulty &&
                r.Difficulty <= query.MaxDifficulty);

            // ─── 排序 ───
            var sorted = query.SortBy switch
            {
                RecipeSortMode.Name => query.Ascending
                    ? results.OrderBy(r => r.DisplayName)
                    : results.OrderByDescending(r => r.DisplayName),
                RecipeSortMode.Difficulty => query.Ascending
                    ? results.OrderBy(r => r.Difficulty)
                    : results.OrderByDescending(r => r.Difficulty),
                RecipeSortMode.Category => query.Ascending
                    ? results.OrderBy(r => r.Category)
                    : results.OrderByDescending(r => r.Category),
                RecipeSortMode.SourceType => query.Ascending
                    ? results.OrderBy(r => r.SourceType)
                    : results.OrderByDescending(r => r.SourceType),
                RecipeSortMode.TimesCrafted => query.Ascending
                    ? results.OrderBy(r => r.TimesCrafted)
                    : results.OrderByDescending(r => r.TimesCrafted),
                _ => results
            };

            _lastSearchQuery = query;
            _lastSearchResults = sorted.ToList();

            EventBus.Publish(new RecipeSearchResultEvent
            {
                TotalResults = _lastSearchResults.Count,
                KnownCount = _lastSearchResults.Count(r => _knownRecipeIds.Contains(r.Id)),
                Query = query.Keyword
            });

            return _lastSearchResults;
        }

        /// <summary>获取上次搜索结果</summary>
        public List<RecipeEntry> GetLastSearchResults() => _lastSearchResults;

        /// <summary>获取完整配方统计信息</summary>
        public (int total, int known, int favorites, int selfCreated, int basic, int sect, int explore, int boss)
            GetStatistics()
        {
            int total = _recipeList.Count;
            int known = _knownRecipeIds.Count;
            int fav = _favoriteRecipeIds.Count;
            int self = GetSelfCreatedRecipes().Count;
            int basic = GetKnownCountBySource(RecipeSourceType.Basic);
            int sect = GetKnownCountBySource(RecipeSourceType.Sect);
            int explore = GetKnownCountBySource(RecipeSourceType.Exploration);
            int boss = GetKnownCountBySource(RecipeSourceType.Boss);
            return (total, known, fav, self, basic, sect, explore, boss);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  工具
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>加权随机选取 (RecipeMutationEntry)</summary>
        private static RecipeMutationEntry WeightedPickMutation(RecipeMutationEntry[] pool)
        {
            if (pool == null || pool.Length == 0)
                return default;

            if (pool.Length == 1)
                return pool[0];

            float totalWeight = 0f;
            foreach (var item in pool) totalWeight += item.Weight;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;
            foreach (var item in pool)
            {
                accumulated += item.Weight;
                if (roll < accumulated)
                    return item;
            }

            return pool[pool.Length - 1];
        }

        /// <summary>加权随机选取 (RecipeResultEntry)</summary>
        private static RecipeResultEntry WeightedPickResult(RecipeResultEntry[] pool)
        {
            if (pool == null || pool.Length == 0)
                return default;

            if (pool.Length == 1)
                return pool[0];

            float totalWeight = 0f;
            foreach (var item in pool) totalWeight += item.Weight;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;
            foreach (var item in pool)
            {
                accumulated += item.Weight;
                if (roll < accumulated)
                    return item;
            }

            return pool[pool.Length - 1];
        }

        /// <summary>获取来源类型的中文名称</summary>
        public static string GetSourceTypeDisplayName(RecipeSourceType type)
        {
            return type switch
            {
                RecipeSourceType.Basic => "基础配方",
                RecipeSourceType.Sect => "门派配方",
                RecipeSourceType.Exploration => "探索配方",
                RecipeSourceType.Boss => "首领配方",
                RecipeSourceType.SelfCreated => "自创配方",
                _ => "未知"
            };
        }

        /// <summary>获取配方分类的中文名称</summary>
        public static string GetCategoryDisplayName(RecipeCategory category)
        {
            return category switch
            {
                RecipeCategory.Pill => "丹药",
                RecipeCategory.Elixir => "灵液",
                RecipeCategory.Powder => "药散",
                RecipeCategory.Balm => "药膏",
                RecipeCategory.Special => "特殊",
                _ => "全部"
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  调试
        // ═══════════════════════════════════════════════════════════════════════

        public string GetDebugStatus()
        {
            var stats = GetStatistics();
            string output = "=== RecipeDatabase Status ===\n";
            output += $"配方总数: {stats.total}\n";
            output += $"已解锁: {stats.known}\n";
            output += $"收藏: {stats.favorites}\n";
            output += $"自创: {stats.selfCreated}\n";
            output += $"基础: {stats.basic} | 门派: {stats.sect} | 探索: {stats.explore} | BOSS: {stats.boss}\n";

            output += "\n已解锁配方:\n";
            foreach (var id in _knownRecipeIds)
            {
                if (_allRecipes.TryGetValue(id, out var r))
                    output += $"  [{r.GetSourceTypeDisplay()}] {r.DisplayName} " +
                              $"(难度{r.Difficulty}){(r.IsFavorite ? " ★" : "")}\n";
            }

            output += "\n自创配方:\n";
            foreach (var r in GetSelfCreatedRecipes())
            {
                output += $"  {r.DisplayName}" +
                          (r.IsPublic ? " [公开]" : "") +
                          (r.SalePrice > 0 ? $" 售价:{r.SalePrice}" : "") + "\n";
            }

            return output;
        }

        /// <summary>快速测试: 创建并注册一个测试配方</summary>
        public RecipeEntry CreateTestRecipe(string name = "测试丹方")
        {
            string id = "test_recipe_" + Guid.NewGuid().ToString("N");
            var recipe = new RecipeEntry
            {
                Id = id,
                DisplayName = name,
                Description = "测试用配方",
                Category = RecipeCategory.Pill,
                SourceType = RecipeSourceType.Exploration,
                OptimalTemperature = "150f",
                Duration = "50f",
                BaseQualityMin = "0.3f",
                BaseQualityMax = "0.8f",
                Difficulty = "1",
                RequiredProficiency = "1",
                RecommendedOrder = new[] { "mat_a", "mat_b", "mat_c" },
                Materials = new[]
                {
                    new RecipeMaterial { ItemId = "mat_a", DisplayName = "材料A", Quantity = 2 },
                    new RecipeMaterial { ItemId = "mat_b", DisplayName = "材料B", Quantity = 1 },
                    new RecipeMaterial { ItemId = "mat_c", DisplayName = "材料C", Quantity = 1 }
                },
                ResultPool = new[]
                {
                    new RecipeResultEntry { ItemId = id + "_result", ItemName = name, Weight = "1f", MinQuality = PillQuality.Low }
                },
                SourceHint = "测试",
                Tags = new[] { "测试" },
                IsOriginalRecipe = true
            };

            RegisterRecipe(recipe);
            return recipe;
        }
    }
}
