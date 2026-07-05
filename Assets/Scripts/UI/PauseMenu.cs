using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EarthOnline.Framework;

namespace EarthOnline.UI
{
    /// <summary>
    /// 暂停菜单 —— ESC打开/关闭，显示游戏状态和操作清单。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private bool _isPaused;
        private bool _showVictory;

        void Start()
        {
            EventBus.Subscribe("OnEnemyKilled", OnBossKilled);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        void TogglePause()
        {
            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPaused;

            if (_isPaused)
                PrintPauseInfo();
        }

        void PrintPauseInfo()
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("  ⏸️ 游戏暂停");
            Debug.Log("═══════════════════════════════════");

            var stats = PlayerStats.Instance;
            if (stats != null)
                Debug.Log($"  Lv.{stats.playerLevel} | HP:{stats.currentHP}/{stats.maxHP} | 💰{stats.spiritStones} | ⭐{stats.cultivation}");

            var eq = EquipmentManager.Instance;
            if (eq != null) Debug.Log($"  {eq.GetSummary()}");

            var inv = InventoryManager.Instance;
            if (inv != null) Debug.Log($"  背包: {inv.Count}/{inv.maxSlots}件");

            var time = TimeManager.Instance;
            if (time != null) Debug.Log($"  🕐 第{time.GameDay}天 {time.TimeString} {(time.IsDaytime ? "☀️" : "🌙")}");

            Debug.Log("───────────────────────────────");
            Debug.Log("  按键: WASD移动 | 左键攻击 | E对话");
            Debug.Log("  T签到 | O请教 | I状态 | P背包 | C制作");
            Debug.Log("  H回血 | 1-4技能 | ESC继续");
            Debug.Log("═══════════════════════════════════");
        }

        void OnBossKilled(Dictionary<string, object> data)
        {
            string enemyId = data.ContainsKey("enemyId") ? data["enemyId"].ToString() : "";
            if (enemyId == "boss_001" && !_showVictory)
            {
                _showVictory = true;
                Time.timeScale = 0.1f; // Slow-mo victory
                StartCoroutine(ShowVictory());
            }
        }

        System.Collections.IEnumerator ShowVictory()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Time.timeScale = 0f;

            Debug.Log("══════════════════════════════════════════");
            Debug.Log("                                            ");
            Debug.Log("  🏆 恭喜！你击败了虚空行者！              ");
            Debug.Log("                                            ");
            Debug.Log("  虚空裂缝暂时关闭了...                     ");
            Debug.Log("  但这个世界还有更多的秘密等待探索。        ");
            Debug.Log("                                            ");

            var stats = PlayerStats.Instance;
            var time = TimeManager.Instance;
            if (stats != null && time != null)
            {
                Debug.Log($"  最终状态: Lv.{stats.playerLevel} | 第{time.GameDay}天");
                Debug.Log($"  灵石: {stats.spiritStones} | 修为: {stats.cultivation}");
            }

            Debug.Log("                                            ");
            Debug.Log("  🌍 地球Online V1.0 — 感谢游玩！           ");
            Debug.Log("  传说，未完待续...                         ");
            Debug.Log("                                            ");
            Debug.Log("══════════════════════════════════════════");

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnEnemyKilled", OnBossKilled);
            Time.timeScale = 1f;
        }
    }
}
