using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnParticleEventSO", menuName = "Events/SpawnParticleEventSO", order = 0)]
public class SpawnParticleEventSO : ScriptableObject
{
    /// <summary>粒子生成请求：世界位置 + 粒子类型。由 ParticlesManager 订阅并执行。</summary>
    public event Action<Vector3, MyEnums.ParticleType> SpawnParticleEvent;

    public void RaiseSpawnParticleEvent(Vector3 position, MyEnums.ParticleType type)
    {
        SpawnParticleEvent?.Invoke(position, type);
    }
}
