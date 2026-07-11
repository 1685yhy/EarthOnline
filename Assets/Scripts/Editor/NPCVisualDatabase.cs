using UnityEngine;
using System.Collections.Generic;

namespace EarthOnline.Editor
{
    /// <summary>
    /// NPC 标识符 (1–18)
    /// </summary>
    public enum NPCID
    {
        ZhangLao = 1,           // 01 张老 — 神秘老者
        WangTiezhu = 2,         // 02 王铁柱 — 铁匠
        LiLinger = 3,           // 03 李灵儿 — 药铺掌柜
        ChenBanxian = 4,        // 04 陈半仙 — 流浪商人
        ZhaoZhanggui = 5,       // 05 赵掌柜 — 客栈老板
        YunyouDaoren = 6,       // 06 云游道人
        ShouweiDuizhang = 7,    // 07 守卫队长
        LiuLiehu = 8,           // 08 刘猎户
        LaoKuanggong = 9,       // 09 老矿工
        XiaoYaotong = 10,       // 10 小药童
        LinShimei = 11,         // 11 林师妹 (双魂系)
        MingZhanglao = 12,      // 12 明长老 (双魂系)
        Tianxuanzi = 13,        // 13 天玄子 (双魂系)
        JianzhengZhanglao = 14, // 14 见证长老 (双魂系)
        WumingLaozhe = 15,      // 15 无名老者
        SuNianxue = 16,         // 16 苏念雪 — 粉衣女修 (双魂系)
        XuanqingZhenren = 17,   // 17 玄清真人 — 化神修士 (双魂系)
        ChenXuanmo = 18,        // 18 陈玄默 — 醉酒书生 (双魂系)
    }

    /// <summary>
    /// NPC 角色类型（视觉分类，对应角色生成器的 role 参数）
    /// </summary>
    public enum NPCVisualRole
    {
        Elder,          // 长者
        Merchant,       // 商人
        Guard,          // 守卫
        Healer,         // 医者/药铺
        Warrior,        // 战士/武者
        Peasant,        // 平民/铁匠/矿工
        FemaleScholar,  // 女修（苏念雪类型）
        Master,         // 宗师/真人（玄清真人/天玄子类型）
        Drunkard,       // 醉汉（陈玄默类型）
        Child,          // 孩童（小药童类型）
    }

    /// <summary>
    /// NPC 视觉配置数据
    /// </summary>
    public struct NPCVisualConfig
    {
        public string name;
        public Color skinColor;
        public Color clothColor;
        public Color accentColor;
        public NPCVisualRole roleType;
        public float heightScale;
        public float widthScale;
        public bool hasAura;
        public Color auraColor;

        public NPCVisualConfig(
            string name,
            Color skinColor,
            Color clothColor,
            Color accentColor,
            NPCVisualRole roleType,
            float heightScale,
            float widthScale,
            bool hasAura,
            Color auraColor)
        {
            this.name = name;
            this.skinColor = skinColor;
            this.clothColor = clothColor;
            this.accentColor = accentColor;
            this.roleType = roleType;
            this.heightScale = heightScale;
            this.widthScale = widthScale;
            this.hasAura = hasAura;
            this.auraColor = auraColor;
        }
    }

    /// <summary>
    /// 18 个 NPC 的视觉配置静态数据库
    /// 数据来源：/docs/design/npc-visual-spec.md
    /// </summary>
    public static class NPCVisualDatabase
    {
        private static readonly Dictionary<NPCID, NPCVisualConfig> s_configs;

