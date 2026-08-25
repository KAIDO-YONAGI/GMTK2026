# ZCode 工作记忆 —— GameJam 项目规范

> 本文件记录用户对 ZCode 在本项目中提出的所有持续性要求。**每次会话开始时应优先读取本文件并严格遵守。**
> 路径：`D:\Unity\Projects\GameJam2607\Docs\ZCode-工作记忆.md`
> 更新日期：2026-07-26

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
- **不要自行做 Play Mode 游戏流程测试**（2026-08-25）：玩法/流程验证一律交由用户本人执行；ZCode 只做编译诊断、控制台检查等只读操作，不得为测试在场景里创建临时对象（如临时地面）或激活/修改场景对象。

---

## 附录 —— 项目当前架构快照（仅供参考，实际状态以编辑器为准）

> 以下内容于 2026-07-24 通过 Unity MCP、代码和工作区文件核实。编辑器状态是开发快照，不代表最终运行时状态。

- **项目路径**：`D:\Unity\Projects\GameJam2607`
- **Unity 版本**：2022.3.62f3c1 LTS
- **目标平台**：StandaloneWindows64
- **项目类型**：横版解谜（GameJam）
- **渲染管线**：已迁移到 **URP**（2026-07-26 核实）
  - `ProjectSettings/GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 指向 `Assets/URPAssets/New Universal Render Pipeline Asset.asset`（GUID `70dcfc058211a7045b83134a5d1c3e5d`）。
  - `Packages/manifest.json` 安装 `com.unity.render-pipelines.universal` 14.0.12，`com.unity.feature.2d` 为 2.0.1。
  - `PersistentScene` Hierarchy 中新增 `Global Volume Manager` 对象，挂 `Volume + VolumeManager` 组件，用于全局后处理。
- **代码与资源目录**（2026-08-15 功能域重组后，详见附录"目录功能域重组"小节）：
  - 核心脚本：`Assets/Scripts/`（Core / Managers / Player / Rewind / Characters / Pathfinding / Gameplay / Systems / UI / SO 十个功能域）
  - 自研框架：`Assets/FrameWork/`（YSingleton、UIManager、SceneChanger、TimeManager、通用事件 SO 等，2026-08 上旬由 Scripts 迁入）
  - 预制件：`Assets/Prefabs/`；ScriptableObject 资产：`Assets/SOs/`；材质：`Assets/Materials/`（原名 Metarials，已改正拼写）
  - 美术资源主要位于：`Assets/Sprites/`、`Assets/Animations/`、`Assets/AudioRes/`、`Assets/TextMesh Pro/`
- **Unity MCP**：CoplayDev `com.coplaydev.unity-mcp` v10.0.0
  - 全局配置：`~/.zcode/cli/config.json` → `mcp.servers.unity`（HTTP 127.0.0.1:8080/mcp）
  - 本地 Python server 源：`D:\MCP\unity-mcp-local\unity-mcp-10.0.0\Server`
- **远程仓库**：Gitee `https://gitee.com/KAIDOYONAGI/game-jam2607.git`，当前分支 `main`

### 场景与编辑器状态

- **Build Settings**：共 10 条，其中 9 个 enabled，1 个 disabled（2026-07-26 通过 `EditorBuildSettings.scenes` 核实）。

| enabled | 场景 |
|---|---|
| ON | `Assets/Scenes/InitialScene.unity` |
| ON | `Assets/Scenes/PersistentScene.unity` |
| ON | `Assets/Scenes/StartingMenu.unity` |
| ON | `Assets/Scenes/LevelScenes/Level01Test.unity` |
| ON | `Assets/Scenes/LevelScenes/Level02Test.unity` |
| off | `Assets/Scenes/LevelScenes/Level02Test 1.unity`（已从磁盘删除，Build Settings 残留条目；待清理） |
| ON | `Assets/Scenes/PersistentScene_1.unity` |
| ON | `Assets/Scenes/LevelScenes/TeachingLevel01.unity`（教学关卡，新增） |
| ON | `Assets/Scenes/LevelScenes/NormalLevel01.unity`（正式关卡 1，新增） |
| ON | `Assets/Scenes/LevelScenes/NormalLevel02.unity`（正式关卡 2，新增） |

> 注：`Level01Test` / `Level02Test` 与 `NormalLevel0X` 是**并存**关系，Normal 系列是平行新增的正式关卡，不是 Test 的改名。

