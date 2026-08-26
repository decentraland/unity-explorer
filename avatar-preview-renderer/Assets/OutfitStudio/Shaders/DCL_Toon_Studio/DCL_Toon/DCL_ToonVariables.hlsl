#ifndef DCL_TOON_VARIABLES_INCLUDED
#define DCL_TOON_VARIABLES_INCLUDED

#ifdef _DCL_VARIABLE_OPTIMISATION
// m_Floats:
#define _1st2nd_Shades_Feather 0.05f
#define _1st_ShadeColor_Feather 0.02f
#define _1st_ShadeColor_Step 0.2f
#define _2nd_ShadeColor_Feather 0.05f
#define _2nd_ShadeColor_Step 0.2
#define _Add_Antipodean_RimLight 0.0f
#define _Ap_RimLight_FeatherOff 0.0f
#define _Ap_RimLight_Power 0.1f
#define _BaseColor_Step 0.2f
#define _BaseShade_Feather 0.02f
// _BlurLevelMatcap promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _BlurLevelSGM 0.0f
// _BumpScale promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _BumpScaleMatcap 1.0f
#define _CameraRolling_Stabilizer 0.0f
#define _Cutoff 0.5f
#define _Farthest_Distance 100.0f
// _GI_Intensity promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _HighColor_Power 0.7f
#define _Inverse_Clipping 0.0f
#define _Inverse_MatcapMask 0.0f
#define _Inverse_Z_Axis_BLD 1.0f
#define _Is_BLD 0.0f
#define _Is_BakedNormal 0.0f
#define _Is_BlendAddToHiColor 1.0f
#define _Is_BlendAddToMatCap 1.0f
// _Is_BlendBaseColor promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _Is_Filter_HiCutPointLightColor 1.0f
#define _Is_Filter_LightColor 1.0f
#define _Is_LightColor_1st_Shade 1.0f
#define _Is_LightColor_2nd_Shade 1.0f
#define _Is_LightColor_Ap_RimLight 1.0f
#define _Is_LightColor_Base 1.0f
#define _Is_LightColor_HighColor 1.0f
#define _Is_LightColor_MatCap 1.0f
#define _Is_LightColor_Outline 0.0f
// _Is_LightColor_RimLight promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _Is_NormalMapForMatCap 0.0f
#define _Is_NormalMapToBase 1.0f
#define _Is_NormalMapToHighColor 1.0f
#define _Is_NormalMapToRimLight 1.0f
#define _Is_Ortho 0.0f
#define _Is_OutlineTex 0.0f
#define _Is_SpecularToHighColor 0.0f
#define _Is_UseTweakHighColorOnShadow 0.0f
#define _Is_UseTweakMatCapOnShadow 0.0f
#define _LightDirection_MaskOn 0.0f
#define _MatCap 0.0f
#define _Metallic 0.0f
#define _Nearest_Distance 0.5f
#define _OcclusionStrength 1.0f
#define _Offset_X_Axis_BLD -0.05f
#define _Offset_Y_Axis_BLD 0.09f
#define _Offset_Z 0.0f
// _Outline_Width promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _RimLight promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _RimLight_FeatherOff promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _RimLight_InsideMask promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _RimLight_Power promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _Rotate_MatCapUV 0.0f
#define _Rotate_NormalMapForMatCapUV 0.0f
#define _Set_SystemShadowsToBase 1.0f
#define _ShadeColor_Step 0.2f
#define _Smoothness 0.5f
#define _StepOffset 0.0f
#define _Surface 0.0f
#define _TweakHighColorOnShadow 0.0f
#define _TweakMatCapOnShadow 0.0f
#define _Tweak_HighColorMaskLevel -1.0f
#define _Tweak_LightDirection_MaskLevel 0.0f
#define _Tweak_MatCapUV 0.0f
#define _Tweak_MatcapMaskLevel 0.0f
// _Tweak_RimLightMaskLevel promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _Tweak_ShadingGradeMapLevel 0.0f
#define _Tweak_SystemShadowsLevel 0.0f
#define _Unlit_Intensity 4.0f
#define _Use_1stAs2nd 1.0f
#define _Use_BaseAs1st 1.0f
#define _ZOverDrawMode 0.0f


// m_Colors:
#define _1st_ShadeColor float4 (0.9490197, 0.9490197, 0.9490197, 1)
#define _2nd_ShadeColor float4 (0.8000001, 0.8000001, 0.8000001, 1)
#define _Ap_RimLightColor float4 (1, 1, 1, 1)
#define _Color float4 (1, 1, 1, 1)
#define _EmissionColor float4 (0, 0, 0, 1)
#define _HighColor float4 (1, 1, 1, 1)
// _MatCapColor promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _Outline_Color promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
// _RimLightColor promoted to a runtime per-material property (see DCL_ToonInput.hlsl)
#define _HighlightObjectOffset float4 (0.0f, 0.0f, 0.0f, 0.0f)
#define _HighlightColour float4 (0.5f, 0.0f, 0.5f, 1.0f)
#endif

#endif // DCL_TOON_VARIABLES_INCLUDED