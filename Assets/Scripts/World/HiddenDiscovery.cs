using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 隐藏发现 —— 探索特定位置触发事件。
    /// 没有UI标记。只有走过去了才知道有什么。
    /// </summary>
    public class HiddenDiscovery : MonoBehaviour
    {
        public string discoveryId;
        public string discoveryName;
        public string discoveryText;
        public float triggerRange = 3f;
        public string rewardItemId;
        public string rewardItemName;
        public int rewardQuantity = 1;
        public int rewardCultivation;

        private bool _discovered;
        private Transform _player;

        void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void Update()
        {
            if (_discovered || _player == null) return;
            if (Vector3.Distance(transform.position, _player.position) <= triggerRange)
            {
                Discover();
            }
        }

        void Discover()
        {
            _discovered = true;
            Debug.Log($"🔍 [发现] {discoveryName}");
            Debug.Log($"   {discoveryText}");

            if (!string.IsNullOrEmpty(rewardItemId))
            {
                var inv = InventoryManager.Instance;
                inv?.AddItem(new Item { id = rewardItemId, name = rewardItemName, quantity = rewardQuantity, value = 50 });
                Debug.Log($"   📦 获得: {rewardItemName} x{rewardQuantity}");
            }

            if (rewardCultivation > 0)
            {
                PlayerStats.Instance?.AddCultivation(rewardCultivation);
                Debug.Log($"   ⭐ +{rewardCultivation}修为");
            }

            EventBus.Publish("OnDiscoveryFound", new Dictionary<string, object> {
                {"id", discoveryId}, {"name", discoveryName}
            });
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, triggerRange);
        }
    }
}
