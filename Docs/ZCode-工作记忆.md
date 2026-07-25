# ZCode 工作记忆 —— GameJam 项目规范

> 本文件记录用户对 ZCode 在本项目中提出的所有持续性要求。**每次会话开始时应优先读取本文件并严格遵守。**
> 路径：`D:\Unity\Projects\GameJam2607\Docs\ZCode-工作记忆.md`
> 更新日期：2026-07-24

---

## 1. 沟通与文档语言

### 1.1 对话与文档使用中文
- **所有对话回复用中文。**
- **开发文档（.md 文件）用中文撰写。** 包括调研报告、使用说明、配置指南等。
- **代码注释用中文**（保持与现有代码库一致）。

### 1.2 UI 显示文本用英文
- 玩家可见的所有 UI 文字（TMP 文本、按钮 label、标题、提示语）**必须用英文**。
- **Inspector 里显示的文字也用英文**：`[Header("...")]`、`[Tooltip("...")]`、`[CreateAssetMenu(menuName="...")]`、自定义 EditorWindow 标题、菜单路径等。
- 涉及创建/修改 UI 文本时，默认英文，不要主动用中文。
- 例：`"已暂停" → "Paused"`，`"继续" → "Resume"`，`"返回主菜单" → "Return to Menu"`，`[Header("移动参数")] → [Header("Movement")]`。

> 区分原则：**代码注释和对话用中文；玩家可见文本 + Inspector 显示文本用英文。**

---

## 2. MCP 调用节约 token 规范

Unity MCP 每次调用都会返回大量结构化 JSON，容易快速消耗额度。以下规则必须遵守：

### 2.1 优先使用 `execute_code` 批量操作
- 多个独立操作（创建对象、设置字段、添加组件）合并到一个 `execute_code` 调用中完成。
- `execute_code` 的 `return` **只返回关键信息**（如 instanceID、成功/失败计数、关键字段值），不要返回完整对象序列化。
- 反例：`manage_gameobject create` 单次返回完整 Transform（forward/up/right/position/localPosition...约 500 tokens），但通常只需 instanceID。

### 2.2 合并独立调用为 `batch_execute`
- 多个独立的 `manage_components` / `manage_gameobject` 调用，用 `batch_execute` 一次性发出。
- 只在调用之间无依赖时合并。

### 2.3 列表/搜索必须分页
- 所有 `manage_asset search`、`manage_scene get_hierarchy`、`find_gameobjects` 必须加：
  - `page_size`（推荐 10-50，不要用默认）
  - `max_depth`（hierarchy 限制到 2-3 层）
- 避免一次返回整个项目结构。

### 2.4 避免在循环里调 MCP
- 需要循环操作时（如批量改名、批量设置字段），在 `execute_code` 内部用 C# 循环完成，一次返回结果。
- 反例：用 Bash for 循环里调用 `manage_components` —— 每次 MCP 调用都有开销。

### 2.5 反射访问 `execute_code` 沙箱限制
- codedom 编译器不支持 `using UnityEditor;` 和 `using UnityEngine.UI;`，用完整类型名（`UnityEditor.SceneAsset`、`UnityEngine.UI.Button`）。
- codedom 不能调用 `AssetDatabase.DeleteAsset` / `MoveAssetToTrash`（安全检查拦截）—— 用 `MoveAsset` 到 `.trash` 后缀代替。
- codedom 不支持 `EditorSceneManager`（命名空间找不到）—— 用反射调 `UnityEditor.EditorSceneManager.SaveScene`，或直接用 `manage_scene save`。

---

## 3. 场景操作规范

### 3.1 PersistentScene 是常驻基地
- **编辑器开发时**：Hierarchy 里 PersistentScene **必须保持加载**，让用户能完整看到 `Managers (SceneChanger/TimeManager/UIManager) + FadeCanvas + ESCCanvas + EventSystem`。
- **不要用 `OpenSceneMode.Single`** 替换掉 PersistentScene —— 除非是 Play 测试入口场景。

### 3.2 内容场景用 unload 状态
- 内容场景（StartingMenu / Level01 / 后续新增关卡）**以 unload 状态**保留在 Hierarchy 中。
- 实现方式：`EditorSceneManager.OpenScene(path, Additive)` 加载后，用 `EditorSceneManager.CloseScene(scene, false)` 关闭但保留（第二个参数 `false` = 不从 Hierarchy 移除）。
- 最终状态：`PersistentScene (isLoaded=true) + 其他场景 (isLoaded=false)`。

