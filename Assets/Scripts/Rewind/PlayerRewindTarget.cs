using UnityEngine;

/// <summary>
/// 玩家回溯目标适配器。
/// 封装玩家状态抓取/还原，以及回溯期间的输入和刚体冻结。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRewindTarget : MonoBehaviour
{
    [Header("移动事件")]
    [SerializeField] private VoidEventSO movementBegEvent;
    [SerializeField] private VoidEventSO movementEndEvent;

    // 玩家组件引用全部由 Inspector 接线；不做运行时层级查找，也不假设本组件挂在玩家层级下。
    [Header("Player References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerActionController playerActionController;
    [SerializeField] private PlayerMovementFeedback playerMovementFeedback;

    private RigidbodyType2D bodyTypeBeforeRewind;
    private bool isFrozen;
    // 引用未接线的警告只报一次，避免刷屏。
    private bool warnedMissingActionController;
    private bool warnedMissingFeedback;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        WarnIfReferencesMissing();
    }

    private void OnDisable()
    {
        Unfreeze();
    }

    /// <summary>
    /// 抓取一帧回溯快照：位置、血量、动作状态与朝向。
    /// 动作/朝向引用未接线时降级为 None/朝右（见 WarnIfReferencesMissing 的警告）。
    /// </summary>
    public PlayerRewindSnapshot CaptureSnapshot(float time)
    {
        return new PlayerRewindSnapshot
        {
            time = time,
            position = playerTransform.position,
            health = HealthManager.Instance != null ? HealthManager.Instance.CurrentHealth : 0,
            actionState = playerActionController != null
                ? playerActionController.CurrentActionState
                : PlayerActionState.None,
            facingLeft = playerMovementFeedback != null &&
                         playerMovementFeedback.IsFacingLeft
        };
    }

    /// <summary>恢复快照：位置经刚体写入避免与物理竞争；动作状态与朝向只恢复表现，不触发物理。</summary>
    public void ApplySnapshot(PlayerRewindSnapshot snapshot)
    {
        if (playerRigidbody != null)
            playerRigidbody.position = new Vector2(snapshot.position.x, snapshot.position.y);
        else
            playerTransform.position = snapshot.position;

        if (HealthManager.Instance != null)
            HealthManager.Instance.SetHealthForReincarnation(snapshot.health);

        playerActionController?.ApplyRewindActionState(snapshot.actionState);
        playerMovementFeedback?.RestoreFacing(snapshot.facingLeft);
    }

    /// <summary>回溯开始：关闭输入、刚体改 Kinematic 并清空速度，防止倒放期间受物理干扰。</summary>
    public void Freeze()
    {
        if (isFrozen)
            return;

        isFrozen = true;
        movementBegEvent?.OnEventRaised();

        if (playerRigidbody == null)
            return;

        bodyTypeBeforeRewind = playerRigidbody.bodyType;
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
        playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>回溯结束：恢复刚体类型与输入；旧速度不属于当前时刻，清零处理。</summary>
    public void Unfreeze()
    {
        if (!isFrozen)
            return;

        if (playerRigidbody != null)
        {
            playerRigidbody.bodyType = bodyTypeBeforeRewind;
            // 回溯位置与回溯前速度不属于同一时刻，解冻时必须清空旧速度。
            playerRigidbody.velocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        isFrozen = false;
        movementEndEvent?.OnEventRaised();
    }

    /// <summary>引用未接线时快照会静默降级（动作恒 None、朝向恒右），启动时一次性警告便于排查。</summary>
    private void WarnIfReferencesMissing()
    {
        if (playerActionController == null && !warnedMissingActionController)
        {
            warnedMissingActionController = true;
            Debug.LogWarning("[PlayerRewindTarget] PlayerActionController reference is not assigned; snapshots will not record action state.");
        }

        if (playerMovementFeedback == null && !warnedMissingFeedback)
        {
            warnedMissingFeedback = true;
            Debug.LogWarning("[PlayerRewindTarget] PlayerMovementFeedback reference is not assigned; snapshots will not record facing.");
        }
    }
}
