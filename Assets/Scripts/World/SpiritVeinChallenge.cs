using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 灵脉挑战系统 —— 灵脉不是站上去就永远是你的。
    /// 有持有成本（每日消耗灵石）+可被挑战（NPC或其他玩家）+所有权衰减。
    /// </summary>
    [RequireComponent(typeof(SpiritVein))]
    public class SpiritVeinChallenge : MonoBehaviour
    {
        public string ownerName = "无主";     // 当前占有者
        public int dailyCost = 20;           // 每日持有成本（灵石）
        public float challengeCooldown = 120f; // 挑战冷却
        public bool isOccupied => ownerName != "无主";

        private SpiritVein _vein;
        private float _lastChallengeTime;
        private int _daysHeld;
        private Transform _player;

        void Start()
        {
            _vein = GetComponent<SpiritVein>();
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            EventBus.Subscribe("OnDayPassed", OnDayPassed);
        }

        void Update()
        {
            if (_player == null || Time.time - _lastChallengeTime < challengeCooldown) return;

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(_player.position.x, 0, _player.position.z));

            if (dist <= _vein.radius && !isOccupied)
            {
                // 无主权灵脉——按G占领
                if (Input.GetKeyDown(KeyCode.G))
                {
                    ClaimVein();
                }
            }
            else if (dist <= _vein.radius && isOccupied && ownerName != "玩家")
            {
                // NPC占领的灵脉——按G挑战
                if (Input.GetKeyDown(KeyCode.G))
                {
                    ChallengeVein();
                }
            }
        }

        void ClaimVein()
        {
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < dailyCost)
            {
                Debug.Log($"[灵脉] 需要至少{dailyCost}灵石来占据灵脉（每日维护费）。");
                return;
            }

            ownerName = "玩家";
            _daysHeld = 0;
            _lastChallengeTime = Time.time;
            stats.spiritStones -= dailyCost;

            Debug.Log($"🔷 [灵脉] 你占据了{_vein.veinName}！每日维护费{dailyCost}灵石。倍率x{_vein.cultivationMultiplier}。");
            Debug.Log($"[灵脉] ⚠️ NPC可能会来挑战你的灵脉。每守住一天，灵脉价值+5%。");
        }

        void ChallengeVein()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            // NPC守卫实力=灵脉倍率×50
            int guardPower = Mathf.RoundToInt(_vein.cultivationMultiplier * 50);

            Debug.Log($"⚔️ [灵脉] 挑战{_vein.veinName}的守卫！实力:{guardPower}。");

            // 简单战力判定
            int playerPower = stats.playerLevel * 10 + stats.cultivation / 10;
            if (playerPower + Random.Range(0, 30) > guardPower)
            {
                // 胜利
                ownerName = "玩家";
                _daysHeld = 0;
                _lastChallengeTime = Time.time;
                stats.spiritStones -= dailyCost;
                Debug.Log($"🔷 [灵脉] 挑战成功！{_vein.veinName}现在是你的了。");
                stats.AddCultivation(20);
            }
            else
            {
                Debug.Log($"[灵脉] 挑战失败！需要更强的实力。当前战力:{playerPower}，守卫:{guardPower}。");
                stats.TakeDamage(10);
            }
        }

        void OnDayPassed(Dictionary<string, object> data)
        {
            if (!isOccupied || ownerName != "玩家") return;

            var stats = PlayerStats.Instance;
            if (stats == null) return;

            // 每日维护费
            if (stats.spiritStones >= dailyCost)
            {
                stats.spiritStones -= dailyCost;
                _daysHeld++;
                Debug.Log($"[灵脉] 📅 维护费{dailyCost}灵石。持有{_daysHeld}天。");
            }
            else
            {
                // 维护费不够——自动失去
                ownerName = "无主";
                Debug.Log($"[灵脉] 💸 灵石不足以支付维护费。{_vein.veinName}被释放了。");
                return;
            }

            // NPC挑战概率=10%+每天+5%（持有越久越容易被挑战）
            float challengeChance = 0.1f + _daysHeld * 0.05f;
            if (Random.value < challengeChance)
            {
                Debug.Log($"[灵脉] ⚡ 有修士来挑战你的{_vein.veinName}！");
                int challengerPower = 30 + _daysHeld * 10;
                int playerPower = stats.playerLevel * 10 + stats.cultivation / 10;

                if (playerPower > challengerPower)
                {
                    Debug.Log($"[灵脉] 🛡️ 守住了！击退挑战者。");
                    stats.AddCultivation(30);
                }
                else
                {
                    ownerName = $"散修(已占领{_daysHeld}天后被夺)";
                    Debug.Log($"[灵脉] 💔 {_vein.veinName}被夺走了！需要重新挑战。");
                }
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnDayPassed", OnDayPassed);
        }
    }
}
