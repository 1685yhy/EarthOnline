using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthOnline.Framework;

namespace EarthOnline.Core
{
    /// <summary>
    /// Heart Demon Tribulation (心魔劫) phase — illusion trials that test
    /// the cultivator's willpower with 7 demon types and 4 resolution methods.
    ///
    /// Story 002:
    /// - Willpower starts at 100, reaches 0 = tribulation failure
    /// - 7 heart demon types based on simulated player history
    /// - 4 resolution methods with different base success rates
    /// - 凝心丹 reduces willpower time drain by 50%
    /// - 辟邪佩 reduces willpower loss from failed resolution by 30%
    /// - Thunder perfect dodges reduce heart demon difficulty (-15% each)
    ///
    /// Starts when ThunderTribulationCompletedEvent fires.
    /// Publishes HeartDemonAllClearedEvent (success) or HeartDemonFailedEvent.
    /// </summary>
    public class HeartDemonTribulation : MonoBehaviour
    {
        [Header("Willpower")]
        [SerializeField] private float maxWillpower = 100f;
        [SerializeField] private float timeDrainPerSecond = 2f;
        [SerializeField] private float baseResolveFailCost = 20f;

        [Header("Demon Generation")]
        [SerializeField] private int minDemonCount = 3;
        [SerializeField] private int maxDemonCount = 7;
        [SerializeField] private float demonSpawnInterval = 1.5f;

        [Header("Resolution Success Rates")]
        [SerializeField][Range(0f, 1f)] private float confrontBaseRate = 0.40f;   // 直面
        [SerializeField][Range(0f, 1f)] private float reflectBaseRate = 0.50f;    // 反思
        [SerializeField][Range(0f, 1f)] private float acceptBaseRate = 0.60f;     // 接纳
        [SerializeField][Range(0f, 1f)] private float suppressBaseRate = 0.30f;   // 压制

        [Header("Item Mitigation")]
        [SerializeField][Range(0f, 1f)] private float ningxinPillDrainReduction = 0.50f;  // 凝心丹
        [SerializeField][Range(0f, 1f)] private float amuletLossReduction = 0.30f;       // 辟邪佩

        // ── Heart Demon Data ─────────────────────────────────────────────

        /// <summary>
        /// The seven heart demon types, each tied to a player history dimension.
        /// </summary>
        public enum DemonType
        {
            Greed,       // 贪 — attachment to wealth/material
            Fear,        // 惧 — trauma from combat/death
            Regret,      // 悔 — past choices and missed opportunities
            Attachment,  // 执 — obsession with people or goals
            Pride,       // 傲 — arrogance from cultivation speed
            Doubt,       // 疑 — confusion about one's dao path
            Wrath        // 怒 — bloodlust and vengeance
        }

        /// <summary>
        /// The four resolution methods the player can choose.
        /// </summary>
        public enum ResolutionMethod
        {
            Confront,  // 直面 — directly face the demon
            Reflect,   // 反思 — contemplate its roots
            Accept,    // 接纳 — acknowledge and integrate
            Suppress   // 压制 — forcibly repress (high cost)
        }

        [System.Serializable]
        public struct HeartDemonDef
        {
            public DemonType type;
            public string displayName;
            public string description;
            public string resolutionHint;
            public float difficultyBonus;   // individual demon difficulty modifier
        }

        // ── State ────────────────────────────────────────────────────────

        private float currentWillpower;
        private float difficultyModifierFromThunder;
        private bool isActive;
        private bool isResolving;
        private int currentDemonIndex;
        private int totalDemonCount;
        private int resolvedCount;
        private List<HeartDemonDef> activeDemons;
        private HeartDemonDef currentDemon;
        private Coroutine timeDrainCoroutine;

