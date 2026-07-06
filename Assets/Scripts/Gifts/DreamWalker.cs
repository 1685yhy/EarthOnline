using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Gifts
{
    /// <summary>
    /// 梦境行者 —— 可进入他人梦境。获取秘密、影响潜意识。
    /// 代价：在梦境中受伤=现实中受伤。死在梦里=现实中也死了。
    /// </summary>
    public class DreamWalker : GiftBase
    {
        private int _dreamsEntered;
        private string _lastDreamer;

        public override void Initialize(Dictionary<string, object> config) { base.Initialize(config); _dreamsEntered = 0; }
        public override void Activate()
        {
            IsActive = true;
            Debug.Log($"[{GiftName}] 🌙 梦境行者。'每个人的梦都是一扇门。有些门通向秘密。有些门通向噩梦。有些门——通向已经死了的人。'");
        }
        public override void Deactivate() => IsActive = false;
        public override void Upgrade()
        {
            Level++;
            Debug.Log($"[{GiftName}] 梦境探索 Lv.{Level}。");
        }

        public override void UseAbility(string abilityName, Dictionary<string, object> context = null)
        {
            switch (abilityName)
            {
                case "enter_dream": EnterDream(); break;
                case "see_nightmares": SeeNightmares(); break;
                case "lucid_dream": LucidDream(); break;
                case "status": ShowStatus(); break;
            }
        }

        void EnterDream()
        {
            var npcs = Object.FindObjectsOfType<EarthOnline.NPC.NPCBase>();
            if (npcs.Length == 0) { Debug.Log("[梦境] 附近没有沉睡的NPC。"); return; }
            var target = npcs[Random.Range(0, npcs.Length)];
            _dreamsEntered++; _lastDreamer = target.npcName;

            string[] dreamFragments = {
                $"{target.npcName}的梦里——他在逃跑。身后是巨大的黑影。他嘴里不停重复着一个名字。",
                $"{target.npcName}的梦里——他在和一个人说话。那个人的脸是模糊的。他们在争论什么。'不能告诉他'——{target.npcName}说。",
                $"{target.npcName}的梦里——一片虚空。什么都没有。只有心跳声。和一句话：'还差一点。'",
            };
            Debug.Log($"[{GiftName}] 🌙 进入{target.npcName}的梦境...{dreamFragments[Random.Range(0, dreamFragments.Length)]}");

            if (_dreamsEntered >= 5 && StoryProgress == 0) AdvanceStory(1);
        }

        void SeeNightmares()
        {
            if (Level < 2) { Debug.Log("[梦境] 需要Lv.2"); return; }
            Debug.Log("[梦境] 👁️ 看到附近所有沉睡者的噩梦——有一个共同的画面：虚空裂缝。每个人都梦到过它。");
        }

        void LucidDream()
        {
            if (Level < 3) { Debug.Log("[梦境] 需要Lv.3"); return; }
            Debug.Log("[梦境] 🧠 清醒梦境——你可以在他人的梦里留下信息。或改变一些东西。");
        }

        void ShowStatus() => Debug.Log($"[{GiftName}] 入梦{_dreamsEntered}次 最近:{_lastDreamer}");

        public override GiftDisplayInfo GetDisplayInfo() => new()
        {
            Name = GiftName, Type = GiftType, Rarity = Rarity, Level = Level,
            Description = $"入梦{_dreamsEntered}次\n潜入(Lv.1) 噩梦视觉(Lv.2) 清醒梦(Lv.3)",
            Abilities = new List<string> { "enter_dream", "see_nightmares(Lv.2)", "lucid_dream(Lv.3)", "status" },
            StoryHint = _dreamsEntered >= 5 ? "所有NPC的梦里都有虚空裂缝。不是巧合。有人在通过梦境向所有人传递同一个画面。是谁？想干什么？" : "梦不会说谎。但做梦的人会。"
        };

        public override string GetStoryMilestoneDescription(int i) => i switch
        {
            1 => "你看到了所有梦的共同点：虚空裂缝的画面——是由一个源头发出的。追溯源头，你找到了一个人。不是NPC。是一个和你一样的穿越者。他在这里很久了。他在用梦境向全大陆的人发送警告。但他的梦里——虚空已经吞噬了半个灵气大陆。这是未来。还是已经发生的过去？",
            _ => $"Milestone {i}"
        };
    }
}
