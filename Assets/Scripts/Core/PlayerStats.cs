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
        public int cultivation = 0;       // 修为
        public long spiritStones = 0;         // 灵石
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
                long lostGold = spiritStones / 5; // 失去20%灵石
                spiritStones -= lostGold;
                Debug.Log($"[Player] 💀 你倒下了...失去了{lostGold}灵石(20%)。但故事不会就此结束。");
                EventBus.Publish("OnPlayerDeath", new Dictionary<string, object> {{"lostGold", lostGold}});
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

        void UpdateHUD()
        {
            if (TimeManager.Instance == null) return;
            var timeStr = TimeManager.Instance.TimeString;
            var dayStr = $"第{TimeManager.Instance.GameDay}天";
            var weather = WeatherSystem.Instance?.GetWeatherEmoji() ?? "";
            var status = $"🏷️ Lv.{playerLevel} | ❤️ {currentHP}/{maxHP} | 💰 {spiritStones} | ⭐ {cultivation} | {weather} {timeStr} {dayStr}";
            EventBus.Publish("OnStatusUpdate", new Dictionary<string, object> { {"status", status} });
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnSignInComplete", OnSignInReward);
            EventBus.Unsubscribe("OnCultivationBoost", OnCultivationGain);
        }
    }
}