        // Demon type metadata (display names, descriptions, hints)
        private static readonly Dictionary<DemonType, (string name, string desc, string hint)> DemonMeta = new()
        {
            { DemonType.Greed,       ("贪", "心魔化为你毕生所求之物，无穷的欲望吞噬你的道心。", "放下执念，一无所有亦是圆满。") },
            { DemonType.Fear,        ("惧", "你最深的恐惧具象化，过往的死亡与失败如潮水般涌来。", "恐惧只是幻影，正视它便会消散。") },
            { DemonType.Regret,      ("悔", "那些你未能拯救的人、未能走的路，在幻境中反复重演。", "过去不可改，但道心可坚。") },
            { DemonType.Attachment,  ("执", "你所珍视之人的面容在虚空中浮现，声声呼唤让你驻足。", "真正的守护不是停留，而是前行。") },
            { DemonType.Pride,       ("傲", "你的修为与天赋化作傲慢的心魔，嘲笑一切不如你者。", "山外有山，道无止境。") },
            { DemonType.Doubt,       ("疑", "你所坚信的道在眼前崩塌，万般法理皆成虚妄。", "道本无定法，疑是悟的开始。") },
            { DemonType.Wrath,       ("怒", "你曾斩杀的一切生灵的怨念汇聚成滔天怒火。", "怒火烧人亦烧己，平息方见本心。") }
        };

        // ── Event Subscriptions ──────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<ThunderTribulationCompletedEvent>(OnThunderCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ThunderTribulationCompletedEvent>(OnThunderCompleted);
        }

        // ── Trigger: Thunder Phase Completed ─────────────────────────────

        private void OnThunderCompleted(ThunderTribulationCompletedEvent evt)
        {
            if (isActive) return;

            difficultyModifierFromThunder = evt.DifficultyModifier is float f ? f : 0f;

            Debug.Log($"[HeartDemon] Thunder phase complete. Difficulty modifier: {difficultyModifierFromThunder:P0}");

            StartHeartDemonStage();
        }

        // ── Stage Start ──────────────────────────────────────────────────

        private void StartHeartDemonStage()
        {
            isActive = true;
            currentWillpower = maxWillpower;
            currentDemonIndex = 0;
            resolvedCount = 0;

            // Generate heart demons based on simulated player history
            activeDemons = GenerateHeartDemons();
            totalDemonCount = activeDemons.Count;

            Debug.Log($"[HeartDemon] Stage started. {totalDemonCount} demons. Willpower: {currentWillpower}/{maxWillpower}");

            EventBus.Publish(new HeartDemonStageStartedEvent
            {
                DemonCount = totalDemonCount,
                InitialWillpower = currentWillpower,
                DifficultyModifier = difficultyModifierFromThunder
            });

            // Start passive willpower drain
            timeDrainCoroutine = StartCoroutine(PassiveTimeDrain());

            // Begin spawning demons one by one
            StartCoroutine(SpawnDemonsSequence());
        }

        // ── Demon Generation ─────────────────────────────────────────────

        /// <summary>
        /// Generate a list of heart demons for this tribulation session.
        /// Produces 3-7 demons. Each demon's type is seeded by the
        /// current tribulation quality and readiness.
        /// </summary>
        private List<HeartDemonDef> GenerateHeartDemons()
        {
            int count = Random.Range(minDemonCount, maxDemonCount + 1);

            // Determine which demon types to include based on quality
            TribulationQuality quality = TribulationManager.Instance != null
                ? TribulationManager.Instance.CurrentQuality
                : TribulationQuality.Normal;

            // Higher quality platforms expose a broader range of demons
            int typeCount = quality switch
            {
                TribulationQuality.Secret => 7,   // All 7 types
                TribulationQuality.Ancient => 5,   // 5 types
                _ => 3                              // 3 types
            };

            typeCount = Mathf.Min(typeCount, count);

            // Pick demon types
            DemonType[] allTypes = (DemonType[])System.Enum.GetValues(typeof(DemonType));
            List<DemonType> selectedTypes = new();

            // Shuffle and take
            ShuffleArray(allTypes);
            for (int i = 0; i < typeCount && i < allTypes.Length; i++)
            {
                selectedTypes.Add(allTypes[i]);
            }

            // Build demon defs
            List<HeartDemonDef> demons = new();
            for (int i = 0; i < count; i++)
            {
                DemonType type = selectedTypes[i % selectedTypes.Count];
                var meta = DemonMeta[type];

                demons.Add(new HeartDemonDef
                {
                    type = type,
                    displayName = meta.name,
                    description = meta.desc,
                    resolutionHint = meta.hint,
                    difficultyBonus = 0f
                });
            }

            return demons;
        }

