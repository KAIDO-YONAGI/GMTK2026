using UnityEngine;

/// <summary>
/// 尖刺发射器。外部调用 Activate()/Deactivate() 开启/关闭自动发射（间隔由 launchInterval 控制）。
/// Fire() 可供外部直接触发一次单发。
/// 三个方法都加了 ContextMenu，可在编辑器中通过右键调用。
/// </summary>
public class SpikeLauncher : MonoBehaviour
{
    [SerializeField] private FlyingSpike spikePrefab;
    [SerializeField] private float launchInterval = 0f;
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    [Header("粒子事件")]
    [Tooltip("发射粒子的生成请求事件，由中央粒子管理器统一池化播放。")]
    [SerializeField] private SpawnParticleEventSO spawnParticleEvent;
    [Tooltip("固定发射粒子位置的变换节点，通常位于发射器头部附近。")]
    [SerializeField] private Transform particlePoint;
    
    [Header("尖刺参数")]
    [SerializeField] private float spikeSpeed = 5f;
    [SerializeField] private float spikeLifetime = 5f;
    [SerializeField] private int spikeDamage = 1;

    [Header("动画")]
    [SerializeField] private AnimationClip fireClip;
    [SerializeField] private Animator animator;

    private bool isActive;
    private bool isPaused;
    private float timer;
    private int fireClipHash;

    private void OnEnable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
        
        string stateName = fireClip.name;
        fireClipHash = Animator.StringToHash(stateName);
    }

    private void OnDisable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent -= OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
    }

    private void OnReincarnationBeg()
    {
        isPaused = true;
    }

    private void OnReincarnationEnd()
    {
        isPaused = false;
    }

    [ContextMenu("Activate")]
    public void Activate()
    {
        isActive = !isActive;
    }

    [ContextMenu("Deactivate")]
    public void Deactivate()
    {
        isActive = false;
    }

    private void Update()
    {
        if (!isActive || isPaused || launchInterval <= 0f) return;

        timer += Time.deltaTime;
        if (timer >= launchInterval)
        {
            timer = 0f;
            FireAnim();
        }
    }

    public void RotateByMouse(float angleDelta)
    {
        if (isPaused) return;
        transform.Rotate(0f, 0f, angleDelta);
    }

    [ContextMenu("Fire")]
    public void Fire()
    {
        if (spikePrefab == null) return;

        // 尖刺挂到发射器下收纳，避免散落在场景根目录；worldPositionStays=true 保持世界姿态，
        // 防止继承发射器 (1, 1.4, 1) 的非均匀缩放导致尖刺被拉长
        FlyingSpike spike = Instantiate(spikePrefab, transform.position, Quaternion.identity);
        spike.transform.SetParent(transform, true);
        
        spike.SetDamage(spikeDamage);
        spike.SetSpeed(spikeSpeed);
        spike.SetLifetime(spikeLifetime);

        spike.Launch(transform.up);
        PlayShootingParticle();
        AudioManager.Instance?.PlaySFX(MyEnums.SFXType.SpikeLaunch);
    }

    /// <summary>向中央粒子管理器请求在枪口位置播放发射粒子（世界坐标，不跟随发射器）。</summary>
    private void PlayShootingParticle()
    {
        Vector3 position = particlePoint != null ? particlePoint.position : transform.position;
        spawnParticleEvent?.RaiseSpawnParticleEvent(position, MyEnums.ParticleType.Shoot);
    }

    public void FireAnim()
    {
        if (animator != null)
        {
            animator.Play(fireClipHash, 0, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.up * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1.2f);
    }
}
