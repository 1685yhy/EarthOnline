using System;

public class WeChatSDK : IPlatformSDK
{
    public void Initialize(Action<bool> onComplete)
    {
        // WeChat WX SDK initialization
        // WX.Initialize();
        // In real implementation, call WX SDK methods
        onComplete?.Invoke(true);
    }

    public void ShowRewardAd(string placement, Action<bool> onComplete)
    {
        // var rewardedVideoAd = WX.CreateRewardedVideoAd({...});
        // rewardedVideoAd.OnClose(res => onComplete?.Invoke(res.isEnded));
        onComplete?.Invoke(true);
    }

    public void ShowInterstitial(Action onComplete)
    {
        // var interstitialAd = WX.CreateInterstitialAd({...});
        // interstitialAd.OnClose(() => onComplete?.Invoke());
        onComplete?.Invoke();
    }

    public void Share(string title, string imageUrl, Action<bool> onComplete)
    {
        // WX.ShareAppMessage({ title, imageUrl });
        onComplete?.Invoke(true);
    }

    public void GetLeaderboard(string key, int count, Action<LeaderboardEntry[]> onComplete)
    {
        // WX.GetFriendCloudStorage({ keyList: [key] });
        onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
    }

    public void SubmitScore(string key, int score, Action<bool> onComplete)
    {
        // WX.SetUserCloudStorage({ KVDataList: [{key, value: score.ToString()}] });
        onComplete?.Invoke(true);
    }

    public void Vibrate(string type)
    {
        // type switch { "light" => WX.VibrateShort("light"), "heavy" => WX.VibrateShort("heavy"), _ => WX.VibrateLong() }
    }

    public string GetUserId() => ""; // WX.GetOpenId() or similar
}
