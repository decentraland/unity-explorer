# Skybox

The sky is rendered by one Shader Graph material (`GenesisSkybox.mat`) driven by `SkyboxRenderController`, and everything that defines how it *looks* over a day lives in **look presets**: `SkyboxLookPreset` ScriptableObjects under `Assets/DCL/SkyBox/Presets/`. The *time* of day is a separate concern, owned by the skybox state machine, and is unchanged by the look.

## The looks

| Preset | Role | Where it is referenced |
|---|---|---|
| `StylizedV1.asset` | **The look that ships.** Stylized sky with a baked colour lookup, layered cloud strips, computed sun and moon arcs and procedural stars. Sun haze is available but ships off. | `Prefab/SkyboxRenderController.prefab` → `preset` |
| `Legacy.asset` | The previous sky, a 1:1 migration of the values that used to be hard-coded, and the base of every SDK-controlled sky. **Do not delete it.** | `Prefab/SkyboxRenderController.prefab` → `sceneLookPreset`; `SkyboxSettings.asset` → `LookPresets`, Addressable `SkyboxLookPreset_Legacy` in the `Essentials` group |
| `Halloween2026.asset` | Seasonal night look for Halloween 2026 on top of StylizedV1: more purple clouds, a bigger purple moon and more moonlight (plus brighter stars with more shooting stars). Sky gradients, and so the baked LUT, match StylizedV1, as do the sunrise, day and sunset colours. Turned on remotely (see *Feature flag* below). | `SkyboxSettings.asset` → `LookPresets`, Addressable `SkyboxLookPreset_Halloween2026` in the `Essentials` group |

Legacy stays for two reasons:

- It is the reference look, and it can be selected at runtime for comparison (see *Debug panel* below).
- **SDK control.** A scene that controls the environment through `PBSkybox`, `PBSun` or `PBEnvironmentFog` runs on the **Legacy** look: its colour bands and curves map directly onto what the SDK exposes. The controller prefab references it as `sceneLookPreset`, so it is always resident (see *SDK control* below).

Presets are plain data. Changing the default look is changing one reference on the controller prefab; switching to another listed look in production needs no build, only the feature flag.

## Data flow

```
SkyboxPlugin ──loads──> SkyboxSettingsAsset (settings, time, look preset list)
             ──instantiates──> SkyboxRenderController prefab (+ its default preset)
             ──applies──> the look named by alfa-skybox-look-preset, when set
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

## SDK control (`PBSkybox`, `PBSun`, `PBEnvironmentFog`)

A scene overrides the environment through three LWW components, valid on its root entity only:

| Component | ID | Fields |
|---|---|---|
| `PBSkybox` | 1223 | `skybox_texture` (equirect 2:1, file textures only), `sky_colors` {zenith, horizon, nadir, rim}, `clouds` {opacity, speed, color}, `stars` {brightness} |
| `PBSun` | 1224 | `color`, `visible`, `intensity` (multiplier on the time-of-day intensity, negative → 0), `direction` (direction the light travels in scene space; unset or zero = time-of-day path) |
| `PBEnvironmentFog` | 1225 | `color`, `density` (exponential, negative → 0) |

While the player is inside a scene with **any** of them, the controller runs the **scene look**: a runtime copy of `sceneLookPreset` (Legacy) with the scene's values written over it. Leaving the scene, or removing the last of the three components, applies the base look again (the default or feature-flagged look, or whatever the debug dropdown picked last).

```
SceneSkyboxHandlerSystem (scene world)   PBSkybox + PBSun + PBEnvironmentFog → sky texture + SceneEnvironmentProfile → SceneSkyboxOverrides (global skybox entity)
ApplySceneSkyboxOverridesSystem (global)  owner present?  → SkyboxRenderController.SetSceneLook(true, profile)
                                          owner gone?     → SkyboxRenderController.SetSceneLook(false, null)
