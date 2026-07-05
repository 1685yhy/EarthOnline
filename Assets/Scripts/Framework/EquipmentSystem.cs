using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 装备系统 —— 武器/防具/饰品插槽，影响战斗属性。
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        public enum Slot { Weapon, Armor, Accessory }

        public Item Weapon { get; private set; }
        public Item Armor { get; private set; }
        public Item Accessory { get; private set; }

        public int AttackBonus => (Weapon?.value ?? 0) / 10;
        public int DefenseBonus => (Armor?.value ?? 0) / 15;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        public bool Equip(Item item)
        {
            if (item.type != "Weapon" && item.type != "Armor" && item.type != "Accessory")
            {
                Debug.LogWarning($"[Equip] {item.name} 不可装备 (type={item.type})");
                return false;
            }

            Slot slot = item.type switch
            {
                "Weapon" => Slot.Weapon,
                "Armor" => Slot.Armor,
                _ => Slot.Accessory
            };

            // Unequip old item
            var old = GetEquipped(slot);
            if (old != null)
            {
                InventoryManager.Instance?.AddItem(old);
                Debug.Log($"[Equip] 卸下 {old.name}");
            }

            switch (slot)
            {
                case Slot.Weapon: Weapon = item; break;
                case Slot.Armor: Armor = item; break;
                case Slot.Accessory: Accessory = item; break;
            }

            Debug.Log($"[Equip] 装备 [{item.rarity}] {item.name} (攻击+{AttackBonus} 防御+{DefenseBonus})");
            EventBus.Publish("OnEquipmentChanged", new Dictionary<string, object> {
                {"slot", slot.ToString()}, {"itemName", item.name}, {"attackBonus", AttackBonus}, {"defenseBonus", DefenseBonus}
            });
            return true;
        }

        public Item Unequip(Slot slot)
        {
            var old = GetEquipped(slot);
            switch (slot)
            {
                case Slot.Weapon: Weapon = null; break;
                case Slot.Armor: Armor = null; break;
                case Slot.Accessory: Accessory = null; break;
            }
            if (old != null) Debug.Log($"[Equip] 卸下 {old.name}");
            return old;
        }

        public Item GetEquipped(Slot slot) => slot switch
        {
            Slot.Weapon => Weapon,
            Slot.Armor => Armor,
            _ => Accessory
        };

        public string GetSummary()
        {
            return $"⚔️ {(Weapon != null ? $"[{Weapon.rarity}]{Weapon.name}" : "空手")} | " +
                   $"🛡️ {(Armor != null ? $"[{Armor.rarity}]{Armor.name}" : "布衣")} | " +
                   $"💍 {(Accessory != null ? $"[{Accessory.rarity}]{Accessory.name}" : "无")} | " +
                   $"攻+{AttackBonus} 防+{DefenseBonus}";
        }
    }
}
