using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 世界配置 —— 每个世界有独立的货币/社会/成长体系。
    /// 地球意志将玩家投放到不同的世界，每个世界规则不同。
    /// </summary>
    [CreateAssetMenu(fileName = "WorldConfig", menuName = "EarthOnline/World Config")]
    public class WorldConfig : ScriptableObject
    {
        [Header("世界基础")]
        public string worldId;
        public string worldName;
        public string worldDescription;
        public string worldType; // Cultivation(修真), Urban(都市), Apocalypse(末日), Fantasy(西幻), SciFi(科幻)

        [Header("货币体系")]
        public string currencyName = "灵石";
        public string currencyIcon = "💎";
        public string currencyDescription = "修真世界通用货币，蕴含微量灵力。既可用于交易，也可直接吸收修炼。";

        [Header("社会结构")]
        public string socialStructure = "宗门林立，散修艰难求生。势力分为：凡人王朝、修仙宗门、上古世家。";
        public string[] factions = { "天元宗", "青云门", "散修联盟", "凡人王朝" };

        [Header("成长体系")]
        public string growthSystem = "修炼体系：练气→筑基→金丹→元婴→化神→渡劫";
        public int[] levelThresholds = { 100, 300, 600, 1000, 1500 }; // 每阶所需修为
        public string[] realmNames = { "练气期", "筑基期", "金丹期", "元婴期", "化神期" };

        [Header("可用金手指")]
        public string[] availableGiftTypes = { "System", "Mentor", "Body", "Weapon", "Bloodline", "Knowledge" };

        [Header("起始条件")]
        public int startingCurrency = 100;
        public int startingHP = 100;
        public string startingScene = "EarthOnline_Main";
        public string startingPosition = "0,1.5,0";

        /// <summary>
        /// 根据修为获取当前境界名称。
        /// </summary>
        public string GetRealmName(int cultivation)
        {
            for (int i = levelThresholds.Length - 1; i >= 0; i--)
                if (cultivation >= levelThresholds[i])
                    return realmNames[Mathf.Min(i + 1, realmNames.Length - 1)];
            return realmNames.Length > 0 ? realmNames[0] : "凡人";
        }
    }
}
