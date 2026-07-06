using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 市场供需系统 —— 商品价格随供需波动。不是固定价格。
    /// 购买→库存减少→价格上涨。NPC补货→价格回落。
    /// 社会运行逻辑：市场不是静态价格表。
    /// </summary>
    [System.Serializable]
    public class MarketItem
    {
        public string itemId, itemName;
        public int basePrice;
        public int currentStock;
        public int maxStock;
        public float demand; // 0-1, higher = faster price rise
    }

    public class MarketSystem : MonoBehaviour
    {
        public static MarketSystem Instance { get; private set; }
        public List<MarketItem> market = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupMarket();
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
        }

        void SetupMarket()
        {
            market = new List<MarketItem>
            {
                new() { itemId="item_heal_pill_001", itemName="回血丹", basePrice=30, currentStock=20, maxStock=30, demand=0.7f },
                new() { itemId="item_pill_001", itemName="聚气丹", basePrice=25, currentStock=15, maxStock=25, demand=0.8f },
                new() { itemId="item_spirit_stone", itemName="灵石碎片", basePrice=20, currentStock=30, maxStock=40, demand=0.5f },
                new() { itemId="item_herb_001", itemName="止血草", basePrice=10, currentStock=25, maxStock=35, demand=0.6f },
                new() { itemId="item_iron_sword", itemName="铁剑", basePrice=100, currentStock=2, maxStock=5, demand=0.3f },
                new() { itemId="item_leather_armor", itemName="皮甲", basePrice=80, currentStock=2, maxStock=5, demand=0.3f },
                new() { itemId="item_spirit_core_001", itemName="灵气核心", basePrice=200, currentStock=3, maxStock=8, demand=0.9f },
            };
        }

        /// <summary>获取当前市场价（供需调整后）</summary>
        public int GetMarketPrice(string itemId)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item == null) return 0;

            float scarcityRatio = 1f - (float)item.currentStock / item.maxStock;
            float demandFactor = item.demand;
            float multiplier = 1f + scarcityRatio * demandFactor * 2f; // 缺货+高需求→最高3x价格

            return Mathf.RoundToInt(item.basePrice * multiplier);
        }

        /// <summary>玩家购买→减少库存</summary>
        public void OnPlayerPurchase(string itemId, int quantity = 1)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item != null)
                item.currentStock = Mathf.Max(0, item.currentStock - quantity);
        }

        /// <summary>玩家出售→增加库存</summary>
        public void OnPlayerSell(string itemId, int quantity = 1)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item != null)
                item.currentStock = Mathf.Min(item.maxStock, item.currentStock + quantity);
        }

        void OnDayPassed(Dictionary<string, object> data)
        {
            // 每天NPC补货
            foreach (var item in market)
            {
                int restock = Mathf.RoundToInt(item.maxStock * 0.2f);
                item.currentStock = Mathf.Min(item.maxStock, item.currentStock + restock);
            }
            Debug.Log("[市场] 📦 每日补货完成。");
        }

        public void ShowMarketReport()
        {
            Debug.Log($"═══════ 市场行情 ═══════");
            foreach (var item in market)
            {
                int price = GetMarketPrice(item.itemId);
                string trend = price > item.basePrice ? "📈" : price < item.basePrice ? "📉" : "➡️";
                Debug.Log($"  {trend} {item.itemName}: {price}灵石 (库存{item.currentStock}/{item.maxStock})");
            }
        }

        void OnDestroy() => EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
    }
}
