using UnityEngine;
using UnityEngine.UI;
using EarthOnline.Framework;
using System.Collections.Generic;

namespace EarthOnline.UI
{
    /// <summary>
    /// 游戏内HUD。交互提示、对话气泡、状态文字。
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("交互提示")]
        public GameObject interactionHint;
        public Text interactionText;

        [Header("对话气泡")]
        public GameObject dialogueBubble;
        public Text dialogueText;
        public float dialogueDisplayTime = 4f;

        [Header("状态面板")]
        public Text statusText;

        private float _dialogueTimer = 0f;

        void Start()
        {
            interactionHint?.SetActive(false);
            dialogueBubble?.SetActive(false);
            EventBus.Subscribe("OnNPCInteract", OnNPCInteracted);
            EventBus.Subscribe("OnStatusUpdate", OnStatusUpdate);
            EventBus.Subscribe("OnQuestAccepted", OnQuestUpdate);
            EventBus.Subscribe("OnQuestCompleted", OnQuestUpdate);
        }

        void Update()
        {
            if (_dialogueTimer > 0)
            {
                _dialogueTimer -= Time.deltaTime;
                if (_dialogueTimer <= 0)
                    dialogueBubble?.SetActive(false);
            }
        }

        void OnNPCInteracted(Dictionary<string, object> data)
        {
            if (dialogueBubble != null && dialogueText != null)
            {
                string npcName = data.ContainsKey("npcName") ? data["npcName"].ToString() : "???";
                string dialogue = data.ContainsKey("dialogue") ? data["dialogue"].ToString() : "...";
                dialogueText.text = $"<b>{npcName}</b>\n{dialogue}";
                dialogueBubble.SetActive(true);
                _dialogueTimer = dialogueDisplayTime;
            }
        }

        public void ShowInteractionHint(string hint)
        {
            if (interactionHint != null && interactionText != null)
            {
                interactionText.text = hint;
                interactionHint.SetActive(true);
            }
        }

        public void HideInteractionHint()
        {
            interactionHint?.SetActive(false);
        }

        public void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        void OnStatusUpdate(Dictionary<string, object> data)
        {
            string status = data.ContainsKey("status") ? data["status"].ToString() : "";
            UpdateStatus(status);
        }

        void OnQuestUpdate(Dictionary<string, object> data)
        {
            string title = data.ContainsKey("title") ? data["title"].ToString() : "";
            string status = data.ContainsKey("questId") != data.ContainsKey("rewardGold")
                ? $"📋 {title} — 进行中" : $"✅ {title} — 完成！";
            UpdateStatus(status);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnNPCInteracted);
            EventBus.Unsubscribe("OnStatusUpdate", OnStatusUpdate);
            EventBus.Unsubscribe("OnQuestAccepted", OnQuestUpdate);
            EventBus.Unsubscribe("OnQuestCompleted", OnQuestUpdate);
        }
    }
}