- **编辑器核实时状态**（2026-07-26）：
  - Play Mode：未运行；未暂停。
  - 当前活动场景：`PersistentScene`。
  - 实际 loaded：`PersistentScene`、`PersistentScene_1`。
  - 当前 unload：`NormalLevel01`、`NormalLevel02`（在 Hierarchy 但未加载）。
  - 注意：早期记录里出现的 `IceBlock`（`Ice + IceBlock`）等对象已不在当前 loaded 场景的根级摘要中——这些关卡机关现置于各 Level 场景内，而非 PersistentScene。
- **`PersistentScene` 当前层级摘要**（仅根级对象 + 直接组件，2026-07-26 核实）：

```text
VirtualCamera       [CinemachineVirtualCamera + CinemachineConfiner2D + ConfinerFinder + CameraShake]
Player              [SpriteRenderer + Rigidbody2D + Animator + UnityAnimatorPlayer + CapsuleCollider2D]
Managers            [空根容器，子对象见下]
Canvases            [空根容器 ×2]
AudioManager        [AudioManager]            ← 新增，挂 PersistentScene 根
Global Volume Manager [Volume + VolumeManager] ← URP 全局后处理
```

> 与 2026-07-24 快照相比：① `VirtualCamera` 新增 `CameraShake`；② `Player` 现挂 `Animator + UnityAnimatorPlayer + CapsuleCollider2D`（原记录为 `CircleCollider2D`，已改）；③ 新增 `AudioManager` 和 `Global Volume Manager`；④ 原 `IceBlock` / `WearablePoint` / `GroundCheck` / `ReincarnationManager` / `HealthManager` 等子对象因当前为根级摘要未展开，**修改前请用 MCP 实时核实**而非依赖此快照。

- **Canvas 配置**：当前核实到的 5 个 Canvas 均为 `ScreenSpaceOverlay`，`ScaleWithScreenSize`，参考分辨率 `(1920, 1080)`，`matchWidthOrHeight=0`：
  - `PersistentScene`：`FadeCanvas`、`ESCCanvas`、`GuideCanvas`
  - `PersistentScene_1`：`GameOverCanvas`、`HUDCanvas`

### 回溯系统与冰块内容

- **公共回溯基类**：`Assets/Scripts/Systems/Rewind/RewindableBase.cs` 提供时间窗口裁剪、倒放协程、冷却和冻结/解冻钩子。
- **玩家轮回**：`Assets/Scripts/Characters/Player/ReincarnationManager.cs:181-231` 在按住 `R` 时记录玩家位置与血量快照；当前可回溯窗口按衰老状态在 1 至 8 秒之间插值。
- **玩家输入**：`Assets/Scripts/Characters/Player/PlayerMovement.cs:14-87` 已增加 `startInputEvent` / `stopInputEvent`，通过事件开关输入并在停止时清除水平速度；`ReincarnationManager` 不再直接禁用 `PlayerMovement` 组件。
- **独立冰块机关**：
  - `Assets/Scripts/Gameplay/Ice/IceBlock.cs:5-209` 继承 `RewindableBase`，在完整/碎裂之间切换视觉和碰撞，并仅记录状态变化快照。
  - `Assets/Prefabs/Ice/IceBlock.prefab` 已被 Unity 识别；当前 `PersistentScene` 中已有 `IceBlock` 实例，带 `Ice`、`IceBlock`、`BoxCollider2D` 和 `IntactVisual`。
- **本次核实的工作区状态（2026-07-24）**：除本工作记忆文件外，`git status` 未显示其他未提交变更；`IceBlock.cs` 与 `IceBlock.prefab` 已被 Git 跟踪。

### 音效系统（2026-07-26 新增记录）

- **核心三件套**（脚本在 `2aa1e85` 提交引入，2026-07-25；音效 mp3 与配置在 `d10f4c6`、调用点在 `d1e3860`，2026-07-26）：
  - `Assets/Scripts/Systems/Audio/AudioConfigSO.cs:25` —— `public class AudioConfigSO : ScriptableObject`，菜单 `Audio/AudioConfig`，含 `SFXEntry[] sfxEntries` 和 `BGMEntry[] bgmEntries`，按权重随机 (`PickWeighted`)。
  - `Assets/Scripts/Systems/Audio/AudioManager.cs:7` —— 单例 `public static AudioManager Instance`，`Awake` 里 `DontDestroyOnLoad`，挂在 **`PersistentScene` 根对象 `AudioManager`** 上（已核实）。
  - `Assets/Scripts/Systems/Audio/AudioPlayer.cs:3-46` —— 带 `AudioSource` 的池元素，播完自动 `ReturnPlayer`。
