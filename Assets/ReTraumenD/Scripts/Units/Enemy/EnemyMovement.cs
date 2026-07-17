using UnityEngine;
using MyEnums;
using System.Collections.Generic;
public class EnemyMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform player;
    private Animator animator;

    private int facingDirec = 1;//敌人朝向
    private EnemyState enemyState;//存储敌人唯一的状态
    private float attackCoolDownTimer;//攻击间隔冷却计时器

    public int speed = 5;
    public float attackDetectRange = 2;
    public float attackCoolDown = 1;//每次恢复计时会被赋给计时器
    public float playerDetectRange = 5;
    public Transform detectionPoint;//侦测点，可以代替OnCollisionEnter2D碰撞触发
    public LayerMask playerMask;//创建公共玩家层，在unity中完成绑定
    public MovementController aStarController;

    private float velocityCooldown = 0.2f;
    private float velocityTimer;
    private Vector2 lastVelocity;
    float threshold;
    private bool playerInRange;
    private float detectInterval = 0.2f;
    private float detectTimer;




    public void AnimatorSM(EnemyState newState)
    {
        //退出当前动画
        if (enemyState == EnemyState.Idle)
            animator.SetBool("isIdle", false);
        else if (enemyState == EnemyState.Chasing)
            animator.SetBool("isChasing", false);
        else if (enemyState == EnemyState.Attacking)
            animator.SetBool("isAttacking", false);
        //更新状态
        enemyState = newState;
        //进入新动画
        if (enemyState == EnemyState.Idle)
            animator.SetBool("isIdle", true);
        else if (enemyState == EnemyState.Chasing)
            animator.SetBool("isChasing", true);
        else if (enemyState == EnemyState.Attacking)
            animator.SetBool("isAttacking", true);
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }
    private void Start()
    {
        AnimatorSM(EnemyState.Idle);//注意状态改变需要在找到animator之后才开始
        threshold = aStarController.GetThreshold();
    }
    private void Update()
    {
        if (enemyState != EnemyState.KnockBack)
        {
            detectTimer -= Time.deltaTime;
            if (detectTimer <= 0)
            {
                CheckForPlayer();
                detectTimer = detectInterval;
            }

            if (attackCoolDownTimer > 0)
                attackCoolDownTimer -= Time.deltaTime;//减去了实际的时间，保证间隔一致，不会因为游戏帧数/刷新率而变化

            if (enemyState == EnemyState.Chasing)
                Chase();
            else if (enemyState == EnemyState.Attacking)
                rb.velocity = Vector2.zero;
            else if (enemyState == EnemyState.Idle && !playerInRange)
                aStarController.ResetPath();
        }
    }

    private void CheckForPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(detectionPoint.position, playerDetectRange, playerMask);
        if (hit != null)
        {
            playerInRange = true;
            player = hit.transform;
            if ((player.position - transform.position).sqrMagnitude <= attackDetectRange * attackDetectRange && attackCoolDownTimer <= 0)
            {
                AnimatorSM(EnemyState.Attacking);
                attackCoolDownTimer = attackCoolDown;//重置时间
                return;
            }
            else if ((player.position - transform.position).sqrMagnitude > attackDetectRange * attackDetectRange && enemyState == EnemyState.Idle)
            {
                AnimatorSM(EnemyState.Chasing);
            }
        }
        else
        {
            playerInRange = false;
            rb.velocity = Vector2.zero;
            AnimatorSM(EnemyState.Idle);
        }
    }

    void Chase()
    {
        if (aStarController == null)
            return;

        if (velocityTimer > 0)
            velocityTimer -= Time.deltaTime;

        Vector3 startPos = transform.position;
        Vector3 endPos = player.position;
        Vector3 optPos = (player.position - transform.position).normalized * .2f + startPos;
        //防止敌人产生远离玩家的路径，增加一个优化点，优先从这个点开始寻路，如果这个点不可行走才从敌人当前位置开始寻路

        Vector3 posToGo = aStarController.GetPosToGo(optPos, startPos, endPos);
        Vector2 direction = Vector2.zero;
        if (!(posToGo == Vector3.zero) && posToGo != null)
            direction = (posToGo - transform.position).normalized;

        else
        {
            AnimatorSM(EnemyState.Idle);
            return;
        }

        SetVelocity(direction, speed);

        if ((transform.position - posToGo).sqrMagnitude < threshold * threshold)
        {
            aStarController.ArrivedPos();
        }
    }
    private void SetVelocity(Vector2 direction, float speed)
    {
        bool shouldUpdate = velocityTimer <= 0 || lastVelocity == Vector2.zero ||
                           Vector2.Dot(direction, lastVelocity) < 0.9f;

        if (shouldUpdate)
        {
            rb.velocity = direction * speed;
            lastVelocity = direction;
            velocityTimer = velocityCooldown;

            if (direction.x * facingDirec < 0)
            {
                Flip();
            }
        }
    }
    private void Flip()
    {
        facingDirec *= -1;
        transform.localScale = new Vector3(-1 * transform.localScale.x, transform.localScale.y, transform.localScale.z);
        //这个向量应该乘-1而不是facingDirec，因为facingDirec仅记录当前的状态（朝向），可以为1
    }
    private void OnDrawGizmosSelected()//为查找角色的环着色
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(detectionPoint.position, playerDetectRange);
    }
}

