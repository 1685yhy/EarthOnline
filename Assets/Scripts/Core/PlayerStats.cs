using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 玩家属性 —— HP、修为、灵石、等级。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        [Header("基础属性")]
        public int maxHP = 100;
        public int currentHP = 100;
        public int cultivation = 0;          // 修为
        public long spiritStones = 0;        // 灵石(货币，不可直接修炼)
        public int spiritEssence = 0;        // 灵韵(修炼资源，从灵石转化)
        public int playerLevel = 1;
        public int expToNextLevel = 100;
        public int currentExp = 0;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnSignInComplete", OnSignInReward);
            EventBus.Subscribe("OnCultivationBoost", OnCultivationGain);
            UpdateHUD();
        }

        void OnSignInReward(Dictionary<string, object> data)
        {
            int reward = data.ContainsKey("reward") ? (int)data["reward"] : 0;
            AddSpiritStone(reward);
        }

        void OnCultivationGain(Dictionary<string, object> data)
        {
            int amount = data.ContainsKey("amount") ? (int)data["amount"] : 0;
            AddCultivation(amount);
        }

        public void AddSpiritStone(long amount)
        {
            spiritStones += amount;
            Debug.Log($"[Player] +{amount}灵石 (总计:{spiritStones})");
            UpdateHUD();
        }

        /// <summary>将灵石转化为灵韵用于修炼。兑换率：10灵石=1灵韵</summary>
        public void ConvertToEssence(int spiritStoneCost)
        {
            if (spiritStones < spiritStoneCost)
            {
                Debug.Log($"[Player] 灵石不足。需要{spiritStoneCost}灵石。");
                return;
            }
            spiritStones -= spiritStoneCost;
            int essenceGain = spiritStoneCost / 10;
            spiritEssence += essenceGain;
            Debug.Log($"[Player] 💱 转化{spiritStoneCost}灵石→{essenceGain}灵韵 (灵韵:{spiritEssence})");
            UpdateHUD();
        }

        public void AddCultivation(int amount)
        {
            cultivation += amount;
            currentExp += amount;
            while (currentExp >= expToNextLevel)
            {
                currentExp -= expToNextLevel;
                playerLevel++;
                expToNextLevel = Mathf.FloorToInt(expToNextLevel * 1.5f);
                Debug.Log($"[Player] 升级! Lv.{playerLevel}! (HP+20)");
                maxHP += 20; currentHP = maxHP;

                // 每5级额外奖励
                string bonus = "";
                if (playerLevel % 5 == 0)
                {
                    int goldReward = playerLevel * 50;
                    spiritStones += goldReward;
                    bonus = $" +{goldReward}💰奖励";
                    Debug.Log($"[Player] 🎉 Lv.{playerLevel}成就奖励: +{goldReward}灵石！");
                }

                currentHP = maxHP;
                Combat.FloatingDamage.Spawn(transform.position,
                    $"Lv.{playerLevel}!", new Color(1f, 0.85f, 0f), 2f);
                EventBus.Publish("OnPlayerLevelUp", new Dictionary<string, object> {
                    {"level", playerLevel}, {"maxHP", maxHP}, {"bonus", bonus}
                });
            }
            Debug.Log($"[Player] +{amount}修为 (总计:{cultivation}, Lv.{playerLevel})");
            UpdateHUD();
        }

        public void TakeDamage(int damage)
        {
            currentHP -= damage;
            Debug.Log($"[Player] 受到{damage}伤害! HP:{currentHP}/{maxHP}");
            if (currentHP <= 0)
            {
                currentHP = 0;

                // 新手经济保护：萌新期死亡不损失灵石（经济平衡文档V1 6.2.2）
                if (NewbieProtection.ShouldLoseSpiritStonesOnDeath)
                {
                    long lostGold = spiritStones / 5; // 正常失去20%灵石
                    spiritStones -= lostGold;
                    Debug.Log($"[Player] 💀 你倒下了...失去了{lostGold}灵石(20%)。但故事不会就此结束。");
                    EventBus.Publish("OnPlayerDeath", new Dictionary<string, object> {{"lostGold", lostGold}});
                }
                else
                {
                    Debug.Log($"[Player] 💀 你倒下了...（新手保护：灵石未损失）");
                    EventBus.Publish("OnPlayerDeath", new Dictionary<string, object> {{"lostGold", 0L}});
                }

                currentHP = maxHP / 2;
            }
            UpdateHUD();
        }

        public void Heal(int amount)
        {
            currentHP = Mathf.Min(currentHP + amount, maxHP);
            Debug.Log($"[Player] 恢复{amount}HP (HP:{currentHP}/{maxHP})");
            UpdateHUD();
        }

        public void UpdateHUD()
        {
            if (TimeManager.Instance == null) return;
            var timeStr = TimeManager.Instance.TimeString;
            var dayStr = $"第{TimeManager.Instance.GameDay}天";
            var weather = WeatherSystem.Instance?.GetWeatherEmoji() ?? "";

            // 世界感知：显示当前世界的货币和境界
            string currencyDisplay = "灵石"; // 默认修真世界
            string realmDisplay = "";
            // TODO: 从WorldConfig读取，目前硬编码灵气大陆
            if (cultivation >= 1500) realmDisplay = "化神期";
            else if (cultivation >= 1000) realmDisplay = "元婴期";
            else if (cultivation >= 600) realmDisplay = "金丹期";
            else if (cultivation >= 300) realmDisplay = "筑基期";
            else if (cultivation >= 100) realmDisplay = "练气期";

            var status = $"🏷️ Lv.{playerLevel} {realmDisplay} | ❤️ {currentHP}/{maxHP} | 💎{spiritStones}灵石 | ✨{spiritEssence}灵韵 | ⭐ {cultivation} | {weather} {timeStr} {dayStr}";
            EventBus.Publish("OnStatusUpdate", new Dictionary<string, object> { {"status", status} });
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnSignInComplete", OnSignInReward);
            EventBus.Unsubscribe("OnCultivationBoost", OnCultivationGain);
        }
    }
}
