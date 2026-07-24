using Cinemachine;
using UnityEngine;

/// <summary>
/// 临时诊断脚本：排查"玩家冲出边界后相机鬼畜"的根因。
/// 每 0.5 秒打印相机位置、玩家位置、玩家是否在 Confiner 边界内、FramingTransposer 阻尼状态。
/// 确认根因后删除此脚本。
/// </summary>
public class CameraJitterDiagnostic : MonoBehaviour
{
    private CinemachineVirtualCamera vcam;
    private CinemachineConfiner2D confiner;
    private CinemachineFramingTransposer body;
    private Transform player;
    private float timer;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        confiner = GetComponent<CinemachineConfiner2D>();
        body = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
    }

    private void Start()
    {
        // 找玩家（VirtualCamera 的 Follow 目标）
        if (vcam != null && vcam.Follow != null) player = vcam.Follow;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void LateUpdate()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 0.1f) return; // 10Hz 采样，减少日志量
        timer = 0f;

        if (player == null || confiner == null || confiner.m_BoundingShape2D == null)
        {
            Debug.Log("[CamDiag] 缺少 player 或 confiner, player=" + (player!=null) + " confiner=" + (confiner!=null) + " shape=" + (confiner!=null && confiner.m_BoundingShape2D!=null));
            return;
        }

        Vector3 camPos = transform.position;
        Vector3 playerPos = player.position;
        Bounds b = confiner.m_BoundingShape2D.bounds;
        bool playerInside = b.Contains(playerPos);
        // 相机视野半高/半宽（正交 size=5 → 半高 5，半宽 = 5 * aspect）
        float camHalfH = 5f;
        float camHalfW = camHalfH * Camera.main.aspect;
        // 相机视野是否完全在边界内（Confiner 的约束目标）
        bool camViewInside = (camPos.x - camHalfW >= b.min.x) && (camPos.x + camHalfW <= b.max.x)
                          && (camPos.y - camHalfH >= b.min.y) && (camPos.y + camHalfH <= b.max.y);

        Debug.Log("[CamDiag] cam=" + camPos.ToString("F2") + " player=" + playerPos.ToString("F2")
            + " playerInBounds=" + playerInside + " camViewInBounds=" + camViewInside
            + " dampX=" + (body!=null?body.m_XDamping.ToString("F2"):"?"));
    }
}