### 3.3 编辑内容场景的流程
1. 在 Hierarchy 里**双击** unload 状态的场景 → 它变为 loaded
2. 编辑
3. 保存
4. 右键 → Close → 回到 unload 状态
- 或 MCP 用 `OpenScene(Additive)` 加载 → 编辑 → `CloseScene(false)`。

### 3.4 Play 测试例外
- Play 测试时可以单独加载 `InitialScene`（Build Index 0），验证完整启动链（InitialScene → PersistentScene → StartingMenu）。
- 这是 `OpenSceneMode.Single` 的唯一允许使用场景。

### 3.5 UI 画布配置规范
- **所有 Canvas 必须配置为 1920×1080**：
  - `CanvasScaler.uiScaleMode = ScaleWithScreenSize`（枚举值 1）
  - `CanvasScaler.referenceResolution = (1920, 1080)`
  - `CanvasScaler.matchWidthOrHeight = 0`（当前选择，完全匹配宽度；如未来需要长宽比兼容性更好，可改 0.5）
- 新建 Canvas 时必须立即应用此配置。
- UI 子元素用 anchor（anchorMin/anchorMax）适配分辨率，不要硬编码绝对位置。

---

## 4. 禁止杜撰原则

### 4.1 改代码前先读
- 修改任何 `.cs` 文件前，必须先用 `Read` 工具完整读取该文件（不能只凭记忆/之前的对话）。
- 修改依赖的其他文件（如父类、接口、SO 类）也要先读。

### 4.2 写文档前先核实
- 写使用文档/调研报告时，所有具体陈述必须基于：
  - 实际读取的代码（引用具体文件:行号）
  - 通过 MCP `execute_code` 从编辑器读取的实际状态（字段值、配置、hierarchy）
- 不允许基于"训练数据"或"Unity 一般做法"猜测。

### 4.3 编辑器状态以实际为准
- 文档里描述的"当前配置"必须用 `execute_code` 实时核实（不要假设之前会话的状态没变）。
- 涉及 GUID/instanceID 引用时，即时查询，不用记忆里的值。

---

## 5. 行为边界

- 用户说 X 就做 X，不主动扩展到 Y。
- 重大决策（删文件、改架构、重写方法）先用 `AskUserQuestion` 或 `EnterPlanMode` 确认。

---

## 附录 —— 项目当前架构快照（仅供参考，实际状态以编辑器为准）

> 以下内容于 2026-07-24 通过 Unity MCP、代码和工作区文件核实。编辑器状态是开发快照，不代表最终运行时状态。

- **项目路径**：`D:\Unity\Projects\GameJam2607`
- **Unity 版本**：2022.3.62f3c1 LTS
- **目标平台**：StandaloneWindows64
- **项目类型**：横版解谜（GameJam）
- **渲染管线**：当前仍为 Built-in
  - `GraphicsSettings.currentRenderPipeline` 和默认管线资产均为 `null`。
  - `Packages/manifest.json` 已安装 `com.unity.render-pipelines.universal` 14.0.12，`com.unity.feature.2d` 为 2.0.1。
  - `Assets/URPAssets/New Universal Render Pipeline Asset.asset` 已存在，但尚未分配到 Graphics/Quality 设置。
  - URP 迁移仍是计划事项；迁移完成后需在此处记录实际使用的 URP Asset 路径。
- **代码与资源目录**：
  - 核心脚本：`Assets/Scripts/`
  - 预制件：`Assets/Prefabs/`
  - ScriptableObject：`Assets/SO/`
  - 美术资源主要位于：`Assets/ArtRes/`、`Assets/Sprites/`、`Assets/TextMesh Pro/`
- **Unity MCP**：CoplayDev `com.coplaydev.unity-mcp` v10.0.0
  - 全局配置：`~/.zcode/cli/config.json` → `mcp.servers.unity`（HTTP 127.0.0.1:8080/mcp）
  - 本地 Python server 源：`D:\MCP\unity-mcp-local\unity-mcp-10.0.0\Server`
