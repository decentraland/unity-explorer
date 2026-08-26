# Outfit Studio

Editor tool for artists: compose an outfit from marketplace wearables, pose the avatar with an
emote and capture high-res stills / MP4 video — without leaving Unity.

Open via **Decentraland ▸ Outfit Studio**.

## Workflow

1. Open the studio scene via **Decentraland ▸ Open Outfit Studio Scene** (a dedicated copy of
   Main with set dressing — custom lighting/backdrop/post), or `Assets/Scenes/Main.unity`;
   the tool works in either.
2. Open the Outfit Studio window and browse the marketplace catalog (left pane).
   Search, filter by slot / rarity / body and click items to equip them.
   **The avatar assembles live in the Scene/Game view in edit mode** — no play mode needed for
   outfit selection (static idle pose; use **Clear Preview** in the toolbar to remove it).
3. Pick a pose: a **quick-pose button** (one per GLB in `Assets/OutfitStudio/Poses/` — drop your
   single-frame pose GLBs there and they appear as buttons under the **Pose** header; `⟳` rescans),
   an embedded emote from the dropdown, or any marketplace emote from the **Emotes / Poses** tab.
   Poses/emotes play in play mode; use ▶ / ❚❚ / ■ and the scrubber to freeze a specific frame.
4. Press **▶ Enter Play** for animation and capture (or Apply while playing). The edit-mode
   preview clears itself automatically and the renderer's builder mode loads the same outfit;
   changes keep auto-applying while you pick items.
5. Capture:
   - **Capture Still** — PNG at the configured resolution (independent of the Game view size),
     optionally with a transparent background.
   - **Start/Stop Video** — manual MP4 recording of the Game view (Unity Recorder).
   - **Record Emote** — records exactly one full emote playback.
   - **Record Turntable** — deterministic 360° spin over the configured duration.

   Files land in the `Captures/` folder next to the project by default.

## Reproducing an outfit

- **Share code** — the query-string in the Share code box fully describes the outfit
  (body shape, wearable URNs, colors, pose). Copy it, send it to someone, paste it back with
  **Load from code**. The same string works as `Bootstrap.debugUrl` and as URL parameters for
  the deployed web renderer (builder mode).
- **Presets** — save named `OutfitPreset` assets in the project for a local outfit library.

## Shader buttons (studio scene)

At the top of the outfit pane, three buttons pick the avatar's shader — applied to everything
in the viewport (edit AND play mode) and kept across reloads until you pick another:

- **DCL_Toon** — the official Decentraland shader, exactly as shipped. Default.
- **DCL_Toon_Studio** — same look, unlocked: rim light (`RimLight_Power`, color, mask...),
  ambient (`GI_Intensity`) and normal-map strength are live material properties, spot/point
  lights affect the avatar (per-pixel, studio scene only), and the metallic-branch features
  (normal maps + stylized matcap metallics) are included. Select a wearable's material instance
  in play mode to tweak the knobs live.
- **DCL_Stylized_PBR** — a stylized physically-based look (Fortnite/Overwatch direction):
  wrapped stylized diffuse, soft GGX specular, cloth sheen, clearcoat, artist rim, matcap-based
  metal reflections, plus an on/off outline toggle on the material.

Switching is lossless — all three share the same material inputs. Eyes/eyebrows/mouth always
keep their own shader. Only the dedicated studio scene is affected; the shipping renderer never
sees these shaders.

**Tuning controls:** picking DCL_Toon_Studio or DCL_Stylized_PBR reveals a **Matcap dropdown**
(which reflection texture the stylized metal uses) plus live art-direction sliders under the
buttons — rim intensity/power/mask/color, ambient, metal strength, matcap tint/blur, and (for
PBR) diffuse wrap, shadow sharpness, specular softness, sheen, clearcoat and an **Emission
Strength** control. They apply instantly in the viewport (edit and play mode), persist between
sessions, and **Reset shader defaults** clears them. Both shaders ship with a tuned default look
(warm-gold rim); stock DCL_Toon has no controls — it's the fixed official look.

Both shaders read the same wearable metallic data, so a **metallic wearable shows chrome/matcap
metal** — pick a matcap and tune Metal Strength / Matcap Metal Blend (PBR) to match the look.

## Debug tab & Clean View

The renderer's built-in play-mode debug overlay (JSBridge invoke, URL presets, Print Config,
Random Profile, zoom) lives in the window's **Debug** tab. The **Clean View** toolbar toggle
(on by default) hides that overlay in the Game view so only the avatar is visible — mouse-drag
rotation and the loading spinner keep working. Toggle it off to get the classic in-game overlay
back (plain play mode without the window is unaffected either way).

## Load from Collection (Debug tab)

Preview a whole collection like the explorer's `--self-preview-builder-collections`:
- **Published collection**: paste the `0x...` contract address → Load. No login needed.
- **Draft (unpublished) collection**: paste the collection UUID from the Builder URL. This needs
  your Decentraland identity once (~30 days): log into builder.decentraland.org, open devtools →
  Application → Local Storage, copy the entry containing `ephemeralIdentity`/`authChain`, and
  paste it into the Identity field. It's stored in Unity's EditorPrefs (outside the repo).
  **Never commit or share that JSON — it contains a temporary private key.**

Click grid items to equip; draft items ride along in share codes as `base64=` params. Draft
emotes play in play mode only.

## Notes

- Browsing, preset editing **and 3D outfit preview** work in edit mode; emote playback and
  capture need play mode. The edit-mode preview is a static idle pose (no spring bones/outline).
- The prod/dev toggle in the toolbar switches between `.org` and `.zone` backends.
- Wearables with no representation for the selected body shape are skipped with a warning.
- Everything lives in this folder except two small touch points: the `com.unity.recorder`
  dependency in `Packages/manifest.json` and emote-URN support in
  `PreviewController.LoadForBuilder`.
