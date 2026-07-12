using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple settings panel with BGM/SFX volume toggles.
/// Works at runtime — no editor dependency.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Button bgmToggle;
    [SerializeField] private Button sfxToggle;
    [SerializeField] private Button vibrationToggle;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text bgmLabel;
    [SerializeField] private Text sfxLabel;
    [SerializeField] private Text vibrationLabel;

    private bool bgmOn = true;
    private bool sfxOn = true;
    private bool vibrationOn = true;

    private void Start()
    {
        // Load settings
        bgmOn = PlayerPrefs.GetInt("setting_bgm", 1) == 1;
        sfxOn = PlayerPrefs.GetInt("setting_sfx", 1) == 1;
        vibrationOn = PlayerPrefs.GetInt("setting_vibration", 1) == 1;

        if (bgmToggle != null) bgmToggle.onClick.AddListener(ToggleBGM);
        if (sfxToggle != null) sfxToggle.onClick.AddListener(ToggleSFX);
        if (vibrationToggle != null) vibrationToggle.onClick.AddListener(ToggleVibration);
        if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

        UpdateLabels();
    }

    private void ToggleBGM()
    {
        bgmOn = !bgmOn;
        PlayerPrefs.SetInt("setting_bgm", bgmOn ? 1 : 0);
        var am = AudioManager.Instance;
        if (am != null)
        {
            if (bgmOn) am.RestoreBGM();
            else am.StopBGM();
        }
        UpdateLabels();
    }

    private void ToggleSFX()
    {
        sfxOn = !sfxOn;
        PlayerPrefs.SetInt("setting_sfx", sfxOn ? 1 : 0);
        UpdateLabels();
    }

    private void ToggleVibration()
    {
        vibrationOn = !vibrationOn;
        PlayerPrefs.SetInt("setting_vibration", vibrationOn ? 1 : 0);
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (bgmLabel != null) bgmLabel.text = bgmOn ? "🔊 音乐 开" : "🔇 音乐 关";
        if (sfxLabel != null) sfxLabel.text = sfxOn ? "🔊 音效 开" : "🔇 音效 关";
        if (vibrationLabel != null) vibrationLabel.text = vibrationOn ? "📳 震动 开" : "📴 震动 关";
    }

    public bool IsBGMOn => bgmOn;
    public bool IsSFXOn => sfxOn;
    public bool IsVibrationOn => vibrationOn;
}
