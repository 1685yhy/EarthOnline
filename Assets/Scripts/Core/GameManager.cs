using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.Gifts;

namespace EarthOnline
{
    /// <summary>
    /// GameManager —— 游戏总控。
    /// V0.1: 初始化框架、注册金手指、管理游戏状态。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("玩家设置")]
        public string playerName = "穿越者";
        public int startingCurrency = 100;

        private GameState _state = GameState.Init;
        public GameState CurrentState => _state;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _state = GameState.Init;
        }

        void Start()
        {
            // 确保核心框架组件存在（EventBus是静态类，不需要Component）
            EnsureComponent<GiftManager>();
            EnsureComponent<SaveManager>();

            // 注册所有金手指模板
            RegisterAllGifts();

            // 测试：自动给玩家激活一个签到系统
            AutoActivateStarterGift();

            _state = GameState.Playing;
            Debug.Log("========== [GameManager] EarthOnline V0.1 Ready ==========");
        }

        void EnsureComponent<T>() where T : Component
        {
            if (GetComponent<T>() == null)
                gameObject.AddComponent<T>();
        }

        void RegisterAllGifts()
        {
            var gm = GiftManager.Instance;
            if (gm == null) return;

            // 注册签到系统
            var signIn = new SignInSystem();
            signIn.Initialize(new Dictionary<string, object>
            {
                {"id", "gift_sign_in_001"},
                {"name", "签到系统"},
                {"type", "System"},
                {"rarity", "R"},
                {"storyOrigin", "来自未知文明的新手引导系统，为所有穿越者提供基本生存保障。但请注意：它的前7任宿主全部在第一个月死亡。"},
                {"storyMystery", "制造这个系统的文明发生了什么？为什么系统日志中有大量被删除的数据？第30天签到将揭示真相..."}
            });
            gm.RegisterTemplate(signIn);

            Debug.Log("[GameManager] All gift templates registered.");
        }

        void AutoActivateStarterGift()
        {
            var gm = GiftManager.Instance;
            if (gm != null)
            {
                var gift = gm.ActivateGift("gift_sign_in_001");
                if (gift != null)
                {
                    Debug.Log($"[GameManager] Starter gift activated: {gift.GiftName}");
                }
            }
        }

        void Update()
        {
            if (_state != GameState.Playing) return;

            // ESC 释放鼠标
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }

            // 测试：按T键触发签到
            if (Input.GetKeyDown(KeyCode.T))
            {
                var gm = GiftManager.Instance;
                if (gm != null)
                {
                    var gifts = gm.GetActiveGifts();
                    foreach (var g in gifts)
                    {
                        g.UseAbility("sign_in");
                    }
                }
            }

            // 测试：按I键查看状态
            if (Input.GetKeyDown(KeyCode.I))
            {
                var gm = GiftManager.Instance;
                if (gm != null)
                {
                    var gifts = gm.GetActiveGifts();
                    foreach (var g in gifts)
                    {
                        g.UseAbility("get_status");
                    }
                }
            }
        }

        void OnDestroy()
        {
            EventBus.Clear();
        }
    }

    public enum GameState
    {
        Init,
        MainMenu,
        Playing,
        Paused,
        Dialog,
        Loading
    }
}