SkyboxRenderController.SetSceneLook       profile.ApplyTo(sceneCopy, Legacy) → ApplyPreset(sceneCopy)
```

- The handler rebuilds the profile when any of the three components is dirty, when the set of present components changes (removing one of several sets no dirty flag on the others), and always when the scene becomes current again (dirty flags are reset while it is not current).
- `SceneEnvironmentProfile.ApplyTo` writes **every** SDK-controlled preset value: the scene's gradient where it has one, the Legacy value where it does not. Unsetting a field in the scene therefore restores the Legacy value on the next apply. Derived rules live there too: the sky bands drive the ambient trilight, the rim follows an overridden horizon, `PBSun.color` tints both the light and the disc, `PBSun.visible = false` swaps the disc, halo, moon and lens-flare curves for constant zero, `PBEnvironmentFog.density` is written to all four phase anchors of `fogDensityByPhase`. `timeToPhase` is pinned to identity, since SDK gradient `time` is the time of day itself.
- `PBSun.intensity` and `PBSun.direction` are not preset values: `SetSceneLook` keeps them on the controller (neutral `1` / none while the scene look is off), and `UpdateDirectionalLight` applies them after the clip or celestial-path evaluation. The multiplier goes on the assigned intensity only, so the intensity fallback (the light's own value on the clip path, the last undipped value on the computed path) never compounds it. A fixed direction sets the light rotation (pole-safe up hint, as in the celestial path) and `_DclSunDirection` to its opposite at every time of day; colour and intensity keep following the time of day.
- `SkyboxLookPreset` exposes `internal` setters for exactly the preset values; the asset is never written, only the runtime copy.
- SDK gradients are protocol `ColorGradient`s converted by `ColorGradientConverter`. `UnityEngine.Gradient` holds at most 8 colour keys, so a gradient with more keys is resampled at 8 evenly spaced times.
- The per-frame code has no override branches beyond the sun multiplier and direction: it evaluates `preset.*` as for any other look. The texture paths outside the preset are `SetSkyboxOverride`, which swaps `RenderSettings.skybox` to the `DCL/PanoramicSkybox` material while the time-of-day values keep going to the Genesis material (which is why those writes target the cached material, not `RenderSettings.skybox`), and the reflection override of `SkyboxToCubemapRendererFeature`. Reflections are always derived from `skybox_texture` (`SceneSkyboxOverrides.ReflectionSource`).
- **Video sources are disabled** (`SceneSkyboxHandlerSystem.VIDEO_TEXTURES_ENABLED`) until their cost is measured on mobile: a `VideoTexture` logs a warning and is ignored. The supporting code stays for the next iteration: the handler's video slot, `IMediaFactory.TryAddScreenSpaceConsumer`/`RemoveScreenSpaceConsumer`/`HasVideoTexture` (a screen-space consumer is never culled by `UpdateMediaPlayerPrioritizationSystem`), and the sliced reflection regeneration `SkyboxToCubemapRendererFeature` keeps up while the override is a `RenderTexture`.
- `SceneSkyboxOverrides.Owner` is the scene identity that owns the overrides: a `SceneShortInfo`, not a base parcel, since portable experiences share `0,0`. Any of the three components on the current scene makes it the owner as soon as something is published, whatever fields it sets, so a scene with only a `skybox_texture` also runs on the Legacy base.

## Feature flag

`explorer-alfa-skybox-look-preset` picks the production look without a build. String payload (any variant name) holding the name of a `SkyboxSettings.asset → LookPresets` entry, for example `Halloween2026` (case and surrounding whitespace are ignored).

- `SkyboxPlugin` reads it once at start-up, loads the entry's Addressable and applies it before the controller initialises, so the controller sets up fog, statics and lens flare from that look and the first rendered frame already has it. A change on the server reaches players on their next launch.
- Flag off, no payload or an empty payload keep the prefab's default look. A name missing from the list logs a warning and a failed load logs the exception; both keep the default look, and the skybox always starts.
- Only listed presets can be selected, and only ones shipped in the build: a new look needs a release before the flag can point at it.

## Debug panel

**Debug panel → Skybox → Look preset** switches looks at runtime. The dropdown lists the default preset plus the entries of `SkyboxSettings.asset → LookPresets`, which are Addressable references loaded the first time they are needed. It opens on the active look, so it shows the flagged one when the flag is set, and reuses its loaded asset. Switching re-applies the statics and the current time, so the change is immediate.

## Authoring

- **Scene**: `Assets/Scenes/SkyboxAuthoring.unity`. Select `SkyboxRenderController` in Play mode for a time-of-day slider, a real-time day loop (**Day length (s)** + **Play**), phase shortcuts and a four-phase screenshot capture.
- **Preset inspector**: a *Phase palette* table (every colour role × Night/Sunrise/Day/Sunset, editing the ramp keys in place), a per-phase hue/saturation/brightness shift, the fog density row, and the LUT bake. Edits apply live in Play.
- **Edit mode** (no Play) shows the stylized sky without clouds and stars: those come from global values that only exist while the controller runs.

## Textures

- **Cloud strips** (`Textures/Clouds/SkyboxClouds{Cumulus,Low,Overhead}_V2.png`): 4096×512, RGBA as data, linear, repeat U / clamp V, BC7 with mips. R shading, G backlit look, B growth order, A mask; RGB premultiplied by A. Artist-authored.
- **Sky LUT** (`Presets/<Preset>_SkyLut.asset`, one per preset, e.g. `StylizedV1_SkyLut.asset`): 256×5 RGBAHalf, baked from that preset's four sky gradients.
- **Horizon noise** (`Textures/Noise4.png`): tiling noise for the horizon silhouettes and the star dim patches.

## Known caveats

- The stylized path takes authored colours **as-is** (no sRGB→linear conversion), consistently across the lookup, clouds and haze; `StylizedV1` was tuned against that behaviour. Changing it means re-tuning.
- The listed presets sit in the `Essentials` Addressables group so the flag and QA can reach them in builds. Each is only loaded when flagged or picked in the debug dropdown, but each adds to the shipped bundle. Drop an entry once its look is no longer needed (a seasonal look after its season). Legacy stays: the controller prefab references it as `sceneLookPreset`, since SDK-controlled scenes start from it, so it is always resident.
- `DCL/PanoramicSkybox` (visible sky) and `DCL/EquirectToCube` (reflection cubemap) share one lat-long mapping, `uv = (atan2(d.x, d.z) / 2π + 0.5, asin(d.y) / π + 0.5)`. Change both together, or the reflections stop matching the sky.
- `DCL/EquirectToCube` is created through `Shader.Find` by `SkyboxToCubemapRendererFeature`, so it must stay in *Always Included Shaders* (`ProjectSettings/GraphicsSettings.asset`), otherwise player builds strip it. It builds a fullscreen triangle from `SV_VertexID`, so faces must be drawn with `CoreUtils.DrawFullScreen`; `Graphics.Blit` would feed it quad vertices.
- Fog on/off is also written by the quality settings. A preset with fog enabled turns fog on once when the controller initialises, then drives fog colour and density every frame.
