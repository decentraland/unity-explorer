#ifndef SCENE_SURFACE_DATA_INCLUDED
#define SCENE_SURFACE_DATA_INCLUDED

// Must match Universal ShaderGraph master node
struct SurfaceData_Scene
{
    half3 albedo;
    half  metallic;
    half  smoothness;
    half3 normalTS;
    half3 emission;
    half  occlusion;
    half  alpha;
};

#endif
