using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    public class Recipe
    {
        public string id, resultItemId, resultItemName, resultType, resultRarity;
        public int resultQuantity, resultValue;
        public Dictionary<string, int> ingredients = new();
    }

    public class CraftingManager : MonoBehaviour
    {
        public static CraftingManager Instance { get; private set; }
        private List<Recipe> _recipes = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }

            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterRecipes();
            EventBus.Subscribe("OnCraftRequest", d => {
                string id = d.ContainsKey("recipeId") ? d["recipeId"].ToString() : "";
                if (!string.IsNullOrEmpty(id)) Craft(id);
            });
        }

        void RegisterRecipes()
        {
            AddRecipe("craft_heal_pill", "回血丹", "Consumable", "R", 1, 40,
                ("item_herb_001", 3));
            AddRecipe("craft_spirit_core", "灵气核心", "Material", "SR", 1, 200,
                ("item_spirit_stone", 5), ("item_pill_001", 2));
            AddRecipe("craft_steel_sword", "精钢剑", "Weapon", "SR", 1, 250,
                ("item_iron_sword", 1), ("item_spirit_core_001", 1));
            AddRecipe("craft_guard_ring", "守护之戒", "Accessory", "SR", 1, 300,
                ("item_spirit_core_001", 2), ("item_chaos_fragment", 1));
            // V2.0 new recipes
            AddRecipe("craft_cultivation_pill", "筑基丹", "Consumable", "SR", 1, 150,
                ("item_pill_001", 3), ("item_spirit_stone", 5));
            AddRecipe("craft_dragon_scale_armor", "龙鳞甲", "Armor", "SSR", 1, 800,
                ("item_leather_armor", 1), ("item_spirit_core_001", 3), ("item_chaos_fragment", 1));
            AddRecipe("craft_spirit_elixir", "灵力药剂", "Consumable", "R", 3, 60,
                ("item_herb_001", 2), ("item_spirit_stone", 2));
            AddRecipe("craft_breakthrough_pill", "突破丹", "Consumable", "SR", 1, 300,
                ("item_pill_001", 5), ("item_spirit_core_001", 1), ("item_ginseng_1000yr", 1));
            AddRecipe("craft_talisman", "护身符", "Accessory", "R", 1, 80,
                ("item_spirit_stone", 3), ("item_herb_001", 1));
            AddRecipe("craft_spirit_bomb", "灵气炸弹", "Consumable", "R", 2, 50,
                ("item_spirit_stone", 5));
            AddRecipe("craft_dragon_pill", "龙血丹", "Consumable", "SSR", 1, 600, ("item_pill_001", 5), ("item_spirit_core_001", 2), ("item_ginseng_1000yr", 1));
            AddRecipe("craft_antidote", "解毒丹", "Consumable", "R", 2, 35,
                ("item_herb_001", 2));
            AddRecipe("craft_spirit_amulet", "灵蕴护符", "Accessory", "SR", 1, 350,
            AddRecipe("craft_elixir_supreme", "大还丹", "Consumable", "SSR", 1, 500, ("item_heal_pill_001", 3), ("item_spirit_core_001", 2), ("item_cultivation_elixir", 1));

                ("item_spirit_jade", 1), ("item_spirit_core_001", 2));
        }

        void AddRecipe(string id, string name, string type, string rarity, int qty, int value, params (string, int)[] ings)
        {
            var r = new Recipe { id = id, resultItemId = id, resultItemName = name, resultType = type,
                resultRarity = rarity, resultQuantity = qty, resultValue = value };
            foreach (var (itemId, count) in ings) r.ingredients[itemId] = count;
            _recipes.Add(r);
        }

        public List<Recipe> GetAvailableRecipes()
        {
            var inv = InventoryManager.Instance;
            return inv == null ? new() : _recipes.FindAll(r => {
                foreach (var ing in r.ingredients)
                    if (!inv.HasItem(ing.Key, ing.Value)) return false;
                return true;
            });
        }

        public List<Recipe> GetAllRecipes() => new(_recipes);

        public bool Craft(string recipeId)
        {
            var recipe = _recipes.Find(r => r.id == recipeId);
            if (recipe == null) return false;
            var inv = InventoryManager.Instance;
            if (inv == null) return false;
            foreach (var ing in recipe.ingredients)
                if (!inv.HasItem(ing.Key, ing.Value)) { Debug.Log($"[Craft] 材料不足！需要 {ing.Key} x{ing.Value}"); return false; }
            foreach (var ing in recipe.ingredients) inv.RemoveItem(ing.Key, ing.Value);
            inv.AddItem(new Item { id = recipe.resultItemId, name = recipe.resultItemName,
                type = recipe.resultType, rarity = recipe.resultRarity,
                quantity = recipe.resultQuantity, value = recipe.resultValue });
            Debug.Log($"[Craft] 制作成功！[{recipe.resultRarity}] {recipe.resultItemName} x{recipe.resultQuantity}");
            EventBus.Publish("OnItemCrafted", new Dictionary<string, object> {{"recipeId", recipeId}, {"itemName", recipe.resultItemName}});
            return true;
        }

        void OnDestroy() { EventBus.Unsubscribe("OnCraftRequest", d => {}); }
    }
}
