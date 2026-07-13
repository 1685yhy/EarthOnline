using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V2.2 对话树系统 —— 玩家选择塑造命运。
    /// 不再是"按E→NPC说一句话→结束"。而是"按E→NPC说话→玩家选择→NPC回应→世界变化"。
    /// </summary>
    [System.Serializable]
    public class DialogueNode
    {
        public string id;
        public string speakerName;      // 谁说
        public string text;             // 对话内容
        public List<DialogueChoice> choices = new(); // 玩家可选回复
        public string nextNodeId;       // 无选项时自动跳到下一个节点
        public string onEnterEvent;     // 进入节点时触发的事件
        public string onExitEvent;      // 离开节点时触发的事件
        public Dictionary<string, string> conditions = new(); // 显示条件
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string text;             // 显示给玩家的文字
        public string nextNodeId;       // 选择后跳到哪个节点
        public string requiredOrigin;   // 需要特定出身
        public string requiredGift;     // 需要特定金手指
        public int requiredFame;        // 需要最低善名
        public int maxInfamy;           // 最高恶名限制
        public string consequenceEvent; // 选择后果事件
    }

    [RequireComponent(typeof(NPCBase))]
    public class DialogueTree : MonoBehaviour
    {
        private NPCBase _npc;
        private Dictionary<string, DialogueNode> _nodes = new();
        private DialogueNode _currentNode;
        private bool _inDialogue;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            BuildDialogue();
        }

        void BuildDialogue()
        {
            // V3.0: 优先使用外部JSON对话数据（NPCDialogueLoader）
            // 如果加载器存在且有该NPC的数据，则跳过硬编码对话树
            var loader = NPCDialogueLoader.Instance;
            if (loader != null && loader.HasData(_npc.npcId))
            {
                var loaded = loader.GetDialogueNodes(_npc.npcId);
                if (loaded != null && loaded.Count > 0)
                {
                    _nodes = loaded;
                    return;
                }
            }

            // 降级到硬编码对话树（适用于无外部数据或加载失败的NPC）
            _nodes = _npc.npcId switch
            {
                "npc_zhang_001" => BuildZhangDialogue(),
                "npc_wang_001" => BuildWangDialogue(),
                "npc_li_001" => BuildLiDialogue(),
                "npc_chen_001" => BuildChenDialogue(),
                "npc_zhao_001" => BuildZhaoDialogue(),
                _ => BuildDefaultDialogue()
            };
        }

        public void StartDialogue()
        {
            if (_inDialogue) return;
            _inDialogue = true;
            _currentNode = _nodes.ContainsKey("start") ? _nodes["start"] : null;
            if (_currentNode != null) ShowCurrentNode();
        }

        void ShowCurrentNode()
        {
            if (_currentNode == null) { EndDialogue(); return; }

            Debug.Log($"── {_currentNode.speakerName} ──");
            Debug.Log($"\"{_currentNode.text}\"");

            // 显示玩家选项
            if (_currentNode.choices.Count > 0)
            {
                Debug.Log($"  你的回复:");
                for (int i = 0; i < _currentNode.choices.Count; i++)
                {
                    var c = _currentNode.choices[i];
                    bool available = IsChoiceAvailable(c);
                    string prefix = available ? $"{i + 1}" : "✗";
                    Debug.Log($"    [{prefix}] {c.text}{(available ? "" : " (条件不满足)")}");
                }
                Debug.Log($"  按数字键1-{_currentNode.choices.Count}选择回复");
            }
            else
            {
                Debug.Log($"  (按任意键继续)");
                StartCoroutine(WaitForContinue());
            }
        }

        bool IsChoiceAvailable(DialogueChoice c)
        {
            if (!string.IsNullOrEmpty(c.requiredOrigin))
            {
                var origin = OriginManager.ChosenOrigin.ToString();
                if (origin != c.requiredOrigin) return false;
            }
            if (c.requiredFame > 0)
            {
                var rep = ReputationSystem.Instance;
                if (rep == null || rep.fame < c.requiredFame) return false;
            }
            if (c.maxInfamy < 999)
            {
                var rep = ReputationSystem.Instance;
                if (rep != null && rep.infamy > c.maxInfamy) return false;
            }
            return true;
        }

        void Update()
        {
            if (!_inDialogue) return;

            // 数字键选择
            if (_currentNode != null && _currentNode.choices.Count > 0)
            {
                for (int i = 0; i < _currentNode.choices.Count; i++)
                {
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    {
                        MakeChoice(i);
                        return;
                    }
                }
            }
        }

        void MakeChoice(int index)
        {
            if (_currentNode == null || index >= _currentNode.choices.Count) return;
            var choice = _currentNode.choices[index];

            if (!IsChoiceAvailable(choice))
            {
                Debug.Log($"[对话] 这个选项当前不可用。");
                return;
            }

            // 处理后果
            if (!string.IsNullOrEmpty(choice.consequenceEvent))
            {
                HandleConsequence(choice.consequenceEvent);
            }

            // 跳转
            if (!string.IsNullOrEmpty(choice.nextNodeId) && _nodes.ContainsKey(choice.nextNodeId))
            {
                _currentNode = _nodes[choice.nextNodeId];
                ShowCurrentNode();
            }
            else
            {
                EndDialogue();
            }
        }

        void HandleConsequence(string eventName)
        {
            switch (eventName)
            {
                case "give_herb_quest":
                    Debug.Log($"[{_npc.npcName}] '拿着这些药，去北山看看。那里有你需要的答案。'");
                    PlayerStats.Instance?.AddSpiritStone(30);
                    break;
                case "reveal_secret":
                    var sec = GetComponent<NPCSecret>();
                    Debug.Log($"[{_npc.npcName}] 向你透露了一个秘密...");
                    break;
                case "gain_cultivation":
                    PlayerStats.Instance?.AddCultivation(25);
                    Debug.Log($"[{_npc.npcName}] 传授了你一些修炼心得 +25修为。");
                    break;
                case "offend_npc":
                    var mem = GetComponent<NPCMemory>();
                    mem?.Remember(MemoryType.Harmed, $"被你无礼对待", -8);
                    ReputationSystem.Instance?.AddInfamy(5, $"对{_npc.npcName}无礼");
                    Debug.Log($"[{_npc.npcName}] 脸色一沉...");
                    break;
            }
        }

        System.Collections.IEnumerator WaitForContinue()
        {
            float deadline = Time.time + 5f;
            while (Time.time < deadline)
            {
                if (Input.anyKeyDown)
                {
                    if (!string.IsNullOrEmpty(_currentNode?.nextNodeId) && _nodes.ContainsKey(_currentNode.nextNodeId))
                    {
                        _currentNode = _nodes[_currentNode.nextNodeId];
                        ShowCurrentNode();
                    }
                    else
                    {
                        EndDialogue();
                    }
                    yield break;
                }
                yield return null;
            }
            EndDialogue();
        }

        void EndDialogue()
        {
            _inDialogue = false;
            _currentNode = null;
            Debug.Log($"[对话] 结束。");
        }

        // --- NPC-specific dialogues ---

        Dictionary<string, DialogueNode> BuildZhangDialogue()
        {
            var d = new Dictionary<string, DialogueNode>();
            d["start"] = new DialogueNode { id="start", speakerName="张老", text="年轻人，你也穿越了？...看来这个世界的'玩家'越来越多了。你有什么想问老夫的？",
                choices = new() {
                    new() { text="这个世界的秘密是什么？", nextNodeId="secret", requiredFame=10 },
                    new() { text="你怎么知道我是穿越者？", nextNodeId="know_you" },
                    new() { text="你能教我修炼吗？", nextNodeId="teach", requiredFame=20 },
                    new() { text="没什么，路过而已。", nextNodeId="bye" },
                }};
            d["secret"] = new DialogueNode { id="secret", speakerName="张老", text="这个世界...被虚空侵蚀了三百年。天道正在崩溃。每一个穿越者都是地球意志投放到这里的——为了找到解决虚空的方法。",
                nextNodeId="start" };
            d["know_you"] = new DialogueNode { id="know_you", speakerName="张老", text="老夫见过很多穿越者了。你是第47个。前46个...大多数都死了。有的被宗门杀了。有的被虚空吞了。有的——自己选择了放弃。",
                choices = new() {
                    new() { text="我不会成为第47个。", nextNodeId="determined", consequenceEvent="gain_cultivation" },
                    new() { text="那你还在这里等什么？", nextNodeId="waiting" },
                }};
            d["determined"] = new DialogueNode { id="determined", speakerName="张老", text="哈！说得好。老夫在你身上看到了一些不一样的东西。拿着——这是老夫年轻时写的一些修炼心得。",
                nextNodeId="start" };
            d["waiting"] = new DialogueNode { id="waiting", speakerName="张老", text="在等我妻子。她在虚空里。三十年了——我知道她还活着。每一个穿越者都是我找到她的希望。每一个穿越者——虚空都会放出来。但没有人能把她带回来。",
                nextNodeId="start" };
            d["teach"] = new DialogueNode { id="teach", speakerName="张老", text="修炼之道，重在坚持。每天签到、多和这个世界的人交流、在灵脉上修炼——这些看似简单的事，坚持下去，你会比大多数穿越者走得更远。",
                onEnterEvent="gain_cultivation", nextNodeId="start" };
            d["bye"] = new DialogueNode { id="bye", speakerName="张老", text="去吧。这个世界很大——去看看。但记住——虚空不会等你的。", nextNodeId=null };
            return d;
        }

        Dictionary<string, DialogueNode> BuildWangDialogue() => new() {
            ["start"] = new DialogueNode { id="start", speakerName="王铁柱", text="嘿！要打铁吗？我这儿可是整个新手村最好的铁匠铺！",
                choices = new() {
                    new() { text="我想看看你的货。", nextNodeId="shop" },
                    new() { text="你这铁匠铺...开了多久了？", nextNodeId="story" },
                    new() { text="算了，下次吧。", nextNodeId=null },
                }},
            ["shop"] = new DialogueNode { id="shop", speakerName="王铁柱", text="自己看！都是好货。按Y键打开交易。", nextNodeId="start" },
            ["story"] = new DialogueNode { id="story", speakerName="王铁柱", text="...(他手上的锤子停了一下)...十年了。从离开炼器阁到现在。这把铁锤跟了我十年——比我弟弟跟我的时间都长。",
                choices = new() {
                    new() { text="你弟弟...他还好吗？", nextNodeId="brother", requiredFame=15 },
                    new() { text="不提不开心的事了。", nextNodeId="start", consequenceEvent="gain_cultivation" },
                }},
            ["brother"] = new DialogueNode { id="brother", speakerName="王铁柱", text="...（他沉默了很久）那把剑——我铸的。他拿着它杀了天元宗的长老。现在他们在找铸剑的人。也在找他。如果你见到他...告诉他——哥哥不怪他。",
                nextNodeId="start" },
        };

        Dictionary<string, DialogueNode> BuildLiDialogue() => new() {
            ["start"] = new DialogueNode { id="start", speakerName="李灵儿", text="需要买药吗？我这里有各种丹药——都是我亲手炼的。",
                choices = new() {
                    new() { text="看看有什么药。", nextNodeId="shop" },
                    new() { text="山里为什么采不到药了？", nextNodeId="herbs" },
                    new() { text="你的炼丹术...是谁教的？", nextNodeId="father", requiredFame=10 },
                }},
            ["shop"] = new DialogueNode { id="shop", speakerName="李灵儿", text="按Y键看货。需要什么直接说。", nextNodeId="start" },
            ["herbs"] = new DialogueNode { id="herbs", speakerName="李灵儿", text="虚空裂缝越来越大。山里的妖兽被虚空影响了——变得狂躁。采药人都不敢去了。你要是敢进山——帮我采点药回来。我会给你报酬的。",
                nextNodeId="start", onExitEvent="give_herb_quest" },
            ["father"] = new DialogueNode { id="father", speakerName="李灵儿", text="我爹...前天元宗副宗主。他不许我告诉任何人。但你救了张老的孙子——我信你。我爹被宗主废掉了修为。因为他发现了宗主在用人血炼丹。他想阻止——然后他变成了一个废人。",
                nextNodeId="start" },
        };

        Dictionary<string, DialogueNode> BuildChenDialogue() => new() {
            ["start"] = new DialogueNode { id="start", speakerName="陈半仙", text="走过路过不要错过！我这儿的货——嘿，不是我吹，整个灵气大陆你找不到第二家！",
                choices = new() {
                    new() { text="看看有什么。", nextNodeId="shop" },
                    new() { text="你的货是从哪来的？", nextNodeId="source" },
                    new() { text="你听说过'活着的墓'吗？", nextNodeId="tomb", requiredFame=20 },
                }},
            ["shop"] = new DialogueNode { id="shop", speakerName="陈半仙", text="自己挑自己选！按Y交易。", nextNodeId="start" },
            ["source"] = new DialogueNode { id="source", speakerName="陈半仙", text="哈！你以为我是流浪商人？我是倒斗的！专门挖古修士的墓。这些东西都是从墓里挖出来的。每一件都有来历。不敢说全干净——但绝对都是真货。",
                nextNodeId="start" },
            ["tomb"] = new DialogueNode { id="tomb", speakerName="陈半仙", text="...(他压低了声音)你也知道？那是一座活着的墓——每百年出现一次。下次出现是三个月后。我已经在找队伍了。你要是敢去——算你一个。但丑话说在前头：上一次进墓的十个人——只出来了两个。",
                nextNodeId="start" },
        };

        Dictionary<string, DialogueNode> BuildZhaoDialogue() => new() {
            ["start"] = new DialogueNode { id="start", speakerName="赵掌柜", text="客官里边请！住宿50灵石一晚。打听消息？那得看你请我喝什么酒了。",
                choices = new() {
                    new() { text="最近有什么新消息？", nextNodeId="news" },
                    new() { text="你这客栈开了多久了？", nextNodeId="history" },
                }},
            ["news"] = new DialogueNode { id="news", speakerName="赵掌柜", text="矿脉那边在打架。天元宗和青云门的人。已经打了两天了——死了三个散修。你要是去那边——小心点。另外——最近有几个陌生人住进了客栈。他们不像是来挖矿的。像是在找人。",
                nextNodeId="start" },
            ["history"] = new DialogueNode { id="history", speakerName="赵掌柜", text="三十年。见过的人比天元宗长老见过的还多。你是第47个从虚空裂缝出来的人。前46个——后来都消失了。有的被宗门带走了。有的被杀了。有的一觉醒来就不在了。你小心点。",
                nextNodeId="start" },
        };

        Dictionary<string, DialogueNode> BuildDefaultDialogue() => new() {
            ["start"] = new DialogueNode { id="start", speakerName=_npc?.npcName ?? "???", text="你好，旅行者。有什么需要帮助的吗？",
                choices = new() {
                    new() { text="没什么，路过。", nextNodeId=null },
                }},
        };
    }
}
