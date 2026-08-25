using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家回溯残影的运行时控制器。
/// 负责视觉采样、对象池、时间线裁剪和淡出更新。
/// </summary>
internal sealed class PlayerGhostTrailController
{
    private readonly Transform ownerTransform;
    private readonly SpriteRenderer sourceRenderer;
    private readonly float samplingMultiplier;
    private readonly int sortingOrderOffset;
    private readonly List<PlayerGhostTrailEntry> ghosts =
        new List<PlayerGhostTrailEntry>();

    private ObjectPool<PlayerGhost> ghostPool;
    private float samplingAccumulator;
    private PlayerGhostVisualState previousVisualState;
    private bool hasPreviousVisualState;
    private float lastFadeTime;

    public PlayerGhostTrailController(
        Transform ownerTransform,
        SpriteRenderer sourceRenderer,
        PlayerGhost ghostPrefab,
        int prewarmCount,
        float samplingMultiplier,
        int sortingOrderOffset)
    {
        this.ownerTransform = ownerTransform;
        this.sourceRenderer = sourceRenderer;
        this.samplingMultiplier = samplingMultiplier;
        this.sortingOrderOffset = sortingOrderOffset;

        SetupGhostPool(ghostPrefab, prewarmCount);
        lastFadeTime = Time.time;

        if (sourceRenderer != null)
        {
            previousVisualState = CaptureVisualState();
            hasPreviousVisualState = true;
        }
    }

    public void Resume()
    {
        lastFadeTime = Time.time;
    }

    public void Record(float snapshotTime, float rewindableSeconds)
    {
        Trim(snapshotTime - rewindableSeconds);
        TryCreate(snapshotTime);
    }

