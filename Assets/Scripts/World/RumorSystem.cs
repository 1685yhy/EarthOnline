using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 流言系统 —— 世界中的信息通过NPC之间传播。
    /// 赵掌柜是主要信息来源，但其他NPC也会偶尔透露消息。
    /// </summary>
    public class RumorSystem : MonoBehaviour
    {
        public static RumorSystem Instance { get; private set; }

        private List<Rumor> _activeRumors = new();
        private List<Rumor> _rumorPool = new();
        private float _nextRumorTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetupRumorPool();
            _nextRumorTime = Time.time + 180f; // 每3分钟生成新流言
            EventBus.Subscribe("OnDayPassed", OnNewDay);
        }

        void SetupRumorPool()
        {
            _rumorPool = new List<Rumor> {
                new() { title = "矿脉之争", content = "听说天元宗和青云门在抢西边新发现的灵石矿。两边都已经派了人过去。散修们在赌哪边会赢。", source = "赵掌柜" },
                new() { title = "失踪的修士", content = "最近三个月，有七个散修在北方山区失踪了。没有任何打斗痕迹——人就是凭空消失了。", source = "李灵儿" },
                new() { title = "虚空裂缝扩大", content = "北边的虚空裂缝比以前大了两倍。有修士说在裂缝附近看到了...不是这个世界的东西。", source = "张老" },
                new() { title = "天元宗内讧", content = "天元宗内门弟子之间出现了分裂。有人说副宗主的'走火入魔'不是意外——是宗主下的手。", source = "王铁柱" },
                new() { title = "古墓出现", content = "陈半仙说一座'活着的墓'三个月后会出现。已经有三个势力在准备人手了——都想抢在别人前面进去。", source = "陈半仙" },
                new() { title = "妖兽异动", content = "森林里的妖兽最近异常活跃。老猎人说是地震的前兆。但修士们说——地震不会让妖兽的眼睛变成紫色。", source = "赵掌柜" },
                new() { title = "雨夜来客", content = "每个下雨的夜晚，客栈都会来一个戴着兜帽的人。他不说话，不吃饭，只是坐在角落里看着门口——像是在等什么人。", source = "赵掌柜" },
            };
        }

        void Update()
        {
            if (Time.time >= _nextRumorTime)
            {
                _nextRumorTime = Time.time + Random.Range(180f, 360f);
                SpreadNewRumor();
            }
        }

        void SpreadNewRumor()
        {
            var available = _rumorPool.FindAll(r => !_activeRumors.Contains(r));
            if (available.Count == 0) { _activeRumors.Clear(); available = _rumorPool; }

            var rumor = available[Random.Range(0, available.Count)];
            _activeRumors.Add(rumor);

            Debug.Log($"📢 [流言] {rumor.source}说：'{rumor.title}'——{rumor.content}");

            EventBus.Publish("OnNewRumor", new Dictionary<string, object> {
                {"title", rumor.title}, {"content", rumor.content}, {"source", rumor.source}
            });
        }

        void OnNewDay(Dictionary<string, object> data)
        {
            // 每天可能更新流言池
            if (Random.value < 0.3f && _activeRumors.Count > 0)
                _activeRumors.RemoveAt(0);
        }

        /// <summary>获取当前活跃的流言列表</summary>
        public List<Rumor> GetActiveRumors() => new(_activeRumors);

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnDayPassed", OnNewDay);
        }
    }

    public class Rumor
    {
        public string title, content, source;
    }
}
