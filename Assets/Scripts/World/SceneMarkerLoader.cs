using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;
using System.Linq;

namespace EarthOnline.World
{
    /// <summary>
    /// 场景标记加载器 —— 从 SceneMarkers.json 读取放置数据，
    /// 在运行时实例化灵脉、地下城入口、发现点、快速旅行点和宝箱。
    ///
    /// 设计原则：
    /// - 数据驱动：所有标记位置和参数由 JSON 配置，不动代码改布局。
    /// - 与现有组件兼容：使用 SpiritVein / DungeonEntrance / HiddenDiscovery /
    ///   FastTravel / TreasureChest 已有组件，不重复造轮子。
    /// - 防重复：提供 shouldClearExisting 参数清理场景中已有标记。
    /// </summary>
    public class SceneMarkerLoader : MonoBehaviour
    {
        [Header("加载配置")]
        [SerializeField] private string jsonResourcePath = "Data/SceneMarkers";
        [Tooltip("加载前是否清除场景中已有的同名标记（按组件类型清理）")]
        [SerializeField] private bool shouldClearExisting = true;

        /// <summary>所有已生成的标记根对象，层级整洁。 </summary>
        private GameObject _markersRoot;
        private SceneMarkerData _data;

        // ════════════════════════════════════════════════════════════════
        //  Singleton
        // ════════════════════════════════════════════════════════════════

        public static SceneMarkerLoader Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            LoadAndSpawnAll();
        }

        // ════════════════════════════════════════════════════════════════
        //  公开加载入口
        // ════════════════════════════════════════════════════════════════

        /// <summary>加载 JSON 并生成所有场景标记。</summary>
        public void LoadAndSpawnAll()
        {
            _data = ConfigLoader.Load<SceneMarkerData>(jsonResourcePath);
            if (_data == null)
            {
                Debug.LogError("[SceneMarkerLoader] 无法加载 SceneMarkers.json，请检查资源路径。");
                return;
            }

            if (shouldClearExisting)
                ClearExistingMarkers();

            _markersRoot = new GameObject("_SceneMarkers");
            _markersRoot.transform.SetParent(transform);

            SpawnSpiritVeins();
            SpawnDungeonEntrances();
            SpawnHiddenDiscoveries();
            SpawnFastTravelPoints();
            SpawnTreasureChests();

            Debug.Log($"[SceneMarkerLoader] 场景标记加载完成：" +
                $"灵脉{_data.spiritVeins.Length}个，地下城{_data.dungeonEntrances.Length}个，" +
                $"发现{_data.hiddenDiscoveries.Length}个，旅行点{_data.fastTravelPoints.Length}个，" +
                $"宝箱{_data.treasureChests.Length}个。");
        }

