using UnityEngine;

public class PlatformBridge : MonoBehaviour
{
    public static PlatformBridge Instance { get; private set; }
    public IPlatformSDK SDK { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_WECHAT
        SDK = new WeChatSDK();
#elif UNITY_DOUYIN
        SDK = new DouyinSDK();
#else
        SDK = new EditorSDK();
#endif
    }
}
