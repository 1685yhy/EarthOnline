using UnityEngine;

namespace EarthOnline
{
    /// <summary>
    /// 新手经济保护判定（经济平衡文档V1 第6章）
    /// 萌新期：境界<练气期 且 游戏天数<=7
    /// 过渡期：境界<筑基期 且 游戏天数<=14
    /// 死亡保护 + 签到加成 + 商店折扣。
    /// </summary>
    public static class NewbieProtection
    {
        public enum ProtectionLevel
        {
            None,       // 保护结束
            Transition, // 过渡期（14天内）
            Sprout      // 萌新期（7天内）
        }

        /// <summary>当前玩家保护等级</summary>
        public static ProtectionLevel Level
        {
            get
            {
                var cult = CultivationManager.Instance;
                var time = TimeManager.Instance;
                if (cult == null || time == null) return ProtectionLevel.None;

                int gameDay = time.GameDay;

                // 萌新：凡人期 + 游戏天数<=7
                if (cult.CurrentRealm == CultivationManager.Realm.Mortal && gameDay <= 7)
                    return ProtectionLevel.Sprout;

                // 过渡：未达筑基期 + 游戏天数<=14
                if (gameDay <= 14 && cult.CurrentRealm < CultivationManager.Realm.Foundation)
                    return ProtectionLevel.Transition;

                return ProtectionLevel.None;
            }
        }

        /// <summary>是否为萌新保护期</summary>
        public static bool IsSprout => Level == ProtectionLevel.Sprout;

        /// <summary>是否在任何保护期内</summary>
        public static bool IsProtected => Level != ProtectionLevel.None;

        /// <summary>获取NPC商店折扣倍率（萌新8折 过渡9折）</summary>
        public static float GetShopDiscount()
        {
            return Level switch
            {
                ProtectionLevel.Sprout => 0.8f,
                ProtectionLevel.Transition => 0.9f,
                _ => 1.0f
            };
        }

        /// <summary>萌新期死亡是否损失灵石</summary>
        public static bool ShouldLoseSpiritStonesOnDeath => !IsSprout;

        /// <summary>获取每日签到新手加成</summary>
        public static int GetSignInBonus()
        {
            return IsSprout ? 20 : 0;
        }
    }
}