- **播放机制**：`Queue<AudioPlayer> _pool`，预热 `warmupCount=4`、上限 `maxPoolSize=16`。入口 `AudioManager.PlaySFX(MyEnums.SFXType)` / `PlayBGM(MyEnums.BGMType)`，启动时自动播 `startupBGM=Gaming`。
- **SFX 类型枚举**（`Assets/Scripts/Core/Definitions/MyEnums.cs:80-97`，共 16 项）：`Walk / Run / Jump / Land / Hurt / IceBroken / IceFreeze / SpikeLaunch / PickUp / LoadScene / PressurePlate / Dead / SpringBounce / SpeedUp / ...`。当前 `AudioConfig.asset` 已配置实际 mp3 clip 的是前 11 类（Walk/Run/Jump/Land/Hurt/IceBroken/IceFreeze/SpikeLaunch/PickUp/LoadScene/PressurePlate）；`Dead/SpringBounce/SpeedUp` 枚举存在但 asset 中暂未配 clip。
- **音效 mp3 资源**：`Assets/AudioRes/SFX/`（走路 01-04 / 跑步 01-04 / 跳跃 / 落地 / 受击 01-02 / 冰冻结 / 冰碎裂 / 弓发射 / 捡起物品 / 加载音效）。
- **主要调用点**：脚步声在 `PlayerAnimationController.cs:237-261`（按 walk/run 间隔），跳跃起跳在 `:205`，落地在 `:279`，受击在 `HealthManager.cs:142`，冰块在 `IceBlock.cs:106/237`，发射器在 `SpikeLauncher.cs:111`，捡起在 `PickupSpike.cs:28`，加载场景在 `SceneChanger.cs:157`，压力板在 `PressurePlate.cs:55`。

### 粒子预制件（2026-07-26 新增记录）

`Assets/Prefabs/Particles/` 下当前 5 个（`LuoDi / IceParticle / ShootingParticle / HurtParticle / RunParticle`）：

| Prefab | 触发脚本 | 行号 | 用途 |
|---|---|---|---|
| `LuoDi.prefab` | `PlayerMovement` `landParticlePool`（对象池，prewarm 3） | `PlayerMovement.cs:20-23,291-302` | 落地粒子 |
| `IceParticle.prefab` | `IceBlock.iceParticlePrefab`，`PlayIceParticle()` 直接 Instantiate | `IceBlock.cs:26,120-131` | 冻结/碎裂时冰屑 |
| `ShootingParticle.prefab` | `SpikeLauncher.shootingParticlePrefab`，发射时 Instantiate 到 `particlePoint` | `SpikeLauncher.cs:17,114-125` | 尖刺发射烟尘 |
| `HurtParticle.prefab` | `HealthManager.hurtParticlePool`（对象池） | `HealthManager.cs:26,78-88` | 受击 |
| `RunParticle.prefab` | `PlayerMovement.runParticleSystem`（直接挂玩家，非池） | `PlayerMovement.cs:17,271-280` | 地面奔跑拖尾 |

### 濒死提示面板（2026-07-26 新增记录）

- **`Assets/Scripts/UI/DyingHintManager.cs:1-92`**：血量到 0 但未真正死亡时显示的"自救"面板控制器。
  - 订阅 `healthChangedEvent`（IntEventSO）和 `deathEvent`（DeathEventSO_）`DyingHintManager.cs:36-52`。
  - 血量归零显示 CanvasGroup，回血或死亡时隐藏 `DyingHintManager.cs:54-67`。
  - **Give Up 按钮**在 `Awake` 里代码绑定 `DyingHintManager.cs:31-33`，点击调用 `HealthManager.Instance.OnDie()` 放弃自救 `DyingHintManager.cs:75-82`。

### 玩家动画与"loaded 状态"修复（2026-07-26 新增记录）

- **PlayerAnimationController**（`Assets/Scripts/Characters/Player/PlayerAnimationController.cs`）：**不使用 Animator 参数**，而是通过 `UnityAnimatorPlayer` 直接播放 `AnimationClip`。
  - `PlayClipOrStay(state)` (`:268-281`) 查表拿 clip + `AnimationPlayParameters` 后 `animatorPlayer.Play(...)`；clip/params 来自 `Assets/SOs/PlayerAnimationConfig.asset`（`PlayerAnimationConfigSO`）。
  - 状态枚举含 `Idle/Walk/Run/Jump/Fall/Land/None`，本次 `ca879f8` 新增 `None` 状态 (`:28`)。
  - 落地检测：`!wasGrounded && isGrounded` → `PlayClipOrStay(PlayerAnimState.Land)` `:157-161`。
