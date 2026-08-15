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

    [Header("粒子效果")]
    [Tooltip("发射尖刺时播放的粒子特效。")]
    [SerializeField] private ParticleSystem shootingParticlePrefab;
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

        FlyingSpike spike = Instantiate(spikePrefab, transform.position, Quaternion.identity, transform);
        spike.transform.SetParent(null);
        
        spike.SetDamage(spikeDamage);
        spike.SetSpeed(spikeSpeed);
        spike.SetLifetime(spikeLifetime);

        spike.Launch(transform.up);
        PlayShootingParticle();
        AudioManager.Instance?.PlaySFX(MyEnums.SFXType.SpikeLaunch);
    }

    private void PlayShootingParticle()
    {
        if (shootingParticlePrefab == null) return;

        Transform parent = particlePoint != null ? particlePoint : transform;
        ParticleSystem particle = Instantiate(shootingParticlePrefab, parent);
        particle.transform.localPosition = Vector3.zero;
        particle.transform.localRotation = shootingParticlePrefab.transform.localRotation;
        particle.Play(true);

        var main = particle.main;
        Destroy(particle.gameObject, main.duration + main.startLifetime.constantMax);
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
