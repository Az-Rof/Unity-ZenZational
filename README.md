# ZenZational

A Unity-based 2D top-down game. Fight waves of zombies, collect buffs, and battle bosses. The project also experiments with glitch effects and cinematic transitions.

> **Status:** In development. The name, story, gameplay balance, and some boss features may change.

## Requirements

- **Unity Editor:** `6000.5.5f1` (Unity 6)
- **Render Pipeline:** Universal Render Pipeline (URP) `17.6.0`
- Other packages are managed by Unity Package Manager through `Packages/manifest.json`.

## Opening and running the project

1. Clone or download this repository.
2. In **Unity Hub**, select **Add/Open project** and choose the repository folder.
3. Make sure Unity Hub uses Unity `6000.5.5f1`.
4. Wait for Unity to import the assets and resolve the packages.
5. Open `Assets/Scenes/TitleScreen.unity` to start at the menu, or `Assets/Scenes/Gameplay.unity` to test gameplay directly.
6. Press **Play** in the Unity Editor.

From the Title Screen, use the **Start** button. Gameplay is also configured as scene index `1`; if a build cannot find the scenes, check **File → Build Profiles/Build Settings** and make sure both Title Screen and Gameplay are included.

## Controls

The main controls use **WASD** and the mouse. Gameplay input uses the Unity Input System.

| Input | Action |
|---|---|
| `W`, `A`, `S`, `D` | Move |
| Mouse | Aim and direct the weapon |
| Left mouse button | Shoot |
| `G` | Trigger the glitch test effect |
| `Esc` | Open/close the pause menu |

> Controls may change depending on scene, weapon, and Input System configuration.

## Current gameplay flow

`WaveManager` runs the progression using coroutines:

1. **Wave 1:** zombies, followed by one buff selection.
2. **Wave 2:** glitch effect, zombies, one buff selection, a fake power-off transition, then Boss 1.
3. **Boss 1:** melee attacks, barrel throws, and jumps that can release radial shockwaves.
4. **Wave 3:** a stronger glitch effect, zombies, then one buff selection.
5. **Final reveal:** a regular zombie appears as a decoy; the screen glitches and fades to black; a face sprite lunges into view and stares silently; the scream (if assigned) cues Boss 1 to appear and perform its opening attack.

A dedicated final-boss prefab is not available yet, so the current encounter reuses the Boss 1 prefab and a standard zombie. The `finalBossRevealFace` field in the Gameplay scene currently uses Boss 1's sprite; assign a dedicated close-up face sprite for a better reveal. The `GlitchingManager`'s **Face Reveal Scream** field also needs an `AudioClip` to make the scream audible. Verify the cinematic timing in Play Mode.

## Main systems

- **Player and weapons:** `Assets/Scripts/Player/playerController.cs`, `Assets/Scripts/WeaponData/`, and prefabs in `Assets/Prefab/Gun/`.
- **Zombies and bosses:** `Assets/Scripts/NPC/` and `Assets/Prefab/Enemies/`.
- **Waves and encounters:** `Assets/Scripts/NPC/WaveManager.cs`.
- **Buffs:** `Assets/Scripts/BuffSystem/`; choices are displayed through `BuffSelectionUI`.
- **Glitch and reveal:** `Assets/Scripts/Glitching/GlitchingManager.cs`.
- **Audio:** `Assets/Scripts/audioController/` and `Assets/Audio/`.
- **Main scenes:** `Assets/Scenes/TitleScreen.unity`, `Assets/Scenes/Gameplay.unity`, and `Assets/Scenes/GlitchingTest.unity`.

### Configuring glitch effects

On the `GlitchingManager` object in Gameplay, **Effect Mode** selects which controller is used:

- `Analog`
- `Digital`
- `Both`

Wave 2 and Wave 3 glitch strength is configured on the `WaveManager` component in the Gameplay scene. Make sure both Analog and Digital controllers are assigned on `GlitchingManager`.

### Configuring the final-boss reveal

On `WaveManager` in the Gameplay scene:

- `finalBossPrefab`: dedicated boss prefab (if empty, Boss 1 is used).
- `finalBossDecoyPrefab`: decoy prefab (if empty, the zombie prefab is used).
- `finalBossRevealFace`: sprite displayed during the reveal.
- `finalRevealBlackDuration`, `finalFaceLungeDuration`, `finalFaceStareDuration`, `finalFaceScale`: reveal timing and scale.

On `GlitchingManager`, assign an `AudioClip` to **Face Reveal Scream**. If left empty, the reveal continues without a dedicated scream sound.

## Folder structure

```text
Assets/
├── Audio/          # Music and sound effects
├── Content/        # Source visual assets
├── Graphics/       # UI, sprites, fonts, and other visual assets
├── Prefab/         # Player, zombie, boss, weapon, UI, and prop prefabs
├── Scenes/         # TitleScreen, Gameplay, GlitchingTest
├── Scripts/        # Player, NPC, buff, wave, glitch, and audio logic
└── Settings/       # Render/input settings and scene templates
Packages/           # Unity Package Manager manifest and lock file
ProjectSettings/    # Unity project configuration
```

## Important packages

See `Packages/manifest.json` for the complete package list and versions. Important project dependencies include:

- Universal Render Pipeline (`com.unity.render-pipelines.universal`)
- Input System (`com.unity.inputsystem`)
- Unity UI (`com.unity.ugui`)
- KinoGlitch Universal (`jp.keijiro.kino-glitch.universal`)
- NoiseShader (`jp.keijiro.noiseshader`), required by the glitch shader

Use Unity Package Manager and the project manifest to resolve packages. Do not remove NoiseShader while the KinoGlitch shader depends on it.

## Building

1. Open the project with the specified Unity version.
2. Open **Build Profiles** (or **Build Settings**, depending on the Unity interface).
3. Make sure `TitleScreen.unity` and `Gameplay.unity` are included, with Title Screen as the starting scene.
4. Select a target platform and click **Build**.

A .NET build using the `.csproj` files can help check some C# code, but it is **not a substitute for a Unity build**. Unity generally generates the `.csproj` and `.slnx` files; use the Unity project as the source of truth.

## Development notes

- Run and verify gameplay changes in Unity Play Mode; compiling alone does not validate Inspector references or scene behavior.
- Prefabs assigned to `WaveManager` should include `charStats` and any required AI components.
- Do not delete `.meta` files separately from their assets; Unity uses their GUIDs to maintain references.
- Parts of the final encounter are still scaffolding and need iteration/testing, including cinematic timing, the close-up face image, scream audio, and the opening attack.
