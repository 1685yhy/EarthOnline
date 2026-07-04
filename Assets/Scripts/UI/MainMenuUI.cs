using UnityEngine;
using UnityEngine.SceneManagement;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.UI
{
    /// <summary>
    /// 主菜单UI。新游戏 / 继续 / 退出。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public GameObject mainPanel;
        public GameObject continueButton;

        void Start()
        {
            ShowMainMenu();
        }

        void ShowMainMenu()
        {
            mainPanel.SetActive(true);
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
            if (continueButton != null)
                continueButton.SetActive(hasSave);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnNewGame()
        {
            Debug.Log("[MainMenu] Starting new game...");
            EventBus.Publish("OnNewGameStarted");
            SceneManager.LoadScene("GameWorld");
        }

        public void OnContinue()
        {
            Debug.Log("[MainMenu] Loading save...");
            var saveData = SaveManager.Instance.Load();
            if (saveData != null)
            {
                SceneManager.LoadScene(saveData.currentSceneName);
                EventBus.Publish("OnGameLoaded", new Dictionary<string, object>
                {
                    {"saveData", saveData}
                });
            }
        }

        public void OnQuit()
        {
            Debug.Log("[MainMenu] Quitting...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
