using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.NPC
{
    /// <summary>
    /// V2.2 NPC羁绊系统 —— 异性可结为道侣，同性可结为兄弟/姐妹。
    /// 结为羁绊后获得战斗加成和特殊对话。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCBond : MonoBehaviour
    {
        public enum BondType { None, Dao侣, 兄弟, 姐妹, 师徒 }

        public BondType currentBond = BondType.None;
        public string bondPartnerName;
        public int bondLevel; // 1-10
        public bool canPropose => currentBond == BondType.None;

        private NPCBase _npc;
        private NPCMemory _mem;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _mem = GetComponent<NPCMemory>();
        }

        void Update()
        {
            // 玩家靠近NPC时→如果有足够好感→可以求婚/结拜
            if (!canPropose || _mem == null || _npc == null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < 3f && _mem.NetAttitude >= 70 && Input.GetKeyDown(KeyCode.Y))
            {
                ProposeBond();
            }
        }

        void ProposeBond()
        {
            Debug.Log($"💍 [{_npc.npcName}] 你们的关系已经很深了...");
            Debug.Log($"   按1=求婚(道侣) | 按2=结拜(兄弟/姐妹) | 其他键=取消");

            StartCoroutine(WaitForBondChoice());
        }

        System.Collections.IEnumerator WaitForBondChoice()
        {
            float deadline = Time.time + 5f;
            while (Time.time < deadline)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    currentBond = BondType.Dao侣;
                    bondPartnerName = "玩家";
                    Debug.Log($"💒 [{_npc.npcName}] 你们结为道侣！从此生死与共。战斗时道侣会助战。");
                    ReputationSystem.Instance?.AddFame(20, $"与{_npc.npcName}结为道侣");
                    yield break;
                }
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    currentBond = BondType.兄弟;
                    bondPartnerName = "玩家";
                    Debug.Log($"🤝 [{_npc.npcName}] 你们义结金兰！从此有福同享，有难同当。");
                    ReputationSystem.Instance?.AddFame(15, $"与{_npc.npcName}义结金兰");
                    yield break;
                }
                if (Input.anyKeyDown) { Debug.Log("[羁绊] 取消。"); yield break; }
                yield return null;
            }
        }
    }
}
