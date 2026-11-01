# Time Machine

![Unity](https://img.shields.io/badge/Unity-2022.3.62f3c1-222222?logo=unity&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white) ![URP](https://img.shields.io/badge/URP-14.0.12-555555)

[简体中文](README.md) | **English**

A 2D side-scrolling puzzle platformer built with Unity and C#, centered on time rewind and continuous aging. Rewinding changes the character's position and mechanism states, while aging weakens movement and jumping, turning each level into a puzzle against a limited lifetime.

Created for the **Count Down** theme at GMTK Game Jam 2026, during July 23–26, 2026. Two time rules run together: rewinding changes the past while aging consumes the future.

Play online: [Time Machine — itch.io](https://retraumend.itch.io/time-machine)

## Project Features

| Feature | Content |
|---|---|
| Aging and survival | Age increases continuously and weakens movement and jumping, with different death messages for aging and damage |
| Time rewind | Restores character and mechanism states; its available window shrinks with age while aging continues |
| Interactive mechanisms | Flowing water and freezing, breakable ice, aimable spike launchers, pickup spikes, pressure plates and springs |
| Puzzle progression | A tutorial and two main levels that progress from character-state rewinds to combined mechanisms and cascading triggers |
| Feedback and UI | A rewind meter, health hearts, a dying-state prompt, an aging vignette, particles and screen shake |

### Aging and Time Rewind

Each level has a configurable age range and aging rate. Age affects movement speed, jumping and the rewind window. Rewinding rolls scene states back several seconds while aging continues in real time, spending the character's remaining lifetime as the past changes.

Rewind includes a drain rate, cooldown and a brief movement boost afterward. Water, ice and fired spikes participate in state rollback, while pooled silhouette afterimages trace the character's movement during rewinds.

### Mechanisms and Level Progression

Water spreads downward cell by cell and freezes on a timer. Ice blocks passage and can be shattered or rewound; triggering it after a break restarts the freezing timer. Spike launchers use an aiming ring and support continuous or single-shot firing. Fired spikes can be rewound, and pickup spikes can break ice.

Pressure-plate events connect water flows, doors and other mechanisms. Springs provide vertical movement, while trap spikes and death zones add survival pressure. Levels progress from rewinding the character to water–ice transitions, spike recall and cascading mechanism triggers after a rewind.

### Survival Feedback and Presentation

The HUD displays health as hearts, with particles and screen shake accompanying damage. At zero health, the character enters a dying state and can rewind to recover or give up and restart. Reaching the age limit triggers death from aging; restarting a level resets age.

URP provides 2D rendering, and Cinemachine handles camera boundaries and screen shake. Layered background art, frame-by-frame character animation, original background music and selected sound effects establish the atmosphere. The vignette deepens with age.

### Jam Results

| Category | Rank | Score | Raw Score |
|---|---|---|---|
| Audio | #3249 | 3.042 | 3.167 |
| Narrative | #3935 | 2.562 | 2.667 |
| Creativity | #4482 | 3.283 | 3.417 |
| Art | #4647 | 3.042 | 3.167 |
| Fun | #7228 | 2.402 | 2.500 |

The game placed approximately 4,700–5,000 out of about 10,500 entries, around the top 45%. Ratings are listed on the [itch.io jam page](https://itch.io/jam/gmtk-jam-2026/rate/4819494).

## Getting Started

The game is playable on [itch.io](https://retraumend.itch.io/time-machine). Controls are `A / D` or the arrow keys to move, Space to jump and hold `R` to rewind. Full controls, dependencies and Windows build instructions are in the [setup and build guide](Docs/项目上手与构建.md), in Chinese.

## Documentation

| Document | Content |
|---|---|
| [Documentation index](Docs/README.md) | Setup, building and development references |
| [Scene loading and panels](Docs/场景加载与面板系统使用说明.md) | Development reference for scene transitions, event connections and panel management |
| [Team conventions](Docs/团队协作规范.md) | Team responsibilities and collaboration conventions |

The reference documents above are in Chinese.

Documentation routing and concurrent agent work use [Y_MultipleAgentWorkflow](https://github.com/KAIDO-YONAGI/Y_MultipleAgentWorkflow), a workflow I developed and maintain.

## Credits and License

### Team

| Member | Role |
|---|---|
| ReTraumenD | Gameplay, requirements and core-mechanic design, rewind code, mechanisms, testing and level design |
| Smdj | Character controller and animation state machine, mechanisms and art pipeline integration; contributed gameplay and level design |
| Cola | Layered background art, frame-by-frame character animation, particle effects, original background music and sound-effect selection |

### Code

Project code is licensed under the [GNU General Public License v3.0](LICENSE).

### Art & Audio Assets

> ⚠️ **Redistribution of all art assets and audio resources is strictly prohibited.**

The following content **must not be copied, reposted, re-published, or used for any purpose other than this project in any form**:

- All sprites, tilesets and UI assets under `Assets/Sprites/`
- Particle effect assets under `Assets/Particles04/` (original creations built on NBShader materials and features — see Credits)
- Animation clips under `Assets/Animations/`
- Background music under `Assets/AudioRes/BGM/` (SFX come from third-party free sound libraries — see Credits below)
- All background paintings, character frame animations, and visual design elements used in the scenes

These assets are original works by team members or used with authorization; rights are limited to the display and operation of this project, `Time Machine`.

> The restrictions above apply to team-made original assets. Third-party assets (the three packs under `Assets/Sprites/`, SFX under `Assets/AudioRes/SFX/`, and the effect framework under `Packages/NB_FX/` — see Credits below) are copyrighted by their original authors and governed by their respective licenses.

### Third-Party Asset Credits

This project uses the following third-party free assets, credited here with thanks:

| Asset Pack | Author | Used For | License | Source |
|------------|--------|----------|---------|--------|
| [Pixel Platformer](https://kenney.nl/assets/pixel-platformer) | Kenney (kenney.nl) | Water animations (Anim_GroupA / Anim_GroupB) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/pixel-platformer |
| [Physics Assets](https://kenney.nl/assets/physics-assets) | Kenney (kenney.nl) | Metal and fracture textures (elementMetal series) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/physics-assets |
| [SimpleKeys - Animated Pixel Keyboard Keys](https://beamedeighth.itch.io/simplekeys-animated-pixel-keyboard-keys) | beamedeighth | R key animation (R.png + R_Press) | Free for commercial use | https://beamedeighth.itch.io/simplekeys-animated-pixel-keyboard-keys |
| [Taira Komori's Free Sound Effects](https://taira-komori.net/freesoundcn.html) | Taira Komori | All sound effects (SFX) | Free for commercial use | https://taira-komori.net/freesoundcn.html |
| [NBShader (NB effect framework)](https://zhuanlan.zhihu.com/p/1914718313197573768) | Tang Yuxuan (XuanXuan) | Materials and post-processing used by particle effects | See author's release page | https://zhuanlan.zhihu.com/p/1914718313197573768 |
| [NoiseShader](https://github.com/keijiro/NoiseShader) | Keijiro Takahashi | Noise function library bundled with NB_FX (HLSL) | [MIT](https://opensource.org/licenses/MIT) | https://github.com/keijiro/NoiseShader |

> Local paths: `Assets/Sprites/kenney_pixel-platformer/`, `Assets/Sprites/kenney_physics-assets/`, `Assets/Sprites/SimpleKeys/`, `Assets/AudioRes/SFX/`, `Packages/NB_FX/`.
