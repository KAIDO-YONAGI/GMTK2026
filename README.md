# Time Machine

![Unity](https://img.shields.io/badge/Unity-2022.3.62f3-222222?logo=unity&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white) ![URP](https://img.shields.io/badge/URP-14.0.12-555555)

**简体中文** | [English](README_EN.md)

基于 Unity 与 C# 的 2D 横版解谜与平台跳跃游戏，以时间回溯和持续衰老为核心。回溯可以改变角色位置和机关状态，衰老则逐渐削弱移动与跳跃能力，关卡解谜因此受到生存时间的限制。

作品参加了主题为 **Count Down** 的 GMTK Game Jam 2026，开发周期为 2026-07-23 至 2026-07-26。两套时间规则同时运行：回溯改变过去，衰老持续消耗未来。

在线试玩：[Time Machine — itch.io](https://retraumend.itch.io/time-machine)

## 项目内容

| 功能 | 内容 |
|---|---|
| 衰老与生存 | 年龄持续增长，移动与跳跃能力逐渐减弱；衰老和受伤对应不同的死亡提示 |
| 时间回溯 | 恢复角色与机关状态，可用时间窗口随年龄缩短；回溯期间衰老仍会继续 |
| 机关交互 | 水流与结冰、可破坏冰块、可调方向的尖刺发射器、可拾取尖刺、压力板与弹簧 |
| 关卡解谜 | 一关教学关和两个正式关卡，从角色回溯逐步引入机关组合与级联触发 |
| 反馈与界面 | 回溯时间条、血量心形、濒死自救提示、衰老暗角、粒子与屏幕震动 |

### 衰老与时间回溯

各关卡可配置年龄范围与增长速度，年龄同时影响移动速度、跳跃能力和可回溯时长。回溯可将场景状态退回数秒之前，衰老仍按现实时间推进，角色在修改过去时也会继续消耗生存时间。

回溯包含消耗、冷却和结束后的短暂移速提升。水流、冰块和发射出的尖刺同步参与状态回滚；角色回溯时，通过对象池生成的虚影剪影呈现运动轨迹。

### 机关组合与关卡推进

水流按格子向下扩散并计时结冰。冰块阻挡通行，可以被打破和回溯，破碎后再次触发会重新计时结冰。尖刺发射器通过操作环调整方向，支持连续与单次发射，发射出的尖刺可以通过回溯收回；拾取的尖刺可以用来破冰。

压力板通过事件连接水流、门等机关，弹簧提供竖直弹跳，陷阱尖刺和死亡区增加生存压力。关卡从角色位置与状态回溯开始，逐步引入水冰转换、尖刺收回与回溯后的机关级联。

### 生存反馈与场景表现

HUD 以心形显示生命值，受伤时伴随粒子与屏幕震动。血量归零后角色进入濒死状态，可以回溯自救或放弃并重开；到达年龄上限则触发衰老死亡。重开关卡会重置年龄。

场景采用 URP 渲染，Cinemachine 负责相机边界与震动，结合多图层背景、角色序列帧动画、原创背景音乐与音效呈现氛围。屏幕暗角随衰老进度逐渐加深。

### 比赛信息

| 评分维度 | 位次 | Score | Raw Score |
|---|---|---|---|
| 音频 | #3249 | 3.042 | 3.167 |
| 叙事 | #3935 | 2.562 | 2.667 |
| 创意 | #4482 | 3.283 | 3.417 |
| 美术 | #4647 | 3.042 | 3.167 |
| 乐趣 | #7228 | 2.402 | 2.500 |

作品在约 10,500 个参赛作品中排名约第 4700～5000 名，约前 45%。评分来源见 [itch.io 评审页面](https://itch.io/jam/gmtk-jam-2026/rate/4819494)。

## 使用入口

游戏可在 [itch.io](https://retraumend.itch.io/time-machine) 在线试玩。`A / D` 或方向键左右移动，空格跳跃，按住 `R` 回溯。完整操作、工程依赖与 Windows 构建见[项目上手与构建](Docs/项目上手与构建.md)。

## 文档导航

| 文档 | 内容 |
|---|---|
| [文档索引](Docs/README.md) | 上手、构建与开发参考入口 |
| [场景加载与面板系统](Docs/场景加载与面板系统使用说明.md) | 场景切换、事件接线与面板管理的开发参考 |
| [团队协作规范](Docs/团队协作规范.md) | 团队分工与协作约定 |

文档路由与多 Agent 并发协作采用我开发并维护的 [Y_MultipleAgentWorkflow](https://github.com/KAIDO-YONAGI/Y_MultipleAgentWorkflow)。

## 致谢与许可

### 团队

| 成员 | 职责 |
|---|---|
| ReTraumenD | 负责游戏玩法、需求与核心机制设计、回溯代码、机关实现、测试与关卡设计 |
| Smdj | 角色控制器与动画状态机、机关实现、美术管线对接，参与玩法与关卡设计 |
| Cola | 多图层背景原画、角色序列帧动画、粒子特效、原创背景音乐与音效选配 |

### 代码

项目代码采用 [GNU General Public License v3.0](LICENSE) 许可。

### 美术与音频资产

> ⚠️ **所有美术资产与音频资源严禁二次分发。**

本项目中的以下内容**不得以任何形式复制、转载、再发布或用于本项目以外的任何用途**：

- `Assets/Sprites/` 下的所有精灵图、图块集、UI 素材
- `Assets/Particles04/` 下的粒子特效素材（粒子制作为原创，基于 NBShader 材质与功能实现，见借物表）
- `Assets/Animations/` 下的动画片段
- `Assets/AudioRes/BGM/` 下的背景音乐（音效来自第三方免费音效库，见下方借物表）
- 场景中使用的所有背景原画、角色序列帧动画、视觉设计元素

这些资产为团队成员原创或经授权使用，权利仅限于本项目 `Time Machine` 的展示与运行。

> 上述限制针对团队成员原创资产；第三方素材（`Assets/Sprites/` 下三个素材包、`Assets/AudioRes/SFX/` 音效、`Packages/NB_FX/` 特效框架，见下方借物表）版权归其原作者所有，遵循各自原始许可协议。

### 第三方素材借物表

本项目使用了以下第三方免费素材，在此注明出处以示感谢：

| 素材包 | 作者 | 本项目用途 | 许可协议 | 获取地址 |
|--------|------|-----------|----------|----------|
| [Pixel Platformer](https://kenney.nl/assets/pixel-platformer) | Kenney (kenney.nl) | 水动画（Anim_GroupA / Anim_GroupB） | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/pixel-platformer |
| [Physics Assets](https://kenney.nl/assets/physics-assets) | Kenney (kenney.nl) | 金属及其破裂纹理（elementMetal 系列） | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/physics-assets |
| [SimpleKeys - Animated Pixel Keyboard Keys](https://beamedeighth.itch.io/simplekeys-animated-pixel-keyboard-keys) | beamedeighth | 按键 R 动画（R.png + R_Press） | 免费商用 | https://beamedeighth.itch.io/simplekeys-animated-pixel-keyboard-keys |
| [小森平的免费下载音效](https://taira-komori.net/freesoundcn.html) | 小森平（Taira Komori） | 全部游戏音效（SFX） | 免费商用 | https://taira-komori.net/freesoundcn.html |
| [NBShader（NB 特效框架）](https://zhuanlan.zhihu.com/p/1914718313197573768) | 唐雨轩（轩轩） | 粒子特效所用材质与后处理功能 | 见作者发布页 | https://zhuanlan.zhihu.com/p/1914718313197573768 |
| [NoiseShader](https://github.com/keijiro/NoiseShader) | Keijiro Takahashi | NB_FX 内置的噪声函数库（HLSL） | [MIT](https://opensource.org/licenses/MIT) | https://github.com/keijiro/NoiseShader |

> 对应本地路径：`Assets/Sprites/kenney_pixel-platformer/`、`Assets/Sprites/kenney_physics-assets/`、`Assets/Sprites/SimpleKeys/`、`Assets/AudioRes/SFX/`、`Packages/NB_FX/`。
