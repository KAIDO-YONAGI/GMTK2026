using System;
using System.Collections;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的冻结控制器。
/// 负责结冰请求、倒计时、碎冰恢复以及冰粒子和音效。
/// </summary>
internal sealed class WaterFreezeController
{
    private readonly WaterSource owner;
    private readonly WaterSourceRuntimeState state;
    private readonly Func<bool> isOperationLocked;
    private readonly Action recordSnapshot;
    private readonly float freezeDelay;
    private readonly ParticleSystem iceParticlePrefab;

    public WaterFreezeController(
        WaterSource owner,
        WaterSourceRuntimeState state,
        Func<bool> isOperationLocked,
        Action recordSnapshot,
        float freezeDelay,
        ParticleSystem iceParticlePrefab)
    {
        this.owner = owner;
        this.state = state;
        this.isOperationLocked = isOperationLocked;
        this.recordSnapshot = recordSnapshot;
        this.freezeDelay = freezeDelay;
        this.iceParticlePrefab = iceParticlePrefab;
    }

    public bool IsFrozen()
    {
        return FreezeStateMachine.IsFrozen(state.FreezeState);
    }

    public void StartFreezing()
    {
        if (isOperationLocked())
            return;

        FreezeStateMachine.Request(ref state.FreezeState);

        if (FreezeStateMachine.IsFrozen(state.FreezeState) &&
            !HasStage2Units())
        {
            return;
        }

        if (state.FreezeCoroutine != null)
            return;

        state.FreezeCoroutine = owner.StartCoroutine(FreezeAfterDelay());
    }

    public void NotifyUnitBroken(WaterFlowUnit unit)
    {
        if (unit == null)
            return;

        unit.ForcePhase(WaterFlowUnit.WaterPhase.Stage2);
        FreezeStateMachine.Break(ref state.FreezeState);
        recordSnapshot();

        if (isOperationLocked())
            return;

        PlayIceParticle(unit.transform.position);
        AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceBroken);
    }

    public void StopForRewind()
    {
        if (state.FreezeCoroutine == null)
            return;

        owner.StopCoroutine(state.FreezeCoroutine);
        state.FreezeCoroutine = null;
    }

    public void ResumeAfterRewind()
    {
        if (!FreezeStateMachine.IsPendingFreeze(state.FreezeState) ||
            state.FreezeCoroutine != null)
        {
            return;
        }

        state.FreezeCoroutine = owner.StartCoroutine(FreezeAfterDelay());
    }

    public void Dispose()
    {
        StopForRewind();
    }

    public void PlayIceParticle(Vector3 position)
    {
        if (iceParticlePrefab == null)
            return;

        ParticleSystem particle = UnityEngine.Object.Instantiate(
            iceParticlePrefab,
            position,
            iceParticlePrefab.transform.rotation);
        particle.Play(true);

        ParticleSystem.MainModule main = particle.main;
        UnityEngine.Object.Destroy(
            particle.gameObject,
            main.duration + main.startLifetime.constantMax);
    }

    private bool HasStage2Units()
    {
        for (int i = 0; i < state.Units.Count; i++)
        {
            WaterFlowUnit unit = state.Units[i];
            if (unit != null &&
                unit.Phase == WaterFlowUnit.WaterPhase.Stage2)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator FreezeAfterDelay()
    {
        float elapsed = 0f;
        while (elapsed < freezeDelay)
        {
            if (!isOperationLocked())
                elapsed += Time.deltaTime;
            yield return null;
        }

        FreezeStateMachine.ConfirmFrozen(ref state.FreezeState);
        bool anyChanged = false;
        for (int i = 0; i < state.Units.Count; i++)
        {
            WaterFlowUnit unit = state.Units[i];
            if (unit == null ||
                unit.Phase != WaterFlowUnit.WaterPhase.Stage2)
            {
                continue;
            }

            unit.SetPhase(WaterFlowUnit.WaterPhase.Frozen);
            PlayIceParticle(unit.transform.position);
            anyChanged = true;
        }

        if (anyChanged)
        {
            recordSnapshot();
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.IceFreeze);
        }

        state.FreezeCoroutine = null;
    }
}
