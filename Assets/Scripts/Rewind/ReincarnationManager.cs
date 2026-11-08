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

    [Header("回溯事件")]
    [SerializeField] private VoidEventSO reincarnationEvent;

    [Tooltip("实际开始回溯时发出。")]
    [SerializeField] private VoidEventSO reincarnationBegEvent;

    [Tooltip("实际结束回溯且冷却开始前发出。")]
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    [Tooltip("每次应用玩家快照后发出。")]
    [SerializeField] private FloatEventSO snapshotAppliedEvent;

    [Header("长按回溯")]
    [Tooltip("开启长按激活。关闭时，按下 R 立即开始回溯。")]
    [SerializeField] private bool enableHoldToRewind = true;

    [Tooltip("开始回溯前必须按住 R 的秒数；更短的点击会被忽略。")]
    [SerializeField, Min(0f)] private float rewindHoldSeconds = 0.3f;

    [Header("回溯时间窗口")]
    [Tooltip("玩家处于最年轻状态时的最大可回溯秒数。")]
    [SerializeField] private float maxRewindBaseSeconds = 8f;

    [Tooltip("玩家完全衰老时的最小可回溯秒数。")]
    [SerializeField] private float minRewindBaseSeconds = 1f;

    [Tooltip("快照保留与回溯播放共用的时间窗口倍率。")]
    [SerializeField] private float rewindWindowMultiplier = 1f;

    [Header("回溯播放")]
    [FormerlySerializedAs("rewindSpeed")]
    [Tooltip("每经过一秒真实时间消耗的已记录快照时间（秒）。")]
    [SerializeField, Min(0f)] private float rewindConsumptionPerSecond = 1f;

    [Tooltip("回溯结束后重新记录快照前的冷却时间（秒）。")]
    [SerializeField, Min(0f)] private float rewindCooldownSeconds = 1f;

    private readonly PlayerRewindHistory history = new PlayerRewindHistory();

    private PlayerRewindTarget rewindTarget;
    private float rewindLimitForCurrentOperation;
    private VoidEventSO subscribedSceneLoadedEvent;

    // 本次按住 R 已蓄力的秒数；松开立即清零。
    private float rewindHoldTimer;
    // 本次按住是否已经触发过回溯，避免按住不放连续触发。
    private bool rewindHoldTriggered;

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
        UpdateRewindInput();
    }

    /// <summary>
    /// R 输入：开启长按检测时必须连续按住 rewindHoldSeconds 秒才启动回溯，快速点按无效；
    /// 关闭长按检测时保持原本的“按下即触发”。
    /// 触发过或处于回溯/冷却期间都必须先松开再重新按压，避免按住不放连续触发。
    /// </summary>
    private void UpdateRewindInput()
    {
        // 关闭长按检测：维持原有行为，按下立即触发。
        if (!enableHoldToRewind)
        {
            if (Input.GetKeyDown(KeyCode.R) && !IsRewindOperationLocked)
                StartRewindByInput();

            return;
        }

        // 松开 R 立即清空蓄力，保证下一次按压必须重新按满。
        if (Input.GetKeyUp(KeyCode.R))
        {
            rewindHoldTimer = 0f;
            rewindHoldTriggered = false;
        }

        if (IsRewindOperationLocked || rewindHoldTriggered)
            return;

        if (!Input.GetKey(KeyCode.R))
        {
            rewindHoldTimer = 0f;
            return;
        }

        rewindHoldTimer += Time.deltaTime;
        if (rewindHoldTimer < rewindHoldSeconds)
            return;

        rewindHoldTriggered = true;
        StartRewindByInput();
    }

    /// <summary>发出回溯输入事件并尝试启动回溯。</summary>
    private void StartRewindByInput()
    {
        if (reincarnationEvent == null)
            return;

        reincarnationEvent.OnEventRaised();
        StartRewind();
    }

    /// <summary>改开关或秒数时清空蓄力状态，避免切换检测模式后残留旧状态。</summary>
    private void OnValidate()
    {
        rewindHoldTimer = 0f;
        rewindHoldTriggered = false;
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

        // 场景切换/禁用后清空长按蓄力，避免跨场景残留“按住即触发”。
        rewindHoldTimer = 0f;
        rewindHoldTriggered = false;
    }

    private void ClearTimeline()
    {
        history.Clear();
        rewindLimitForCurrentOperation = 0f;
        HistoryCleared?.Invoke();
    }
}
