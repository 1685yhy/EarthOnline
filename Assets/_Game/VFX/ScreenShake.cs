using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    private float intensity;
    private float duration;
    private float elapsed;
    private Vector3 originalPos;
    private float noiseOffsetX;
    private float noiseOffsetY;

    private void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;
        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetY = Random.Range(0f, 100f);
    }

    public void Trigger(float intensity, float duration)
    {
        this.intensity = Mathf.Max(this.intensity, intensity);
        this.duration = Mathf.Max(this.duration, duration);
        this.elapsed = 0f;
        originalPos = transform.localPosition;
        // New noise seeds each trigger for varied feel
        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetY = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (elapsed >= duration)
        {
            if (transform.localPosition != originalPos)
                transform.localPosition = Vector3.Lerp(transform.localPosition, originalPos, Time.deltaTime * 10f);
            return;
        }

        elapsed += Time.deltaTime;
        float decay = 1f - (elapsed / duration);
        decay = decay * decay; // Quadratic decay — smoother fade-out

        // Use Perlin noise for organic, non-repetitive shake
        float noiseX = Mathf.PerlinNoise(noiseOffsetX + elapsed * 60f, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(noiseOffsetY + elapsed * 60f, 1f) * 2f - 1f;

        float x = noiseX * intensity * decay;
        float y = noiseY * intensity * decay;
        transform.localPosition = originalPos + new Vector3(x, y, 0);
    }
}