- **"loaded 状态"修复**（`28b5f21` + `ca879f8`）：这里的 "loaded" 指场景加载完成回调 `OnSceneLoaded()`（由 `VoidEventSO sceneLoadedEvent` 触发），**不是某个枚举字段**。修复点：
  - `PlayerMovement.OnSceneLoaded()` (`PlayerMovement.cs:192-228`)：补上 `wasGrounded=true`、停 `runParticleSystem`、清 `speedUpFeedbackGroup` 透明度、回收所有活跃落地粒子。这是修复 **弹簧死亡 retry 后会残留死亡/加速 buff/UI 反馈/粒子** 的 bug（`2c7acfa`）。
  - `PlayerAnimationController.OnSceneLoaded()` (`:71-77`)：重置动画瞬态、强制 `wasGrounded=false` 再播 Idle，避免切场景后落地检测残留上一场景状态。
- **死亡时禁止移动**（`044fb85`）：在 `PlayerMovement` 三个入口加同一道闸 `if (HealthManager.Instance.IsDead && CurrentHealth <= 0f)`：`UpdateMovementState()` `:249-255`、`HandleJumpInput()` `:315-321`、`ApplyMovementVelocity()` `:340-346`（清零速度并 return）。另有一道独立的 `isInputEnabled` 开关用于回溯期停输入，与死亡无关。
- **注**：`a44ffb3`「尝试修死后按 AD 反转角色失败」未成功，问题仍未解决，后续修改 PlayerMovement 朝向逻辑时需关注。

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
- **`CanvasToToggle` 枚举**：`ESC / GameOver / Dialog / Default`，定义于 `Assets/Scripts/Core/Definitions/MyEnums.cs:56-66`；`Default` 仍表示无面板打开，不作为真实面板目标。
- **场景加载代码**：`InitialLoad` 以 Additive 方式加载配置的持久场景，`SceneChanger` 以 Additive 方式加载目标场景并在完成后触发 `sceneLoadedEvent`；依据 `Assets/Scripts/Systems/Scene/InitialLoad.cs:8-43`、`Assets/Scripts/Systems/Scene/SceneChanger.cs:197-252`。
- **验证时状态**：编辑器未编译、未运行 Play Mode；控制台未发现 C# error，仅有 Unity MCP WebSocket/批处理提示。`IceBlock.cs` 的脚本诊断为 0 errors、2 warnings，属于工具启发式提示，后续测试时仍需关注。
- **相关说明**：`Docs/场景加载与面板系统使用说明.md`。本附录的场景清单以本次 Unity MCP 实测为准。

### 目录功能域重组（2026-08-15 执行）

> 依据按功能逐项审查（92 个 .cs 全部审读）的区分度评估结论执行 B 档重组。全程 `AssetDatabase.MoveAsset`（36 个 .cs + Metarials→Materials 目录改名 + Files→Docs 收纳），移动前后 meta GUID 逐一比对 **36/36 一致**，场景/Prefab/SO 引用零断裂；重组后编译 0 error。提交：回滚点 `e89d8aa` → 重组 `039a015`（87 项 R100 纯 rename，代码内容零改动）。

