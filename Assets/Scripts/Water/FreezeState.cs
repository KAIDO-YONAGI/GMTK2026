namespace WaterMechanism
{
    /// <summary>
    /// 水源冻结状态机（水源级，独立于单元相位 <see cref="WaterFlowUnit.WaterPhase"/>）。
    ///
    /// 用枚举集中表达"结冰流程"的三态，替代原本的 isFrozen + freezeRequested 两个 bool，
    /// 让状态分支用 switch / 枚举比较表达，避免 bool 组合产生不合法的中间态（如"已结冰但未请求"）。
    ///
    /// 状态流转：
    /// <code>
    ///   Idle  ──踩 trigger──▶ Requested ──倒计时结束──▶ Frozen
    ///    ▲                                           │
    ///    └───────────────冰被击碎────────────────────┘
    /// </code>
    ///
    /// 与回溯的关系：
    /// freezeState 随快照保存/还原。回溯结束后若处于 Requested（请求了但还没结成冰），
    /// WaterSource 会自动重启结冰倒计时——即"回溯到结冰前会自动重新结冰"，无需玩家再踩 trigger。
    /// 只有 Idle 态不结冰。冰被击碎回到 Idle，要求重新踩 trigger 才能再次结冰。
    /// </summary>
    public enum FreezeState
    {
        /// <summary>未请求结冰。水正常流动（Stage1→Stage2），不结冰。</summary>
        Idle = 0,

        /// <summary>已踩 trigger 请求结冰，倒计时进行中。回溯到此态会自动重新结冰。</summary>
        Requested = 1,

        /// <summary>已结冰。后续新成熟单元直接进入 Frozen 相位。</summary>
        Frozen = 2
    }
}