        static NPCVisualDatabase()
        {
            s_configs = new Dictionary<NPCID, NPCVisualConfig>(18);

            // ================================================================
            // 重要 NPC (≤10 Primitives)
            // ================================================================

            // 01 张老 — 神秘老者
            s_configs[NPCID.ZhangLao] = new NPCVisualConfig(
                name: "张老",
                skinColor: HexToColor("#E8D5C4"),
                clothColor: HexToColor("#5C4033"),
                accentColor: HexToColor("#E8E0D0"),
                roleType: NPCVisualRole.Elder,
                heightScale: 1.1f,
                widthScale: 0.8f,
                hasAura: true,
                auraColor: HexToColor("#4488CC")
            );

            // 02 王铁柱 — 铁匠
            s_configs[NPCID.WangTiezhu] = new NPCVisualConfig(
                name: "王铁柱",
                skinColor: HexToColor("#B8956A"),
                clothColor: HexToColor("#8B4513"),
                accentColor: HexToColor("#CC3333"),
                roleType: NPCVisualRole.Peasant,
                heightScale: 0.9f,
                widthScale: 1.3f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 03 李灵儿 — 药铺掌柜
            s_configs[NPCID.LiLinger] = new NPCVisualConfig(
                name: "李灵儿",
                skinColor: HexToColor("#F0E0C8"),
                clothColor: HexToColor("#3A7D44"),
                accentColor: HexToColor("#2E8B57"),
                roleType: NPCVisualRole.Healer,
                heightScale: 0.9f,
                widthScale: 0.7f,
                hasAura: true,
                auraColor: HexToColor("#44CC88")
            );

            // 04 陈半仙 — 流浪商人
            s_configs[NPCID.ChenBanxian] = new NPCVisualConfig(
                name: "陈半仙",
                skinColor: HexToColor("#D4A76A"),
                clothColor: HexToColor("#C4A23A"),
                accentColor: HexToColor("#DAA520"),
                roleType: NPCVisualRole.Merchant,
                heightScale: 1.0f,
                widthScale: 1.2f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 05 赵掌柜 — 客栈老板
            s_configs[NPCID.ZhaoZhanggui] = new NPCVisualConfig(
                name: "赵掌柜",
                skinColor: HexToColor("#E8C9A0"),
                clothColor: HexToColor("#8B2500"),
                accentColor: HexToColor("#F5F5DC"),
                roleType: NPCVisualRole.Merchant,
                heightScale: 1.05f,
                widthScale: 1.15f,
                hasAura: false,
                auraColor: Color.clear
            );

            // ================================================================
            // 次要 NPC (≤5 Primitives)
            // ================================================================

            // 06 云游道人
            s_configs[NPCID.YunyouDaoren] = new NPCVisualConfig(
                name: "云游道人",
                skinColor: HexToColor("#E0D5C0"),
                clothColor: HexToColor("#6B5B8A"),
                accentColor: HexToColor("#6B5B8A"),
                roleType: NPCVisualRole.Warrior,
                heightScale: 1.15f,
                widthScale: 0.75f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 07 守卫队长
            s_configs[NPCID.ShouweiDuizhang] = new NPCVisualConfig(
                name: "守卫队长",
                skinColor: HexToColor("#C4A67A"),
                clothColor: HexToColor("#2C3E50"),
                accentColor: HexToColor("#2C3E50"),
                roleType: NPCVisualRole.Guard,
                heightScale: 1.15f,
                widthScale: 1.2f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 08 刘猎户
            s_configs[NPCID.LiuLiehu] = new NPCVisualConfig(
                name: "刘猎户",
                skinColor: HexToColor("#B8956A"),
                clothColor: HexToColor("#6B4226"),
                accentColor: HexToColor("#6B4226"),
                roleType: NPCVisualRole.Warrior,
                heightScale: 1.0f,
                widthScale: 0.8f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 09 老矿工
            s_configs[NPCID.LaoKuanggong] = new NPCVisualConfig(
                name: "老矿工",
                skinColor: HexToColor("#8B7355"),
                clothColor: HexToColor("#4A4A4A"),
                accentColor: HexToColor("#FFD700"),
                roleType: NPCVisualRole.Peasant,
                heightScale: 0.85f,
                widthScale: 1.15f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 10 小药童
            s_configs[NPCID.XiaoYaotong] = new NPCVisualConfig(
                name: "小药童",
                skinColor: HexToColor("#F0E0C8"),
                clothColor: HexToColor("#90C695"),
                accentColor: HexToColor("#90C695"),
                roleType: NPCVisualRole.Child,
                heightScale: 0.7f,
                widthScale: 0.65f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 11 林师妹 (双魂系)
            s_configs[NPCID.LinShimei] = new NPCVisualConfig(
                name: "林师妹",
                skinColor: HexToColor("#F5E0D0"),
                clothColor: HexToColor("#87CEEB"),
                accentColor: HexToColor("#87CEEB"),
                roleType: NPCVisualRole.FemaleScholar,
                heightScale: 0.8f,
                widthScale: 0.7f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 12 明长老 (双魂系)
            s_configs[NPCID.MingZhanglao] = new NPCVisualConfig(
                name: "明长老",
                skinColor: HexToColor("#D4C4A0"),
                clothColor: HexToColor("#4A0E4E"),
                accentColor: HexToColor("#4A0E4E"),
                roleType: NPCVisualRole.Elder,
                heightScale: 1.1f,
                widthScale: 0.7f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 13 天玄子 (双魂系)
            s_configs[NPCID.Tianxuanzi] = new NPCVisualConfig(
                name: "天玄子",
                skinColor: HexToColor("#E8E0D0"),
                clothColor: HexToColor("#F5F5DC"),
                accentColor: HexToColor("#FFD700"),
                roleType: NPCVisualRole.Master,
                heightScale: 1.2f,
                widthScale: 0.9f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 14 见证长老 (双魂系)
            s_configs[NPCID.JianzhengZhanglao] = new NPCVisualConfig(
                name: "见证长老",
                skinColor: HexToColor("#D4C0A0"),
                clothColor: HexToColor("#696969"),
                accentColor: HexToColor("#696969"),
                roleType: NPCVisualRole.Elder,
                heightScale: 1.0f,
                widthScale: 1.0f,
                hasAura: false,
                auraColor: Color.clear
            );

            // 15 无名老者
            s_configs[NPCID.WumingLaozhe] = new NPCVisualConfig(
                name: "无名老者",
                skinColor: HexToColor("#E0D0C0"),
                clothColor: HexToColor("#808080"),
                accentColor: HexToColor("#808080"),
                roleType: NPCVisualRole.Elder,
                heightScale: 1.0f,
                widthScale: 0.8f,
                hasAura: false,
                auraColor: Color.clear
            );

            // ================================================================
            // 重要双魂新增 NPC (≤10 Primitives)
            // ================================================================

            // 16 苏念雪 — 粉衣女修
            s_configs[NPCID.SuNianxue] = new NPCVisualConfig(
                name: "苏念雪",
                skinColor: HexToColor("#F0D8D0"),
                clothColor: HexToColor("#D87093"),
                accentColor: HexToColor("#9370DB"),
                roleType: NPCVisualRole.FemaleScholar,
                heightScale: 1.0f,
                widthScale: 0.7f,
                hasAura: true,
                auraColor: HexToColor("#D87093")
            );

            // 17 玄清真人 — 化神修士
            s_configs[NPCID.XuanqingZhenren] = new NPCVisualConfig(
                name: "玄清真人",
                skinColor: HexToColor("#E8CFB0"),
                clothColor: HexToColor("#F8F8FF"),
                accentColor: HexToColor("#FFD700"),
                roleType: NPCVisualRole.Master,
                heightScale: 1.15f,
                widthScale: 0.9f,
                hasAura: true,
                auraColor: HexToColor("#FFD700")
            );

            // 18 陈玄默 — 醉酒书生
            s_configs[NPCID.ChenXuanmo] = new NPCVisualConfig(
                name: "陈玄默",
                skinColor: HexToColor("#D4B896"),
                clothColor: HexToColor("#6B7B8D"),
                accentColor: HexToColor("#8B7355"),
                roleType: NPCVisualRole.Drunkard,
                heightScale: 1.0f,
                widthScale: 0.75f,
                hasAura: true,
                auraColor: HexToColor("#4466AA")
            );
        }

        /// <summary>
        /// 获取指定 NPC 的视觉配置。
        /// </summary>
        public static NPCVisualConfig GetConfig(NPCID id)
        {
            return s_configs[id];
        }

        /// <summary>
        /// 尝试获取指定 NPC 的视觉配置，若不存在返回 false。
        /// </summary>
        public static bool TryGetConfig(NPCID id, out NPCVisualConfig config)
        {
            return s_configs.TryGetValue(id, out config);
        }

        /// <summary>
        /// 获取所有已注册的 NPC 配置。
        /// </summary>
        public static IEnumerable<NPCVisualConfig> GetAllConfigs()
        {
            return s_configs.Values;
        }

        /// <summary>
        /// 获取所有已注册的 NPC ID。
        /// </summary>
        public static IEnumerable<NPCID> GetAllIDs()
        {
            return s_configs.Keys;
        }

        /// <summary>
        /// 注册的自定义配置数量（当前固定为 18）。
        /// </summary>
        public static int Count => s_configs.Count;

        // ====================================================================
        // 内部工具
        // ====================================================================

        /// <summary>
        /// 将 hex 颜色字符串 (#RRGGBB) 转换为 Unity Color。
        /// 若解析失败则返回洋红色 (magenta) 以帮助调试。
        /// </summary>
        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            Debug.LogWarning($"[NPCVisualDatabase] Failed to parse hex color: {hex}. Falling back to magenta.");
            return Color.magenta;
        }
    }
}
