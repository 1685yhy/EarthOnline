using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC基类。V0.1：名字标签 + E键对话触发。
    /// 后续版本扩展为完整五层大脑（感知/需求/记忆/决策/行动）。
    /// </summary>
    public class NPCBase : MonoBehaviour
    {
        [Header("基础信息")]
        public string npcId = "npc_001";
        public string npcName = "无名老者";
        public string npcTitle = "";
        public string greetingText = "你好，穿越者...";
        public float interactionRange = 4f;

        private GameObject _nameTagInstance;
        private Text _nameTagText;
        private Transform _playerTransform;
        private bool _playerInRange = false;
        public bool IsInteracting { get; private set; } = false;

        void Start()
        {
            CreateNameTag();
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void CreateNameTag()
        {
            _nameTagInstance = new GameObject("NameTag");
            _nameTagInstance.transform.SetParent(transform);
            _nameTagInstance.transform.localPosition = new Vector3(0, 2.3f, 0);
            _nameTagInstance.transform.localScale = Vector3.one * 0.05f;

            Canvas canvas = _nameTagInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _nameTagInstance.AddComponent<CanvasScaler>();

            GameObject bg = new GameObject("BG");
            bg.transform.SetParent(_nameTagInstance.transform);
            bg.transform.localPosition = Vector3.zero;
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.6f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(200, 40);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(bg.transform);
            textObj.transform.localPosition = Vector3.zero;
            _nameTagText = textObj.AddComponent<Text>();
            _nameTagText.text = string.IsNullOrEmpty(npcTitle)
                ? npcName : $"{npcName}\n<size=10>{npcTitle}</size>";
            // 使用系统字体（2022.3和Unity 6均兼容）
            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
                builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            _nameTagText.font = builtinFont;
            _nameTagText.fontSize = 14;
            _nameTagText.alignment = TextAnchor.MiddleCenter;
            _nameTagText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(180, 36);

            _nameTagInstance.AddComponent<Billboard>();

            // Quest marker
            InvokeRepeating(nameof(UpdateQuestMarker), 1f, 5f);
        }

        void UpdateQuestMarker()
        {
            // Simple: check via QuestManager if this NPC has quests
            var qm = Object.FindObjectOfType<EarthOnline.Framework.QuestManager>();
            if (qm != null)
            {
                var quest = qm.GetQuestFromNPC(npcId);
                if (quest != null && _nameTagText != null)
                {
                    string marker = quest.isAccepted ? " ❓" : " ❗";
                    if (!_nameTagText.text.EndsWith("❗") && !_nameTagText.text.EndsWith("❓"))
                        _nameTagText.text += marker;
                }
            }
        }

        void Update()
        {
            if (_playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            _playerInRange = distance <= interactionRange;

            if (_playerInRange && Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }

        public virtual void Interact()
        {
            IsInteracting = true;

            // 使用好感度系统的个性化问候
            string text = greetingText;
            var rel = GetComponent<NPCRelationship>();
            if (rel != null) text = rel.GetPersonalizedGreeting();

            Debug.Log($"[NPC:{npcName}] {text}");
            EarthOnline.Framework.EventBus.Publish("OnNPCInteract", new Dictionary<string, object>
            {
                {"npcId", npcId}, {"npcName", npcName}, {"dialogue", text}
            });

            Invoke(nameof(EndInteraction), 1.5f);
        }

        void EndInteraction()
        {
            IsInteracting = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }

    /// <summary>让GameObject始终面向主摄像机</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;
        }
    }
}
