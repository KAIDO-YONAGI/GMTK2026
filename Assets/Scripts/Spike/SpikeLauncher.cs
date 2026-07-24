using UnityEngine;

/// <summary>
/// 尖刺发射器。外部调用 Activate()/Deactivate() 开启/关闭自动发射（间隔由 launchInterval 控制）。
/// Fire() 可外部直接触发一次单发。
/// 三个方法都加了 ContextMenu，可在 Editor 右键调用。
/// </summary>
public class SpikeLauncher : MonoBehaviour
{
    [SerializeField] private FlyingSpike spikePrefab;
    [SerializeField] private Vector2 launchDirection = Vector2.right;
    [SerializeField] private float launchInterval = 0f;

    private bool isActive;
    private float timer;

    [ContextMenu("Activate")]
    public void Activate()
    {
        isActive = true;
    }

    [ContextMenu("Deactivate")]
    public void Deactivate()
    {
        isActive = false;
    }

    private void Update()
    {
        if (!isActive || launchInterval <= 0f) return;

        timer += Time.deltaTime;
        if (timer >= launchInterval)
        {
            timer = 0f;
            Fire();
        }
    }

    [ContextMenu("Fire")]
    public void Fire()
    {
        if (spikePrefab == null) return;

        FlyingSpike spike = Instantiate(spikePrefab, transform.position, Quaternion.identity);
        spike.Launch(launchDirection);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, launchDirection.normalized * 2f);
    }
}