**重组要点**：
- **Rewind 三处合一** → `Scripts/Rewind/`：RewindableBase + 2 接口（←Systems/Rewind/）、ReincarnationManager / PlayerRewindTarget / PlayerRewindHistory（←Player/）、Ghosts/ 四件（PlayerGhost + 原 Player/Rewind/Ghosts/ 三件）。
- **Manager 统一口径** → `Scripts/Managers/`：+HealthManager（←Player/，被 7 文件引用的全局单例）、+GoalTrigger（←Gameplay/Triggers/，YSingleton 通关流程，类名未改）。
- **Player/ 只留本体**：PlayerMovement、PlayerAnimationController、PlayerAnimationConfigSO、WearableManager。
- **UI 四分**：CanvasManagers/（+DyingHintManager、StartingMenuController ←根级，口径=ICanvasManager 面板）、Dialog/（原 DialogScripts，History/ 原名 HistoryManager/）、HUD/（HealthHeartUI、RewindTimeBar）、Widgets/（Joystick、UIDrag、UIFlicker、ScrollbarFix + SpikeLauncherRingInput ←Gameplay/Spikes/）。
- **SO 按域三分**：Events/（+DeathEventSO_ ←根级）、Dialog/（CharacterSO、DialogSO、RefuseDialogSO、ItemSO、GuidSO）、Scene/（DifficultySettingsSO、SceneArtAssetsSO）。
- **单文件/名不副实目录撤销**：Platforms/（仅 1 个编辑辅助脚本，PlatformConfigurator 提到 Gameplay/ 根）、Systems/Scene/（SceneArtAssetsTrigger 归 Systems/Audio/，场景切换逻辑实际在 FrameWork）。
- **其他**：MyEnums.cs 归 `Scripts/Core/`；删空目录 9 个（UI/Buttons、UI/Menu、Systems/Rewind、Systems/Scene、Gameplay/Platforms、UI/DialogScripts 树、Player/Rewind 树）；`Assets/Files/GJBrainStorm_1.md → Docs/`。
- **不动**：FrameWork/ 整体、Characters/、Pathfinding/（确认为通用算法保留顶层）、Gameplay 六个机关子目录、Systems 其余四个子目录、Prefabs/SOs/Sprites 等资产目录内部。

**本文档历史小节的旧路径映射**：
| 旧路径（上文引用） | 现路径 |
|---|---|
| `Scripts/Characters/Player/PlayerMovement.cs` 等 | `Scripts/Player/…` |
| `Scripts/Characters/Player/ReincarnationManager.cs` | `Scripts/Rewind/ReincarnationManager.cs` |
| `Scripts/Systems/Rewind/RewindableBase.cs` | `Scripts/Rewind/RewindableBase.cs` |
| `Scripts/Core/Definitions/MyEnums.cs` | `Scripts/Core/MyEnums.cs` |
| `Scripts/UI/DyingHintManager.cs` | `Scripts/UI/CanvasManagers/DyingHintManager.cs` |
| `Scripts/Systems/Scene/InitialLoad.cs`、`SceneChanger.cs` | `FrameWork/Scripts/Scene/…`（FrameWork 重构迁入，非本次） |
| `Scripts/UI/CanvasManagers/ESCMenuManager.cs`、`UIManager` | `FrameWork/Scripts/UI/…`（同上） |

**死代码/零资产清单（2026-08-15 审查确认，本次只移不删，留待单独清理）**：
- 半成品/断线：EnemyCombat（攻击逻辑全注释）、EnemyHealth（OnDefeated 事件无订阅者）
- 空占位/孤儿类：VisitedHistoryManager（空类）、InventorySlotsStatsSO（无资产无引用）
- 无消费者：Joystick（移动端预留）
- MyEnums 死枚举（0 引用）：PlayerState、PersistentType、QuestState、SaveType、SaveLoadPanelType
- 零资产实例 SO 类：DialogSO、CharacterSO、RefuseDialogSO、ItemSO、GuidSO、InventorySlotsStatsSO（对话/物品系统只写了代码未配数据）
- 重复实现（代码层问题，非目录）：AudioManager 自建 AudioPlayer 池未复用 Systems/Pooling/ObjectPool；WaterFlowTrigger 与 WaterFreezeTrigger 代码 95% 重复

---

## GJ 复盘归因（2026-07-27，供后续项目复用）

> 本节由项目负责人在 GJ 结束后复盘归纳，作为后续项目的教训记录。详细分析见 `C:\Users\12248\Desktop\GameJam2607-评估报告.md` 第九章。

### 归因一：单点过载 + 沟通乏力（流程层）

- **现象**：本次 GJ 负责人同时承担**核心概念构思、需求分发与跟进、关卡设计、主程之一**四重角色；git 数据佐证——GJ 期负责人产出 3928 行代码 + 99 次提交（另一位程序 smdj 2573 行），同时承担全局职责。
- **根因**：全局上下文只存在于一个人脑中 → 无人能对等质疑 → 需求跟进深度不足 → **集成验证环节缺位**。曾提出的"环形讨论"机制试图解决（把全局理解扩散到至少第二个人，使需求能被质疑、集成能被交叉验证），但本次未落地。
- **后果**：3 号玩家反馈 "pressing down the button did nothing"——`PressurePlate.cs:21` 的 `onActivate` 是 `UnityEvent`，触发目标在场景 Inspector 绑定。这类"场景接线"最依赖"亲自跑一遍验证"，恰是单点过载最易挤掉的环节。**这不是个人能力问题，是流程/产能结构问题**——换成任何人在此角色配置下都会漏。
- **后续对策**：落地"环形讨论"机制；集成验证环节从主程身上剥离（见评估报告 9.3.5）。

