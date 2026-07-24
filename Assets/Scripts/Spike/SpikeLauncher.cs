using UnityEngine;

/// <summary>
/// 尖刺发射器。外部调用 Activate()/Deactivate() 开启/关闭自动发射（间隔由 launchInterval 控制）。
/// Fire() 可外部直接触发一次单发。
/// 三个方法都加了 ContextMenu，可在 Editor 右键调用。
/// </summary>
public class SpikeLauncher : MonoBehaviour
{
    [SerializeField] private FlyingSpike spikePrefab;
    [SerializeField] private float launchInterval = 0f;
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    private bool isActive;
    private bool isPaused;
    private float timer;

    private void OnEnable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
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
        Debug.Log("Activate");
        isActive = !isActive;
    }

    [ContextMenu("Deactivate")]
    public void Deactivate()
    {
        Debug.Log("Deactivate");
        isActive = false;
    }

    private void Update()
    {
        if (!isActive || isPaused || launchInterval <= 0f) return;

        timer += Time.deltaTime;
        if (timer >= launchInterval)
        {
            timer = 0f;
            Fire();
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

        spike.Launch(transform.up);
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
