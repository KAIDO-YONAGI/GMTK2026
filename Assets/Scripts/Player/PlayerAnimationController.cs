using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private UnityAnimatorPlayer animatorPlayer;
    [SerializeField] private PlayerAnimationConfigSO animationConfig;

    private enum PlayerAgeStage { Youth, Adult, Elder, Frail }
    private enum PlayerAnimState { Idle, Walk, WalkHunched, WalkFrail, Run, Hurt, Dead, Jump, Land }
    private PlayerAnimState currentAnimState;
    private bool wasGrounded = true;

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

        bool isGrounded = playerMovement.IsGrounded;

        if (wasGrounded && !isGrounded)
        {
            PlayClipOrStay(PlayerAnimState.Jump);
        }
        else if (!wasGrounded && isGrounded)
        {
            PlayClipOrStay(PlayerAnimState.Land);
        }
        else if (isGrounded)
        {
            // Land 动画播放期间不切换，等它播完自然保持最后一帧后再切到 Idle/Walk
            if (currentAnimState == PlayerAnimState.Land && animatorPlayer.IsPlaying)
            {
                wasGrounded = true;
                return;
            }

            bool isMoving = Mathf.Abs(playerMovement.HorizontalInput) > 0.01f;
            PlayClipOrStay(isMoving ? GetMoveStateByAge() : PlayerAnimState.Idle);
        }

        wasGrounded = isGrounded;
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
