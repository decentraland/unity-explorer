#ifndef _DCL_BOUNDS_FUNCTIONS_
#define _DCL_BOUNDS_FUNCTIONS_

#include "MathematicsFunctions.cginc"

inline bool TestBoundingBox(float3 position, float3 boundsCentre, float3 boundsExtents)
{
    float3 Min = boundsCentre - boundsExtents;
    float3 Max = boundsCentre + boundsExtents;

    bool bIsInside =    position.x >= Min.x &&
                        position.x <= Max.x &&
                        position.y >= Min.y &&
                        position.y <= Max.y &&
                        position.z >= Min.z &&
                        position.z <= Max.z;

    return bIsInside;
}

inline bool TestBoundingSphere(float3 position, float3 boundsCentre, float radius)
{
    return distance(boundsCentre, position) <= radius;
}

inline bool TestBoundingCapsule(float3 position, float boundsCentre, float height, float radius, float3 axis, float4x4 transform)
{
    float pointChange = 0;
    if (height / 2 > radius)
    {
        pointChange = (height / 2) - radius;
        float4x4 scaled = SetScaleOfMatrix(transform, 1);

        // https://math.stackexchange.com/questions/1905533/find-perpendicular-distance-from-point-to-line-in-3d
        float3 A = position;
        float3 B = mul(scaled, float4(boundsCentre.x + axis.x * pointChange, boundsCentre.y + axis.y * pointChange, boundsCentre.z + axis.z * pointChange, 1.0)).xyz;
        float3 C = mul(scaled, float4(boundsCentre.x - axis.x * pointChange, boundsCentre.y - axis.y * pointChange, boundsCentre.z - axis.z * pointChange, 1.0)).xyz;

        float3 d = (C - B) / distance(C, B);
        float3 v = A - B;
        float t = dot(v, d);
        float3 P = B + t * d;
    
        float distPB = distance(P, B);
        float distPC = distance(P, C);
        float distBC = distance(B, C);

        if (abs(distBC - (distPB + distPC)) < 0.1)
            return distance(P, A) <= radius;
        else if (distPB < distPC)
            return distance(B, A) <= radius;
        else
            return distance(C, A) <= radius;
    }
    else
        return distance(boundsCentre + transform._14_24_34, position) <= radius;
}
#endif