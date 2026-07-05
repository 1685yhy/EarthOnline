using UnityEngine;

namespace EverythingCombiner
{
    /// <summary>
    /// 合成配方 ScriptableObject
    /// 定义 A + B → C 的合成规则
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "万物合成师/合成配方")]
    public class SynthesisRecipe : ScriptableObject
    {
        [Header("配方输入")]
        public ElementData elementA;       // 元素A
        public ElementData elementB;       // 元素B

        [Header("配方输出")]
        public ElementData result;         // 合成结果

        [Header("合成条件")]
        [Range(0f, 100f)]
        public float successRate = 100f;   // 成功率（100=必定成功）
        public bool requiresDiscovery = false; // 是否需要先"发现"这个配方

        [Header("合成特效")]
        public string successMessage;      // 成功文案
        public Color flashColor = Color.yellow; // 闪光颜色
    }
}
