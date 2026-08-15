# My_SceneAndUIFrameWork

基于 Unity `SceneManager` 的多场景 Additive 加载与 UI 画布管理框架。运行时不依赖 Addressables、玩法脚本或音频系统，使用旧输入系统和 UGUI。

## 包含内容

```text
FrameWork/
  Scripts/Core/       单例基类与通用枚举
  Scripts/SO/         场景数据和事件通道
  Scripts/Scene/      初始加载、常驻保护、场景切换、Retry、Teleport、时间管理
  Scripts/UI/         UIManager、焦点栈、面板接口及通用按钮/面板控制器
  Samples/Scenes/     已完成基本接线的 InitialScene、PersistentScene、LevelScene，以及独立 MenuScene
  Samples/Prefabs/    可直接放进关卡的 Teleport
  Samples/SOAssets/   Sample 使用的场景数据和事件资产
  Samples/Scripts/    不依赖玩法代码的 Sample 面板与玩家控制器
```

导入 `.unitypackage` 不会自动修改 Build Settings。

## 运行 Sample

1. 在 `File > Build Settings` 中手动加入：
   - `Assets/FrameWork/Samples/Scenes/InitialScene.unity`
   - `Assets/FrameWork/Samples/Scenes/PersistentScene.unity`
   - `Assets/FrameWork/Samples/Scenes/LevelScene.unity`
   - 测试菜单示例时，再加入 `Assets/FrameWork/Samples/Scenes/MenuScene.unity`
2. 主示例的场景顺序固定为 `InitialScene -> PersistentScene -> LevelScene`。`MenuScene` 是额外的 Additive 内容场景，不替换或改动这条默认启动链。
3. 打开并运行 `InitialScene`。`InitialLoad` 会以 Additive 模式加载 `PersistentScene`，随后其中的 `SceneChanger` 加载 `LevelScene`。
4. 使用方向键或 `A/D` 移动 Sample Player，进入右侧 Teleport 后会整组重载目标关卡，并回到 `(-3, 0, 0)`。
5. 测试 `MenuScene` 时，应先保持 `PersistentScene` 已加载，再以 Additive 模式加载 `MenuScene`。其中的 `StartButton` 使用 `ButtonSceneToggler` 切换到 `LevelScene`。

Sample 还展示了以下 UI 行为：

| 输入/按钮 | 行为 |
|---|---|
| `ESC` | 打开或关闭 Pause Sample |
| `G` | 打开 Guide Sample；Guide 不允许由 ESC 直接关闭，但会被互斥的 Pause 面板关闭 |
| `O` | 打开 Blocked Input Sample；打开期间吞掉 ESC 和其他全局面板切换输入，只能使用面板内的 Close 按钮 |
| `Reload Level` | 使用 `ButtonSceneToggler` 执行普通场景组切换 |
| `Retry` | 使用 `RetryButton` 广播重试事件，由中央 `RetryManager` 查表并重载 |

`LevelScene` 内只有 Teleport 关卡配置；`MenuScene` 只展示菜单切场按钮，不持有常驻管理器。

## 关卡场景配置

当前框架要求放进各关卡场景的配置只有 **Teleport**。玩家、Camera、Light、`SceneChanger`、`RetryManager` 和全局 UI 应由 `PersistentScene` 统一持有，不要复制进关卡场景。

### 1. 创建场景数据

为每个需要加载的场景创建一个 `GameSceneSO`：

- `Scene Asset`：拖入对应 `.unity` 场景。
- `Scene Type`：普通关卡选择 `Location`，菜单选择 `Menu`。
- `Initial Position`：没有明确传送坐标时使用的默认出生点。

`GameSceneSO` 不会自动修改 Build Settings。场景仍需由集成人员手动加入构建列表。

### 2. 配置 Teleport

可以直接把 `Assets/FrameWork/Samples/Prefabs/Teleport.prefab` 放进关卡，也可以自行创建：

