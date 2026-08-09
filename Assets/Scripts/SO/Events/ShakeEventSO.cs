using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "ShakeEventSO", menuName = "Events/ShakeEventSO", order = 0)]
public class ShakeEventSO : ScriptableObject
{
    public UnityAction<CameraShake.ShakeParameters> ShakeEvent;

    public void RaiseShakeEvent(CameraShake.ShakeParameters parameters)
    {
        ShakeEvent?.Invoke(parameters);
    }
}
