using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 领地系统 —— 占据区域可获得税收和名声。
    /// 社会逻辑：强者占有资源，弱者缴税。
    /// </summary>
    [System.Serializable]
    public class Territory
    {
        public string id, name, description;
        public string controllingFaction;
        public int taxRate;       // 税率(灵石/天)
        public bool playerOwned;
        public int daysHeld;
    }

    public class TerritorySystem : MonoBehaviour
    {
        public static TerritorySystem Instance { get; private set; }
        public List<Territory> territories = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            CreateTerritories();
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
        }

        void CreateTerritories()
        {
            territories = new List<Territory>
            {
                new() { id="village", name="新手村", description="灵气大陆北域的一个小村庄", controllingFaction="无主", taxRate=10 },
                new() { id="north_mine", name="北部矿脉", description="灵石矿脉，天元宗和青云门争夺中", controllingFaction="天元宗", taxRate=50 },
                new() { id="beast_forest", name="妖兽森林", description="妖兽密集的森林，御兽遗族藏身处", controllingFaction="御兽遗族", taxRate=0 },
                new() { id="void_edge", name="虚空边缘", description="虚空裂缝附近的危险区域", controllingFaction="无主", taxRate=0 },
                new() { id="market_road", name="商路", description="连接各个区域的贸易路线", controllingFaction="商盟", taxRate=30 },
            };
        }

        void OnDayPassed(Dictionary<string, object> data)
        {
            foreach (var t in territories)
            {
                if (t.playerOwned)
                {
                    t.daysHeld++;
                    int income = t.taxRate;
                    PlayerStats.Instance?.AddSpiritStone(income);
                    if (t.daysHeld % 3 == 0) // 每3天报告一次
                        Debug.Log($"[领地] 🏰 {t.name}——税收{income}灵石/天。已持有{t.daysHeld}天。");
                }
            }

            // 派系之间可能发生领土冲突
            foreach (var t in territories)
            {
                if (!t.playerOwned && t.controllingFaction != "无主" && Random.value < 0.1f)
                {
                    Debug.Log($"[领地] ⚔️ {t.controllingFaction}和敌对势力在{t.name}发生了小规模冲突。");
                }
            }
        }

        public bool ClaimTerritory(string territoryId)
        {
            var t = territories.Find(x => x.id == territoryId);
            if (t == null) return false;
            if (t.controllingFaction != "无主" && !t.playerOwned)
            {
                Debug.Log($"[领地] {t.name}目前被{t.controllingFaction}控制。需要击败他们的守卫。");
                return false;
            }
            t.playerOwned = true;
            t.controllingFaction = "玩家";
            Debug.Log($"[领地] 🏰 你占据了{t.name}！每日税收{t.taxRate}灵石。");
            return true;
        }

        void OnDestroy() => EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
    }
}