1. 创建带 `Collider2D` 的触发区域并勾选 `Is Trigger`。
2. 添加 `SceneToggler` 组件。脚本文件名是 `Teleport.cs`，组件类名是 `SceneToggler`。
3. `Load Event SO` 使用与常驻 `SceneChanger` 相同的 `SceneLoadEventSO`。
4. `Scene To Load` 按确定顺序填写目标的**完整 Additive 场景组**，不要加入任何常驻场景。
5. `New Position` 填切换后的玩家位置；保持零向量时，使用列表首个 `GameSceneSO.Initial Position`。
6. `Is To Fade` 决定是否使用 `SceneChanger` 中配置的过渡效果。

触发对象必须带 `Player` Tag。二维触发检测还要求双方具有合适的 `Collider2D`，并且至少一方带 `Rigidbody2D`。

### 3. 配置中央场景链路

- `InitialScene` 的 `Bootstrapper/InitialLoad.Persistent Scenes` 配置为 `PersistentSceneSO`。
- `PersistentScene` 持有玩家、全局管理器和 UI；其中 `SceneChanger.First Scene To Load` 配置为初始关卡场景组，例如 `LevelSceneSO`。
- 常驻 `SceneChanger` 的 `Load Event SO` 必须与 Teleport、普通切场按钮和 `RetryManager` 使用同一资产。
- `First Scene To Load` 是启动时按顺序加载的初始内容场景组。
- `Scene Loaded Event` 在整组加载完成后广播；玩法侧的回血、复位和声音等逻辑应订阅这个事件，各自处理业务状态。
- `InitialLoad.Persistent Scenes` 用于注册并 Additive 加载必须常驻的场景。注册后，`SceneChanger` 和 `RetryManager` 会阻止它们进入卸载或重试列表。

### 4. 配置 Retry

整个运行链路只保留一个常驻 `RetryManager`。每条 `RetryConfig` 分别配置：

- `Match Scene`：用于识别当前关卡的配置键。
- `Scenes To Reload`：按顺序填写需要完整重载的 Additive 场景组。
- `Spawn Position`：重试出生点；为零向量时回退到 `Match Scene.Initial Position`。

Retry 按钮只挂 `RetryButton.HandleRetry`。普通切场按钮使用 `ButtonSceneToggler`，两者不共用分支。

## Additive 与协作开发

这里使用 Additive 的首要目的，是把场景拆成清晰的协作所有权边界，而不是单纯追求运行性能。

建议按以下方式开发：

1. `InitialScene` 只负责加载常驻场景；`PersistentScene` 由集成人员维护，集中放置玩家、全局管理器、Camera、Light 和跨关卡 UI。
2. 每个关卡使用独立 `.unity` 场景，并明确负责人。需要进一步拆分美术、玩法或灯光场景时，也必须先约定各自所有权。
3. 不要让多人同时修改同一个 `.unity` 文件。Unity 场景 YAML 的合并结果很难可靠验证，远程修改或其他本地修改很容易在保存、拉取或冲突处理时失效。
4. 开发者只保存自己负责的场景，避免习惯性使用 `Save All`。集成人员通过 `GameSceneSO` 的场景组把各场景组合起来。
5. 共享对象优先做成有明确负责人的 Prefab；不要把同一套玩家、管理器或 UI 分别复制到多个关卡。
6. 完整场景组必须以固定顺序写进 `Scene To Load` 或 `Scenes To Reload`，确保普通切场与 Retry 得到相同组合。
7. Build Settings 由集成人员集中维护。资产导入和 `GameSceneSO.OnValidate` 都不应自动改写构建列表。

这样的拆分能让关卡开发真正分场景进行，减少同一场景反复保存造成的覆盖，也能避免远程或本地已有修改在协作中无声失效。

## UI 面板配置

实现 `ICanvasManager` 的面板需要把自己的 `ToggleCanvasEventSO` 同时加入 `UIManager.Toggle Canvas Events`。

- `Close On Escape`：仅决定该面板位于顶层时，ESC 能否关闭它。
- `Blocks Global Input`：面板打开时吞掉 ESC、快捷键和 `RequestCanvasToggle` 请求。
- `RequestCanvasClose`：显式关闭入口，不受上述两个选项限制，可用于按钮、互斥关闭和场景复位。
- `Mutex Canvases`：列入其中的已打开面板会在另一个互斥面板打开时关闭。

面板的排序由 `CanvasFocusStack` 按打开顺序维护，不需要手动竞争 `sortingOrder`。
