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
            Font cnFont = Font.CreateDynamicFontFromOSFont("SimHei", 14);
            if (cnFont == null) cnFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cnFont == null) cnFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            _nameTagText.font = cnFont;
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

            // 检查是否有秘密可揭示
            var sec = GetComponent<NPCSecret>();
            string hint = sec?.GetHint();

            Debug.Log($"╔══════════════════════════════╗");
            Debug.Log($"║  {npcName}{(string.IsNullOrEmpty(npcTitle) ? "" : $" · {npcTitle}")}");
            Debug.Log($"╠══════════════════════════════╣");
            Debug.Log($"║  \"{text}\"");
            if (!string.IsNullOrEmpty(hint))
                Debug.Log($"║  ({hint})");
            Debug.Log($"╚══════════════════════════════╝");

            EarthOnline.Framework.EventBus.Publish("OnNPCInteract", new Dictionary<string, object>
            {
                {"npcId", npcId}, {"npcName", npcName}, {"dialogue", text}
            });

            // V2.0: 如果有商店，在对话中提供选项
            var shop = EarthOnline.Framework.ShopManager.Instance;
            if (shop != null)
            {
                var items = shop.GetShop(npcId);
                if (items.Count > 0)
                {
                    Debug.Log($"[{npcName}] 💬 '需要看看我的货吗？' (按Y打开商店，其他键继续)");
                    StartCoroutine(WaitForShopInput(npcId));
                }
            }

            Invoke(nameof(EndInteraction), 3f);
        }

        System.Collections.IEnumerator WaitForShopInput(string shopNpcId)
        {
            float deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    EarthOnline.Framework.ShopManager.Instance?.ShowShop(shopNpcId);
                    yield break;
                }
                if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Y)) yield break;
                yield return null;
            }
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
