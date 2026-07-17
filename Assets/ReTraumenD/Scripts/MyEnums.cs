
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
    public enum SceneType
    {
        Location,
        Menu,
        Retry
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
    public enum CanvasToToggle//显示优先级系统和画布唤起系统共用
    {
        ESC,
        GameOver,
        Stats,
        Skills,
        Dialog,
        Quest,
        Shop,
        Integrated,
        Backpack,
        SaveLoad,
        Default
        //default没有索引，建议新的枚举在此之前添加
        //此外，不应该使用default的任何索引，因为语义上它代表默认界面，（目前）是ESC的重要判断依据
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
}
