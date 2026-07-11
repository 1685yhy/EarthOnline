using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Framework
{
    [System.Serializable]
    public class Achievement
    {
        public string id, title, description, category;
        public bool unlocked;
        public int reward;
    }

    /// <summary>
    /// 成就系统 —— 里程碑式奖励，追踪玩家进度。
    /// 分类：战斗(11) / 修炼(9) / 探索(7) / 社交(8) = 35项
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }
        private Dictionary<string, Achievement> _achievements = new();

        // ================================================================
        //  追踪计数器
        // ================================================================
        private int _killCount;
        private int _comboCount;
        private int _bossKillCount;
        private int _dodgeCountInBattle;
        private bool _noHitAttempt;
        private int _chestOpenedCount;
        private int _secretFoundCount;
        private int _travelPointCount;
        private int _echoCount;
        private int _skillCount;
        private int _npcFavorMax;
        private int _guildReputation;

        // ================================================================
        //  事件句柄引用（用于正确取消订阅）
        // ================================================================
        private System.Action<Dictionary<string, object>> _onEnemyKilled;
        private System.Action<Dictionary<string, object>> _onComboChange;
        private System.Action<Dictionary<string, object>> _onBossKilled;
        private System.Action<Dictionary<string, object>> _onPlayerDodge;
        private System.Action<Dictionary<string, object>> _onBossBattleStart;
        private System.Action<Dictionary<string, object>> _onPlayerHit;
        private System.Action<Dictionary<string, object>> _onBossBattleEnd;
        private System.Action<Dictionary<string, object>> _onPlayerDeath;
        private System.Action<Dictionary<string, object>> _onPlayerLevelUp;
        private System.Action<Dictionary<string, object>> _onBreakthrough;
        private System.Action<Dictionary<string, object>> _onItemCrafted;
        private System.Action<Dictionary<string, object>> _onSkillLearned;
        private System.Action<Dictionary<string, object>> _onMaxLevelReached;
        private System.Action<Dictionary<string, object>> _onEchoDiscovered;
        private System.Action<Dictionary<string, object>> _onChestOpened;
        private System.Action<Dictionary<string, object>> _onSecretFound;
        private System.Action<Dictionary<string, object>> _onTravelPointReached;
        private System.Action<Dictionary<string, object>> _onItemPurchased;
        private System.Action<Dictionary<string, object>> _onQuestCompleted;
        private System.Action<Dictionary<string, object>> _onSignInComplete;
        private System.Action<Dictionary<string, object>> _onNpcFavorChange;
        private System.Action<Dictionary<string, object>> _onDaoPartner;
        private System.Action<Dictionary<string, object>> _onGuildJoin;
        private System.Action<Dictionary<string, object>> _onGuildReputationChange;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RegisterAll();

            // ============================================================
            //  战斗事件订阅
            // ============================================================
            _onEnemyKilled = d =>
            {
                _killCount++;
                CheckCombatKillAchievements();
            };
            EventBus.Subscribe("OnEnemyKilled", _onEnemyKilled);

            _onComboChange = d =>
            {
                if (d.TryGetValue("combo", out var v))
                    _comboCount = System.Convert.ToInt32(v);
                CheckComboAchievements();
            };
            EventBus.Subscribe("OnComboChange", _onComboChange);

            _onBossKilled = d =>
            {
                _bossKillCount++;
                CheckBossAchievements();
            };
            EventBus.Subscribe("OnBossKilled", _onBossKilled);

            _onPlayerDodge = d => { _dodgeCountInBattle++; };
            EventBus.Subscribe("OnPlayerDodge", _onPlayerDodge);

            _onBossBattleStart = d => { _noHitAttempt = true; _dodgeCountInBattle = 0; };
            EventBus.Subscribe("OnBossBattleStart", _onBossBattleStart);

            _onPlayerHit = d => { _noHitAttempt = false; };
            EventBus.Subscribe("OnPlayerHit", _onPlayerHit);

            _onBossBattleEnd = d =>
            {
                if (_noHitAttempt) Check("no_hit");
                if (_dodgeCountInBattle >= 10) Check("perfect_dodge");
            };
            EventBus.Subscribe("OnBossBattleEnd", _onBossBattleEnd);

            _onPlayerDeath = d =>
            {
                Check("death_1");
                _comboCount = 0;
            };
            EventBus.Subscribe("OnPlayerDeath", _onPlayerDeath);

            // ============================================================
            //  修炼事件订阅
            // ============================================================
            _onPlayerLevelUp = d =>
            {
                int level = 0;
                if (d.TryGetValue("level", out var v)) level = System.Convert.ToInt32(v);
                if (level <= 0 && PlayerStats.Instance != null)
                    level = PlayerStats.Instance.playerLevel;
                CheckLevelAchievements(level);
            };
            EventBus.Subscribe("OnPlayerLevelUp", _onPlayerLevelUp);

            _onBreakthrough = d =>
            {
                string stage = "";
                if (d.TryGetValue("stage", out var v)) stage = v.ToString();
                CheckBreakthroughAchievements(stage);
            };
            EventBus.Subscribe("OnBreakthrough", _onBreakthrough);

            _onItemCrafted = d => Check("craft_1");
            EventBus.Subscribe("OnItemCrafted", _onItemCrafted);

            _onSkillLearned = d =>
            {
                _skillCount++;
                if (_skillCount >= 1)  Check("skill_1");
                if (_skillCount >= 10) Check("skill_10");
            };
            EventBus.Subscribe("OnSkillLearned", _onSkillLearned);

            _onMaxLevelReached = d => Check("max_level");
            EventBus.Subscribe("OnMaxLevelReached", _onMaxLevelReached);

            // ============================================================
            //  探索事件订阅
            // ============================================================
            _onEchoDiscovered = d =>
            {
                _echoCount++;
                Check("echo_discover");
                int totalEchoes = PlayerPrefs.GetInt("total_echo_count", 5);
                if (_echoCount >= totalEchoes) Check("echo_all");
            };
            EventBus.Subscribe("OnEchoDiscovered", _onEchoDiscovered);

            _onChestOpened = d =>
            {
                _chestOpenedCount++;
                if (_chestOpenedCount >= 1)  Check("chest_1");
                if (_chestOpenedCount >= 50) Check("chest_50");
            };
            EventBus.Subscribe("OnChestOpened", _onChestOpened);

            _onSecretFound = d =>
            {
                _secretFoundCount++;
                Check("secret_1");
            };
            EventBus.Subscribe("OnSecretFound", _onSecretFound);

            _onTravelPointReached = d =>
            {
                _travelPointCount++;
                Check("travel_1");
            };
            EventBus.Subscribe("OnTravelPointReached", _onTravelPointReached);

            _onItemPurchased = d => Check("shop_1");
            EventBus.Subscribe("OnItemPurchased", _onItemPurchased);

            // ============================================================
            //  社交事件订阅
            // ============================================================
            _onQuestCompleted = d =>
            {
                Check("quest_1");
                Check("quest_all");
            };
            EventBus.Subscribe("OnQuestCompleted", _onQuestCompleted);

            _onSignInComplete = d => Check("sign_7");
            EventBus.Subscribe("OnSignInComplete", _onSignInComplete);

            _onNpcFavorChange = d =>
            {
                if (d.TryGetValue("favor", out var v))
                {
                    _npcFavorMax = System.Convert.ToInt32(v);
                    if (_npcFavorMax >= 1)   Check("npc_favor_1");
                    if (_npcFavorMax >= 100) Check("npc_favor_max");
                }
            };
            EventBus.Subscribe("OnNPCFavorChange", _onNpcFavorChange);

            _onDaoPartner = d => Check("dao_partner");
            EventBus.Subscribe("OnDaoPartner", _onDaoPartner);

            _onGuildJoin = d => Check("guild_join");
            EventBus.Subscribe("OnGuildJoin", _onGuildJoin);

            _onGuildReputationChange = d =>
            {
                if (d.TryGetValue("reputation", out var v))
                {
                    _guildReputation = System.Convert.ToInt32(v);
                    if (_guildReputation >= 1000) Check("guild_rep_high");
                }
            };
            EventBus.Subscribe("OnGuildReputationChange", _onGuildReputationChange);
        }

        // ================================================================
        //  成就注册
        // ================================================================
        void RegisterAll()
        {
            // ===== 战斗 (11项) =====
            Add("first_blood",   "初战告捷",   "首次击败敌人",           30,  "战斗");
            Add("combo_10",      "十连击破",   "达成10连击",             80,  "战斗");
            Add("combo_30",      "无双乱舞",   "达成30连击",             200, "战斗");
            Add("kill_50",       "百人斩",     "击败50名敌人",           150, "战斗");
            Add("kill_500",      "千人斩",     "击败500名敌人",          500, "战斗");
            Add("kill_1000",     "万夫莫敌",   "击败1000名敌人",        1000, "战斗");
            Add("boss_kill",     "弑神者",     "击败虚空行者Boss",      1000, "战斗");
            Add("boss_kill_5",   "Boss猎手",   "击败5个Boss",            300, "战斗");
            Add("no_hit",        "无伤通关",   "不受伤击败任意Boss",     500, "战斗");
            Add("perfect_dodge", "身法如神",   "单场战斗完美闪避10次",   150, "战斗");
            Add("death_1",       "涅槃重生",   "首次死亡",                10,  "战斗");

            // ===== 修炼 (9项) =====
            Add("lv5",       "初窥门径",   "达到Lv.5",       100,  "修炼");
            Add("lv10",      "小有所成",   "达到Lv.10",      300,  "修炼");
            Add("lv20",      "金丹大道",   "达到Lv.20",      500,  "修炼");
            Add("lv30",      "元婴出世",   "达到Lv.30",      800,  "修炼");
            Add("lv50",      "化神之境",   "达到Lv.50",     1500,  "修炼");
            Add("max_level", "极限突破",   "达到13层极限",  2000,  "修炼");
            Add("craft_1",   "炼金术士",   "首次制作物品",    50,   "修炼");
            Add("skill_1",   "初学乍练",   "学习第一个技能",  80,   "修炼");
            Add("skill_10",  "博学多才",   "学习10个技能",   300,  "修炼");

            // ===== 探索 (7项) =====
            Add("echo_discover", "回响发现",     "首次发现回响",       100, "探索");
            Add("echo_all",      "回响收藏家",   "发现所有回响",       500, "探索");
            Add("chest_1",       "开箱达人",     "首次开启宝箱",        30, "探索");
            Add("chest_50",      "宝藏猎人",     "开启50个宝箱",       200, "探索");
            Add("secret_1",      "探索者",       "发现第一个隐藏地点", 100, "探索");
            Add("travel_1",      "行者无疆",     "首次到达旅行点",      50, "探索");
            Add("collector",     "收藏家",       "收集所有类型物品",   150, "探索");

            // ===== 社交 (8项) =====
            Add("quest_1",       "助人为乐",   "完成第一个任务",     50,  "社交");
            Add("quest_all",     "冒险家",     "完成所有任务",      500,  "社交");
            Add("sign_7",        "坚持不懈",   "连续签到7天",       200,  "社交");
            Add("shop_1",        "购物狂",     "首次购买物品",       20,  "社交");
            Add("npc_favor_1",   "初识",       "首次提升NPC好感度",  80,  "社交");
            Add("npc_favor_max", "莫逆之交",   "NPC好感度达到MAX",  500,  "社交");
            Add("dao_partner",   "道侣",       "结成道侣",          500,  "社交");
            Add("guild_join",    "入帮",       "加入帮派",          100,  "社交");
            Add("guild_rep_high","帮派中坚",   "帮派声望达到1000",  300,  "社交");
            Add("rich",          "富甲一方",   "拥有1000灵石",      100,  "社交");
        }

        void Add(string id, string title, string desc, int reward, string category)
        {
            _achievements[id] = new Achievement
            {
                id = id,
                title = title,
                description = desc,
                reward = reward,
                category = category
            };
        }

        // ================================================================
        //  条件检查辅助方法
        // ================================================================
        void CheckCombatKillAchievements()
        {
            if (_killCount >= 1)    Check("first_blood");
            if (_killCount >= 50)   Check("kill_50");
            if (_killCount >= 500)  Check("kill_500");
            if (_killCount >= 1000) Check("kill_1000");
        }

        void CheckComboAchievements()
        {
            if (_comboCount >= 10) Check("combo_10");
            if (_comboCount >= 30) Check("combo_30");
        }

        void CheckBossAchievements()
        {
            Check("boss_kill");
            if (_bossKillCount >= 5) Check("boss_kill_5");
        }

        void CheckLevelAchievements(int level)
        {
            if (level >= 5)  Check("lv5");
            if (level >= 10) Check("lv10");
            if (level >= 20) Check("lv20");
            if (level >= 30) Check("lv30");
            if (level >= 50) Check("lv50");
        }

        void CheckBreakthroughAchievements(string stage)
        {
            // stage 取值: "refining"=练气, "foundation"=筑基, "core"=金丹, "infant"=元婴
            if (stage == "refining")   Check("breakthrough_1");
            if (stage == "foundation") Check("breakthrough_2");
        }

        // ================================================================
        //  核心检查 & 解锁
        // ================================================================
        void Check(string id)
        {
            if (!_achievements.ContainsKey(id) || _achievements[id].unlocked) return;
            Unlock(id);
        }

        void Unlock(string id)
        {
            var a = _achievements[id];
            a.unlocked = true;
            var stats = PlayerStats.Instance;
            if (stats != null) stats.AddSpiritStone(a.reward);

            Debug.Log($"[Achievement] 成就解锁: [{a.title}] {a.description} +{a.reward}灵石");
            EarthOnline.Combat.FloatingDamage.Spawn(
                Camera.main?.transform.position ?? Vector3.zero,
                $"<b>成就</b> {a.title}", new Color(1f, 0.85f, 0.1f), 3f);

            EventBus.Publish("OnAchievementUnlocked", new Dictionary<string, object>
            {
                {"id", a.id},
                {"title", a.title},
                {"category", a.category},
                {"reward", a.reward}
            });
        }

        // ================================================================
        //  公开接口
        // ================================================================
        public List<Achievement> GetAll() => new List<Achievement>(_achievements.Values);

        public List<Achievement> GetByCategory(string category)
        {
            var result = new List<Achievement>();
            foreach (var kv in _achievements)
            {
                if (kv.Value.category == category)
                    result.Add(kv.Value);
            }
            return result;
        }

        public int GetUnlockedCount()
        {
            int count = 0;
            foreach (var kv in _achievements)
            {
                if (kv.Value.unlocked) count++;
            }
            return count;
        }

        public int GetTotalCount() => _achievements.Count;

        // ================================================================
        //  清理
        // ================================================================
        void OnDestroy()
        {
            EventBus.Unsubscribe("OnEnemyKilled",           _onEnemyKilled);
            EventBus.Unsubscribe("OnComboChange",           _onComboChange);
            EventBus.Unsubscribe("OnBossKilled",            _onBossKilled);
            EventBus.Unsubscribe("OnPlayerDodge",           _onPlayerDodge);
            EventBus.Unsubscribe("OnBossBattleStart",       _onBossBattleStart);
            EventBus.Unsubscribe("OnPlayerHit",             _onPlayerHit);
            EventBus.Unsubscribe("OnBossBattleEnd",         _onBossBattleEnd);
            EventBus.Unsubscribe("OnPlayerDeath",           _onPlayerDeath);
            EventBus.Unsubscribe("OnPlayerLevelUp",         _onPlayerLevelUp);
            EventBus.Unsubscribe("OnBreakthrough",          _onBreakthrough);
            EventBus.Unsubscribe("OnItemCrafted",           _onItemCrafted);
            EventBus.Unsubscribe("OnSkillLearned",          _onSkillLearned);
            EventBus.Unsubscribe("OnMaxLevelReached",       _onMaxLevelReached);
            EventBus.Unsubscribe("OnEchoDiscovered",        _onEchoDiscovered);
            EventBus.Unsubscribe("OnChestOpened",           _onChestOpened);
            EventBus.Unsubscribe("OnSecretFound",           _onSecretFound);
            EventBus.Unsubscribe("OnTravelPointReached",    _onTravelPointReached);
            EventBus.Unsubscribe("OnItemPurchased",         _onItemPurchased);
            EventBus.Unsubscribe("OnQuestCompleted",        _onQuestCompleted);
            EventBus.Unsubscribe("OnSignInComplete",        _onSignInComplete);
            EventBus.Unsubscribe("OnNPCFavorChange",        _onNpcFavorChange);
            EventBus.Unsubscribe("OnDaoPartner",            _onDaoPartner);
            EventBus.Unsubscribe("OnGuildJoin",             _onGuildJoin);
            EventBus.Unsubscribe("OnGuildReputationChange", _onGuildReputationChange);
        }
    }
}
