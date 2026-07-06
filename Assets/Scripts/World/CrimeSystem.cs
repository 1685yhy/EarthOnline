using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline
{
    /// <summary>
    /// V2.1 罪恶系统 —— 社会运行逻辑：攻击NPC→通缉→守卫追捕→缴罚款或坐牢。
    /// 不是"你打了人什么事没有"——是"现实社会怎么处理，这个世界就怎么处理"。
    /// </summary>
    public enum CrimeLevel { Innocent, Suspect, Wanted, Hunted, PublicEnemy }

    public class CrimeSystem : MonoBehaviour
    {
        public static CrimeSystem Instance { get; private set; }

        public CrimeLevel currentLevel = CrimeLevel.Innocent;
        public int bounty;                      // 悬赏金额（灵石）
        public int crimesCommitted;             // 犯罪次数
        public List<string> crimeRecord = new(); // 犯罪记录
        public float wantedTimer;               // 通缉倒计时（秒）

        private float _guardCheckInterval = 30f;
        private float _nextGuardCheck;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _nextGuardCheck = Time.time + _guardCheckInterval;
        }

        void Update()
        {
            // 通缉倒计时——时间久了没人报案，悬赏降低
            if (bounty > 0 && currentLevel < CrimeLevel.Hunted)
            {
                wantedTimer += Time.deltaTime;
                if (wantedTimer > 300f) // 5分钟无新犯罪→降温
                {
                    bounty = Mathf.Max(0, bounty - 10);
                    wantedTimer = 0;
                    UpdateCrimeLevel();
                    if (bounty > 0) Debug.Log($"[通缉] ⏳ 热度降低。悬赏:{bounty}灵石。");
                }
            }

            // 守卫定期巡逻检查
            if (Time.time >= _nextGuardCheck && currentLevel >= CrimeLevel.Wanted)
            {
                _nextGuardCheck = Time.time + _guardCheckInterval;
                GuardPatrol();
            }
        }

        /// <summary>攻击NPC——当场被通缉</summary>
        public void ReportAssault(string npcName, Vector3 location)
        {
            string record = $"攻击{npcName}（{location}）";
            crimeRecord.Add(record);
            crimesCommitted++;
            bounty += 50;
            wantedTimer = 0;
            UpdateCrimeLevel();

            Debug.Log($"🚨 [通缉] {record}！悬赏+50灵石。当前悬赏:{bounty}灵石。等级:{currentLevel}");

            // 附近NPC目睹→态度变化
            NotifyNearbyNPCs(location, MemoryType.Harmed, $"目睹玩家攻击{npcName}", -10);
        }

        /// <summary>击杀NPC——重罪</summary>
        public void ReportMurder(string npcName, Vector3 location)
        {
            string record = $"杀害{npcName}（{location}）";
            crimeRecord.Add(record);
            crimesCommitted++;
            bounty += 200;
            wantedTimer = 0;
            UpdateCrimeLevel();

            Debug.Log($"💀 [通缉] {record}！！悬赏+200灵石。当前悬赏:{bounty}灵石。等级:{currentLevel}");
            Debug.Log($"[通缉] ⚠️ 守卫正在追捕你！缴罚款可清除悬赏。按L查看状态。");

            NotifyNearbyNPCs(location, MemoryType.Betrayed, $"目睹玩家杀害{npcName}", -30);
        }

        /// <summary>偷窃</summary>
        public void ReportTheft(string victimName, string itemName, Vector3 location)
        {
            string record = $"偷窃{victimName}的{itemName}";
            crimeRecord.Add(record);
            crimesCommitted++;
            bounty += 30;
            wantedTimer = 0;
            UpdateCrimeLevel();

            Debug.Log($"🕵️ [通缉] {record}。悬赏+30灵石。");
            NotifyNearbyNPCs(location, MemoryType.Stole, $"目睹玩家偷窃{victimName}", -5);
        }

        void UpdateCrimeLevel()
        {
            currentLevel = bounty switch
            {
                0 => CrimeLevel.Innocent,
                <= 100 => CrimeLevel.Suspect,
                <= 300 => CrimeLevel.Wanted,
                <= 800 => CrimeLevel.Hunted,
                _ => CrimeLevel.PublicEnemy
            };
        }

        /// <summary>缴纳罚款清除悬赏</summary>
        public bool PayBounty()
        {
            if (bounty <= 0) { Debug.Log("[通缉] 你没有悬赏。"); return true; }
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < bounty)
            {
                Debug.Log($"[通缉] 灵石不足。需要{bounty}灵石清除悬赏。你只有{stats?.spiritStones ?? 0}灵石。");
                return false;
            }

            stats.spiritStones -= bounty;
            Debug.Log($"[通缉] 💰 缴纳{bounty}灵石罚款。悬赏已清除。");
            bounty = 0;
            currentLevel = CrimeLevel.Innocent;
            crimeRecord.Clear();
            return true;
        }

        void GuardPatrol()
        {
            if (bounty <= 0) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Debug.Log($"[通缉] 👮 守卫巡逻中...你被发现了！悬赏{bounty}灵石。");
            Debug.Log($"[通缉] 按L查看状态。缴纳{bounty}灵石可清除悬赏。");

            // 高悬赏→守卫主动攻击
            if (currentLevel >= CrimeLevel.Hunted)
            {
                var stats = PlayerStats.Instance;
                if (stats != null)
                {
                    int guardDamage = 20 + bounty / 10;
                    stats.TakeDamage(guardDamage);
                    Debug.Log($"[通缉] ⚔️ 守卫对你发动攻击！-{guardDamage}HP。立即缴纳罚款！");
                }
            }
        }

        void NotifyNearbyNPCs(Vector3 location, MemoryType memType, string description, int weight)
        {
            var allNpcs = Object.FindObjectsOfType<NPCBase>();
            foreach (var npc in allNpcs)
            {
                float dist = Vector3.Distance(npc.transform.position, location);
                if (dist < 30f) // 30m内的NPC都是目击者
                {
                    var mem = npc.GetComponent<NPCMemory>();
                    mem?.Remember(memType, description, weight);
                }
            }
        }

        public string GetStatusText()
        {
            string levelText = currentLevel switch
            {
                CrimeLevel.Innocent => "清白",
                CrimeLevel.Suspect => "可疑",
                CrimeLevel.Wanted => "被通缉",
                CrimeLevel.Hunted => "被追捕",
                CrimeLevel.PublicEnemy => "公敌",
                _ => "未知"
            };
            return $"🚨 {levelText} | 悬赏:{bounty}灵石 | 犯罪{crimesCommitted}次";
        }
    }
}
