using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 物品系统 —— 物品定义、背包管理、世界拾取。
    /// </summary>
    [System.Serializable]
    public class Item
    {
        public string id;
        public string name;
        public string description;
        public string type;     // Consumable, Weapon, Material, Quest, Skill
        public string rarity;   // N, R, SR, SSR
        public int quantity;
        public int value;
        public string icon;     // future: sprite path

        public Item Clone()
        {
            return new Item
            {
                id = id, name = name, description = description,
                type = type, rarity = rarity, quantity = quantity, value = value, icon = icon
            };
        }
    }

    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public int maxSlots = 20;
        private List<Item> _items = new List<Item>();

        public event System.Action OnInventoryChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool AddItem(Item item)
        {
            var existing = _items.Find(i => i.id == item.id);
            if (existing != null)
            {
                existing.quantity += item.quantity;
            }
            else
            {
                if (_items.Count >= maxSlots)
                {
                    Debug.LogWarning($"[Inventory] Full! Cannot add {item.name}");
                    return false;
                }
                _items.Add(item.Clone());
            }

            Debug.Log($"[Inventory] +{item.quantity}x {item.name} ({_items.Count}/{maxSlots})");
            EventBus.Publish("OnItemAdded", new Dictionary<string, object>
            {
                {"itemId", item.id}, {"itemName", item.name}, {"quantity", item.quantity}
            });
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            var item = _items.Find(i => i.id == itemId);
            if (item == null) return false;

            item.quantity -= quantity;
            if (item.quantity <= 0)
                _items.Remove(item);

            EventBus.Publish("OnItemRemoved", new Dictionary<string, object>
            {
                {"itemId", itemId}, {"quantity", quantity}
            });
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool HasItem(string itemId, int quantity = 1)
        {
            var item = _items.Find(i => i.id == itemId);
            return item != null && item.quantity >= quantity;
        }

        public Item GetItem(string itemId)
        {
            return _items.Find(i => i.id == itemId);
        }

        public List<Item> GetAllItems() => new List<Item>(_items);

        public int Count => _items.Count;
        public bool IsFull => _items.Count >= maxSlots;
    }
}
