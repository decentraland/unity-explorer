# 3D asset reference

Read before placing, downloading, converting, or exporting any 3D model.

## Picking from the sdk-skills catalog

- Fetch the `preview:` thumbnail BEFORE downloading — some models render near-black or broken even in their own previews (e.g. arcade-cabinet-atari). Use the exact `[anim: ...]` clip names; a wrong clip name fails silently (no error, no motion) — cross-examine: burst-capture (`-n 3 -i 1`) and diff frames to prove an animation is actually running.
- Catalog dims/pivots lie: bounding boxes can include baked animation paths or outline meshes, pivots can sit mid-crown (palms) or nowhere near the mesh (a baked drive path carried one car's mesh entirely off-parcel — swap such models rather than debug them). Place → screenshot → adjust is faster than trusting listed sizes.

## Downloading models

- Free CC0 model sources that download cleanly via curl: kenney.nl (zip URL is on the asset page, FBX+OBJ+GLTF inside), and itch.io free packs via the scripted flow: POST `<game>/download_url` with the page's csrf_token → GET the returned key URL → grab `data-upload_id` → POST `<game>/file/<id>?source=game_download` → signed CDN URL (expires ~60s, download immediately).
- Downloaded GLBs into the scene folder hot-load without restarting the dev server. Many props ship with no colliders — cross-examine solidity: walk onto them and check the player's `y` via `get_player_state`; add `visibleMeshesCollisionMask: 3` for anything that should be solid.

## Blender authoring & conversion

- Blender-authored GLBs work end-to-end: export with `bpy.ops.export_scene.gltf(use_selection=True)` straight into the scene's Models folder (hot-loads like any file change). Set the object origin to bottom-center before export (`cursor to (0,0,0)` + `origin_set(type='ORIGIN_CURSOR')` with geometry built up from z=0) so `position.y = 0` grounds the model, and `transform_apply` rotation/scale for clean transforms. Principled BSDF emissive (Emission Color + Strength) renders as expected in Explorer, including the zero-channel neon saturation rule (see `visuals.md`).
- Converting downloaded FBX/OBJ to GLB in Blender works, with three verified traps: (1) FBX materials can import with Principled Alpha = 0 (FBX transparency-factor quirk, seen on Quaternius packs) — the GLB then has `alphaMode: MASK` with baseColor alpha 0 and the model is INVISIBLE in Explorer while its entity, tween and logs all look healthy; force Alpha=1 + `blend_method='OPAQUE'` before export, and when a GLB renders nothing, parse its JSON chunk (nodes/materials) instead of guessing. (2) The glTF exporter's default animation mode exports EVERY action in the .blend that fits the armature, so clips from other imported models leak into each GLB; use `export_animation_mode='ACTIVE_ACTIONS'` with the right action active (exports one clip named `Animation`). (3) Some kits (e.g. Kenney furniture) ship ASCII FBX which Blender refuses — run `file *.fbx` first and fall back to the kit's OBJ folder. Skinned meshes respect entity Transform scale, so oversize rigs can be scaled at the entity.

## Composite gotchas

- Composite-authored box primitives need `"box": {"uvs": []}` in `core::MeshRenderer` — a bare `"box": {}` crashes `sdk-commands build` with `TypeError: message.uvs is not iterable`.
