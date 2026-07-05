using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 宝箱 —— 走近按E打开，随机获得物品。
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        public float openRange = 3f;
        public bool isOpened = false;

        [System.Serializable]
        public class LootEntry { public string itemId; public string itemName; public string type; public string rarity; public int qty; public int value; public float weight; }

        public LootEntry[] possibleLoot = new LootEntry[] {
            new() { itemId="item_herb_001", itemName="止血草", type="Consumable", rarity="N", qty=5, value=10, weight=0.3f },
            new() { itemId="item_spirit_stone", itemName="灵石碎片", type="Material", rarity="R", qty=3, value=50, weight=0.25f },
            new() { itemId="item_pill_001", itemName="聚气丹", type="Consumable", rarity="R", qty=2, value=30, weight=0.2f },
            new() { itemId="item_heal_pill_001", itemName="回血丹", type="Consumable", rarity="R", qty=1, value=40, weight=0.15f },
            new() { itemId="item_spirit_core_001", itemName="灵气核心", type="Material", rarity="SR", qty=1, value=200, weight=0.1f },
        };

        private Transform _player;
        private bool _playerInRange;
        private GameObject _visual;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            CreateVisual();
        }

        void CreateVisual()
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.transform.SetParent(transform);
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localScale = new Vector3(0.8f, 0.6f, 0.6f);
            var r = _visual.GetComponent<Renderer>();
            if (r != null) { var m = new Material(Shader.Find("Standard")); m.color = new Color(0.6f, 0.3f, 0.05f); r.material = m; }

            // Lid
            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Lid";
            lid.transform.SetParent(transform);
            lid.transform.localPosition = Vector3.up * 0.35f;
            lid.transform.localScale = new Vector3(0.85f, 0.1f, 0.65f);
            var lr = lid.GetComponent<Renderer>();
            if (lr != null) { var lm = new Material(Shader.Find("Standard")); lm.color = new Color(0.7f, 0.4f, 0.1f); lr.material = lm; }

            // Glow particle (simplified: just a small sphere)
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(transform);
            glow.transform.localPosition = Vector3.up * 0.7f;
            glow.transform.localScale = Vector3.one * 0.15f;
            var gr = glow.GetComponent<Renderer>();
            if (gr != null) { var gm = new Material(Shader.Find("Standard")); gm.color = new Color(1f, 0.8f, 0.2f); gm.EnableKeyword("_EMISSION"); gm.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.2f) * 0.5f); gr.material = gm; }
            glow.GetComponent<Collider>().isTrigger = true;
        }

        void Update()
        {
            if (isOpened || _player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            _playerInRange = dist <= openRange;

            if (_playerInRange && Input.GetKeyDown(KeyCode.E))
            {
                Open();
            }
        }

        void Open()
        {
            if (isOpened) return;
            isOpened = true;

            // Weighted random loot
            float totalWeight = 0;
            foreach (var l in possibleLoot) totalWeight += l.weight;
            float roll = Random.Range(0, totalWeight);
            float cum = 0;
            LootEntry selected = possibleLoot[0];
            foreach (var l in possibleLoot)
            {
                cum += l.weight;
                if (roll <= cum) { selected = l; break; }
            }

            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.AddItem(new Item
                {
                    id = selected.itemId, name = selected.itemName,
                    type = selected.type, rarity = selected.rarity,
                    quantity = selected.qty, value = selected.value
                });
            }

            Debug.Log($"[宝箱] 打开！获得 [{selected.rarity}] {selected.itemName} x{selected.qty}!");
            EventBus.Publish("OnChestOpened", new Dictionary<string, object> {
                {"itemName", selected.itemName}, {"rarity", selected.rarity}
            });

            // Visual feedback
            var r = _visual.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.3f, 0.2f, 0.1f);
            transform.localScale = Vector3.one * 0.7f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, openRange);
        }
    }
}