    public void RemoveAtOrAfter(float snapshotTime)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i].SnapshotTime >= snapshotTime)
                RemoveAt(i);
        }
    }

    public void Update(bool pauseFade, float fadeDuration)
    {
        // 残影现在是 ReincarnationManager 的子对象，父级会随玩家移动；
        // 无论是否暂停淡出（回溯中同样需要），都先钉回记录的世界位形。
        PinGhostWorldPose();

        if (pauseFade)
        {
            lastFadeTime = Time.time;
            return;
        }

        if (fadeDuration <= 0f)
        {
            Clear();
            return;
        }

        float currentTime = Time.time;
        float fadeDelta = Mathf.Max(0f, currentTime - lastFadeTime);
        lastFadeTime = currentTime;

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            PlayerGhostTrailEntry entry = ghosts[i];
            SpriteRenderer ghostRenderer = entry.Renderer;
            if (ghostRenderer == null)
            {
                RemoveAt(i);
                continue;
            }

            entry.FadeElapsed += fadeDelta;
            if (entry.FadeElapsed >= fadeDuration)
            {
                RemoveAt(i);
                continue;
            }

            Color color = entry.SourceColor;
            float remainingAlpha =
                Mathf.Clamp01(1f - entry.FadeElapsed / fadeDuration) * 0.6f;
            color.a =
                entry.SourceColor.a * remainingAlpha * remainingAlpha;
            ghostRenderer.color = color;
        }
    }

    /// <summary>
    /// 把所有残影钉回创建时记录的世界位形。
    /// 残影收纳在 ReincarnationManager 层级下，而该父级会随玩家移动（含待机呼吸缩放），
    /// 不每帧校正的话拖尾会跟着玩家跑。
    /// </summary>
    private void PinGhostWorldPose()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            PlayerGhostTrailEntry entry = ghosts[i];
            if (entry.Ghost == null)
                continue;

            Transform ghostTransform = entry.Ghost.transform;
            ghostTransform.position = entry.WorldPosition;
            ghostTransform.rotation = entry.WorldRotation;
            SetWorldScale(ghostTransform, entry.WorldScale);
        }
    }

    /// <summary>按世界缩放写入 localScale：除以父级 lossyScale，逐轴防除零。</summary>
    private void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Vector3 parentScale = ownerTransform.lossyScale;
        target.localScale = new Vector3(
            worldScale.x / Mathf.Max(Mathf.Abs(parentScale.x), 1e-05f),
            worldScale.y / Mathf.Max(Mathf.Abs(parentScale.y), 1e-05f),
            worldScale.z / Mathf.Max(Mathf.Abs(parentScale.z), 1e-05f));
    }

    public void Reset()
    {
        Clear();
        samplingAccumulator = 0f;
        hasPreviousVisualState = false;
        lastFadeTime = Time.time;
    }

    public void Dispose()
    {
        Clear();
        ghostPool?.Clear();
        ghostPool = null;
    }

    private void SetupGhostPool(PlayerGhost ghostPrefab, int prewarmCount)
    {
        PlayerGhost prefab = ghostPrefab;
        if (prefab == null)
        {
            GameObject template = new GameObject("GhostTemplate");
            template.SetActive(false);
            template.AddComponent<SpriteRenderer>();
            prefab = template.AddComponent<PlayerGhost>();
            template.transform.SetParent(ownerTransform);
        }

        ghostPool = new ObjectPool<PlayerGhost>(
            prefab,
            prewarmCount,
            ownerTransform);
    }

    private void TryCreate(float snapshotTime)
    {
        if (sourceRenderer == null)
            return;

        float clampedSampling = Mathf.Clamp01(samplingMultiplier);
        PlayerGhostVisualState currentState = CaptureVisualState();
        bool hasChanged =
            !hasPreviousVisualState ||
            !currentState.Equals(previousVisualState);

        previousVisualState = currentState;
        hasPreviousVisualState = true;

        if (!hasChanged || clampedSampling <= 0f)
            return;

        samplingAccumulator += clampedSampling;
        if (samplingAccumulator < 1f)
            return;

        samplingAccumulator -= 1f;
        CreateGhost(snapshotTime, currentState);
    }

    private PlayerGhostVisualState CaptureVisualState()
    {
        Transform sourceTransform = sourceRenderer.transform;
        return new PlayerGhostVisualState
        {
            Position = sourceTransform.position,
            Rotation = sourceTransform.rotation,
            Scale = sourceTransform.lossyScale,
            Sprite = sourceRenderer.sprite,
            Color = sourceRenderer.color,
            FlipX = sourceRenderer.flipX,
            FlipY = sourceRenderer.flipY
        };
    }

    private void CreateGhost(
        float snapshotTime,
        PlayerGhostVisualState visualState)
    {
        PlayerGhost ghost = ghostPool?.Get();
        if (ghost == null || ghost.Renderer == null)
            return;

        Transform ghostTransform = ghost.transform;
        // 残影统一收纳在 ReincarnationManager 下，不再散落到场景根；
        // 父级会随玩家移动/缩放，世界位形由 PinGhostWorldPose 每帧钉住。
        ghostTransform.SetParent(ownerTransform);
        ghostTransform.position = visualState.Position;
        ghostTransform.rotation = visualState.Rotation;
        SetWorldScale(ghostTransform, visualState.Scale);

        SpriteRenderer ghostRenderer = ghost.Renderer;
        ghostRenderer.sprite = visualState.Sprite;
        ghostRenderer.color = visualState.Color;
        ghostRenderer.flipX = visualState.FlipX;
        ghostRenderer.flipY = visualState.FlipY;
        ghostRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        ghostRenderer.sortingOrder =
            sourceRenderer.sortingOrder + sortingOrderOffset;
        ghostRenderer.drawMode = sourceRenderer.drawMode;
        ghostRenderer.size = sourceRenderer.size;
        ghostRenderer.maskInteraction = sourceRenderer.maskInteraction;
        ghostRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;

        ghosts.Add(new PlayerGhostTrailEntry
        {
            SnapshotTime = snapshotTime,
            SourceColor = visualState.Color,
            WorldPosition = visualState.Position,
            WorldRotation = visualState.Rotation,
            WorldScale = visualState.Scale,
            Ghost = ghost
        });
    }

    private void Trim(float expireBefore)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i].SnapshotTime < expireBefore)
                RemoveAt(i);
        }
    }

    private void RemoveAt(int index)
    {
        PlayerGhostTrailEntry entry = ghosts[index];
        ghosts.RemoveAt(index);

        if (entry.Ghost == null)
            return;

        if (ghostPool != null)
            ghostPool.Return(entry.Ghost);
        else
            Object.Destroy(entry.Ghost.gameObject);
    }

    private void Clear()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
            RemoveAt(i);
    }
}
