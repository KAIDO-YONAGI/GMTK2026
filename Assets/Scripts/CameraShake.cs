using Cinemachine;
using UnityEngine;
using System.Collections.Generic;

public class CameraShake : MonoBehaviour
{
    [System.Serializable]
    public struct ShakeParameters
    {
        public float intensity;
        public float duration;
        public float frequency;
    }

    [SerializeField] private ShakeEventSO shakeEvent;
    [Tooltip("The virtual camera driving this shake. Auto-detected from this GameObject if left empty.")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    private struct ActiveShake
    {
        public float startTime;
        public float duration;
        public float intensity;
        public float frequency;

        public ActiveShake(ShakeParameters p, float now)
        {
            startTime = now;
            duration = p.duration;
            intensity = p.intensity;
            frequency = p.frequency;
        }
    }

    private List<ActiveShake> _activeShakes = new();
    private CinemachineBasicMultiChannelPerlin _noise;

    private void Awake()
    {
        // 脚本挂在 VirtualCamera 上时，自动获取自身的虚拟相机组件
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (virtualCamera != null)
            _noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void OnEnable()
    {
        if (shakeEvent != null)
            shakeEvent.ShakeEvent += OnShakeEvent;
    }

    private void OnDisable()
    {
        if (shakeEvent != null)
            shakeEvent.ShakeEvent -= OnShakeEvent;
    }

    private void OnShakeEvent(ShakeParameters parameters)
    {
        _activeShakes.Add(new ActiveShake(parameters, Time.time));
    }

    private void LateUpdate()
    {
        if (_noise == null) return;

        float now = Time.time;
        float totalAmplitude = 0f;
        float weightedFrequency = 0f;

        for (int i = _activeShakes.Count - 1; i >= 0; i--)
        {
            ActiveShake shake = _activeShakes[i];
            float elapsed = now - shake.startTime;

            if (elapsed >= shake.duration)
            {
                _activeShakes.RemoveAt(i);
                continue;
            }

            // 平方衰减：开始最强，末尾平滑收敛到 0
            float normalized = 1f - elapsed / shake.duration;
            float decay = normalized * normalized;
            float amplitude = shake.intensity * decay;

            totalAmplitude += amplitude;
            // 按 amplitude 加权累计 frequency，多震动叠加时取主导震动的频率
            weightedFrequency += amplitude * shake.frequency;
        }

        // 通过 Cinemachine 的 Noise 组件驱动震动，不直接改 transform.position，
        // 避免被 CinemachineBrain 在同帧覆盖
        _noise.m_AmplitudeGain = totalAmplitude;
        _noise.m_FrequencyGain = totalAmplitude > 0f ? weightedFrequency / totalAmplitude : 0f;
    }
}
