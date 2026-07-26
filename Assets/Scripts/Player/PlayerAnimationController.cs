using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private UnityAnimatorPlayer animatorPlayer;
    [SerializeField] private PlayerAnimationConfigSO animationConfig;
    [SerializeField] private float jumpDelay = 0.18f;

    [Header("Footsteps")]
    [Tooltip("Seconds between walk footstep SFX.")]
    [SerializeField, Min(0.05f)] private float walkStepInterval = 0.52f;
    [Tooltip("Seconds between run footstep SFX.")]
    [SerializeField, Min(0.05f)] private float runStepInterval = 0.4f;

    private enum PlayerAgeStage { Youth, Adult, Elder, Frail }
    private enum PlayerAnimState { Idle, Walk, WalkHunched, WalkFrail, Run, Hurt, Dead, Jump, Land }
    private enum JumpPhase { None, WaitingForTakeoff, WaitingForAirborne, Airborne }
    private PlayerAnimState currentAnimState;
    private bool wasGrounded = true;
    private JumpPhase jumpPhase;
    private bool hasActiveFootstepState;
    private PlayerAnimState activeFootstepState;
    private float nextFootstepTime;

    private PlayerAgeStage CurrentAgeStage
    {
        get
        {
            float t = AgeManager.instance.AgeNormalized;
            if (t < 0.25f) return PlayerAgeStage.Youth;
            if (t < 0.5f) return PlayerAgeStage.Adult;
            if (t < 0.75f) return PlayerAgeStage.Elder;
            return PlayerAgeStage.Frail;
        }
    }
    private bool pendingHurt;

    private void OnHurt(int changeHp)
    {
        if (changeHp < 0)
            pendingHurt = true;
    }

    private void Update()
    {
        if (animatorPlayer == null || animationConfig == null || playerMovement == null)
            return;

        if (pendingHurt)
        {
            pendingHurt = false;
            PlayClipOrStay(PlayerAnimState.Hurt);
        }

        // 跳跃输入 → 先播 Jump 动画，在 50% 位置执行起跳
        if (playerMovement.TryConsumeJump())
        {
            ResetFootstepAudio();
            PlayClipOrStay(PlayerAnimState.Jump);
            jumpPhase = JumpPhase.WaitingForTakeoff;
            animatorPlayer.AddNormalizedTimeCallback(jumpDelay, ExecuteJump);
            wasGrounded = true;
            return;
        }

        bool isGrounded = playerMovement.IsGrounded;

        if (jumpPhase != JumpPhase.None)
        {
            ResetFootstepAudio();
            UpdateJumpPhase(isGrounded);
            return;
        }

        if (wasGrounded && !isGrounded)
        {
            ResetFootstepAudio();
            PlayClipOrStay(PlayerAnimState.Jump);
        }
        else if (!wasGrounded && isGrounded)
        {
            ResetFootstepAudio();
            PlayClipOrStay(PlayerAnimState.Land);
        }
        else if (isGrounded)
        {
            // 跳跃/落地的单次动画播放期间不切换
            if (currentAnimState == PlayerAnimState.Jump && animatorPlayer.IsPlaying)
            {
                ResetFootstepAudio();
                wasGrounded = true;
                return;
            }
            if (currentAnimState == PlayerAnimState.Land && animatorPlayer.IsPlaying)
            {
                ResetFootstepAudio();
                wasGrounded = true;
                return;
            }

            bool isMoving = Mathf.Abs(playerMovement.HorizontalInput) > 0.01f;
            if (isMoving)
            {
                PlayerAnimState moveState = GetMoveStateByAge();
                PlayClipOrStay(moveState);
                UpdateFootstepAudio(moveState);
            }
            else
            {
                ResetFootstepAudio();
                PlayClipOrStay(PlayerAnimState.Idle);
            }
        }
        else
        {
            ResetFootstepAudio();
        }

        wasGrounded = isGrounded;
    }

    private void ExecuteJump()
    {
        if (jumpPhase != JumpPhase.WaitingForTakeoff)
            return;

        playerMovement.ExecuteJump();
        AudioManager.Instance?.PlaySFX(MyEnums.SFXType.Jump);
        jumpPhase = JumpPhase.WaitingForAirborne;
    }

    private void UpdateJumpPhase(bool isGrounded)
    {
        if (jumpPhase == JumpPhase.WaitingForTakeoff)
        {
            wasGrounded = isGrounded;
            return;
        }

        if (jumpPhase == JumpPhase.WaitingForAirborne)
        {
            if (!isGrounded)
                jumpPhase = JumpPhase.Airborne;

            wasGrounded = isGrounded;
            return;
        }

        if (!isGrounded)
        {
            wasGrounded = false;
            return;
        }

        jumpPhase = JumpPhase.None;
        wasGrounded = true;
        PlayClipOrStay(PlayerAnimState.Land);
    }

    private void UpdateFootstepAudio(PlayerAnimState state)
    {
        if (state != PlayerAnimState.Run &&
            state != PlayerAnimState.Walk &&
            state != PlayerAnimState.WalkHunched &&
            state != PlayerAnimState.WalkFrail)
        {
            ResetFootstepAudio();
            return;
        }

        if (!hasActiveFootstepState || activeFootstepState != state)
        {
            hasActiveFootstepState = true;
            activeFootstepState = state;
            nextFootstepTime = Time.time;
        }

        if (Time.time < nextFootstepTime)
            return;

        bool isRunning = state == PlayerAnimState.Run;
        AudioManager.Instance?.PlaySFX(isRunning ? MyEnums.SFXType.Run : MyEnums.SFXType.Walk);
        nextFootstepTime = Time.time + (isRunning ? runStepInterval : walkStepInterval);
    }

    private void ResetFootstepAudio()
    {
        hasActiveFootstepState = false;
    }

    void PlayClipOrStay(PlayerAnimState state)
    {
        if (state == currentAnimState)
            return;

        currentAnimState = state;
        AnimationClip clip = GetClipForState(state);
        if (clip != null)
        {
            animatorPlayer.Play(clip, GetParamsForState(state));
            if (state == PlayerAnimState.Land)
                AudioManager.Instance?.PlaySFX(MyEnums.SFXType.Land);
        }
    }

    PlayerAnimState GetMoveStateByAge()
    {
        return CurrentAgeStage switch
        {
            PlayerAgeStage.Youth => PlayerAnimState.Run,
            PlayerAgeStage.Adult => PlayerAnimState.Walk,
            PlayerAgeStage.Elder => PlayerAnimState.WalkHunched,
            PlayerAgeStage.Frail => PlayerAnimState.WalkFrail,
            _ => PlayerAnimState.Idle
        };
    }

    AnimationClip GetClipForState(PlayerAnimState state)
    {
        return state switch
        {
            PlayerAnimState.Idle => animationConfig.idle.clip,
            PlayerAnimState.Walk => animationConfig.walk.clip,
            PlayerAnimState.WalkHunched => animationConfig.walk驼背.clip,
            PlayerAnimState.WalkFrail => animationConfig.walk拄拐.clip,
            PlayerAnimState.Run => animationConfig.run.clip,
            PlayerAnimState.Hurt => animationConfig.hurt.clip,
            PlayerAnimState.Jump => animationConfig.jump.clip,
            PlayerAnimState.Land => animationConfig.land.clip,
            PlayerAnimState.Dead => animationConfig.dead.clip,
            _ => null
        };
    }

    AnimationPlayParameters GetParamsForState(PlayerAnimState state)
    {
        return state switch
        {
            PlayerAnimState.Idle => animationConfig.idle.parameters,
            PlayerAnimState.Walk => animationConfig.walk.parameters,
            PlayerAnimState.WalkHunched => animationConfig.walk驼背.parameters,
            PlayerAnimState.WalkFrail => animationConfig.walk拄拐.parameters,
            PlayerAnimState.Run => animationConfig.run.parameters,
            PlayerAnimState.Hurt => animationConfig.hurt.parameters,
            PlayerAnimState.Jump => animationConfig.jump.parameters,
            PlayerAnimState.Land => animationConfig.land.parameters,
            PlayerAnimState.Dead => animationConfig.dead.parameters,
            _ => AnimationPlayParameters.Default
        };
    }

    public void PlayDeadAnimation()
    {
        if (animatorPlayer == null || animationConfig == null || animationConfig.dead.clip == null)
            return;

        currentAnimState = PlayerAnimState.Dead;
        animatorPlayer.Play(animationConfig.dead.clip, animationConfig.dead.parameters);
    }
}
