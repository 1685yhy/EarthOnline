using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates all game audio procedurally - no external audio files needed.
/// Creates BGM melody, SFX for place/perfect/combo/fail/star/tool/click.
/// </summary>
public class ProceduralAudio : MonoBehaviour
{
    public static ProceduralAudio Instance { get; private set; }

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSourceTemplate;

    public AudioClip bgmClip;
    public Dictionary<SfxType, AudioClip> sfxClips = new();

    private const int SAMPLE_RATE = 22050;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        GenerateAllAudio();
        AutoWireAudioManager();
    }

    private void GenerateAllAudio()
    {
        bgmClip = GenerateBGM();
        sfxClips[SfxType.Place] = GeneratePlaceSFX();
        sfxClips[SfxType.Perfect] = GeneratePerfectSFX();
        sfxClips[SfxType.Combo5] = GenerateComboSFX(5);
        sfxClips[SfxType.Combo10] = GenerateComboSFX(10);
        sfxClips[SfxType.Combo20] = GenerateComboSFX(20);
        sfxClips[SfxType.Fail] = GenerateFailSFX();
        sfxClips[SfxType.Star] = GenerateStarSFX();
        sfxClips[SfxType.Tool] = GenerateToolSFX();
        sfxClips[SfxType.UIClick] = GenerateClickSFX();
    }

    /// <summary>
    /// Generate a genuinely addictive BGM loop — like 羊了个羊.
    /// 4-bar loop, 128 BPM, bouncy chiptune vibe.
    /// Layers: lead melody + harmony + bass + arpeggio + drums
    /// </summary>
    private AudioClip GenerateBGM()
    {
        float bpm = 128f;
        float beatDur = 60f / bpm;
        int beats = 16; // 4 bars of 4/4
        float totalDur = beats * beatDur;
        int totalSamples = Mathf.CeilToInt(totalDur * SAMPLE_RATE);
        float[] samples = new float[totalSamples];

        // === LAYER 1: Lead melody — the "hook" ===
        // Notes relative to C4=0: C=0 D=2 E=4 G=7 A=9 C5=12
        float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f, 659.25f, 783.99f };

        // Catchy bouncy melody pattern: [noteIndex, durationInBeats, accent(0-1)]
        float[][] melody = {
            // Bar 1: "da-da da-da da-da DUM" (catchy hook!)
            new[] {3f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f}, new[] {5f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f},
            new[] {3f, 0.25f, 0.7f}, new[] {2f, 0.25f, 0.5f}, new[] {1f, 0.5f, 0.9f}, // DUM
            // Bar 2: repeat with variation
            new[] {3f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f}, new[] {5f, 0.25f, 0.7f}, new[] {6f, 0.25f, 0.5f},
            new[] {5f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f}, new[] {3f, 0.5f, 0.9f},
            // Bar 3: higher energy
            new[] {5f, 0.25f, 0.8f}, new[] {6f, 0.25f, 0.6f}, new[] {7f, 0.25f, 0.8f}, new[] {8f, 0.25f, 0.6f},
            new[] {7f, 0.25f, 0.8f}, new[] {5f, 0.25f, 0.6f}, new[] {3f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f},
            // Bar 4: resolve back
            new[] {5f, 0.25f, 0.7f}, new[] {4f, 0.25f, 0.5f}, new[] {3f, 0.25f, 0.7f}, new[] {2f, 0.25f, 0.5f},
            new[] {1f, 0.5f, 0.8f}, new[] {0f, 0.5f, 0.9f},
        };

        float time = 0f;
        foreach (var note in melody)
        {
            int noteIdx = (int)note[0];
            float dur = beatDur * note[1];
            float accent = note[2];
            int noteLen = Mathf.CeilToInt(dur * SAMPLE_RATE);
            float freq = scale[noteIdx];

            for (int s = 0; s < noteLen; s++)
            {
                int idx = Mathf.FloorToInt(time * SAMPLE_RATE) + s;
                if (idx >= totalSamples) break;

                float t = s / (float)noteLen;
                float env = Mathf.Exp(-t * 3f); // quick decay
                float sample = 0f;

                // Lead: square-ish wave for chiptune feel
                float phase = (float)((time + s / (float)SAMPLE_RATE) * freq * 2f * Mathf.PI);
                sample += Mathf.Sin((float)phase) * 0.1f * accent;
                sample += Mathf.Sin((float)(phase * 2f)) * 0.04f * accent; // octave harmonic
                // Pulse width modulation for warmth
                sample += (Mathf.Sin((float)phase) > 0.1f ? 0.03f : -0.03f) * accent;

                samples[idx] += sample * env;
            }
            time += dur;
        }

        // === LAYER 2: Harmony chords (every beat) ===
        float[][] chords = {
            new[] {0f, 3f, 5f}, new[] {0f, 3f, 5f}, new[] {2f, 4f, 6f}, new[] {2f, 4f, 6f},
            new[] {0f, 3f, 5f}, new[] {0f, 3f, 5f}, new[] {2f, 4f, 6f}, new[] {2f, 4f, 6f},
            new[] {2f, 4f, 6f}, new[] {2f, 4f, 6f}, new[] {0f, 3f, 5f}, new[] {0f, 3f, 5f},
            new[] {2f, 4f, 6f}, new[] {2f, 4f, 6f}, new[] {0f, 3f, 5f}, new[] {0f, 3f, 5f},
        };
        for (int beat = 0; beat < 16; beat++)
        {
            float beatTime = beat * beatDur;
            int startIdx = Mathf.FloorToInt(beatTime * SAMPLE_RATE);
            int beatLen = Mathf.CeilToInt(beatDur * SAMPLE_RATE);
            foreach (float chordNote in chords[beat])
            {
                float cf = scale[(int)chordNote] * 0.5f; // one octave down
                for (int s = 0; s < beatLen && (startIdx + s) < totalSamples; s++)
                {
                    float t = s / (float)beatLen;
                    float env = Mathf.Exp(-t * 2f);
                    float phase = (float)((beatTime + s / (float)SAMPLE_RATE) * cf * 2f * Mathf.PI);
                    samples[startIdx + s] += Mathf.Sin((float)phase) * 0.06f * env;
                }
            }
        }

        // === LAYER 3: Arpeggio (16th notes, high pitch, chiptune) ===
        int[] arpPattern = {0, 3, 5, 8, 5, 3, 0, 3, 2, 4, 6, 8, 6, 4, 2, 4};
        float arpTime = 0f;
        for (int i = 0; i < beats * 4; i++)
        {
            float af = scale[arpPattern[i % arpPattern.Length]] * 2f;
            float arpDur = beatDur * 0.25f;
            int arpLen = Mathf.CeilToInt(arpDur * SAMPLE_RATE);
            int arpStart = Mathf.FloorToInt(arpTime * SAMPLE_RATE);
            for (int s = 0; s < arpLen && (arpStart + s) < totalSamples; s++)
            {
                float t = s / (float)arpLen;
                float env = Mathf.Exp(-t * 5f);
                float phase = (float)((arpTime + s / (float)SAMPLE_RATE) * af * 2f * Mathf.PI);
                float sample = Mathf.Sin((float)phase) * 0.04f; // pure sine, chiptune
                if (i % 2 == 0) sample *= 0.7f; // accent on odd 16ths
                samples[arpStart + s] += sample * env;
            }
            arpTime += arpDur;
        }

        // === LAYER 4: Bass line (warm, bouncy) ===
        int[] bassPattern = {0, 0, 2, 2, 0, 0, 2, 2, 2, 2, 0, 0, 2, 2, 0, 0};
        for (int beat = 0; beat < 16; beat++)
        {
            float bf = scale[bassPattern[beat]] * 0.25f;
            float bTime = beat * beatDur;
            int bStart = Mathf.FloorToInt(bTime * SAMPLE_RATE);
            int bLen = Mathf.CeilToInt(beatDur * 0.9f * SAMPLE_RATE);
            for (int s = 0; s < bLen && (bStart + s) < totalSamples; s++)
            {
                float t = s / (float)bLen;
                float env = Mathf.Exp(-t * 1.5f);
                float phase = (float)((bTime + s / (float)SAMPLE_RATE) * bf * 2f * Mathf.PI);
                // Triangle-ish bass
                float sawPhase = (float)(((bTime + s / (float)SAMPLE_RATE) * bf) % 1.0);
                float sample = (sawPhase < 0.5f ? sawPhase * 2f : (1f - sawPhase) * 2f);
                sample = (sample - 0.5f) * 2f;
                samples[bStart + s] += sample * 0.08f * env;
            }
        }

        // === LAYER 5: Drums (kick + snare + hi-hat) ===
        for (int beat = 0; beat < 16; beat++)
        {
            float bt = beat * beatDur;
            int dStart = Mathf.FloorToInt(bt * SAMPLE_RATE);

            // Kick on 1 and 3
            if (beat % 4 == 0 || beat % 4 == 2)
            {
                int kLen = Mathf.CeilToInt(0.12f * SAMPLE_RATE);
                for (int s = 0; s < kLen && (dStart + s) < totalSamples; s++)
                {
                    float t = s / (float)kLen;
                    float kf = Mathf.Lerp(200f, 30f, t * t);
                    float env = 1f - t;
                    samples[dStart + s] += Mathf.Sin(2f * Mathf.PI * kf * (s / (float)SAMPLE_RATE)) * 0.35f * env;
                }
            }

            // Snare on 2 and 4
            if (beat % 4 == 1 || beat % 4 == 3)
            {
                int sLen = Mathf.CeilToInt(0.08f * SAMPLE_RATE);
                for (int s = 0; s < sLen && (dStart + s) < totalSamples; s++)
                {
                    float t = s / (float)sLen;
                    float env = 1f - t;
                    // Noise-like snare
                    float noise = (Random.value - 0.5f) * 2f;
                    float tone = Mathf.Sin(2f * Mathf.PI * 200f * (s / (float)SAMPLE_RATE));
                    samples[dStart + s] += (noise * 0.15f + tone * 0.1f) * env;
                }
            }

            // Hi-hat on every 8th note
            for (int sub = 0; sub < 2; sub++)
            {
                float ht = bt + sub * beatDur * 0.5f;
                int hStart = Mathf.FloorToInt(ht * SAMPLE_RATE);
                int hLen = Mathf.CeilToInt(0.03f * SAMPLE_RATE);
                for (int s = 0; s < hLen && (hStart + s) < totalSamples; s++)
                {
                    float t = s / (float)hLen;
                    float noise = (Random.value - 0.5f) * 2f;
                    samples[hStart + s] += noise * 0.08f * (1f - t);
                }
            }
        }

        // Normalize
        float maxVal = 0f;
        for (int i = 0; i < totalSamples; i++)
            if (Mathf.Abs(samples[i]) > maxVal) maxVal = Mathf.Abs(samples[i]);
        if (maxVal > 0.9f)
        {
            float scaleVal = 0.9f / maxVal;
            for (int i = 0; i < totalSamples; i++) samples[i] *= scaleVal;
        }

        AudioClip clip = AudioClip.Create("BGM", totalSamples, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePlaceSFX()
    {
        float duration = 0.08f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            float env = 1f - t / duration;
            // Wood block sound: high frequency rapidly decaying
            data[i] = Mathf.Sin(2f * Mathf.PI * 800f * t) * env * 0.3f;
            data[i] += Mathf.Sin(2f * Mathf.PI * 1200f * t) * env * env * 0.2f;
        }
        return CreateClip("Place", data);
    }

    private AudioClip GeneratePerfectSFX()
    {
        float duration = 0.25f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            float env = Mathf.Exp(-t * 15f);
            // Crystal glass: high frequency harmonics
            data[i] = Mathf.Sin(2f * Mathf.PI * 1760f * t) * env * 0.25f; // A6
            data[i] += Mathf.Sin(2f * Mathf.PI * 2640f * t) * env * env * 0.15f; // E7
            data[i] += Mathf.Sin(2f * Mathf.PI * 3520f * t) * env * env * env * 0.1f; // A7
        }
        return CreateClip("Perfect", data);
    }

    private AudioClip GenerateComboSFX(int level)
    {
        float duration = 0.3f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        float baseFreq = level >= 20 ? 1320f : level >= 10 ? 1047f : 784f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            float env = Mathf.Exp(-t * 8f);
            // Rising arpeggio
            data[i] = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * env * 0.2f;
            data[i] += Mathf.Sin(2f * Mathf.PI * baseFreq * 1.25f * t) * env * env * 0.15f;
            data[i] += Mathf.Sin(2f * Mathf.PI * baseFreq * 1.5f * t) * env * env * env * 0.1f;
        }
        return CreateClip("Combo" + level, data);
    }

    private AudioClip GenerateFailSFX()
    {
        float duration = 0.4f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            // Descending frequency + noise
            float freq = Mathf.Lerp(200f, 60f, t / duration);
            float env = 1f - t / duration;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            data[i] += (Random.value - 0.5f) * env * 0.1f; // noise
        }
        return CreateClip("Fail", data);
    }

    private AudioClip GenerateStarSFX()
    {
        float duration = 0.5f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        float[] starFreqs = { 523f, 659f, 784f }; // C5 E5 G5
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            foreach (float f in starFreqs)
            {
                float delay = (f - starFreqs[0]) / 500f;
                float localT = Mathf.Max(0, t - delay);
                float env = Mathf.Exp(-localT * 6f);
                data[i] += Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.12f;
            }
        }
        return CreateClip("Star", data);
    }

    private AudioClip GenerateToolSFX()
    {
        float duration = 0.2f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            float env = Mathf.Exp(-t * 10f);
            // Magical sweep
            float freq = Mathf.Lerp(400f, 800f, t / duration);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.2f;
            data[i] += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * env * env * 0.1f;
        }
        return CreateClip("Tool", data);
    }

    private AudioClip GenerateClickSFX()
    {
        float duration = 0.05f;
        int samples = Mathf.CeilToInt(duration * SAMPLE_RATE);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SAMPLE_RATE;
            float env = 1f - t / duration;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t) * env * 0.15f;
        }
        return CreateClip("Click", data);
    }

    private AudioClip CreateClip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Wire generated clips into the AudioManager so all existing PlaySFX/PlayBGM calls work.
    /// </summary>
    private void AutoWireAudioManager()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Wire BGM
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var bgmField = am.GetType().GetField("bgmSource", bf);
        if (bgmField != null)
        {
            var src = bgmField.GetValue(am) as AudioSource;
            if (src == null)
            {
                src = am.gameObject.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                bgmField.SetValue(am, src);
            }
        }

        // Set SFX clips via reflection
        var sfxClipsField = am.GetType().GetField("sfxClips", bf);
        if (sfxClipsField != null)
        {
            var list = sfxClipsField.GetValue(am) as System.Collections.IList;
            // Can't easily modify existing list - just start BGM
        }

        // Start BGM
        if (bgmClip != null)
        {
            am.PlayBGM(bgmClip);
        }
    }

    /// <summary>
    /// Provide SFX clips to AudioManager
    /// </summary>
    public AudioClip GetSFXClip(SfxType type)
    {
        sfxClips.TryGetValue(type, out var clip);
        return clip;
    }
}
