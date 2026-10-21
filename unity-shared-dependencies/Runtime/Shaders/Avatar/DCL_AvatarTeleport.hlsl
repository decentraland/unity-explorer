#ifndef DCL_AVATAR_TELEPORT_INCLUDED
#define DCL_AVATAR_TELEPORT_INCLUDED

// Global state selects only the local avatar's current GPU buffer allocation.
int _DCLTeleportAvatar;
float4 _DCLTeleportEffect; // Charge, dissolve, elapsed seconds, reserved.

bool DCLTeleportActive()
{
    #ifdef _DCL_COMPUTE_SKINNING
        return _DCLTeleportEffect.x > 0.0 && _lastAvatarVertCount == _DCLTeleportAvatar;
    #else
        return false;
    #endif
}

float DCLTeleportNoise(float2 uv)
{
    float2 cell = floor(uv * 100.0);
    return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
}

void DCLTeleportClip(float2 uv)
{
    if (DCLTeleportActive())
        clip(DCLTeleportNoise(uv) - _DCLTeleportEffect.y * 1.01);
}

half3 DCLTeleportColor(half3 color, float3 positionWS, float3 normalWS, float2 uv)
{
    if (!DCLTeleportActive()) return color;

    float time = _DCLTeleportEffect.z;
    float3 positionOS = TransformWorldToObject(positionWS);
    float2 grid = float2(atan2(positionOS.z, positionOS.x) * 3.0, positionOS.y * 9.0 - time * 1.8);
    float2 cell = frac(grid);
    float vertical = 1.0 - smoothstep(0.025, 0.075, abs(cell.x - 0.5));
    float horizontal = 1.0 - smoothstep(0.025, 0.075, abs(cell.y - 0.5));
    float gate = step(0.35, frac(sin(dot(floor(grid), float2(41.3, 17.1))) * 1437.4));
    float circuitry = max(vertical * step(cell.y, 0.7), horizontal * gate);
    float scan = pow(saturate(sin(positionOS.y * 32.0 - time * 13.0)), 12.0);
    float rim = pow(1.0 - saturate(abs(dot(normalize(normalWS), GetWorldSpaceNormalizeViewDir(positionWS)))), 2.0);
    float edge = (1.0 - smoothstep(0.0, 0.08, DCLTeleportNoise(uv) - _DCLTeleportEffect.y * 1.01))
        * step(0.01, _DCLTeleportEffect.y);
    half3 energy = half3(0.015, 0.3, 0.85) + half3(0.12, 1.5, 2.2) * (circuitry + scan * 0.35 + rim + edge * 2.0);
    return lerp(color, energy, _DCLTeleportEffect.x);
}

#endif
