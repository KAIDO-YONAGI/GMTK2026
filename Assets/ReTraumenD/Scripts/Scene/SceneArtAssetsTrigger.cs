using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 场景美术资产触发器。放在每个内容场景里，订阅 SceneLoadEventSO。
/// 当收到事件且 scene == artAssets.ownerScene 时，本场景即将激活 → 播放音频；
/// 否则本场景即将被卸载 → 停止音频。
/// 同时通过 OnEnable/OnDisable 兜底（场景首次加载/卸载时事件可能尚未触发）。
/// </summary>
public class SceneArtAssetsTrigger : MonoBehaviour
{
    [Header("Config")]
    public SceneArtAssetsSO artAssets;
    public SceneLoadEventSO loadEventSO;

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource ambientSource;

    private bool isMineActive;

    void OnEnable()
    {
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent += OnLoadRequest;
        // 场景刚加载时默认假设自己是激活的（首次启动场景时事件还没来得及触发）
        isMineActive = true;
        ApplyAudio();
    }

    void OnDisable()
    {
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent -= OnLoadRequest;
        StopAudio();
    }

    private void OnLoadRequest(List<GameSceneSO> scenes, Vector3 pos, bool isFade)
    {
        // 目标列表包含本场景 SO 时，本场景即将被加载（激活）。
        if (artAssets != null && artAssets.ownerScene != null)
            isMineActive = scenes != null && scenes.Contains(artAssets.ownerScene);
        else
            isMineActive = false;
        ApplyAudio();
    }

    private void ApplyAudio()
    {
        if (isMineActive) PlayAudio();
        else StopAudio();
    }

    private void PlayAudio()
    {
        if (artAssets == null) return;
        if (bgmSource != null && artAssets.bgm != null)
        {
            bgmSource.clip = artAssets.bgm;
            bgmSource.volume = artAssets.bgmVolume;
            bgmSource.loop = true;
            bgmSource.Play();
        }
        if (ambientSource != null && artAssets.ambient != null)
        {
            ambientSource.clip = artAssets.ambient;
            ambientSource.volume = artAssets.ambientVolume;
            ambientSource.loop = true;
            ambientSource.Play();
        }
    }

    private void StopAudio()
    {
        if (bgmSource != null) bgmSource.Stop();
        if (ambientSource != null) ambientSource.Stop();
    }
}
