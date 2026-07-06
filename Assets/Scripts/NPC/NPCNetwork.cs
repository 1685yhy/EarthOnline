using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V2.1 NPC关系网络 —— NPC之间有关系：朋友、师徒、仇人、旧识。
    /// 你帮了A → B（和A关系好的）对你的态度也上升。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCNetwork : MonoBehaviour
    {
        public enum RelationType { Friend, Master, Student, Rival, Lover, Family, Stranger }

        [System.Serializable]
        public class NPCRelation
        {
            public string targetNpcId;
            public RelationType type;
            public string description;
            public int closeness; // 0-100
        }

        public List<NPCRelation> relations = new();
        private NPCBase _npc;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            SetupDefaultRelations();
            EventBus.Subscribe("OnNPCInteract", OnNPCInteracted);
        }

        void SetupDefaultRelations()
        {
            relations = _npc.npcId switch
            {
                "npc_zhang_001" => new() {
                    new() { targetNpcId="npc_wang_001", type=RelationType.Friend, description="王铁柱帮张老修过房顶", closeness=60 },
                    new() { targetNpcId="npc_li_001", type=RelationType.Student, description="李灵儿向张老请教过炼丹", closeness=50 },
                },
                "npc_wang_001" => new() {
                    new() { targetNpcId="npc_zhang_001", type=RelationType.Friend, description="张老是王铁柱最敬重的长辈", closeness=60 },
                    new() { targetNpcId="npc_li_001", type=RelationType.Rival, description="铁匠铺和药铺争过一块地", closeness=-20 },
                },
                "npc_li_001" => new() {
                    new() { targetNpcId="npc_zhang_001", type=RelationType.Master, description="李灵儿视张老为半个师父", closeness=70 },
                    new() { targetNpcId="npc_chen_001", type=RelationType.Friend, description="两人经常交换药材情报", closeness=40 },
                },
                "npc_chen_001" => new() {
                    new() { targetNpcId="npc_zhao_001", type=RelationType.Friend, description="陈半仙每次来都住赵掌柜的客栈", closeness=50 },
                },
                "npc_zhao_001" => new() {
                    new() { targetNpcId="npc_chen_001", type=RelationType.Friend, description="最老的客人——住了三十年", closeness=50 },
                    new() { targetNpcId="npc_zhang_001", type=RelationType.Friend, description="赵掌柜帮张老瞒着身份", closeness=70 },
                },
                _ => new()
            };
        }

        /// <summary>获取此NPC和另一个NPC的关系</summary>
        public NPCRelation GetRelation(string otherNpcId)
        {
            return relations.Find(r => r.targetNpcId == otherNpcId);
        }

        void OnNPCInteracted(Dictionary<string, object> data)
        {
            string interactedNpcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            // 你帮了NPC A → NPC A的朋友知道了 → 态度上升
            var rel = GetRelation(interactedNpcId);
            if (rel != null && rel.closeness > 30)
            {
                var mem = GetComponent<NPCMemory>();
                if (mem != null)
                {
                    mem.Remember(MemoryType.Helped,
                        $"{_npc.npcName}听说你帮了{interactedNpcId}（{rel.description}）", 2);
                }
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnNPCInteracted);
        }
    }
}
