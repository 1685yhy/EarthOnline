using System;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 签到系统金手指 —— V0.1 第一个可玩的金手指。
    /// 每天签到获得奖励，连续签到奖励递增。
    /// 故事钩子：一个来自未知文明的"新手引导系统"，但它的前任宿主全部死亡...
    /// </summary>
    public class SignInSystem : GiftBase
    {
        [System.Serializable]
        public class Config
        {
            public int[] dailyRewards = { 10, 20, 30, 50, 80, 120, 200 };
            public float signInCooldownHours = 24f;
        }

        private Config _config;
        private int _consecutiveDays;
        private DateTime _lastSignIn;

        public override void Initialize(Dictionary<string, object> config)
        {
            base.Initialize(config);
            _config = new Config();

            if (config.ContainsKey("dailyRewards"))
            {
                var rewards = config["dailyRewards"];
                if (rewards is System.Collections.IList list)
                {
                    _config.dailyRewards = new int[list.Count];
                    for (int i = 0; i < list.Count; i++)
                        _config.dailyRewards[i] = Convert.ToInt32(list[i]);
                }
            }
        }

        public override void Activate()
        {
            IsActive = true;
            // 加载存档的签到数据
            _consecutiveDays = PlayerPrefs.GetInt($"SignIn_{GiftId}_Days", 0);
            string lastSignStr = PlayerPrefs.GetString($"SignIn_{GiftId}_Last", "");
            if (!string.IsNullOrEmpty(lastSignStr))
                DateTime.TryParse(lastSignStr, out _lastSignIn);

            Debug.Log($"[{GiftName}] 激活！连续签到 {_consecutiveDays} 天");
            EventBus.Publish("OnSignInActivated", new Dictionary<string, object>
            {
                {"giftId", GiftId},
                {"consecutiveDays", _consecutiveDays}
            });
        }

        public override void Deactivate()
        {
            IsActive = false;
        }

        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 升级到 Lv.{Level} —— 奖励倍率 x{1 + Level * 0.5f}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            if (abilityName == "sign_in")
                DoSignIn();
            else if (abilityName == "get_status")
                GetStatus();
            else
                Debug.LogWarning($"[{GiftName}] Unknown ability: {abilityName}");
        }

        public void DoSignIn()
        {
            DateTime now = DateTime.Now;
            if ((now - _lastSignIn).TotalHours < _config.signInCooldownHours)
            {
                var remaining = TimeSpan.FromHours(_config.signInCooldownHours) - (now - _lastSignIn);
                Debug.Log($"[{GiftName}] 签到冷却中，剩余 {remaining.Hours}h{remaining.Minutes}m");
                EventBus.Publish("OnSignInCooldown", new Dictionary<string, object>
                {
                    {"remainingHours", remaining.TotalHours}
                });
                return;
            }

            // 检查是否超过48小时（断签）
            if ((now - _lastSignIn).TotalHours > 48)
                _consecutiveDays = 0;

            _consecutiveDays++;
            int dayIndex = Mathf.Min(_consecutiveDays - 1, _config.dailyRewards.Length - 1);
            int reward = _config.dailyRewards[dayIndex];
            int bonus = Mathf.FloorToInt(reward * (Level - 1) * 0.5f);

            // 新手经济保护：前7天签到额外+20/天（经济平衡文档V1 6.2.1）
            int newbieBonus = NewbieProtection.GetSignInBonus();
            int totalReward = reward + bonus + newbieBonus;

            _lastSignIn = now;

            // 保存
            PlayerPrefs.SetInt($"SignIn_{GiftId}_Days", _consecutiveDays);
            PlayerPrefs.SetString($"SignIn_{GiftId}_Last", now.ToString("O"));

            Debug.Log($"[{GiftName}] ✓ 签到第 {_consecutiveDays} 天！获得 {totalReward} 灵石 (基础{reward} + 等级加成{bonus}" +
                (newbieBonus > 0 ? $" + 新手加成{newbieBonus}" : "") + $")");

            EventBus.Publish("OnSignInComplete", new Dictionary<string, object>
            {
                {"day", _consecutiveDays},
                {"reward", totalReward},
                {"baseReward", reward},
                {"levelBonus", bonus},
                {"newbieBonus", newbieBonus}
            });

            // 第7天触发故事里程碑
            if (_consecutiveDays == 7)
                AdvanceStory(1);
        }

        public void GetStatus()
        {
            DateTime now = DateTime.Now;
            bool canSign = (now - _lastSignIn).TotalHours >= _config.signInCooldownHours;
            int nextDayIndex = Mathf.Min(_consecutiveDays, _config.dailyRewards.Length - 1);
            int nextReward = _config.dailyRewards[nextDayIndex];

            Debug.Log($"[{GiftName}] 连续{_consecutiveDays}天 | " +
                $"下次奖励:{nextReward}灵石 | " +
                $"状态:{(canSign ? "可签到" : "冷却中")} | Lv.{Level}");
        }

        public override GiftDisplayInfo GetDisplayInfo()
        {
            return new GiftDisplayInfo
            {
                Name = GiftName,
                Type = GiftType,
                Rarity = Rarity,
                Level = Level,
                Description = $"每日签到获得灵石奖励。连续签到 {_consecutiveDays} 天。\n" +
                    $"下次奖励: {_config.dailyRewards[Mathf.Min(_consecutiveDays, _config.dailyRewards.Length - 1)]} 灵石",
                Abilities = new List<string> { "sign_in (签到)", "get_status (查看状态)" },
                StoryHint = _consecutiveDays >= 7
                    ? "你发现系统日志里有一段被删除的记录..."
                    : "这个系统似乎隐瞒了什么..."
            };
        }

        public override string GetStoryMilestoneDescription(int milestoneIndex)
        {
            return milestoneIndex switch
            {
                1 => "【第7天签到】系统弹出一条红色警告：'检测到宿主存活超过7天。历史数据：前7任宿主平均存活时间：3天。恭喜你打破了记录。系统已解锁隐藏功能。'",
                2 => "【第30天签到】系统的制造者留下了一段全息影像：'如果你能看到这条信息，说明你活过了30天。我们失败了，但也许你能成功。这是我们文明最后的礼物...'",
                _ => $"Milestone {milestoneIndex} (TODO)"
            };
        }
    }
}
