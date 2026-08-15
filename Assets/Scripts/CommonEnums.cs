namespace MyEnums
{
    public enum SceneType
    {
        Location,
        Menu,
        Retry,
         //default没有功能性索引，建议新的枚举在此之前添加
        Default
    }
    public enum CanvasToToggle//显示优先级系统和画布唤起系统共用
    {
        ESC,
        GameOver,
        Dialog,
        Ending,
        Menu,
        Dying,
        Guide,
        //default没有索引，建议新的枚举在此之前添加
        //此外，不应该使用default的任何索引，因为语义上它代表默认界面，（目前）是ESC的重要判断依据
        Default
    }
}
