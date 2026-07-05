using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 开场穿越动画 —— 黑屏→心跳→觉醒→字幕→进入游戏。
    /// </summary>
    public class OpeningSequence : MonoBehaviour
    {
        public float totalDuration = 8f;

        void Start()
        {
            StartCoroutine(PlayOpening());
        }

        IEnumerator PlayOpening()
        {
            // 1. 黑屏 + 心跳声 (2秒)
            Debug.Log(""); // blank line for separation
            Debug.Log("┌─────────────────────────────────┐");
            Debug.Log("│                                 │");
            yield return new WaitForSeconds(0.5f);
            Debug.Log("│     ♥ ── 砰 ── 砰 ── 砰        │");
            yield return new WaitForSeconds(1f);
            Debug.Log("│                                 │");
            Debug.Log("│  你睁开眼睛...                   │");
            yield return new WaitForSeconds(1f);
            Debug.Log("│  陌生的天空。陌生的空气。         │");
            yield return new WaitForSeconds(1.5f);
            Debug.Log("│                                 │");
            Debug.Log("│  你穿越了。                      │");
            Debug.Log("│                                 │");
            yield return new WaitForSeconds(2f);
            Debug.Log("│  如果这是你——                    │");
            Debug.Log("│  你会怎么做？                      │");
            Debug.Log("│  你会加入宗门？还是独行天下？       │");
            Debug.Log("│  你会救人？还是杀人？              │");
            Debug.Log("│  你会成为英雄——还是枭雄？         │");
            Debug.Log("│                                   │");
            Debug.Log("│  这个世界没有剧本。                │");
            Debug.Log("│  你的选择——就是唯一的故事。        │");
            Debug.Log("│                                 │");
            yield return new WaitForSeconds(2f);
            Debug.Log("└─────────────────────────────────┘");
            Debug.Log("");

            EventBus.Publish("OnOpeningComplete");
        }
    }
}
