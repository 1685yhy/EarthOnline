using UnityEngine;

namespace EverythingCombiner
{
    /// <summary>
    /// 元素稀有度等级
    /// </summary>
    public enum ElementRarity
    {
        Common = 0,    // 普通 - 60%
        Rare = 1,      // 稀有 - 25%
        Epic = 2,      // 史诗 - 10%
        Legend = 3,    // 传说 - 4%
        Mythic = 4     // 神话 - 1%
    }

    /// <summary>
    /// 元素类别（用于图鉴分组）
    /// </summary>
    public enum ElementCategory
    {
        Basic,      // 基础元素
        Material,   // 材料
        Life,       // 生命
        Tool,       // 工具
        Structure,  // 建筑
        Magic,      // 魔法
        Technology, // 科技
        Cosmic,     // 宇宙
        Myth        // 神话
    }

    /// <summary>
    /// 元素数据 ScriptableObject
    /// 每个可合成的元素都是一个 ElementData 资产
    /// </summary>
    [CreateAssetMenu(fileName = "Element_", menuName = "万物合成师/元素数据")]
    public class ElementData : ScriptableObject
    {
        [Header("基础信息")]
        public string elementId;           // 唯一ID，如 "fire", "steam"
        public string elementName;         // 显示名称，如"火"
        public string emoji;               // emoji图标，如"🔥"
        public ElementCategory category;   // 所属类别
        public ElementRarity rarity;       // 稀有度

        [Header("描述")]
        [TextArea(2, 4)]
        public string description;         // 元素描述
        public string discoveryQuote;      // 首次发现时的文案

        [Header("视觉")]
        public Sprite icon;                // 元素图标
        public Color themeColor = Color.white; // 主题色
        public GameObject vfxPrefab;       // 合成成功时的特效（可选）

        [Header("合成")]
        public bool isBaseElement;         // 是否是基础元素（不可被合成）
        public bool isDiscoverable = true; // 是否可以被玩家合成发现
        public int unlockValue = 1;        // 发现时获得的分值
    }
}
