#ifndef SCENE_PLANE_CLIPPING_INCLUDED
#define SCENE_PLANE_CLIPPING_INCLUDED

static const float3 _PlaneX = float3(1.0, 0.0, 0.0);
static const float3 _PlaneY = float3(0.0, 1.0, 0.0);
static const float3 _PlaneZ = float3(0.0, 0.0, 1.0);

void ClipFragmentViaPlaneTests(const float3 _positionWS, const float _PlaneClippingPosX, const float _PlaneClippingNegX, const float _PlaneClippingPosZ, const float _PlaneClippingNegZ, const float _PlaneClippingPosY, const float _PlaneClippingNegY)
{
    // Horizontal clipping planes
    float distanceX = dot(_positionWS, _PlaneX);
    if ((distanceX - _PlaneClippingPosX) <= 0.0)
        clip(-1);
    if ((-distanceX + _PlaneClippingNegX) <= 0.0)
        clip(-1);
    float distanceZ = dot(_positionWS, _PlaneZ);
    if ((distanceZ - _PlaneClippingPosZ) <= 0.0)
        clip(-1);
    if ((-distanceZ + _PlaneClippingNegZ) <= 0.0)
        clip(-1);

    // Vertical clipping planes
    float distanceY = dot(_positionWS, _PlaneY);
    if ((distanceY - _PlaneClippingPosY) <= 0.0)
        clip(-1);
    if ((-distanceY + _PlaneClippingNegY) <= 0.0)
        clip(-1);
}

#endif