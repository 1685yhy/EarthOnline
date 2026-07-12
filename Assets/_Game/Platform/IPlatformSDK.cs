using System;

[Serializable]
public struct LeaderboardEntry
{
    public string userId;
    public string userName;
    public int score;
    public int rank;
}

public interface IPlatformSDK
{
    void Initialize(Action<bool> onComplete);
    void ShowRewardAd(string placement, Action<bool> onComplete);
    void ShowInterstitial(Action onComplete);
    void Share(string title, string imageUrl, Action<bool> onComplete);
    void GetLeaderboard(string key, int count, Action<LeaderboardEntry[]> onComplete);
    void SubmitScore(string key, int score, Action<bool> onComplete);
    void Vibrate(string type);
    string GetUserId();
}
