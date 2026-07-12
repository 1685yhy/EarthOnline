using UnityEngine;

public class BlockSpawner : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Transform blockContainer;

    private GameObject currentBlock;
    private Color currentBlockColor;
    private float moveSpeed = 3f;
    private float baseSpeed = 3f;
    private int direction = 1;
    private float blockWidth = 4.5f;
    private float speedMultiplier = 1f;
    private float freezeTimer = 0f;
    private bool slowMotionActive = false;

    // Internal speed curve for hard levels
    private bool useInternalCurve = false;
    private AnimationCurve speedCurve;
    private int targetLayers;

    public GameObject CurrentBlock => currentBlock;
    public float BlockWidth => blockWidth;
    public float CurrentSpeed => moveSpeed;
    public float BaseSpeed => baseSpeed;

    /// <summary>
    /// Ensure a block prefab exists. Creates a simple white quad if none assigned.
    /// </summary>
    private void EnsurePrefab()
    {
        if (blockPrefab != null) return;
        blockPrefab = new GameObject("Block_Default", typeof(SpriteRenderer));
        var sr = blockPrefab.GetComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100);
        sr.sortingOrder = 0;
        blockPrefab.SetActive(false);
        DontDestroyOnLoad(blockPrefab);
    }

    public void Configure(float width, float speed, bool useCurve, AnimationCurve curve, int target)
    {
        blockWidth = width;
        baseSpeed = speed;
        moveSpeed = speed;
        useInternalCurve = useCurve;
        speedCurve = curve;
        targetLayers = target;
        speedMultiplier = 1f;
        slowMotionActive = false;
        freezeTimer = 0f;
    }

    /// <summary>
    /// Stack-centered movement bounds (H5 swBounds formula).
    /// Level 1: extraMul=0.05 (narrow zone, almost can't miss)
    /// Level 2+: extraMul=2.5 (wide zone, movement goes far past stack edges)
    /// </summary>
    public (float minCenter, float maxCenter) CalculateBounds(float bw)
    {
        float topCenterX = GameManager.Instance.StackManager.GetTopX();
        float topWidth = GameManager.Instance.StackManager.GetTopWidth();
        float halfStack = topWidth * 0.5f;
        float cx = topCenterX;

        // Original H5: extraMul = levelId === 1 ? 0.05 : 2.5
        int levelId = GameManager.Instance.CurrentLevelId;
        float extraMul = levelId == 1 ? 0.05f : 2.5f;
        float extra = topWidth * extraMul;

        // Original: minEdge = cx - hs - hb - extra, maxEdge = cx + hs + hb + extra
        // Unity center bounds: minCenter = minEdge + hb, maxCenter = maxEdge - hb
        float minCenter = cx - halfStack - extra;
        float maxCenter = cx + halfStack + extra;

        return (minCenter, maxCenter);
    }

    public void ResetForLevel()
    {
        if (currentBlock != null) { Destroy(currentBlock); currentBlock = null; }
        speedMultiplier = 1f;
        slowMotionActive = false;
        freezeTimer = 0f;
    }

    public void SpawnFirstBlock()
    {
        EnsurePrefab();
        if (currentBlock != null) Destroy(currentBlock);
        if (GameManager.Instance == null)
        { Debug.LogError("GameManager is null in SpawnFirstBlock"); return; }
        // Block spawns at bottom of screen (camera-relative), flies up to hit stack at top
        float spawnY = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.2f, 0)).y;

        var bounds = CalculateBounds(blockWidth);
        bool fromRight = Random.value > 0.5f;
        Vector3 pos = new Vector3(fromRight ? bounds.maxCenter : bounds.minCenter, spawnY, 0);
        currentBlock = Instantiate(blockPrefab, pos, Quaternion.identity, blockContainer);
        currentBlock.transform.localScale = new Vector3(blockWidth, GameManager.Instance.BlockHeight, 1);

        // Textured block via BlockTextureGen
        currentBlockColor = GetRandomColor();
        ApplyBlockStyle(currentBlock, currentBlockColor);

        direction = fromRight ? -1 : 1;
    }

    public void SpawnNextBlock(float newWidth, float newSpeed)
    {
        EnsurePrefab();
        blockWidth = newWidth;
        moveSpeed = newSpeed * (slowMotionActive ? 0.3f : 1f);

        if (currentBlock != null) Destroy(currentBlock);

        if (GameManager.Instance == null)
        { Debug.LogError("GameManager is null in SpawnNextBlock"); return; }
        // Block spawns at bottom of screen (camera-relative), flies up to hit stack at top
        float spawnY = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.2f, 0)).y;
        var bounds = CalculateBounds(blockWidth);
        bool fromRight = Random.value > 0.5f;
        Vector3 pos = new Vector3(fromRight ? bounds.maxCenter : bounds.minCenter, spawnY, 0);
        currentBlock = Instantiate(blockPrefab, pos, Quaternion.identity, blockContainer);
        currentBlock.transform.localScale = new Vector3(blockWidth, GameManager.Instance.BlockHeight, 1);

        // Textured block via BlockTextureGen
        currentBlockColor = GetRandomColor();
        ApplyBlockStyle(currentBlock, currentBlockColor);

        direction = fromRight ? -1 : 1;
    }

    public (float width, float speed) GetNextBlockParams(int currentScore)
    {
        // Width follows the current stack top (original mechanic)
        float newWidth = GameManager.Instance.StackManager.GetTopWidth();

        // Speed increases with layers stacked (original formula)
        // speed = (baseSpeed + score * speedIncrement) * levelMul
        float speedIncrement = baseSpeed * 0.04f; // ~4% per layer
        float speed = (baseSpeed + currentScore * speedIncrement);
        if (useInternalCurve && targetLayers > 0)
            speed *= speedCurve.Evaluate((float)currentScore / targetLayers);
        return (newWidth, speed);
    }

    private void Update()
    {
        if (currentBlock == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        // Freeze effect
        if (freezeTimer > 0)
        {
            freezeTimer -= Time.deltaTime;
            return;
        }

        float effectiveSpeed = moveSpeed * speedMultiplier;
        Vector3 pos = currentBlock.transform.position;
        pos.x += effectiveSpeed * direction * Time.deltaTime;

        // Stack-centered movement bounds (H5 swBounds)
        var bounds = CalculateBounds(blockWidth);
        if (pos.x <= bounds.minCenter) { pos.x = bounds.minCenter; direction = 1; }
        if (pos.x >= bounds.maxCenter) { pos.x = bounds.maxCenter; direction = -1; }

        currentBlock.transform.position = pos;
    }

    public void ReverseDirection() => direction *= -1;
    public void SetSlowMotion(bool active) => slowMotionActive = active;
    public void SetSpeedMultiplier(float mul) => speedMultiplier = mul;

    public void Freeze(float duration)
    {
        freezeTimer = duration;
    }

    public Vector3 GetCurrentBlockPosition()
    {
        return currentBlock != null ? currentBlock.transform.position : Vector3.zero;
    }

    public void ClearCurrentBlock()
    {
        if (currentBlock != null) { Destroy(currentBlock); currentBlock = null; }
    }

    // Default 6-color palette for blocks (糖果乐园 theme default)
    private Color[] blockPalette = new Color[] {
        new Color(1f, 0.329f, 0.439f),     // #FF5470 coral
        new Color(0f, 0.808f, 0.788f),     // #00CEC9 teal
        new Color(0.424f, 0.361f, 0.906f), // #6C5CE7 violet
        new Color(0.992f, 0.796f, 0.431f), // #FDCB6E gold
        new Color(0f, 0.722f, 0.580f),     // #00B894 emerald
        new Color(0.992f, 0.475f, 0.659f), // #FD79A8 pink
    };
    private int _colorIdx = 0;

    /// <summary>
    /// Update the block color palette (called by ThemeManager on theme change).
    /// </summary>
    public void SetBlockPalette(Color[] colors)
    {
        if (colors != null && colors.Length > 0)
        {
            blockPalette = colors;
            _colorIdx = 0;
        }
    }

    private Color GetRandomColor()
    {
        return blockPalette[(_colorIdx++) % blockPalette.Length];
    }

    /// <summary>
    /// Apply flat solid color to block — no texture generation.
    /// </summary>
    public static void ApplyBlockStyle(GameObject block, Color color)
    {
        var sr = block.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        sr.color = color;
    }

    // Called by GameManager during Dropping state resolution
    public void OnDropComplete()
    {
        if (currentBlock == null) return;
        Vector3 blockPos = currentBlock.transform.position;
        float bw = currentBlock.transform.localScale.x;
        GameManager.Instance.StackManager.PlaceBlock(blockPos.x, bw, blockPos.y, currentBlock, currentBlockColor);
        // currentBlock is destroyed inside PlaceBlock, then a new one is spawned
        // by SpawnNextBlock called from OnBlockPlaced
    }
}
