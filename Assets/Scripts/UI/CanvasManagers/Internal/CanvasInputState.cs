using System;
using System.Collections.Generic;

/// <summary>
/// UIManager 的每帧输入请求状态。
/// 合并代码请求和按键输入，并在处理完成后统一清空。
/// </summary>
internal sealed class CanvasInputState
{
    private readonly MyEnums.CanvasToToggle[] canvases;
    private readonly Dictionary<MyEnums.CanvasToToggle, bool> requests =
        new Dictionary<MyEnums.CanvasToToggle, bool>();

    public CanvasInputState()
    {
        canvases = (MyEnums.CanvasToToggle[])Enum.GetValues(
            typeof(MyEnums.CanvasToToggle));

        for (int i = 0; i < canvases.Length; i++)
            requests[canvases[i]] = false;
    }

    public void Request(MyEnums.CanvasToToggle canvas)
    {
        if (requests.ContainsKey(canvas))
            requests[canvas] = true;
    }

    public bool IsRequested(MyEnums.CanvasToToggle canvas)
    {
        return requests.TryGetValue(canvas, out bool requested) &&
            requested;
    }

    public void Reset()
    {
        for (int i = 0; i < canvases.Length; i++)
            requests[canvases[i]] = false;
    }
}
