using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC秘密系统 —— 每个NPC有隐藏的秘密，基于互动深度逐步揭示。
    /// 没有"好感度75/100"——只有"他今天主动跟你说了什么"。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCSecret : MonoBehaviour
    {
        [System.Serializable]
        public class Secret
        {
            public int revealThreshold; // 互动次数阈值
            public string hint;         // 未揭示时的暗示
            public string revelation;   // 揭示后的对话
            public bool revealed;
        }

        public Secret[] secrets;
        private NPCBase _npc;
        private NPCRelationship _rel;
        private int _interactionCount;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _rel = GetComponent<NPCRelationship>();
        }

        void Update()
        {
            if (_rel == null) return;
            int newCount = _rel.talkCount;
            if (newCount != _interactionCount)
            {
                _interactionCount = newCount;
                CheckSecrets();
            }
        }

        void CheckSecrets()
        {
            foreach (var s in secrets)
            {
                if (!s.revealed && _interactionCount >= s.revealThreshold)
                {
                    s.revealed = true;
                    Debug.Log($"[NPC:{_npc.npcName}] 💬 \"{s.revelation}\"");
                }
            }
        }

        /// <summary>
        /// 获取当前未揭示的最高秘密提示（用于NPC的随机台词）
        /// </summary>
        public string GetHint()
        {
            foreach (var s in secrets)
                if (!s.revealed && _interactionCount >= s.revealThreshold - 2)
                    return s.hint;
            return null;
        }
    }
}
