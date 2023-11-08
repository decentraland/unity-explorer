#ifndef DCL_STARBOX_FRAGMENT_INCLUDED
#define DCL_STARBOX_FRAGMENT_INCLUDED

// Includes
#include "Assets/DCL/Rendering/SkyBox/DCL_SkyBox_Data.hlsl"
#include "UnityLightingCommon.cginc"
#include "Lighting.cginc"

#define PI 3.14159f

float3 LatlongToDirectionCoordinate(float2 coord)
{
    float theta = coord.y * PI;
    float phi = (coord.x * 2.f * PI - PI*0.5f);

    float cosTheta = cos(theta);
    float sinTheta = sqrt(1.0 - min(1.0, cosTheta*cosTheta));
    float cosPhi = cos(phi);
    float sinPhi = sin(phi);

    float3 direction = float3(sinTheta*cosPhi, cosTheta, sinTheta*sinPhi);
    direction.xy *= -1.0;
    return direction;
}

/////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////

float _starArraySRA0[1024];
float _starArraySDEC0[1024];

float4 st_frag(sk_v2f IN) : SV_Target
{
    half3 vOutputCol = half3(0.0, 0.0, 0.0);
    half3 vStarCol = half3(1.0, 1.0, 1.0);
    for (int i = 0; i < 1024; ++i)
    {
        float3 vLatLongCoord = LatlongToDirectionCoordinate(float2(_starArraySRA0[i], _starArraySDEC0[i]));

        float3 vEyeRay = normalize(IN.direction);
        float fLdotV =pow(saturate(dot(vLatLongCoord.xyz, vEyeRay)), 10000);
        if(fLdotV >= 0.99f)
        {
            vOutputCol += vStarCol.xyz;
        }
    }
    
    return half4(vOutputCol.rgb, 1.0);
}

#endif // DCL_STARBOX_FRAGMENT_INCLUDED