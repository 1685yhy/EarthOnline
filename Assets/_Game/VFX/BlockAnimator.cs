using UnityEngine;
using System.Collections;

public class BlockAnimator : MonoBehaviour
{
    public static BlockAnimator Instance { get; private set; }
    private void Awake() => Instance = this;

    /// <summary>
    /// Play a brief entrance animation: scale from 0 to 1 with a slight bounce.
    /// Called when a new moving block is spawned.
    /// </summary>
    public void PlayEntranceAnimation(GameObject block)
    {
        if (block == null) return;
        StartCoroutine(EntranceRoutine(block));
    }

    private IEnumerator EntranceRoutine(GameObject block)
    {
        Transform t = block.transform;
        Vector3 targetScale = t.localScale;
        Vector3 startScale = new Vector3(0.01f, 0.01f, 1f);
        t.localScale = startScale;

        float elapsed = 0f;
        float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            // Overshoot to 1.05 then settle to 1.0
            float scale;
            if (p < 0.7f)
                scale = Mathf.Lerp(0f, 1.05f, p / 0.7f);
            else
                scale = Mathf.Lerp(1.05f, 1f, (p - 0.7f) / 0.3f);
            t.localScale = new Vector3(
                targetScale.x * scale,
                targetScale.y * scale,
                1f);
            yield return null;
        }
        t.localScale = targetScale;
    }

    public void PlayPlaceAnimation(GameObject block, bool isPerfect)
    {
        if (block == null) return;
        StartCoroutine(PlaceAnimRoutine(block, isPerfect));
    }

    private IEnumerator PlaceAnimRoutine(GameObject block, bool isPerfect)
    {
        Vector3 originalScale = block.transform.localScale;
        Transform t = block.transform;

        // Squash down — longer duration so it's actually visible
        float elapsed = 0f;
        float squashDuration = 0.08f;
        while (elapsed < squashDuration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / squashDuration;
            t.localScale = new Vector3(
                originalScale.x * (1f + 0.20f * p),
                originalScale.y * (1f - 0.30f * p),
                1f);
            yield return null;
        }

        // Stretch back with overshoot
        elapsed = 0f;
        float stretchDuration = 0.18f;
        while (elapsed < stretchDuration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / stretchDuration;
            // Spring-back with more pronounced overshoot
            float overshoot = Mathf.Sin(p * Mathf.PI) * (1f - p) * 1.2f;
            t.localScale = new Vector3(
                originalScale.x * (1f - 0.08f * overshoot),
                originalScale.y * (1f + 0.20f * overshoot),
                1f);
            yield return null;
        }

        t.localScale = originalScale;

        // Perfect glow flash — use a coroutine for smoother color flash
        if (isPerfect)
        {
            var sr = block.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color originalColor = sr.color;
                // Rapid white flash
                sr.color = Color.white;
                yield return new WaitForSeconds(0.04f);
                // Fade through yellow to original
                float flashElapsed = 0f;
                float flashDuration = 0.10f;
                while (flashElapsed < flashDuration)
                {
                    flashElapsed += Time.deltaTime;
                    float p = flashElapsed / flashDuration;
                    sr.color = Color.Lerp(Color.yellow, originalColor, p);
                    yield return null;
                }
                sr.color = originalColor;
            }
        }
    }

    public void PlayComboPulse(GameObject block, int comboLevel)
    {
        if (block == null) return;
        StartCoroutine(ComboPulseRoutine(block, comboLevel));
    }

    private IEnumerator ComboPulseRoutine(GameObject block, int comboLevel)
    {
        Vector3 original = block.transform.localScale;
        float scale = 1f + comboLevel * 0.025f;
        scale = Mathf.Min(scale, 1.3f);
        Vector3 target = original * scale;

        float elapsed = 0f;
        float duration = 0.10f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            block.transform.localScale = Vector3.Lerp(original, target, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        duration = 0.20f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            block.transform.localScale = Vector3.Lerp(target, original,
                1f - Mathf.Pow(1f - p, 3f));
            yield return null;
        }

        block.transform.localScale = original;
    }

    public void PlayDropAnimation(GameObject block, float startY, float targetY)
    {
        if (block == null) return;
        StartCoroutine(DropRoutine(block, startY, targetY));
    }

    private IEnumerator DropRoutine(GameObject block, float startY, float targetY)
    {
        float elapsed = 0f;
        float duration = 0.15f;
        Vector3 pos = block.transform.position;
        pos.y = startY;
        block.transform.position = pos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Bounce easing
            float bounce = 1f - Mathf.Pow(1f - t, 4f);
            pos.y = Mathf.Lerp(startY, targetY, bounce);
            block.transform.position = pos;
            yield return null;
        }

        pos.y = targetY;
        block.transform.position = pos;
    }
}
