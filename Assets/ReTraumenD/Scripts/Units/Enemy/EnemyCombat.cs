using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    public int damage = 1;
    public Transform attackPoint;
    public float weaponRange;
    public LayerMask playerLayer;
    public float konckBackForce=5;
    public float stunTime=0.3f;

    
    public void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);
        //意为在玩家层的一个以weaponRange为半径，attackPoint为圆心的圆形范围内查找目标，玩家层在unity中添加，能够排除其它对象

        if (hits.Length > 0&&hits[0].enabled)//如果有目标，并且目标具有PlayerHealth组件
        {
            // hits[0].GetComponent<PlayerHealth>().ChangeHealth(-damage);
            // hits[0].GetComponent<PlayerMovement>().KnockBack(transform, konckBackForce,stunTime);
        }
    }
}
