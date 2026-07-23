using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReincarnationManager : MonoBehaviour
{
    [Header("Send Reincarnation Message")]
    [SerializeField] private VoidEventSO reincarnationEvent;

    [Header("Rewind Settings")]
    [SerializeField] private float maxRewindBaseSeconds = 8f;
    [SerializeField] private float minRewindBaseSeconds = 1f;

    [Tooltip("Rewind playback speed. 1 = real-time reverse (1:1 with recorded time). " +
             "Higher = faster rewind. Driven by real elapsed time, not fixed step.")]
    [SerializeField] private float rewindSpeed = 1f;

    [Tooltip("Cooldown after rewind playback finishes before new snapshots are recorded. " +
             "Input is restored immediately; only recording stays paused.")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    // 玩家位置 + 血量快照（值类型，避免 GC 压力）
    private struct PlayerSnapshot
    {
        public float time;
        public Vector3 position;
        public int health;
    }

    // 物理帧入队的快照队列
    private readonly Queue<PlayerSnapshot> snapshots = new Queue<PlayerSnapshot>();

    // 当前衰老速率：越年轻越接近 1，越老越接近 0
    private float agingRate = 1f;

    // 回溯进行中标记：冻结输入 + 停止记录新快照
    private bool isRewinding = false;

    // 玩家根 Transform（位置来源）和移动脚本（冻结输入用）
    private Transform playerTransform;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        playerTransform = transform.root;
        playerMovement = playerTransform.GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        // 按下 R 键触发轮回空事件 + 启动回溯（引用未拖入时跳过，避免空引用）
        if (Input.GetKeyDown(KeyCode.R) && reincarnationEvent != null)
        {
            reincarnationEvent.OnEventRaised();
            OnReincarnationRised();
        }
    }

    /// <summary>
    /// 每个物理帧更新：刷新衰老速率、记录快照、按可回溯时间裁剪队列。
    /// </summary>
    private void FixedUpdate()
    {
        // 回溯进行中不记录新快照，避免与回放互相干扰
        if (isRewinding) return;

        // 1. 更新衰老速率：年轻→1，老→0（AgeManager.instance 已做 Clamp01）
        if (AgeManager.instance != null)
        {
            agingRate = 1f - AgeManager.instance.AgeNormalized;
        }

        // 2. 入队当前快照（位置来自玩家根，血量来自单例 HealthManager）
        var snap = new PlayerSnapshot
        {
            time = Time.time,
            position = playerTransform.position,
            health = HealthManager.Instance != null ? HealthManager.Instance.CurrentHealth : 0
        };
        snapshots.Enqueue(snap);

        // 3. 严格按可回溯时间裁剪：丢弃所有超出窗口的旧快照
        // 可回溯时间：年轻(agingRate=1)→max，老(agingRate=0)→min，按 agingRate 线性插值
        float rewindableSeconds = Mathf.Lerp(minRewindBaseSeconds, maxRewindBaseSeconds, agingRate);
        float expireBefore = Time.time - rewindableSeconds;
        while (snapshots.Count > 0 && snapshots.Peek().time < expireBefore)
        {
            snapshots.Dequeue();
        }
    }

    /// <summary>
    /// R 键触发：倒带回溯。以真实时间驱动，rewoundGameTime = elapsed × rewindSpeed。
    /// 回溯掉的游戏时间与记录时一致（rewindSpeed=1 时为 1:1 实时倒放）。
    /// </summary>
    private void OnReincarnationRised()
    {
        if (isRewinding) return;        // 防重入
        if (snapshots.Count == 0) return;

        StartCoroutine(RewindRoutine());
    }

    private IEnumerator RewindRoutine()
    {
        isRewinding = true;

        // 冻结玩家输入
        if (playerMovement != null) playerMovement.enabled = false;

        // 队列转数组，按时间戳驱动回溯（最新 → 最早）
        var arr = snapshots.ToArray();
        int idx = arr.Length - 1;
        float latestTime = arr[idx].time;   // 回溯起点的游戏时间
        float rewindStartReal = Time.time;   // 回溯开始的真实时间

        // 先落到最新一帧
        ApplySnapshot(arr[idx]);

        // 按真实时间推进：elapsed × rewindSpeed = 已倒回的游戏时间量
        while (idx > 0)
        {
            yield return null;

            float rewoundGameTime = (Time.time - rewindStartReal) * rewindSpeed;
            float targetTime = latestTime - rewoundGameTime;

            // 跳过所有比 targetTime 更新的快照，应用到第一个 <= targetTime 的
            while (idx > 0 && arr[idx - 1].time >= targetTime)
            {
                idx--;
                ApplySnapshot(arr[idx]);
            }
        }

        // ===== 回溯播放完毕 =====

        // 恢复玩家输入（冷却期间玩家可操作，只是不记录新快照）
        if (playerMovement != null) playerMovement.enabled = true;

        // 冷却：期间 isRewinding 仍为 true，FixedUpdate 不会记录新快照
        yield return new WaitForSeconds(rewindCooldownSeconds);

        // 冷却结束：清空已消费的队列、解除回溯冻结（FixedUpdate 重新记录）
        snapshots.Clear();
        isRewinding = false;
    }

    private void ApplySnapshot(in PlayerSnapshot snap)
    {
        // 恢复位置
        playerTransform.position = snap.position;

        // 恢复血量（走轮回专用接口，绕过死亡/治疗事件）
        if (HealthManager.Instance != null)
        {
            HealthManager.Instance.SetHealthForReincarnation(snap.health);
        }
    }
}
