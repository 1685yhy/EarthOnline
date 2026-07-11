using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    [System.Serializable]
    public class ShopItem
    {
        public string itemId; public string itemName; public string type;
        public string rarity; public int price; public int stock;
    }

    /// <summary>
    /// 商店系统 —— NPC对话时按B打开商店。
    /// V2.0 集成经济系统V1：名声价格修正 + 新手保护折扣 + 市场供需联动。
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        private Dictionary<string, List<ShopItem>> _npcShops = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupShops();
        }

        void SetupShops()
        {
            // 价格已对齐经济平衡文档V1 5.3节定价
            _npcShops["npc_chen_001"] = new List<ShopItem> {
                new() { itemId="item_heal_pill_001", itemName="回血丹", type="Consumable", rarity="R", price=40, stock=5 },
                new() { itemId="item_pill_001", itemName="聚气丹", type="Consumable", rarity="R", price=25, stock=5 },
                new() { itemId="item_spirit_stone", itemName="灵石碎片", type="Material", rarity="R", price=10, stock=10 },
                new() { itemId="item_iron_sword", itemName="铁剑", type="Weapon", rarity="R", price=100, stock=1 },
                new() { itemId="item_leather_armor", itemName="皮甲", type="Armor", rarity="R", price=80, stock=1 },
                new() { itemId="item_steel_sword", itemName="精钢剑", type="Weapon", rarity="SR", price=250, stock=1 },
            };

            _npcShops["npc_li_001"] = new List<ShopItem> {
                new() { itemId="item_herb_001", itemName="止血草", type="Consumable", rarity="N", price=10, stock=10 },
                new() { itemId="item_heal_pill_001", itemName="回血丹", type="Consumable", rarity="R", price=40, stock=3 },
            };
        }

        public List<ShopItem> GetShop(string npcId)
        {
            return _npcShops.ContainsKey(npcId) ? _npcShops[npcId] : new List<ShopItem>();
        }

        /// <summary>计算物品的最终购买价格（含名声修正 + 新手保护折扣）</summary>
        int CalculateBuyPrice(ShopItem shopItem)
        {
            int basePrice = shopItem.price;

            // 1. 名声价格修正（善名→便宜，恶名→贵）
            //    ShopPriceModifier = 1 + infamy*0.02 - fame*0.01
            float repModifier = ReputationSystem.Instance?.ShopPriceModifier ?? 1f;

            // 2. 新手保护折扣（萌新8折 过渡9折）
            float newbieDiscount = NewbieProtection.GetShopDiscount();

            // 3. 萌新期 R及以下物品半价
            float rarityDiscount = 1f;
            if (NewbieProtection.Level == NewbieProtection.ProtectionLevel.Sprout &&
                (shopItem.rarity == "N" || shopItem.rarity == "R"))
                rarityDiscount = 0.5f;

            int finalPrice = Mathf.RoundToInt(basePrice * repModifier * newbieDiscount * rarityDiscount);
            return Mathf.Max(1, finalPrice);
        }

        public bool Buy(string npcId, string itemId)
        {
            var shop = GetShop(npcId);
            var shopItem = shop.Find(s => s.itemId == itemId);
            if (shopItem == null) { Debug.Log("[Shop] 此物品不在售。"); return false; }
            if (shopItem.stock <= 0) { Debug.Log("[Shop] 已售罄！"); return false; }

            var stats = PlayerStats.Instance;
            if (stats == null) return false;

            // === 经济系统V1：动态定价 ===
            int basePrice = shopItem.price;
            int finalPrice = CalculateBuyPrice(shopItem);

            if (stats.spiritStones < finalPrice)
            {
                Debug.Log($"[Shop] 灵石不足！需要{finalPrice}💰 (原价{basePrice}, 名声修正后)");
                return false;
            }

            stats.AddSpiritStone(-finalPrice);
            shopItem.stock--;

            // 通知市场系统供需变化
            MarketSystem.Instance?.OnPlayerPurchase(shopItem.itemId);

            var inv = InventoryManager.Instance;
            inv?.AddItem(new Item
            {
                id = shopItem.itemId, name = shopItem.itemName,
                type = shopItem.type, rarity = shopItem.rarity,
                quantity = 1, value = basePrice
            });

            string priceNote = finalPrice != basePrice ? $" (修正后{finalPrice})" : "";
            Debug.Log($"[Shop] 购买 [{shopItem.rarity}] {shopItem.itemName} -{finalPrice}💰{priceNote} (库存:{shopItem.stock})");
            EventBus.Publish("OnItemPurchased", new Dictionary<string, object> {
                {"itemName", shopItem.itemName}, {"price", finalPrice}, {"basePrice", basePrice}
            });
            return true;
        }

        /// <summary>计算物品出售价格（善名→溢价，恶名→压价）</summary>
        int CalculateSellPrice(Item item)
        {
            int baseSellPrice = item.value / 2;

            // 名声影响：善名卖高价，恶名被压价
            float sellModifier = 1f;
            if (ReputationSystem.Instance != null)
            {
                sellModifier = 1f + ReputationSystem.Instance.fame * 0.005f
                                  - ReputationSystem.Instance.infamy * 0.01f;
                sellModifier = Mathf.Clamp(sellModifier, 0.5f, 2f);
            }

            // 新手保护：萌新出售加成20%
            if (NewbieProtection.IsSprout)
                sellModifier *= 1.2f;

            int finalPrice = Mathf.RoundToInt(baseSellPrice * sellModifier);
            return Mathf.Max(1, finalPrice);
        }

        public bool Sell(string itemId)
        {
            var inv = InventoryManager.Instance;
            var stats = PlayerStats.Instance;
            if (inv == null || stats == null) return false;

            var item = inv.GetItem(itemId);
            if (item == null) return false;

            // === 经济系统V1：出售定价 ===
            int baseSellPrice = item.value / 2;
            int finalPrice = CalculateSellPrice(item);

            inv.RemoveItem(itemId, 1);
            stats.AddSpiritStone(finalPrice);

            // 通知市场系统
            MarketSystem.Instance?.OnPlayerSell(itemId);

            Debug.Log($"[Shop] 出售 {item.name} +{finalPrice}💰 (原价{baseSellPrice})");
            EventBus.Publish("OnItemSold", new Dictionary<string, object> {
                {"itemName", item.name}, {"price", finalPrice}, {"basePrice", baseSellPrice}
            });
            return true;
        }

        public void ShowShop(string npcId)
        {
            var shop = GetShop(npcId);
            Debug.Log($"═══════ 商店 ═══════");
            foreach (var s in shop)
            {
                string stockStr = s.stock > 0 ? $"库存:{s.stock}" : "售罄";

                // 显示有效价格
                int effectivePrice = CalculateBuyPrice(s);
                string priceStr = effectivePrice != s.price
                    ? $"{effectivePrice}💰(原{s.price})"
                    : $"{s.price}💰";

                Debug.Log($"  [{s.rarity}] {s.itemName} — {priceStr} ({stockStr}) ID:{s.itemId}");
            }
            Debug.Log($"  你的灵石: {PlayerStats.Instance?.spiritStones ?? 0}💰");
            string protectionNote = "";
            if (NewbieProtection.Level == NewbieProtection.ProtectionLevel.Sprout)
                protectionNote = " [新手保护8折+R以下半价]";
            else if (NewbieProtection.Level == NewbieProtection.ProtectionLevel.Transition)
                protectionNote = " [过渡保护9折]";
            Debug.Log($"  价格受名声影响：{ReputationSystem.Instance?.ShopPriceModifier ?? 1f:F2}x{protectionNote}");
            Debug.Log($"  按B+数字购买 | N卖物品");
        }
    }
}
