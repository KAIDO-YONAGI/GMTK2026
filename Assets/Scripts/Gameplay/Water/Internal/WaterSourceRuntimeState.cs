using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的可变运行时状态。
/// 该类不参与 Unity 序列化，只负责让各个职责控制器共享同一份状态。
/// </summary>
internal sealed class WaterSourceRuntimeState
{
    public readonly List<WaterFlowUnit> Units = new List<WaterFlowUnit>();
    public readonly List<PressurePlate> HitPressurePlates = new List<PressurePlate>();

    public int StepCount;
    public bool IsFlowing;
    public bool HasFlowedOnce;
    public FreezeState FreezeState = FreezeState.Idle;
    public bool IsSnapshotRecording;

    public Coroutine FlowCoroutine;
    public Coroutine AdvanceCoroutine;
    public Coroutine FreezeCoroutine;

    public void ClearCoroutineReferences()
    {
        FlowCoroutine = null;
        AdvanceCoroutine = null;
        FreezeCoroutine = null;
    }
}
