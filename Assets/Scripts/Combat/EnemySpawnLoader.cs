using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Combat
{
    // ─── JSON 配置数据类 ──────────────────────────────────────────────

    /// <summary>
    /// 与 EnemySpawns.json 中单个敌人条目对应的可序列化数据结构。
    /// </summary>
    [Serializable]
    public class EnemySpawnEntry
    {
        // 基本信息（必填）
        public string enemyId;
        public string displayName;
        public string realm;               // 境界等级（如"练气1层"、"筑基3层"）

        // 基础属性
        public float baseHP;
        public float baseAttack;
        public float baseDefense;

        // 刷新区域
        public string spawnZone;           // 区域名称（如"落霞村周边"）
        public SpawnPosition spawnPosition;

        // 刷新行为
        public float respawnTime = 30f;     // 刷新间隔（秒）
        public float patrolRadius = 6f;     // 巡逻半径
        public string dropTableRef;         // 掉落表引用
        public bool isAggressive = true;    // 是否主动攻击
    }

    /// <summary>JSON 坐标（对应 Vector3）</summary>
    [Serializable]
    public class SpawnPosition
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    /// <summary>JSON 根数据（包装数组以兼容 JsonUtility）</summary>
    [Serializable]
    public class EnemySpawnData
    {
        public string version;
        public string description;
        public EnemySpawnEntry[] spawns;
    }

    // ─── 运行时 Spawn 状态跟踪 ──────────────────────────────────────

    /// <summary>
    /// 运行时跟踪单个 Spawn 点的状态。
    /// </summary>
    public class SpawnInstance
    {
        public EnemySpawnEntry config;           // 原始配置
        public GameObject spawnedEnemy;           // 当前已生成的敌人实例（未死亡时）
        public float deathTime;                   // 死亡时间戳（Time.time）
        public bool IsAlive => spawnedEnemy != null && !spawnedEnemy.GetComponent<EnemyAI>().IsDead;
        public bool IsReadyToRespawn(float currentTime) => !IsAlive && (currentTime - deathTime) >= config.respawnTime;
    }

    // ─── 刷新选项 ────────────────────────────────────────────────────

    /// <summary>
    /// 控制 Spawn 时的覆写行为。
    /// </summary>
    public class SpawnOptions
    {
        public Vector3? overridePosition;         // 覆写生成位置
        public Transform parent;                  // 父级 Transform
        public Action<GameObject, EnemySpawnEntry> onSpawned; // 生成完成回调
    }

    // ─── 敌人 Spawn 加载器 ──────────────────────────────────────────

    /// <summary>
    /// 从 Resources/Data/EnemySpawns.json 加载敌人刷新配置，
    /// 提供按区域、按 ID 的查询与运行时生成功能。
    ///
    /// 自动装载机制：
    /// - [BeforeSceneLoad] 预加载全部配置到缓存
    /// - [AfterSceneLoad] 自动生成所有已配置的敌人
    /// </summary>
    public static class EnemySpawnLoader
    {
        private const string ConfigResourcePath = "Data/EnemySpawns";

        /// <summary>运行时所有 Spawn 配置缓存（enemyId → entry）</summary>
        private static Dictionary<string, EnemySpawnEntry> _cachedConfigs;

        /// <summary>运行时 Spawn 实例跟踪列表</summary>
        private static List<SpawnInstance> _activeSpawns;

        /// <summary>是否已启用自动刷新循环</summary>
        private static bool _respawnLoopActive;

        // ── 枚举帮助 ────────────────────────────────────────────────

        /// <summary>
        /// 境界层次枚举，用于排序和比较。
        /// </summary>
        public enum RealmTier
        {
            Unknown = 0,
            练气1层 = 11, 练气2层 = 12, 练气3层 = 13,
            练气4层 = 14, 练气5层 = 15, 练气6层 = 16,
            练气7层 = 17, 练气8层 = 18, 练气9层 = 19,
            筑基1层 = 21, 筑基2层 = 22, 筑基3层 = 23, 筑基4层 = 24,
            筑基5层 = 25, 筑基6层 = 26, 筑基7层 = 27, 筑基8层 = 28, 筑基9层 = 29,
            金丹1层 = 31, 金丹2层 = 32, 金丹3层 = 33,
            元婴1层 = 41
        }

        private static readonly Dictionary<string, RealmTier> RealmTierMap
            = new Dictionary<string, RealmTier>(StringComparer.OrdinalIgnoreCase);

        static EnemySpawnLoader()
        {
            // 初始化境界映射
            foreach (RealmTier val in Enum.GetValues(typeof(RealmTier)))
                RealmTierMap[val.ToString()] = val;

            _activeSpawns = new List<SpawnInstance>();
        }

        // ── 境界工具 ─────────────────────────────────────────────────

        /// <summary>
        /// 解析境界字符串为枚举值，用于比较强弱。
        /// </summary>
        public static RealmTier ParseRealm(string realmStr)
        {
            if (string.IsNullOrEmpty(realmStr))
                return RealmTier.Unknown;

            if (RealmTierMap.TryGetValue(realmStr, out RealmTier parsed))
                return parsed;

            Debug.LogWarning($"[EnemySpawnLoader] 无法解析境界: \"{realmStr}\"");
            return RealmTier.Unknown;
        }

        /// <summary>
        /// 获取两个境界之间的压制倍率（攻方 vs 守方）。
        /// 攻方高于守方每层 +0.15，攻方低于守方每层 -0.25。
        /// </summary>
        public static float GetRealmSuppression(string attackerRealm, string defenderRealm)
        {
            int diff = (int)ParseRealm(attackerRealm) - (int)ParseRealm(defenderRealm);
            if (diff > 0)
                return Mathf.Min(1.0f + diff * 0.15f, 10.0f);
            if (diff < 0)
                return Mathf.Max(1.0f - (-diff) * 0.25f, 0.1f);
            return 1.0f;
        }

        // ── 公开 API：配置加载 ──────────────────────────────────────

        /// <summary>
        /// 预加载全部敌人刷新配置到缓存。
        /// 返回缓存中的配置总数，失败时返回 0。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreloadCache()
        {
            EnemySpawnEntry[] all = LoadInternal();
            if (all == null || all.Length == 0)
            {
                Debug.LogError("[EnemySpawnLoader] 预加载失败：配置文件为空或格式错误");
                return;
            }

            _cachedConfigs = new Dictionary<string, EnemySpawnEntry>(all.Length);
            foreach (EnemySpawnEntry entry in all)
            {
                if (!string.IsNullOrEmpty(entry.enemyId) && !_cachedConfigs.ContainsKey(entry.enemyId))
                {
                    _cachedConfigs[entry.enemyId] = entry;
                }
                else if (!string.IsNullOrEmpty(entry.enemyId))
                {
                    Debug.LogWarning($"[EnemySpawnLoader] 重复 enemyId: {entry.enemyId}，已跳过");
                }
            }

            Debug.Log($"[EnemySpawnLoader] 预加载完成: {_cachedConfigs.Count} 个敌人配置已缓存");
        }

        /// <summary>
        /// 场景加载完成后自动生成场景中所有非 BOSS 敌人。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnAfterSceneLoad()
        {
            SpawnAllZones();
        }

        /// <summary>
        /// 加载全部敌人刷新配置。
        /// 结果会被缓存，重复调用不会重新加载。
        /// </summary>
        public static EnemySpawnEntry[] LoadAllSpawns()
        {
            if (_cachedConfigs != null && _cachedConfigs.Count > 0)
            {
                var all = new EnemySpawnEntry[_cachedConfigs.Count];
                _cachedConfigs.Values.CopyTo(all, 0);
                return all;
            }

            EnemySpawnEntry[] result = LoadInternal();
            if (result != null)
            {
                _cachedConfigs = new Dictionary<string, EnemySpawnEntry>(result.Length);
                foreach (EnemySpawnEntry entry in result)
                {
                    if (!string.IsNullOrEmpty(entry.enemyId))
                        _cachedConfigs[entry.enemyId] = entry;
                }
            }

            return result;
        }

        /// <summary>
        /// 按 enemyId 查找单个 Spawn 配置。
        /// </summary>
        public static EnemySpawnEntry GetSpawnById(string enemyId)
        {
            if (_cachedConfigs == null)
                LoadAllSpawns();

            if (_cachedConfigs != null && _cachedConfigs.TryGetValue(enemyId, out EnemySpawnEntry entry))
                return entry;

            Debug.LogWarning($"[EnemySpawnLoader] 未找到 enemyId: {enemyId}");
            return null;
        }

        /// <summary>
        /// 按区域名称获取该区域所有 Spawn 配置。
        /// </summary>
        public static EnemySpawnEntry[] GetSpawnsByZone(string zoneName)
        {
            EnemySpawnEntry[] all = LoadAllSpawns();
            if (all == null) return Array.Empty<EnemySpawnEntry>();

            var matches = new List<EnemySpawnEntry>();
            foreach (EnemySpawnEntry entry in all)
            {
                if (string.Equals(entry.spawnZone, zoneName, StringComparison.OrdinalIgnoreCase))
                    matches.Add(entry);
            }

            return matches.ToArray();
        }

        /// <summary>
        /// 获取所有区域名称列表。
        /// </summary>
        public static string[] GetAllZoneNames()
        {
            EnemySpawnEntry[] all = LoadAllSpawns();
            if (all == null) return Array.Empty<string>();

            var zones = new HashSet<string>();
            foreach (EnemySpawnEntry entry in all)
            {
                if (!string.IsNullOrEmpty(entry.spawnZone))
                    zones.Add(entry.spawnZone);
            }

            var result = new string[zones.Count];
            zones.CopyTo(result);
            return result;
        }

        /// <summary>
        /// 强制重新加载配置（清空缓存后重载）。
        /// </summary>
        public static void ReloadAll()
        {
            _cachedConfigs = null;
            Resources.UnloadUnusedAssets();
            LoadAllSpawns();
            Debug.Log("[EnemySpawnLoader] 配置已重载");
        }

        // ── 公开 API：运行时生成 ──────────────────────────────────────

        /// <summary>
        /// 在场景中生成一个敌人。
        /// </summary>
        /// <param name="entry">Spawn 配置条目</param>
        /// <param name="options">生成选项（位置覆写、父级、回调等）</param>
        /// <returns>生成的 GameObject，包含 EnemyAI 组件，失败时返回 null</returns>
        public static GameObject SpawnEnemy(EnemySpawnEntry entry, SpawnOptions options = null)
        {
            if (entry == null)
            {
                Debug.LogError("[EnemySpawnLoader] SpawnEnemy: entry 为 null");
                return null;
            }

            GameObject go = CreateEnemyInternal(entry, options);

            // 注册到运行时跟踪列表（仅当未提供覆写位置时——否则视为临时生成）
            if (options?.overridePosition == null)
                TrackSpawnInstance(go, entry);

            return go;
        }

        /// <summary>
        /// 按 enemyId 生成敌人。
        /// </summary>
        public static GameObject SpawnEnemyById(string enemyId, SpawnOptions options = null)
        {
            EnemySpawnEntry entry = GetSpawnById(enemyId);
            if (entry == null)
            {
                Debug.LogError($"[EnemySpawnLoader] 无法生成: enemyId \"{enemyId}\" 未找到");
                return null;
            }

            return SpawnEnemy(entry, options);
        }

        /// <summary>
        /// 批量生成指定区域的所有敌人。
        /// </summary>
        /// <returns>生成的 GameObject 列表</returns>
        public static List<GameObject> SpawnZone(string zoneName, Transform parent = null)
        {
            EnemySpawnEntry[] entries = GetSpawnsByZone(zoneName);
            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning($"[EnemySpawnLoader] 区域 \"{zoneName}\" 无配置或未找到");
                return new List<GameObject>(0);
            }

            var spawned = new List<GameObject>(entries.Length);
            foreach (EnemySpawnEntry entry in entries)
            {
                GameObject go = SpawnEnemy(entry, new SpawnOptions { parent = parent });
                if (go != null)
                    spawned.Add(go);
            }

            Debug.Log($"[EnemySpawnLoader] 区域 \"{zoneName}\" 生成完成: {spawned.Count}/{entries.Length} 个敌人");
            return spawned;
        }

        /// <summary>
        /// 生成所有已配置区域的敌人（对每个区域调用 SpawnZone）。
        /// </summary>
        public static void SpawnAllZones()
        {
            string[] zones = GetAllZoneNames();
            int total = 0;
            foreach (string zone in zones)
            {
                List<GameObject> spawned = SpawnZone(zone);
                total += spawned.Count;
            }
            Debug.Log($"[EnemySpawnLoader] 全区域生成完成: {total} 个敌人已生成");

            // 启动刷新循环
            if (!_respawnLoopActive && _activeSpawns.Count > 0)
            {
                _respawnLoopActive = true;
                GameObject updater = new GameObject("[EnemySpawnRespawnLoop]");
                updater.hideFlags = HideFlags.HideAndDontSave;
                updater.AddComponent<EnemyRespawnUpdater>();
                Debug.Log("[EnemySpawnLoader] 刷新循环已启动");
            }
        }

        /// <summary>
        /// 根据玩家当前境界推荐合适的刷怪区域。
        /// </summary>
        public static string[] GetRecommendedZonesForRealm(string playerRealm)
        {
            RealmTier playerTier = ParseRealm(playerRealm);
            if (playerTier == RealmTier.Unknown)
                return GetAllZoneNames();

            string[] all = GetAllZoneNames();
            var recommended = new List<string>();

            foreach (string zone in all)
            {
                EnemySpawnEntry[] zoneEntries = GetSpawnsByZone(zone);
                if (zoneEntries == null || zoneEntries.Length == 0)
                    continue;

                // 取区域中最高境界的敌人作为区域等级
                RealmTier highest = RealmTier.Unknown;
                foreach (EnemySpawnEntry e in zoneEntries)
                {
                    RealmTier t = ParseRealm(e.realm);
                    if (t > highest) highest = t;
                }

                // 推荐范围：玩家境界 ± 2 层
                int diff = (int)playerTier - (int)highest;
                if (Mathf.Abs(diff) <= 2)
                    recommended.Add(zone);
            }

            return recommended.ToArray();
        }

        // ── 内部实现 ─────────────────────────────────────────────────

        /// <summary>
        /// 从 Resources 加载 JSON 并解析。
        /// </summary>
        private static EnemySpawnEntry[] LoadInternal()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[EnemySpawnLoader] 配置文件未找到: Resources/{ConfigResourcePath}.json");
                return null;
            }

            EnemySpawnData configData;
            try
            {
                configData = JsonUtility.FromJson<EnemySpawnData>(jsonAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnemySpawnLoader] JSON 解析失败: {ex.Message}");
                return null;
            }

            if (configData == null || configData.spawns == null || configData.spawns.Length == 0)
            {
                Debug.LogError("[EnemySpawnLoader] 配置数据为空，请检查 EnemySpawns.json");
                return null;
            }

            foreach (EnemySpawnEntry entry in configData.spawns)
            {
                // 验证必填字段
                if (string.IsNullOrEmpty(entry.enemyId))
                    Debug.LogWarning("[EnemySpawnLoader] 发现缺少 enemyId 的条目，已跳过");
                if (entry.spawnPosition == null)
                {
                    Debug.LogWarning($"[EnemySpawnLoader] 条目 {entry.enemyId ?? "未知"} 缺少 spawnPosition，已填充默认值 (0,0,0)");
                    entry.spawnPosition = new SpawnPosition();
                }
            }

            return configData.spawns;
        }

        /// <summary>
        /// 将配置值写入 EnemyAI 组件（内部方法，无副作用）。
        /// </summary>
        private static void ConfigureEnemyAI(EnemyAI ai, EnemySpawnEntry entry)
        {
            ai.enemyId = entry.enemyId;
            ai.enemyName = entry.displayName;
            ai.maxHP = Mathf.RoundToInt(entry.baseHP);
            ai.attackPower = Mathf.RoundToInt(entry.baseAttack);
            ai.patrolRadius = entry.patrolRadius;

            // 境界相关的属性缩放（更高境界 = 更高感知/速度/攻击频率）
            RealmTier tier = ParseRealm(entry.realm);
            float tierFactor = 1.0f + (int)tier * 0.01f;

            ai.moveSpeed = Mathf.Lerp(1.5f, 4.0f, tierFactor * 0.1f);
            ai.chaseSpeed = ai.moveSpeed * 1.8f;
            ai.detectRange = Mathf.Lerp(6f, 30f, tierFactor * 0.1f);
            ai.attackRange = Mathf.Lerp(1.2f, 3.0f, tierFactor * 0.05f);
            ai.attackCooldown = Mathf.Lerp(2.0f, 0.8f, tierFactor * 0.05f);

            // 非主动型敌人降低感知
            if (!entry.isAggressive)
            {
                ai.detectRange *= 0.5f;
            }

            // 掉落配置（通过 dropTableRef 简化指引——实际掉落逻辑由 DropTableManager 处理）
            ai.dropItemId = entry.dropTableRef ?? "item_default";
            ai.dropItemName = entry.displayName + "掉落";
            ai.dropQuantity = 1;
            ai.dropChance = 0.5f;
        }

        /// <summary>
        /// 内部创建敌人 GameObject 并配置 EnemyAI（不操作跟踪列表）。
        /// </summary>
        private static GameObject CreateEnemyInternal(EnemySpawnEntry entry, SpawnOptions options)
        {
            Vector3 position = options?.overridePosition ?? entry.spawnPosition.ToVector3();
            Transform parent = options?.parent;

            GameObject go = new GameObject(entry.enemyId);
            if (parent != null)
                go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = "Enemy";

            EnemyAI ai = go.AddComponent<EnemyAI>();
            ConfigureEnemyAI(ai, entry);

            options?.onSpawned?.Invoke(go, entry);
            return go;
        }

        /// <summary>
        /// 将生成的敌人注册到运行时跟踪列表，用于刷新管理。
        /// </summary>
        private static void TrackSpawnInstance(GameObject go, EnemySpawnEntry entry)
        {
            // 移除旧的同名跟踪（安全清理）
            _activeSpawns.RemoveAll(s => s.config.enemyId == entry.enemyId);

            var instance = new SpawnInstance
            {
                config = entry,
                spawnedEnemy = go
            };

            _activeSpawns.Add(instance);
        }

        // ── 内部：刷新循环 ──────────────────────────────────────────

        /// <summary>
        /// 隐藏的 MonoBehaviour，驱动刷新循环。
        /// 由 SpawnAllZones 自动创建，不可见。
        /// </summary>
        private class EnemyRespawnUpdater : MonoBehaviour
        {
            void Update()
            {
                float now = Time.time;

                // 反向迭代：新的 SpawnInstance 总是追加到尾部，不影响未处理元素
                for (int i = _activeSpawns.Count - 1; i >= 0; i--)
                {
                    SpawnInstance si = _activeSpawns[i];

                    // 敌人刚死亡——记录时间
                    if (si.spawnedEnemy != null)
                    {
                        EnemyAI ai = si.spawnedEnemy.GetComponent<EnemyAI>();
                        if (ai != null && ai.IsDead && si.deathTime == 0f)
                        {
                            si.deathTime = now;
                            si.spawnedEnemy = null;
                        }
                        else if (ai == null)
                        {
                            // 敌人被意外销毁
                            si.deathTime = now;
                            si.spawnedEnemy = null;
                        }
                    }

                    // 需要刷新 —— 直接内部创建，避免 TrackSpawnInstance 修改列表
                    if (si.spawnedEnemy == null && si.deathTime > 0f && si.IsReadyToRespawn(now))
                    {
                        GameObject go = CreateEnemyInternal(si.config, null);
                        si.spawnedEnemy = go;
                        si.deathTime = 0f;
                    }
                }
            }
        }
    }
}
