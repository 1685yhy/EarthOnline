using System;
using System.Collections.Generic;
using System.Linq;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.World
{
    // ════════════════════════════════════════════════════════════════════════
    //  JSON Data Contracts (mirrors EconomyConfig.json structure)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Root config wrapper for JsonUtility deserialization.</summary>
    [Serializable]
    public class EconomyConfigWrapper
    {
        public List<ItemPriceEntry> itemPrices;
        public List<LegacyItemPriceMapping> legacyItemPriceMapping;
        public List<ShopConfig> shops;
        public List<CraftingCostEntry> craftingCosts;
        public List<SpiritVeinCostEntry> spiritVeinCosts;
        public List<FastTravelZone> fastTravelZones;
        public RepairFormulaConfig repairFormula;
        public List<TaxRateEntry> taxRates;
        public GamblingConfig gamblingConfig;
        public AuctionConfig auctionConfig;
    }

    [Serializable]
    public class ItemPriceEntry
    {
        public string itemId;
        public int buyPrice;
        public int sellPrice;
        public float fluctuationRange;
    }

    [Serializable]
    public class LegacyItemPriceMapping
    {
        public string oldId;
        public string newId;
        public int fallbackBuyPrice;
        public int fallbackSellPrice;
    }

    [Serializable]
    public class ShopConfig
    {
        public string shopId;
        public string displayName;
        public string npcOwner;
        public float buyMultiplier;
        public float sellMultiplier;
        public List<ShopItemEntry> itemsForSale;
    }

    [Serializable]
    public class ShopItemEntry
    {
        public string itemId;
        public int stock;
        public int refreshDays;
    }

    [Serializable]
    public class CraftingCostEntry
    {
        public string recipeId;
        public int spiritStoneCost;
        public int equipmentDurabilityCost;
        public List<CraftingMaterialCost> materials;
    }

    [Serializable]
    public class CraftingMaterialCost
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class SpiritVeinCostEntry
    {
        public string realm;
        public string realmName;
        public int perHour;
    }

    [Serializable]
    public class FastTravelZone
    {
        public string zoneId;
        public string zoneName;
        public int zoneIndex;
        public List<ZoneTravelCost> pricesToOtherZones;
    }

    [Serializable]
    public class ZoneTravelCost
    {
        public int targetZoneIndex;
        public int cost;
    }

    [Serializable]
    public class QualityFactorEntry
    {
        public string quality;
        public float factor;
    }

    [Serializable]
    public class RepairFormulaConfig
    {
        public float baseMultiplier;
        public List<QualityFactorEntry> qualityFactors;
        public int minCost;
        public string description;
    }

    [Serializable]
    public class TaxRateEntry
    {
        public string regionId;
        public string regionName;
        public float taxRate;
    }

    [Serializable]
    public class GamblingConfig
    {
        public int minBet;
        public int maxBet;
        public float houseEdge;
        public float payoutMultiplierMin;
        public float payoutMultiplierMax;
        public int dailyGamblingLimit;
    }

    [Serializable]
    public class AuctionConfig
    {
        public float startingBidMultiplier;
        public float bidIncrementPercent;
        public float buyoutMultiplier;
        public int listingFee;
        public int auctionDurationHours;
        public float sellerTaxPercent;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Economy Data Loader
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 经济数据加载器
    ///
    /// 从 Resources/Data/EconomyConfig.json 加载完整经济配置（定价、商店、交易、
    /// 灵脉、传送、修理、税率、博彩/拍卖），注入 ShopSystem 和 MarketSystem
    /// 供游戏运行时使用。
    ///
    /// JSON 是唯一天源——改数据不动代码。
    ///
    /// 使用方式：
    ///   场景中的任意初始化入口调用：
    ///     EconomyDataLoader.LoadFromResources();
    ///     EconomyDataLoader.ApplyToSystems();
    ///
    ///   或将此脚本挂载到任意 GameObject 并勾选 autoApply。
    /// </summary>
    public class EconomyDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]
        [SerializeField, Tooltip("Resources 路径（不含扩展名）")]
        private string jsonResourcesPath = "Data/EconomyConfig";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载后自动注入 ShopSystem / MarketSystem")]
        private bool autoApply = true;

        [Header("=== 状态 ===")]
        [SerializeField]
        private int lastLoadedItemCount;

        [SerializeField]
        private bool loadSucceeded;

        // ── Runtime stores ────────────────────────────────────────────────

        /// <summary>All item prices keyed by itemId.</summary>
        public static Dictionary<string, ItemPriceEntry> ItemPrices { get; private set; } = new();

        /// <summary>Maps old legacy item IDs to new Items.json IDs with fallback prices.</summary>
        public static Dictionary<string, LegacyItemPriceMapping> LegacyMappings { get; private set; } = new();

        /// <summary>All shop configs keyed by shopId.</summary>
        public static Dictionary<string, ShopConfig> Shops { get; private set; } = new();

        /// <summary>Crafting costs keyed by recipeId.</summary>
        public static Dictionary<string, CraftingCostEntry> CraftingCosts { get; private set; } = new();

        /// <summary>Spirit vein cultivation costs keyed by realm string.</summary>
        public static Dictionary<string, SpiritVeinCostEntry> SpiritVeinCosts { get; private set; } = new();

        /// <summary>Fast travel zones keyed by zoneId.</summary>
        public static Dictionary<string, FastTravelZone> FastTravelZones { get; private set; } = new();

        /// <summary>Fast travel cost matrix [fromZoneIndex, toZoneIndex] -> cost.</summary>
        public static Dictionary<(int, int), int> FastTravelCostMatrix { get; private set; } = new();

        /// <summary>Quality -> factor for repair cost calculation.</summary>
        public static Dictionary<string, float> RepairQualityFactors { get; private set; } = new();

        /// <summary>Tax rate by regionId.</summary>
        public static Dictionary<string, TaxRateEntry> TaxRates { get; private set; } = new();

        /// <summary>Raw repair formula config (metadata + baseMultiplier).</summary>
        public static RepairFormulaConfig RepairFormulaData { get; private set; }

        /// <summary>Gambling config.</summary>
        public static GamblingConfig GamblingData { get; private set; }

        /// <summary>Auction config.</summary>
        public static AuctionConfig AuctionData { get; private set; }

        // ── Legacy ID resolution cache ────────────────────────────────────

        private static readonly Dictionary<string, string> LegacyToNewId = new();

        // ──────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (loadOnAwake)
            {
                loadSucceeded = LoadFromResources(jsonResourcesPath);
                lastLoadedItemCount = ItemPrices.Count;
            }
        }

        private void Start()
        {
            if (loadSucceeded && autoApply)
            {
                ApplyToSystems();
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Load economy config from Resources.
        /// Must be called before any price/shop queries.
        /// </summary>
        /// <param name="path">Resources 路径（不含扩展名，默认 "Data/EconomyConfig"）</param>
        /// <returns>true 表示加载成功</returns>
        public static bool LoadFromResources(string path = "Data/EconomyConfig")
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[EconomyDataLoader] 未找到经济配置: {path}.json");
                return false;
            }

            var wrapper = JsonUtility.FromJson<EconomyConfigWrapper>(jsonAsset.text);
            if (wrapper == null)
            {
                Debug.LogError("[EconomyDataLoader] JSON 解析失败");
                return false;
            }

            // ── Build item price index ──
            ItemPrices.Clear();
            if (wrapper.itemPrices != null)
            {
                foreach (var entry in wrapper.itemPrices)
                {
                    if (!string.IsNullOrEmpty(entry.itemId))
                        ItemPrices[entry.itemId] = entry;
                }
            }

            // ── Build legacy mapping index ──
            LegacyMappings.Clear();
            LegacyToNewId.Clear();
            if (wrapper.legacyItemPriceMapping != null)
            {
                foreach (var mapping in wrapper.legacyItemPriceMapping)
                {
                    LegacyMappings[mapping.oldId] = mapping;
                    LegacyToNewId[mapping.oldId] = mapping.newId;
                }
            }

            // ── Build shop index ──
            Shops.Clear();
            if (wrapper.shops != null)
            {
                foreach (var shop in wrapper.shops)
                {
                    if (!string.IsNullOrEmpty(shop.shopId))
                        Shops[shop.shopId] = shop;
                }
            }

            // ── Build crafting cost index ──
            CraftingCosts.Clear();
            if (wrapper.craftingCosts != null)
            {
                foreach (var cost in wrapper.craftingCosts)
                {
                    if (!string.IsNullOrEmpty(cost.recipeId))
                        CraftingCosts[cost.recipeId] = cost;
                }
            }

            // ── Build spirit vein cost index ──
            SpiritVeinCosts.Clear();
            if (wrapper.spiritVeinCosts != null)
            {
                foreach (var vein in wrapper.spiritVeinCosts)
                {
                    if (!string.IsNullOrEmpty(vein.realm))
                        SpiritVeinCosts[vein.realm] = vein;
                }
            }

            // ── Build fast travel index ──
            FastTravelZones.Clear();
            FastTravelCostMatrix.Clear();
            if (wrapper.fastTravelZones != null)
            {
                foreach (var zone in wrapper.fastTravelZones)
                {
                    FastTravelZones[zone.zoneId] = zone;
                    if (zone.pricesToOtherZones != null)
                    {
                        foreach (var cost in zone.pricesToOtherZones)
                        {
                            FastTravelCostMatrix[(zone.zoneIndex, cost.targetZoneIndex)] = cost.cost;
                        }
                    }
                }
            }

            // ── Build repair formula ──
            RepairQualityFactors.Clear();
            RepairFormulaData = wrapper.repairFormula;
            if (wrapper.repairFormula?.qualityFactors != null)
            {
                foreach (var qf in wrapper.repairFormula.qualityFactors)
                {
                    if (!string.IsNullOrEmpty(qf.quality))
                        RepairQualityFactors[qf.quality] = qf.factor;
                }
            }

            // ── Tax rates index ──
            TaxRates.Clear();
            if (wrapper.taxRates != null)
            {
                foreach (var tax in wrapper.taxRates)
                {
                    if (!string.IsNullOrEmpty(tax.regionId))
                        TaxRates[tax.regionId] = tax;
                }
            }

            // ── Gambling / Auction ──
            GamblingData = wrapper.gamblingConfig ?? new GamblingConfig
                { minBet = 10, maxBet = 10000, houseEdge = 0.05f };
            AuctionData = wrapper.auctionConfig ?? new AuctionConfig
                { startingBidMultiplier = 0.7f, bidIncrementPercent = 0.1f, buyoutMultiplier = 2.0f };

            Debug.Log($"[EconomyDataLoader] 成功加载经济配置: " +
                      $"{ItemPrices.Count} 项定价, " +
                      $"{Shops.Count} 间商店, " +
                      $"{CraftingCosts.Count} 条配方成本, " +
                      $"{SpiritVeinCosts.Count} 个灵脉境界, " +
                      $"{FastTravelZones.Count} 个传送区域");

            // Update instance fields for inspector state display
            // (Requires a reference to the loader or use a static trick.)
            // These are best-effort; static callers manage their own state.
            return true;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Feeds ShopSystem & MarketSystem
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 将加载的经济数据注入 ShopSystem 和 MarketSystem。
        ///
        /// ShopSystem:
        ///   遍历 Shops 配置，构建 _npcShops 字典。
        ///   BuyMultiplier/SellMultiplier 存储为后续计算参考。
        ///
        /// MarketSystem:
        ///   遍历 ItemPrices，构建 MarketItem 列表。
        ///   初始库存 = maxStock * 0.5（从配置的供需平衡出发）。
        /// </summary>
        public static void ApplyToSystems()
        {
            if (ItemPrices.Count == 0 && LegacyMappings.Count == 0)
            {
                Debug.LogWarning("[EconomyDataLoader] 未加载经济数据，跳过系统注入。");
                return;
            }

            ApplyToShopSystem();
            ApplyToMarketSystem();

            Debug.Log("[EconomyDataLoader] 已注入 ShopSystem + MarketSystem");
        }

        private static void ApplyToShopSystem()
        {
            var shopManager = ShopManager.Instance;
            if (shopManager == null)
            {
                Debug.LogWarning("[EconomyDataLoader] ShopManager.Instance 为 null，跳过商店注入。");
                return;
            }

            // Use reflection to access private _npcShops field
            var field = typeof(ShopManager).GetField("_npcShops",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogError("[EconomyDataLoader] 无法反射获取 ShopManager._npcShops");
                return;
            }

            var npcShops = field.GetValue(shopManager) as Dictionary<string, List<ShopItem>>;
            if (npcShops == null)
            {
                Debug.LogError("[EconomyDataLoader] ShopManager._npcShops 为 null");
                return;
            }

            npcShops.Clear();

            foreach (var shopConfig in Shops.Values)
            {
                var shopItems = new List<ShopItem>();

                foreach (var sale in shopConfig.itemsForSale)
                {
                    // Resolve item name and type from ItemDataLoader if available
                    string itemName = sale.itemId;
                    string itemType = "Consumable";
                    string rarity = "R";

                    var itemDef = ItemDataLoader.GetDef(sale.itemId);
                    if (itemDef != null)
                    {
                        itemName = itemDef.displayName;
                        itemType = MapTypeForShop(itemDef.itemType);
                        rarity = itemDef.quality;
                    }

                    // Resolve base price
                    int price = GetBuyPrice(sale.itemId);

                    shopItems.Add(new ShopItem
                    {
                        itemId = sale.itemId,
                        itemName = itemName,
                        type = itemType,
                        rarity = rarity,
                        price = price,
                        stock = sale.stock
                    });
                }

                npcShops[shopConfig.npcOwner] = shopItems;

                Debug.Log($"[EconomyDataLoader]   商店 [{shopConfig.displayName}] → " +
                          $"{shopItems.Count} 件商品 (NPC: {shopConfig.npcOwner})");
            }
        }

        private static void ApplyToMarketSystem()
        {
            var marketSystem = MarketSystem.Instance;
            if (marketSystem == null)
            {
                Debug.LogWarning("[EconomyDataLoader] MarketSystem.Instance 为 null，跳过市场注入。");
                return;
            }

            var field = typeof(MarketSystem).GetField("market",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field == null)
            {
                Debug.LogError("[EconomyDataLoader] 无法反射获取 MarketSystem.market");
                return;
            }

            var marketList = field.GetValue(marketSystem) as List<MarketItem>;
            if (marketList == null)
            {
                Debug.LogError("[EconomyDataLoader] MarketSystem.market 为 null");
                return;
            }

            marketList.Clear();

            foreach (var priceEntry in ItemPrices.Values)
            {
                string itemName = priceEntry.itemId;
                var itemDef = ItemDataLoader.GetDef(priceEntry.itemId);
                if (itemDef != null)
                    itemName = itemDef.displayName;

                // Initial stock = 50% of a demand-driven estimate
                int maxStock = Mathf.RoundToInt(Mathf.Lerp(5, 50, 1f - (priceEntry.fluctuationRange * 0.8f)));
                maxStock = Mathf.Max(5, maxStock);
                int initialStock = Mathf.RoundToInt(maxStock * 0.5f);

                // Demand inversely related to fluctuation (stable items = higher demand)
                float demand = Mathf.Clamp01(1f - (priceEntry.fluctuationRange * 1.5f));

                marketList.Add(new MarketItem
                {
                    itemId = priceEntry.itemId,
                    itemName = itemName,
                    basePrice = priceEntry.sellPrice,
                    currentStock = initialStock,
                    maxStock = maxStock,
                    demand = demand
                });
            }

            // Also add legacy items that aren't in the new item set
            foreach (var mapping in LegacyMappings.Values)
            {
                if (ItemPrices.ContainsKey(mapping.newId))
                    continue; // already covered

                marketList.Add(new MarketItem
                {
                    itemId = mapping.oldId,
                    itemName = mapping.oldId,
                    basePrice = mapping.fallbackSellPrice,
                    currentStock = 10,
                    maxStock = 20,
                    demand = 0.5f
                });
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Query Methods
        // ──────────────────────────────────────────────────────────────────

        /// <summary>Get the buy price (what player pays) for an item.</summary>
        public static int GetBuyPrice(string itemId)
        {
            if (ItemPrices.TryGetValue(itemId, out var entry))
                return entry.buyPrice;

            // Try legacy -> new resolution
            if (LegacyToNewId.TryGetValue(itemId, out var newId) && ItemPrices.TryGetValue(newId, out var mappedEntry))
                return mappedEntry.buyPrice;

            // Fallback: legacy mapping fallback price
            if (LegacyMappings.TryGetValue(itemId, out var legacy))
                return legacy.fallbackBuyPrice;

            return 0;
        }

        /// <summary>Get the sell price (what player receives) for an item.</summary>
        public static int GetSellPrice(string itemId)
        {
            if (ItemPrices.TryGetValue(itemId, out var entry))
                return entry.sellPrice;

            if (LegacyToNewId.TryGetValue(itemId, out var newId) && ItemPrices.TryGetValue(newId, out var mappedEntry))
                return mappedEntry.sellPrice;

            if (LegacyMappings.TryGetValue(itemId, out var legacy))
                return legacy.fallbackSellPrice;

            return 0;
        }

        /// <summary>Get price fluctuation range (0-1) for an item.</summary>
        public static float GetFluctuationRange(string itemId)
        {
            if (ItemPrices.TryGetValue(itemId, out var entry))
                return entry.fluctuationRange;

            return 0.1f;
        }

        /// <summary>Resolve legacy item ID to new Items.json ID. Returns null if not found.</summary>
        public static string ResolveItemId(string legacyOrNewId)
        {
            if (ItemPrices.ContainsKey(legacyOrNewId))
                return legacyOrNewId;

            if (LegacyToNewId.TryGetValue(legacyOrNewId, out var newId))
                return newId;

            return null;
        }

        /// <summary>Get a shop's config by shopId.</summary>
        public static ShopConfig GetShopConfig(string shopId)
        {
            return Shops.TryGetValue(shopId, out var shop) ? shop : null;
        }

        /// <summary>Get crafting cost by recipeId.</summary>
        public static CraftingCostEntry GetCraftingCost(string recipeId)
        {
            return CraftingCosts.TryGetValue(recipeId, out var cost) ? cost : null;
        }

        /// <summary>Get spirit vein cultivation cost per hour for a realm.</summary>
        public static int GetSpiritVeinCost(string realm, int hours = 1)
        {
            return SpiritVeinCosts.TryGetValue(realm, out var vein) ? vein.perHour * hours : 0;
        }

        /// <summary>Get fast travel cost between two zone indices.</summary>
        public static int GetFastTravelCost(int fromZoneIndex, int toZoneIndex)
        {
            return FastTravelCostMatrix.TryGetValue((fromZoneIndex, toZoneIndex), out var cost) ? cost : int.MaxValue;
        }

        /// <summary>Get fast travel cost between two zone IDs.</summary>
        public static int GetFastTravelCost(string fromZoneId, string toZoneId)
        {
            if (!FastTravelZones.TryGetValue(fromZoneId, out var fromZone)) return int.MaxValue;
            if (!FastTravelZones.TryGetValue(toZoneId, out var toZone)) return int.MaxValue;
            return GetFastTravelCost(fromZone.zoneIndex, toZone.zoneIndex);
        }

        /// <summary>
        /// Calculate equipment repair cost.
        /// Formula: ceil( (1 - durabilityPercent) × qualityFactor × basePrice / 10 )
        /// </summary>
        /// <param name="quality">Quality string: N/R/SR/SSR/UR</param>
        /// <param name="durabilityPercent">Current durability (0.0 - 1.0)</param>
        /// <param name="basePrice">Item base price (sell price)</param>
        /// <returns>Cost in spirit stones</returns>
        public static int CalculateRepairCost(string quality, float durabilityPercent, int basePrice)
        {
            float qualityFactor = RepairQualityFactors.TryGetValue(quality, out var factor) ? factor : 1.0f;
            float damagePercent = Mathf.Clamp01(1f - durabilityPercent);
            int cost = Mathf.CeilToInt(damagePercent * qualityFactor * basePrice / 10f);
            return Mathf.Max(RepairFormulaData?.minCost ?? 1, cost);
        }

        /// <summary>Get tax rate for a region (0.0 - 1.0).</summary>
        public static float GetTaxRate(string regionId)
        {
            return TaxRates.TryGetValue(regionId, out var tax) ? tax.taxRate : 0f;
        }

        /// <summary>Get all tax rate entries.</summary>
        public static List<TaxRateEntry> GetAllTaxRates()
        {
            return TaxRates.Values.ToList();
        }

        /// <summary>Get gambling configuration.</summary>
        public static GamblingConfig GetGamblingConfig() => GamblingData;

        /// <summary>Get auction configuration.</summary>
        public static AuctionConfig GetAuctionConfig() => AuctionData;

        // ──────────────────────────────────────────────────────────────────
        //  Internal Helpers
        // ──────────────────────────────────────────────────────────────────

        private static string MapTypeForShop(string itemType)
        {
            return itemType switch
            {
                "Weapon" => "Weapon",
                "Armor" => "Armor",
                "Elixir" => "Consumable",
                "Material" => "Material",
                "Consumable" => "Consumable",
                "Special" => "Accessory",
                _ => "Consumable"
            };
        }
    }
}
