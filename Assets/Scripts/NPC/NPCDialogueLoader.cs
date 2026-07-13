using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V3.0 NPC对话数据加载器 —— 从Resources/Data/NPCDialogues.json加载对话数据
    /// 并注入到场景中的NPC组件。
    ///
    /// 工作流程：
    /// 1. Awake阶段加载JSON，注册为单例
    /// 2. DialogueTree.BuildDialogue()查询此加载器获取对话节点
    /// 3. 提供NPCBase问候语、秘密、关系等数据的注入接口
    /// </summary>
    public class NPCDialogueLoader : MonoBehaviour
    {
        private static NPCDialogueLoader _instance;
        public static NPCDialogueLoader Instance => _instance;

        [Header("调试")]
        public bool verbose = false;

        private Dictionary<string, NpcData> _dataMap = new();

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadJsonData();
        }

        void Start()
        {
            // 将所有已加载的数据注入到场景中的NPC组件
            InjectToAllNpcs();
        }

        // ============================================================
        // JSON加载
        // ============================================================

        void LoadJsonData()
        {
            int baseCount = LoadFromFile("Data/NPCDialogues");
            int extCount = LoadFromFile("Data/NPCDialogues_Extended");

            if (baseCount == 0 && extCount == 0)
            {
                Debug.LogWarning("[NPCDialogueLoader] 未找到NPC对话数据文件，将使用硬编码对话。");
                return;
            }

            if (verbose)
                Debug.Log($"[NPCDialogueLoader] 成功加载 {_dataMap.Count} 个NPC的对话数据 (基础{baseCount}+扩展{extCount})。");
        }

        /// <summary>
        /// 从指定的 Resources 路径加载 NPC JSON 数据并合并到 _dataMap。
        /// </summary>
        int LoadFromFile(string resourcesPath)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcesPath);
            if (textAsset == null)
            {
                if (verbose) Debug.Log($"[NPCDialogueLoader] {resourcesPath}.json 未找到，跳过。");
                return 0;
            }

            NpcDialoguesContainer container;
            try
            {
                container = JsonUtility.FromJson<NpcDialoguesContainer>(textAsset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NPCDialogueLoader] {resourcesPath}.json 解析失败: {e.Message}");
                return 0;
            }

            if (container == null || container.npcs == null || container.npcs.Length == 0)
            {
                Debug.LogWarning($"[NPCDialogueLoader] {resourcesPath}.json 数据为空。");
                return 0;
            }

            int count = 0;
            foreach (var npc in container.npcs)
            {
                if (!string.IsNullOrEmpty(npc.npcId))
                {
                    _dataMap[npc.npcId] = npc;
                    count++;
                }
            }

            return count;
        }

        // ============================================================
        // NPC注入
        // ============================================================

        void InjectToAllNpcs()
        {
            NPCBase[] allNpcs = FindObjectsOfType<NPCBase>(true);
            int injected = 0;
            foreach (var npc in allNpcs)
            {
                if (InjectNpc(npc))
                    injected++;
            }
            if (verbose && injected > 0)
                Debug.Log($"[NPCDialogueLoader] 已注入 {injected}/{allNpcs.Length} 个NPC。");
        }

        /// <summary>
        /// 为单个NPC注入对话数据（问候语、对话树、秘密、关系）。
        /// 可以在运行时动态创建的NPC上调用。
        /// </summary>
        public bool InjectNpc(NPCBase npc)
        {
            if (npc == null || !_dataMap.ContainsKey(npc.npcId))
                return false;

            NpcData data = _dataMap[npc.npcId];

            // 1. NPCBase问候语（取第一条作为默认问候语）
            if (data.greetingLines != null && data.greetingLines.Length > 0)
            {
                npc.greetingText = data.greetingLines[0];
            }

            // 2. DialogueTree对话节点（后续在BuildDialogue中通过GetDialogueNodes懒加载）
            //    无需在此设置 —— DialogueTree查询NPCDialogueLoader.Instance

            // 3. NPCSecret秘密
            var secretComp = npc.GetComponent<NPCSecret>();
            if (secretComp != null && data.secrets != null && data.secrets.Length > 0)
            {
                var secrets = new List<NPCSecret.Secret>();
                foreach (var s in data.secrets)
                {
                    secrets.Add(new NPCSecret.Secret
                    {
                        revealThreshold = s.revealThreshold,
                        hint = s.hint ?? "",
                        revelation = s.revelation ?? "",
                        revealed = false
                    });
                }
                secretComp.secrets = secrets.ToArray();
            }

            // 4. NPCNetwork关系
            var networkComp = npc.GetComponent<NPCNetwork>();
            if (networkComp != null && data.relations != null && data.relations.Length > 0)
            {
                var relations = new List<NPCNetwork.NPCRelation>();
                foreach (var r in data.relations)
                {
                    if (System.Enum.TryParse<NPCNetwork.RelationType>(r.type, out var relType))
                    {
                        relations.Add(new NPCNetwork.NPCRelation
                        {
                            targetNpcId = r.targetNpcId,
                            type = relType,
                            description = r.description ?? "",
                            closeness = r.closeness
                        });
                    }
                }
                if (relations.Count > 0)
                    networkComp.relations = relations;
            }

            return true;
        }

        // ============================================================
        // 公共查询接口 —— 供DialogueTree等组件调用
        // ============================================================

        /// <summary>
        /// 检查指定NPC是否有外部对话数据。
        /// </summary>
        public bool HasData(string npcId)
        {
            return !string.IsNullOrEmpty(npcId) && _dataMap.ContainsKey(npcId);
        }

        /// <summary>
        /// 获取指定NPC的问候语列表。
        /// </summary>
        public string[] GetGreetingLines(string npcId)
        {
            if (!_dataMap.ContainsKey(npcId)) return null;
            return _dataMap[npcId].greetingLines;
        }

        /// <summary>
        /// 获取指定NPC的告别语列表。
        /// </summary>
        public string[] GetFarewellLines(string npcId)
        {
            if (!_dataMap.ContainsKey(npcId)) return null;
            return _dataMap[npcId].farewellLines;
        }

        /// <summary>
        /// 获取指定NPC的对话树节点字典。
        /// 由DialogueTree.BuildDialogue()调用。
        /// </summary>
        public Dictionary<string, DialogueNode> GetDialogueNodes(string npcId)
        {
            if (!_dataMap.ContainsKey(npcId)) return null;

            NpcData data = _dataMap[npcId];
            var nodes = new Dictionary<string, DialogueNode>();

            foreach (var nd in data.dialogueNodes)
            {
                var node = new DialogueNode
                {
                    id = nd.id,
                    speakerName = string.IsNullOrEmpty(nd.speakerName) ? data.npcName : nd.speakerName,
                    text = nd.text ?? "",
                    nextNodeId = string.IsNullOrEmpty(nd.nextNodeId) ? null : nd.nextNodeId,
                    onEnterEvent = nd.onEnterEvent ?? "",
                    onExitEvent = nd.onExitEvent ?? "",
                    choices = new List<DialogueChoice>(),
                    conditions = new Dictionary<string, string>()
                };

                if (nd.choices != null)
                {
                    foreach (var cd in nd.choices)
                    {
                        node.choices.Add(new DialogueChoice
                        {
                            text = cd.text ?? "",
                            nextNodeId = cd.nextNodeId ?? "",
                            requiredOrigin = cd.requiredOrigin ?? "",
                            requiredGift = cd.requiredGift ?? "",
                            requiredFame = cd.requiredFame,
                            maxInfamy = cd.maxInfamy,
                            consequenceEvent = cd.consequenceEvent ?? ""
                        });
                    }
                }

                nodes[nd.id] = node;
            }

            return nodes;
        }

        // ============================================================
        // JSON序列化数据模型（匹配文件结构）
        // ============================================================

        [System.Serializable]
        public class NpcDialoguesContainer
        {
            public NpcData[] npcs;
        }

        [System.Serializable]
        public class NpcData
        {
            public string npcId;
            public string npcName;
            public string npcTitle;
            public string[] greetingLines;
            public string[] farewellLines;
            public DialogueNodeData[] dialogueNodes;
            public SecretData[] secrets;
            public RelationData[] relations;
        }

        [System.Serializable]
        public class DialogueNodeData
        {
            public string id;
            public string speakerName;
            public string text;
            public ChoiceData[] choices;
            public string nextNodeId;
            public string onEnterEvent;
            public string onExitEvent;
            public string[] conditions;
        }

        [System.Serializable]
        public class ChoiceData
        {
            public string text;
            public string nextNodeId;
            public string requiredOrigin;
            public string requiredGift;
            public int requiredFame;
            public int maxInfamy;
            public string consequenceEvent;
        }

        [System.Serializable]
        public class SecretData
        {
            public int revealThreshold;
            public string hint;
            public string revelation;
        }

        [System.Serializable]
        public class RelationData
        {
            public string targetNpcId;
            public string type;
            public string description;
            public int closeness;
        }
    }
}
