using UnityEngine;
using System.Collections.Generic;

public class StackManager : MonoBehaviour
{
    [SerializeField] private GameConfigSO config;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Transform stackContainer;

    private List<StackedBlock> stack = new List<StackedBlock>();
    private Stack<StackedBlock> undoStack = new Stack<StackedBlock>();

    public struct StackedBlock
    {
        public GameObject gameObject;
        public float centerX;
        public float yPosition;
        public float width;
        public Color color;
        public int previousCombo;
    }

    /// <summary>
    /// Ensure a block prefab exists. Creates a simple white quad if none assigned.
    /// </summary>
    private GameObject EnsurePrefab()
    {
        if (blockPrefab != null) return blockPrefab;
        blockPrefab = new GameObject("Block_Default", typeof(SpriteRenderer));
        var sr = blockPrefab.GetComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100);
        sr.sortingOrder = 0;
        blockPrefab.SetActive(false);
        DontDestroyOnLoad(blockPrefab);
        return blockPrefab;
    }

    public int LayerCount => stack.Count;
    public float TopY => stack.Count > 0 ? stack[stack.Count - 1].yPosition : (config != null ? config.stackStartY : 1.5f);
    public float TopWidth => stack.Count > 0 ? stack[stack.Count - 1].width : (config != null ? config.initialBlockWidth : 2.8f);
    public float TopX => stack.Count > 0 ? stack[stack.Count - 1].centerX : 0f;

    public float GetTopY() => TopY;
    public float GetTopWidth() => TopWidth;
    public float GetTopX() => TopX;

    public void PlaceBlock(float blockCenterX, float blockWidth, float blockY, GameObject blockObject, Color paletteColor)
    {
        EnsurePrefab();
        float bh = config != null ? config.blockHeight : 1.2f; // fallback default
        float tx = TopX;
        float tw = TopWidth;
        float ty = TopY + bh;

        // Calculate overlap
        float blockLeft = blockCenterX - blockWidth / 2f;
        float blockRight = blockCenterX + blockWidth / 2f;
        float topLeft = tx - tw / 2f;
        float topRight = tx + tw / 2f;

        float overlapLeft = Mathf.Max(blockLeft, topLeft);
        float overlapRight = Mathf.Min(blockRight, topRight);
        float overlapWidth = overlapRight - overlapLeft;

        float perfectTol = config != null ? config.perfectTolerance : 0.15f;
        bool isPerfect = Mathf.Abs(blockCenterX - tx) <= perfectTol;

        // No overlap = game over
        if (overlapWidth <= 0.001f)
        {
            // Spawn falling block piece
            SpawnFallingPiece(blockCenterX, blockY, blockWidth, paletteColor, 0);
            // Fail particle effect
            ParticleManager.Instance?.EmitFail(new Vector3(blockCenterX, blockY, 0));
            if (blockObject != null) Destroy(blockObject);
            GameManager.Instance.OnBlockMissed();
            return;
        }

        // Spawn leftover pieces
        if (blockLeft < topLeft)
        {
            float leftoverW = topLeft - blockLeft;
            float leftoverX = blockLeft + leftoverW / 2f;
            SpawnFallingPiece(leftoverX, ty, leftoverW, paletteColor, -1);
        }
        if (blockRight > topRight)
        {
            float leftoverW = blockRight - topRight;
            float leftoverX = blockRight - leftoverW / 2f;
            SpawnFallingPiece(leftoverX, ty, leftoverW, paletteColor, 1);
        }

        // Create placed block — match original game mechanics
        // cutLoss: non-perfect gets additional width penalty
        float cutLoss = isPerfect ? 0f : Mathf.Abs(blockCenterX - tx) * 0.3f;
        float finalWidth = Mathf.Max(0.3f, overlapWidth - cutLoss);
        float initWidth = config != null ? config.initialBlockWidth : 2.8f;
        // Combo bonus: combo >= 3 makes block wider (reward for skill)
        int combo = GameManager.Instance.CurrentCombo;
        if (isPerfect && combo >= 3) {
            float bonus = Mathf.Min(combo * 0.03f, 0.4f);
            finalWidth = Mathf.Min(finalWidth + bonus, initWidth * 1.1f);
        }
        float finalX = overlapLeft + (overlapWidth - finalWidth) / 2f;

        GameObject placed = Instantiate(blockPrefab,
            new Vector3(finalX, ty, 0), Quaternion.identity, stackContainer);
        placed.transform.localScale = new Vector3(finalWidth, bh, 1);
        BlockSpawner.ApplyBlockStyle(placed, paletteColor);

        // Cleanup
        if (blockObject != null) Destroy(blockObject);

        // Track undo (save combo before placement for restoration)
        if (stack.Count > 0)
        {
            var prevBlock = stack[stack.Count - 1];
            prevBlock.previousCombo = GameManager.Instance.CurrentCombo;
            undoStack.Push(prevBlock);
        }

        stack.Add(new StackedBlock
        {
            gameObject = placed,
            centerX = finalX,
            yPosition = ty,
            width = finalWidth,
            color = paletteColor
        });

        GameManager.Instance.OnBlockPlaced(stack.Count, isPerfect, overlapWidth);

        // BGM intensity based on current combo level
        AudioManager.Instance?.SetBGMIntensity(Mathf.Clamp01(GameManager.Instance.CurrentCombo / 20f));

        // Trigger effects — matching original game
        if (isPerfect)
        {
            ParticleManager.Instance?.EmitPerfect(new Vector3(finalX, ty, 0), paletteColor);
            ScreenShake.Instance?.Trigger(0.12f, 0.12f);
            BlockAnimator.Instance?.PlayPlaceAnimation(placed, true);
            // Perfect SFX with rising pitch based on combo
            float pitch = 1f + combo * 0.03f;
            AudioManager.Instance?.PlaySFX(SfxType.Perfect, Mathf.Min(pitch, 1.5f));
            if (combo >= 5)
            {
                SfxType comboType = combo >= 20 ? SfxType.Combo20 : combo >= 10 ? SfxType.Combo10 : SfxType.Combo5;
                AudioManager.Instance?.PlaySFX(comboType);
                ParticleManager.Instance?.EmitCombo(new Vector3(finalX, ty, 0), combo, paletteColor);
                BlockAnimator.Instance?.PlayComboPulse(placed, combo);
            }
        }
        else
        {
            BlockAnimator.Instance?.PlayPlaceAnimation(placed, false);
            AudioManager.Instance?.PlaySFX(SfxType.Place, 0.9f + Random.value * 0.2f); // Slight pitch variation
            // Original shake formula: max(0, 10 - overlapWidth * 0.6)
            float shakeAmount = Mathf.Max(0, 0.3f - overlapWidth * 0.02f);
            if (shakeAmount > 0.01f) ScreenShake.Instance?.Trigger(shakeAmount, 0.15f);
            // Small overlap → danger shake
            if (overlapWidth > 0 && overlapWidth < tw * 0.15f)
                ScreenShake.Instance?.Trigger(0.2f, 0.3f);
        }
    }

    private void SpawnFallingPiece(float x, float y, float w, Color c, int direction)
    {
        EnsurePrefab();
        float bh = config != null ? config.blockHeight : 1.2f;
        GameObject piece = Instantiate(blockPrefab,
            new Vector3(x, y, 0), Quaternion.identity, stackContainer);
        piece.transform.localScale = new Vector3(w, bh, 1);
        BlockSpawner.ApplyBlockStyle(piece, c);

        var fb = piece.AddComponent<FallingBlock>();
        fb.Init(direction);

        // Auto-destroy
        Destroy(piece, 3f);
    }

    public void ClearStack()
    {
        foreach (var sb in stack)
            if (sb.gameObject != null) Destroy(sb.gameObject);
        stack.Clear();
        undoStack.Clear();

        // Clean up all falling pieces
        if (blockPrefab != null)
        {
            var pieces = FindObjectsOfType<FallingBlock>();
            foreach (var fb in pieces)
                if (fb != null && fb.gameObject != null) Destroy(fb.gameObject);
        }
    }

    public void ResetTopWidth()
    {
        if (stack.Count > 0)
        {
            var top = stack[stack.Count - 1];
            if (top.gameObject != null) Destroy(top.gameObject);

            float newWidth = config != null ? config.initialBlockWidth : 2.8f;
            float newX = Mathf.Clamp(top.centerX, -5f + newWidth / 2f, 5f - newWidth / 2f);

            GameObject go = Instantiate(blockPrefab,
                new Vector3(newX, top.yPosition, 0), Quaternion.identity, stackContainer);
            go.transform.localScale = new Vector3(newWidth, config != null ? config.blockHeight : 1.2f, 1);
            BlockSpawner.ApplyBlockStyle(go, top.color);

            stack[stack.Count - 1] = new StackedBlock
            {
                gameObject = go, centerX = newX,
                yPosition = top.yPosition, width = newWidth, color = top.color
            };

            ParticleManager.Instance?.EmitPerfect(new Vector3(newX, top.yPosition, 0), Color.yellow);
        }
    }

    public void UndoLastPlacement()
    {
        if (undoStack.Count == 0 || stack.Count == 0) return;
        var current = stack[stack.Count - 1];
        var previous = undoStack.Pop();

        if (current.gameObject != null) Destroy(current.gameObject);
        stack.RemoveAt(stack.Count - 1);

        // Restore combo to value before this placement
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCombo(previous.previousCombo);
            GameManager.Instance.DecrementScore();
        }

        // Re-spawn the moving block to match the new top width
        var bs = FindObjectOfType<BlockSpawner>();
        if (bs != null)
        {
            bs.ClearCurrentBlock();
            float newWidth = GetTopWidth();
            bs.Configure(newWidth, bs.BaseSpeed, false, null, 0);
            bs.SpawnFirstBlock();
        }
    }
}
