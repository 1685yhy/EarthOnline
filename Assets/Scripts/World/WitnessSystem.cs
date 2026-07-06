using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;
using EarthOnline.NPC;

namespace EarthOnline
{
    /// <summary>
    /// V2.2 见证人举报系统 —— NPC目睹犯罪后会去举报。
    /// 不是"你打了人什么事没有"——NPC会走路去最近的守卫/派系据点报告。
    /// 社会运行逻辑：目击者不是摆设。
    /// </summary>
    public class WitnessSystem : MonoBehaviour
    {
        public static WitnessSystem Instance { get; private set; }

        private List<WitnessReport> _pendingReports = new();
        private float _nextReportTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _nextReportTime = Time.time + 30f;
        }

        /// <summary>NPC目睹犯罪→提交报告</summary>
        public void WitnessCrime(string npcName, string crime, Vector3 location)
        {
            _pendingReports.Add(new WitnessReport { witness=npcName, crime=crime, location=location, time=Time.time });
        }

        void Update()
        {
            if (Time.time >= _nextReportTime && _pendingReports.Count > 0)
            {
                _nextReportTime = Time.time + Random.Range(30f, 60f);
                ProcessReport();
            }
        }

        void ProcessReport()
        {
            var report = _pendingReports[0];
            _pendingReports.RemoveAt(0);

            // 根据不同派系——举报到不同地方
            var faction = Random.value < 0.5f ? "tianyuan" : "qingyun";
            var fs = FactionSystem.Instance;
            if (fs != null)
            {
                fs.ModifyReputation(faction, -3);
                Debug.Log($"[见证] 📋 {report.witness}向{fs.GetFaction(faction)?.name ?? faction}举报了你的{report.crime}！");
                Debug.Log($"[见证] 派系声望下降——他们开始注意到你了。");
            }

            // 增加悬赏
            var cs = CrimeSystem.Instance;
            if (cs != null && cs.bounty < 100)
            {
                cs.bounty += 10;
                Debug.Log($"[见证] 🚨 悬赏+10（目击者举报）。当前悬赏:{cs.bounty}。");
            }
        }
    }

    class WitnessReport
    {
        public string witness, crime;
        public Vector3 location;
        public float time;
    }
}
