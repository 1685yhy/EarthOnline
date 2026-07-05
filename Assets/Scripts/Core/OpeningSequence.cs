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
            Debug.Log("│  这个世界不是为你准备的。         │");
            Debug.Log("│  但它的规则，从今天开始为你改写。   │");
            Debug.Log("│                                 │");
            yield return new WaitForSeconds(2f);
            Debug.Log("└─────────────────────────────────┘");
            Debug.Log("");

            EventBus.Publish("OnOpeningComplete");
        }
    }
}
