using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline
{
    /// <summary>
    /// V2.0 修炼系统 —— 境界突破、功法修炼、灵力运转。
    /// 不再是 PlayerLevel 1→N。是练气→筑基→金丹→...→渡劫→大成。
    /// </summary>
    public class CultivationManager : MonoBehaviour
    {
        public static CultivationManager Instance { get; private set; }

        public enum Realm { Mortal, QiRefining, Foundation, GoldenCore, NascentSoul, SpiritSevering, Tribulation, GreatAscension }
        public Realm CurrentRealm { get; private set; } = Realm.Mortal;
        public int CurrentLayer { get; private set; } = 0; // 当前境界第几层
        public int MaxLayer => IsPlayer ? 13 : 9; // 主角特权：每境13层

        public bool IsPlayer { get; set; } = true;

        // 突破所需修为阈值
        private static readonly Dictionary<Realm, int> _breakthroughThresholds = new()
        {
            [Realm.QiRefining] = 100,
            [Realm.Foundation] = 300,
            [Realm.GoldenCore] = 800,
            [Realm.NascentSoul] = 1800,
            [Realm.SpiritSevering] = 3500,
            [Realm.Tribulation] = 6000,
            [Realm.GreatAscension] = 10000,
        };

        public string RealmName => CurrentRealm switch
        {
            Realm.Mortal => "凡人",
            Realm.QiRefining => "练气期",
            Realm.Foundation => "筑基期",
            Realm.GoldenCore => "金丹期",
            Realm.NascentSoul => "元婴期",
            Realm.SpiritSevering => "化神期",
            Realm.Tribulation => "渡劫期",
            Realm.GreatAscension => "大成期",
            _ => "未知"
        };

        public string FullTitle
        {
            get
            {
                if (CurrentRealm == Realm.Mortal) return "凡人";
                return $"{RealmName} 第{CurrentLayer}层";
            }
        }

        public event System.Action<Realm, int> OnRealmBreakthrough;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            EventBus.Subscribe("OnCultivationBoost", OnCultivationGained);
        }

        void OnCultivationGained(Dictionary<string, object> data)
        {
            int amount = data.ContainsKey("amount") ? (int)data["amount"] : 0;
            // Check if can break through to next layer
            CheckLayerAdvance();
        }

        /// <summary>
        /// 检查是否可以突破到下一层/境界
        /// </summary>
        public void CheckLayerAdvance()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            int cultivation = stats.cultivation;

            // 当前境界内升级（第N层 → 第N+1层）
            int nextLayerThreshold = GetNextLayerCultivation();
            if (cultivation >= nextLayerThreshold && CurrentLayer < MaxLayer)
            {
                AdvanceLayer();
            }

            // 大境界突破
            Realm nextRealm = CurrentRealm + 1;
            if (nextRealm <= Realm.GreatAscension &&
                _breakthroughThresholds.ContainsKey(nextRealm) &&
                cultivation >= _breakthroughThresholds[nextRealm] &&
                CurrentLayer >= MaxLayer)
            {
                AttemptBreakthrough(nextRealm);
            }
        }

        int GetNextLayerCultivation()
        {
            if (!_breakthroughThresholds.ContainsKey(CurrentRealm))
                return 100; // 凡人→练气的门槛
            int baseThreshold = _breakthroughThresholds[CurrentRealm];
            return baseThreshold + CurrentLayer * (baseThreshold / 5);
        }

        void AdvanceLayer()
        {
            CurrentLayer++;
            Debug.Log($"[修炼] ⬆ {RealmName} 第{CurrentLayer}层！");
            EarthOnline.Combat.FloatingDamage.Spawn(
                PlayerStats.Instance?.transform.position ?? Vector3.zero,
                $"{RealmName} {CurrentLayer}层", new Color(0.3f, 0.8f, 1f), 2f);

            if (CurrentLayer == MaxLayer)
            {
                Debug.Log($"[修炼] ⚠️ {RealmName}大圆满！需要突破瓶颈才能进入下一境界。");
                Debug.Log($"[修炼] 突破需要：修为达标 + 突破材料 + 机缘/顿悟。");
            }

            PlayerStats.Instance?.UpdateHUD();
        }

        /// <summary>
        /// 大境界突破——有成功率和代价
        /// </summary>
        void AttemptBreakthrough(Realm targetRealm)
        {
            // 突破成功率：基础70% + 层数加成(每多1层+3%)
            float successRate = 0.70f + (CurrentLayer - 9) * 0.03f;
            successRate = Mathf.Clamp(successRate, 0.3f, 0.95f);

            Debug.Log($"══════════════════════════════");
            Debug.Log($"  ⚡ 尝试突破：{RealmName} → {GetRealmName(targetRealm)}");
            Debug.Log($"  成功率：{successRate * 100:F0}%");
            Debug.Log($"══════════════════════════════");

            if (Random.value < successRate)
            {
                // 成功！
                CurrentRealm = targetRealm;
                CurrentLayer = 1;

                string abilityMsg = targetRealm switch
                {
                    Realm.Foundation => "🏔️ 可御剑飞行！按Shift+F御剑。神识探测解锁——可以看到隐藏的灵气节点。",
                    Realm.GoldenCore => "🔥 丹火凝聚！可学习炼丹和炼器。灵力护体自动开启——受到伤害-20%。",
                    Realm.NascentSoul => "👻 元婴出窍！可分身执行远程任务。死亡时元婴可逃逸一次。",
                    Realm.SpiritSevering => "🌌 领域初成！可创造小型领域影响周围环境。法则感悟开始。",
                    Realm.Tribulation => "⚡ 天劫降临！每一次突破都是一次天劫。渡过→大成。失败→重修。",
                    Realm.GreatAscension => "🌟 已臻大成！可开辟洞天、创建宗门、飞升上界。",
                    _ => ""
                };

                Debug.Log($"🎉 突破成功！已踏入{GetRealmName(targetRealm)}！");
                if (!string.IsNullOrEmpty(abilityMsg)) Debug.Log($"[突破] {abilityMsg}");

                OnRealmBreakthrough?.Invoke(targetRealm, CurrentLayer);
                EventBus.Publish("OnRealmBreakthrough", new Dictionary<string, object> {
                    {"realm", targetRealm.ToString()}, {"layer", CurrentLayer}
                });
                EarthOnline.Combat.FloatingDamage.Spawn(
                    PlayerStats.Instance?.transform.position ?? Vector3.zero,
                    $"🎉 {GetRealmName(targetRealm)}！", new Color(1f, 0.85f, 0f), 3f);
            }
            else
            {
                // 失败
                var stats = PlayerStats.Instance;
                int cultivationLoss = stats != null ? Mathf.RoundToInt(stats.cultivation * 0.15f) : 50;

                Debug.Log($"💥 突破失败！修为倒退{15}%。需重新积累。");
                Debug.Log($"[突破] 提示：提高层数可增加成功率。每多修炼1层+3%成功率。");

                if (stats != null)
                {
                    stats.cultivation -= cultivationLoss;
                    stats.currentExp = Mathf.Max(0, stats.currentExp - cultivationLoss);
                }
                EarthOnline.Combat.FloatingDamage.Spawn(
                    PlayerStats.Instance?.transform.position ?? Vector3.zero,
                    "突破失败", new Color(1f, 0.2f, 0.2f), 2f);
            }

            PlayerStats.Instance?.UpdateHUD();
        }

        string GetRealmName(Realm r) => r switch
        {
            Realm.Mortal => "凡人", Realm.QiRefining => "练气期", Realm.Foundation => "筑基期",
            Realm.GoldenCore => "金丹期", Realm.NascentSoul => "元婴期",
            Realm.SpiritSevering => "化神期", Realm.Tribulation => "渡劫期",
            Realm.GreatAscension => "大成期", _ => "未知"
        };

        void OnDestroy()
        {
            EventBus.Unsubscribe("OnCultivationBoost", OnCultivationGained);
        }
    }
}
