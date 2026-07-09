using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline
{
    /// <summary>
    /// 4K音频管理器 —— 按专业音频方案升级。
    /// Alpha: 19个音频文件 | 三通道(BGM/SFX/Narration) | 分层环境音
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("音频通道")]
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioSource _narrationSource;
        private AudioSource _ambientSource;

        [Header("音量(0-1)")]
        [Range(0,1)] public float masterVolume = 0.8f;
        [Range(0,1)] public float bgmVolume = 0.5f;
        [Range(0,1)] public float sfxVolume = 0.8f;
        [Range(0,1)] public float narrationVolume = 1f;
        [Range(0,1)] public float ambientVolume = 0.4f;

        [Header("SFX池")]
        public int sfxPoolSize = 8;
        private List<AudioSource> _sfxPool = new();
        private int _sfxPoolIndex;

        [Header("BGM曲目")]
        public AudioClip bgmVillage;
        public AudioClip bgmWild;
        public AudioClip bgmBattle;
        public AudioClip bgmVoid;

        [Header("Ambient")]
        public AudioClip ambSpiritFlow;
        public AudioClip ambAncientWind;
        public AudioClip ambDistantBeast;
        public AudioClip ambCraneCry;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);

            // 创建音频通道
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true; _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false; _sfxSource.spatialBlend = 0f;

            _narrationSource = gameObject.AddComponent<AudioSource>();
            _narrationSource.playOnAwake = false; _narrationSource.spatialBlend = 0f;
            _narrationSource.priority = 0; // 最高优先级——叙事音效永不被打断

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.loop = true; _ambientSource.playOnAwake = false;
            _ambientSource.spatialBlend = 0f;

            // SFX对象池
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var go = new GameObject($"SFX_Pool_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sfxPool.Add(src);
            }
        }

        void Start()
        {
            // 订阅游戏事件
            EventBus.Subscribe("OnPlayerAttack", _ => PlaySFX("spirit_attack"));
            EventBus.Subscribe("OnEnemyKilled", _ => PlaySFX("enemy_death"));
            EventBus.Subscribe("OnItemAdded", _ => PlaySFX("item_pickup"));
            EventBus.Subscribe("OnPlayerDeath", _ => PlaySFX("player_death"));
            EventBus.Subscribe("OnQuestCompleted", _ => PlaySFX("quest_complete"));
            EventBus.Subscribe("OnRealmBreakthrough", _ => PlayNarration("realm_breakthrough"));
            EventBus.Subscribe("OnAchievementUnlocked", _ => PlayNarration("achievement"));
            EventBus.Subscribe("OnNPCInteract", _ => PlaySFX("npc_greeting"));

            // 启动环境音
            if (ambSpiritFlow != null) { _ambientSource.clip = ambSpiritFlow; _ambientSource.volume = ambientVolume * masterVolume; _ambientSource.Play(); }

            // 播放村庄BGM
            PlayBGM("village");
        }

        /// <summary>从对象池播放SFX</summary>
        public void PlaySFX(string clipName)
        {
            var clip = Resources.Load<AudioClip>($"Audio/SFX/{clipName}");
            if (clip == null) return;

            var src = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % sfxPoolSize;
            src.Stop();
            src.clip = clip;
            src.volume = sfxVolume * masterVolume;
            src.Play();
        }

        /// <summary>播放叙事音效(独立通道，不被SFX覆盖)</summary>
        public void PlayNarration(string clipName)
        {
            var clip = Resources.Load<AudioClip>($"Audio/Narration/{clipName}");
            if (clip == null) return;
            _narrationSource.Stop();
            _narrationSource.clip = clip;
            _narrationSource.volume = narrationVolume * masterVolume;
            _narrationSource.Play();

            // 播放叙事音时BGM自动Duck(降低)
            StartCoroutine(DuckBGM(clip.length));
        }

        System.Collections.IEnumerator DuckBGM(float duration)
        {
            float origVol = bgmVolume;
            _bgmSource.volume = bgmVolume * 0.3f * masterVolume;
            yield return new WaitForSeconds(duration);
            _bgmSource.volume = origVol * masterVolume;
        }

        /// <summary>播放/切换BGM</summary>
        public void PlayBGM(string trackName)
        {
            AudioClip clip = trackName switch
            {
                "village" => bgmVillage, "wild" => bgmWild,
                "battle" => bgmBattle, "void" => bgmVoid,
                _ => bgmVillage
            };
            if (clip == null || (_bgmSource.clip == clip && _bgmSource.isPlaying)) return;

            StartCoroutine(CrossfadeBGM(clip));
        }

        System.Collections.IEnumerator CrossfadeBGM(AudioClip newClip)
        {
            // Fade out
            float t = 0;
            while (t < 1f) { t += Time.deltaTime * 2f; _bgmSource.volume = bgmVolume * masterVolume * (1f - t); yield return null; }
            _bgmSource.Stop(); _bgmSource.clip = newClip; _bgmSource.Play();
            // Fade in
            t = 0;
            while (t < 1f) { t += Time.deltaTime * 2f; _bgmSource.volume = bgmVolume * masterVolume * t; yield return null; }
        }

        public void SetMasterVolume(float v) { masterVolume = v; }
        public void SetBGMVolume(float v) { bgmVolume = v; }
        public void SetSFXVolume(float v) { sfxVolume = v; }
        public void SetNarrationVolume(float v) { narrationVolume = v; }
    }
}
