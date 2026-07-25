using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private AudioConfigSO config;
    [SerializeField] private int maxPoolSize = 16;
    [SerializeField] private int warmupCount = 4;

    [Header("BGM")]
    [SerializeField] private float crossfadeDuration = 1f;

    [Header("Volume")]
    [Range(0f, 1f)] public float sfxVolume = 0.5f;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    private AudioSource _bgmSource;
    private Queue<AudioPlayer> _pool = new();
    private int _totalCreated;
    private MyEnums.BGMType _currentBGM;
    private Coroutine _crossfadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
    }

    [Header("Startup")]
    [Tooltip("启动时自动播放的 BGM 类型。设为 Default 则不自动播放。")]
    [SerializeField] private MyEnums.BGMType startupBGM = MyEnums.BGMType.Gaming;

    private void Start()
    {
        Prewarm();
        // 启动时自动播放全局 BGM（AudioConfig 未配置该类型时安全跳过）
        if (startupBGM != MyEnums.BGMType.Default)
            PlayBGM(startupBGM);
    }

    private void Prewarm()
    {
        for (int i = 0; i < warmupCount; i++)
        {
            var go = new GameObject("AudioPlayer");
            go.transform.SetParent(transform);
            var p = go.AddComponent<AudioPlayer>();
            p.Init(this);
            p.StopAllCoroutines();
            go.SetActive(false);
            _pool.Enqueue(p);
            _totalCreated++;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>根据音效枚举播放</summary>
    public void PlaySFX(MyEnums.SFXType type)
    {
        if (config == null) return;
        AudioClip clip = config.GetSFXClip(type);
        if (clip == null) return;
        PlayClip(clip);
    }

    /// <summary>直接播放指定 AudioClip</summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        AudioPlayer player = GetPlayer();
        if (player == null) return;
        player.Play(clip, sfxVolume);
    }

    /// <summary>播放 BGM（带淡入淡出）</summary>
    public void PlayBGM(MyEnums.BGMType type)
    {
        if (config == null) return;
        if (type == _currentBGM && _bgmSource.isPlaying) return;
        AudioClip clip = config.GetBGMClip(type);
        if (clip == null) return;

        if (_crossfadeCoroutine != null)
            StopCoroutine(_crossfadeCoroutine);
        _crossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
        _currentBGM = type;
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        if (_bgmSource.isPlaying && _bgmSource.clip != null)
        {
            float startVol = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / crossfadeDuration);
                yield return null;
            }
            _bgmSource.Stop();
        }

        _bgmSource.clip = newClip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        float elapsed2 = 0f;
        while (elapsed2 < crossfadeDuration)
        {
            elapsed2 += Time.deltaTime;
            _bgmSource.volume = Mathf.Lerp(0f, bgmVolume, elapsed2 / crossfadeDuration);
            yield return null;
        }
        _bgmSource.volume = bgmVolume;
        _crossfadeCoroutine = null;
    }

    private AudioPlayer GetPlayer()
    {
        if (_pool.Count > 0)
        {
            var p = _pool.Dequeue();
            if (p != null) return p;
        }

        if (_totalCreated < maxPoolSize)
        {
            var go = new GameObject("AudioPlayer");
            go.transform.SetParent(transform);
            var p = go.AddComponent<AudioPlayer>();
            p.Init(this);
            _totalCreated++;
            return p;
        }

        return null;
    }

    public void ReturnPlayer(AudioPlayer player)
    {
        if (player == null) return;
        player.StopAllCoroutines();
        if (player.gameObject != null)
        {
            player.gameObject.SetActive(false);
            player.transform.SetParent(transform);
        }
        _pool.Enqueue(player);
    }

    public void StopAllSFX()
    {
        foreach (var p in GetComponentsInChildren<AudioPlayer>(true))
            p.StopAndReturn();
    }

    public void StopBGM()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }
        _bgmSource.Stop();
    }
}
