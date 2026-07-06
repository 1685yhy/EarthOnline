using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 御兽宗师 —— 可驯服妖兽为己用。
    /// 代价：驯服的妖兽需要喂食灵石。妖兽受伤会降低忠诚度。
    /// </summary>
    public class BeastTamer : GiftBase
    {
        private int _tamedCount;
        private int _currentBeasts;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _tamedCount = 0; _currentBeasts = 0; }
        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🐺 御兽之力觉醒。'妖兽不是敌人——它们只是还没被理解。第一个教你这一点的人——已经死了。被当成'妖修'处决的。他不是妖修。他只是不想杀。'");
        }
        public override void Deactivate() => IsActive = false;

        public override void Upgrade()
        {
            Level++;
            string unlock = Level switch
            {
                2 => "可驯服精英妖兽。",
                3 => "可同时拥有2只妖兽。妖兽获得战斗技能。",
                4 => "可与妖兽合体——获得妖兽的能力。",
                _ => ""
            };
            Debug.Log($"[{GiftName}] 御兽精进 Lv.{Level}。{unlock}");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "tame": TameBeast(); break;
                case "feed": FeedBeast(); break;
                case "call": CallBeast(); break;
                case "status": ShowStatus(); break;
            }
        }

        void TameBeast()
        {
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 100) { Debug.Log("[御兽] 驯服需要100灵石的食物。"); return; }
            if (_currentBeasts >= Level) { Debug.Log($"[御兽] 最多驯服{Level}只妖兽。升级增加上限。"); return; }
            stats.spiritStones -= 100; _tamedCount++; _currentBeasts++;
            Debug.Log($"[{GiftName}] 🐾 驯服成功！(第{_tamedCount}只，当前{_currentBeasts}只)");
            if (_tamedCount >= 5 && StoryProgress == 0) AdvanceStory(1);
        }

        void FeedBeast()
        {
            var stats = PlayerStats.Instance;
            if (stats == null || stats.spiritStones < 20) { Debug.Log("[御兽] 喂食需要20灵石。"); return; }
            stats.spiritStones -= 20;
            Debug.Log($"[{GiftName}] 🍖 喂食妖兽 +忠诚度。");
        }

        void CallBeast()
        {
            if (_currentBeasts <= 0) { Debug.Log("[御兽] 没有驯服的妖兽。"); return; }
            Debug.Log($"[{GiftName}] 📯 召唤妖兽！{_currentBeasts}只妖兽加入战斗。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 御兽 Lv.{Level} | 驯服{_tamedCount}只 | 当前{_currentBeasts}只");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"驯服{_tamedCount}只 当前{_currentBeasts}只\nLv.1驯服 Lv.2精英 Lv.3双兽 Lv.4合体",
            Abilities = new List<string> { "tame", "feed", "call", "status" },
            StoryHint = _tamedCount >= 5 ? "你在妖兽森林遇到了一个老人。他说他是'最后一代御兽师'。他等了你50年。" : "妖兽不是兽——是尚未被理解的智慧。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "老人说：'五十年前，御兽师被天元宗定为妖修——全部处决。我是唯一的幸存者。我躲在这片森林里，等一个能继承这门技艺的人。你是第47个来这片森林的人。前面46个——都选择了杀妖兽取材料。你选择了理解它们。这就够了。'",
            _ => $"Milestone {i}"
        };
    }
}
