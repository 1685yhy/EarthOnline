using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 金手指基类。所有金手指（系统面板/老爷爷/血脉等）继承此类。
    /// 每个金手指包含：能力层（玩法）+ 故事层（叙事钩子）+ 成长层（可进化）。
    /// </summary>
    public abstract class GiftBase
    {
        public string GiftId { get; protected set; }
        public string GiftName { get; protected set; }
        public string GiftType { get; protected set; }
        public string Rarity { get; protected set; }
        public int Level { get; protected set; } = 1;
        public bool IsActive { get; protected set; } = false;

        public string StoryOrigin { get; protected set; }
        public string StoryMystery { get; protected set; }
        public int StoryProgress { get; protected set; } = 0;

        public virtual void Initialize(Dictionary<string, object> config)
        {
            GiftId = config.ContainsKey("id") ? config["id"].ToString() : "unknown";
            GiftName = config.ContainsKey("name") ? config["name"].ToString() : "未命名";
            GiftType = config.ContainsKey("type") ? config["type"].ToString() : "Unknown";
            Rarity = config.ContainsKey("rarity") ? config["rarity"].ToString() : "N";
            StoryOrigin = config.ContainsKey("storyOrigin") ? config["storyOrigin"].ToString() : "";
            StoryMystery = config.ContainsKey("storyMystery") ? config["storyMystery"].ToString() : "";
        }

        public abstract void Activate();
        public abstract void Deactivate();
        public abstract void Upgrade();
        public abstract void UseAbility(string abilityName, Dictionary<string, object> context = null);
        public abstract GiftDisplayInfo GetDisplayInfo();

        public void AdvanceStory(int milestoneIndex)
        {
            StoryProgress = milestoneIndex;
            EventBus.Publish("OnGiftStoryAdvanced", new Dictionary<string, object>
            {
                {"giftId", GiftId}, {"milestone", milestoneIndex}
            });
            Debug.Log($"[Gift:{GiftName}] Story milestone {milestoneIndex} reached");
        }

        public abstract string GetStoryMilestoneDescription(int milestoneIndex);
    }

    [System.Serializable]
    public class GiftDisplayInfo
    {
        public string Name;
        public string Type;
        public string Rarity;
        public int Level;
        public string Description;
        public List<string> Abilities;
        public string StoryHint;
    }
}
