# Skybox

The sky is rendered by one Shader Graph material (`GenesisSkybox.mat`) driven by `SkyboxRenderController`, and everything that defines how it *looks* over a day lives in **look presets**: `SkyboxLookPreset` ScriptableObjects under `Assets/DCL/SkyBox/Presets/`. The *time* of day is a separate concern, owned by the skybox state machine, and is unchanged by the look.

## The two looks

| Preset | Role | Where it is referenced |
|---|---|---|
| `StylizedV1.asset` | **The look that ships.** Stylized sky with a baked colour lookup, layered cloud strips, computed sun and moon arcs and procedural stars. Sun haze is available but ships off. | `Prefab/SkyboxRenderController.prefab` → `preset` |
| `Legacy.asset` | The previous sky, a 1:1 migration of the values that used to be hard-coded, and the base of every SDK-controlled sky. **Do not delete it.** | `Prefab/SkyboxRenderController.prefab` → `sceneLookPreset`; `SkyboxSettings.asset` → `DebugLookPresets`, Addressable `SkyboxLookPreset_Legacy` in the `Essentials` group |

Legacy stays for two reasons:

- It is the reference look, and it can be selected at runtime for comparison (see *Debug panel* below).
- **SDK control.** A scene that controls the skybox through the `PBSkybox` component runs on the **Legacy** look: its colour bands and curves map directly onto what the SDK exposes. The controller prefab references it as `sceneLookPreset`, so it is always resident (see *SDK control* below).

Both presets are plain data. Changing the shipped look is changing one reference on the controller prefab.

## Data flow

```
SkyboxPlugin ──loads──> SkyboxSettingsAsset (settings, time, debug look list)
             ──instantiates──> SkyboxRenderController prefab (+ its preset)
             ──injects──> SkyboxTimeUpdateSystem

SkyboxTimeUpdateSystem (every frame)
  └─ SkyboxStateMachine → time of day   (SDK component > scene metadata > realm > UI override > global clock)
  └─ SkyboxRenderController.UpdateSkybox(timeOfDay)
       ├─ light: rotation, intensity, disc size, halo, lens flare, haze   (keyed on time of day)
       └─ palette: sky, clouds, disc colour, ambient, fog               (keyed on phase)
```

`SkyboxRenderController` reads everything from the preset:

- **Once per preset** (`ApplyPresetStatics`): material statics, cloud strips and layer setup, stars and haze constants, reflection intensity, the `_DCL_SKY_STYLIZED` shader keyword.
- **Every frame**: colours evaluated at the **phase**, light and disc values evaluated at the **time of day**.

**Phase vs. time.** A preset has a `timeToPhase` curve. Every colour ramp is keyed on phase (Night 0, Sunrise 0.25, Day 0.5, Sunset 0.75, Night 1), while sun position, disc size and halo follow clock time. That lets the palette timing be reshaped without touching the celestial mechanics, and keeps dawn and dusk palettes pinned when the day length changes.

## What a preset holds

| Block | Fields | Notes |
|---|---|---|
| Timing | `timeToPhase` | Identity by default. |
| Sun mechanics | `lightIntensity`, `sunSize`, `sunOpacity` curves | Time-keyed. Empty curve = keep the legacy animation clip's channel. |
| Directional light | `directionalColorRamp`, `sunColorRamp`, halo curves, `moonMaskSize` | |
| Celestial path | `computeCelestialPath`, sunrise/sunset, moonrise/moonset, azimuth and tilt per body, swap window, moon mask and disc size, `moonColorRamp` | Replaces the rotation clip. The light crosses from sun to moon inside a hidden swap window. |
| Sun haze | `sunHaze*` | Disc grows, flattens, softens, takes a ragged rim and a top-to-bottom gradient near the horizon. Sun only. |
| Sky lookup | `useSkyLut`, four elevation gradients (`skyNight` … `skySunset`), baked `skyLut`, horizon noise | Bake with the **Bake sky LUT** button after editing the gradients. |
| Stars v2 | density, size, per-phase brightness, twinkle, patches, horizon fade, shooting-star rate | Procedural, no texture. |
| Clouds v2 | `cloudLayers[]` (strip, stretch/offset, tiling, speed, strength, opacity, presence per phase), lit and shadow ramps, highlight, zenith fade, occlusion | Up to three strips on the Unreal dome mapping. |
| Ambient / fog / reflections | trilight ramps, `fogColorRamp`, `fogDensityByPhase`, `reflectionIntensity` | |
| Material (legacy bands) | spreads, blends, rim, legacy stars, cloud cubemap, second sun | Used by the legacy variant only; hidden in the inspector while the lookup is on. |

Fields that one mode ignores are folded away by the preset inspector, and `SkyboxRenderController` warns about unsupported combinations each time a preset is applied (for example Clouds v2 without the sky lookup).

## Shader

Two variants of the same graphs, selected by the global **`_DCL_SKY_STYLIZED`** multi_compile keyword, set from `useSkyLut`:

- **Legacy variant**: the original node maths, byte-for-byte.
- **Stylized variant**: the sky lookup, stars, cloud strips and additive sun composite; the legacy sky, stars and cloud cubemap are compiled out.

