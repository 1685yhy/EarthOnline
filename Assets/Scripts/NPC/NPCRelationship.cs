using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC好感度系统 —— 对话次数、送礼、任务完成影响好感度。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCRelationship : MonoBehaviour
    {
        public int affinity = 0;          // 好感度 0-100
        public int talkCount = 0;
        public string relationship = "陌生人"; // 陌生人→熟人→朋友→挚友→?

        [Header("好感度阈值")]
        public int acquaintanceThreshold = 5;   // 5次对话→熟人
        public int friendThreshold = 15;        // 15次→朋友
        public int closeFriendThreshold = 30;   // 30次→挚友

        private NPCBase _npc;
        private string _npcId;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _npcId = _npc.npcId;

            EventBus.Subscribe("OnNPCInteract", OnNPCInteract);
            EventBus.Subscribe("OnGiftGiven", OnGiftGiven);
        }

        void OnNPCInteract(Dictionary<string, object> data)
        {
            string id = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            if (id != _npcId) return;

            talkCount++;
            affinity = Mathf.Min(affinity + 1, 100);
            UpdateRelationship();
        }

        void OnGiftGiven(Dictionary<string, object> data)
        {
            string id = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            if (id != _npcId) return;

            int value = data.ContainsKey("value") ? (int)data["value"] : 5;
            affinity = Mathf.Min(affinity + value, 100);
            UpdateRelationship();
        }

        void UpdateRelationship()
        {
            string oldRel = relationship;

            if (affinity >= closeFriendThreshold) relationship = "挚友";
            else if (affinity >= friendThreshold) relationship = "朋友";
            else if (affinity >= acquaintanceThreshold) relationship = "熟人";
            else relationship = "陌生人";

            if (relationship != oldRel)
            {
                Debug.Log($"[NPC:{_npc.npcName}] 好感度提升! {oldRel} → {relationship} (好感:{affinity})");
                EventBus.Publish("OnRelationshipChanged", new Dictionary<string, object> {
                    {"npcId", _npcId}, {"npcName", _npc.npcName},
                    {"oldRelation", oldRel}, {"newRelation", relationship}, {"affinity", affinity}
                });
            }
        }

        /// <summary>
        /// 根据好感度返回个性化问候。
        /// </summary>
        public string GetPersonalizedGreeting()
        {
            return relationship switch
            {
                "挚友" => $"哟，{_npc.npcName}的好朋友来了！今天又有什么新鲜事？",
                "朋友" => $"嘿，{_npc.npcName}见到你真高兴！(好感:{affinity})",
                "熟人" => $"又见面了。{_npc.npcName}这次有什么需要帮忙的吗？",
                _ => _npc.greetingText
            };
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnNPCInteract", OnNPCInteract);
            EventBus.Unsubscribe("OnGiftGiven", OnGiftGiven);
        }
    }
}