- **远程仓库**：Gitee `https://gitee.com/KAIDOYONAGI/game-jam2607.git`，当前分支 `main`

### 场景与编辑器状态

- **Build Settings**：以下 7 个场景全部 enabled。

| Build Index | 场景 |
|---|---|
| 0 | `Assets/Scenes/InitialScene.unity` |
| 1 | `Assets/Scenes/PersistentScene.unity` |
| 2 | `Assets/Scenes/StartingMenu.unity` |
| 3 | `Assets/Scenes/LevelScenes/Level01Test.unity` |
| 4 | `Assets/Scenes/LevelScenes/Level02Test.unity` |
| 5 | `Assets/Scenes/LevelScenes/Level02Test 1.unity` |
| 6 | `Assets/Scenes/PersistentScene_1.unity` |

- **编辑器核实时状态**：
  - Play Mode：未运行；未暂停。
  - 当前活动场景：`PersistentScene`。
  - 实际 loaded：`PersistentScene`、`PersistentScene_1`、`Level01Test`。
  - 当前保留为 unload：`StartingMenu`、`Level02Test`、`Level02Test 1`。
  - 按场景规范，开发最终状态应为 `PersistentScene` loaded、其余内容场景 unload；当前额外加载了 `PersistentScene_1` 和 `Level01Test`，属于待整理的编辑器状态，本次仅记录，不自动改场景。
- **`PersistentScene` 当前层级摘要**：

```text
VirtualCamera       [CinemachineVirtualCamera + CinemachineConfiner2D + ConfinerFinder]
Player              [PlayerMovement + Rigidbody2D + CircleCollider2D]
├── WearablePoint   [WearableManager]
├── GroundCheck
├── ReincarnationManager
└── HealthManager
Managers
├── SceneChanger    [SceneChanger]
│   └── FadeCanvas  [Canvas + CanvasGroup + CanvasScaler + GraphicRaycaster]
├── TimeManager     [TimeManager]
├── UIManager       [UIManager]
└── EventSystem     [EventSystem + StandaloneInputModule]
Canvases
├── ESCCanvas       [ESCMenuManager]
└── GuideCanvas     [GuideCanvas]
IceBlock            [Ice + IceBlock + BoxCollider2D]
└── IntactVisual    [SpriteRenderer]
```

- **Canvas 配置**：当前核实到的 5 个 Canvas 均为 `ScreenSpaceOverlay`，`ScaleWithScreenSize`，参考分辨率 `(1920, 1080)`，`matchWidthOrHeight=0`：
  - `PersistentScene`：`FadeCanvas`、`ESCCanvas`、`GuideCanvas`
  - `PersistentScene_1`：`GameOverCanvas`、`HUDCanvas`

### 回溯系统与冰块内容

- **公共回溯基类**：`Assets/Scripts/Rewind/RewindableBase.cs` 提供时间窗口裁剪、倒放协程、冷却和冻结/解冻钩子。
- **玩家轮回**：`Assets/Scripts/Player/ReincarnationManager.cs:181-231` 在按住 `R` 时记录玩家位置与血量快照；当前可回溯窗口按衰老状态在 1 至 8 秒之间插值。
- **玩家输入**：`Assets/Scripts/Player/PlayerMovement.cs:14-87` 已增加 `startInputEvent` / `stopInputEvent`，通过事件开关输入并在停止时清除水平速度；`ReincarnationManager` 不再直接禁用 `PlayerMovement` 组件。
- **独立冰块机关**：
  - `Assets/Scripts/RewindableThings/IceBlock.cs:5-209` 继承 `RewindableBase`，在完整/碎裂之间切换视觉和碰撞，并仅记录状态变化快照。
  - `Assets/Prefabs/Ice/IceBlock.prefab` 已被 Unity 识别；当前 `PersistentScene` 中已有 `IceBlock` 实例，带 `Ice`、`IceBlock`、`BoxCollider2D` 和 `IntactVisual`。
- **本次核实的工作区状态（2026-07-24）**：除本工作记忆文件外，`git status` 未显示其他未提交变更；`IceBlock.cs` 与 `IceBlock.prefab` 已被 Git 跟踪。

### UI 与验证

