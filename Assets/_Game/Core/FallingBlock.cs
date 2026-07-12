using UnityEngine;

public class FallingBlock : MonoBehaviour
{
    private float velocityY = 0f;
    private float velocityX = 0f;
    private float rotationSpeed = 0f;
    private float gravity = -9.8f;
    private SpriteRenderer sr;
    private Vector3 startScale;

    public void Init(int direction)
    {
        sr = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
        velocityY = Random.Range(4f, 10f);
        velocityX = direction * Random.Range(1f, 4f);
        if (direction == 0) velocityX = (Random.value - 0.5f) * 4f;
        rotationSpeed = (Random.value - 0.5f) * 360f;
        gravity = -9.8f - Random.value * 5f;
    }

    private void Update()
    {
        velocityY += gravity * Time.deltaTime;
        Vector3 pos = transform.position;
        pos.y += velocityY * Time.deltaTime;
        pos.x += velocityX * Time.deltaTime;
        transform.position = pos;
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        // Smooth fade-out: start fading at y=-5, gone by y=-10
        if (pos.y < -5f)
        {
            float t = Mathf.InverseLerp(-5f, -10f, pos.y);
            if (sr != null)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
            }
            // Scale down as we fall out of view
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
        }
    }
}
