using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum SfxType { Place, Perfect, Combo5, Combo10, Combo20, Fail, Star, Tool, UIClick }

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public struct SfxEntry { public SfxType type; public AudioClip clip; }

    [SerializeField] private List<SfxEntry> sfxClips;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private int sfxPoolSize = 8;

    private Dictionary<SfxType, AudioClip> sfxMap = new();
    private Queue<AudioSource> sfxPool = new();
    private List<AudioSource> activeSfx = new();
    private Dictionary<AudioSource, Coroutine> sfxCoroutines = new();
    private float bgmBasePitch = 1f;
    private float bgmBaseVolume = 0.5f;

    // Priority: higher = more important, lower numbers get interrupted first
    private static readonly Dictionary<SfxType, int> Priority = new()
    {
        {SfxType.Combo20, 5}, {SfxType.Combo10, 4}, {SfxType.Combo5, 3},
        {SfxType.Perfect, 3}, {SfxType.Fail, 4}, {SfxType.Star, 3},
        {SfxType.Place, 1}, {SfxType.Tool, 2}, {SfxType.UIClick, 1}
    };

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var e in sfxClips)
            if (e.clip != null) sfxMap[e.type] = e.clip;

        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource src;
            if (sfxSourcePrefab != null)
            {
                src = Instantiate(sfxSourcePrefab, transform);
            }
            else
            {
                var go = new GameObject("SFXSource");
                go.transform.SetParent(transform);
                src = go.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            sfxPool.Enqueue(src);
        }
    }

    private void Start()
    {
        // Auto-play BGM from ProceduralAudio
        if (bgmSource == null || bgmSource.clip == null)
        {
            var pa = ProceduralAudio.Instance;
            if (pa != null && pa.bgmClip != null)
                PlayBGM(pa.bgmClip);
        }
    }

    public void PlaySFX(SfxType type, float pitch = 1f)
    {
        // Respect SFX mute toggle from SettingsPanel
        if (PlayerPrefs.GetInt("setting_sfx", 1) == 0) return;

        // Try procedural audio fallback
        if (!sfxMap.ContainsKey(type))
        {
            var pa = ProceduralAudio.Instance;
            if (pa != null)
            {
                var clip = pa.GetSFXClip(type);
                if (clip != null) sfxMap[type] = clip;
            }
        }
        if (!sfxMap.ContainsKey(type)) return;

        AudioSource src;
        if (sfxPool.Count > 0)
            src = sfxPool.Dequeue();
        else
        {
            // Reuse the lowest-priority active source
            if (activeSfx.Count == 0) return; // no source available at all
            src = activeSfx[0];
            activeSfx.RemoveAt(0);
            // Stop any stale ReturnSFX coroutine before reusing
            if (sfxCoroutines.TryGetValue(src, out var oldCoroutine) && oldCoroutine != null)
                StopCoroutine(oldCoroutine);
            sfxCoroutines.Remove(src);
            src.Stop();
        }

        activeSfx.Add(src);
        src.pitch = pitch;
        src.PlayOneShot(sfxMap[type]);
        sfxCoroutines[src] = StartCoroutine(ReturnSFX(src, sfxMap[type].length / pitch));
    }

    private System.Collections.IEnumerator ReturnSFX(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay + 0.05f);
        activeSfx.Remove(src);
        src.pitch = 1f;
        sfxCoroutines.Remove(src);
        sfxPool.Enqueue(src);
    }

    // BGM
    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
        }
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.pitch = bgmBasePitch;
        bgmSource.volume = bgmBaseVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void SetBGMIntensity(float t)
    {
        if (bgmSource == null) return;
        // t: 0 (calm) to 1 (intense)
        bgmSource.pitch = Mathf.Lerp(bgmBasePitch, bgmBasePitch * 1.15f, Mathf.Clamp01(t));
        bgmSource.volume = Mathf.Lerp(bgmBaseVolume * 0.6f, bgmBaseVolume * 1.3f, Mathf.Clamp01(t));
    }

    public void PitchDownBGM()
    {
        if (bgmSource == null) return;
        bgmSource.pitch = bgmBasePitch - 0.2f; // ~2 semitones down
        StartCoroutine(FadeBGMVolume(bgmBaseVolume * 0.3f, 0.5f));
    }

    public void RestoreBGM()
    {
        if (bgmSource == null) return;
        bgmSource.pitch = bgmBasePitch;
        StartCoroutine(FadeBGMVolume(bgmBaseVolume, 0.3f));
    }

    public float GetBGMProgress()
    {
        if (bgmSource == null || bgmSource.clip == null) return 0f;
        float effectiveLength = bgmSource.clip.length / bgmSource.pitch;
        return (bgmSource.time % effectiveLength) / effectiveLength;
    }

    private System.Collections.IEnumerator FadeBGMVolume(float target, float duration)
    {
        if (bgmSource == null) yield break;
        float start = bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
    }

    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;
}
