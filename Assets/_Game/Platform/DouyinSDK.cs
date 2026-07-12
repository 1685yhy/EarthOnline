using System;

public class DouyinSDK : IPlatformSDK
{
    public void Initialize(Action<bool> onComplete)
    {
        // TT SDK initialization
        // tt.login(...)
        onComplete?.Invoke(true);
    }

    public void ShowRewardAd(string placement, Action<bool> onComplete)
    {
        // var rewardedVideoAd = tt.createRewardedVideoAd({...});
        // rewardedVideoAd.onClose(res => onComplete?.Invoke(res.isEnded));
        onComplete?.Invoke(true);
    }

    public void ShowInterstitial(Action onComplete)
    {
        // var interstitialAd = tt.createInterstitialAd({...});
        // interstitialAd.onClose(() => onComplete?.Invoke());
        onComplete?.Invoke();
    }

    public void Share(string title, string imageUrl, Action<bool> onComplete)
    {
        // tt.shareAppMessage({ title, imageUrl });
        onComplete?.Invoke(true);
    }

    public void GetLeaderboard(string key, int count, Action<LeaderboardEntry[]> onComplete)
    {
        onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
    }

    public void SubmitScore(string key, int score, Action<bool> onComplete)
    {
        onComplete?.Invoke(true);
    }

    public void Vibrate(string type)
    {
        // type switch { "light" => tt.vibrateShort(), "heavy" => tt.vibrateLong(), _ => tt.vibrateLong() }
    }

    public string GetUserId() => "";
}
