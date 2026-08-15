using UnityEngine;

namespace WaterMechanism
{
    /// <summary>
    /// 水源冻结状态机的转换逻辑封装（纯逻辑，不持有 Unity 对象）。
    ///
    /// 设计目的：把"状态如何流转"的规则集中在一处，WaterSource 只负责"何时调用"。
    /// 当前状态以 ref 形式传入并由方法原地修改，使本类无需持有可变字段、
    /// 既保持无状态易测试，又能让 WaterSource 把 freezeState 直接存进快照。
    ///
    /// 合法转换：
    /// <list type="bullet">
    /// <item>Request：Idle/Requested → Requested（进入触发器；幂等）</item>
    /// <item>ConfirmFrozen：Requested → Frozen（结冰倒计时到点）</item>
    /// <item>Break：Frozen → Idle（冰被击碎，撤回结冰意图，需重新进入触发器）</item>
    /// </list>
    /// 其它转换（如 Idle → Frozen）视为非法，记录警告但不改变状态。
    /// </summary>
    public static class FreezeStateMachine
    {
        /// <summary>是否已处于冻结模式（Frozen 态）。新成熟单元将直接进入 Frozen 相位。</summary>
        public static bool IsFrozen(FreezeState state) => state == FreezeState.Frozen;

        /// <summary>
        /// 是否已有结冰意图（Requested 或 Frozen）。回溯后用此判断要不要自动重结冰。
        /// </summary>
        public static bool HasFreezeIntent(FreezeState state)
            => state == FreezeState.Requested || state == FreezeState.Frozen;

        /// <summary>是否处于"请求了但尚未结成冰"的中间态。回溯结束后会据此自动重启结冰倒计时。</summary>
        public static bool IsPendingFreeze(FreezeState state)
            => state == FreezeState.Requested;

        /// <summary>
        /// 进入触发器：记录结冰意图。Idle→Requested；已是 Requested/Frozen 则幂等忽略。
        /// </summary>
        public static void Request(ref FreezeState state)
        {
            if (state == FreezeState.Idle) state = FreezeState.Requested;
            // Requested 和 Frozen 保持不变（幂等）。
        }

        /// <summary>
        /// 结冰倒计时到点：Requested→Frozen。
        /// 非 Requested 态（如已 Frozen 或 Idle）调用此方法视为非法，记警告不改状态。
        /// </summary>
        public static void ConfirmFrozen(ref FreezeState state)
        {
            if (state == FreezeState.Requested) { state = FreezeState.Frozen; return; }
            Debug.LogWarning("[FreezeStateMachine] ConfirmFrozen 被调用但当前状态=" + state + "（非法转换，忽略）");
        }

        /// <summary>
        /// 冰被击碎：Frozen→Idle。撤回结冰意图，要求重新进入触发器才能再次结冰。
        /// 非 Frozen 态调用视为非法，记警告不改状态（防止 NotifyUnitBroken 在异常态被触发）。
        /// </summary>
        public static void Break(ref FreezeState state)
        {
            if (state == FreezeState.Frozen) { state = FreezeState.Idle; return; }
            Debug.LogWarning("[FreezeStateMachine] Break 被调用但当前状态=" + state + "（非法转换，忽略）");
        }
    }
}
