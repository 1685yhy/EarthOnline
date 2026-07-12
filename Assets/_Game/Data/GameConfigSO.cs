using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "BounceTower/GameConfig")]
public class GameConfigSO : ScriptableObject
{
    [Header("Block")]
    public float blockHeight = 1.2f;
    public float initialBlockWidth = 2.8f;
    public float perfectTolerance = 0.15f;
    public float baseSpeed = 5f;

    [Header("Stack")]
    public float stackStartY = 1.5f;  // upper portion — stack visible at top
    public float cameraFollowSpeed = 4f;

    [Header("Level")]
    public int maxLevels = 30;
}
