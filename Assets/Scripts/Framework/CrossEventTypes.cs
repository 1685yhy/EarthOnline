using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Framework {













    public struct BossBreathingWindowEvent
    {
        public object Duration;
        public object IsActive;
    }

    public struct BossDefeatedEvent
    {
        public object BossId;
        public object BossName;
        public object FinalPhase;
    }

    public struct BossDialogueEvent
    {
        public object DisplayDuration;
        public object Line;
        public object Speaker;
    }

    public struct BossDiplomacyOfferEvent
    {
        public object BaseSuccessRate;
        public object BossId;
        public object BossName;
        public object ConditionDescription;
        public object EffectiveSuccessRate;
        public object PeaceWindowDuration;
        public object RequiredItems;
    }

    public struct BossDiplomacyResultEvent
    {
        public object BossId;
        public object BossName;
        public object Dialogue;
        public object GrudgeChange;
        public object Result;
    }

    public struct BossDropRolledEvent
    {
        public object BossId;
        public object BossName;
        public object IsFirstKill;
        public object IsForgeMaterials;
        public object ItemIds;
        public object ItemNames;
        public object PerfectHuntBonus;
        public object Qualities;
        public object Quantities;
    }

    public struct BossEnrageEvent
    {
        public object IsEnraged;
        public object TimeUntilEnrage;
    }

    public struct BossEntranceEvent
    {
        public object BossName;
        public object Dialogue;
        public object Realm;
        public object Title;
    }

    public struct BossEscapeEvent
    {
        public object BossId;
        public object BossName;
        public object GrudgeIncrease;
    }

    public struct BossFirstKillEvent
    {
        public object BossId;
        public object BossName;
        public object SpecialItemId;
        public object SpecialItemName;
        public object TitleId;
        public object TitleName;
    }

    public struct BossForgeMaterialDropEvent
    {
        public object BossId;
        public object BossName;
        public object ForgeRecipeIds;
        public object ForgeRecipeNames;
        public object MaterialItemId;
        public object MaterialItemName;
        public object Quantity;
    }

    public struct BossGrudgeDecayedEvent
    {
        public object BossId;
        public object BossName;
        public object DaysSinceLastEntry;
        public object NewLevel;
        public object OldLevel;
    }

    public struct BossGrudgeUpdatedEvent
    {
        public object BossId;
        public object BossName;
        public object NewLevel;
        public object OldLevel;
        public object Reason;
    }

    public struct BossPathSelectedEvent
    {
        public object BossId;
        public object BossName;
        public object PathType;
        public object PlayerId;
    }

    public struct BossPeaceBrokenEvent
    {
        public object BossId;
        public object BossName;
        public object GrudgeLevel;
        public object IsEnraged;
    }

    public struct BossPhaseChangedEvent
    {
        public object BreathingWindowDuration;
        public object CurrentHPPercent;
        public object Dialogue;
        public object NewPhaseIndex;
    }

    public struct BossReinforcementEvent
    {
        public object AllyCount;
        public object AllyNames;
        public object BossId;
        public object BossName;
        public object ConsumedItems;
        public object ReinforcementType;
    }

    public struct BossRenegedEvent
    {
        public object BossId;
        public object BossName;
        public object GrudgeIncrease;
    }

    public struct BossRetreatEvent
    {
        public object AggressionIncrease;
        public object BossId;
        public object BossName;
        public object RegionId;
    }

    public struct BossRewardDistributionEvent
    {
        public object BossId;
        public object BossName;
        public object CultivationMultiplier;
        public object DropMultiplier;
        public object PathType;
        public object ReputationMultiplier;
        public object UnlocksTitle;
    }

    public struct BossSameSpeciesKillEvent
    {
        public object BossId;
        public object BossName;
        public object GrudgeIncrease;
        public object SpeciesId;
    }

    public struct BossStealthEvent
    {
        public object BossId;
        public object BossName;
        public object CombatFavorChange;
        public object Success;
        public object SuccessRate;
    }


    public struct CelestialHerbGatheredEvent
    {
        public object NodeName;
        public object PlayerName;
        public object RegionId;
    }






    public struct DaoBodyFormedEvent
    {
        public object BodyType;
        public object BodyTypeName;
        public object FailureCount;
        public object Quality;
        public object QualityName;
        public object Success;
    }

    public struct DaoQuestionPresentedEvent
    {
        public object AnswerTexts;
        public object IsLastQuestion;
        public object QuestionIndex;
        public object QuestionText;
        public object TotalQuestions;
    }

    public struct DaoQuestioningCompletedEvent
    {
        public object AlignmentStrength;
        public object DaoHeartScore;
        public object DominantDimension;
        public object EmotionScore;
        public object ObsessionScore;
        public object PowerViewScore;
    }

    public struct DaoQuestioningStartedEvent
    {
        public object TotalQuestions;
    }


    public struct DiscoveryMapMarkerEvent
    {
        public object AddMarker;
        public object DiscoveryId;
        public object DiscoveryType;
        public object DisplayName;
        public object IsPermanent;
        public object ShowQuestionMark;
        public object WorldPosition;
    }

    public struct DiscoveryTriggeredEvent
    {
        public object Description;
        public object DiscoveryId;
        public object DiscoveryType;
        public object DisplayName;
        public object IsFirstDiscovery;
        public object IsFromSave;
        public object WorldPosition;
    }














    public struct DynamicEventActiveEvent
    {
        public object EventId;
        public object EventName;
        public object IsActive;
    }















    public struct FogBatchRevealedEvent
    {
        public object CellsChanged;
        public object RegionId;
    }

    public struct FogCellRevealedEvent
    {
        public object CellX;
        public object CellY;
        public object NewLayer;
        public object OldLayer;
    }

    public struct FogHighGroundVisionEvent
    {
        public object CurrentRadius;
        public object IsActive;
        public object RemainingDuration;
    }









    public struct GameEvent
    {
        public object OnTrigger;
        public object c;
        public object description;
        public object id;
        public object localScale;
        public object t;
        public object title;
    }

    public struct GatheringCompletedEvent
    {
        public object IsCrit;
        public object NodeId;
        public object NodeName;
        public object ProficiencyGained;
        public object Quantity;
    }

    public struct GatheringFailedEvent
    {
        public object FailReason;
        public object NodeId;
        public object NodeName;
    }

    public struct GatheringInterruptedEvent
    {
        public object NodeId;
        public object Reason;
    }


    public struct GatheringProgressEvent
    {
        public object Elapsed;
        public object NodeId;
        public object Progress;
    }

    public struct GatheringStartedEvent
    {
        public object NodeId;
        public object NodeName;
        public object RegionId;
        public object TotalDuration;
    }

    public struct HeartDemonAllClearedEvent
    {
        public object RemainingWillpower;
        public object ResolvedCount;
        public object TotalDemons;
    }

    public struct HeartDemonFailedEvent
    {
        public object DemonsRemaining;
        public object LastDemonType;
    }

    public struct HeartDemonResolvedEvent
    {
        public object DemonIndex;
        public object DemonType;
        public object ResolutionMethod;
        public object Success;
        public object WillpowerCost;
    }

    public struct HeartDemonSpawnedEvent
    {
        public object DemonIndex;
        public object DemonType;
        public object Description;
        public object ResolutionHint;
        public object TotalDemons;
    }

    public struct HeartDemonStageStartedEvent
    {
        public object DemonCount;
        public object DifficultyModifier;
        public object InitialWillpower;
    }

    public struct HeartDemonWillPowerChangedEvent
    {
        public object CurrentWillpower;
        public object MaxWillpower;
        public object PreviousWillpower;
        public object Reason;
    }




















    public struct PerceptionStateChangedEvent
    {
        public object CurrentRadius;
        public object IsActive;
        public object ResourcesDetected;
    }

    public struct PerfectHuntEvent
    {
        public object BossId;
        public object BossName;
        public object TotalWeaknesses;
    }













    public struct ReputationGainedEvent
    {
        public object Amount;
        public object Reason;
        public object RegionId;
    }

    public struct ResourceDepletedEvent
    {
        public object NodeId;
        public object NodeName;
        public object RespawnTimeDays;
    }

    public struct ResourceDiscoveredEvent
    {
        public object IsRare;
        public object NodeId;
        public object NodeName;
        public object RegionId;
    }


    public struct ResourceRespawnedEvent
    {
        public object NodeId;
        public object NodeName;
        public object RegionId;
    }

    public struct RiskBoundaryWarningEvent
    {
        public object Distance;
        public object ThreatType;
        public object WarningIntensity;
        public object ZoneId;
        public object ZoneName;
    }

    public struct RiskCrossingConfirmEvent
    {
        public object BaseRiskRating;
        public object RiskFactor;
        public object RiskLevel;
        public object RiskLevelName;
        public object ThreatType;
        public object ZoneId;
        public object ZoneName;
    }

    public struct RiskLevelChangedEvent
    {
        public object Color;
        public object CurrentLevel;
        public object LevelName;
        public object PreviousLevel;
        public object RiskFactor;
    }

    public struct RiskZoneEnteredEvent
    {
        public object RiskFactor;
        public object RiskLevel;
        public object ZoneId;
        public object ZoneName;
    }


















    public struct SpiritScanResultEvent
    {
        public object DetectionChance;
        public object Direction;
        public object DisplayName;
        public object Distance;
        public object Position;
    }

    public struct SpiritScanStateEvent
    {
        public object CooldownRemaining;
        public object IsActive;
        public object TotalCooldown;
    }



    public struct ThunderSplashDamageEvent
    {
        public object DamageToBarrier;
        public object RemainingBarrierDurability;
    }

    public struct ThunderStrikeDodgedEvent
    {
        public object ConsecutiveDodges;
        public object StrikeIndex;
        public object TotalPerfectDodges;
    }

    public struct ThunderStrikeStruckEvent
    {
        public object Damage;
        public object DistanceFromPlayer;
        public object PlayerHit;
        public object SplashDamage;
        public object StrikeIndex;
        public object StrikePosition;
    }

    public struct ThunderStrikeWarningEvent
    {
        public object BaseDamage;
        public object CenterPosition;
        public object StrikeIndex;
        public object TimeUntilStrike;
        public object TotalStrikes;
        public object WarningRadius;
    }

    public struct ThunderTribulationCompletedEvent
    {
        public object DaoBodyBonus;
        public object DifficultyModifier;
        public object PerfectDodges;
        public object TotalStrikes;
    }


    public struct TribulationBarrierCreatedEvent
    {
        public object MaxDurability;
        public object Radius;
    }

    public struct TribulationBarrierDamagedEvent
    {
        public object Damage;
        public object RemainingDurability;
    }

    public struct TribulationBarrierDestroyedEvent
    {
        public object TimeSurvived;
    }

    public struct TribulationCompletedEvent
    {
        public object Quality;
        public object ReadinessScore;
        public object Success;
    }

    public struct TribulationConfirmationEvent
    {
        public object EstimatedSuccessRate;
        public object PlatformId;
        public object Quality;
        public object ReadinessScore;
        public object Show;
        public object DaoBodyPenalty;
        public object PillScore;
        public object EquipScore;
        public object FormScore;
        public object EscortScore;
    }

    public struct TribulationPlatformActivatedEvent
    {
        public object PlatformId;
        public object PlayerId;
        public object Quality;
    }

    public struct TribulationPlatformQualityChangedEvent
    {
        public object NewQuality;
        public object OldQuality;
        public object PlatformId;
    }

    public struct TribulationStartedEvent
    {
        public object BarrierMaxDurability;
        public object BarrierRadius;
        public object EstimatedSuccessRate;
        public object Quality;
        public object ReadinessScore;
    }






    public struct WeaknessDiscoveredEvent
    {
        public object BossId;
        public object BossName;
        public object DamageMultiplier;
        public object DisplayName;
        public object ReconMethod;
        public object WeaknessType;
    }

    public struct WeaknessExploitEvent
    {
        public object BossId;
        public object DamageMultiplier;
        public object IsPerfectHunt;
        public object WeaknessType;
    }

    public struct WeaknessUIUpdateEvent
    {
        public object BossId;
        public object DiscoveredNames;
        public object DiscoveredTypes;
        public object TotalWeaknesses;
        public object WeaknessesDiscovered;
    }

    public struct WeaknessVFXEvent
    {
        public object BossId;
        public object DamageMultiplier;
        public object ElementType;
        public object WeaknessType;
    }

    public struct WorldAnnouncementEvent
    {
        public object Category;
        public object Message;
    }

    public struct RiskCrossingConfirmedEvent
    {
        public object ZoneId;
    }

    public struct RiskCrossingDeclinedEvent
    {
        public object ZoneId;
    }

    public struct TimeOfDayChangedEvent
    {
        public object IsNight;
    }






}