using UnityEngine;
using UnityEngine.UI;

public class LevelSelectPanel : MonoBehaviour
{
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Transform gridContainer;

    private void OnEnable() => BuildGrid();

    public void BuildGrid()
    {
        // Clear existing - use while loop to avoid skipping children during destruction
        while (gridContainer.childCount > 0)
            DestroyImmediate(gridContainer.GetChild(0).gameObject);

        int maxUnlocked = SaveManager.Instance != null
            ? SaveManager.Instance.Current.MaxUnlockedLevel()
            : 1;

        for (int i = 1; i <= 30; i++)
        {
            var btn = Instantiate(levelButtonPrefab, gridContainer);
            var btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = i.ToString();

            bool unlocked = i <= maxUnlocked;
            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = unlocked;
                int levelId = i; // capture for closure
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLevelSelected(levelId));
            }

            // Stars display
            int stars = SaveManager.Instance != null
                ? SaveManager.Instance.Current.GetLevelStars(i) : 0;
            var starText = btn.transform.Find("Stars")?.GetComponent<Text>();
            if (starText != null && stars > 0)
                starText.text = new string('⭐', stars);
        }
    }

    private void OnLevelSelected(int levelId)
    {
        var level = LevelManager.Instance.GetLevel(levelId);
        if (level != null)
        {
            GameManager.Instance.StartLevel(
                level.levelId, level.targetLayers, level.initialBlockWidth,
                level.baseSpeed, level.useInternalCurve, level.speedCurve);
            gameObject.SetActive(false);
        }
    }
}
