using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 宗门派系系统 —— 灵气大陆的社会集体。
    /// 每个派系有态度、有冲突、有利益。你在一个派系的声望影响你在另一个派系的待遇。
    /// </summary>
    [System.Serializable]
    public class Faction
    {
        public string id, name, description;
        public int playerReputation;       // -100到100
        public List<string> allies = new();     // 同盟
        public List<string> enemies = new();    // 敌对
        public string territory;           // 势力范围描述

        public string ReputationTitle => playerReputation switch
        {
            >= 80 => "座上宾",
            >= 40 => "友好",
            >= 10 => "中立",
            >= -10 => "冷淡",
            >= -40 => "敌视",
            >= -80 => "仇敌",
            _ => "不共戴天"
        };
    }

    public class FactionSystem : MonoBehaviour
    {
        public static FactionSystem Instance { get; private set; }
        public List<Faction> factions = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            CreateFactions();
            // 你做了一件事→影响所有相关派系
            EventBus.Subscribe("OnNPCInteract", OnInteraction);
            EventBus.Subscribe("OnEnemyKilled", OnEnemyKilled);
            EventBus.Subscribe("OnCrimeCommitted", OnCrime);
        }

        void CreateFactions()
        {
            factions = new List<Faction>
            {
                new Faction {
                    id="tianyuan", name="天元宗", description="正道第一宗门。表面光鲜——暗地用人血炼丹。",
                    allies=new(){"qingyun"}, enemies=new(){"rogue","beast_tamers"},
                    territory="北域·天元山", playerReputation=0
                },
                new Faction {
                    id="qingyun", name="青云门", description="第二大宗门。和天元宗争夺矿脉。相对干净——但也不是什么好人。",
                    allies=new(){"tianyuan"}, enemies=new(){"rogue"},
                    territory="东域·青云山脉", playerReputation=0
                },
                new Faction {
                    id="rogue", name="散修联盟", description="无门无派的自由修士。成员复杂——有隐世高手，也有亡命之徒。",
                    allies=new(){}, enemies=new(){"tianyuan","qingyun"},
                    territory="全域·坊市和野外", playerReputation=10
                },
                new Faction {
                    id="merchant_guild", name="商盟", description="控制灵气大陆贸易路线的商人组织。陈半仙和赵掌柜都是成员。",
                    allies=new(){"rogue"}, enemies=new(){},
                    territory="全域·坊市和商路", playerReputation=10
                },
                new Faction {
                    id="beast_tamers", name="御兽遗族", description="五十年前被天元宗定为'妖修'的神秘群体。最后的传人躲在妖兽森林。",
                    allies=new(){}, enemies=new(){"tianyuan"},
                    territory="妖兽森林·隐秘据点", playerReputation=0
                },
            };
        }

        void OnInteraction(Dictionary<string, object> data)
        {
            string npcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            // 和NPC对话→相关派系轻微好感
            var mapping = new Dictionary<string, string> {
                {"npc_zhang_001", "rogue"}, {"npc_wang_001", "rogue"},
                {"npc_li_001", "rogue"}, {"npc_chen_001", "merchant_guild"},
                {"npc_zhao_001", "merchant_guild"},
            };
            if (mapping.ContainsKey(npcId))
                ModifyReputation(mapping[npcId], 1);
        }

        void OnEnemyKilled(Dictionary<string, object> data)
        {
            // 杀妖兽→散修联盟好感上升，天元宗冷淡
            ModifyReputation("rogue", 1);
        }

        void OnCrime(Dictionary<string, object> data)
        {
            // 犯罪→所有正道派系好感下降
            ModifyReputation("tianyuan", -3);
            ModifyReputation("qingyun", -3);
        }

        public void ModifyReputation(string factionId, int amount)
        {
            var f = factions.Find(x => x.id == factionId);
            if (f == null) return;
            f.playerReputation = Mathf.Clamp(f.playerReputation + amount, -100, 100);

            if (Mathf.Abs(amount) >= 5)
            {
                Debug.Log($"[派系] {f.name}对你的态度变为:{f.ReputationTitle}({f.playerReputation})");
                // 同盟派系也跟着变化
                foreach (var allyId in f.allies)
                {
                    var ally = factions.Find(x => x.id == allyId);
                    if (ally != null) ally.playerReputation = Mathf.Clamp(ally.playerReputation + amount/2, -100, 100);
                }
            }
        }

        public Faction GetFaction(string id) => factions.Find(f => f.id == id);
    }
}
