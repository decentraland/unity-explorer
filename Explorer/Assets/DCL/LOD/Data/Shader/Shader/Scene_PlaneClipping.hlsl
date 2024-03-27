#ifndef SCENE_PLANE_CLIPPING_INCLUDED
#define SCENE_PLANE_CLIPPING_INCLUDED

static const float3 _PlaneX = float3(1.0, 0.0, 0.0);
static const float3 _PlaneY = float3(0.0, 0.0, 1.0);

void ClipFragmentViaPlaneTests(const float3 _positionWS, const float _PlaneClippingPosX, const float _PlaneClippingNegX, const float _PlaneClippingPosZ, const float _PlaneClippingNegZ)
{
    float distanceX = dot(_positionWS, _PlaneX);
    clip(distanceX - _PlaneClippingPosX);
    clip(-distanceX + _PlaneClippingNegX);

    float distanceZ = dot(_positionWS, _PlaneY);
    clip(distanceZ - _PlaneClippingPosZ);
    clip(-distanceZ + _PlaneClippingNegZ);
}

#endif