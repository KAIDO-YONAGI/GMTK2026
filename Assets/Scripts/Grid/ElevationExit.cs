using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevation_Exit : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] edgeColliders;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")//判断仅为玩家时才执行操作，防止因为怪物触发
        {
            foreach (Collider2D collider in mountainColliders)
            {
                collider.enabled = true;//恢复碰撞体积
            }
            foreach (Collider2D edge in edgeColliders)
            {
                edge.enabled = false;
            }

        }
        else return;
        collision.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 5;
    }
}
