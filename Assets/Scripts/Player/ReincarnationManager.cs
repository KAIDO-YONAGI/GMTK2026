using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家轮回回溯的编排层。
/// 负责输入、时间窗口、倒放生命周期和对外事件，不直接管理玩家状态、快照容器或残影表现。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerRewindTarget))]
public class ReincarnationManager : RewindableBase
{
    public static ReincarnationManager Instance { get; private set; }

    [Header("Reincarnation Events")]
    [SerializeField] private VoidEventSO reincarnationEvent;

    [Tooltip("Raised when rewind actually begins.")]
    [SerializeField] private VoidEventSO reincarnationBegEvent;

    [Tooltip("Raised when rewind actually ends, before cooldown starts.")]
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    [Tooltip("Raised after each player snapshot is applied.")]
    [SerializeField] private FloatEventSO snapshotAppliedEvent;

    [Header("Rewind Time Window")]
    [Tooltip("Maximum rewind seconds when the player is fully young.")]
    [SerializeField] private float maxRewindBaseSeconds = 8f;

    [Tooltip("Minimum rewind seconds when the player is fully aged.")]
    [SerializeField] private float minRewindBaseSeconds = 1f;

    [Tooltip("Multiplier shared by snapshot retention and rewind playback.")]
    [SerializeField] private float rewindWindowMultiplier = 1f;

    [Header("Rewind Playback")]
    [FormerlySerializedAs("rewindSpeed")]
    [Tooltip("Recorded snapshot seconds consumed per real second.")]
    [SerializeField, Min(0f)] private float rewindConsumptionPerSecond = 1f;

    [Tooltip("Cooldown before new snapshots can be recorded after rewind.")]
    [SerializeField, Min(0f)] private float rewindCooldownSeconds = 1f;

    private readonly PlayerRewindHistory history = new PlayerRewindHistory();

    private PlayerRewindTarget rewindTarget;
    private float rewindLimitForCurrentOperation;
    private VoidEventSO subscribedSceneLoadedEvent;

    /// <summary>记录快照后广播：快照时间戳、当时的保留窗口。</summary>
    public event Action<float, float> SnapshotRecorded;

    /// <summary>应用快照后广播该快照时间戳。</summary>
    public event Action<float> SnapshotApplied;

    /// <summary>场景切换、禁用或回溯消费完成时广播时间线已清空。</summary>
    public event Action HistoryCleared;

    public float MaxRewindBaseSeconds => maxRewindBaseSeconds;
    public float MinRewindBaseSeconds => minRewindBaseSeconds;
    public float CurrentRewindableSeconds => CalculateCurrentRewindableSeconds();
    public float CurrentStoredSeconds => history.StoredSeconds;
    public float AgeLimitedRewindableSeconds => CalculateAgeLimitedRewindableSeconds();
    public override int SnapshotCount => history.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rewindTarget = GetComponent<PlayerRewindTarget>();

        if (rewindTarget == null)
            Debug.LogError("[ReincarnationManager] PlayerRewindTarget is required.", this);
    }

    private void OnEnable()
    {
        TrySubscribeSceneLoadedEvent();
    }

    private void Start()
    {
        // 所有 Awake 完成后再补订阅，避免 SceneChanger 初始化顺序不确定。
        TrySubscribeSceneLoadedEvent();
    }

    private void OnDisable()
    {
        if (subscribedSceneLoadedEvent != null)
        {
            subscribedSceneLoadedEvent.VoidEvent -= OnSceneLoaded;
            subscribedSceneLoadedEvent = null;
        }

        ResetRewindHistory();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        history.Clear();
    }

    private void Update()
    {
        if (IsRewindOperationLocked)
            return;

        if (Input.GetKeyDown(KeyCode.R) && reincarnationEvent != null)
        {
            reincarnationEvent.OnEventRaised();
            StartRewind();
        }
    }

    private void FixedUpdate()
    {
        if (!IsRewindOperationLocked)
            RecordSnapshot();
    }

    public override void RecordSnapshot()
    {
        if (rewindTarget == null)
            return;

        float rewindableSeconds = CalculateAgeLimitedRewindableSeconds();
        PlayerRewindSnapshot snapshot = rewindTarget.CaptureSnapshot(Time.time);

        history.Record(snapshot, rewindableSeconds);
        SnapshotRecorded?.Invoke(snapshot.time, rewindableSeconds);
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked || history.Count == 0)
            return;

        rewindLimitForCurrentOperation = CalculateCurrentRewindableSeconds();
        PlayerRewindSnapshot[] playbackSnapshots = history.BorrowPlayback(out int count);
        if (playbackSnapshots == null || count == 0)
            return;

        StartCoroutine(RewindBackwards(
            playbackSnapshots,
            count,
            ApplySnapshot,
            Mathf.Max(0f, rewindConsumptionPerSecond),
            CalculateCurrentRewindableSeconds,
            rewindCooldownSeconds));
    }

    protected override void OnFreeze()
    {
        rewindTarget?.Freeze();
        reincarnationBegEvent?.OnEventRaised();
    }

    protected override void OnUnfreeze()
    {
        rewindTarget?.Unfreeze();
        reincarnationEndEvent?.OnEventRaised();
    }

    protected override void ClearSnapshots()
    {
        ClearTimeline();
    }

    protected override bool ShouldContinueRewind()
    {
        return Input.GetKey(KeyCode.R);
    }

    private void ApplySnapshot(PlayerRewindSnapshot snapshot)
    {
        rewindTarget?.ApplySnapshot(snapshot);
        SnapshotApplied?.Invoke(snapshot.time);
        snapshotAppliedEvent?.OnEventRaised(snapshot.time);
    }

    private float CalculateCurrentRewindableSeconds()
    {
        if (IsRewinding)
            return rewindLimitForCurrentOperation;

        if (history.Count == 0)
            return 0f;

        return Mathf.Min(CalculateAgeLimitedRewindableSeconds(), history.StoredSeconds);
    }

    private float CalculateAgeLimitedRewindableSeconds()
    {
        float agingRate = Mathf.Clamp01(1f - AgeManager.Instance.AgeNormalized);
        float baseSeconds = Mathf.Lerp(minRewindBaseSeconds, maxRewindBaseSeconds, agingRate);
        return baseSeconds * Mathf.Max(0f, rewindWindowMultiplier);
    }

    private void TrySubscribeSceneLoadedEvent()
    {
        if (Instance != this || subscribedSceneLoadedEvent != null)
            return;
        if (SceneChanger.Instance == null || SceneChanger.Instance.sceneLoadedEvent == null)
            return;

        subscribedSceneLoadedEvent = SceneChanger.Instance.sceneLoadedEvent;
        subscribedSceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        ResetRewindHistory();
    }

    /// <summary>切换场景或禁用玩家时，终止当前回溯并清空旧时间线。</summary>
    private void ResetRewindHistory()
    {
        if (IsRewindOperationLocked)
            StopAllCoroutines();

        rewindTarget?.Unfreeze();
        ClearTimeline();

        isRewinding = false;
        isRewindCoolingDown = false;
        currentRewoundGameTime = 0f;
    }

    private void ClearTimeline()
    {
        history.Clear();
        rewindLimitForCurrentOperation = 0f;
        HistoryCleared?.Invoke();
    }
}
