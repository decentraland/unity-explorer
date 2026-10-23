# Shader families

Source: project assets (material and prefab scan, no runtime content)  
Unity: 6000.5.9f1  
Captured: 2026-10-23 06:25 UTC  
Regenerate: menu `Decentraland/Bevy Migration/Shader Inventory`, or run a player build with `--shader-inventory[=<path>]` (BevyMigration/docs/README.md § 7, step 1).

## Families

| Family | Shaders | Materials | Renderers | Ported in step |
|---|---:|---:|---:|---|
| Unassigned | 52 | 94 | 13 | never |
| UrpLit | 12 | 104 | 20030 | 3 |
| Toon | 5 | 6 | 0 | 4 |
| Unlit | 16 | 18 | 72 | 3 |
| TmpSdf | 7 | 21 | 9 | 4 |
| Ui | 8 | 19 | 146 | 8 |
| Landscape | 26 | 74 | 5718 | 11 |
| Particles | 6 | 13 | 2 | 11 |
| Skybox | 2 | 4 | 0 | 11 |
| Internal | 18 | 23 | 0 | never |

## Shaders

| Shader | Family | Materials | Renderers |
|---|---|---:|---:|
| DCL/Scene | UrpLit | 43 | 19969 |
| Universal Render Pipeline/Nature/Stylized Grass | Landscape | 10 | 5475 |
| Ocean/Explorer (639281880446487650) | Landscape | 14 | 200 |
| Sprites/Default | Ui | 2 | 118 |
| Universal Render Pipeline/Lit | UrpLit | 44 | 60 |
| Universal Render Pipeline/Unlit | Unlit | 2 | 48 |
| UnityLibrary/Sprites/FlipBook (Cutout) | Ui | 1 | 24 |
| DCL/Universal Render Pipeline/Unlit | Unlit | 1 | 22 |
| Shader Graphs/Rock | Landscape | 5 | 12 |
| DCL/Tree | Landscape | 6 | 9 |
| DCL/TreeImpostor | Landscape | 6 | 9 |
| Shader Graphs/Rock_GPUIPro | Landscape | 4 | 9 |
| TextMeshPro/Distance Field SSD | TmpSdf | 10 | 9 |
| DCL/GhostHologram | Unassigned | 1 | 2 |
| Shader Graphs/Cliff | Landscape | 2 | 2 |
| Shader Graphs/SatelliteView | Unassigned | 3 | 2 |
| Sprites/Mask | Ui | 1 | 2 |
| DCL/Lobby/StageBackdrop | Unassigned | 1 | 1 |
| DCL/Lobby/StageBlobShadow | Unassigned | 1 | 1 |
| DCL/Lobby/StageDust | Unassigned | 1 | 1 |
| DCL/Lobby/StageFloor | Unassigned | 1 | 1 |
| DCL/Lobby/StageMist | Unassigned | 1 | 1 |
| DCL/Lobby/StageVignette | Unassigned | 1 | 1 |
| DCL/Sprites/CircleAlwaysOnTop | Ui | 1 | 1 |
| DCL/Sprites/Default | Ui | 1 | 1 |
| DCL/Unlit | Unlit | 2 | 1 |
| Particles/Standard Unlit | Particles | 1 | 1 |
| Shader Graphs/Badge | Unassigned | 2 | 1 |
| Shader Graphs/Foliage | Landscape | 2 | 1 |
| Shader Graphs/FresnelShader | Unassigned | 2 | 1 |
| Shader Graphs/glTF-pbrMetallicRoughness | Unassigned | 6 | 1 |
| Universal Render Pipeline/2D/Sprite-Unlit-Default | Unlit | 1 | 1 |
| Universal Render Pipeline/Particles/Unlit | Particles | 2 | 1 |
| Universal Render Pipeline/Simple Lit | UrpLit | 4 | 1 |
| Unlit/MapOcean | Landscape | 1 | 1 |
| 2D/RenderAs2D-Flattening | Unassigned | 1 | 0 |
| Custom/AnimatedBackgroundMovingTexture | Unassigned | 3 | 0 |
| Custom/LavaFlow (SoftMaskable) | Ui | 6 | 0 |
| DCL/ChatReactions/EmojiInstancedUnlit_URP | Unlit | 1 | 0 |
| DCL/DCL_Avatar_Facial_Features | Toon | 1 | 0 |
| DCL/DCL_Toon | Toon | 2 | 0 |
| DCL/HighlightInput_Blur | Unassigned | 1 | 0 |
| DCL/HighlightInput_Override | Unassigned | 1 | 0 |
| DCL/HighlightOutput | Unassigned | 1 | 0 |
| DCL/Jump Indicator | Unassigned | 3 | 0 |
| DCL/MountainLit | UrpLit | 1 | 0 |
| DCL/Universal Render Pipeline/Lit | UrpLit | 5 | 0 |
| Flipping/FlipperLinearToGammaShader | Unassigned | 1 | 0 |
| GPUInstancerPro/Billboard/BillboardHDRP_GPUIPro | Landscape | 1 | 0 |
| GPUInstancerPro/Billboard/BillboardURP_GPUIPro | Landscape | 1 | 0 |
| GPUInstancerPro/Foliage_SG | Landscape | 2 | 0 |
| GUI/Text Shader | Unassigned | 24 | 0 |
| Hidden/Decal/0_Decal Simple | Internal | 1 | 0 |
| Hidden/Decal/1_Decal Material Volume | Internal | 1 | 0 |
| Hidden/InternalErrorShader | Internal | 1 | 0 |
| Hidden/Lit/0_Lit Basic | UrpLit | 1 | 0 |
| Hidden/Lit/1_Lit Full | UrpLit | 1 | 0 |
| Hidden/Shader Graphs/0_2D Sprite Lit | UrpLit | 1 | 0 |
| Hidden/Shader Graphs/1_2D Sprite Unlit | Unlit | 1 | 0 |
| Hidden/Shader Graphs/2_2D Sprite Custom Lit | UrpLit | 1 | 0 |
| Hidden/Shader Graphs/BuiltIn Lit Basic | Internal | 1 | 0 |
| Hidden/Shader Graphs/BuiltIn Unlit Basic | Unlit | 1 | 0 |
| Hidden/Shader Graphs/Custom Render Texture | Internal | 1 | 0 |
| Hidden/Shader Graphs/Fullscreen Basic URP | Internal | 1 | 0 |
| Hidden/Shader Graphs/Particles/0_Particle Unlit | Particles | 1 | 0 |
| Hidden/Shader Graphs/Particles/2_Particle Lit | Particles | 1 | 0 |
| Hidden/Shader Graphs/UITKBasicTemplate | Internal | 1 | 0 |
| Hidden/Shader Graphs/UITKFullTemplate | Internal | 1 | 0 |
| Hidden/SurfaceCache/Fallback | Internal | 1 | 0 |
| Hidden/Terrain/Terrain Simple | Landscape | 1 | 0 |
| Hidden/Terrain/Terrain Standard 4 Layers URP | Landscape | 1 | 0 |
| Hidden/TextCore/Distance Field SSD | Internal | 6 | 0 |
| Hidden/UGUI Canvas/UGUI Canvas Basic | Internal | 1 | 0 |
| Hidden/UGUI Canvas/UGUI Canvas Meter | Internal | 1 | 0 |
| Hidden/Unlit/Unlit Simple | Unlit | 1 | 0 |
| Hidden/VFX Graph/0_VFXGraph Unlit | Unlit | 1 | 0 |
| Hidden/VFX Graph/1_VFXGraph Lit | UrpLit | 1 | 0 |
| Hidden/VFX Graph/2_VFXGraph SixWay Lit | UrpLit | 1 | 0 |
| Hidden/VFX/AvatarAirJumpVfx/Simple Burst/Output Particle URP Lit Mesh | Toon | 1 | 0 |
| Hidden/VFX/AvatarFootstep/Simple Burst/Output Particle URP Lit Mesh | Toon | 1 | 0 |
| Hidden/VFX/AvatarFootstepVfx/Simple Burst/Output Particle URP Lit Mesh | Toon | 1 | 0 |
| Hidden/VFX/Firework/Burst/Burst Quad | Internal | 1 | 0 |
| Hidden/VFX/Fountain/System (1)/A/WaterFlatRipples | Landscape | 1 | 0 |
| Hidden/VFX/Glider_vfx/Simple Loop (1)/Output Particle Shader Graph Mesh - Unlit | Particles | 1 | 0 |
| Hidden/VFX/Head_Trail/Heads/Heads Quad | Internal | 1 | 0 |
| Hidden/VFX/Minimal_System/Minimal/Render Quad | Internal | 1 | 0 |
| Hidden/VFX/Simple_Burst/Simple Burst/Render Quad | Internal | 1 | 0 |
| Hidden/VFX/Simple_Loop/Simple Loop/Render Quad | Internal | 1 | 0 |
| Hidden/VFX/Simple_Trail/Ribbon/Render Strip | Internal | 1 | 0 |
| NoiseTest/HLSL/NoiseTest | Unassigned | 1 | 0 |
| Ocean/Explorer (639281880446487650) (Tessellation) | Landscape | 3 | 0 |
| S_LoadingSpinner (SoftMaskable) | Ui | 5 | 0 |
| Shader Graphs/ArnoldStandardSurface | Unassigned | 1 | 0 |
| Shader Graphs/ArnoldStandardSurfaceTransparent | Unassigned | 1 | 0 |
| Shader Graphs/Decal | Unassigned | 2 | 0 |
| Shader Graphs/Flowers | Landscape | 2 | 0 |
| Shader Graphs/FountainWater | Landscape | 1 | 0 |
| Shader Graphs/FullscreenInvertColors | Unassigned | 2 | 0 |
| Shader Graphs/GenesisSky | Skybox | 2 | 0 |
| Shader Graphs/GenesisSkyBoxFullScreen | Skybox | 2 | 0 |
| Shader Graphs/HologramShader | Unassigned | 1 | 0 |
| Shader Graphs/PhysicalMaterial3DsMax | Unassigned | 1 | 0 |
| Shader Graphs/PhysicalMaterial3DsMaxTransparent | Unassigned | 1 | 0 |
| Shader Graphs/RadialWave | Unassigned | 4 | 0 |
| Shader Graphs/SG_FinalGhost | Unassigned | 2 | 0 |
| Shader Graphs/SimpleOcean | Landscape | 1 | 0 |
| Shader Graphs/SlashShader | Unassigned | 1 | 0 |
| Shader Graphs/Terrain | Landscape | 2 | 0 |
| Shader Graphs/Tree Bark | Landscape | 1 | 0 |
| Shader Graphs/VFXDefaultSingleMesh | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Blend | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Blend-double | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Opaque | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Opaque-double | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Premultiply | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrMetallicRoughness-Premultiply-double | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrSpecularGlossiness | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrSpecularGlossiness-Blend | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrSpecularGlossiness-Blend-double | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrSpecularGlossiness-Opaque | Unassigned | 1 | 0 |
| Shader Graphs/glTF-pbrSpecularGlossiness-Opaque-double | Unassigned | 1 | 0 |
| Shader Graphs/glTF-unlit | Unlit | 1 | 0 |
| Shader Graphs/glTF-unlit-Blend | Unlit | 1 | 0 |
| Shader Graphs/glTF-unlit-Blend-double | Unlit | 1 | 0 |
| Shader Graphs/glTF-unlit-Opaque | Unlit | 1 | 0 |
| Shader Graphs/glTF-unlit-Opaque-double | Unlit | 1 | 0 |
| Stylized Water 2/Cutout | Landscape | 1 | 0 |
| TextMeshPro/Distance Field | TmpSdf | 3 | 0 |
| TextMeshPro/SRP/TMP_SDF-HDRP LIT | TmpSdf | 1 | 0 |
| TextMeshPro/SRP/TMP_SDF-HDRP UNLIT | TmpSdf | 1 | 0 |
| TextMeshPro/SRP/TMP_SDF-URP Lit | TmpSdf | 1 | 0 |
| TextMeshPro/SRP/TMP_SDF-URP Unlit | TmpSdf | 1 | 0 |
| TextMeshPro/Sprite | TmpSdf | 4 | 0 |
| UI/Default | Ui | 2 | 0 |
| Universal Render Pipeline/2D/Mesh2D-Lit-Default | Unassigned | 1 | 0 |
| Universal Render Pipeline/2D/Mesh2D-Unlit-Default | Unlit | 1 | 0 |
| Universal Render Pipeline/2D/Sprite-Lit-Default | Unassigned | 1 | 0 |
| Universal Render Pipeline/2D/Sprite-Mask | Unassigned | 1 | 0 |
| Universal Render Pipeline/Autodesk Interactive/AutodeskInteractive | Unassigned | 1 | 0 |
| Universal Render Pipeline/Autodesk Interactive/AutodeskInteractiveMasked | Unassigned | 1 | 0 |
| Universal Render Pipeline/Autodesk Interactive/AutodeskInteractiveTransparent | Unassigned | 1 | 0 |
| Universal Render Pipeline/Nature/SpeedTree8_PBRLit | Landscape | 2 | 0 |
| Universal Render Pipeline/Nature/SpeedTree8_PBRLit_GPUIPro | Landscape | 1 | 0 |
| Universal Render Pipeline/Nature/SpeedTree9_URP | Landscape | 1 | 0 |
| Universal Render Pipeline/Particles/Simple Lit | Particles | 7 | 0 |
| Universal Render Pipeline/Terrain/Lit | Landscape | 2 | 0 |
| Universal Render Pipeline/VR/SpatialMapping/Occlusion | Unassigned | 1 | 0 |
| Universal Render Pipeline/VR/SpatialMapping/Wireframe | Unassigned | 1 | 0 |
| VFX Graph/VFXDefault | Unassigned | 1 | 0 |
| VFX Graph/VFXErrorFallback | Unassigned | 1 | 0 |
| VFX Graph/VFXSpriteLit | UrpLit | 1 | 0 |
| VFX Graph/VFXSpriteUnlit | Unlit | 1 | 0 |
