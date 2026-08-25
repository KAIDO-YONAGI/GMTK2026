using UnityEngine;

/// <summary>
/// 玩家权威动作状态。由 PlayerActionController 维护，回溯快照会完整记录并恢复。
/// 待机与行走按年龄阶段拆分为四个变体，其余动作全年龄共用。
/// </summary>
public enum PlayerActionState
{
    None,
    IdleYouth,
    IdleAdult,
    IdleElder,
    IdleFrail,
    Run,
    Walk,
    WalkHunched,
    WalkFrail,
    Hurt,
    Dead,
    Jump,
    Land
}

/// <summary>
/// 玩家动作编排器。
/// 统一采样输入、维护权威动作状态，并向移动和动画组件下达命令。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerActionController : MonoBehaviour
{
    /// <summary>年龄阶段划分，与待机/行走动画变体一一对应。</summary>
    private enum PlayerAgeStage
    {
        Youth,
        Adult,
        Elder,
        Frail
    }

    /// <summary>跳跃流程阶段：接受输入 → 延迟后施加冲量 → 等待离地 → 空中 → 落地转 Land。</summary>
    private enum JumpPhase
    {
        None,
        WaitingForTakeoff,
        WaitingForAirborne,
        Airborne
    }

    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAnimationController animationController;

    [Header("Jump")]
    [Tooltip("Delay in seconds between accepting jump input and applying the jump impulse.")]
    [SerializeField, Min(0f)] private float jumpDelay = 0.18f;

    [Tooltip("Max seconds to wait for leaving the ground after the jump impulse; on timeout the state falls back to normal (e.g. bumping head on a low ceiling).")]
    [SerializeField, Min(0.05f)] private float takeoffTimeout = 0.3f;

    [Header("Input Events")]
    [SerializeField] private VoidEventSO startInputEvent;
    [SerializeField] private VoidEventSO stopInputEvent;

    [Header("Lifecycle Events")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    // 权威动作状态：动画、回溯快照都读这里，而不是各自判断。
    private PlayerActionState currentActionState = PlayerActionState.None;
    private JumpPhase jumpPhase;
    private float takeoffTime;
    // 回溯倒放期间为 true，正常状态机停摆，只允许 ApplyRewindActionState 驱动表现。
    private bool isRewinding;
    // 瞬态动作（Land/Hurt）播放期间挂起正常状态切换，直到动画完成回调。
    private bool isWaitingForActionCompletion;

    public PlayerActionState CurrentActionState => currentActionState;
    public bool IsRewinding => isRewinding;

    private void OnEnable()
    {
        if (animationController != null)
            animationController.ActionCompleted += OnAnimationActionCompleted;
        if (playerMovement != null)
            playerMovement.Landed += OnLanded;
        if (startInputEvent != null)
            startInputEvent.VoidEvent += OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent += OnStopInput;
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent += OnReincarnationBegin;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
    }

    private void Start()
    {
        SynchronizeNormalActionState();
    }

    private void OnDisable()
    {
        if (animationController != null)
            animationController.ActionCompleted -= OnAnimationActionCompleted;
        if (playerMovement != null)
            playerMovement.Landed -= OnLanded;
        if (startInputEvent != null)
            startInputEvent.VoidEvent -= OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent -= OnStopInput;
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent -= OnReincarnationBegin;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;

        ResetTransientState();
        playerMovement?.SetHorizontalInput(0f);
    }

    /// <summary>
    /// 每帧唯一的输入采样点：读一次输入后分发给移动组件并推导动作状态，
    /// 避免移动、动画、朝向各自采样导致同一帧读到不同结果。
    /// </summary>
    private void Update()
    {
        if (playerMovement == null || isRewinding)
            return;

        // 死亡后清空输入并锁定 Dead 状态，其余分支全部短路。
        if (IsPlayerDead())
        {
            playerMovement.SetHorizontalInput(0f);
            SetActionState(PlayerActionState.Dead);
            return;
        }

        // 输入被关闭（回溯冻结等）时只清输入，不改写当前动作表现。
        if (!playerMovement.IsInputEnabled)
        {
            playerMovement.SetHorizontalInput(0f);
            return;
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        playerMovement.SetHorizontalInput(horizontalInput);

        if (UpdateJumpAction())
            return;

        if (isWaitingForActionCompletion)
            return;

        if (Input.GetButtonDown("Jump") && playerMovement.IsGrounded)
        {
            BeginJump();
            return;
        }

        if (!playerMovement.IsGrounded)
        {
            SetActionState(PlayerActionState.Jump);
            return;
        }

        // 静止时按年龄切到对应待机，移动时切到对应步态。
        SetActionState(Mathf.Abs(horizontalInput) > 0.01f
            ? GetMoveStateByAge()
            : GetIdleStateByAge());
    }

    /// <summary>回溯应用快照时更新动作引擎状态，只驱动表现，不执行物理或音效。</summary>
    public void ApplyRewindActionState(PlayerActionState state)
    {
        currentActionState = state;
        animationController?.PlayActionState(state, true);
    }

    /// <summary>接受跳跃输入：先切 Jump 状态播放起跳动画，延迟 jumpDelay 后才施加物理冲量。</summary>
    private void BeginJump()
    {
        jumpPhase = JumpPhase.WaitingForTakeoff;
        takeoffTime = Time.time + jumpDelay;
        isWaitingForActionCompletion = false;
        SetActionState(PlayerActionState.Jump);
    }

    /// <summary>
    /// 推进跳跃阶段状态机。返回 true 表示跳跃流程进行中，调用方跳过常规状态推导。
    /// </summary>
    private bool UpdateJumpAction()
    {
        if (jumpPhase == JumpPhase.None)
            return false;

        if (jumpPhase == JumpPhase.WaitingForTakeoff)
        {
            if (Time.time >= takeoffTime)
            {
                if (playerMovement.ExecuteJump())
                {
                    AudioManager.Instance?.PlaySFX(MyEnums.SFXType.Jump);
                    // takeoffTime 在此复用为冲量实际施加时刻，供 WaitingForAirborne 阶段做超时判断。
                    takeoffTime = Time.time;
                    jumpPhase = JumpPhase.WaitingForAirborne;
                }
                else
                {
                    // 冲量施加失败（死亡、已离地等）时回退常规状态，避免卡死在起跳前摇。
                    jumpPhase = JumpPhase.None;
                    SynchronizeNormalActionState();
                    return false;
                }
            }

            return true;
        }

        if (jumpPhase == JumpPhase.WaitingForAirborne)
        {
            if (!playerMovement.IsGrounded)
            {
                jumpPhase = JumpPhase.Airborne;
            }
            else if (Time.time >= takeoffTime + takeoffTimeout)
            {
                // 低矮天花板顶头后被弹回，上升幅度小于地面检测半径导致始终未"离地"，
                // 超时视为起跳失败，回退常规推导，避免永久卡在起跳状态。
                jumpPhase = JumpPhase.None;
                SynchronizeNormalActionState();
                return false;
            }

            return true;
        }

        if (!playerMovement.IsGrounded)
            return true;

        // 跳跃流程自己收尾：落地瞬间转 Land 瞬态动作。
        jumpPhase = JumpPhase.None;
        EnterTransientAction(PlayerActionState.Land);
        return true;
    }

    /// <summary>
    /// 处理跳跃流程之外的落地（弹簧、平台移动等导致的离地）。
    /// 起跳前摇期间收到 Landed 说明尚未离地，属同帧地面抖动，忽略。
    /// </summary>
    private void OnLanded()
    {
        if (isRewinding || jumpPhase == JumpPhase.WaitingForTakeoff)
            return;

        jumpPhase = JumpPhase.None;
        EnterTransientAction(PlayerActionState.Land);
    }

    /// <summary>进入瞬态动作（Land/Hurt）：播放一次并挂起状态机，等动画完成回调再恢复。</summary>
    private void EnterTransientAction(PlayerActionState state)
    {
        currentActionState = state;
        isWaitingForActionCompletion =
            animationController != null &&
            animationController.PlayActionState(state, false);
    }

    /// <summary>瞬态动画完成回调。只接受与当前状态一致的回调，过期回调（状态已切换）直接丢弃。</summary>
    private void OnAnimationActionCompleted(PlayerActionState state)
    {
        if (!isWaitingForActionCompletion || state != currentActionState)
            return;

        isWaitingForActionCompletion = false;
        SynchronizeNormalActionState();
    }

    /// <summary>输入恢复（回溯结束/场景加载完成）：重置瞬态并立即重算动作状态。</summary>
    private void OnStartInput()
    {
        playerMovement?.SetInputEnabled(true);
        ResetTransientState();
        SynchronizeNormalActionState();
    }

    /// <summary>输入关闭（回溯冻结）：清空瞬态，动作表现交由 ApplyRewindActionState 驱动。</summary>
    private void OnStopInput()
    {
        playerMovement?.SetInputEnabled(false);
        ResetTransientState();
    }

    /// <summary>场景加载完成：重置回溯标记并恢复输入，防止上一场景的冻结状态泄漏到新场景。</summary>
    private void OnSceneLoaded()
    {
        isRewinding = false;
        playerMovement?.SetInputEnabled(true);
        ResetTransientState();
        SynchronizeNormalActionState();
    }

    /// <summary>回溯倒放开始：停摆正常状态机，并抑制脚步声等反馈。</summary>
    private void OnReincarnationBegin()
    {
        isRewinding = true;
        ResetTransientState();
        animationController?.SetFeedbackSuppressed(true);
    }

    /// <summary>回溯倒放结束：按当前实时输入与落地状态立即重算动作状态。</summary>
    private void OnReincarnationEnd()
    {
        isRewinding = false;
        ResetTransientState();
        SynchronizeNormalActionState();
    }

    /// <summary>
    /// 脱离瞬态/回溯后，按实时状态（死亡、落地、输入）立即重算动作状态，
    /// 避免残留 Land 或回溯前的旧动画。
    /// </summary>
    private void SynchronizeNormalActionState()
    {
        if (playerMovement == null || isRewinding)
            return;

        if (IsPlayerDead())
        {
            SetActionState(PlayerActionState.Dead);
            return;
        }

        if (!playerMovement.IsGrounded)
        {
            SetActionState(PlayerActionState.Jump);
            return;
        }

        SetActionState(Mathf.Abs(playerMovement.HorizontalInput) > 0.01f
            ? GetMoveStateByAge()
            : GetIdleStateByAge());
    }

    /// <summary>写入权威动作状态并驱动动画。同状态幂等，但会解除反馈抑制（回溯结束后恢复脚步声等）。</summary>
    private void SetActionState(PlayerActionState state)
    {
        if (currentActionState == state)
        {
            animationController?.SetFeedbackSuppressed(false);
            return;
        }

        currentActionState = state;
        animationController?.PlayActionState(state, false);
    }

    /// <summary>按年龄阶段返回步态：Youth 跑、Adult 走、Elder 驼背走、Frail 拄拐走。</summary>
    private PlayerActionState GetMoveStateByAge()
    {
        switch (GetCurrentAgeStage())
        {
            case PlayerAgeStage.Youth:
                return PlayerActionState.Run;
            case PlayerAgeStage.Adult:
                return PlayerActionState.Walk;
            case PlayerAgeStage.Elder:
                return PlayerActionState.WalkHunched;
            case PlayerAgeStage.Frail:
                return PlayerActionState.WalkFrail;
            default:
                return PlayerActionState.Walk;
        }
    }

    /// <summary>按年龄阶段返回待机变体，保证停步时停在当前年龄的正确待机动画。</summary>
    private PlayerActionState GetIdleStateByAge()
    {
        switch (GetCurrentAgeStage())
        {
            case PlayerAgeStage.Youth:
                return PlayerActionState.IdleYouth;
            case PlayerAgeStage.Adult:
                return PlayerActionState.IdleAdult;
            case PlayerAgeStage.Elder:
                return PlayerActionState.IdleElder;
            case PlayerAgeStage.Frail:
                return PlayerActionState.IdleFrail;
            default:
                return PlayerActionState.IdleYouth;
        }
    }

    /// <summary>按年龄归一化划分四段：以 0.25 / 0.5 / 0.75 为界对应 Youth / Adult / Elder / Frail。</summary>
    private static PlayerAgeStage GetCurrentAgeStage()
    {
        float ageNormalized = AgeManager.Instance != null
            ? AgeManager.Instance.AgeNormalized
            : 0f;

        if (ageNormalized < 0.25f)
            return PlayerAgeStage.Youth;
        if (ageNormalized < 0.5f)
            return PlayerAgeStage.Adult;
        if (ageNormalized < 0.75f)
            return PlayerAgeStage.Elder;
        return PlayerAgeStage.Frail;
    }

    private static bool IsPlayerDead()
    {
        return HealthManager.Instance != null &&
               HealthManager.Instance.IsDead &&
               HealthManager.Instance.CurrentHealth <= 0f;
    }

    /// <summary>清空跳跃阶段与瞬态等待标记，用于输入切换、场景加载等重置时机。</summary>
    private void ResetTransientState()
    {
        jumpPhase = JumpPhase.None;
        takeoffTime = 0f;
        isWaitingForActionCompletion = false;
    }
}
