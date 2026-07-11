using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 市场供需系统 —— 商品价格随供需波动。不是固定价格。
    /// 购买→库存减少→价格上涨。NPC补货→价格回落。
    /// V1经济扩展：名声影响 + 随机事件影响 + 新物品定价。
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

        // 随机事件造成的临时价格修正（key=itemId, value=倍率）
        private Dictionary<string, float> _eventPriceModifiers = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupMarket();
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
            EventBus.Subscribe("OnRandomEvent", OnRandomEvent);
        }

        void SetupMarket()
        {
            // 价格对齐经济平衡文档V1 5.3节
            market = new List<MarketItem>
            {
                new() { itemId="item_heal_pill_001", itemName="回血丹", basePrice=40, currentStock=20, maxStock=30, demand=0.7f },
                new() { itemId="item_pill_001", itemName="聚气丹", basePrice=25, currentStock=15, maxStock=25, demand=0.8f },
                new() { itemId="item_spirit_stone", itemName="灵石碎片", basePrice=10, currentStock=40, maxStock=50, demand=0.5f },
                new() { itemId="item_herb_001", itemName="止血草", basePrice=10, currentStock=25, maxStock=35, demand=0.6f },
                new() { itemId="item_iron_sword", itemName="铁剑", basePrice=100, currentStock=2, maxStock=5, demand=0.3f },
                new() { itemId="item_leather_armor", itemName="皮甲", basePrice=80, currentStock=2, maxStock=5, demand=0.3f },
                new() { itemId="item_spirit_core_001", itemName="灵气核心", basePrice=50, currentStock=5, maxStock=15, demand=0.9f },
                // 新增物品（经济平衡文档V1 5.3节）
                new() { itemId="item_steel_sword", itemName="精钢剑", basePrice=250, currentStock=1, maxStock=3, demand=0.4f },
                new() { itemId="item_spirit_jade", itemName="灵玉", basePrice=150, currentStock=3, maxStock=8, demand=0.6f },
                new() { itemId="item_void_crystal", itemName="虚空水晶", basePrice=300, currentStock=1, maxStock=3, demand=0.9f },
                new() { itemId="item_phoenix_feather", itemName="凤凰羽", basePrice=500, currentStock=1, maxStock=2, demand=0.95f },
                new() { itemId="item_ancient_rune", itemName="上古符文", basePrice=1000, currentStock=1, maxStock=2, demand=0.9f },
            };
        }

        /// <summary>
        /// 获取当前市场价（供需调整 + 名声 + 事件）
        /// 公式：基准价 × (1 + 稀缺系数 × 需求系数 × 波动幅度) × 名声修正 × 事件修正
        /// </summary>
        public int GetMarketPrice(string itemId)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item == null) return 0;

            // 供需波动：缺货率越高价格越贵，需求越大波动越剧烈
            float scarcityRatio = 1f - (float)item.currentStock / item.maxStock;
            float multiplier = 1f + scarcityRatio * item.demand * 2f;

            // 名声修正（善名低价，恶名高价）
            if (ReputationSystem.Instance != null)
                multiplier *= ReputationSystem.Instance.ShopPriceModifier;

            // 随机事件临时价格修正
            if (_eventPriceModifiers.ContainsKey(itemId))
                multiplier *= _eventPriceModifiers[itemId];

            return Mathf.RoundToInt(item.basePrice * multiplier);
        }

        /// <summary>玩家购买→减少库存（价格随之上升）</summary>
        public void OnPlayerPurchase(string itemId, int quantity = 1)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item != null)
            {
                int before = item.currentStock;
                item.currentStock = Mathf.Max(0, item.currentStock - quantity);
                Debug.Log($"[市场] 玩家购入 {quantity}x {item.itemName}（库存:{before}→{item.currentStock}）");
            }
        }

        /// <summary>玩家出售→增加库存（价格随之下降）</summary>
        public void OnPlayerSell(string itemId, int quantity = 1)
        {
            var item = market.Find(m => m.itemId == itemId);
            if (item != null)
            {
                int before = item.currentStock;
                item.currentStock = Mathf.Min(item.maxStock, item.currentStock + quantity);
                Debug.Log($"[市场] 玩家出售 {quantity}x {item.itemName}（库存:{before}→{item.currentStock}）");
            }
        }

        /// <summary>随机事件影响特定商品价格</summary>
        public void ApplyEventPriceModifier(string itemId, float modifier)
        {
            if (modifier <= 0f) return;
            _eventPriceModifiers[itemId] = modifier;
            Debug.Log($"[市场] 随机事件影响 {itemId} 价格 x{modifier:F2}");
        }

        /// <summary>每日NPC补货→价格回落</summary>
        void OnDayPassed(Dictionary<string, object> data)
        {
            foreach (var item in market)
            {
                // 每日补充20%最大库存
                int restock = Mathf.RoundToInt(item.maxStock * 0.2f);
                // 高需求物品补货略少（市场逻辑）
                if (item.demand > 0.8f)
                    restock = Mathf.RoundToInt(restock * 0.8f);
                restock = Mathf.Max(1, restock);

                int before = item.currentStock;
                item.currentStock = Mathf.Min(item.maxStock, item.currentStock + restock);
                if (item.currentStock > before)
                    Debug.Log($"[市场] 补货 {item.itemName}: {before}→{item.currentStock}");
            }

            // 每日清除事件价格修正
            _eventPriceModifiers.Clear();
            Debug.Log("[市场] 每日补货完成，价格回落。");
        }

        /// <summary>响应随机事件，影响相关商品价格</summary>
        void OnRandomEvent(Dictionary<string, object> data)
        {
            if (data == null) return;

            if (data.ContainsKey("affectedItem") && data.ContainsKey("priceModifier"))
            {
                string itemId = data["affectedItem"] as string;
                float modifier = 1f;
                if (data["priceModifier"] is float fVal)
                    modifier = fVal;
                else if (data["priceModifier"] is int iVal)
                    modifier = iVal;

                if (!string.IsNullOrEmpty(itemId))
                    ApplyEventPriceModifier(itemId, modifier);
            }
        }

        public void ShowMarketReport()
        {
            Debug.Log($"═══════ 市场行情 ═══════");
            foreach (var item in market)
            {
                int price = GetMarketPrice(item.itemId);
                string trend = price > item.basePrice ? "📈" : price < item.basePrice ? "📉" : "➡️";
                string eventNote = _eventPriceModifiers.ContainsKey(item.itemId) ? " [事件影响]" : "";
                Debug.Log($"  {trend} {item.itemName}: {price}灵石 (库存{item.currentStock}/{item.maxStock}){eventNote}");
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
            EventBus.Unsubscribe("OnRandomEvent", OnRandomEvent);
        }
    }
}
