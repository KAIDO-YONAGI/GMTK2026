using UnityEngine;

[System.Serializable]
public struct PlayerAnimationEntry
{
    public AnimationClip clip;
    public AnimationPlayParameters parameters;
}

[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Player/AnimationConfig", order = 0)]
public class PlayerAnimationConfigSO : ScriptableObject
{
    public PlayerAnimationEntry idle;
    public PlayerAnimationEntry walk;
    public PlayerAnimationEntry run;
    public PlayerAnimationEntry hurt;
    public PlayerAnimationEntry dead;
}
