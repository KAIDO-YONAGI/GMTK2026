using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家轮回回溯：继承 RewindableBase，实现 Player 专属的快照抓取/还原/冻结。
/// R 键触发 VoidEventSO 后启动倒放，按真实时间把玩家位置和血量倒回过去。
/// </summary>
public class ReincarnationManager : RewindableBase
{
    // 单例：玩家轮回回溯全局唯一，方便外部访问
    public static ReincarnationManager Instance { get; private set; }

    [Header("Send Reincarnation Message")]
    [SerializeField] private VoidEventSO reincarnationEvent;

    [Tooltip("回溯真正开始时触发（OnFreeze 时刻，不是 R 键脉冲）。表现层用它做「进入回溯」反馈。")]
    [SerializeField] private VoidEventSO reincarnationBegEvent;

    [Tooltip("回溯真正结束时触发（OnUnfreeze 时刻，冷却之前）。表现层用它做「退出回溯」反馈。")]
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    [Tooltip("每次 apply 应用快照后广播该快照的时间戳（snap.time）。表现层可据此显示「倒回到了哪个时刻」。")]
    [SerializeField] private FloatEventSO snapshotAppliedEvent;

    [Header("Movement Events")]
    [SerializeField] private VoidEventSO MovementBegEventSO;
    [SerializeField] private VoidEventSO MovementEndEventSO;

    [Header("Rewind Time Window")]
    [Tooltip("完全年轻（agingRate=1）时的可回溯秒数。")]
    [SerializeField] private float maxRewindBaseSeconds = 8f;

    [Tooltip("完全衰老（agingRate=0）时的可回溯秒数。即使老到极致也保留最小窗口。")]
    [SerializeField] private float minRewindBaseSeconds = 1f;

    [Tooltip("可回溯窗口倍率。实际保留的快照时长 = 基础窗口 × 此倍率。" +
             "大于 1 时能回溯更久（积累更多历史快照）。" +
             "积累（记录保留时长）与消耗（按 R 倒放能回到多久）共用此倍率，保持统一。")]
    [SerializeField] private float rewindWindowMultiplier = 1f;

