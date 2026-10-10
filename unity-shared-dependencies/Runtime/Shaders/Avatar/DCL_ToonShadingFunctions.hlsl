#include "DCL_ToonBody.hlsl"

float3 CalculateBaseColour(float3 _vBaseColour, float3 _vTextureColour, float3 _vLightColour, float _fIsLightColourBase, float _fLightIntensity = 1.0f)
{
    return lerp( (_vBaseColour.rgb*_vTextureColour.rgb*_fLightIntensity), ((_vBaseColour.rgb*_vTextureColour.rgb)*_vLightColour), _fIsLightColourBase );
}

float3 Calculate1stShadeColour(float4 _vShadeMapTexColour, float4 _vBaseColour, float4 _vMainTextureColour, float4 _v1st_ShadeColor, float3 _vLightColour, float _fUse_BaseAs1st, float _fIs_LightColor_1st_Shade)
{
    float4 _1st_ShadeMap_var = lerp(_vShadeMapTexColour,float4(_vBaseColour.rgb, _vMainTextureColour.a),_fUse_BaseAs1st);
    float3 Set_1st_ShadeColor = lerp( (_v1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb), ((_v1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*_vLightColour), _fIs_LightColor_1st_Shade );
    return Set_1st_ShadeColor;
}

float3 Calculate2ndShadeColour(float4 _vShadeMapTexColour, float4 _v1st_ShadeMap_var, float4 _vMainTextureColour, float4 _v2nd_ShadeColor, float3 _vLightColour, float _fUse_1stAs2nd, float _fIs_LightColor_2nd_Shade)
{
    float4 _2nd_ShadeMap_var = lerp(_vShadeMapTexColour, _v1st_ShadeMap_var, _fUse_1stAs2nd);
    float3 Set_2nd_ShadeColor = lerp( (_v2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb), ((_v2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb) * _vLightColour), _fIs_LightColor_2nd_Shade );
    return Set_2nd_ShadeColor;
}

// float3 Calculate1stShadePositionColour()
// {
//     //int nSet_1st_ShadePositionArrID = _Set_1st_ShadePositionArr_ID;
//     float4 _Set_1st_ShadePosition_var = SAMPLE_SET_1ST_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_1st_ShadePosition),nSet_1st_ShadePositionArrID);
//     return _Set_1st_ShadePosition_var;
// }
//
// float3 Calculate2ndShadePositionColour()
// {
//     //int nSet_2nd_ShadePositionArrID = _Set_2nd_ShadePositionArr_ID;
//     float4 _Set_2nd_ShadePosition_var = SAMPLE_SET_2ND_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_2nd_ShadePosition),nSet_2nd_ShadePositionArrID);
//     return _Set_2nd_ShadePosition_var;
// }

float3 CalculateRimLightColour( float4 _vRimLightMask,
                                float3 _RimLightColor,
                                float3 _vLightColour,
                                float3 _vSurfaceNormal,
                                float3 _vTextureNormal,
                                float3 _vViewDirection,
                                float3 _vLightDirection,
                                float3 _vHighlightColor,
                                float3 _vAp_RimLightColor,
                                float _fIs_LightColor_RimLight,
                                float _fIs_NormalMapToRimLight,
                                float _fRimLight_Power,
                                float _fRimLight_InsideMask,
                                float _fRimLight_FeatherOff,
                                float _fTweak_LightDirection_MaskLevel,
                                float _fLightDirection_MaskOn,
                                float _fAp_RimLight_Power,
                                float _fAp_RimLight_FeatherOff,
                                float _fTweak_RimLightMaskLevel,
                                float _fAdd_Antipodean_RimLight,
                                float _fIs_LightColor_Ap_RimLight,
                                float _fRimLight)
{
    float3 _Is_LightColor_RimLight_var = lerp( _RimLightColor.rgb, (_RimLightColor.rgb*_vLightColour), _fIs_LightColor_RimLight );
    float _RimArea_var = abs(1.0 - dot(lerp( _vSurfaceNormal, _vTextureNormal, _fIs_NormalMapToRimLight ), _vViewDirection));
    float _RimLightPower_var = pow(_RimArea_var,exp2(lerp(3,0,_fRimLight_Power)));
    float _Rimlight_InsideMask_var = saturate(lerp( (0.0 + ( (_RimLightPower_var - _fRimLight_InsideMask) * (1.0 - 0.0) ) / (1.0 - _fRimLight_InsideMask)), step(_fRimLight_InsideMask,_RimLightPower_var), _fRimLight_FeatherOff ));
    float _VertHalfLambert_var = 0.5*dot(_vSurfaceNormal,_vLightDirection)+0.5;
    float3 _LightDirection_MaskOn_var = lerp( (_Is_LightColor_RimLight_var*_Rimlight_InsideMask_var), (_Is_LightColor_RimLight_var*saturate((_Rimlight_InsideMask_var-((1.0 - _VertHalfLambert_var)+_fTweak_LightDirection_MaskLevel)))), _fLightDirection_MaskOn );
    float _ApRimLightPower_var = pow(_RimArea_var,exp2(lerp(3,0,_fAp_RimLight_Power)));
    float3 Set_RimLight = (saturate((_vRimLightMask.g+_fTweak_RimLightMaskLevel))*lerp( _LightDirection_MaskOn_var, (_LightDirection_MaskOn_var+(lerp( _vAp_RimLightColor.rgb, (_vAp_RimLightColor.rgb*_vLightColour), _fIs_LightColor_Ap_RimLight )*saturate((lerp( (0.0 + ( (_ApRimLightPower_var - _fRimLight_InsideMask) * (1.0 - 0.0) ) / (1.0 - _fRimLight_InsideMask)), step(_fRimLight_InsideMask,_ApRimLightPower_var), _fAp_RimLight_FeatherOff )-(saturate(_VertHalfLambert_var)+_fTweak_LightDirection_MaskLevel))))), _fAdd_Antipodean_RimLight ));
    //Composition: HighColor and RimLight as _RimLight_var
    float3 _RimLight_var = lerp( _vHighlightColor, (_vHighlightColor+Set_RimLight), _fRimLight );
    return _RimLight_var;
}

float2 CalculateMapCapUV(float2 _UV0,
                        float _fRotate_NormalMapForMatCapUV)
{
    float2 _Rot_MatCapNmUV_var = RotateUV(_UV0, (_fRotate_NormalMapForMatCapUV*3.141592654), float2(0.5, 0.5), 1.0);
    return _Rot_MatCapNmUV_var;
}

float2 CalculateMapCapNormalUV( float4x4 _mUNITY_MATRIX_V,
                                float3 _vNormalMapForMatCap_var,
                                float3 _vViewDirection,
                                float3 _vNormalDirection,
                                half _fIs_NormalMapForMatCap,
                                float3x3 _mTangentTransform,
                                half _fIs_Ortho,
                                float _fTweak_MatCapUV,
                                float _fRotate_MatCapUV,
                                float _fMirrorFlag,
                                float _fCameraRolling_Stabilizer)
{
    // CameraRolling Stabilizer
    float3 _Camera_Right = _mUNITY_MATRIX_V[0].xyz;
    float3 _Camera_Front = _mUNITY_MATRIX_V[2].xyz;
    float3 _Up_Unit = float3(0, 1, 0);
    float3 _Right_Axis = cross(_Camera_Front, _Up_Unit);
    // Invert if it's "inside the mirror".
    half _sign_Mirror = _fMirrorFlag; // Mirror Script Determination: if sign_Mirror = -1, determine "Inside the mirror".
    if(_sign_Mirror < 0)
    {
        _Right_Axis = -1 * _Right_Axis;
        _fRotate_MatCapUV = -1 * _fRotate_MatCapUV;
    }
    else
    {
        _Right_Axis = _Right_Axis;
    }
    float _Camera_Right_Magnitude = sqrt(_Camera_Right.x*_Camera_Right.x + _Camera_Right.y*_Camera_Right.y + _Camera_Right.z*_Camera_Right.z);
    float _Right_Axis_Magnitude = sqrt(_Right_Axis.x*_Right_Axis.x + _Right_Axis.y*_Right_Axis.y + _Right_Axis.z*_Right_Axis.z);
    float _Camera_Roll_Cos = dot(_Right_Axis, _Camera_Right) / (_Right_Axis_Magnitude * _Camera_Right_Magnitude);
    float _Camera_Roll = acos(clamp(_Camera_Roll_Cos, -1, 1));
    half _Camera_Dir = _Camera_Right.y < 0 ? -1 : 1;
    float _Rot_MatCapUV_var_ang = (_fRotate_MatCapUV*3.141592654) - _Camera_Dir*_Camera_Roll*_fCameraRolling_Stabilizer;

    // MatCap with camera skew correction
    float3 viewNormal = (mul(_mUNITY_MATRIX_V, float4(lerp( _vNormalDirection, mul( _vNormalMapForMatCap_var.rgb, _mTangentTransform ).rgb, _fIs_NormalMapForMatCap ),0))).rgb;
    float3 NormalBlend_MatcapUV_Detail = viewNormal.rgb * float3(-1,-1,1);
    float3 NormalBlend_MatcapUV_Base = (mul( _mUNITY_MATRIX_V, float4(_vViewDirection.xyz,0) ).rgb*float3(-1,-1,1)) + float3(0,0,1);
    float3 noSknewViewNormal = NormalBlend_MatcapUV_Base*dot(NormalBlend_MatcapUV_Base, NormalBlend_MatcapUV_Detail)/NormalBlend_MatcapUV_Base.b - NormalBlend_MatcapUV_Detail;                
    float2 _ViewNormalAsMatCapUV = (lerp(noSknewViewNormal,viewNormal,_fIs_Ortho).rg*0.5)+0.5;
    float2 _Rot_MatCapUV_var = RotateUV((0.0 + ((_ViewNormalAsMatCapUV - (0.0+_fTweak_MatCapUV)) * (1.0 - 0.0) ) / ((1.0-_fTweak_MatCapUV) - (0.0+_fTweak_MatCapUV))), _Rot_MatCapUV_var_ang, float2(0.5, 0.5), 1.0);

    // If it is "inside the mirror", flip the UV left and right.
    if(_sign_Mirror < 0)
    {
        _Rot_MatCapUV_var.x = 1-_Rot_MatCapUV_var.x;
    }
    else
    {
        _Rot_MatCapUV_var = _Rot_MatCapUV_var;
    }

    return _Rot_MatCapUV_var;
}

float3 CalculateMatCapColour(   float4 _vMatCap_Sampler_var,
                                float4 _vSet_MatcapMask_var,
                                float _fInverse_MatcapMask,
                                float _fTweak_MatcapMaskLevel,
                                float4 _MatCapColor,
                                float3 Set_LightColor,
                                float _fIs_LightColor_MatCap,
                                float _fSet_FinalShadowMask,
                                float _fTweakMatCapOnShadow,
                                float3 _vSet_HighColor,
                                half _fIs_BlendAddToMatCap,
                                half _fIs_UseTweakMatCapOnShadow,
                                float3 _vRimLight_var,
                                float3 _vSet_RimLight,
                                half _fRimLight,
                                half _fMatCap)
{
    //int nNormalMapForMatCapArrID = _NormalMapForMatCapArr_ID;
    //float3 _NormalMapForMatCap_var = UnpackNormalScale(SAMPLE_NORMALMAPFORMATCAP(TRANSFORM_TEX(_Rot_MatCapNmUV_var, _NormalMapForMatCap), nNormalMapForMatCapArrID), _BumpScaleMatcap);
    
    // int nMatCap_SamplerArrID = _MatCap_SamplerArr_ID;
    // float4 _MatCap_Sampler_var = SAMPLE_MATCAP(TRANSFORM_TEX(_Rot_MatCapUV_var, _MatCap_Sampler), nMatCap_SamplerArrID, _BlurLevelMatcap);
    
    // int nSet_MatcapMaskArrID = _Set_MatcapMaskArr_ID;
    // float4 _Set_MatcapMask_var = SAMPLE_SET_MATCAPMASK(TRANSFORM_TEX(Set_UV0, _Set_MatcapMask), nSet_MatcapMaskArrID);

    // MatcapMask
    float _Tweak_MatcapMaskLevel_var = saturate(lerp(_vSet_MatcapMask_var.g, (1.0 - _vSet_MatcapMask_var.g), _fInverse_MatcapMask) + _fTweak_MatcapMaskLevel);
    float3 _Is_LightColor_MatCap_var = lerp( (_vMatCap_Sampler_var.rgb*_MatCapColor.rgb), ((_vMatCap_Sampler_var.rgb*_MatCapColor.rgb)*Set_LightColor), _fIs_LightColor_MatCap );
    // ShadowMask on Matcap in Blend mode : multiply
    float3 Set_MatCap = lerp( _Is_LightColor_MatCap_var, (_Is_LightColor_MatCap_var*((1.0 - _fSet_FinalShadowMask)+(_fSet_FinalShadowMask*_fTweakMatCapOnShadow)) + lerp(_vSet_HighColor*_fSet_FinalShadowMask*(1.0-_fTweakMatCapOnShadow), float3(0.0, 0.0, 0.0), _fIs_BlendAddToMatCap)), _fIs_UseTweakMatCapOnShadow );

    // Composition: RimLight and MatCap as finalColor
    // Broke down finalColor composition
    float3 matCapColorOnAddMode = _vRimLight_var+Set_MatCap*_Tweak_MatcapMaskLevel_var;
    float _Tweak_MatcapMaskLevel_var_MultiplyMode = _Tweak_MatcapMaskLevel_var * lerp (1.0, (1.0 - (_fSet_FinalShadowMask)*(1.0 - _fTweakMatCapOnShadow)), _fIs_UseTweakMatCapOnShadow);
    float3 matCapColorOnMultiplyMode = _vSet_HighColor*(1-_Tweak_MatcapMaskLevel_var_MultiplyMode) + _vSet_HighColor*Set_MatCap*_Tweak_MatcapMaskLevel_var_MultiplyMode + lerp(float3(0,0,0),_vSet_RimLight,_fRimLight);
    float3 matCapColorFinal = lerp(matCapColorOnMultiplyMode, matCapColorOnAddMode, _fIs_BlendAddToMatCap);
    float3 finalColor = lerp(_vRimLight_var, matCapColorFinal, _fMatCap);// Final Composition before Emissive
    return finalColor;
}

float3 CalculateEmissionColour_Simple(float4 _vEmissive_Tex_var, float3 _vEmissive_Color)
{
    return _vEmissive_Tex_var.rgb * _vEmissive_Color.rgb * _vEmissive_Tex_var.a;;
}

// Calculation View Coord UV for Scroll
float2 CalculateEmissionAnimationUV(float4x4 _mUNITY_MATRIX_V,
                                    float4 _vTime,
                                    float3 _vSurfaceNormal,
                                    float3 _vViewDirection,
                                    float2 _vUV0,
                                    float _fRotate_EmissiveUV,
                                    float _fCamera_Roll,
                                    float _fIs_ViewCoord_Scroll,
                                    float _fBase_Speed,
                                    float _fIs_PingPong_Base,
                                    float _fScroll_EmissiveU,
                                    float _fScroll_EmissiveV,
                                    half _fCamera_Dir,
                                    half _fsign_Mirror)
{
    float3 viewNormal_Emissive = (mul(_mUNITY_MATRIX_V, float4(_vSurfaceNormal,0))).xyz;
    float3 NormalBlend_Emissive_Detail = viewNormal_Emissive * float3(-1,-1,1);
    float3 NormalBlend_Emissive_Base = (mul( _mUNITY_MATRIX_V, float4(_vViewDirection,0)).xyz*float3(-1,-1,1)) + float3(0,0,1);
    float3 noSknewViewNormal_Emissive = NormalBlend_Emissive_Base*dot(NormalBlend_Emissive_Base, NormalBlend_Emissive_Detail)/NormalBlend_Emissive_Base.z - NormalBlend_Emissive_Detail;
    float2 _ViewNormalAsEmissiveUV = noSknewViewNormal_Emissive.xy*0.5+0.5;
    float2 _ViewCoord_UV = RotateUV(_ViewNormalAsEmissiveUV, -(_fCamera_Dir*_fCamera_Roll), float2(0.5,0.5), 1.0);
    // Invert if it's "inside the mirror".
    if(_fsign_Mirror < 0)
    {
        _ViewCoord_UV.x = 1-_ViewCoord_UV.x;
    }
    else
    {
        _ViewCoord_UV = _ViewCoord_UV;
    }
    float2 emissive_uv = lerp(_vUV0, _ViewCoord_UV, _fIs_ViewCoord_Scroll);
    float4 _time_var = _vTime;
    float _base_Speed_var = (_time_var.g*_fBase_Speed);
    float _Is_PingPong_Base_var = lerp(_base_Speed_var, sin(_base_Speed_var), _fIs_PingPong_Base );
    float2 scrolledUV = emissive_uv - float2(_fScroll_EmissiveU, _fScroll_EmissiveV)*_Is_PingPong_Base_var;
    float rotateVelocity = _fRotate_EmissiveUV*3.141592654;
    float2 _rotate_EmissiveUV_var = RotateUV(scrolledUV, rotateVelocity, float2(0.5, 0.5), _Is_PingPong_Base_var);
    return _rotate_EmissiveUV_var;
}

float3 CalculateEmissionColour_Animation(   float4 _vEmissive_Tex_var,
                                            float4 _vEmissive_Tex_var_Rotation,
                                            float4 _vViewShift,
                                            float4 _vColorShift,
                                            float4 _vEmissive_Color,
                                            float4 _vTime,
                                            float3 _vNormalDirection,
                                            float3 _vViewDirection,
                                            float _fColorShift_Speed,
                                            float _fIs_ColorShift,
                                            float _fIs_ViewShift)
{
    float4 _time_var = _vTime;
    float _colorShift_Speed_var = 1.0 - cos(_time_var.g*_fColorShift_Speed);
    float viewShift_var = smoothstep( 0.0, 1.0, max(0,dot(_vNormalDirection,_vViewDirection)));
    float4 colorShift_Color = lerp(_vEmissive_Color, lerp(_vEmissive_Color, _vColorShift, _colorShift_Speed_var), _fIs_ColorShift);
    float4 viewShift_Color = lerp(_vViewShift, colorShift_Color, viewShift_var);
    float4 emissive_Color = lerp(colorShift_Color, viewShift_Color, _fIs_ViewShift);
    float3 vAnimatedColour = emissive_Color.rgb * _vEmissive_Tex_var_Rotation.rgb * _vEmissive_Tex_var.a;
    return vAnimatedColour;
}