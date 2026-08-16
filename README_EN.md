# Time Machine

[简体中文](README.md) | **English**

> **GMTK Game Jam 2026** entry — Theme: *Count Down*
> Development window: 2026-07-23 – 2026-07-26 (4 days)

Play online: [Time Machine - itch.io](https://retraumend.itch.io/time-machine)

---

## Controls

| Key | Action |
|-----|--------|
| A / D or ← / → | Move left / right |
| Space | Jump |
| Hold R | Time rewind |
| ESC | Pause menu |
| Left Mouse | Drag certain mechanisms |

---

## Core Concept

`Time Machine` is a Unity 2D side-scrolling puzzle platformer built around one cruel idea:

**Your body is the countdown itself — and the consumable you spend to escape the loop of a chaotic timeline.**

The core theme: **rewind the past, while the future burns away as you age.**

> *You can change the past and reach a better future, but your body won't stop counting down. No matter how bright what lies ahead, you will fade away in the flow of time…*

---

## Core Mechanics

### Aging

- Age increases in real time; range and speed are configurable per level
- Aging is irreversible and keeps running even during rewinds
- Movement speed and jump power decay as you age, with a sharp slowdown near death
- A screen vignette gradually deepens with aging progress for a sense of pressure
- Reaching the age limit dies of old age (`OldDead`); dying from damage is a violent death (`UnnormalDead`) — each with its own death message
- Death restarts the current level with age reset

### Time Rewind

- Hold the rewind key to rewind continuously, rolling the scene back several seconds
- The rewind window shrinks linearly with age: longest when young, shortest when old
- Rewinding has a drain rate and cooldown; a brief speed boost follows each rewind
- Interactive mechanisms rewind in sync: water, ice, spikes and more can all be rolled back
- Silhouette afterimages trail behind you while rewinding (object-pooled)

### Health & Survival

- Health shown as HUD hearts; damage triggers particles and screen shake
- At zero health you enter a dying state — rewind to save yourself, or stagger toward one last chance
- Giving up restarts the current level

### HUD

- Rewind time bar (remaining amount + color gradient)
- Health hearts
- Dying hint panel

---

## Level Mechanisms

| Mechanism | Description |
|-----------|-------------|
| **Water & Ice** | A trigger starts the water flow or freezing; water spreads downward cell by cell and freezes on a timer. Ice can be shattered and rewound; after breaking, triggering again restarts the freezing timer. Ice blocks passage; water does not |
| **Spike Launcher** | Rotate the aiming ring to set the firing direction; supports continuous and single-shot modes. Fired spikes can be rewound back |
| **Pickup Spike** | Pick it up and use it to shatter ice |
| **Trap Spikes** | Deal damage on contact |
| **Spring** | Vertical bounce |
| **Pressure Plate** | Triggers bound events when stepped on (e.g. start water flow, open doors) |
| **Triggers** | Death zones, level goals, guide zones |

---

## Level Design

3 finished levels + 2 test scenes:

- **Teaching Level**: rewind tutorial, passing through obstacles
- **Normal Level 1**: puzzle-solving under death pressure
- **Normal Level 2**: flowing-water freezing mechanism combos

Puzzle difficulty progresses in three tiers:

1. Rewind **your own** position and state
2. Rewind **mechanism** states (water ↔ ice, spike recall)
3. **Cascading mechanism triggers** after a rewind

---

## Team

| Member | Role |
|--------|------|
| **ReTraumenD** | Gameplay design, requirement management, core mechanic design, rewind code, mechanism implementation, testing and level design |
| **Smdj** | Character controller and animation state machine, mechanism implementation, art pipeline integration; contributed gameplay and level design ideas |
| **Cola** | Side-scrolling background art (multi-layer PSD with parallax), 2D character frame-by-frame animation, particle effects, original BGM, SFX selection |

---

## Jam Results

itch.io ratings (GMTK Game Jam 2026):

| Category | Rank | Score | Raw Score |
|----------|------|-------|-----------|
| Audio | #3249 | 3.042 | 3.167 |
| Narrative | #3935 | 2.562 | 2.667 |
| Creativity | #4482 | 3.283 | 3.417 |
| Art | #4647 | 3.042 | 3.167 |
| Fun | #7228 | 2.402 | 2.500 |

---

## Tech Stack

- **Engine**: Unity 2022.3.62f3c1 LTS
- **Render pipeline**: Universal Render Pipeline (URP) 14.0.12
- **Camera**: Cinemachine (Virtual Camera + Confiner2D + Camera Shake)
- **Target platform**: Standalone Windows 64-bit
- **2D feature set**: com.unity.feature.2d 2.0.1
- **UI**: TextMesh Pro + Canvas (ScaleWithScreenSize, 1920×1080)

---

## Project Structure

```
Assets/
├── Scripts/          # C# scripts
│   ├── Characters/   # Player character (movement, animation, rewind, health)
│   ├── Gameplay/     # Level mechanisms (ice, spikes, springs, pressure plates, water, etc.)
│   ├── Systems/      # Systems (rewind framework, audio, scene management, time management)
│   ├── UI/           # UI controllers (ESC panel, Game Over, HUD, guide canvas)
│   ├── Managers/     # Global managers
│   └── SO/           # ScriptableObject definitions
├── Scenes/           # Scene files
│   ├── InitialScene.unity        # Entry scene
│   ├── PersistentScene.unity      # Persistent scene (managers, camera, player, canvases)
│   ├── StartingMenu.unity        # Main menu
│   └── LevelScenes/              # Level scenes
│       ├── TeachingLevel01       # Teaching level
│       ├── NormalLevel01         # Normal level 1
│       └── NormalLevel02         # Normal level 2
├── Prefabs/          # Prefabs (mechanisms, particles, UI components)
├── SOs/              # ScriptableObject assets (animation config, audio config, event channels)
├── Sprites/          # Sprites and tilesets
├── AudioRes/         # Audio resources (BGM / SFX)
├── Animations/       # Animation clips
├── Particles04/      # Particle effect assets
├── URPAssets/        # URP pipeline assets
└── Shaders/          # Custom shaders
```

---

## License & Asset Notice

### Code

The code of this project is licensed under the [GNU General Public License v3.0](LICENSE).

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