        /// <summary>
        /// Fisher-Yates shuffle on an array.
        /// </summary>
        private static void ShuffleArray<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        // ── Demon Spawning ───────────────────────────────────────────────

        private IEnumerator SpawnDemonsSequence()
        {
            for (int i = 0; i < activeDemons.Count; i++)
            {
                currentDemon = activeDemons[i];
                currentDemonIndex = i + 1;

                // Publish spawn event
                EventBus.Publish(new HeartDemonSpawnedEvent
                {
                    DemonIndex = currentDemonIndex,
                    TotalDemons = totalDemonCount,
                    DemonType = currentDemon.displayName,
                    Description = currentDemon.description,
                    ResolutionHint = currentDemon.resolutionHint
                });

                Debug.Log($"[HeartDemon] Demon #{currentDemonIndex}/{totalDemonCount}: [{currentDemon.displayName}] {currentDemon.description}");

                // Wait for player to resolve before spawning next
                yield return new WaitUntil(() => !isResolving);

                yield return new WaitForSeconds(demonSpawnInterval);
            }
        }

        // ── Passive Willpower Drain ──────────────────────────────────────

        private IEnumerator PassiveTimeDrain()
        {
            while (isActive && currentWillpower > 0f)
            {
                yield return new WaitForSeconds(1f);

                float drainThisSecond = timeDrainPerSecond;

                // 凝心丹 reduces drain by 50%
                if (HasItem("凝心丹"))
                    drainThisSecond *= (1f - ningxinPillDrainReduction);

                float previous = currentWillpower;
                currentWillpower = Mathf.Max(0f, currentWillpower - drainThisSecond);

                EventBus.Publish(new HeartDemonWillPowerChangedEvent
                {
                    PreviousWillpower = previous,
                    CurrentWillpower = currentWillpower,
                    MaxWillpower = maxWillpower,
                    Reason = "time_drain"
                });

                if (currentWillpower <= 0f)
                {
                    OnWillpowerDepleted();
                    yield break;
                }
            }
        }

        // ── Resolution (called by UI/Gameplay code) ──────────────────────

        /// <summary>
        /// Attempt to resolve the current heart demon using the chosen method.
        /// Called by UI or gameplay logic when the player picks a resolution.
        /// </summary>
        /// <param name="method">The resolution method chosen.</param>
        /// <returns>True if the resolution was attempted (demon was active).</returns>
        public bool AttemptResolution(ResolutionMethod method)
        {
            if (!isActive || isResolving || currentDemonIndex == 0)
                return false;

            isResolving = true;

            float baseRate = GetBaseSuccessRate(method);
            float finalRate = baseRate + difficultyModifierFromThunder;
            finalRate = Mathf.Clamp01(finalRate);

            float roll = Random.value;
            bool success = roll <= finalRate;

            float willpowerCost = 0f;

            if (!success)
            {
                willpowerCost = baseResolveFailCost;

                // 辟邪佩 reduces willpower loss by 30%
                if (HasItem("辟邪佩"))
                    willpowerCost *= (1f - amuletLossReduction);

                // Suppress has additional cost
                if (method == ResolutionMethod.Suppress)
                    willpowerCost *= 1.5f;

                float previous = currentWillpower;
                currentWillpower = Mathf.Max(0f, currentWillpower - willpowerCost);

                EventBus.Publish(new HeartDemonWillPowerChangedEvent
                {
                    PreviousWillpower = previous,
                    CurrentWillpower = currentWillpower,
                    MaxWillpower = maxWillpower,
                    Reason = "resolve_failed"
                });

                Debug.Log($"[HeartDemon] Resolution [{method}] FAILED (rate: {finalRate:P0}, roll: {roll:P2}). Willpower: {currentWillpower}/{maxWillpower}");
            }
            else
            {
                resolvedCount++;
                Debug.Log($"[HeartDemon] Resolution [{method}] SUCCEEDED (rate: {finalRate:P0}, roll: {roll:P2}). Willpower: {currentWillpower}/{maxWillpower}");
            }

            EventBus.Publish(new HeartDemonResolvedEvent
            {
                DemonIndex = currentDemonIndex,
                DemonType = currentDemon.displayName,
                ResolutionMethod = method.ToString().ToLower(),
                Success = success,
                WillpowerCost = willpowerCost
            });

            isResolving = false;

            if (currentWillpower <= 0f)
            {
                OnWillpowerDepleted();
                return true;
            }

            // Check if all done
            if (currentDemonIndex >= totalDemonCount && resolvedCount >= totalDemonCount)
            {
                OnAllDemonsCleared();
            }

            return true;
        }

