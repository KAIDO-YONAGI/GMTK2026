using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WaterSource 的流水控制器。
/// 负责生成路径、碰撞检测、单元成熟和末端拖尾。
/// </summary>
internal sealed class WaterFlowController
{
    private readonly WaterSource owner;
    private readonly WaterSourceRuntimeState state;
    private readonly Func<bool> isOperationLocked;
    private readonly Action beginSnapshotRecording;
    private readonly Action recordSnapshot;
    private readonly Func<bool> isFrozen;
    private readonly Action<Vector3> playIceParticle;
    private readonly WaterFlowUnit unitTemplate;
    private readonly Transform spawnRoot;
    private readonly float cellSize;
    private readonly float flowInterval;
    private readonly float stage1ToStage2Seconds;
    private readonly LayerMask groundMask;

    public WaterFlowController(
        WaterSource owner,
        WaterSourceRuntimeState state,
        Func<bool> isOperationLocked,
        Action beginSnapshotRecording,
        Action recordSnapshot,
        Func<bool> isFrozen,
        Action<Vector3> playIceParticle,
        WaterFlowUnit unitTemplate,
        Transform spawnRoot,
        float cellSize,
        float flowInterval,
        float stage1ToStage2Seconds,
        LayerMask groundMask)
    {
        this.owner = owner;
        this.state = state;
        this.isOperationLocked = isOperationLocked;
        this.beginSnapshotRecording = beginSnapshotRecording;
        this.recordSnapshot = recordSnapshot;
        this.isFrozen = isFrozen;
        this.playIceParticle = playIceParticle;
        this.unitTemplate = unitTemplate;
        this.spawnRoot = spawnRoot;
        this.cellSize = cellSize;
        this.flowInterval = flowInterval;
        this.stage1ToStage2Seconds = stage1ToStage2Seconds;
        this.groundMask = groundMask;
    }

    public void StartFlowing()
    {
        if (state.HasFlowedOnce ||
            state.IsFlowing ||
            isOperationLocked())
        {
            return;
        }

        beginSnapshotRecording();
        state.IsFlowing = true;
        state.HasFlowedOnce = true;
        state.FlowCoroutine = owner.StartCoroutine(FlowLoop());
        state.AdvanceCoroutine = owner.StartCoroutine(AdvanceLoop());
        recordSnapshot();
    }

    public void StopForRewind()
    {
        StopCoroutine(ref state.FlowCoroutine);
        StopCoroutine(ref state.AdvanceCoroutine);
    }

    public void ResumeAfterRewind()
    {
        if (!state.IsFlowing)
            return;

        state.FlowCoroutine = owner.StartCoroutine(FlowLoop());
        state.AdvanceCoroutine = owner.StartCoroutine(AdvanceLoop());
    }

    public void Dispose()
    {
        StopForRewind();
    }

    private IEnumerator FlowLoop()
    {
        while (state.IsFlowing && !isOperationLocked())
        {
            state.StepCount++;
            Vector3 nextPosition = (spawnRoot != null
                    ? spawnRoot.position
                    : owner.transform.position)
                + Vector3.down * (cellSize * state.StepCount);

            FindFlowHits(
                nextPosition,
                cellSize,
                groundMask,
                state.HitPressurePlates,
                out RaycastHit2D groundHit);

            for (int i = 0; i < state.HitPressurePlates.Count; i++)
            {
                PressurePlate plate = state.HitPressurePlates[i];
                if (plate != null)
                    plate.Activate();
            }

            if (groundHit.collider != null)
            {
                SpawnUnitAt(nextPosition);

                if (state.Units.Count > 0)
                    SpawnTail(state.Units[state.Units.Count - 1]);

                state.IsFlowing = false;
                break;
            }

            SpawnUnitAt(nextPosition);
            yield return new WaitForSeconds(flowInterval);
        }

        state.IsFlowing = false;
        state.FlowCoroutine = null;
    }

    private IEnumerator AdvanceLoop()
    {
        while (true)
        {
            if (isOperationLocked())
            {
                yield return null;
                continue;
            }

            bool anyChanged = false;
            float now = Time.time;
            bool frozenMode = isFrozen();

            for (int i = 0; i < state.Units.Count; i++)
            {
                WaterFlowUnit unit = state.Units[i];
                if (unit == null ||
                    unit.Phase != WaterFlowUnit.WaterPhase.Stage1 ||
                    unit.LockAtStage1 ||
                    now - unit.BirthTime < stage1ToStage2Seconds)
                {
                    continue;
                }

                unit.SetPhase(frozenMode
                    ? WaterFlowUnit.WaterPhase.Frozen
                    : WaterFlowUnit.WaterPhase.Stage2);

                if (frozenMode)
                    playIceParticle(unit.transform.position);

                anyChanged = true;
            }

            if (anyChanged)
            {
                recordSnapshot();
                if (frozenMode)
                    AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceFreeze);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void SpawnUnitAt(Vector3 worldPosition)
    {
        if (unitTemplate == null)
        {
            Debug.LogError(
                "[WaterSource] unitTemplate 未配置，无法生成水流单元。",
                owner);
            state.IsFlowing = false;
            return;
        }

        WaterFlowUnit unit = UnityEngine.Object.Instantiate(
            unitTemplate,
            worldPosition,
            Quaternion.identity,
            spawnRoot != null ? spawnRoot : owner.transform);
        unit.Init(owner);
        state.Units.Add(unit);
        recordSnapshot();
    }

    private void SpawnTail(WaterFlowUnit parentUnit)
    {
        if (unitTemplate == null || parentUnit == null)
            return;

        WaterFlowUnit tail = UnityEngine.Object.Instantiate(
            unitTemplate,
            parentUnit.transform);
        tail.transform.localPosition = Vector3.down * (cellSize * 0.5f);
        tail.Init(null);
        tail.LockAtStage1 = true;
        tail.SetPhase(WaterFlowUnit.WaterPhase.Stage1);
    }

    private static void FindFlowHits(
        Vector2 origin,
        float distance,
        LayerMask groundMask,
        List<PressurePlate> pressurePlates,
        out RaycastHit2D nearestGroundHit)
    {
        pressurePlates.Clear();
        nearestGroundHit = default;
        float nearestGroundDistance = float.PositiveInfinity;
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            Vector2.down,
            distance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.CompareTag("PressurePlate"))
                continue;

            int hitLayerMask = 1 << hitCollider.gameObject.layer;
            if ((groundMask.value & hitLayerMask) == 0 ||
                hits[i].distance >= nearestGroundDistance)
            {
                continue;
            }

            nearestGroundDistance = hits[i].distance;
            nearestGroundHit = hits[i];
        }

        float maxPlateDistance = nearestGroundHit.collider != null
            ? nearestGroundDistance + 0.001f
            : float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null ||
                !hitCollider.CompareTag("PressurePlate") ||
                hits[i].distance > maxPlateDistance)
            {
                continue;
            }

            PressurePlate plate = hitCollider.GetComponent<PressurePlate>();
            if (plate != null && !pressurePlates.Contains(plate))
                pressurePlates.Add(plate);
        }
    }

    private void StopCoroutine(ref Coroutine coroutine)
    {
        if (coroutine == null)
            return;

        owner.StopCoroutine(coroutine);
        coroutine = null;
    }
}
