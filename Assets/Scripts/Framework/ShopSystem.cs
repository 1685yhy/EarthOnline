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
            _npcShops["npc_chen_001"] = new List<ShopItem> {
                new() { itemId="item_heal_pill_001", itemName="回血丹", type="Consumable", rarity="R", price=30, stock=5 },
                new() { itemId="item_pill_001", itemName="聚气丹", type="Consumable", rarity="R", price=25, stock=5 },
                new() { itemId="item_spirit_stone", itemName="灵石碎片", type="Material", rarity="R", price=20, stock=10 },
                new() { itemId="item_iron_sword", itemName="铁剑", type="Weapon", rarity="R", price=100, stock=1 },
                new() { itemId="item_leather_armor", itemName="皮甲", type="Armor", rarity="R", price=80, stock=1 },
            };

            _npcShops["npc_li_001"] = new List<ShopItem> {
                new() { itemId="item_herb_001", itemName="止血草", type="Consumable", rarity="N", price=10, stock=10 },
                new() { itemId="item_heal_pill_001", itemName="回血丹", type="Consumable", rarity="R", price=25, stock=3 },
            };
        }

        public List<ShopItem> GetShop(string npcId)
        {
            return _npcShops.ContainsKey(npcId) ? _npcShops[npcId] : new List<ShopItem>();
        }

        public bool Buy(string npcId, string itemId)
        {
            var shop = GetShop(npcId);
            var shopItem = shop.Find(s => s.itemId == itemId);
            if (shopItem == null) { Debug.Log("[Shop] 此物品不在售。"); return false; }
            if (shopItem.stock <= 0) { Debug.Log("[Shop] 已售罄！"); return false; }

            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < shopItem.price)
            {
                Debug.Log($"[Shop] 灵石不足！需要{shopItem.price}💰");
                return false;
            }

            stats.AddSpiritStone(-shopItem.price);
            shopItem.stock--;

            var inv = InventoryManager.Instance;
            inv?.AddItem(new Item
            {
                id = shopItem.itemId, name = shopItem.itemName,
                type = shopItem.type, rarity = shopItem.rarity,
                quantity = 1, value = shopItem.price
            });

            Debug.Log($"[Shop] 购买 [{shopItem.rarity}] {shopItem.itemName} -{shopItem.price}💰 (库存:{shopItem.stock})");
            EventBus.Publish("OnItemPurchased", new Dictionary<string, object> {
                {"itemName", shopItem.itemName}, {"price", shopItem.price}
            });
            return true;
        }

        public bool Sell(string itemId)
        {
            var inv = InventoryManager.Instance;
            var stats = PlayerStats.Instance;
            if (inv == null || stats == null) return false;

            var item = inv.GetItem(itemId);
            if (item == null) return false;

            int sellPrice = item.value / 2;
            inv.RemoveItem(itemId, 1);
            stats.AddSpiritStone(sellPrice);

            Debug.Log($"[Shop] 出售 {item.name} +{sellPrice}💰");
            return true;
        }

        public void ShowShop(string npcId)
        {
            var shop = GetShop(npcId);
            Debug.Log($"═══════ 商店 ═══════");
            foreach (var s in shop)
            {
                string stockStr = s.stock > 0 ? $"库存:{s.stock}" : "售罄";
                Debug.Log($"  [{s.rarity}] {s.itemName} — {s.price}💰 ({stockStr}) ID:{s.itemId}");
            }
            Debug.Log($"  你的灵石: {PlayerStats.Instance?.spiritStones ?? 0}💰");
            Debug.Log($"  价格受名声影响：{ReputationSystem.Instance?.ShopPriceModifier ?? 1f:F2}x");
            Debug.Log($"  按B+数字购买 | N卖物品");
        }
    }
}
