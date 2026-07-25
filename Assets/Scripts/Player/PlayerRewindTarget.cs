using UnityEngine;

/// <summary>
/// 玩家回溯目标适配器。
/// 封装玩家状态抓取/还原，以及回溯期间的输入和刚体冻结。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRewindTarget : MonoBehaviour
{
    [Header("Movement Events")]
    [SerializeField] private VoidEventSO movementBegEvent;
    [SerializeField] private VoidEventSO movementEndEvent;

    private Transform playerTransform;
    private Rigidbody2D playerRigidbody;

    private RigidbodyType2D bodyTypeBeforeRewind;
    private Vector2 velocityBeforeRewind;
    private float angularVelocityBeforeRewind;
    private bool isFrozen;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        ResolvePlayerComponents();
    }

    private void OnDisable()
    {
        Unfreeze();
    }

    public PlayerRewindSnapshot CaptureSnapshot(float time)
    {
        ResolvePlayerComponents();

        return new PlayerRewindSnapshot
        {
            time = time,
            position = playerTransform.position,
            health = HealthManager.Instance != null ? HealthManager.Instance.CurrentHealth : 0
        };
    }

    public void ApplySnapshot(PlayerRewindSnapshot snapshot)
    {
        ResolvePlayerComponents();

        if (playerRigidbody != null)
            playerRigidbody.position = new Vector2(snapshot.position.x, snapshot.position.y);
        else
            playerTransform.position = snapshot.position;

        if (HealthManager.Instance != null)
            HealthManager.Instance.SetHealthForReincarnation(snapshot.health);
    }

    public void Freeze()
    {
        if (isFrozen)
            return;

        ResolvePlayerComponents();
        isFrozen = true;
        movementBegEvent?.OnEventRaised();

        if (playerRigidbody == null)
            return;

        bodyTypeBeforeRewind = playerRigidbody.bodyType;
        velocityBeforeRewind = playerRigidbody.velocity;
        angularVelocityBeforeRewind = playerRigidbody.angularVelocity;

        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
        playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Unfreeze()
    {
        if (!isFrozen)
            return;

        if (playerRigidbody != null)
        {
            playerRigidbody.bodyType = bodyTypeBeforeRewind;
            playerRigidbody.velocity = velocityBeforeRewind;
            playerRigidbody.angularVelocity = angularVelocityBeforeRewind;
        }

        isFrozen = false;
        movementEndEvent?.OnEventRaised();
    }

    private void ResolvePlayerComponents()
    {
        if (playerTransform == null)
            playerTransform = transform.root;
        if (playerRigidbody == null && playerTransform != null)
            playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
    }
}
