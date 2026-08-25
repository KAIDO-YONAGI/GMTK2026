using System;
using UnityEngine;

/// <summary>
/// 玩家动画播放器适配层。
/// 把 PlayerActionState 映射为 PlayerAnimationConfigSO 里的动画条目并交给 AnimationPlayerBase 播放；
/// 只负责表现（含脚步声、落地音效），不做任何物理或输入决策。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private AnimationPlayerBase animatorPlayer;
    [SerializeField] private PlayerAnimationConfigSO animationConfig;

    [Header("Footsteps")]
    [SerializeField] private float walkStepInterval = 0.52f;
    [SerializeField] private float runStepInterval = 0.4f;

    // 当前正在展示的动作状态：与权威状态分离，回溯重放时表现由快照直接驱动。
    private PlayerActionState displayedActionState = PlayerActionState.None;
    private float nextFootstepTime;
    // 回溯重放期间为 true，屏蔽脚步声/落地音效等额外反馈。
    private bool feedbackSuppressed;

    public event Action<PlayerActionState> ActionCompleted;

    public PlayerActionState DisplayedActionState => displayedActionState;
    public bool IsPlaying => animatorPlayer != null && animatorPlayer.IsPlaying;

    private void Update()
    {
        UpdateFootsteps();
    }

    /// <summary>
    /// 播放动作状态对应的动画。返回 clip 是否有效，供瞬态动作判断能否等待完成回调。
    /// suppressFeedback = true 供回溯重放使用：只恢复画面，不触发音效和完成回调。
    /// </summary>
    public bool PlayActionState(PlayerActionState state, bool suppressFeedback = false)
    {
        feedbackSuppressed = suppressFeedback;

        // 同状态幂等：不重启动画，避免循环动画每帧被重置回第一帧。
        if (state == displayedActionState)
        {
            return TryGetEntry(state, out PlayerAnimationEntry currentEntry) && currentEntry.clip != null;
        }

        displayedActionState = state;
        ResetFootstepTiming();

        if (!TryGetEntry(state, out PlayerAnimationEntry entry) || entry.clip == null || animatorPlayer == null)
        {
            return false;
        }

        bool played = animatorPlayer.Play(entry.clip, entry.parameters);
        if (!played)
            return false;

        // 只有非回溯的瞬态动作才注册完成回调，驱动 PlayerActionController 恢复常规状态。
        if (IsTransientState(state) && !suppressFeedback)
            animatorPlayer.AddCompleteCallback(() => ActionCompleted?.Invoke(state));

        if (state == PlayerActionState.Land && !suppressFeedback)
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.Land);

        return played;
    }

    /// <summary>回溯期间屏蔽脚步声等反馈；解除抑制时重置脚步计时避免连响。</summary>
    public void SetFeedbackSuppressed(bool suppressed)
    {
        feedbackSuppressed = suppressed;
        if (suppressed)
        {
            ResetFootstepTiming();
        }
    }

    /// <summary>移动类状态按固定间隔循环播放脚步声（Run 用快间隔，其余用慢间隔）。</summary>
    private void UpdateFootsteps()
    {
        if (feedbackSuppressed || !IsMovingState(displayedActionState) || Time.time < nextFootstepTime)
        {
            return;
        }

        AudioManager.Instance?.PlaySFX(
            displayedActionState == PlayerActionState.Run
                ? MyEnums.SFXType.Run
                : MyEnums.SFXType.Walk);

        nextFootstepTime = Time.time + GetStepInterval(displayedActionState);
    }

    private void ResetFootstepTiming()
    {
        nextFootstepTime = Time.time;
    }

    private float GetStepInterval(PlayerActionState state)
    {
        return state == PlayerActionState.Run ? runStepInterval : walkStepInterval;
    }

    /// <summary>是否为需要脚步声的移动状态。</summary>
    private static bool IsMovingState(PlayerActionState state)
    {
        return state == PlayerActionState.Run ||
               state == PlayerActionState.Walk ||
               state == PlayerActionState.WalkHunched ||
               state == PlayerActionState.WalkFrail;
    }

    /// <summary>是否为一次性瞬态动作（播完即回到常规状态推导）。</summary>
    private static bool IsTransientState(PlayerActionState state)
    {
        return state == PlayerActionState.Hurt || state == PlayerActionState.Land;
    }

    /// <summary>动作状态到 SO 配置条目的唯一映射表；None 与未配置的状态返回 false。</summary>
    private bool TryGetEntry(PlayerActionState state, out PlayerAnimationEntry entry)
    {
        entry = default;
        if (animationConfig == null)
        {
            return false;
        }

        switch (state)
        {
            case PlayerActionState.IdleYouth:
                entry = animationConfig.idleYouth;
                break;
            case PlayerActionState.IdleAdult:
                entry = animationConfig.idleAdult;
                break;
            case PlayerActionState.IdleElder:
                entry = animationConfig.idleElder;
                break;
            case PlayerActionState.IdleFrail:
                entry = animationConfig.idleFrail;
                break;
            case PlayerActionState.Run:
                entry = animationConfig.run;
                break;
            case PlayerActionState.Walk:
                entry = animationConfig.walk;
                break;
            case PlayerActionState.WalkHunched:
                entry = animationConfig.walkHunched;
                break;
            case PlayerActionState.WalkFrail:
                entry = animationConfig.walkFrail;
                break;
            case PlayerActionState.Hurt:
                entry = animationConfig.hurt;
                break;
            case PlayerActionState.Dead:
                entry = animationConfig.dead;
                break;
            case PlayerActionState.Jump:
                entry = animationConfig.jump;
                break;
            case PlayerActionState.Land:
                entry = animationConfig.land;
                break;
        }

        return entry.clip != null;
    }
}
