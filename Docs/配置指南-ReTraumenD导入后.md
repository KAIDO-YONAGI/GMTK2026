# ReTraumenD + SMDJ 导入后的配置指南

> **目标**: 让导入的 ReTraumenD 代码包 + SMDJ 美术包在 GameJam 项目里正常编译运行
> **当前状态**: 资产已导入，但项目设置缺失，编译会报错

---

## 配置前的体检结果

| 检查项 | 当前状态 | 需要操作 |
|---|---|---|
| Unity 版本 | 2022.3.62f3c1 LTS | ✅ 已对齐源项目，无需改 |
| 渲染管线 | Built-in | ✅ 与代码兼容 |
| **Newtonsoft.Json 包** | ❌ **未装** | ⚠️ 代码未引用，但建议装（存档会用到） |
| **Addressables 包** | ✅ 已装 1.22.3 | 需初始化配置（见步骤 4） |
| **TextMesh Pro** | ✅ 已装 3.0.7 | 代码用 `TMP_Text`，已 OK |
| **Input System** | ✅ 已装，但 `activeInputHandler: 0`（旧版） | 代码用的是旧 `Input.GetButtonDown`，**保持 0 即可，不要切** |
| **Tag** | 空 | ⚠️ 需补 |
| **Layer** | 全空 | ⚠️ **必须补**（A* 寻路硬编码 Layer 名） |
| **Sorting Layer** | 只有 Default | ⚠️ 2D 项目必须补 |
| **Build Scenes** | 只有 SampleScene | ⚠️ 需确认是否够用 |

---

## 步骤 1：补 Layer（**最重要，不补 A* 寻路会失效**）

代码 `AStarNodeManager.cs:160-178` 用字符串匹配 Layer 名：

```csharp
case "Obstacle":   // ← 硬编码
case "Walkable":   // ← 硬编码
```

**操作**：`Edit → Project Settings → Tags and Layers`

在 **User Layer**（从第 8 层起）填入：

| Layer 索引 | 名称 | 用途 |
|---|---|---|
| 8 | **Player** | 玩家 |
| 9 | Enemy | 敌人（若不迁 ARPG 可不填，但代码里有引用） |
| 10 | **Obstacle** | ⚠️ A* 寻路依赖 |
| 11 | **Walkable** | ⚠️ A* 寻路依赖 |
| 12 | NPC | NPC |
| 13 | Confiner | 相机边界（SceneChanger 用） |

> 💡 **名称必须全字匹配**（大小写敏感）。如果只用 A* 寻路，至少要补 `Obstacle` 和 `Walkable`。

---

## 步骤 2：补 Sorting Layer（2D 项目必备）

**操作**：`Edit → Project Settings → Tags and Layers → Sorting Layers`

点 **+** 添加以下层级（**顺序很重要，从下到上**）：

```
Default       （已有）
Background    ← 添加（远景/天空）
Terrain       ← 添加（地面/瓦片）
Entities      ← 添加（角色/敌人/NPC）
Foreground    ← 添加（前景遮挡物）
UI            （UGUI 用 Canvas 自带 Order，可不要）
```

> 💡 横版解谜角色要被某些前景（如柱子）遮挡时，`Foreground` 层必不可少。Sprite Renderer 的 `Sorting Layer` 选对应层级。

---

## 步骤 3：补 Tag

**操作**：`Edit → Project Settings → Tags and Layers → Tags`

点 **+** 添加（按需，代码里 `gameObject.CompareTag("...")` 用到）：

- `Enemy`（敌人识别，EnemyCombat 代码里会用到）
- `Confiner`（Cinemachine 边界）
- `Player`（如果代码用 Tag 而不是 Layer 识别玩家）

---

## 步骤 4：Addressables 初始化（代码会用到）

代码 `InitialLoad.cs` 和 `GameSceneSO.cs` 引用了 Addressables。虽然导入的代码包**没有用到实际的场景加载逻辑**，但**不配置会编译报错**。

**操作**：

1. 菜单 `Window → Asset Management → Addressables → Groups`
2. 弹窗里点 **Create Addressables Settings**
3. 这会在 `Assets/AddressableAssetsData/` 生成配置（已 ignore，不进 git）

> ⚠️ 如果你**不用 Addressables**（解谜 GameJam 建议不用），可以反过来：
> - 删除 `Assets/ReTraumenD/Scripts/InitialLoad.cs`
> - 删除 `Assets/ReTraumenD/Scripts/Scene/SceneChanger.cs`
> - 在 `GameSceneSO.cs` 里删掉 `using UnityEngine.AddressableAssets;` 和 `AssetReferenceT` 字段
>
> 这样就能从 `manifest.json` 卸载 Addressables 包，更干净。

---

## 步骤 5：确认输入系统模式

代码用的是 **旧 Input Manager**：

```csharp
// UIManager.cs:128
bool pressed = Input.GetButtonDown(binding.buttonName);

// NPCChat.cs:100
if (... && Input.GetMouseButtonDown(0))
```

**操作**：`Edit → Project Settings → Player → Other Settings → Active Input Handling`

