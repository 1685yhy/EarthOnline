using UnityEngine;
using System;

public class EditorSDK : IPlatformSDK
{
    public void Initialize(Action<bool> onComplete) => onComplete?.Invoke(true);

    public void ShowRewardAd(string placement, Action<bool> onComplete)
    {
        Debug.Log($"[EditorSDK] Reward ad shown: {placement}");
        onComplete?.Invoke(true);
    }

    public void ShowInterstitial(Action onComplete)
    {
        Debug.Log("[EditorSDK] Interstitial shown");
        onComplete?.Invoke();
    }

    public void Share(string title, string imageUrl, Action<bool> onComplete)
    {
        Debug.Log($"[EditorSDK] Share: {title}");
        onComplete?.Invoke(true);
    }

    public void GetLeaderboard(string key, int count, Action<LeaderboardEntry[]> onComplete)
    {
        onComplete?.Invoke(new[]
        {
            new LeaderboardEntry { userName="TestPlayer1", score=42, rank=1 },
            new LeaderboardEntry { userName="TestPlayer2", score=35, rank=2 },
            new LeaderboardEntry { userName="TestPlayer3", score=28, rank=3 },
        });
    }

    public void SubmitScore(string key, int score, Action<bool> onComplete)
    {
        Debug.Log($"[EditorSDK] Score submitted: {key}={score}");
        onComplete?.Invoke(true);
    }

    public void Vibrate(string type) => Debug.Log($"[EditorSDK] Vibrate: {type}");

    public string GetUserId() => "editor_test_user";
}
