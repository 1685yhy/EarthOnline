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
            AddRecipe("craft_spirit_amulet", "灵蕴护符", "Accessory", "SR", 1, 350, ("item_spirit_jade", 1), ("item_spirit_core_001", 2));
            AddRecipe("craft_hydra_blade", "九头蛇之刃", "Weapon", "SSR", 1, 2500, ("item_steel_sword", 1), ("item_void_crystal", 3), ("item_spirit_core_001", 5));
            AddRecipe("craft_elixir_ultra", "仙丹", "Consumable", "SSR", 1, 2500, ("item_phoenix_feather", 1), ("item_void_heart", 1), ("item_cultivation_elixir", 3));
            AddRecipe("craft_titan_armor", "泰坦之甲", "Armor", "SSR", 1, 3000, ("item_dragon_scale_armor", 1), ("item_ancient_rune", 2), ("item_spirit_core_001", 5));
            AddRecipe("craft_void_heart", "虚空之心制品", "Accessory", "SSR", 1, 3000, ("item_void_heart", 1), ("item_void_crystal", 3), ("item_ancient_rune", 2));
            AddRecipe("craft_behemoth_armor", "比蒙战甲", "Armor", "SSR", 1, 3500, ("item_titan_core", 1), ("item_dragon_scale_armor", 1), ("item_ancient_rune", 3));
            AddRecipe("craft_kraken_blade", "深渊之刃", "Weapon", "SSR", 1, 2800, ("item_steel_sword", 1), ("item_void_crystal", 3), ("item_titan_core", 1));
            AddRecipe("craft_chimera_bow", "奇美拉之弓", "Weapon", "SSR", 1, 3200, ("item_dragon_fang", 2), ("item_void_crystal", 2), ("item_phoenix_feather", 1));
            AddRecipe("craft_immortal_elixir", "仙灵药剂", "Consumable", "SSR", 1, 2000, ("item_phoenix_feather", 1), ("item_spirit_core_001", 5), ("item_ginseng_1000yr", 1));
            AddRecipe("craft_necro_staff", "死灵法杖", "Weapon", "SSR", 1, 3500, ("item_void_crystal", 3), ("item_ancient_rune", 2), ("item_titan_core", 1));
            AddRecipe("craft_archon_blade", "圣光之刃", "Weapon", "SSR", 1, 4000, ("item_titan_core", 2), ("item_phoenix_feather", 2), ("item_ancient_rune", 1));
            AddRecipe("craft_abyss_core", "深渊核心", "Accessory", "SSR", 1, 5000, ("item_void_heart", 1), ("item_titan_core", 2), ("item_ancient_rune", 3));
            AddRecipe("craft_void_lord", "虚空王冠", "Accessory", "SSR", 1, 8000, ("item_void_heart", 3), ("item_titan_core", 3), ("item_ancient_rune", 5));
            AddRecipe("craft_thunder_spear", "雷霆之矛", "Weapon", "SSR", 1, 5000, ("item_phoenix_feather", 3), ("item_titan_core", 2), ("item_dragon_fang", 2));
            AddRecipe("craft_frost_shield", "冰霜巨盾", "Armor", "SSR", 1, 5000, ("item_titan_core", 2), ("item_void_crystal", 5), ("item_ancient_rune", 2));
            AddRecipe("craft_shadow_cloak", "暗影斗篷", "Armor", "SSR", 1, 4500, ("item_void_crystal", 5), ("item_dragon_fang", 3), ("item_phoenix_feather", 1));
            AddRecipe("craft_blood_armor", "血魔战甲", "Armor", "SSR", 1, 4000, ("item_void_crystal", 4), ("item_titan_core", 1), ("item_dragon_fang", 2));
            AddRecipe("craft_world_eater", "吞星者之刃", "Weapon", "SSR", 1, 10000, ("item_void_heart", 5), ("item_titan_core", 5), ("item_ancient_rune", 5));
            AddRecipe("craft_solar_crown", "太阳王冠", "Accessory", "SSR", 1, 10000, ("item_phoenix_feather", 5), ("item_titan_core", 5), ("item_dragon_fang", 5));
            AddRecipe("craft_dragon_lord", "龙王之冠", "Accessory", "SSR", 1, 12000, ("item_dragon_fang", 5), ("item_titan_core", 5), ("item_phoenix_feather", 3));
            AddRecipe("craft_celestial_blade", "天界圣剑", "Weapon", "SSR", 1, 15000, ("item_titan_core", 5), ("item_phoenix_feather", 5), ("item_void_heart", 3));
            AddRecipe("craft_void_titan_armor", "虚空泰坦甲", "Armor", "SSR", 1, 20000, ("item_void_heart", 5), ("item_titan_core", 5), ("item_ancient_rune", 5));
            AddRecipe("craft_void_general", "虚空将军之刃", "Weapon", "SSR", 1, 8000, ("item_void_heart", 2), ("item_void_crystal", 5), ("item_titan_core", 2));
            AddRecipe("craft_fallen_crown", "堕落王冠", "Accessory", "SSR", 1, 7000, ("item_void_heart", 2), ("item_dragon_fang", 3), ("item_ancient_rune", 2));
            AddRecipe("craft_guardian_shield", "守护者之盾", "Armor", "SSR", 1, 10000, ("item_titan_core", 3), ("item_phoenix_feather", 3), ("item_void_crystal", 5));
            AddRecipe("craft_chaos_ring", "混沌之戒", "Accessory", "SSR", 1, 6000, ("item_void_heart", 1), ("item_ancient_rune", 3), ("item_dragon_fang", 2));
            AddRecipe("craft_void_spider_silk", "虚空蛛丝甲", "Armor", "SSR", 1, 5000, ("item_void_crystal", 5), ("item_spirit_core_001", 5), ("item_dragon_fang", 1));
            AddRecipe("craft_final_weapon", "终焉之刃", "Weapon", "SSR", 1, 50000, ("item_void_heart", 10), ("item_titan_core", 10), ("item_ancient_rune", 10));
            AddRecipe("craft_peace_ring", "和平之戒", "Accessory", "SSR", 1, 30000, ("item_phoenix_feather", 5), ("item_dragon_fang", 5), ("item_void_crystal", 10));
            AddRecipe("craft_new_world_key", "新世界之钥", "Accessory", "SSR", 1, 100000, ("item_void_heart", 10), ("item_phoenix_feather", 10), ("item_dragon_fang", 10));
            AddRecipe("craft_dream_blade", "梦魇之刃", "Weapon", "SSR", 1, 6000, ("item_void_crystal", 5), ("item_ancient_rune", 2), ("item_dragon_fang", 2));
            AddRecipe("craft_soul_vessel", "灵魂容器", "Accessory", "SSR", 1, 5500, ("item_void_heart", 2), ("item_phoenix_feather", 2), ("item_ancient_rune", 2));
            AddRecipe("craft_time_amulet", "时光护符", "Accessory", "SSR", 1, 7000, ("item_phoenix_feather", 3), ("item_void_crystal", 5), ("item_titan_core", 2));
            AddRecipe("craft_nether_sword", "冥龙之剑", "Weapon", "SSR", 1, 8000, ("item_dragon_fang", 3), ("item_void_crystal", 5), ("item_ancient_rune", 3));
            AddRecipe("craft_storm_hammer", "风暴之锤", "Weapon", "SSR", 1, 7000, ("item_titan_core", 3), ("item_phoenix_feather", 2), ("item_dragon_fang", 2));
            AddRecipe("craft_void_eater_armor", "虚空吞噬甲", "Armor", "SSR", 1, 9000, ("item_void_heart", 3), ("item_titan_core", 3), ("item_void_crystal", 5));
            AddRecipe("craft_shadow_blade", "暗影之刃", "Weapon", "SSR", 1, 4500, ("item_void_crystal", 4), ("item_ancient_rune", 1), ("item_dragon_fang", 1));
            AddRecipe("craft_flesh_armor", "血肉战甲", "Armor", "SSR", 1, 5500, ("item_void_crystal", 5), ("item_titan_core", 1), ("item_ancient_rune", 2));
            AddRecipe("craft_bone_sword", "龙骨之刃", "Weapon", "SSR", 1, 6500, ("item_dragon_fang", 3), ("item_void_crystal", 4), ("item_ancient_rune", 2));
            AddRecipe("craft_dragon_heart", "龙心项链", "Accessory", "SSR", 1, 8000, ("item_dragon_fang", 5), ("item_phoenix_feather", 3), ("item_titan_core", 2));
            AddRecipe("craft_rune_staff", "符文法杖", "Weapon", "SSR", 1, 7000, ("item_ancient_rune", 3), ("item_void_crystal", 5), ("item_titan_core", 1));
            AddRecipe("craft_flame_sword", "烈焰之刃", "Weapon", "SSR", 1, 7500, ("item_phoenix_feather", 3), ("item_titan_core", 2), ("item_dragon_fang", 2));
            AddRecipe("craft_star_crown", "星辰之冠", "Accessory", "SSR", 1, 9000, ("item_phoenix_feather", 5), ("item_titan_core", 3), ("item_ancient_rune", 3));
            AddRecipe("craft_void_essence", "虚空精华", "Consumable", "SSR", 1, 6000, ("item_void_heart", 2), ("item_void_crystal", 5), ("item_phoenix_feather", 2));
            AddRecipe("craft_wisdom_crown", "智慧之冠", "Accessory", "SSR", 1, 7000, ("item_ancient_rune", 3), ("item_phoenix_feather", 2), ("item_titan_core", 1));
            AddRecipe("craft_archive_key", "档案馆之钥", "Accessory", "SSR", 1, 8000, ("item_ancient_rune", 5), ("item_titan_core", 3), ("item_void_crystal", 5));
            AddRecipe("craft_void_soul", "虚空之魂", "Consumable", "SSR", 1, 5000, ("item_void_heart", 1), ("item_void_crystal", 5), ("item_phoenix_feather", 1));
            AddRecipe("craft_titan_ring", "泰坦之戒", "Accessory", "SSR", 1, 6000, ("item_titan_core", 2), ("item_ancient_rune", 2), ("item_dragon_fang", 2));
            AddRecipe("craft_wind_blade", "风暴之刃", "Weapon", "SSR", 1, 6500, ("item_phoenix_feather", 3), ("item_dragon_fang", 2), ("item_titan_core", 1));
            AddRecipe("craft_ancient_seal", "远古封印", "Accessory", "SSR", 1, 5000, ("item_ancient_rune", 3), ("item_void_crystal", 5), ("item_titan_core", 1));
            AddRecipe("craft_void_pact", "虚空契约", "Accessory", "SSR", 1, 6000, ("item_void_heart", 2), ("item_void_crystal", 5), ("item_ancient_rune", 2));
            AddRecipe("craft_void_hound_fang", "虚空猎牙", "Weapon", "SSR", 1, 5000, ("item_void_crystal", 4), ("item_dragon_fang", 1), ("item_ancient_rune", 1));
            AddRecipe("craft_cerberus_fang", "地狱獠牙", "Weapon", "SSR", 1, 3500, ("item_dragon_fang", 2), ("item_void_heart", 1), ("item_void_crystal", 4));
            AddRecipe("craft_medusa_gaze", "美杜莎之眼", "Accessory", "SSR", 1, 3200, ("item_ancient_rune", 2), ("item_spirit_core_001", 5), ("item_void_crystal", 2));
            AddRecipe("craft_banshee_wail", "女妖之嚎", "Accessory", "SSR", 1, 2800, ("item_void_crystal", 3), ("item_spirit_core_001", 4), ("item_phoenix_feather", 1));
            AddRecipe("craft_wendigo_claw", "温迪戈之爪", "Weapon", "SSR", 1, 3000, ("item_dragon_fang", 1), ("item_void_crystal", 3), ("item_ancient_rune", 1));
            AddRecipe("craft_wyvern_scale", "飞龙鳞甲", "Armor", "SSR", 1, 2000, ("item_dragon_scale_armor", 1), ("item_spirit_core_001", 5), ("item_void_crystal", 2));
            AddRecipe("craft_serpent_venom", "蛇毒药剂", "Consumable", "SR", 2, 150, ("item_herb_001", 3), ("item_pill_001", 2));
            AddRecipe("craft_rune_blade", "符文之刃", "Weapon", "SSR", 1, 1800, ("item_ancient_rune", 1), ("item_steel_sword", 2), ("item_spirit_core_001", 3));
            AddRecipe("craft_blood_sword", "血祭之刃", "Weapon", "SSR", 1, 1200, ("item_steel_sword", 1), ("item_void_crystal", 1));
            AddRecipe("craft_world_amulet", "世界护符", "Accessory", "SSR", 1, 1500,
                ("item_world_seed", 1), ("item_spirit_core_001", 5), ("item_void_crystal", 1));
            AddRecipe("craft_phoenix_elixir", "凤凰药剂", "Consumable", "SSR", 1, 800,
                ("item_phoenix_feather", 1), ("item_cultivation_elixir", 1), ("item_spirit_core_001", 3));
            AddRecipe("craft_void_weapon", "虚空之刃", "Weapon", "SSR", 1, 1000,
                ("item_void_crystal", 1), ("item_steel_sword", 1), ("item_spirit_core_001", 2));
            AddRecipe("craft_elixir_supreme", "大还丹", "Consumable", "SSR", 1, 500,
                ("item_heal_pill_001", 3), ("item_spirit_core_001", 2), ("item_cultivation_elixir", 1));
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
