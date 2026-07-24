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
    [SerializeField] private VoidEventSO reincarnationEvent;

    private Vector3 flyDirection;
    private float aliveTimer;
    private bool isFrozen;

    private struct SpikeSnapshot : IRewindSnapshot
    {
        public float time;
        public Vector3 position;
        public float Time => time;
    }

    private readonly Queue<SpikeSnapshot> snapshots = new Queue<SpikeSnapshot>();

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
    }

    private void OnEnable()
    {
        if (reincarnationEvent != null)
            reincarnationEvent.VoidEvent += StartRewind;
    }

    private void OnDisable()
    {
        if (reincarnationEvent != null)
            reincarnationEvent.VoidEvent -= StartRewind;
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
        if (other.TryGetComponent<Ice>(out _))
        {
            Destroy(other.gameObject);
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

        StartCoroutine(RewindBackwards(snapshots.ToArray(), ApplySnapshot, rewindSpeed,
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
    }
}
