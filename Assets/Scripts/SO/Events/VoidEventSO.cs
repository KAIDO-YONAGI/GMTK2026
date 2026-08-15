using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO", order = 0)]

public class VoidEventSO : ScriptableObject
{
    public event Action VoidEvent;

    public void OnEventRaised()
    {
        VoidEvent?.Invoke();
    }
}
