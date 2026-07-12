using UnityEngine;

public class DailyChallenge : MonoBehaviour
{
    public static DailyChallenge Instance { get; private set; }
    private void Awake() => Instance = this;

    public (int targetLayers, float speedMul, float blockWidth, int seed) GetToday()
    {
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        int seed = 0;
        foreach (char c in today)
            seed = seed * 31 + c;
        System.Random rng = new System.Random(seed);

        int target = 15 + Mathf.Abs(seed % 30);
        float speed = 1.5f + Mathf.Abs((seed >> 8) % 100) / 50f;
        float width = 0.4f + Mathf.Abs((seed >> 16) % 100) / 250f;

        return (target, speed, width, seed);
    }
}
