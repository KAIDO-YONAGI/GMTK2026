using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的流水职责：生成路径、碰撞检测、单元成熟与末端拖尾。
/// </summary>
public partial class WaterSource
{
    /// <summary>启动流水。流过一次后被锁死（hasFlowedOnce），回溯（OnReincarnationEnd）后才解锁。</summary>
    public void StartFlowing()
    {
        if (hasFlowedOnce) return; // 已流过一次，忽略直到回溯重置
        if (isFlowing) return;
        if (IsRewindOperationLocked) return;

        BeginSnapshotRecording();
        isFlowing = true;
        hasFlowedOnce = true; // 标记已触发，FlowLoop 结束后也不再响应 trigger
        flowCoroutine = StartCoroutine(FlowLoop());
        advanceCoroutine = StartCoroutine(AdvanceLoop());
        RecordSnapshot(); // 状态转变：开始流水
    }

    // ===== 流水协程：每隔 flowInterval 向下一格生成单元 =====
    private IEnumerator FlowLoop()
    {
        while (isFlowing && !IsRewindOperationLocked)
        {
            stepCount++;
            Vector3 nextPos = (spawnRoot != null ? spawnRoot.position : transform.position)
                              + Vector3.down * (cellSize * stepCount);

            // 扫描整格路径：压力板按 Tag 触发但不参与阻挡，只有 groundMask 内的其他碰撞体才会阻断水流。
            FindFlowHits(
                nextPos,
                cellSize,
                groundMask,
                hitPressurePlates,
                out RaycastHit2D groundHit);

            for (int i = 0; i < hitPressurePlates.Count; i++)
            {
                PressurePlate plate = hitPressurePlates[i];
                if (plate != null) plate.Activate();
            }

            if (groundHit.collider != null)
            {
                // 命中地面 / 平台：这一格贴着地面，必须生成它让水流视觉贴紧地面（不悬空），生成后停止向下扩散。
                // 已生成单元继续推进到 Stage2、可结冰。
                SpawnUnitAt(nextPos);

                // 给最后这格单元挂一个拖尾子物体：向下偏移半格，锁定 Stage1，消除视觉悬空。
                // 拖尾不加入 units 列表、不参与回溯推进（纯视觉装饰，由父单元管理生命周期）。
                WaterFlowUnit lastUnit = units[units.Count - 1];
                SpawnTail(lastUnit);
                isFlowing = false;
                break;
            }

            SpawnUnitAt(nextPos);
            yield return new WaitForSeconds(flowInterval);
        }

        isFlowing = false;
        flowCoroutine = null;
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
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null) continue;

            if (hitCollider.CompareTag("PressurePlate")) continue;

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
            {
                pressurePlates.Add(plate);
            }
        }
    }

    // ===== 状态推进协程：轮询 Stage1 单元，成熟后切 Stage2 或 Frozen =====
    private IEnumerator AdvanceLoop()
    {
        while (true)
        {
            if (IsRewindOperationLocked)
            {
                yield return null;
                continue;
            }

            bool anyChanged = false;
            float now = Time.time;

            // 走状态机判定：已 Frozen 态下，新成熟单元直接进 Frozen 相位；否则进 Stage2。
            bool frozenMode = FreezeStateMachine.IsFrozen(freezeState);
            for (int i = 0; i < units.Count; i++)
            {
                WaterFlowUnit unit = units[i];
                if (unit == null) continue;

                if (unit.Phase == WaterFlowUnit.WaterPhase.Stage1 &&
                    !unit.LockAtStage1 &&
                    now - unit.BirthTime >= stage1ToStage2Seconds)
                {
                    unit.SetPhase(frozenMode
                        ? WaterFlowUnit.WaterPhase.Frozen
                        : WaterFlowUnit.WaterPhase.Stage2);

                    if (frozenMode)
                    {
                        PlayIceParticle(unit.transform.position);
                    }

                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                RecordSnapshot(); // 状态转变：Stage1→Stage2
                if (frozenMode)
                {
                    AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceFreeze);
                }
            }

            yield return new WaitForSeconds(0.1f); // 10Hz 轮询足够
        }
    }

    private void SpawnUnitAt(Vector3 worldPos)
    {
        if (unitTemplate == null)
        {
            Debug.LogError("[WaterSource] unitTemplate 未配置，无法生成水流单元。", this);
            isFlowing = false;
            return;
        }

        WaterFlowUnit unit = Instantiate(
            unitTemplate,
            worldPos,
            Quaternion.identity,
            spawnRoot != null ? spawnRoot : transform);
        unit.Init(this);
        units.Add(unit);
        RecordSnapshot(); // 状态转变：单元出生
    }

    /// <summary>
    /// 给最后命中地面的单元挂一个拖尾子物体，消除水流视觉悬空。
    /// 拖尾复用 WaterFlowUnit.prefab，作为父单元的子物体（localPosition.y = -cellSize/2），
    /// 锁定在 Stage1（不推进 Stage2），不加入 units 列表、不参与回溯推进——纯视觉装饰，随父单元销毁。
    /// </summary>
    private void SpawnTail(WaterFlowUnit parentUnit)
    {
        if (unitTemplate == null || parentUnit == null) return;

        WaterFlowUnit tail = Instantiate(unitTemplate, parentUnit.transform);
        tail.transform.localPosition = Vector3.down * (cellSize * 0.5f);
        tail.Init(null); // 不传 owner：拖尾的 Ice 永不到 Frozen，不需要碎冰通知
        tail.LockAtStage1 = true;
        tail.SetPhase(WaterFlowUnit.WaterPhase.Stage1);
    }
}
