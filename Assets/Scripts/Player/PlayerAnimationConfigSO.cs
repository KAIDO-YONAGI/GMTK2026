using UnityEngine;
using UnityEngine.Serialization;

/// <summary>单个动作的动画配置：片段加播放参数（速度、循环、起始时间等）。</summary>
[System.Serializable]
public struct PlayerAnimationEntry
{
    public AnimationClip clip;
    public AnimationPlayParameters parameters;
}

/// <summary>
/// 玩家全部动作状态到动画条目的配置表，PlayerAnimationController 按状态查表播放。
/// 通过 UnityAnimatorPlayer 播放时要求 clip 名与 Animator state 名一致。
/// FormerlySerializedAs 用于中文旧字段名的序列化迁移，已配置的资产不会断引用。
/// </summary>
[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Player/AnimationConfig", order = 0)]
public class PlayerAnimationConfigSO : ScriptableObject
{
    [Header("待机")]
    [FormerlySerializedAs("idle")]
    public PlayerAnimationEntry idleYouth;
    public PlayerAnimationEntry idleAdult;
    public PlayerAnimationEntry idleElder;
    [FormerlySerializedAs("idle拄拐")]
    public PlayerAnimationEntry idleFrail;

    [Header("移动")]
    public PlayerAnimationEntry walk;
    [FormerlySerializedAs("walk驼背")]
    public PlayerAnimationEntry walkHunched;
    [FormerlySerializedAs("walk拄拐")]
    public PlayerAnimationEntry walkFrail;
    public PlayerAnimationEntry run;

    [Header("动作")]
    public PlayerAnimationEntry jump;
    public PlayerAnimationEntry land;
    public PlayerAnimationEntry hurt;
    public PlayerAnimationEntry dead;
}
