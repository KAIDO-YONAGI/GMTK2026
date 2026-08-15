using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class VolumeManager : YSingleton<VolumeManager>
{
    [SerializeField, Min(0f)] private float maxVignetteIntensity = 0.5f;

    private Volume volume;

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private DepthOfField depthOfField;
    private Tonemapping tonemapping;

    protected override void OnSingletonInitialized()
    {
        volume = GetComponent<Volume>();
        CacheComponents();
    }

    private void Update()
    {
        if (vignette == null || AgeManager.Instance == null) return;

        float t = AgeManager.Instance.AgeNormalized;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = Mathf.Lerp(0f, maxVignetteIntensity, Mathf.Pow(t, 0.5f));
    }

    private void CacheComponents()
    {
        if (volume == null || volume.profile == null) return;
        volume.profile.TryGet(out vignette);
        volume.profile.TryGet(out colorAdjustments);
        volume.profile.TryGet(out bloom);
        volume.profile.TryGet(out depthOfField);
        volume.profile.TryGet(out tonemapping);
    }

    public Vignette Vignette => vignette;
    public ColorAdjustments ColorAdjustments => colorAdjustments;
    public Bloom Bloom => bloom;
    public DepthOfField DepthOfField => depthOfField;
    public Tonemapping Tonemapping => tonemapping;

    public void SetVignetteIntensity(float intensity)
    {
        if (vignette == null) return;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = intensity;
    }

    public void SetSaturation(float saturation)
    {
        if (colorAdjustments == null) return;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = saturation;
    }

    public void SetExposure(float exposure)
    {
        if (colorAdjustments == null) return;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = exposure;
    }

    public void SetBloomIntensity(float intensity)
    {
        if (bloom == null) return;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = intensity;
    }

    public void EnableVignette(bool enable)
    {
        if (vignette == null) return;
        vignette.active = enable;
    }

    public void EnableBloom(bool enable)
    {
        if (bloom == null) return;
        bloom.active = enable;
    }
}