### 归因二：解谜引导盲区（设计层）

- **现象**：4 条玩家评价中 3 条反映"难理解"（mindbending / hard to wrap head around / took awhile to get the gist）。
- **根因（自述）**：解谜游戏玩得少，不知道解谜该有什么样的引导，默认了"解谜就是赤石的"。
- **外部资料查证结论**（详见评估报告 9.4，来源：GameDesignSkills / Machinations / Rachel Moeller / Level Design Book）：
  1. 解谜应"让玩家始终看见终点"（想"怎么解"而非"解什么"）。
  2. 早期关卡保证"唯一解"做隐形教学。
  3. 机关需"视觉示能"——外观/idle 动画让玩家一眼看出可交互（本项目压力板缺"踩我"的视觉信号，玩家③才误当成按钮）。
  4. 示能只解决"能不能动"，**不要把"解法"也指示出来**，否则变成按键流程而非解谜。
- **后续对策**：补机关可交互示能；首关做唯一解教学；设定视觉层（衰败程度/齿轮状态）承担引导职能。

---

## 修订记录

| 日期 | 修订内容 |
|---|---|
| 2026-07-18 | 初始版本，汇总所有用户提出的持续性规范 |
| 2026-07-23 | 附录标注渲染管线计划迁移到 URP，记录迁移尚未执行 |
| 2026-07-24 | 依据 Unity MCP 实时状态更新项目路径、渲染管线、Build Settings、编辑器场景状态、Canvas、回溯系统 |
| 2026-07-25 | ESCCanvas 新增 RetryButton（OnRetryButton），行为对齐 GameOver 面板的 Retry，复用 RetryEvent.asset |
| 2026-07-25 | GameOverCanvas 死亡提示词按 DeathType 动态切换（老死 "Rest in peace..." / 暴毙 "Slaughtered without mercy!"）；GameOver RetryButton 样式向 ESC 靠拢（无 sprite / 300×60 / 字号 36） |
| 2026-07-26 | 附录核实更新：**渲染管线已迁移到 URP**（`Assets/URPAssets/New Universal Render Pipeline Asset.asset` 已分配给 GraphicsSettings）；Build Settings 从 7 场景扩到 10 场景，新增 `TeachingLevel01` / `NormalLevel01` / `NormalLevel02`，`Level02Test 1` 已禁用且文件已删（Build Settings 残留待清理）；`PersistentScene` 新增 `AudioManager`（根对象）和 `Global Volume Manager`（URP 全局后处理），`Player` 改用 `CapsuleCollider2D + Animator + UnityAnimatorPlayer`，`VirtualCamera` 新增 `CameraShake` |
| 2026-07-26 | 新增"音效系统"小节：`AudioConfigSO` / `AudioManager`（单例+池）/ `AudioPlayer` 三件套，16 个 SFXType 枚举与 mp3 资源；新增"粒子预制件"小节：`LuoDi`/`IceParticle`/`ShootingParticle`/`HurtParticle`/`RunParticle` 五个 prefab 及触发脚本；新增"濒死提示面板"小节：`DyingHintManager`（血量归零自救面板 + Give Up 按钮） |
| 2026-07-26 | 新增"玩家动画与 loaded 状态修复"小节：`PlayerAnimationController` 改用 `UnityAnimatorPlayer` 直播 AnimationClip（非 Animator 参数）；`OnSceneLoaded` 回调修复弹簧死亡 retry 后状态残留（`PlayerMovement.cs:192-228`）；死亡禁止移动在 `PlayerMovement` 三处入口加闸（`:249-255/315-321/340-346`）；标注死后 AD 反转 bug 未解决（`a44ffb3` 失败） |
| 2026-07-27 | 新增"GJ 复盘归因"小节：归因一（单点过载+沟通乏力，流程层，附 git 数据佐证与"环形讨论"对策）；归因二（解谜引导盲区，设计层，附 4 份外部资料查证结论与对策）。详见桌面评估报告第九章 |
| 2026-08-15 | 执行目录功能域重组（B 档）：合并 Rewind 三处、统一 Managers 口径、UI 细分四子目录、SO 按域三分、Metarials→Materials、Files→Docs、删 9 个空目录；36 个移动脚本 GUID 前后一致零断链、编译 0 error（提交 e89d8aa→039a015）；新增旧路径映射表与死代码/零资产清单备查 |
