using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool that generates 8 silent placeholder WAV files (1 second, 44100Hz, 16-bit mono)
/// into Resources/Audio/SFX/ and Resources/Audio/Narration/ so AudioManager can
/// load them via Resources.Load without errors.
/// </summary>
public static class AudioPlaceholderGenerator
{
    private const string MenuRoot = "EarthOnline/Generate Audio Placeholders";

    // File names MUST match what AudioManager.PlaySFX / PlayNarration passes to Resources.Load.
    // From AudioManager.Start() event subscriptions:
    private static readonly string[] SfxFiles =
    {
        "spirit_attack.wav",
        "enemy_death.wav",
        "item_pickup.wav",
        "player_death.wav",
        "quest_complete.wav",
        "npc_greeting.wav",
    };

    private static readonly string[] NarrationFiles =
    {
        "realm_breakthrough.wav",
        "achievement.wav",
    };

    // WAV constants for 1 second of 44100Hz 16-bit mono silence
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;
    private const int DataSize = SampleRate * (BitsPerSample / 8); // 88200 bytes
    private const int FileSize = 36 + DataSize; // total file size minus the 8-byte RIFF header size field

    [MenuItem(MenuRoot)]
    private static void Generate()
    {
        string dataPath = Application.dataPath;
        string sfxDir   = Path.Combine(dataPath, "Resources", "Audio", "SFX");
        string narDir   = Path.Combine(dataPath, "Resources", "Audio", "Narration");

        Directory.CreateDirectory(sfxDir);
        Directory.CreateDirectory(narDir);

        byte[] silentWav = BuildSilentWav();

        int generated = 0;

        foreach (string name in SfxFiles)
        {
            string path = Path.Combine(sfxDir, name);
            File.WriteAllBytes(path, silentWav);
            generated++;
        }

        foreach (string name in NarrationFiles)
        {
            string path = Path.Combine(narDir, name);
            File.WriteAllBytes(path, silentWav);
            generated++;
        }

        AssetDatabase.Refresh();

        Debug.Log($"[AudioPlaceholderGenerator] Generated {generated} silent WAV files.\n" +
                  $"  SFX:       {sfxDir}/\n" +
                  $"  Narration: {narDir}/");
    }

    /// <summary>
    /// Build a complete valid WAV byte array containing 1 second of silence.
    /// Format: 44100 Hz, 16-bit, mono, PCM.
    /// </summary>
    private static byte[] BuildSilentWav()
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            // ---------- RIFF header ----------
            bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            bw.Write(FileSize);                     // ChunkSize: file size - 8
            bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

            // ---------- fmt sub-chunk ----------
            bw.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            bw.Write(16);                           // Sub-chunk size (PCM)
            bw.Write((short)1);                     // Audio format (PCM = 1)
            bw.Write(Channels);                     // Num channels
            bw.Write(SampleRate);                   // Sample rate
            bw.Write(SampleRate * Channels * (BitsPerSample / 8)); // Byte rate
            bw.Write((short)(Channels * (BitsPerSample / 8)));    // Block align
            bw.Write(BitsPerSample);                // Bits per sample

            // ---------- data sub-chunk ----------
            bw.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            bw.Write(DataSize);                     // Sub-chunk size

            // Silent PCM data (all zeros for 1 second)
            byte[] silence = new byte[DataSize];
            bw.Write(silence);

            bw.Flush();
            return ms.ToArray();
        }
    }
}
