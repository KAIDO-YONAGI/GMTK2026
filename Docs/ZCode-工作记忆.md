# ZCode 工作记忆 —— GameJam 项目规范

> 本文件记录用户对 ZCode 在本项目中提出的所有持续性要求。**每次会话开始时应优先读取本文件并严格遵守。**
> 路径：`D:\Unity\Projects\GameJam\Docs\ZCode-工作记忆.md`
> 更新日期：2026-07-18

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
- **编辑器开发时**：Hierarchy 里 PersistentScene **必须保持加载**，让用户能完整看到 `Managers (SceneChanger/TimeManager/UIManager) + FadeCanvas + ESCPanel + EventSystem`。
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

- **Unity 版本**：2022.3.62f3c1 LTS
- **渲染管线**：Built-in
- **目标项目类型**：横版解谜（GameJam）
- **核心代码包**：`Assets/ReTraumenD/`（从 My_ARPG 项目迁移改造）
- **美术资源包**：`Assets/SMDJ/`（含 570+ 图标、阿里普惠体中文字体 SDF）
- **Unity MCP**：CoplayDev `com.coplaydev.unity-mcp` v10.0.0
  - 全局配置：`~/.zcode/cli/config.json` → `mcp.servers.unity`（HTTP 127.0.0.1:8080/mcp）
  - 本地 Python server 源：`D:\MCP\unity-mcp-local\unity-mcp-10.0.0\Server`
- **远程仓库**：Gitee `https://gitee.com/KAIDOYONAGI/game-jam2607`（分支 `main`）
- **场景架构**（详见 `Docs/Scene-and-Panel-System-Usage.md`）：
  - Build Index 0: `InitialScene`（入口）
  - Build Index 1: `PersistentScene`（常驻）
  - Build Index 2: `StartingMenu`（主菜单）
  - Build Index 3: `Level01`（关卡）
- **面板系统**：UIManager 事件驱动 + ESCPanel（ESC 暂停菜单已配置）
- **CanvasToToggle 枚举**：`ESC / GameOver / Dialog / Default`（已精简，去掉了 ARPG 专属值）

---

## 修订记录

| 日期 | 修订内容 |
|---|---|
| 2026-07-18 | 初始版本，汇总所有用户提出的持续性规范 |