        /// <summary>在场景中创建单个标记类型的运行时实例。 </summary>
        public GameObject SpawnSingleMarker(string markerType, string markerId)
        {
            if (_data == null)
            {
                Debug.LogError("[SceneMarkerLoader] 数据未加载，请先调用 LoadAndSpawnAll。");
                return null;
            }

            switch (markerType)
            {
                case "spiritVein":
                    var sv = _data.spiritVeins.FirstOrDefault(s => s.id == markerId);
                    return sv != null ? SpawnSpiritVein(sv, _markersRoot?.transform ?? transform) : null;

                case "dungeon":
                    var de = _data.dungeonEntrances.FirstOrDefault(d => d.id == markerId);
                    return de != null ? SpawnDungeonEntrance(de, _markersRoot?.transform ?? transform) : null;

                case "discovery":
                    var hd = _data.hiddenDiscoveries.FirstOrDefault(h => h.id == markerId);
                    return hd != null ? SpawnHiddenDiscovery(hd, _markersRoot?.transform ?? transform) : null;

                case "fastTravel":
                    var ft = _data.fastTravelPoints.FirstOrDefault(f => f.pointId == markerId);
                    return ft != null ? SpawnFastTravelPoint(ft, _markersRoot?.transform ?? transform) : null;

                case "chest":
                    var tc = _data.treasureChests.FirstOrDefault(c => c.id == markerId);
                    return tc != null ? SpawnTreasureChest(tc, _markersRoot?.transform ?? transform) : null;

                default:
                    Debug.LogWarning($"[SceneMarkerLoader] 未知标记类型: {markerType}");
                    return null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  内部生成方法
        // ════════════════════════════════════════════════════════════════

        #region Spawn Methods

        private void SpawnSpiritVeins()
        {
            var container = new GameObject("SpiritVeins");
            container.transform.SetParent(_markersRoot.transform);

            foreach (var vein in _data.spiritVeins)
                SpawnSpiritVein(vein, container.transform);
        }

        private GameObject SpawnSpiritVein(SpiritVeinData vein, Transform parent)
        {
            var go = new GameObject(vein.veinName ?? vein.id);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(vein.position.x, vein.position.y, vein.position.z);

            var sv = go.AddComponent<SpiritVein>();
            sv.veinName = vein.veinName ?? vein.id;
            sv.cultivationMultiplier = vein.cultivationMultiplier;
            sv.spiritRegenBonus = vein.spiritRegenBonus;
            sv.radius = vein.radius;

            var challenge = go.AddComponent<SpiritVeinChallenge>();
            challenge.dailyCost = vein.dailyCost;
            challenge.ownerName = "无主";

            return go;
        }

        private void SpawnDungeonEntrances()
        {
            var container = new GameObject("DungeonEntrances");
            container.transform.SetParent(_markersRoot.transform);

            foreach (var entrance in _data.dungeonEntrances)
                SpawnDungeonEntrance(entrance, container.transform);
        }

        private GameObject SpawnDungeonEntrance(DungeonEntranceData entrance, Transform parent)
        {
            var go = new GameObject(entrance.dungeonName ?? entrance.id);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(entrance.position.x, entrance.position.y, entrance.position.z);

            var de = go.AddComponent<DungeonEntrance>();
            de.dungeonName = entrance.dungeonName ?? entrance.id;
            de.enterRange = entrance.enterRange > 0 ? entrance.enterRange : 3f;
            de.warningMessage = entrance.warningMessage ?? $"⚠️ 前方 {entrance.dungeonName}，建议 {entrance.recommendedRealm} 以上进入。";

            // 挂载额外属性以便其他系统读取（DungeonInstance / 地图UI等）
            var tag = go.AddComponent<MarkerTag>();
            tag.markerType = "dungeon";
            tag.markerId = entrance.id;
            tag.stringData = new Dictionary<string, string>
            {
                { "dungeonId", entrance.dungeonId },
                { "dungeonScene", entrance.dungeonScene },
                { "recommendedRealm", entrance.recommendedRealm },
                { "recommendedLevel", entrance.recommendedLevel.ToString() }
            };

            return go;
        }

        private void SpawnHiddenDiscoveries()
        {
            var container = new GameObject("Discoveries");
            container.transform.SetParent(_markersRoot.transform);

            foreach (var discovery in _data.hiddenDiscoveries)
                SpawnHiddenDiscovery(discovery, container.transform);
        }

        private GameObject SpawnHiddenDiscovery(HiddenDiscoveryData discovery, Transform parent)
        {
            var go = new GameObject(discovery.discoveryName ?? discovery.id);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(discovery.position.x, discovery.position.y, discovery.position.z);

            var hd = go.AddComponent<HiddenDiscovery>();
            hd.discoveryId = discovery.id;
            hd.discoveryName = discovery.discoveryName ?? discovery.id;
            hd.discoveryText = discovery.discoveryText ?? "";
            hd.triggerRange = discovery.triggerRange > 0 ? discovery.triggerRange : 6f;
            hd.rewardItemId = discovery.rewards?.itemId;
            hd.rewardItemName = discovery.rewards?.itemName;
            hd.rewardQuantity = discovery.rewards?.quantity ?? 1;
            hd.rewardCultivation = discovery.rewards?.cultivation ?? 0;

            // 注册条件到 DiscoverySystem
            if (discovery.conditions != null && discovery.conditions.Length > 0)
                RegisterDiscoveryConditions(discovery.id, discovery.conditions);

            return go;
        }

        private void SpawnFastTravelPoints()
        {
            var container = new GameObject("FastTravelPoints");
            container.transform.SetParent(_markersRoot.transform);

            foreach (var point in _data.fastTravelPoints)
                SpawnFastTravelPoint(point, container.transform);
        }

        private GameObject SpawnFastTravelPoint(FastTravelData point, Transform parent)
        {
            var go = new GameObject(point.pointName ?? point.pointId);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(point.position.x, point.position.y, point.position.z);

            var ft = go.AddComponent<FastTravel>();
            ft.pointName = point.pointName ?? point.pointId;
            ft.pointId = point.pointId;
            ft.activateRange = 3f;

            // 挂载解锁条件标记
            var tag = go.AddComponent<MarkerTag>();
            tag.markerType = "fastTravel";
            tag.markerId = point.pointId;
            tag.stringData = new Dictionary<string, string>
            {
                { "unlockCondition", point.unlockCondition ?? "discover" },
                { "unlockRealm", point.unlockRealm ?? "" },
                { "unlockLevel", point.unlockLevel.ToString() }
            };

            return go;
        }

        private void SpawnTreasureChests()
        {
            var container = new GameObject("TreasureChests");
            container.transform.SetParent(_markersRoot.transform);

            foreach (var chest in _data.treasureChests)
                SpawnTreasureChest(chest, container.transform);
        }

        private GameObject SpawnTreasureChest(TreasureChestData chest, Transform parent)
        {
            var go = new GameObject("宝箱_" + chest.id);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(chest.position.x, chest.position.y, chest.position.z);

            var tc = go.AddComponent<TreasureChest>();
            tc.openRange = 3f;

            // 挂载 loot table 引用
            var tag = go.AddComponent<MarkerTag>();
            tag.markerType = "treasureChest";
            tag.markerId = chest.id;
            tag.stringData = new Dictionary<string, string>
            {
                { "lootTable", chest.lootTable ?? "" },
                { "respawnTime", chest.respawnTime.ToString() }
            };

            return go;
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        //  条件注册（与 DiscoverySystem 集成）
        // ════════════════════════════════════════════════════════════════

        private void RegisterDiscoveryConditions(string discoveryId, DiscoveryConditionData[] conditions)
        {
            if (DiscoverySystem.Instance == null)
            {
                Debug.LogWarning("[SceneMarkerLoader] DiscoverySystem 未找到，发现条件将在下次场景加载时注册。");
                return;
            }

            // 通过反射或直接配置 DiscoverySystem 的 discoveryConfigs
            // 由于 DiscoverySystem 的配置列表在 Inspector 中序列化，
            // 运行时我们通过 EventBus 通知。
            var condList = new List<DiscoverySystem.DiscoveryConfigEntry>();
            foreach (var cond in conditions)
            {
                var entry = new DiscoverySystem.DiscoveryConfigEntry
                {
                    discoveryId = discoveryId,
                    type = DiscoveryType.Hidden // 默认，可以细化
                };

                // 解析条件
                if (cond.type == "CultivationAbove" && int.TryParse(cond.value, out int minCult))
                {
                    entry.conditions = new DiscoverySystem.DiscoveryCondition[]
                    {
                        new DiscoverySystem.DiscoveryCondition
                        {
                            type = DiscoverySystem.ConditionType.CultivationAbove,
                            value = cond.value
                        }
                    };
                }

                condList.Add(entry);
            }

            // 发布条件注册事件供 DiscoverySystem 消费
            EventBus.Publish("OnDiscoveryConditionsRegistered", new Dictionary<string, object>
            {
                { "discoveryId", discoveryId },
                { "conditions", condList }
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  清理
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 清理场景中已有的标记组件，避免重复生成。
        /// 通过搜索 SpiritVein / DungeonEntrance / HiddenDiscovery / FastTravel / TreasureChest 并移除。
        /// </summary>
        private void ClearExistingMarkers()
        {
            int removed = 0;

            var veins = FindObjectsOfType<SpiritVein>();
            foreach (var v in veins) { Destroy(v.gameObject); removed++; }

            var dungeons = FindObjectsOfType<DungeonEntrance>();
            foreach (var d in dungeons) { Destroy(d.gameObject); removed++; }

            var discoveries = FindObjectsOfType<HiddenDiscovery>();
            foreach (var d in discoveries) { Destroy(d.gameObject); removed++; }

            var travels = FindObjectsOfType<FastTravel>();
            foreach (var t in travels) { Destroy(t.gameObject); removed++; }

            var chests = FindObjectsOfType<TreasureChest>();
            foreach (var c in chests) { Destroy(c.gameObject); removed++; }

            if (_markersRoot != null)
                Destroy(_markersRoot);

            if (removed > 0)
                Debug.Log($"[SceneMarkerLoader] 清理了 {removed} 个已有场景标记。");
        }

        /// <summary>获取加载后的原始数据（只读）。</summary>
        public SceneMarkerData GetRawData() => _data;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  JSON 数据模型 —— 与 SceneMarkers.json 结构完全对应
    // ═════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class SceneMarkerData
    {
        public string version;
        public string generatedForScene;
        public string description;
        public SpiritVeinData[] spiritVeins = new SpiritVeinData[0];
        public DungeonEntranceData[] dungeonEntrances = new DungeonEntranceData[0];
        public HiddenDiscoveryData[] hiddenDiscoveries = new HiddenDiscoveryData[0];
        public FastTravelData[] fastTravelPoints = new FastTravelData[0];
        public TreasureChestData[] treasureChests = new TreasureChestData[0];
    }

    [System.Serializable]
    public class SpiritVeinData
    {
        public string id;
        public string veinName;
        public Vector3Data position;
        public string type;
        public float cultivationMultiplier = 1.5f;
        public float spiritRegenBonus = 3f;
        public float radius = 5f;
        public int dailyCost = 20;
        public string description;
    }

    [System.Serializable]
    public class DungeonEntranceData
    {
        public string id;
        public string dungeonName;
        public Vector3Data position;
        public string dungeonId;
        public string dungeonScene;
        public string recommendedRealm;
        public int recommendedLevel;
        public float enterRange = 3f;
        public string warningMessage;
        public string description;
    }

    [System.Serializable]
    public class HiddenDiscoveryData
    {
        public string id;
        public string discoveryName;
        public string discoveryText;
        public Vector3Data position;
        public string discoveryType;
        public float triggerRange = 6f;
        public DiscoveryConditionData[] conditions;
        public DiscoveryRewardData rewards;
        public string description;
    }

    [System.Serializable]
    public class DiscoveryConditionData
    {
        public string type;
        public string value;
    }

    [System.Serializable]
    public class DiscoveryRewardData
    {
        public string itemId;
        public string itemName;
        public int quantity = 1;
        public int cultivation;
    }

    [System.Serializable]
    public class FastTravelData
    {
        public string id;
        public string pointName;
        public string pointId;
        public Vector3Data position;
        public string unlockCondition = "discover";
        public string unlockRealm;
        public int unlockLevel;
        public string description;
    }

    [System.Serializable]
    public class TreasureChestData
    {
        public string id;
        public Vector3Data position;
        public string lootTable;
        public int respawnTime;
        public string description;
    }

    [System.Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data() { }
        public Vector3Data(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static implicit operator Vector3(Vector3Data v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator Vector3Data(Vector3 v) => new Vector3Data(v.x, v.y, v.z);
    }

    /// <summary>
    /// 附加到运行时生成的标记对象上，存储元数据供其他系统查询。
    /// </summary>
    public class MarkerTag : MonoBehaviour
    {
        public string markerType;    // "spiritVein" / "dungeon" / "discovery" / "fastTravel" / "treasureChest"
        public string markerId;
        public Dictionary<string, string> stringData = new Dictionary<string, string>();
    }
}
