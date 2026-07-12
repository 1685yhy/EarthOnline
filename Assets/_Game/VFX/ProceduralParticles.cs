using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural particle system - creates particles via code, no prefabs needed.
/// Attach to ParticleManager GameObject.
/// Uses TextMesh with proper z-ordering to ensure visibility in orthographic camera.
/// </summary>
public class ProceduralParticles : MonoBehaviour
{
    public static ProceduralParticles Instance { get; private set; }

    private int maxParticles = 500;
    private int activeCount = 0;
    private Camera _cam;

    private void Awake() => Instance = this;
    private void Start() => _cam = Camera.main;

    /// <summary>
    /// Emit star particles at position
    /// </summary>
    public void EmitStars(Vector3 position, Color color, int count = 15)
    {
        for (int i = 0; i < count; i++)
        {
            var go = CreateParticle(position, "★", color, 0.8f);
            if (go == null) continue;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0.5f;
            float angle = Random.value * Mathf.PI * 2f;
            float speed = 2f + Random.value * 5f;
            rb.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 3f);
            rb.angularVelocity = (Random.value - 0.5f) * 360f;
            go.transform.localScale = Vector3.one * (0.3f + Random.value * 0.7f);
        }
    }

    /// <summary>
    /// Emit confetti at position for combos
    /// </summary>
    public void EmitConfetti(Vector3 position, Color baseColor, int count = 30)
    {
        for (int i = 0; i < count; i++)
        {
            Color c = Color.HSVToRGB(Random.value, 0.8f, 1f);
            var go = CreateParticle(position, "■", c, 1.2f);
            if (go == null) continue;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0.3f;
            rb.velocity = new Vector2((Random.value - 0.5f) * 8f, Random.value * 8f + 2f);
            rb.angularVelocity = (Random.value - 0.5f) * 720f;
            go.transform.localScale = new Vector3(0.2f + Random.value * 0.4f, 0.1f + Random.value * 0.2f, 1f);
        }
    }

    /// <summary>
    /// Emit debris at position (fail effect)
    /// </summary>
    public void EmitDebris(Vector3 position, Color color, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            var go = CreateParticle(position, "■", color, 1.5f);
            if (go == null) continue;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.velocity = new Vector2((Random.value - 0.5f) * 6f, Random.value * 5f);
            rb.angularVelocity = (Random.value - 0.5f) * 540f;
            go.transform.localScale = new Vector3(0.15f + Random.value * 0.3f, 0.15f + Random.value * 0.3f, 1f);
        }
    }

    /// <summary>
    /// Emit celebration particles for level complete
    /// </summary>
    public void EmitCelebration(Vector3 position, int starCount = 3)
    {
        for (int s = 0; s < starCount; s++)
        {
            float offsetX = (s - 1) * 1.5f;
            EmitStars(position + new Vector3(offsetX, 0, 0), Color.yellow, 25);
        }
        // Gold burst
        for (int i = 0; i < 40; i++)
        {
            var go = CreateParticle(position, "●", new Color(1f, 0.84f, 0f), 2f);
            if (go == null) continue;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = -0.2f;
            float angle = Random.value * Mathf.PI * 2f;
            float speed = 3f + Random.value * 8f;
            rb.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            go.transform.localScale = Vector3.one * (0.1f + Random.value * 0.3f);
        }
    }

    public void DecrementCount() { activeCount = Mathf.Max(0, activeCount - 1); }

    private GameObject CreateParticle(Vector3 pos, string text, Color color, float lifetime)
    {
        if (activeCount >= maxParticles) return null;
        activeCount++;
        var go = new GameObject("Particle");
        // Place in front of the camera so TextMesh is visible with orthographic rendering
        // z = -1 means between camera (z=-10) and blocks (z=0)
        go.transform.position = new Vector3(pos.x, pos.y, -1f);
        var textMesh = go.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 48; // Larger for better visibility
        textMesh.color = color;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // Ensure text renders on top by sorting in front of sprites
        textMesh.characterSize = 0.05f;

        // Fade and destroy
        var fadeComp = go.AddComponent<ParticleFade>();
        fadeComp.lifetime = lifetime;

        return go;
    }
}

/// <summary>
/// Fades out and destroys a particle GameObject
/// </summary>
public class ParticleFade : MonoBehaviour
{
    public float lifetime = 1f;
    private float elapsed;
    private TextMesh textMesh;
    private Vector3 startScale;

    private void Start()
    {
        textMesh = GetComponent<TextMesh>();
        startScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;
        if (textMesh != null)
        {
            var c = textMesh.color;
            c.a = 1f - t;
            textMesh.color = c;
        }
        // Scale down slightly as we fade
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
        if (t >= 1f)
        {
            var pp = ProceduralParticles.Instance;
            if (pp != null) pp.DecrementCount();
            Destroy(gameObject);
        }
    }
}
