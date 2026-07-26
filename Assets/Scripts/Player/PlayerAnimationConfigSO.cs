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
    public PlayerAnimationEntry walk驼背;
    public PlayerAnimationEntry walk拄拐;
    public PlayerAnimationEntry jump;
    public PlayerAnimationEntry land;
    public PlayerAnimationEntry run;
    public PlayerAnimationEntry hurt;
    public PlayerAnimationEntry dead;
}
