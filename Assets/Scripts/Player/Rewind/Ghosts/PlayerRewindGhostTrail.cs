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

    private ReincarnationManager rewindSource;
    private PlayerGhostTrailController trailController;

    private void Awake()
    {
        rewindSource = GetComponent<ReincarnationManager>();
        if (sourceRenderer == null)
            sourceRenderer = transform.root.GetComponent<SpriteRenderer>();

        trailController = new PlayerGhostTrailController(
            transform,
            sourceRenderer,
            ghostPrefab,
            prewarmCount,
            samplingMultiplier,
            sortingOrderOffset);
    }

    private void OnEnable()
    {
        Subscribe();
        trailController?.Resume();
    }

    private void OnDisable()
    {
        Unsubscribe();
        trailController?.Reset();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        trailController?.Dispose();
        trailController = null;
    }

    private void Update()
    {
        bool pauseFade = rewindSource == null || rewindSource.IsRewinding;
        float fadeDuration = rewindSource != null
            ? rewindSource.AgeLimitedRewindableSeconds
            : 0f;
        trailController?.Update(pauseFade, fadeDuration);
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

    private void OnSnapshotRecorded(float snapshotTime, float rewindableSeconds)
    {
        trailController?.Record(snapshotTime, rewindableSeconds);
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        trailController?.RemoveAtOrAfter(snapshotTime);
    }

    private void ResetTrail()
    {
        trailController?.Reset();
    }
}
