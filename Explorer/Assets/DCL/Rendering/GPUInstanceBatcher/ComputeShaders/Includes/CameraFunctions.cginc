#ifndef _DCL_CAMERA_FUNCTIONS_
#define _DCL_CAMERA_FUNCTIONS_

inline void EvaluateInstanceDistance(float4x4 instanceMatrix, out float dist, out float maxViewSize, float3 vBoundsExtents, float3 vCameraPosition, float fCameraHalfAngle)
{
    float3 scale = float3(length(instanceMatrix._11_12_13), length(instanceMatrix._21_22_23), length(instanceMatrix._31_32_33));
    dist = abs(distance(instanceMatrix._14_24_34, vCameraPosition));
    maxViewSize = max(max(vBoundsExtents.x * scale.x, vBoundsExtents.y * scale.y), vBoundsExtents.z * scale.z) / (dist * fCameraHalfAngle * 2);
}

uint4 EvaluateLODLevel(uint nLODCount, float4x4 mLODScreenSpaceSizes, float fShadowDistance, float fScreenSpaceSize, float fDistance)
{
    uint4 output = uint4(8,8,0,8);
    for (uint i = 0; i < nLODCount; ++i)
    {
        if (fScreenSpaceSize >= 1.0f)
        {
            output[0] = i;
            break;
        }
        
        const float fLODInnerSize_A = mLODScreenSpaceSizes[i / 4][i % 4];
        const float fLODOuterSize_A = mLODScreenSpaceSizes[(i+8) / 4][(i+8) % 4];
        if ((fScreenSpaceSize <= fLODInnerSize_A) && (fScreenSpaceSize >= fLODOuterSize_A))
        {
            output[0] = i;
            if (i < nLODCount-1)
            {
                const float fLODInnerSize_B = mLODScreenSpaceSizes[(i+1) / 4][(i+1) % 4];
                const float fLODOuterSize_B = mLODScreenSpaceSizes[(i+8+1) / 4][(i+8+1) % 4];
                if ((fScreenSpaceSize <= fLODInnerSize_B) && (fScreenSpaceSize >= fLODOuterSize_B))
                {
                    output[1] = i+1;
                    output[2] = uint((fScreenSpaceSize - fLODInnerSize_B) / (fLODOuterSize_A - fLODInnerSize_B) * 255);
                }
            }
            // else
            // {
            //     const float fPercentageOfFinalLOD = (fScreenSpaceSize / (fLODInnerSize_A - fLODOuterSize_A));
            //     const float fLastTenPercentDither = clamp((fPercentageOfFinalLOD - 0.9f), 0.0f, 0.1f) * 10.0f;
            //     output[2] = uint(fLastTenPercentDither * 255);
            // }
        
            if (fDistance < fShadowDistance)
                output[3] = i;
            
            break;
        }
    }
    return output;
}

#endif