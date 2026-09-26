#ifndef AVATAR_SURFACE_DATA_INCLUDED
#define AVATAR_SURFACE_DATA_INCLUDED

struct SurfaceData_Avatar
{
    half3 albedo;
    half3 specular;
    half  metallic;
    half  smoothness;
    half3 normalTS;
    half3 emission;
    half  occlusion;
    half  alpha;
    half specularRampInnerMin;
    half specularRampInnerMax;
    half specularRampOuterMin;
    half specularRampOuterMax;
};

#endif