    [Header("Ghost Trail")]
    [Tooltip("Ghost sampling rate relative to changed rewind snapshots. 1 records every changed snapshot; 0 disables ghosts.")]
    [SerializeField, Range(0f, 1f)] private float ghostSamplingMultiplier = 1f;
    [Tooltip("SpriteRenderer used as the source for player ghosts. If empty, the SpriteRenderer on the player root is used.")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [Tooltip("Sorting order offset applied to ghosts relative to the player SpriteRenderer.")]
    [SerializeField] private int ghostSortingOrderOffset = -1;

    // 外部只读访问：回溯窗口及当前实时上限（UI 显示、外部系统查询用）
    public float MaxRewindBaseSeconds => maxRewindBaseSeconds;
    public float MinRewindBaseSeconds => minRewindBaseSeconds;
    public float CurrentRewindableSeconds => CalculateCurrentRewindableSeconds();

    /// <summary>
    /// 当前队列里实际存了多少秒的快照（最早快照到最新快照的时间跨度）。
    /// 实时反映：随 agingRate 缩减窗口时旧快照被裁剪，此值会动态变小。
    /// 与 CurrentRewindableSeconds（理论上限）不同——这是"当下真的能倒回多久"。
    /// 队列为空或正在回溯时返回 0。无 ToArray，适合表现层每帧读取。
    /// </summary>
    public float CurrentStoredSeconds
    {
        get
        {
            if (snapshots.Count == 0) return 0f;
            // earliest=队首（Peek），latest=入队时缓存的时间戳，避免遍历/ToArray
            float earliest = snapshots.Peek().time;
            return Mathf.Max(0f, latestSnapshotTime - earliest);
        }
    }

    [Header("Rewind Playback")]
    [FormerlySerializedAs("rewindSpeed")]
    [Tooltip("Recorded snapshot seconds consumed per real second. 1 = one recorded second per real second. Independent of Age Per Second.")]
    [SerializeField, Min(0f)] private float rewindConsumptionPerSecond = 1f;

    [Tooltip("回溯播放结束后、恢复记录新快照前的冷却秒数。期间输入立即恢复，只是不记录。")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    // 玩家位置 + 血量快照（值类型，避免 GC 压力）
    private struct PlayerSnapshot : IRewindSnapshot
    {
        public float time;
        public Vector3 position;
        public int health;

        // IRewindSnapshot：暴露时间戳供基类裁剪/倒放使用
        public float Time => time;
    }

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
        public SpriteRenderer renderer;
    }

    // 快照队列（容器留子类，基类只提供算法）
    private readonly Queue<PlayerSnapshot> snapshots = new Queue<PlayerSnapshot>();
    private readonly List<GhostSnapshot> ghosts = new List<GhostSnapshot>();

    // 缓存最新快照时间戳，避免 CurrentStoredSeconds 每次 ToArray 遍历队列（表现层每帧读会 GC）
    private float latestSnapshotTime = 0f;
    private float ghostSamplingAccumulator = 0f;
    private GhostVisualState previousGhostVisualState;
    private bool hasPreviousGhostVisualState;
    private float rewindLimitForCurrentOperation;
    private float lastGhostFadeTime;

    // 玩家根 Transform 和物理刚体
    private Transform playerTransform;
    private Rigidbody2D playerRigidbody;

    // 回溯前的物理状态，倒放结束后恢复
    private RigidbodyType2D bodyTypeBeforeRewind;
    private Vector2 velocityBeforeRewind;
    private float angularVelocityBeforeRewind;
    private bool hasFrozenPlayerState;
    private VoidEventSO subscribedSceneLoadedEvent;

    // IRewindable：暴露当前快照数量
    public override int SnapshotCount => snapshots.Count;
    public float AgeLimitedRewindableSeconds => CalculateAgeLimitedRewindableSeconds();

    private void Awake()
    {
        // 单例设置：重复实例时销毁自身，保证全局唯一
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerTransform = transform.root;
        playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
        lastGhostFadeTime = Time.time;

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = playerTransform.GetComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
        {
            previousGhostVisualState = CaptureGhostVisualState();
            hasPreviousGhostVisualState = true;
        }
    }

    private void OnEnable()
    {
        TrySubscribeSceneLoadedEvent();
    }

    private void Start()
    {
        // 所有 Awake 完成后再补订阅，避免 SceneChanger 初始化顺序不确定
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
        // 防止被销毁的单例指针悬挂
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void TrySubscribeSceneLoadedEvent()
    {
        if (Instance != this || subscribedSceneLoadedEvent != null) return;
        if (SceneChanger.instance == null || SceneChanger.instance.sceneLoadedEvent == null) return;

        subscribedSceneLoadedEvent = SceneChanger.instance.sceneLoadedEvent;
        subscribedSceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        ResetRewindHistory();
    }

    /// <summary>切换场景或禁用玩家时，终止当前回溯并清空旧场景快照。</summary>
    private void ResetRewindHistory()
    {
        if (IsRewindOperationLocked)
        {
            StopAllCoroutines();
        }

        if (hasFrozenPlayerState)
        {
            OnUnfreeze();
        }

        snapshots.Clear();
        ClearGhosts();
        latestSnapshotTime = 0f;
        ghostSamplingAccumulator = 0f;
        hasPreviousGhostVisualState = false;
        rewindLimitForCurrentOperation = 0f;
        lastGhostFadeTime = Time.time;
        isRewinding = false;
        isRewindCoolingDown = false;
        currentRewoundGameTime = 0f;
    }

    private void Update()
    {
        UpdateGhosts();
        // 倒放和冷却期间不接受新的触发。
        if (IsRewindOperationLocked) return;

        // 按下 R 键触发轮回空事件 + 启动回溯（引用未拖入时跳过，避免空引用）
        if (Input.GetKeyDown(KeyCode.R) && reincarnationEvent != null)
        {
            reincarnationEvent.OnEventRaised();
            StartRewind();
        }
    }

    /// <summary>每个物理帧更新：刷新衰老速率、抓快照、入队并裁剪。</summary>
    private void FixedUpdate()
    {
        // 倒放和冷却期间不记录新快照，避免与回放互相干扰。
        if (IsRewindOperationLocked) return;

        RecordSnapshot();
    }

    // ===== IRewindable =====
    public override void RecordSnapshot()

    {
        // 1. 刷新衰老速率：年轻→1，老→0（AgeManager.instance 已做 Clamp01）
        float rewindableSeconds = CalculateAgeLimitedRewindableSeconds();

        // 2. 抓取本帧快照（位置来自玩家根，血量来自单例 HealthManager）
        PlayerSnapshot snap = new PlayerSnapshot
        {
            time = Time.time,
            position = playerTransform.position,
            health = HealthManager.Instance != null ? HealthManager.Instance.CurrentHealth : 0
        };

        // 3. 入队并按可回溯时间窗口裁剪过期快照（算法在基类）
        EnqueueAndTrim(snapshots, snap, rewindableSeconds);
        TrimGhosts(snap.time - rewindableSeconds);
        TryRecordGhost(snap.time);
        // 缓存最新快照时间戳，供 CurrentStoredSeconds 零分配读取
        latestSnapshotTime = snap.time;
    }

    public override void StartRewind()
    {
        // 防重入 + 空队列保护
        if (IsRewindOperationLocked) return;
        if (snapshots.Count == 0 || ghosts.Count == 0) return;

        rewindLimitForCurrentOperation = CalculateCurrentRewindableSeconds();

        // 消耗速率直接映射到快照时间轴，保证表现层读取的倒回秒数与实际应用快照一致。
        StartCoroutine(RewindBackwards(
            snapshots.ToArray(),
            ApplySnapshot,
            Mathf.Max(0f, rewindConsumptionPerSecond),
            CalculateCurrentRewindableSeconds,
            rewindCooldownSeconds));
    }

    // ===== RewindableBase 钩子：冻结/解冻/清理（由 RewindBackwards 内部调用）=====
    protected override void OnFreeze()
    {
        hasFrozenPlayerState = true;

        // 通过移动事件管线停止玩家输入。
        if (MovementBegEventSO != null) MovementBegEventSO.OnEventRaised();

        // 回溯真正开始：广播 beg 事件（此时基类 isRewinding 已为 true）
        if (reincarnationBegEvent != null) reincarnationBegEvent.OnEventRaised();

        // 停止物理系统继续修改位置，并保存回溯前的运动状态
        if (playerRigidbody != null)
        {
            bodyTypeBeforeRewind = playerRigidbody.bodyType;
            velocityBeforeRewind = playerRigidbody.velocity;
            angularVelocityBeforeRewind = playerRigidbody.angularVelocity;

            playerRigidbody.velocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    protected override void OnUnfreeze()
    {
        if (!hasFrozenPlayerState) return;

        // 恢复回溯前的物理类型和运动状态
        if (playerRigidbody != null)
        {
            playerRigidbody.bodyType = bodyTypeBeforeRewind;
            playerRigidbody.velocity = velocityBeforeRewind;
            playerRigidbody.angularVelocity = angularVelocityBeforeRewind;
        }

        hasFrozenPlayerState = false;

        // 通过移动事件管线恢复玩家输入。
        if (MovementEndEventSO != null) MovementEndEventSO.OnEventRaised();

        // 回溯真正结束：广播 end 事件以恢复输入（此时基类 isRewinding 已为 false，冷却尚未开始）
        if (reincarnationEndEvent != null) reincarnationEndEvent.OnEventRaised();
    }

    protected override void ClearSnapshots()
    {
        snapshots.Clear();
        ClearGhosts();
        latestSnapshotTime = 0f;
        ghostSamplingAccumulator = 0f;
        hasPreviousGhostVisualState = false;
        rewindLimitForCurrentOperation = 0f;
        lastGhostFadeTime = Time.time;
    }

    protected override bool ShouldContinueRewind()
    {
        // 按住 R 才继续倒放，松开后由基类结束倒放并恢复玩家输入
        return Input.GetKey(KeyCode.R);
    }

    /// <summary>根据当前衰老状态返回可回溯时间窗口（已乘 rewindWindowMultiplier 倍率）。</summary>
    private float CalculateCurrentRewindableSeconds()
    {
        if (IsRewinding)
            return rewindLimitForCurrentOperation;

        float ageLimitedSeconds = CalculateAgeLimitedRewindableSeconds();
        if (ghosts.Count == 0)
            return 0f;

        float ghostHistorySeconds = Mathf.Max(0f, latestSnapshotTime - ghosts[0].snapshotTime);
        return Mathf.Min(ageLimitedSeconds, ghostHistorySeconds);
    }

    private float CalculateAgeLimitedRewindableSeconds()
    {
        float agingRate = Mathf.Clamp01(1f - AgeManager.instance.AgeNormalized);
        float baseSeconds = Mathf.Lerp(minRewindBaseSeconds, maxRewindBaseSeconds, agingRate);
        return baseSeconds * Mathf.Max(0f, rewindWindowMultiplier);
    }

    private void TryRecordGhost(float snapshotTime)
    {
        if (playerSpriteRenderer == null)
            return;

        float samplingMultiplier = Mathf.Clamp01(ghostSamplingMultiplier);
        GhostVisualState currentState = CaptureGhostVisualState();
        bool hasChanged = !hasPreviousGhostVisualState || !currentState.Equals(previousGhostVisualState);
        previousGhostVisualState = currentState;
        hasPreviousGhostVisualState = true;

        if (!hasChanged || samplingMultiplier <= 0f)
            return;

        ghostSamplingAccumulator += samplingMultiplier;
        if (ghostSamplingAccumulator < 1f)
            return;

        ghostSamplingAccumulator -= 1f;
        CreateGhost(snapshotTime, currentState);
    }

    private GhostVisualState CaptureGhostVisualState()
    {
        Transform sourceTransform = playerSpriteRenderer.transform;
        return new GhostVisualState
        {
            position = sourceTransform.position,
            rotation = sourceTransform.rotation,
            scale = sourceTransform.lossyScale,
            sprite = playerSpriteRenderer.sprite,
            color = playerSpriteRenderer.color,
            flipX = playerSpriteRenderer.flipX,
            flipY = playerSpriteRenderer.flipY
        };
    }

    private void CreateGhost(float snapshotTime, GhostVisualState state)
    {
        GameObject ghostObject = new GameObject("PlayerGhost");
        Transform ghostTransform = ghostObject.transform;
        ghostTransform.position = state.position;
        ghostTransform.rotation = state.rotation;
        ghostTransform.localScale = state.scale;

        SpriteRenderer ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = state.sprite;
        ghostRenderer.color = state.color;
        ghostRenderer.flipX = state.flipX;
        ghostRenderer.flipY = state.flipY;
        ghostRenderer.sharedMaterial = playerSpriteRenderer.sharedMaterial;
        ghostRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + ghostSortingOrderOffset;
        ghostRenderer.drawMode = playerSpriteRenderer.drawMode;
        ghostRenderer.size = playerSpriteRenderer.size;
        ghostRenderer.maskInteraction = playerSpriteRenderer.maskInteraction;
        ghostRenderer.spriteSortPoint = playerSpriteRenderer.spriteSortPoint;

        ghosts.Add(new GhostSnapshot
        {
            snapshotTime = snapshotTime,
            fadeElapsed = 0f,
            sourceColor = state.color,
            renderer = ghostRenderer
        });
    }

    private void UpdateGhosts()
    {
        if (IsRewinding)
        {
            lastGhostFadeTime = Time.time;
            return;
        }

        float fadeDuration = CalculateAgeLimitedRewindableSeconds();
        if (fadeDuration <= 0f)
        {
            ClearGhosts();
            return;
        }

        float currentTime = Time.time;
        float fadeDelta = Mathf.Max(0f, currentTime - lastGhostFadeTime);
        lastGhostFadeTime = currentTime;

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            GhostSnapshot ghost = ghosts[i];
            ghost.fadeElapsed += fadeDelta;
            if (ghost.fadeElapsed >= fadeDuration)
            {
                RemoveGhostAt(i);
                continue;
            }

            Color color = ghost.sourceColor;
            float remainingAlpha = Mathf.Clamp01(1f - ghost.fadeElapsed / fadeDuration) * 0.6f;
            color.a = ghost.sourceColor.a * remainingAlpha * remainingAlpha;
            ghost.renderer.color = color;
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
        if (ghost.renderer != null)
            Destroy(ghost.renderer.gameObject);
    }

    private void ClearGhosts()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
            RemoveGhostAt(i);
    }

    /// <summary>把单个快照应用回玩家身上（位置瞬切 + 血量直接设置，绕过死亡/治疗事件）。</summary>
    private void ApplySnapshot(PlayerSnapshot snap)
    {
        RemoveGhostsAtOrAfter(snap.time);
        // 通过刚体恢复位置，避免 Transform 与物理系统同时写入导致闪烁
        if (playerRigidbody != null)
        {
            playerRigidbody.position = new Vector2(snap.position.x, snap.position.y);
        }
        else
        {
            playerTransform.position = snap.position;
        }

        // 恢复血量（走轮回专用接口，绕过死亡/治疗事件）
        //TODO 绕过死亡还需要考虑
        if (HealthManager.Instance != null)
        {
            HealthManager.Instance.SetHealthForReincarnation(snap.health);
        }

        // 每次 apply 应用完快照后，广播该快照的时间戳供表现层使用
        if (snapshotAppliedEvent != null) snapshotAppliedEvent.OnEventRaised(snap.time);
    }
}
