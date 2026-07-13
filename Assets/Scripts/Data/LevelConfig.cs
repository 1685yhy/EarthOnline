using UnityEngine;

namespace StackingCute
{
    [CreateAssetMenu(fileName = "LevelConfig_01", menuName = "StackingCute/LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        [Header("Basic")]
        public int LevelId = 1;
        public int TargetLayers = 3;
        [Range(0.3f, 2.5f)]
        public float SpeedMultiplier = 0.5f;

        [Header("Advanced (Level 3+)")]
        [Tooltip("After this layer, move range increases.")]
        public int RangeIncreaseAfterLayer = 10;
        [Range(0f, 0.5f)]
        public float RangeIncreasePercent = 0.2f;

        [Header("Reverse (Level 4+)")]
        [Tooltip("After this layer, occasional direction reversal.")]
        public int ReverseAfterLayer = 15;
        [Range(0f, 0.1f)]
        public float ReverseChancePerLayer = 0.03f;

        [Header("Speed Ramp (Level 5)")]
        public float SpeedRampPerLayers = 0.2f;
        public int SpeedRampInterval = 10;

        [Header("Narrow Layer (Level 5)")]
        [Range(0f, 0.1f)]
        public float NarrowChance = 0.05f;
        [Range(0.3f, 0.8f)]
        public float NarrowWidthRatio = 0.6f;

        public float GetSpeedForLayer(int layer)
        {
            float speed = SpeedMultiplier;
            if (SpeedRampInterval > 0 && layer > SpeedRampInterval)
            {
                int steps = (layer - 1) / SpeedRampInterval;
                speed += steps * SpeedRampPerLayers;
            }
            return Mathf.Clamp(speed, 0.3f, 2.5f);
        }

        public float GetRangeForLayer(int layer)
        {
            float range = 3f;
            if (layer > RangeIncreaseAfterLayer)
                range *= (1f + RangeIncreasePercent);
            return range;
        }

        public bool ShouldSpawnNarrow(int layer)
        {
            return Random.value < NarrowChance;
        }
    }
}