- **面板系统**：`UIManager` 事件驱动；当前实际 Canvas 名称是 `ESCCanvas`、`GuideCanvas`、`GameOverCanvas`、`HUDCanvas` 和 `FadeCanvas`，不再以 `ESCPanel` 作为场景对象名记录。
- **ESCCanvas 按钮**（`Assets/Scenes/PersistentScene.unity`，2026-07-25 核实）：现有三个按钮，垂直顺序自上而下 `ResumeButton`(y=0) → `MenuButton`(y=-80) → `RetryButton`(y=-160)。三者 OnClick 都绑到 ESCCanvas 上的 `ESCMenuManager` 组件：
  - `OnResumeButton`：仅关闭 ESC 面板（`UIManager.RequestCanvasClose(ESC)`）。
  - `OnReturnToMenuButton`：关闭面板 + 用 `SceneChanger.loadEventSO` 加载 `initScene`（返回主菜单）。
  - `OnRetryButton`（新增）：关闭面板 + `TimeManager.ForceResumeGame()` + `retryEventSO.OnEventRaised()`；`ESCMenuManager.retryEventSO` 引用 `Assets/SOs/Events/RetryEvent.asset`（GUID `4088e814...`，与 GameOver 面板的 `ButtonSceneToggler.retryEventSO` 共用同一 SO），由每场景的 `RetryManager` 监听后执行 `HealthManager.ResetHealth()` + 重载当前场景。**行为完全对齐 GameOver 的 Retry**。
- **GameOverCanvas 死亡提示词**（`Assets/Scenes/PersistentScene_1.unity`，2026-07-25 核实）：`GameOverManager.OnGameOver(true)` 根据 `DeathManager.LastDeathType` 动态设置 `DeathText`(TMP_Text) 的文本：
  - `OldDead`（老死）→ `peacefulDeathMessage` = `"Rest in peace..."`（你死得很安详~）。
  - `UnnormalDead`（被打死/暴毙）→ `brutalDeathMessage` = `"Slaughtered without mercy!"`（你暴毙而亡！）。
  - 字段 `deathText` / `peacefulDeathMessage` / `brutalDeathMessage` 均在 Inspector 可配；原 `peacefulDeathText`(GameObject) 字段已废弃，改为始终显示 DeathText、仅切换文本。
- **GameOverCanvas RetryButton 样式**（2026-07-25）：已向 ESC 面板靠拢——`Image.sprite = null`、`sizeDelta = (300, 60)`、Label `fontSize = 36`，与 ESCCanvas 的三个按钮视觉一致。
- **`CanvasToToggle` 枚举**：`ESC / GameOver / Dialog / Default`，定义于 `Assets/Scripts/MyEnums.cs:56-66`；`Default` 仍表示无面板打开，不作为真实面板目标。
- **场景加载代码**：`InitialLoad` 以 Additive 方式加载配置的持久场景，`SceneChanger` 以 Additive 方式加载目标场景并在完成后触发 `sceneLoadedEvent`；依据 `Assets/Scripts/InitialLoad.cs:8-43`、`Assets/Scripts/Scene/SceneChanger.cs:197-252`。
- **验证时状态**：编辑器未编译、未运行 Play Mode；控制台未发现 C# error，仅有 Unity MCP WebSocket/批处理提示。`IceBlock.cs` 的脚本诊断为 0 errors、2 warnings，属于工具启发式提示，后续测试时仍需关注。
- **相关说明**：`Docs/场景加载与面板系统使用说明.md`。本附录的场景清单以本次 Unity MCP 实测为准。

---

## 修订记录

| 日期 | 修订内容 |
|---|---|
| 2026-07-18 | 初始版本，汇总所有用户提出的持续性规范 |
| 2026-07-23 | 附录标注渲染管线计划迁移到 URP，记录迁移尚未执行 |
| 2026-07-24 | 依据 Unity MCP 实时状态更新项目路径、渲染管线、Build Settings、编辑器场景状态、Canvas、回溯系统 |
| 2026-07-25 | ESCCanvas 新增 RetryButton（OnRetryButton），行为对齐 GameOver 面板的 Retry，复用 RetryEvent.asset |
| 2026-07-25 | GameOverCanvas 死亡提示词按 DeathType 动态切换（老死 "Rest in peace..." / 暴毙 "Slaughtered without mercy!"）；GameOver RetryButton 样式向 ESC 靠拢（无 sprite / 300×60 / 字号 36） |
