#ifndef _DCL_CAMERA_FUNCTIONS_
#define _DCL_CAMERA_FUNCTIONS_

#include "InputParams.cginc"

inline void EvaluateInstanceDistance(float4x4 instanceMatrix, out float dist, out float maxViewSize)
{
    dist = 0;
    maxViewSize = 0;
    
    float3 scale = float3(length(instanceMatrix._11_12_13), length(instanceMatrix._21_22_23), length(instanceMatrix._31_32_33));
    dist = abs(distance(instanceMatrix._14_24_34, vCameraPosition));
    maxViewSize = max(max(vBoundsExtents.x * scale.x, vBoundsExtents.y * scale.y), vBoundsExtents.z * scale.z) / (dist * fCameraHalfAngle * 2);
}

inline uint4 EvaluateLODLevel(uint nLODCount, float4x4 mLODScreenSpaceSizes, float fShadowDistance, float fScreenSpaceSize, float fDistance)
{
    uint4 output = uint4(8,8,0,8);
    for (uint i = 0; i < nLODCount; ++i)
    {
        //if (bIsVisible)
        {
            if (fScreenSpaceSize > mLODScreenSpaceSizes[i] && fScreenSpaceSize < mLODScreenSpaceSizes[i+8])
            {
                output[0] = i;
                if (i < nLODCount-1)
                {
                    if (fScreenSpaceSize > mLODScreenSpaceSizes[i+1] && fScreenSpaceSize < mLODScreenSpaceSizes[i+8+1])
                    {
                        output[1] = i+1;
                        output[2] = uint((fScreenSpaceSize - mLODScreenSpaceSizes[i+1]) / (mLODScreenSpaceSizes[i] - mLODScreenSpaceSizes[i+1]) * 255);
                    }
                }
                else
                {
                    float fPercentageOfFinalLOD = (fScreenSpaceSize / (mLODScreenSpaceSizes[i] - mLODScreenSpaceSizes[i+8]));
                    float fLastTenPercentDither = clamp((fPercentageOfFinalLOD - 0.9f), 0.0f, 0.1f) * 10.0f;
                    output[2] = uint(fLastTenPercentDither * 255);
                }
                //if (bIsShadowVisible)
                {
                    if (fDistance < fShadowDistance)
                        output[3] = i;
                }
                break;
            }
        }
    }
    return output;
}


#endif
