using System;
using System.Collections.Generic;
using UnityEngine;

public class FlyingSpike : RewindableBase
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float rewindSpeed = 1f;
    [SerializeField] private float rewindCooldownSeconds = 1f;
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;
    [SerializeField] private FloatEventSO snapshotAppliedEvent;

    private Vector3 flyDirection;
    private float aliveTimer;
    private float birthTime;
    private bool isFrozen;

    private struct SpikeSnapshot : IRewindSnapshot
    {
        public float time;
        public Vector3 position;
        public float Time => time;
    }

    private readonly Queue<SpikeSnapshot> snapshots = new Queue<SpikeSnapshot>();

    // 倒放数组对象池：复用 StartRewind 时由队列转出的数组，消除每次回溯的分配
    private readonly ArrayPool<SpikeSnapshot> snapshotArrayPool = new ArrayPool<SpikeSnapshot>();
    private SpikeSnapshot[] borrowedPlaybackArray;

    private float GetRewindableSeconds()
    {
        if (ReincarnationManager.Instance != null)
            return ReincarnationManager.Instance.CurrentRewindableSeconds;
        return 8f;
    }

    public void Launch(Vector3 direction)
    {
        flyDirection = direction.normalized;
        transform.up = flyDirection;
        birthTime = Time.time;
    }

    private void OnEnable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
        if (snapshotAppliedEvent != null)
            snapshotAppliedEvent.FloatEvent += OnSnapshotApplied;
    }

    private void OnDisable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent -= OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
        if (snapshotAppliedEvent != null)
            snapshotAppliedEvent.FloatEvent -= OnSnapshotApplied;
    }

    private void OnReincarnationBeg()
    {
        StartRewind();
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        if (snapshotTime < birthTime)
            Destroy(gameObject);
    }

    private void OnReincarnationEnd()
    {
        StopAllCoroutines();
        OnUnfreeze();
        if (this == null) return;
        ClearSnapshots();
        isRewinding = false;
        isRewindCoolingDown = false;
    }
    
    private void Update()
    {
        if (isFrozen || IsRewindOperationLocked) return;

        aliveTimer += Time.deltaTime;
        if (aliveTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += flyDirection * speed * Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (IsRewindOperationLocked) return;

        RecordSnapshot();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Ice>(out var ice))
        {
            ice.Break();
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            if (HealthManager.Instance != null)
                HealthManager.Instance.ChangeHealth(-damage);
        }
    }

    // ===== RewindableBase =====

    public override int SnapshotCount => snapshots.Count;

    public override void RecordSnapshot()
    {
        var snap = new SpikeSnapshot
        {
            time = Time.time,
            position = transform.position
        };

        EnqueueAndTrim(snapshots, snap, GetRewindableSeconds());
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked) return;
        if (snapshots.Count == 0) return;

        // 倒放数组从池借出，CopyTo 填入；协程内部用 count 而非 Length（池借出的 Length 可能更大）
        int count = snapshots.Count;
        borrowedPlaybackArray = snapshotArrayPool.Get(count);
        snapshots.CopyTo(borrowedPlaybackArray, 0);

        StartCoroutine(RewindBackwards(borrowedPlaybackArray, count, ApplySnapshot, rewindSpeed,
            GetRewindableSeconds, rewindCooldownSeconds));
    }

    private void ApplySnapshot(SpikeSnapshot snap)
    {
        transform.position = snap.position;
    }

    protected override bool ShouldContinueRewind()
    {
        return Input.GetKey(KeyCode.R);
    }

    protected override void OnFreeze()
    {
        isFrozen = true;
    }

    protected override void OnUnfreeze()
    {
        isFrozen = false;
    }

    protected override void ClearSnapshots()
    {
        snapshots.Clear();
        // 归还倒放数组到池（与 StartRewind 的借出配对）
        if (borrowedPlaybackArray != null)
        {
            snapshotArrayPool.Return(borrowedPlaybackArray);
            borrowedPlaybackArray = null;
        }
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, speed);
    }
    
    public void SetLifetime(float newLifetime)
    {
        lifetime = Mathf.Max(0f, lifetime);
    }
    
    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(0, damage);
    }
}
