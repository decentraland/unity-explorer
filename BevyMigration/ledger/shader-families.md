# Shader families

Source: play session (runtime renderers)  
Unity: 6000.5.9f1  
Captured: 2026-10-23 06:53 UTC  
Regenerate: menu `Decentraland/Bevy Migration/Shader Inventory`, or run a player build with `--shader-inventory[=<path>]` (BevyMigration/docs/README.md § 7, step 1).

## Families

| Family | Shaders | Materials | Renderers | Ported in step |
|---|---:|---:|---:|---|
| Unassigned | 5 | 20 | 124 | never |
| UrpLit | 2 | 11467 | 49579 | 3 |
| Toon | 3 | 244 | 281 | 4 |
| Unlit | 1 | 209 | 217 | 3 |
| TmpSdf | 1 | 43 | 92 | 4 |
| Ui | 2 | 67 | 335 | 8 |
| Landscape | 9 | 13 | 9673 | 11 |
| Particles | 3 | 55 | 109 | 11 |
| Internal | 1 | 1 | 19 | never |

## Shaders

| Shader | Family | Materials | Renderers |
|---|---|---:|---:|
| DCL/Scene | UrpLit | 11466 | 49578 |
| Universal Render Pipeline/Nature/Stylized Grass | Landscape | 4 | 9392 |
| DCL/DCL_Toon | Toon | 207 | 236 |
| DCL/Unlit | Unlit | 209 | 217 |
| Sprites/Default | Ui | 2 | 206 |
| DCL/Sprites/Default | Ui | 65 | 129 |
| TextMeshPro/Distance Field SSD | TmpSdf | 43 | 92 |
| Universal Render Pipeline/Particles/Unlit | Particles | 53 | 71 |
| Shader Graphs/SatelliteView | Unassigned | 1 | 64 |
| DCL/Tree | Landscape | 2 | 40 |
| DCL/TreeImpostor | Landscape | 1 | 40 |
| Hidden/VFX/Fountain/System (1)/A/WaterFlatRipples | Landscape | 1 | 40 |
| Hidden/VFX/Fountain/System (1)/B/WaterFlatBase | Landscape | 1 | 40 |
| Hidden/VFX/Fountain/System (2)/WaterFlat | Landscape | 1 | 40 |
| Hidden/VFX/Fountain/System/A/WaterPipeInside | Landscape | 1 | 40 |
| Hidden/VFX/Fountain/System/B/WaterPipeOutside | Landscape | 1 | 40 |
| DCL/DCL_Avatar_Facial_Features | Toon | 36 | 39 |
| DCL/GhostHologram | Unassigned | 16 | 21 |
| Hidden/VFX/Glider_vfx/Simple Loop (1)/Output Particle Shader Graph Mesh - Unlit | Particles | 1 | 19 |
| Hidden/VFX/Glider_vfx/Simple Loop/Render Quad | Internal | 1 | 19 |
| Particles/Standard Unlit | Particles | 1 | 19 |
| Shader Graphs/FresnelShader | Unassigned | 1 | 19 |
| Shader Graphs/glTF-pbrMetallicRoughness | Unassigned | 1 | 19 |
| Hidden/VFX/AvatarFootstepVfx/Simple Burst/Output Particle URP Lit Mesh | Toon | 1 | 6 |
| Shader Graphs/Badge | Unassigned | 1 | 1 |
| Universal Render Pipeline/Lit | UrpLit | 1 | 1 |
| Unlit/MapOcean | Landscape | 1 | 1 |
