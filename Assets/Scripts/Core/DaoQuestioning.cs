using System.Collections.Generic;
using EarthOnline.Framework;
using UnityEngine;

namespace EarthOnline.Core
{
    /// <summary>
    /// The 4 dimensions evaluated during Dao Questioning (天道问心).
    /// Each answer maps to one dimension; the dominant dimension determines
    /// the Dao Body type.
    /// </summary>
    public enum DaoDimension
    {
        DaoHeart,    // 道之心 -> 超然 (Transcendent)
        PowerView,   // 力量观 -> 破虚 (Breaker)
        Emotion,     // 情绪   -> 守成 (Guardian)
        Obsession    // 执念   -> 凡人 (Mortal)
    }

    /// <summary>
    /// Accumulated dimension scores after answering all questions.
    /// </summary>
    [System.Serializable]
    public struct DaoQuestionResult
    {
        public float daoHeart;
        public float powerView;
        public float emotion;
        public float obsession;

        /// <summary>Returns the dimension with the highest accumulated score.</summary>
        public readonly DaoDimension GetDominantDimension()
        {
            float max = Mathf.Max(daoHeart, powerView, emotion, obsession);
            if (Mathf.Approximately(daoHeart, max)) return DaoDimension.DaoHeart;
            if (Mathf.Approximately(powerView, max)) return DaoDimension.PowerView;
            if (Mathf.Approximately(emotion, max)) return DaoDimension.Emotion;
            return DaoDimension.Obsession;
        }

        /// <summary>
        /// The gap between the highest and second-highest score [0-∞).
        /// Higher = player has a clearer inclination toward one dimension.
        /// </summary>
        public readonly float AlignmentStrength
        {
            get
            {
                float[] vals = new float[] { daoHeart, powerView, emotion, obsession };
                System.Array.Sort(vals);
                System.Array.Reverse(vals);
                return vals[0] - vals[1];
            }
        }
    }

    /// <summary>A single Dao Questioning question with 4 answer options.</summary>
    [System.Serializable]
    public struct DaoQuestion
    {
        public string questionText;
        public DaoAnswerOption[] answers;
    }

    /// <summary>An answer option that maps to a dimension with a weight.</summary>
    [System.Serializable]
    public struct DaoAnswerOption
    {
        public string text;
        public DaoDimension dimension;
        [Range(0f, 1f)] public float weight;
    }

    /// <summary>
    /// Dao Questioning (天道问心) — a philosophical test that runs after the
    /// heart demon tribulation phase. The player answers questions probing their
    /// Dao heart, power perspective, emotional nature, and attachments.
    ///
    /// Results determine the Dao Body type (via dominant dimension) and
    /// influence the body's quality (via alignment strength).
    ///
    /// <see cref="TribulationBody"/> consumes the questioning result.
    ///
    /// Flow:
    ///   1. HeartDemonAllClearedEvent fires
    ///   2. DaoQuestioning selects N random questions from pool
    ///   3. Each question is presented via DaoQuestionPresentedEvent
    ///   4. UI calls SubmitAnswer() with player's choice
    ///   5. After all questions, publishes DaoQuestioningCompletedEvent
    /// </summary>
    public class DaoQuestioning : MonoBehaviour
    {
        [Header("Questioning Config")]
        [SerializeField] private int questionsToAsk = 3;
        [SerializeField] private List<DaoQuestion> questionPool = new();

        // ── State ────────────────────────────────────────────────────────

        private int currentQuestionIndex;
        private List<DaoQuestion> selectedQuestions;
        private DaoQuestionResult accumulatedScores;
        private bool isActive;
        private bool answersSubmitted;

        // ── Default Question Pool (8 questions) ──────────────────────────

