using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.World
{
    /// <summary>
    /// 物品数据加载器
    ///
    /// 从 Resources/Data/Items.json 加载完整物品库（50件），
    /// 注入 ItemDatabase 供游戏运行时使用。
    ///
    /// JSON 是唯一天源——改数据不动代码。
    ///
    /// 使用方式：
    ///   任意 MonoBehaviour 中调用：
    ///     ItemDataLoader.LoadFromResources();
    ///
    ///   或将此脚本挂载到任意 GameObject 并勾选 loadOnAwake。
    /// </summary>
    public class ItemDataLoader : MonoBehaviour
    {
        [Header("=== 加载配置 ===")]
        [SerializeField, Tooltip("Resources 路径（不含扩展名）")]
        private string jsonResourcesPath = "Data/Items";

        [SerializeField, Tooltip("场景启动时自动加载")]
        private bool loadOnAwake = true;

        [SerializeField, Tooltip("加载前是否清空已有物品数据")]
        private bool clearBeforeLoad;

        [Header("=== 状态 ===")]
        [SerializeField]
        private int lastLoadedCount;

        [SerializeField]
        private bool loadSucceeded;

        // ────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadFromResources(jsonResourcesPath, clearBeforeLoad);
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  实例方法 (Inspector 可调用)
        // ────────────────────────────────────────────────────────────────

        [ContextMenu("重新加载物品数据")]
        public void Reload()
        {
            LoadFromResources(jsonResourcesPath, clearBeforeLoad);
        }

        // ────────────────────────────────────────────────────────────────
        //  静态 API
        // ────────────────────────────────────────────────────────────────

        /// <summary>所有物品定义的主字典（itemId -> ItemDef）</summary>
        public static Dictionary<string, ItemDef> AllItems { get; private set; } = new();

        /// <summary>按品质分组的物品列表</summary>
        public static Dictionary<string, List<ItemDef>> ItemsByQuality { get; private set; } = new();

        /// <summary>按类型分组的物品列表</summary>
        public static Dictionary<string, List<ItemDef>> ItemsByType { get; private set; } = new();

        /// <summary>
        /// 从 Resources 加载物品 JSON 并注入 ItemDatabase。
        /// </summary>
        /// <param name="path">Resources 路径（不含扩展名，默认 "Data/Items"）</param>
        /// <param name="clearFirst">加载前是否清空已有数据</param>
        /// <returns>成功加载的物品数量，-1 表示失败</returns>
        public static int LoadFromResources(string path = "Data/Items", bool clearFirst = false)
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(path);
            if (jsonAsset == null)
            {
                Debug.LogWarning($"[ItemDataLoader] 未找到物品数据: {path}.json (Resources 路径)");
                return -1;
            }

            var wrapper = JsonUtility.FromJson<ItemDatabaseJson>(jsonAsset.text);
            if (wrapper?.items == null || wrapper.items.Length == 0)
            {
                Debug.LogWarning("[ItemDataLoader] 物品数据为空或格式无效");
                return -1;
            }

            if (clearFirst)
            {
                AllItems.Clear();
                ItemsByQuality.Clear();
                ItemsByType.Clear();
                Debug.Log("[ItemDataLoader] 已清空现有物品数据");
            }

            int loadedCount = 0;
            foreach (var def in wrapper.items)
            {
                if (string.IsNullOrEmpty(def.itemId))
                {
                    Debug.LogWarning("[ItemDataLoader] 跳过空 itemId 的物品");
                    continue;
                }

                // 注入主字典
                AllItems[def.itemId] = def;

                // 按品质索引
                if (!ItemsByQuality.ContainsKey(def.quality))
                    ItemsByQuality[def.quality] = new List<ItemDef>();
                ItemsByQuality[def.quality].Add(def);

                // 按类型索引
                if (!ItemsByType.ContainsKey(def.itemType))
                    ItemsByType[def.itemType] = new List<ItemDef>();
                ItemsByType[def.itemType].Add(def);

                // 注入 ItemDatabase.Stories（世界观/故事系统）
                ItemDatabase.Stories[def.itemId] = new ItemStory
                {
                    displayName = def.displayName,
                    rarityName = def.rarityName,
                    story = def.description,
                    origin = def.origin ?? "未知来源"
                };

                loadedCount++;
            }

            Debug.Log($"[ItemDataLoader] 成功加载 {loadedCount} 个物品 ← {path}.json" +
                       $" 品质分布: R={CountByQuality("R")} SR={CountByQuality("SR")}" +
                       $" SSR={CountByQuality("SSR")} UR={CountByQuality("UR")}");

            return loadedCount;
        }

        /// <summary>获取物品定义。</summary>
        public static ItemDef GetDef(string itemId)
        {
            return AllItems.TryGetValue(itemId, out var def) ? def : null;
        }

        /// <summary>获取某品质的物品数量。</summary>
        public static int CountByQuality(string quality)
        {
            return ItemsByQuality.TryGetValue(quality, out var list) ? list.Count : 0;
        }

        /// <summary>获取某类型的物品数量。</summary>
        public static int CountByType(string type)
        {
            return ItemsByType.TryGetValue(type, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// 从 ItemDef 创建运行时 Item 对象（用于背包/装备系统）。
        /// </summary>
        /// <param name="def">物品定义</param>
        /// <param name="quantity">数量（默认 1）</param>
        /// <returns>可直接使用的 Item 实例</returns>
        public static Item CreateRuntimeItem(ItemDef def, int quantity = 1)
        {
            return new Item
            {
                id = def.itemId,
                name = def.displayName,
                description = def.description,
                type = MapItemType(def.itemType),
                rarity = def.quality,
                quantity = quantity,
                value = def.sellPrice,
                icon = def.iconHint
            };
        }

        /// <summary>按 itemId 创建运行时 Item 对象。</summary>
        public static Item CreateRuntimeItemById(string itemId, int quantity = 1)
        {
            var def = GetDef(itemId);
            if (def == null)
            {
                Debug.LogError($"[ItemDataLoader] 未找到物品定义: {itemId}");
                return null;
            }
            return CreateRuntimeItem(def, quantity);
        }

        /// <summary>获取所有物品的 Item 对象列表（数量 1）。</summary>
        public static List<Item> GetAllRuntimeItems()
        {
            var list = new List<Item>(AllItems.Count);
            foreach (var def in AllItems.Values)
            {
                list.Add(CreateRuntimeItem(def, 1));
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        //  内部映射
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 将 JSON 中的通用 itemType 映射到 Item.type 需要的取值。
        /// 装备系直接映射，其余归入 Consumable / Material / Accessory 等。
        /// </summary>
        private static string MapItemType(string jsonItemType)
        {
            return jsonItemType switch
            {
                "Weapon" => "Weapon",
                "Armor"  => "Armor",
                "Elixir" => "Consumable",
                "Material" => "Material",
                "Consumable" => "Consumable",
                "Special" => "Accessory",
                _ => jsonItemType
            };
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  JSON 数据模型 (与 Items.json 严格对应)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>JSON 根容器。</summary>
    [System.Serializable]
    public class ItemDatabaseJson
    {
        public ItemDef[] items;
    }

    /// <summary>物品定义——单件物品的全部数据。</summary>
    [System.Serializable]
    public class ItemDef
    {
        // ── 基础 ──
        public string itemId;        // 唯一标识（如 "wpn_iron_sword"）
        public string displayName;   // 显示名称（如 "断水"）
        public string description;   // 描述/故事文本
        public string itemType;      // Weapon / Armor / Elixir / Material / Consumable / Special
        public string quality;       // R / SR / SSR / UR
        public string iconHint;      // 图标路径提示

        // ── 游戏机制 ──
        public bool stackable;
        public int sellPrice;
        public string equipSlot;     // MainHand / OffHand / Chest / Back / Accessory （非装备类为 null）

        // ── 属性 ──
        public ItemStats stats;       // 装备属性（武器/防具/饰品）
        public ItemEffects effects;   // 消耗效果（丹药/符箓/特殊）

        // ── 世界观 ──
        public string origin;        // 来源
        public string rarityName;    // 品质名称（如 "精铁剑"）

        /// <summary>是否为可装备物品。</summary>
        public bool IsEquippable =>
            itemType == "Weapon" || itemType == "Armor" || itemType == "Special";
    }

    /// <summary>装备属性。</summary>
    [System.Serializable]
    public class ItemStats
    {
        // 基础战斗
        public int attack;
        public int magicAttack;
        public int defense;
        public int magicDefense;
        public int health;
        public int spiritPower;

        // 进阶
        public int critRate;         // 暴击率（百分比值，如 5 表示 5%）
        public int dodge;            // 闪避率
        public int armorPenetration; // 破甲
        public int spiritRegen;      // 灵力回复/秒

        // 元素
        public int iceAttack;
        public int fireAttack;
        public int lightningAttack;
        public int lightningResist;
        public int allResist;

        // 特殊
        public int socialStealth;
        public int luck;
        public int allStats;
        public float attackSpeed;    // 攻速倍率（1.0 = 正常）
    }

    /// <summary>消耗品/特殊效果。</summary>
    [System.Serializable]
    public class ItemEffects
    {
        // 恢复类
        public int restoreHealth;
        public int restoreSpirit;

        // 突破类
        public string breakthrough;       // 突破境界标识
        public float successRate;        // 突破成功率
        public float tribulationBoost;   // 渡劫加成

        // 增益类
        public float cultivationBoost;   // 修炼速度倍率
        public float insight;            // 悟性倍率
        public bool preventQiDeviation;  // 防止走火入魔
        public int duration;             // 持续秒数
        public string sideEffect;        // 副作用描述

        // 永久类
        public int spiritCapacity;       // 灵力上限增加
        public float allStatsPermanent;  // 全属性永久提升
        public bool enlightenment;       // 顿悟

        // 功能类
        public string usageType;         // immediate / buff / breakthrough / permanent
        public bool teleportToMark;      // 传送
        public bool escapeCombat;        // 遁地
        public bool barrierBreak;        // 破障
        public string maxBarrierLevel;   // 可破解最高禁制等级
        public bool summonBeast;         // 召唤
        public string beastLevel;        // 召唤灵兽等级

        // 特殊
        public bool revive;              // 复活
        public bool fullRestore;         // 完全恢复
        public bool seeTruth;            // 勘破虚妄
        public bool curePoison;          // 解毒

        // 身份类
        public bool identitySwitch;      // 双面令
        public int maxSwitches;
        public bool changeAppearance;    // 易容
        public bool rename;              // 改名

        // 收集/剧情类
        public bool collectible;         // 收集品
        public bool fragmentOfTruth;     // 真相碎片
        public bool revealTruth;         // 揭示真相

        // 材料类
        public bool craftingMaterial;    // 是否为锻造/炼药材料
        public string category;          // 材料分类（herb / liquid / scale / fang / essence / shard / crystal / stone / sand）
        public int forgingPower;         // 锻造威力
        public int magicalPower;         // 魔力
        public float enlightenmentChance; // 悟道概率
    }
}
