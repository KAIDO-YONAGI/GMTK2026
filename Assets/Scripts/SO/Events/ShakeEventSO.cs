using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ShakeEventSO", menuName = "Events/ShakeEventSO", order = 0)]
public class ShakeEventSO : ScriptableObject
{
    public event Action<CameraShake.ShakeParameters> ShakeEvent;

    public void RaiseShakeEvent(CameraShake.ShakeParameters parameters)
    {
        ShakeEvent?.Invoke(parameters);
    }
}
