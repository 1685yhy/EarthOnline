using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    [SerializeField] private GameObject perfectParticlePrefab;
    [SerializeField] private GameObject comboParticlePrefab;
    [SerializeField] private GameObject failParticlePrefab;
    [SerializeField] private GameObject levelCompleteParticlePrefab;

    private Dictionary<string, Queue<GameObject>> pools = new();

    private void Awake() => Instance = this;

    private GameObject GetFromPool(GameObject prefab)
    {
        if (prefab == null) return null;
        string key = prefab.name;
        if (!pools.ContainsKey(key)) pools[key] = new Queue<GameObject>();

        GameObject obj;
        if (pools[key].Count > 0)
        {
            obj = pools[key].Dequeue();
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(prefab);
        }
        return obj;
    }

    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        string key = obj.name.Replace("(Clone)", "").Trim();
        if (!pools.ContainsKey(key)) pools[key] = new Queue<GameObject>();
        if (pools[key].Count < 20) // prevent unbounded growth
            pools[key].Enqueue(obj);
        else
            Destroy(obj);
    }

    private void PlayAndReturn(GameObject prefab, Vector3 position, Color? color = null,
        int burstCount = 20, float lifetimeOverride = 0f)
    {
        var go = GetFromPool(prefab);
        if (go == null) return;

        go.transform.position = position;
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            if (color.HasValue)
            {
                var main = ps.main;
                main.startColor = color.Value;
            }
            if (burstCount != 20)
            {
                var emit = ps.emission;
                emit.SetBurst(0, new ParticleSystem.Burst(0, (short)Mathf.Min(burstCount, 100)));
            }
            ps.Stop();
            ps.Clear();
            ps.Play();
            float duration = lifetimeOverride > 0 ? lifetimeOverride : ps.main.duration;
            StartCoroutine(ReturnAfterDelay(go, duration + 0.1f));
        }
        else
        {
            Destroy(go, 2f);
        }
    }

    private System.Collections.IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(obj);
    }

    public void EmitPerfect(Vector3 position, Color color)
    {
        if (perfectParticlePrefab != null)
            PlayAndReturn(perfectParticlePrefab, position, color, 20);
        else
            ProceduralParticles.Instance?.EmitStars(position, color, 15);
    }

    public void EmitCombo(Vector3 position, int comboLevel, Color color)
    {
        if (comboParticlePrefab != null)
            PlayAndReturn(comboParticlePrefab, position, color, 20 + comboLevel * 5);
        else
            ProceduralParticles.Instance?.EmitConfetti(position, color, 20 + comboLevel * 5);
    }

    public void EmitFail(Vector3 position)
    {
        if (failParticlePrefab != null)
            PlayAndReturn(failParticlePrefab, position, null, 15);
        else
            ProceduralParticles.Instance?.EmitDebris(position, Color.red, 12);
    }

    public void EmitLevelComplete(Vector3 position, int stars)
    {
        if (levelCompleteParticlePrefab != null)
            PlayAndReturn(levelCompleteParticlePrefab, position, Color.yellow, stars * 15, 3f);
        else
            ProceduralParticles.Instance?.EmitCelebration(position, stars);
    }

    public void ClearAll()
    {
        foreach (var kvp in pools)
            foreach (var obj in kvp.Value)
                if (obj != null) Destroy(obj);
        pools.Clear();
    }
}
