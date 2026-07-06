using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 钓鱼点 —— 可在水边钓鱼获取材料和灵石。
    /// 修真世界的一种休闲获取资源方式。
    /// </summary>
    public class FishingSpot : MonoBehaviour
    {
        public string spotName = "钓鱼点";
        public float fishRange = 3f;
        public float fishCooldown = 10f;
        private float _lastFishTime;
        private Transform _player;
        private bool _inRange;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void Update()
        {
            if (_player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            _inRange = dist <= fishRange;

            if (_inRange && Input.GetKeyDown(KeyCode.F) && Time.time - _lastFishTime >= fishCooldown)
            {
                Fish();
            }
        }

        void Fish()
        {
            _lastFishTime = Time.time;
            float roll = Random.value;

            if (roll < 0.4f) // 普通鱼
            {
                var inv = InventoryManager.Instance;
                inv?.AddItem(new Item { id = "item_herb_001", name = "水草", type = "Material", rarity = "N", quantity = 2, value = 5 });
                Debug.Log($"[钓鱼] 🎣 钓到了水草×2！");
            }
            else if (roll < 0.7f) // 灵石
            {
                int stones = Random.Range(5, 20);
                PlayerStats.Instance?.AddSpiritStone(stones);
                Debug.Log($"[钓鱼] 💎 钓到了{stones}灵石！");
            }
            else if (roll < 0.9f) // 稀有物品
            {
                var inv = InventoryManager.Instance;
                inv?.AddItem(new Item { id = "item_spirit_stone", name = "灵石碎片", type = "Material", rarity = "R", quantity = 1, value = 15 });
                Debug.Log("[钓鱼] ✨ 钓到了灵石碎片！");
            }
            else // 宝藏
            {
                PlayerStats.Instance?.AddCultivation(30);
                Debug.Log("[钓鱼] 🏆 钓到了一个古旧宝盒！打开获得+30修为！");
                Debug.Log("[钓鱼] 宝盒里还有一张纸条：'留给下一个在河边的人——修行不易，保重。——无名修士'");
            }
        }
    }
}