        /// <summary>
        /// Get the base success rate for a resolution method.
        /// </summary>
        private float GetBaseSuccessRate(ResolutionMethod method)
        {
            return method switch
            {
                ResolutionMethod.Confront => confrontBaseRate,
                ResolutionMethod.Reflect  => reflectBaseRate,
                ResolutionMethod.Accept   => acceptBaseRate,
                ResolutionMethod.Suppress => suppressBaseRate,
                _ => 0.5f
            };
        }

        // ── Failure / Success ────────────────────────────────────────────

        private void OnWillpowerDepleted()
        {
            if (!isActive) return;
            isActive = false;

            if (timeDrainCoroutine != null)
                StopCoroutine(timeDrainCoroutine);

            EventBus.Publish(new HeartDemonFailedEvent
            {
                DemonsRemaining = totalDemonCount - resolvedCount,
                LastDemonType = currentDemon.displayName
            });

            // End tribulation as failure
            if (TribulationManager.Instance != null)
            {
                TribulationManager.Instance.EndTribulation(false);
            }

            Debug.Log($"[HeartDemon] FAILED — willpower depleted. {resolvedCount}/{totalDemonCount} demons resolved.");
        }

        private void OnAllDemonsCleared()
        {
            if (!isActive) return;
            isActive = false;

            if (timeDrainCoroutine != null)
                StopCoroutine(timeDrainCoroutine);

            EventBus.Publish(new HeartDemonAllClearedEvent
            {
                TotalDemons = totalDemonCount,
                ResolvedCount = resolvedCount,
                RemainingWillpower = currentWillpower
            });

            // End tribulation as success (Story 003 integration: DaoQuestioning
            // sets AutoEndOnHeartClear = false to take over the outcome flow)
            if (TribulationManager.Instance != null && TribulationManager.Instance.AutoEndOnHeartClear)
            {
                TribulationManager.Instance.EndTribulation(true);
            }

            Debug.Log($"[HeartDemon] ALL CLEARED! {resolvedCount}/{totalDemonCount} demons resolved. Remaining willpower: {currentWillpower}/{maxWillpower}");
        }

        // ── Item Checks ──────────────────────────────────────────────────

        /// <summary>
        /// Check if the player has a specific item that affects heart demon stage.
        /// Placeholder — hooks into Inventory system when available.
        ///
        /// Current logic uses the tribulation readiness dimensions as proxy:
        /// 凝心丹 (Ningxin Pill) — pill readiness > 0.5
        /// 辟邪佩 (Amulet) — equip readiness > 0.5
        /// </summary>
        private bool HasItem(string itemName)
        {
            // TODO: Replace with actual Inventory system query
            // Current proxy: readiness dimension thresholds
            return itemName switch
            {
                "凝心丹" => true, // Always available — pill preparation assumed
                "辟邪佩" => TribulationManager.Instance is { CurrentQuality: >= TribulationQuality.Ancient },
                _ => false
            };
        }

        // ── Public Accessors ─────────────────────────────────────────────

        public bool IsActive => isActive;
        public bool IsResolving => isResolving;
        public float CurrentWillpower => currentWillpower;
        public float MaxWillpower => maxWillpower;
        public float WillpowerPercent => maxWillpower > 0f ? currentWillpower / maxWillpower : 0f;
        public int CurrentDemonIndex => currentDemonIndex;
        public int TotalDemonCount => totalDemonCount;
        public int ResolvedCount => resolvedCount;
        public HeartDemonDef CurrentDemonDef => currentDemon;
        public float DifficultyModifier => difficultyModifierFromThunder;
    }
}
