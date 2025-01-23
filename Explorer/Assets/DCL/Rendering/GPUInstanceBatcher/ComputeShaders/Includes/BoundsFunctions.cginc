#ifndef _DCL_BOUNDS_FUNCTIONS_
#define _DCL_BOUNDS_FUNCTIONS_

// Bounding Box
// uniform float3 boundsCenter;
// uniform float3 boundsExtents;

// Bounding Sphere

// Bounding Capsule (Avatar)
// uniform float4x4 modifierTransform;
// uniform float modifierRadius;
// uniform float modifierHeight;
// uniform float3 modifierAxis;

#include "MathematicsFunctions.cginc"

inline bool TestBoundingBox(float3 position, float3 boundsCentre, float3 boundsExtents)
{
    float3 Min = boundsCenter - boundsExtents;
    float3 Max = boundsCenter + boundsExtents;

    bool bIsInside =    position.x >= Min.x &&
                        position.x <= Max.x &&
                        position.y >= Min.y &&
                        position.y <= Max.y &&
                        position.z >= Min.z &&
                        position.z <= Max.z;

    return bIsInside;
}

inline bool TestBoundingSphere(float3 position, float radius)
{
    return distance(boundsCenter, position) <= radius;
}

inline bool TestBoundingCapsule(float3 position, float boundsCentre, float height, float radius)
{
    float pointChange = 0;
    if (modifierHeight / 2 > modifierRadius)
    {
        pointChange = (modifierHeight / 2) - modifierRadius;
        float4x4 scaled = SetScaleOfMatrix(modifierTransform, 1);

        // https://math.stackexchange.com/questions/1905533/find-perpendicular-distance-from-point-to-line-in-3d
        float3 A = position;
        float3 B = mul(scaled, float4(boundsCenter.x + modifierAxis.x * pointChange, boundsCenter.y + modifierAxis.y * pointChange, boundsCenter.z + modifierAxis.z * pointChange, 1.0)).xyz;
        float3 C = mul(scaled, float4(boundsCenter.x - modifierAxis.x * pointChange, boundsCenter.y - modifierAxis.y * pointChange, boundsCenter.z - modifierAxis.z * pointChange, 1.0)).xyz;

        float3 d = (C - B) / distance(C, B);
        float3 v = A - B;
        float t = dot(v, d);
        float3 P = B + t * d;
    
        float distPB = distance(P, B);
        float distPC = distance(P, C);
        float distBC = distance(B, C);

        if (abs(distBC - (distPB + distPC)) < 0.1)
            return distance(P, A) <= modifierRadius;
        else if (distPB < distPC)
            return distance(B, A) <= modifierRadius;
        else
            return distance(C, A) <= modifierRadius;
    }
    else
        return distance(boundsCenter + modifierTransform._14_24_34, position) <= modifierRadius;
}
#endif