        private static DaoQuestion[] BuildDefaultPool()
        {
            return new DaoQuestion[]
            {
                new DaoQuestion
                {
                    questionText = "你在一处上古秘境中发现了逆天传承，但需要你舍弃现有的一切修为从头修炼，你的选择是？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "顺应天道，舍弃修为重获新生",            dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "以力破法，保留修为强行参悟",            dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "感念机缘，不舍旧日修行之路",            dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "执着于传承，不愿放弃任何机会",          dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "你毕生所爱之人因你而死，你在幻境中再次见到了她，你会如何面对？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "生死有命，放下执念继续前行",            dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "若有来世，定以无敌之力护她周全",        dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "悲从中来，宁愿沉溺幻境不再醒来",        dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "不惜一切代价也要逆转生死复活她",        dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "最信任的挚友在最关键时刻背叛了你，导致你身陷绝境，你的心境是？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "万事皆有因果，背叛亦是天道的一部分",    dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "此仇必报，待脱困后定要让他付出代价",    dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "心碎难当，无法接受被最信任之人背叛",    dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "绝不原谅，这份恨意是我活下去的动力",    dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "你面前跪着一位杀害无数凡人的魔修，你已将其击败，如何处置？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "天道轮回，废其修为交由天道审判",        dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "就地斩杀！以绝后患即是正义",            dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "虽罪大恶极，但我终究下不了杀手",        dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "必须亲自动手，任何代劳都无法平息怒火",  dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "你遇到一位天赋远不如你但比你更加努力的修行者，你的态度是？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "大道朝天各走一边，人人皆有自己的道",    dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "努力可敬，但天赋才是决定高度的关键",    dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "深受感动，愿不计回报助他一臂之力",      dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "暗自较劲，绝不能被他后来居上",          dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "深山中发现万年仙草，服下可突破瓶颈。但守护妖兽奄奄一息——它的孩子急需此草救命。你会？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "万物有灵，让出仙草，机缘可再得",        dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "先制服妖兽，仙草和幼崽都是我的战利品",  dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "于心不忍，取一半仙草救妖兽的孩子",      dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "突破瓶颈是我毕生所求，什么都不能阻挡",  dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "你即将飞升离开此界，你的弟子与家人跪求你留下，你的选择是？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "顺应天道飞升，缘尽于此界",              dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "待我成仙归来，护佑你们万世",            dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "难以割舍，决定留下陪伴所爱之人",        dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "留下分身守护，真身飞升——我全都要",     dimension = DaoDimension.Obsession, weight = 1f }
                    }
                },
                new DaoQuestion
                {
                    questionText = "修行至今，你认为最强大的力量是什么？",
                    answers = new DaoAnswerOption[4]
                    {
                        new DaoAnswerOption { text = "顺应天地之道，与宇宙共鸣的力量",        dimension = DaoDimension.DaoHeart,  weight = 1f },
                        new DaoAnswerOption { text = "以绝对实力碾压一切阻碍的力量",          dimension = DaoDimension.PowerView, weight = 1f },
                        new DaoAnswerOption { text = "爱与羁绊——守护所爱之人的力量",         dimension = DaoDimension.Emotion,   weight = 1f },
                        new DaoAnswerOption { text = "永不言弃的执念——超越一切的力量",       dimension = DaoDimension.Obsession, weight = 1f }
                    }
                }
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Unity Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (questionPool == null || questionPool.Count == 0)
            {
                questionPool = new List<DaoQuestion>(BuildDefaultPool());
            }

            // Take over the outcome flow from HeartDemonTribulation
            if (TribulationManager.Instance != null)
            {
                TribulationManager.Instance.AutoEndOnHeartClear = false;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<HeartDemonAllClearedEvent>(OnHeartDemonCleared);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HeartDemonAllClearedEvent>(OnHeartDemonCleared);
        }

        // ── Trigger: Heart Demon Phase Completed ─────────────────────────

        private void OnHeartDemonCleared(HeartDemonAllClearedEvent evt)
        {
            StartQuestioning();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Questioning Flow
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Begin the Dao Questioning sequence. Selects random questions
        /// from the pool and starts presenting them.
        /// </summary>
        public void StartQuestioning()
        {
            if (isActive) return;
            isActive = true;
            answersSubmitted = false;
            currentQuestionIndex = 0;
            accumulatedScores = default;

            selectedQuestions = SelectQuestions();

            Debug.Log($"[DaoQuestioning] Started with {selectedQuestions.Count} questions from pool of {questionPool.Count}.");

            EventBus.Publish(new DaoQuestioningStartedEvent
            {
                TotalQuestions = selectedQuestions.Count
            });

            PresentCurrentQuestion();
        }

        /// <summary>
        /// Select random questions from the pool without repetition.
        /// </summary>
        private List<DaoQuestion> SelectQuestions()
        {
            int count = Mathf.Min(questionsToAsk, questionPool.Count);
            List<DaoQuestion> pool = new List<DaoQuestion>(questionPool);
            List<DaoQuestion> selected = new List<DaoQuestion>();

            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, pool.Count);
                selected.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return selected;
        }

        /// <summary>
        /// Fire the presentation event for the current question.
        /// When the last question is answered, CompleteQuestioning is called.
        /// </summary>
        private void PresentCurrentQuestion()
        {
            if (currentQuestionIndex >= selectedQuestions.Count)
            {
                CompleteQuestioning();
                return;
            }

            DaoQuestion q = selectedQuestions[currentQuestionIndex];

            string[] answerTexts = new string[4];
            for (int i = 0; i < 4 && i < q.answers.Length; i++)
            {
                answerTexts[i] = q.answers[i].text;
            }

            EventBus.Publish(new DaoQuestionPresentedEvent
            {
                QuestionIndex = currentQuestionIndex + 1,
                TotalQuestions = selectedQuestions.Count,
                QuestionText = q.questionText,
                AnswerTexts = answerTexts,
                IsLastQuestion = currentQuestionIndex >= selectedQuestions.Count - 1
            });
        }

        // ══════════════════════════════════════════════════════════════════
        //  Answer Submission (called by UI)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Submit the player's answer for the current question.
        /// Accumulates the dimension score and advances to the next question.
        /// </summary>
        /// <param name="answerIndex">0-based answer index [0-3].</param>
        /// <returns>True if the answer was accepted.</returns>
        public bool SubmitAnswer(int answerIndex)
        {
            if (!isActive || answersSubmitted) return false;
            if (currentQuestionIndex >= selectedQuestions.Count) return false;

            DaoQuestion q = selectedQuestions[currentQuestionIndex];
            if (answerIndex < 0 || answerIndex >= q.answers.Length) return false;

            DaoAnswerOption chosen = q.answers[answerIndex];

            // Accumulate score into the appropriate dimension
            switch (chosen.dimension)
            {
                case DaoDimension.DaoHeart:  accumulatedScores.daoHeart  += chosen.weight; break;
                case DaoDimension.PowerView: accumulatedScores.powerView += chosen.weight; break;
                case DaoDimension.Emotion:   accumulatedScores.emotion   += chosen.weight; break;
                case DaoDimension.Obsession: accumulatedScores.obsession += chosen.weight; break;
            }

            Debug.Log($"[DaoQuestioning] Q{currentQuestionIndex + 1}: chose [{chosen.dimension}] weight={chosen.weight}");

            currentQuestionIndex++;
            PresentCurrentQuestion();

            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Completion
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Finalise questioning and publish the result for TribulationBody.
        /// </summary>
        private void CompleteQuestioning()
        {
            isActive = false;
            answersSubmitted = true;

            DaoDimension dominant = accumulatedScores.GetDominantDimension();

            Debug.Log($"[DaoQuestioning] Complete. " +
                      $"DaoHeart:{accumulatedScores.daoHeart:F2} " +
                      $"PowerView:{accumulatedScores.powerView:F2} " +
                      $"Emotion:{accumulatedScores.emotion:F2} " +
                      $"Obsession:{accumulatedScores.obsession:F2} | " +
                      $"Dominant:{dominant} Alignment:{accumulatedScores.AlignmentStrength:F2}");

            EventBus.Publish(new DaoQuestioningCompletedEvent
            {
                DaoHeartScore = accumulatedScores.daoHeart,
                PowerViewScore = accumulatedScores.powerView,
                EmotionScore = accumulatedScores.emotion,
                ObsessionScore = accumulatedScores.obsession,
                DominantDimension = (int)dominant,
                AlignmentStrength = accumulatedScores.AlignmentStrength
            });
        }

        // ── Public Properties ─────────────────────────────────────────────

        public bool IsActive => isActive;
        public int CurrentQuestionIndex => currentQuestionIndex;
        public int TotalQuestions => selectedQuestions?.Count ?? 0;
        public DaoQuestionResult Scores => accumulatedScores;
    }
}
