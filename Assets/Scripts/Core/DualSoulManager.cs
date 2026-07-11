using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// 双魂一体核心系统 —— 信任度+觉醒度双轨并进。
    /// 穿越者和原主人(念安)共享一具身体。你出脑，他出力。
    /// </summary>
    public class DualSoulManager : MonoBehaviour
    {
        public static DualSoulManager Instance { get; private set; }
        public bool IsActive => OriginManager.ChosenOrigin == PlayerOrigin.DualSoul;

        [Header("双魂状态")]
        public int trust = 5;          // 信任度 0-100
        public int awakening = 0;      // 觉醒度 0-100
        public int willpower = 100;    // 意志力
        public int soulCracks = 0;     // 灵魂裂痕层数
        public float syncRate => (trust + awakening) * 0.5f; // 同步率

        [Header("原主人信息")]
        public string hostName = "念安";
        public string hostRealm = "元婴期";

        [Header("复仇名单")]
        public List<RevengeEntry> revengeList = new();

        [System.Serializable]
        public class RevengeEntry
        {
            public string targetName;
            public string crime;
            public bool activated;   // 原主人觉醒后激活
            public bool avenged;     // 已清算
            public bool manual;      // 穿越者手动标记
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (!IsActive) return;
            Debug.Log($"[双魂] 🌓 双魂一体激活——你({PlayerStats.Instance?.playerLevel ?? 1}级) + {hostName}({hostRealm})");
            Debug.Log($"[双魂] 信任:{trust} | 觉醒:{awakening} | 意志:{willpower}");
        }

        void Update()
        {
            if (!IsActive) return;
            // 按T和念安对话
            if (Input.GetKeyDown(KeyCode.T) && trust >= 16)
            {
                TalkToNianAn();
            }
        }

        /// <summary>增加信任度</summary>
        public void AddTrust(int amount, string reason)
        {
            trust = Mathf.Clamp(trust + amount, 0, 100);
            Debug.Log($"[双魂·信任] +{amount} ({reason}) 信任:{trust}");

            if (trust >= 50 && awakening >= 50)
                Debug.Log($"[双魂] ✨ 最佳共鸣——信任和觉醒同步——双魂合一可触发！");
            else if (trust >= 50)
                Debug.Log($"[双魂] 🤝 念安开始真正信任你了。");
            else if (trust >= 16)
                Debug.Log($"[双魂] 💬 念安愿意和你对话了。");
        }

        /// <summary>增加觉醒度</summary>
        public void AddAwakening(int amount, string reason)
        {
            awakening = Mathf.Clamp(awakening + amount, 0, 100);
            Debug.Log($"[双魂·觉醒] +{amount} ({reason}) 觉醒:{awakening}");

            if (awakening >= 100)
            {
                Debug.Log($"[双魂] 🌟 念安完全觉醒了！全服公告：道胎觉醒——天元宗的谎言被揭穿。");
                EventBus.Publish("OnDualSoulAwakened");
            }
            else if (awakening >= 61)
                Debug.Log($"[双魂] 💔 念安正在经历心理破碎——他在重建世界观。");
            else if (awakening >= 21)
                Debug.Log($"[双魂] 🔍 念安开始怀疑师父的话...");
        }

        /// <summary>对话念安</summary>
        void TalkToNianAn()
        {
            string mood = awakening switch { >= 61 => "坚定", >= 21 => "困惑", _ => "懦弱" };
            string[] lines = awakening switch
            {
                >= 80 => new[] { "谢谢你...让我看到了真相。", "我不会再让别人摆布我了——包括你。但我们——可以一起。", "那个秘境...我感觉到了什么。不是恐惧——是归属感。" },
                >= 50 => new[] { "原来...师父一直在骗我。二十年——全部是假的。", "我不知道该信谁——但你说的话——至少验证过的。", "小师妹...她为什么要这样对我？我从来没有害过她。" },
                >= 21 => new[] { "你说的...有些东西我看到了——但我不敢相信。", "如果师父真的在害我...那我这二十年算什么？", "天机长老给我看了一些东西...我需要时间想清楚。" },
                _ => new[] { "你...你真的觉得师父在害我吗？但他是为我好啊...", "我习惯了——不反抗——这样就不会被罚更多。", "你想让我出手？不行——师父会生气的。" }
            };
            Debug.Log($"[双魂] 🗣️ {hostName}({mood}): '{lines[Random.Range(0, lines.Length)]}'");
        }

        /// <summary>记录复仇条目</summary>
        public void RecordRevenge(string target, string crime, bool manual = false)
        {
            revengeList.Add(new RevengeEntry { targetName = target, crime = crime, activated = awakening >= 21, manual = manual });
            Debug.Log($"[双魂·复仇] 📝 {(manual?"手动":"自动")}记录: {target}——{crime}");
        }

        /// <summary>清算复仇条目</summary>
        public void AvengeEntry(int index)
        {
            if (index < 0 || index >= revengeList.Count) return;
            var entry = revengeList[index];
            entry.avenged = true;
            Debug.Log($"[双魂·复仇] ✅ 已清算: {entry.targetName}——{entry.crime}");
            AddTrust(5, $"清算{entry.targetName}");
            AddAwakening(3, $"亲手复仇{entry.targetName}");
        }

        /// <summary>日常意志力恢复</summary>
        public void RestoreWillpower(int amount) { willpower = Mathf.Min(100, willpower + amount); }

        public string GetStatusText()
        {
            string syncLabel = syncRate >= 90 ? "🔥双魂合一" : syncRate >= 70 ? "⚡高度同步" : syncRate >= 40 ? "🌓半同步" : "🌑各自为战";
            return $"🌓 双魂 | {hostName}({hostRealm}) | 信任:{trust} 觉醒:{awakening} | {syncLabel} | 意志:{willpower}";
        }
    }
}
