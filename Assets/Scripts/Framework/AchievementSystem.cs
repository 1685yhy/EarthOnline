using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    [System.Serializable]
    public class Achievement
    {
        public string id, title, description;
        public bool unlocked;
        public int reward;
    }

    /// <summary>
    /// 成就系统 —— 里程碑式奖励，追踪玩家进度。
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }
        private Dictionary<string, Achievement> _achievements = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterAll();
            EventBus.Subscribe("OnEnemyKilled", d => Check("first_blood"));
            EventBus.Subscribe("OnPlayerLevelUp", d => { Check("lv5"); Check("lv10"); });
            EventBus.Subscribe("OnItemCrafted", d => Check("craft_1"));
            EventBus.Subscribe("OnQuestCompleted", d => { Check("quest_1"); Check("quest_all"); });
            EventBus.Subscribe("OnSignInComplete", d => { Check("sign_7"); });
            EventBus.Subscribe("OnItemPurchased", d => Check("shop_1"));
            EventBus.Subscribe("OnPlayerDeath", d => Check("death_1"));
        }

        void RegisterAll()
        {
            Add("first_blood", "初战告捷", "首次击败敌人", 30);
            Add("lv5", "初窥门径", "达到Lv.5", 100);
            Add("lv10", "小有所成", "达到Lv.10", 300);
            Add("craft_1", "炼金术士", "首次制作物品", 50);
            Add("quest_1", "助人为乐", "完成第一个任务", 50);
            Add("quest_all", "冒险家", "完成所有任务", 500);
            Add("sign_7", "坚持不懈", "连续签到7天", 200);
            Add("shop_1", "购物狂", "首次购买物品", 20);
            Add("death_1", "涅槃重生", "首次死亡", 10);
            Add("boss_kill", "弑神者", "击败虚空行者Boss", 1000);
            Add("collector", "收藏家", "收集所有类型物品", 150);
            Add("rich", "富甲一方", "拥有1000灵石", 100);
        }

        void Add(string id, string title, string desc, int reward)
        {
            _achievements[id] = new Achievement { id = id, title = title, description = desc, reward = reward };
        }

        void Check(string id)
        {
            if (!_achievements.ContainsKey(id) || _achievements[id].unlocked) return;

            bool shouldUnlock = id switch
            {
                "first_blood" => true,
                "lv5" => PlayerStats.Instance?.playerLevel >= 5,
                "lv10" => PlayerStats.Instance?.playerLevel >= 10,
                "craft_1" => true,
                "quest_1" => true,
                "quest_all" => Object.FindObjectOfType<QuestManager>()?.GetActiveQuests().Count == 0
                    && PlayerPrefs.GetInt("quest_completed", 0) >= 4,
                "sign_7" => true,
                "shop_1" => true,
                "death_1" => true,
                "boss_kill" => true,
                "collector" => InventoryManager.Instance?.Count >= 10,
                "rich" => PlayerStats.Instance?.spiritStones >= 1000,
                _ => false
            };

            if (shouldUnlock) Unlock(id);
        }

        void Unlock(string id)
        {
            var a = _achievements[id];
            a.unlocked = true;
            var stats = PlayerStats.Instance;
            if (stats != null) stats.AddSpiritStone(a.reward);

            Debug.Log($"🏆 成就解锁: [{a.title}] {a.description} +{a.reward}💰");
            EarthOnline.Combat.FloatingDamage.Spawn(
                Camera.main?.transform.position ?? Vector3.zero,
                $"🏆 {a.title}!", new Color(1f, 0.85f, 0.1f), 3f);

            EventBus.Publish("OnAchievementUnlocked", new Dictionary<string, object> {
                {"title", a.title}, {"reward", a.reward}
            });
        }

        public List<Achievement> GetAll() => new(_achievements.Values);

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnEnemyKilled", d => Check("first_blood"));
        }
    }
}
