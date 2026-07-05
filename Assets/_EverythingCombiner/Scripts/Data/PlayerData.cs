using System;
using System.Collections.Generic;

namespace EverythingCombiner
{
    /// <summary>
    /// 玩家存档数据（可序列化）
    /// 存储所有需要持久化的游戏状态
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // ── 货币 ──
        public int gold;              // 金币（游戏内产出）
        public int gems;              // 钻石（付费货币）

        // ── 收集进度 ──
        public List<string> discoveredElementIds = new List<string>();  // 已发现的元素ID
        public List<string> completedRecipeIds = new List<string>();    // 已完成合成的配方ID
        public int totalDiscoveries;  // 总发现数

        // ── 资源 ──
        public int energy;            // 当前体力
        public int maxEnergy = 30;    // 体力上限
        public string lastEnergyRefillTime; // 上次体力恢复时间

        // ── 道具 ──
        public int hintItems;         // 提示道具数量
        public int luckyCharmItems;   // 幸运符道具数量
        public int speedupItems;      // 加速道具数量

        // ── 每日数据 ──
        public string lastLoginDate;  // 上次登录日期
        public int dailyCombo;        // 连续登录天数
        public List<string> completedDailyTasks = new List<string>(); // 今日已完成日常

        // ── 游戏统计 ──
        public int totalSynthesisCount;   // 总合成次数
        public int totalAdViews;          // 总广告观看次数
        public float totalPlayTimeMinutes; // 总游戏时长（分钟）
        public int highestComboRarity;     // 最高稀有度合成（int）ElementRarity

        // ── 设置 ──
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public bool vibrationEnabled = true;
        public string language = "zh-CN";

        /// <summary>
        /// 创建默认新玩家数据
        /// </summary>
        public static PlayerData CreateDefault()
        {
            var data = new PlayerData
            {
                gold = 100,
                gems = 10,
                energy = 30,
                maxEnergy = 30,
                hintItems = 3,
                luckyCharmItems = 1,
                speedupItems = 1,
                lastEnergyRefillTime = DateTime.UtcNow.ToString("o"),
                lastLoginDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                dailyCombo = 1,
            };

            // 基础元素默认解锁
            data.discoveredElementIds.Add("fire");
            data.discoveredElementIds.Add("water");
            data.discoveredElementIds.Add("earth");
            data.discoveredElementIds.Add("wind");

            return data;
        }
    }
}