保持 **`Input Manager (Old)`**（当前 `activeInputHandler: 0`，已正确，**不要改成 Both 或 Input System**）。

> ⚠️ 如果改成 "New"，所有 `Input.GetXxx` 会编译报错。

---

## 步骤 6：设置 Build Scenes

当前只有 `Assets/ReTraumenD/Scenes/SampleScene.unity` 在 Build 列表里。

**操作**：`File → Build Settings`

- 把要用作入口的场景**拖到 Scenes 列表**（或点 Add Open Scenes）
- 确认**入口场景在列表第 0 位**（index 0 是启动场景）

> 💡 若用 `InitialLoad.cs` 的 Addressables 引导流程，入口场景应设为空场景（含 `InitialLoad` 组件），由它加载 Addressable 场景。若不用 Addressables，直接把主场景放第 0 位。

---

## 步骤 7：配置 TextMesh Pro（首次使用）

SMDJ 包里已有现成字体：`Assets/SMDJ/AlibabaPuHuiTi-3-85-Bold SDF.asset`（**中文字体，已就绪**）。

**操作**：

1. 菜单 `Window → TextMeshPro → Import TMP Essentials`（首次必点，导入 TMP 核心 Shader/精灵）
2. **不需要** Import Examples（可省空间）
3. 在 UI 文本组件（`TMP_Text`）的 `Font Asset` 字段拖入 `AlibabaPuHuiTi-3-85-Bold SDF`（替换默认的 LiberationSans）

---

## 步骤 8：编译验证

完成步骤 1-7 后：

1. **Ctrl+R** 刷新（或切回 Unity 自动 reimport）
2. 看 Console（`Window → General → Console`）
3. 应该 **0 error**

**如果还有报错，常见原因**：

| 报错 | 原因 | 解决 |
|---|---|---|
| `The type or namespace 'AddressableAssets' could not be found` | 没装 Addressables 包，或没初始化 | 步骤 4，或删掉相关代码 |
| `LayerMask: nothing matches 'Obstacle'` 运行时警告 | Layer 没补全 | 步骤 1 |
| `Input.GetButtonDown` 报错 | Active Input Handling 被改了 | 步骤 5 改回 Old |
| TMP 字体显示方块 | 没导入 TMP Essentials | 步骤 7 |
| `The type or namespace 'TMP_Text' could not be found` | 未装 TextMesh Pro | manifest 已有，reimport |

---

## 步骤 9：场景配置（让代码真正跑起来）

代码里的 Manager 都是单例，需要在场景里挂上：

打开 `Assets/ReTraumenD/Scenes/SampleScene.unity`，确认以下对象存在（或自己挂）：

| 组件 | 作用 | 是否必挂 |
|---|---|---|
| `UIManager` | UI 焦点/ESC 管理 | ⚠️ 必挂（否则 UI 不工作） |
| `TimeManager` | 暂停管理 | 若用 ESC 暂停则必挂 |
| `DialogManager` | 对话系统 | 若用对话则必挂 |
| `StatsManager` | 属性系统 | ARPG 用，解谜可删 |
| `EventSystem` | UI 交互 | ⚠️ 必挂（否则按钮点不了） |

> 💡 `EventSystem`：Hierarchy 右键 → UI → 会自动建 Canvas + EventSystem。

---

## 步骤 10：创建 SO 资产实例（事件总线）

代码定义了 SO 类（如 `VoidEventSO`），但**导入包里没有 `.asset` 实例**（因为 SO 资产无法跨项目迁移引用）。

**操作**：Project 窗口右键 → `Create → ...`

需要手动创建的 SO 资产（按需）：

```
Assets/ReTraumenD/SOInstances/         ← 新建文件夹统一管理
├── VoidEvent_SO.asset                 ← Create → Events → VoidEventSO
├── ToggleGameOverEvent.asset          ← Create → Events → ToggleCanvasEventSO
├── ToggleESCEvent.asset
├── SceneLoadEvent.asset               ← Create → Events → SceneLoadEventSO
└── ...（按你解谜用到的面板数量建）
```

> ⚠️ `UIManager` Inspector 里要把这些事件 SO 拖到对应字段，否则 ESC/GameOver 面板的开关不工作。

---

## 速查清单（照这个顺序做一遍）

```
□ 步骤 1  Layer: Player/Obstacle/Walkable/Confiner（必做）
□ 步骤 2  Sorting Layer: Background/Terrain/Entities/Foreground（必做）
□ 步骤 3  Tag: Enemy/Confiner（按需）
□ 步骤 4  Addressables: Create Addressables Settings（或删依赖代码）
□ 步骤 5  Active Input Handling = Old（已对，确认即可）
□ 步骤 6  Build Scenes: 入口场景放 index 0
□ 步骤 7  TMP: Import TMP Essentials + 配置中文字体
□ 步骤 8  Console 0 error 验证
□ 步骤 9  场景里挂 UIManager/TimeManager/EventSystem
□ 步骤 10 创建 SO 资产实例并接到 UIManager
```

做完这 10 步，代码就能编译运行，UI 框架就能用了。
