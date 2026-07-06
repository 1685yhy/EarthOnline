using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline
{
    /// <summary>
    /// V2.1 名声系统 —— 玩家在灵气大陆的名声。善名/恶名影响NPC态度和商店价格。
    /// 社会运行逻辑：你做的事——世界会记住。
    /// </summary>
    public class ReputationSystem : MonoBehaviour
    {
        public static ReputationSystem Instance { get; private set; }

        public int fame;        // 善名（做好事）
        public int infamy;      // 恶名（做坏事）
        public string title => GetTitle();

        // 名声影响商店折扣：善名越高越便宜，恶名越高越贵
        public float ShopPriceModifier => 1f + infamy * 0.02f - fame * 0.01f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnEnemyKilled", d => AddFame(1, $"击杀{d["enemyName"]}"));
            EventBus.Subscribe("OnNPCInteract", d => AddFame(1, $"与{d["npcName"]}交谈"));
            EventBus.Subscribe("OnQuestCompleted", d => AddFame(10, $"完成任务:{d["title"]}"));
            EventBus.Subscribe("OnItemCrafted", d => AddFame(2, $"制作{d["itemName"]}"));
            EventBus.Subscribe("OnAchievementUnlocked", d => AddFame(5, $"成就:{d["title"]}"));
        }

        public void AddFame(int amount, string reason)
        {
            fame += amount;
            if (amount >= 5)
                Debug.Log($"[名声] 👍 +{amount}善名 ({reason}) 善:{fame} 恶:{infamy} 称号:{title}");
        }

        public void AddInfamy(int amount, string reason)
        {
            infamy += amount;
            Debug.Log($"[名声] 👎 +{amount}恶名 ({reason}) 善:{fame} 恶:{infamy} 称号:{title}");
        }

        string GetTitle()
        {
            int net = fame - infamy;
            return net switch
            {
                >= 200 => "万家生佛",
                >= 100 => "侠义之士",
                >= 50 => "正道修士",
                >= 0 => "无名之辈",
                >= -50 => "可疑人物",
                >= -100 => "凶名在外",
                >= -200 => "人人喊打",
                _ => "魔王降世"
            };
        }

        /// <summary>NPC是否愿意和你交易（恶名太高→拒绝）</summary>
        public bool CanTradeWithNPC()
        {
            if (infamy >= 100) { Debug.Log($"[名声] NPC拒绝和你交易——你恶名太盛({title})。"); return false; }
            return true;
        }
    }
}
