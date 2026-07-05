using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EverythingCombiner
{
    /// <summary>
    /// 游戏主管理器
    /// 统筹所有子系统，管理游戏状态
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("系统引用")]
        [SerializeField] private SynthesisManager synthesisManager;
        [SerializeField] private SaveManager saveManager;

        [Header("游戏状态")]
        public GameState currentState = GameState.Idle;

        // 状态事件
        public UnityEvent<GameState> OnStateChanged;
        public UnityEvent<ElementData> OnElementDiscovered;
        public UnityEvent<int> OnGoldChanged;
        public UnityEvent<int> OnGemsChanged;

        // 当前合成状态
        public ElementData selectedElementA { get; private set; }
        public ElementData selectedElementB { get; private set; }
        public bool hasSelectedElement => selectedElementA != null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            // 自动查找依赖
            if (synthesisManager == null)
                synthesisManager = FindAnyObjectByType<SynthesisManager>();
            if (saveManager == null)
                saveManager = FindAnyObjectByType<SaveManager>();

            // 订阅事件
            if (synthesisManager != null)
            {
                synthesisManager.OnSynthesisSuccess += HandleSynthesisSuccess;
                synthesisManager.OnSynthesisFail += HandleSynthesisFail;
                synthesisManager.OnNewElementDiscovered += HandleNewElement;
            }

            SetState(GameState.Idle);
        }

        /// <summary>
        /// 选择第一个合成元素
        /// </summary>
        public void SelectElementA(ElementData element)
        {
            selectedElementA = element;
            SetState(GameState.Selecting);
        }

        /// <summary>
        /// 选择第二个合成元素并执行合成
        /// </summary>
        public void SelectElementB(ElementData element)
        {
            selectedElementB = element;
            SetState(GameState.Synthesizing);

            if (synthesisManager != null)
            {
                var result = synthesisManager.TrySynthesize(selectedElementA, selectedElementB);
                // 结果在 HandleSynthesisSuccess/HandleSynthesisFail 中处理
            }

            // 重置选择
            selectedElementA = null;
            selectedElementB = null;
        }

        /// <summary>
        /// 取消当前选择
        /// </summary>
        public void CancelSelection()
        {
            selectedElementA = null;
            selectedElementB = null;
            SetState(GameState.Idle);
        }

        // ── 事件处理 ──

        private void HandleSynthesisSuccess(ElementData a, ElementData b, ElementData result, ElementRarity rarity)
        {
            Debug.Log($"✨ 合成成功: {a.elementName} + {b.elementName} → {result.elementName} [{rarity}]");
            SetState(GameState.ResultSuccess);

            // 自动存档
            saveManager?.AutoSave();
        }

        private void HandleSynthesisFail(ElementData a, ElementData b)
        {
            Debug.Log($"❌ 合成失败: {a.elementName} + {b.elementName} 没有配方");
            SetState(GameState.ResultFail);
        }

        private void HandleNewElement(ElementData element)
        {
            Debug.Log($"🎉 新发现: {element.elementName}!");
            OnElementDiscovered?.Invoke(element);

            // 给予金币奖励
            int reward = (int)element.rarity * 10 + element.unlockValue;
            AddGold(reward);
        }

        // ── 货币管理 ──

        public void AddGold(int amount)
        {
            if (saveManager?.CurrentData == null) return;
            saveManager.CurrentData.gold += amount;
            OnGoldChanged?.Invoke(saveManager.CurrentData.gold);
        }

        public bool SpendGold(int amount)
        {
            if (saveManager?.CurrentData == null) return false;
            if (saveManager.CurrentData.gold < amount) return false;
            saveManager.CurrentData.gold -= amount;
            OnGoldChanged?.Invoke(saveManager.CurrentData.gold);
            return true;
        }

        public void AddGems(int amount)
        {
            if (saveManager?.CurrentData == null) return;
            saveManager.CurrentData.gems += amount;
            OnGemsChanged?.Invoke(saveManager.CurrentData.gems);
        }

        public bool SpendGems(int amount)
        {
            if (saveManager?.CurrentData == null) return false;
            if (saveManager.CurrentData.gems < amount) return false;
            saveManager.CurrentData.gems -= amount;
            OnGemsChanged?.Invoke(saveManager.CurrentData.gems);
            return true;
        }

        // ── 状态管理 ──

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        public PlayerData GetPlayerData()
        {
            return saveManager?.CurrentData;
        }

        private void OnDestroy()
        {
            if (synthesisManager != null)
            {
                synthesisManager.OnSynthesisSuccess -= HandleSynthesisSuccess;
                synthesisManager.OnSynthesisFail -= HandleSynthesisFail;
                synthesisManager.OnNewElementDiscovered -= HandleNewElement;
            }
        }
    }

    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        Idle,           // 空闲，等待玩家操作
        Selecting,      // 已选择第一个元素，等待第二个
        Synthesizing,   // 正在合成中
        ResultSuccess,  // 合成成功，展示结果
        ResultFail,     // 合成失败，展示反馈
        MenuOpen,       // 菜单打开中
        ShopOpen,       // 商店打开中
    }
}
