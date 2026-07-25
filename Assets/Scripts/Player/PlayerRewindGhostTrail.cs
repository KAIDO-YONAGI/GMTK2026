using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家回溯残影表现。
/// 单向订阅 ReincarnationManager 的时间线事件，不参与回溯判定或快照存储。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ReincarnationManager))]
public sealed class PlayerRewindGhostTrail : MonoBehaviour
{
    [Header("Ghost Trail")]
    [Tooltip("Sampling rate relative to changed rewind snapshots. 1 records every change; 0 disables ghosts.")]
    [SerializeField, Range(0f, 1f)] private float samplingMultiplier = 1f;

    [Tooltip("SpriteRenderer used as the visual source.")]
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Tooltip("Sorting order offset relative to the source renderer.")]
    [SerializeField] private int sortingOrderOffset = -1;

    [Tooltip("Prefab used by the ghost object pool. A runtime template is created when unassigned.")]
    [SerializeField] private PlayerGhost ghostPrefab;

    [Tooltip("Number of ghost objects created when the pool initializes.")]
    [SerializeField, Min(0)] private int prewarmCount = 20;

    private struct GhostVisualState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Sprite sprite;
        public Color color;
        public bool flipX;
        public bool flipY;

        public bool Equals(GhostVisualState other)
        {
            return position == other.position
                && rotation == other.rotation
                && scale == other.scale
                && sprite == other.sprite
                && color == other.color
                && flipX == other.flipX
                && flipY == other.flipY;
        }
    }

    private sealed class GhostSnapshot
    {
        public float snapshotTime;
        public float fadeElapsed;
        public Color sourceColor;
        public PlayerGhost ghost;
        public SpriteRenderer Renderer => ghost != null ? ghost.Renderer : null;
    }

    private readonly List<GhostSnapshot> ghosts = new List<GhostSnapshot>();

    private ReincarnationManager rewindSource;
    private ObjectPool<PlayerGhost> ghostPool;
    private float samplingAccumulator;
    private GhostVisualState previousVisualState;
    private bool hasPreviousVisualState;
    private float lastFadeTime;

    private void Awake()
    {
        rewindSource = GetComponent<ReincarnationManager>();
        if (sourceRenderer == null)
            sourceRenderer = transform.root.GetComponent<SpriteRenderer>();

        lastFadeTime = Time.time;
        if (sourceRenderer != null)
        {
            previousVisualState = CaptureVisualState();
            hasPreviousVisualState = true;
        }

        SetupGhostPool();
    }

    private void OnEnable()
    {
        Subscribe();
        lastFadeTime = Time.time;
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetTrail();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ClearGhosts();
        ghostPool?.Clear();
    }

    private void Update()
    {
        UpdateGhosts();
    }

    private void Subscribe()
    {
        if (rewindSource == null)
            rewindSource = GetComponent<ReincarnationManager>();
        if (rewindSource == null)
            return;

        rewindSource.SnapshotRecorded -= OnSnapshotRecorded;
        rewindSource.SnapshotApplied -= OnSnapshotApplied;
        rewindSource.HistoryCleared -= ResetTrail;

        rewindSource.SnapshotRecorded += OnSnapshotRecorded;
        rewindSource.SnapshotApplied += OnSnapshotApplied;
        rewindSource.HistoryCleared += ResetTrail;
    }

    private void Unsubscribe()
    {
        if (rewindSource == null)
            return;

        rewindSource.SnapshotRecorded -= OnSnapshotRecorded;
        rewindSource.SnapshotApplied -= OnSnapshotApplied;
        rewindSource.HistoryCleared -= ResetTrail;
    }

    private void SetupGhostPool()
    {
        PlayerGhost prefab = ghostPrefab;
        if (prefab == null)
        {
            GameObject template = new GameObject("GhostTemplate");
            template.SetActive(false);
            template.AddComponent<SpriteRenderer>();
            prefab = template.AddComponent<PlayerGhost>();
            template.transform.SetParent(transform);
        }

        ghostPool = new ObjectPool<PlayerGhost>(prefab, prewarmCount, transform);
    }

    private void OnSnapshotRecorded(float snapshotTime, float rewindableSeconds)
    {
        TrimGhosts(snapshotTime - rewindableSeconds);
        TryRecordGhost(snapshotTime);
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        RemoveGhostsAtOrAfter(snapshotTime);
    }

    private void TryRecordGhost(float snapshotTime)
    {
        if (sourceRenderer == null)
            return;

        float clampedSampling = Mathf.Clamp01(samplingMultiplier);
        GhostVisualState currentState = CaptureVisualState();
        bool hasChanged = !hasPreviousVisualState || !currentState.Equals(previousVisualState);

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

    private GhostVisualState CaptureVisualState()
    {
        Transform sourceTransform = sourceRenderer.transform;
        return new GhostVisualState
        {
            position = sourceTransform.position,
            rotation = sourceTransform.rotation,
            scale = sourceTransform.lossyScale,
            sprite = sourceRenderer.sprite,
            color = sourceRenderer.color,
            flipX = sourceRenderer.flipX,
            flipY = sourceRenderer.flipY
        };
    }

    private void CreateGhost(float snapshotTime, GhostVisualState state)
    {
        PlayerGhost ghost = ghostPool?.Get();
        if (ghost == null || ghost.Renderer == null)
            return;

        Transform ghostTransform = ghost.transform;
        ghostTransform.SetParent(null);
        ghostTransform.position = state.position;
        ghostTransform.rotation = state.rotation;
        ghostTransform.localScale = state.scale;

        SpriteRenderer ghostRenderer = ghost.Renderer;
        ghostRenderer.sprite = state.sprite;
        ghostRenderer.color = state.color;
        ghostRenderer.flipX = state.flipX;
        ghostRenderer.flipY = state.flipY;
        ghostRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        ghostRenderer.drawMode = sourceRenderer.drawMode;
        ghostRenderer.size = sourceRenderer.size;
        ghostRenderer.maskInteraction = sourceRenderer.maskInteraction;
        ghostRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;

        ghosts.Add(new GhostSnapshot
        {
            snapshotTime = snapshotTime,
            sourceColor = state.color,
            ghost = ghost
        });
    }

    private void UpdateGhosts()
    {
        if (rewindSource == null || rewindSource.IsRewinding)
        {
            lastFadeTime = Time.time;
            return;
        }

        float fadeDuration = rewindSource.AgeLimitedRewindableSeconds;
        if (fadeDuration <= 0f)
        {
            ClearGhosts();
            return;
        }

        float currentTime = Time.time;
        float fadeDelta = Mathf.Max(0f, currentTime - lastFadeTime);
        lastFadeTime = currentTime;

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            GhostSnapshot ghost = ghosts[i];
            SpriteRenderer ghostRenderer = ghost.Renderer;
            if (ghostRenderer == null)
            {
                RemoveGhostAt(i);
                continue;
            }

            ghost.fadeElapsed += fadeDelta;
            if (ghost.fadeElapsed >= fadeDuration)
            {
                RemoveGhostAt(i);
                continue;
            }

            Color color = ghost.sourceColor;
            float remainingAlpha = Mathf.Clamp01(1f - ghost.fadeElapsed / fadeDuration) * 0.6f;
            color.a = ghost.sourceColor.a * remainingAlpha * remainingAlpha;
            ghostRenderer.color = color;
        }
    }

    private void TrimGhosts(float expireBefore)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i].snapshotTime < expireBefore)
                RemoveGhostAt(i);
        }
    }

    private void RemoveGhostsAtOrAfter(float time)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i].snapshotTime >= time)
                RemoveGhostAt(i);
        }
    }

    private void RemoveGhostAt(int index)
    {
        GhostSnapshot ghost = ghosts[index];
        ghosts.RemoveAt(index);

        if (ghost.ghost == null)
            return;

        if (ghostPool != null)
            ghostPool.Return(ghost.ghost);
        else
            Destroy(ghost.ghost.gameObject);
    }

    private void ClearGhosts()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
            RemoveGhostAt(i);
    }

    private void ResetTrail()
    {
        ClearGhosts();
        samplingAccumulator = 0f;
        hasPreviousVisualState = false;
        lastFadeTime = Time.time;
    }
}
