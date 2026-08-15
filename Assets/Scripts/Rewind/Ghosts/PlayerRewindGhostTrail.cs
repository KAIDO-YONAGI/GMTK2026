using UnityEngine;

/// <summary>
/// 玩家回溯残影表现。
/// 单向订阅 ReincarnationManager 的时间线事件，不参与回溯判定或快照存储。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ReincarnationManager))]
public sealed class PlayerRewindGhostTrail : MonoBehaviour
{
    [Header("回溯残影")]
    [Tooltip("相对于回溯快照变化的采样比例。1 表示每次变化都记录；0 表示不生成残影。")]
    [SerializeField, Range(0f, 1f)] private float samplingMultiplier = 1f;

    [Tooltip("用作残影视觉来源的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Tooltip("相对于源 SpriteRenderer 的排序层级偏移。")]
    [SerializeField] private int sortingOrderOffset = -1;

    [Tooltip("残影对象池使用的预制件。未指定时会在运行时创建模板。")]
    [SerializeField] private PlayerGhost ghostPrefab;

    [Tooltip("对象池初始化时创建的残影对象数量。")]
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
