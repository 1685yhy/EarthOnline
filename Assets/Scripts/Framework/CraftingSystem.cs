using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 制作系统 —— 材料合成物品。按C键打开制作菜单。
    /// </summary>
    [System.Serializable]
    public class Recipe
    {
        public string id;
        public string resultItemId;
        public string resultItemName;
        public string resultType;
        public string resultRarity;
        public int resultQuantity;
        public int resultValue;
        public Dictionary<string, int> ingredients; // itemId -> count
    }

    public class CraftingManager : MonoBehaviour
    {
        public static CraftingManager Instance { get; private set; }

        private List<Recipe> _recipes = new List<Recipe>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterRecipes();
            EventBus.Subscribe("OnCraftRequest", OnCraftRequested);
        }

        void RegisterRecipes()
        {
            _recipes.Add(new Recipe
            {
                id = "craft_heal_pill", resultItemId = "item_heal_pill_001",
                resultItemName = "回血丹", resultType = "Consumable", resultRarity = "R",
                resultQuantity = 1, resultValue = 40,
                ingredients = new Dictionary<string, int> { {"item_herb_001", 3} }
            });

            _recipes.Add(new Recipe
            {
                id = "craft_spirit_core", resultItemId = "item_spirit_core_001",
                resultItemName = "灵气核心", resultType = "Material", resultRarity = "SR",
                resultQuantity = 1, resultValue = 200,
                ingredients = new Dictionary<string, int> {
                    {"item_spirit_stone", 5}, {"item_pill_001", 2}
                }
            });

            // 武器制作
            _recipes.Add(new Recipe
            {
                id = "craft_steel_sword", resultItemId = "item_steel_sword",
                resultItemName = "精钢剑", resultType = "Weapon", resultRarity = "SR",
                resultQuantity = 1, resultValue = 250,
                ingredients = new Dictionary<string, int> {
                    {"item_iron_sword", 1}, {"item_spirit_core_001", 1}
                }
            });

            // 饰品制作
            _recipes.Add(new Recipe
            {
                id = "craft_ring", resultItemId = "item_guard_ring",
                resultItemName = "守护之戒", resultType = "Accessory", resultRarity = "SR",
                resultQuantity = 1, resultValue = 300,
                ingredients = new Dictionary<string, int> {
                    {"item_spirit_core_001", 2}, {"item_chaos_fragment", 1}
                }
            });
        }

        public List<Recipe> GetAvailableRecipes()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return new List<Recipe>();

            return _recipes.FindAll(r =>
            {
                foreach (var ing in r.ingredients)
                    if (!inv.HasItem(ing.Key, ing.Value)) return false;
                return true;
            });
        }

        public List<Recipe> GetAllRecipes() => new List<Recipe>(_recipes);

        public bool Craft(string recipeId)
        {
            var recipe = _recipes.Find(r => r.id == recipeId);
            if (recipe == null) return false;

            var inv = InventoryManager.Instance;
            if (inv == null) return false;

            // 检查材料
            foreach (var ing in recipe.ingredients)
            {
                if (!inv.HasItem(ing.Key, ing.Value))
                {
                    Debug.Log($"[Craft] 材料不足！需要 {ing.Key} x{ing.Value}");
                    return false;
                }
            }

            // 消耗材料
            foreach (var ing in recipe.ingredients)
                inv.RemoveItem(ing.Key, ing.Value);

            // 获得成品
            inv.AddItem(new Item
            {
                id = recipe.resultItemId, name = recipe.resultItemName,
                type = recipe.resultType, rarity = recipe.resultRarity,
                quantity = recipe.resultQuantity, value = recipe.resultValue
            });

            Debug.Log($"[Craft] 制作成功！{recipe.resultItemName} x{recipe.resultQuantity}");
            EventBus.Publish("OnItemCrafted", new Dictionary<string, object> {
                {"recipeId", recipeId}, {"itemName", recipe.resultItemName}
            });
            return true;
        }

        void OnCraftRequested(Dictionary<string, object> data)
        {
            string recipeId = data.ContainsKey("recipeId") ? data["recipeId"].ToString() : "";
            if (!string.IsNullOrEmpty(recipeId)) Craft(recipeId);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnCraftRequest", OnCraftRequested);
        }
    }
}