The stylized logic lives in HLSL Custom Function files under `Assets/DCL/StylizedSkybox/Shaders/HLSL/` (`SkyLut`, `CloudsV2`, `SunDisc`, `SunComposite`, `SkyboxGlobals`). All their parameters are **global shader values** (`_Dcl*`) written by the controller, so the reflection cubemap bake (`SkyboxToCubemapRendererFeature`, which renders the fullscreen twin of the graph) sees the same data without extra material properties. Both main graphs (`GenesisSky`, `GenesisSkyBoxFullScreen`) use the same `GenesisSkyCelestial` sub-graph, whose disc is one `SunDisc` node.

## SDK control (`PBSkybox`)

A scene can override the environment through the `PBSkybox` component on its root entity (textures for the sky, the reflections and the clouds; gradients for the sun, the sky bands, the rim, the fog and the cloud tint; cloud opacity/speed, star brightness, sun visibility). While the player is inside such a scene the controller runs the **scene look**: a runtime copy of `sceneLookPreset` (Legacy) with the scene's values written over it. Leaving the scene, or removing the component, applies the base look again (the shipped preset, or whatever the debug dropdown picked last).

```
SceneSkyboxHandlerSystem (scene world)   PBSkybox → textures + SceneEnvironmentProfile → SceneSkyboxOverrides (global skybox entity)
ApplySceneSkyboxOverridesSystem (global)  owner present?  → SkyboxRenderController.SetSceneLook(true, profile)
                                          owner gone?     → SkyboxRenderController.SetSceneLook(false, null)
SkyboxRenderController.SetSceneLook       profile.ApplyTo(sceneCopy, Legacy) → ApplyPreset(sceneCopy)
```

- `SceneEnvironmentProfile.ApplyTo` writes **every** SDK-controlled preset value: the scene's gradient where it has one, the Legacy value where it does not. Unsetting a field in the scene therefore restores the Legacy value on the next apply. Derived rules live there too: the sky bands drive the ambient trilight, the rim follows an overridden horizon, `sun.color` tints both the light and the disc, `sun.visible = false` swaps the disc, halo, moon and lens-flare curves for constant zero. `timeToPhase` is pinned to identity, since SDK gradient `time` is the time of day itself.
- `SkyboxLookPreset` exposes `internal` setters for exactly those values; the asset is never written, only the runtime copy.
- SDK gradients are protocol `ColorGradient`s converted by `ColorGradientConverter`. `UnityEngine.Gradient` holds at most 8 colour keys, so a gradient with more keys is resampled at 8 evenly spaced times.
- The per-frame code has no override branches: it evaluates `preset.*` as for any other look. The only SDK paths outside the preset are the texture overrides: `SetSkyboxOverride` swaps `RenderSettings.skybox` to the `DCL/PanoramicSkybox` material while the time-of-day values keep going to the Genesis material (which is why those writes target the cached material, not `RenderSettings.skybox`), `SetCloudsOverride` projects an equirect image into a cube render texture that becomes the scene copy's `cloudsCubemap`, and the reflection override goes to `SkyboxToCubemapRendererFeature`.
- `SceneSkyboxOverrides.Owner` is the scene identity that owns the overrides; any `PBSkybox` on the current scene makes it the owner, whatever fields it sets, so a scene with only a `skybox_texture` also runs on the Legacy base.

## Debug panel

**Debug panel → Skybox → Look preset** switches looks at runtime. The dropdown lists the shipped preset plus the entries of `SkyboxSettings.asset → DebugLookPresets`, which are Addressable references loaded the first time they are picked. Today the only entry is Legacy. Switching re-applies the statics and the current time, so the change is immediate.

## Authoring

- **Scene**: `Assets/Scenes/SkyboxAuthoring.unity`. Select `SkyboxRenderController` in Play mode for a time-of-day slider, a real-time day loop (**Day length (s)** + **Play**), phase shortcuts and a four-phase screenshot capture.
- **Preset inspector**: a *Phase palette* table (every colour role × Night/Sunrise/Day/Sunset, editing the ramp keys in place), a per-phase hue/saturation/brightness shift, the fog density row, and the LUT bake. Edits apply live in Play.
- **Edit mode** (no Play) shows the stylized sky without clouds and stars: those come from global values that only exist while the controller runs.

## Textures

- **Cloud strips** (`Textures/Clouds/SkyboxClouds{Cumulus,Low,Overhead}_V2.png`): 4096×512, RGBA as data, linear, repeat U / clamp V, BC7 with mips. R shading, G backlit look, B growth order, A mask; RGB premultiplied by A. Artist-authored.
- **Sky LUT** (`Presets/StylizedV1_SkyLut.asset`): 256×5 RGBAHalf, baked from the four sky gradients.
- **Horizon noise** (`Textures/Noise4.png`): tiling noise for the horizon silhouettes and the star dim patches.

## Known caveats

- The stylized path takes authored colours **as-is** (no sRGB→linear conversion), consistently across the lookup, clouds and haze; `StylizedV1` was tuned against that behaviour. Changing it means re-tuning.
- Legacy is referenced by the controller prefab (SDK-controlled scenes start from it) and listed in the `Essentials` Addressables group for the debug dropdown, so it is always resident.
- Fog on/off is also written by the quality settings. A preset with fog enabled turns fog on once when the controller initialises, then drives fog colour and density every frame.
