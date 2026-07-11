using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarthOnline.Framework;

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
                    string marker = quest.status == QuestStatus.Accepted ? " ❓" : " ❗";
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

            // 1. 任务检测优先：有可用任务则先展示任务信息，再等待玩家决定。
            //    这样不受DialogueTree输入干扰（玩家不会错过Q键提示），
            //    并且对所有NPC生效（不依赖DialogueTree组件）。
            var qm = EarthOnline.Framework.QuestManager.Instance;
            if (qm != null)
            {
                var quest = qm.GetQuestFromNPC(npcId);
                if (quest != null)
                {
                    ShowQuestOffer(quest);
                    StartCoroutine(WaitForQuestResolution(quest.id));
                    return; // 等任务决定后再进入对话
                }
            }

            // 2. 无可用任务 → 正常对话
            BeginDialogueOrGreeting();
        }

        void ShowQuestOffer(EarthOnline.Framework.QuestData quest)
        {
            Debug.Log($"── {npcName}{(string.IsNullOrEmpty(npcTitle) ? "" : $" · {npcTitle}")} ──");
            Debug.Log($"📋 任务：{quest.title}");
            Debug.Log($"📄 {quest.description}");
            string rewardStr = $"🎁 奖励：{quest.rewardSpiritStones}灵石 + {quest.rewardCultivation}修为";
            if (!string.IsNullOrEmpty(quest.rewardItemId)) rewardStr += $" + {quest.rewardItemId}";
            Debug.Log(rewardStr);
            Debug.Log($"[{npcName}] 💡 '我有个任务要交给你...' (按Q接受，其他键跳过)");

            // 游戏内Toast通知
            var toast = UnityEngine.Object.FindObjectOfType<EarthOnline.UI.ToastSystem>();
            if (toast != null)
                toast.Show(EarthOnline.UI.ToastSystem.ToastType.Event,
                    $"❗ {npcName}发布了任务：{quest.title} — 按Q接受");
        }

        System.Collections.IEnumerator WaitForQuestResolution(string questId)
        {
            float deadline = Time.time + 5f;
            while (Time.time < deadline)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    EarthOnline.Framework.QuestManager.Instance?.AcceptQuest(questId);
                    yield break; // 接受任务，结束本次交互（玩家可再按E进入对话）
                }
                if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Q))
                    break; // 跳过任务，进入正常对话
                yield return null;
            }
            // 超时/跳过 → 进入对话
            BeginDialogueOrGreeting();
        }

        void BeginDialogueOrGreeting()
        {
            var tree = GetComponent<DialogueTree>();
            if (tree != null)
            {
                tree.StartDialogue();
                // 发布交互事件——重要！
                // DialogueTree路径原本不发布OnNPCInteract，导致Talk/Guidance类任务无法完成
                EarthOnline.Framework.EventBus.Publish("OnNPCInteract", new Dictionary<string, object>
                {
                    {"npcId", npcId}, {"npcName", npcName}, {"dialogue", "对话开始"}
                });
                Invoke(nameof(EndInteraction), 30f);
                return;
            }

            // 无对话树 → 使用好感度系统的个性化问候
            string text = greetingText;
            var rel = GetComponent<NPCRelationship>();
            if (rel != null) text = rel.GetPersonalizedGreeting();

            // 检查NPC记忆——基于过往互动改变态度
            var mem = GetComponent<NPCMemory>();
            string reflection = mem?.GetMemoryReflection();

            // 检查是否有秘密可揭示
            var sec = GetComponent<NPCSecret>();
            string hint = sec?.GetHint();

            Debug.Log($"── {npcName}{(string.IsNullOrEmpty(npcTitle) ? "" : $" · {npcTitle}")} ──");
            Debug.Log($"\"{text}\"");
            if (!string.IsNullOrEmpty(reflection))
                Debug.Log($"({reflection})");
            if (!string.IsNullOrEmpty(hint))
                Debug.Log($"({hint})");

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
