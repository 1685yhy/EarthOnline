using UnityEngine;
using EarthOnline.Framework;
using System.Collections.Generic;
using System.Collections;

namespace EarthOnline
{
    /// <summary>
    /// 新手引导 —— 渐进式提示，只在第一次触发时显示。
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private HashSet<string> _shownTips = new HashSet<string>();
        private float _tipCooldown;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            StartCoroutine(WelcomeSequence());
            EventBus.Subscribe("OnItemAdded", OnFirstPickup);
            EventBus.Subscribe("OnNPCInteract", OnFirstNPC);
            EventBus.Subscribe("OnEnemyKilled", OnFirstKill);
            EventBus.Subscribe("OnQuestAccepted", OnFirstQuest);
            EventBus.Subscribe("OnQuestCompleted", OnFirstQuestDone);
            EventBus.Subscribe("OnDayPassed", OnNightFall);
        }

        IEnumerator WelcomeSequence()
        {
            yield return new WaitForSeconds(1f);
            ShowTip("welcome", "🌍 欢迎来到地球Online！你是一名穿越者，在这个世界书写属于自己的传说。");

            yield return new WaitForSeconds(5f);
            ShowTip("controls", "🎮 WASD移动 | 鼠标视角 | 滚轮缩放 | Shift加速 | 空格跳跃");

            yield return new WaitForSeconds(5f);
            ShowTip("interact", "💬 接近NPC按E键对话。村子里有三位居民，和他们聊聊吧！");

            yield return new WaitForSeconds(6f);
            ShowTip("pickup", "✨ 地上发光的光球是可以拾取的物品，走过去就能自动捡起。");

            yield return new WaitForSeconds(6f);
            ShowTip("combat", "⚔️ 小心野外的敌人！左键点击攻击，H键使用回复物品。");

            yield return new WaitForSeconds(6f);
            ShowTip("gifts", "💎 捡到特殊物品可以觉醒金手指能力。村子附近藏着黑铁戒指(SR)和混沌碎片(SSR)——找到它们！");
        }

        void ShowTip(string id, string message)
        {
            if (_shownTips.Contains(id)) return;
            _shownTips.Add(id);
            Debug.Log($"[Tutorial] 💡 {message}");
            EventBus.Publish("OnTutorialTip", new Dictionary<string, object> {
                {"id", id}, {"message", message}
            });
        }

        void OnFirstPickup(Dictionary<string, object> data)
        {
            ShowTip("first_pickup",
                $"🎒 获得 {data["itemName"]}！按P键查看背包，按C键打开制作台。");
        }

        void OnFirstNPC(Dictionary<string, object> data)
        {
            ShowTip("first_npc",
                $"👋 {data["npcName"]}好像有话要说...多次对话可以提升好感度。有的NPC会给你任务！");
        }

        void OnFirstKill(Dictionary<string, object> data)
        {
            ShowTip("first_kill",
                $"💀 击败了{data["enemyName"]}！敌人掉落物品已自动收入背包。敌人每天会刷新。");
        }

        void OnFirstQuest(Dictionary<string, object> data)
        {
            ShowTip("first_quest",
                $"📋 接受任务：{data["title"]}！任务进度在屏幕左上角显示。完成任务获得灵石和修为奖励。");
        }

        void OnFirstQuestDone(Dictionary<string, object> data)
        {
            ShowTip("first_quest_done",
                $"✅ 任务完成：{data["title"]}！+{data["rewardGold"]}灵石 +{data["rewardExp"]}修为。继续和NPC对话获取更多任务！");
        }

        void OnNightFall(Dictionary<string, object> data)
        {
            int day = (int)data["day"];
            ShowTip("boss_warning", "⚠️ 北方紫色漩涡附近有强大守护者(300HP)！建议等级5+，装备武器后再去挑战。死亡会失去20%灵石。");
            if (day == 1)
                ShowTip("first_night", "🌙 一天过去了。敌人在夜间更加活跃，小心！每天开始时会自动存档(F5可随时手动存档)。");
            if (day == 1)
                ShowTip("save_hint", "💾 按F5随时手动存档，按J查看成就。死亡会失去20%灵石但不会丢失物品。");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnItemAdded", OnFirstPickup);
            EventBus.Unsubscribe("OnNPCInteract", OnFirstNPC);
            EventBus.Unsubscribe("OnEnemyKilled", OnFirstKill);
            EventBus.Unsubscribe("OnQuestAccepted", OnFirstQuest);
            EventBus.Unsubscribe("OnQuestCompleted", OnFirstQuestDone);
            EventBus.Unsubscribe("OnDayPassed", OnNightFall);
        }
    }
}
