using System.Collections;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的结冰职责：冻结请求、倒计时、碎冰恢复以及冰粒子和音效。
/// </summary>
public partial class WaterSource
{
    /// <summary>
    /// 启动结冰倒计时（由 WaterFreezeTrigger 调用）。
    /// 走状态机：踩 trigger → freezeState 转 Requested。
    /// 是否真正启动协程看当前是否已经「冰完整且无碎冰水」——是则幂等忽略，否则启动倒计时。
    /// 回溯时若还原到 Requested 态（请求了但未结冰），OnReincarnationEnd 会自动重新结冰。
    /// </summary>
    public void StartFreezing()
    {
        if (IsRewindOperationLocked) return;

        // 记录玩家意图（Idle→Requested；已 Requested/Frozen 则幂等）。
        FreezeStateMachine.Request(ref freezeState);

        // 已结冰（Frozen）且无 Stage2（碎冰水）需要重冻 → 已是终态，不重复启动协程。
        if (FreezeStateMachine.IsFrozen(freezeState) && !HasStage2Units()) return;
        if (freezeCoroutine != null) return;

        freezeCoroutine = StartCoroutine(FreezeAfterDelay());
    }

    private bool HasStage2Units()
    {
        for (int i = 0; i < units.Count; i++)
        {
            WaterFlowUnit unit = units[i];
            if (unit != null && unit.Phase == WaterFlowUnit.WaterPhase.Stage2)
            {
                return true;
            }
        }

        return false;
    }

    private void PlayIceParticle(Vector3 position)
    {
        if (iceParticlePrefab == null) return;

        ParticleSystem particle = Instantiate(
            iceParticlePrefab,
            position,
            iceParticlePrefab.transform.rotation);
        particle.Play(true);

        ParticleSystem.MainModule main = particle.main;
        Destroy(particle.gameObject, main.duration + main.startLifetime.constantMax);
    }

    /// <summary>由 WaterFlowUnit 的 Ice.OnBroken 回调调用：单单元碎冰后变回水（Stage2），可再次结冰。</summary>
    public void NotifyUnitBroken(WaterFlowUnit unit)
    {
        if (unit == null) return;

        // 碎冰后切回 Stage2（满水），IceImg 子物体未销毁，后续可被 WaterFreezeTrigger 再次结冰。
        unit.ForcePhase(WaterFlowUnit.WaterPhase.Stage2);

        // 冰被击碎 = 玩家主动行为，撤回结冰意图：必须重新踩 trigger 才能再次结冰。
        // Frozen→Idle。其他未碎冰的单元视觉上仍是冰；这一格变 Stage2 等待重新被请求结冰。
        FreezeStateMachine.Break(ref freezeState);
        RecordSnapshot(); // 状态转变：碎冰→变回水

        if (!IsRewindOperationLocked)
        {
            PlayIceParticle(unit.transform.position);
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceBroken);
        }
    }

    // ===== 结冰协程：倒计时到点把所有 Stage2 单元切 Frozen =====
    private IEnumerator FreezeAfterDelay()
    {
        float elapsed = 0f;
        while (elapsed < freezeDelay)
        {
            if (!IsRewindOperationLocked) elapsed += Time.deltaTime;
            yield return null;
        }

        // 倒计时到点：状态机 Requested→Frozen。此后新成熟单元也直接结冰（AdvanceLoop 据此分流）。
        FreezeStateMachine.ConfirmFrozen(ref freezeState);
        bool anyChanged = false;
        for (int i = 0; i < units.Count; i++)
        {
            WaterFlowUnit unit = units[i];
            if (unit == null) continue;

            if (unit.Phase == WaterFlowUnit.WaterPhase.Stage2)
            {
                unit.SetPhase(WaterFlowUnit.WaterPhase.Frozen);
                PlayIceParticle(unit.transform.position);
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            RecordSnapshot(); // 状态转变：结冰
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceFreeze);
        }

        freezeCoroutine = null;
    }
}
