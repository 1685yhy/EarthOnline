using System;
using UnityEngine;

namespace EverythingCombiner
{
    /// <summary>
    /// 广告管理器 - 三端广告SDK接口
    /// 统一管理微信/抖音/独立App的广告调用
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("广告配置")]
        [SerializeField] private bool enableAds = true;
        [SerializeField] private float interstitialCooldown = 120f; // 插屏广告冷却时间（秒）

        // 平台类型
        private enum AdPlatform { None, WeChat, Douyin, Standalone }
        private AdPlatform currentPlatform;

        // 冷却追踪
        private float lastInterstitialTime = -999f;
        private int dailyAdViewCount;

        // 回调
        private Action onRewardedComplete;
        private Action onRewardedFailed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                DetectPlatform();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeAdSDK();
        }

        /// <summary>
        /// 检测当前运行平台
        /// </summary>
        private void DetectPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL环境，通过UserAgent判断
            string userAgent = "";
            // 实际环境中通过JS获取：Application.ExternalEval("navigator.userAgent");
            if (userAgent.Contains("MicroMessenger"))
                currentPlatform = AdPlatform.WeChat;
            else if (userAgent.Contains("aweme") || userAgent.Contains("Douyin"))
                currentPlatform = AdPlatform.Douyin;
            else
                currentPlatform = AdPlatform.Standalone;
#else
            currentPlatform = AdPlatform.Standalone;
#endif
            Debug.Log($"[AdManager] 当前广告平台: {currentPlatform}");
        }

        /// <summary>
        /// 初始化对应平台SDK
        /// </summary>
        private void InitializeAdSDK()
        {
            switch (currentPlatform)
            {
                case AdPlatform.WeChat:
                    InitWeChatAds();
                    break;
                case AdPlatform.Douyin:
                    InitDouyinAds();
                    break;
                case AdPlatform.Standalone:
                    InitStandaloneAds();
                    break;
            }
        }

        private void InitWeChatAds()
        {
            // TODO: 接入微信小游戏广告SDK
            // wx.createRewardedVideoAd({ adUnitId: 'xxx' })
            Debug.Log("[AdManager] 微信广告SDK初始化");
        }

        private void InitDouyinAds()
        {
            // TODO: 接入抖音小游戏广告SDK（穿山甲）
            // tt.createRewardedVideoAd({ adUnitId: 'xxx' })
            Debug.Log("[AdManager] 抖音广告SDK初始化");
        }

        private void InitStandaloneAds()
        {
            // TODO: 接入Unity Ads / AdMob
            Debug.Log("[AdManager] 独立App广告SDK初始化");
        }

        // ── 激励视频广告（核心变现方式） ──

        /// <summary>
        /// 展示激励视频广告
        /// </summary>
        /// <param name="placement">广告位标识</param>
        /// <param name="onComplete">看完回调</param>
        /// <param name="onFail">失败回调</param>
        public void ShowRewardedAd(string placement, Action onComplete, Action onFail = null)
        {
            if (!enableAds)
            {
                onComplete?.Invoke();
                return;
            }

            onRewardedComplete = onComplete;
            onRewardedFailed = onFail;

            // 记录广告观看
            var data = GameManager.Instance?.GetPlayerData();
            if (data != null)
            {
                data.totalAdViews++;
                dailyAdViewCount++;
            }

            switch (currentPlatform)
            {
                case AdPlatform.WeChat:
                    ShowWeChatRewardedAd(placement);
                    break;
                case AdPlatform.Douyin:
                    ShowDouyinRewardedAd(placement);
                    break;
                case AdPlatform.Standalone:
                    // 开发/测试环境：直接给奖励
                    Debug.Log($"[AdManager] 测试模式：模拟广告完成 - {placement}");
                    onRewardedComplete?.Invoke();
                    break;
            }
        }

        private void ShowWeChatRewardedAd(string placement)
        {
            // TODO: 调用微信激励视频API
            Debug.Log($"[AdManager] 微信激励视频: {placement}");
            // 模拟完成
            onRewardedComplete?.Invoke();
        }

        private void ShowDouyinRewardedAd(string placement)
        {
            // TODO: 调用抖音激励视频API
            Debug.Log($"[AdManager] 抖音激励视频: {placement}");
            // 模拟完成
            onRewardedComplete?.Invoke();
        }

        // ── 插屏广告 ──

        /// <summary>
        /// 展示插屏广告（有冷却时间）
        /// </summary>
        public bool ShowInterstitialAd(string placement)
        {
            if (!enableAds) return false;

            // 冷却检查
            if (Time.time - lastInterstitialTime < interstitialCooldown)
                return false;

            lastInterstitialTime = Time.time;

            switch (currentPlatform)
            {
                case AdPlatform.WeChat:
                    // wx.showInterstitialAd(...)
                    break;
                case AdPlatform.Douyin:
                    // tt.showInterstitialAd(...)
                    break;
            }

            Debug.Log($"[AdManager] 插屏广告: {placement}");
            return true;
        }

        // ── Banner广告 ──

        public void ShowBannerAd()
        {
            if (!enableAds) return;
            // TODO: 底部Banner广告
        }

        public void HideBannerAd()
        {
            // TODO: 隐藏Banner
        }

        // ── 每日广告限制 ──

        public bool CanShowAd()
        {
            // 每日最多看20个广告（平衡体验和收入）
            return dailyAdViewCount < 20;
        }

        public void ResetDailyAdCount()
        {
            dailyAdViewCount = 0;
        }
    }
}
