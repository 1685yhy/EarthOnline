using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 世界中的可拾取物品 —— 走近自动拾取。
    /// </summary>
    public class WorldPickup : MonoBehaviour
    {
        public string itemId;
        public string itemName = "未知物品";
        public string itemType = "Consumable";
        public string itemRarity = "N";
        public int quantity = 1;
        public int value = 10;
        public float pickupRange = 2f;
        public float bobHeight = 0.3f;
        public float bobSpeed = 2f;
        public float spinSpeed = 30f;

        private Transform _player;
        private Vector3 _startPos;

        void Start()
        {
            _startPos = transform.position;
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            // 默认外观：发光小球
            if (GetComponent<Renderer>() == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(transform);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * 0.3f;
                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    // 根据品质改颜色
                    mat.color = itemRarity switch
                    {
                        "SR" => new Color(0.8f, 0.2f, 1f),   // 紫色
                        "R" => new Color(0.2f, 0.5f, 1f),    // 蓝色
                        "SSR" => new Color(1f, 0.8f, 0.1f),  // 金色
                        _ => new Color(0.5f, 0.8f, 0.5f),    // 绿色(N)
                    };
                    // 自发光效果
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", mat.color * 0.3f);
                    renderer.material = mat;
                }
                sphere.GetComponent<Collider>().isTrigger = true;
            }
        }

        void Update()
        {
            // 上下浮动
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _startPos + Vector3.up * bob;
            // 旋转
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= pickupRange)
            {
                PickUp();
            }
        }

        void PickUp()
        {
            var inv = InventoryManager.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[WorldPickup] No InventoryManager found!");
                return;
            }

            var item = new Item
            {
                id = itemId,
                name = itemName,
                type = itemType,
                rarity = itemRarity,
                quantity = quantity,
                value = value,
                description = $"从世界中拾取的{itemName}"
            };

            if (inv.AddItem(item))
            {
                string storyName = ItemDatabase.GetDisplayName(itemId);
                string displayName = storyName != itemId ? $"{storyName}({itemName})" : itemName;
                Debug.Log($"[Pickup] ✨ 获得 [{itemRarity}] {displayName} x{quantity}");
                if (ItemDatabase.Stories.ContainsKey(itemId))
                    Debug.Log($"[Pickup] '{ItemDatabase.Stories[itemId].story}'");
                Destroy(gameObject);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
