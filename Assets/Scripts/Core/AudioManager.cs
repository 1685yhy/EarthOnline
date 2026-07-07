using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// M1 音频管理器占位 —— 为Alpha版本提供基础音频框架。
    /// 当前使用Debug.Log模拟音频事件——后续替换为实际AudioSource播放。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("音量")]
        [Range(0,1)] public float masterVolume = 0.8f;
        [Range(0,1)] public float sfxVolume = 0.8f;
        [Range(0,1)] public float bgmVolume = 0.5f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnPlayerAttack", _ => PlaySFX("spirit_attack"));
            EventBus.Subscribe("OnEnemyKilled", _ => PlaySFX("enemy_death"));
            EventBus.Subscribe("OnItemAdded", _ => PlaySFX("item_pickup"));
            EventBus.Subscribe("OnPlayerDeath", _ => PlaySFX("player_death"));
            EventBus.Subscribe("OnQuestCompleted", _ => PlaySFX("quest_complete"));
        }

        public void PlaySFX(string clipName)
        {
            // V3.0替换为实际AudioSource.Play
            // AudioClip clip = Resources.Load<AudioClip>($"Audio/SFX/{clipName}");
            // if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
        }

        public void PlayBGM(string trackName)
        {
            // V3.0替换为实际BGM播放
            // AudioClip clip = Resources.Load<AudioClip>($"Audio/BGM/{trackName}");
        }

        public void SetMasterVolume(float v) { masterVolume = v; }
        public void SetSFXVolume(float v) { sfxVolume = v; }
        public void SetBGMVolume(float v) { bgmVolume = v; }
    }
}
