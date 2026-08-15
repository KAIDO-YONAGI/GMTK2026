
namespace MyEnums
{
    public enum PlayerState
    {

        Idle,
        Running,
        Attacking,
        Shooting,
        KnockBack

    }
    public enum EnemyState
    {
        Idle,
        Chasing,
        Attacking,
        KnockBack
    }
    public enum PersistentType
    {
        ReadWrite,
        DontPersist
    }
    public enum AStarNodeType
    {
        Obstacle,
        Walkable
    }
    public enum NPCState
    {
        Idle,
        Wander,
        Patrol,
        Chat
    }

    public enum QuestState
    {
        Idle,
        Accepted,
        Decline,
        IsToComplete,
        Completed

    }
    public enum SaveType
    {
        SystemSave,
        PlayerSave
    }
    public enum SaveLoadPanelType
    {
        Save,
        Load
    }
    public enum DeathType
    {
        OldDead,
        UnnormalDead
    }
    public enum SFXType
    {
        None,
        Walk,
        IceBroken,
        IceFreeze,
        Hurt,
        SpikeLaunch,
        LoadScene,
        Dead,
        SpringBounce,
        SpeedUp,
        Land,
        Jump,
        PickUp,
        PressurePlate,
        Run
    }
    public enum BGMType
    {
        Menu,
        Gaming,
         // Default 没有功能性索引，建议在此之前添加新的枚举。
        Default
    }
}
