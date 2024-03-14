#ifndef DCL_TOON_INPUT_INCLUDED
#define DCL_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float, _utsTechnique)
UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_DEFINE_INSTANCED_PROP(half, _Use_BaseAs1st)
UNITY_DEFINE_INSTANCED_PROP(half, _Use_1stAs2nd)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_Base)
UNITY_DEFINE_INSTANCED_PROP(float4, _1st_ShadeMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _1st_ShadeColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_1st_Shade)
UNITY_DEFINE_INSTANCED_PROP(float4, _2nd_ShadeMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _2nd_ShadeColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_2nd_Shade)
UNITY_DEFINE_INSTANCED_PROP(float4, _NormalMap_ST)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_NormalMapToBase)
UNITY_DEFINE_INSTANCED_PROP(half, _Set_SystemShadowsToBase)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_SystemShadowsLevel)
UNITY_DEFINE_INSTANCED_PROP(float, _BaseColor_Step)
UNITY_DEFINE_INSTANCED_PROP(float, _BaseShade_Feather)
UNITY_DEFINE_INSTANCED_PROP(float4, _Set_1st_ShadePosition_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _ShadeColor_Step)
UNITY_DEFINE_INSTANCED_PROP(float, _1st2nd_Shades_Feather)
UNITY_DEFINE_INSTANCED_PROP(float4, _Set_2nd_ShadePosition_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _ShadingGradeMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_ShadingGradeMapLevel)
UNITY_DEFINE_INSTANCED_PROP(half, _BlurLevelSGM)
UNITY_DEFINE_INSTANCED_PROP(float, _1st_ShadeColor_Step)
UNITY_DEFINE_INSTANCED_PROP(float, _1st_ShadeColor_Feather)
UNITY_DEFINE_INSTANCED_PROP(float, _2nd_ShadeColor_Step)
UNITY_DEFINE_INSTANCED_PROP(float, _2nd_ShadeColor_Feather)
UNITY_DEFINE_INSTANCED_PROP(float4, _HighColor)
UNITY_DEFINE_INSTANCED_PROP(float4, _HighColor_Tex_ST)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_HighColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_NormalMapToHighColor)
UNITY_DEFINE_INSTANCED_PROP(float, _HighColor_Power)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_SpecularToHighColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_BlendAddToHiColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_UseTweakHighColorOnShadow)
UNITY_DEFINE_INSTANCED_PROP(float, _TweakHighColorOnShadow)
UNITY_DEFINE_INSTANCED_PROP(float4, _Set_HighColorMask_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_HighColorMaskLevel)
UNITY_DEFINE_INSTANCED_PROP(half, _RimLight)
UNITY_DEFINE_INSTANCED_PROP(float4, _RimLightColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_RimLight)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_NormalMapToRimLight)
UNITY_DEFINE_INSTANCED_PROP(float, _RimLight_Power)
UNITY_DEFINE_INSTANCED_PROP(float, _RimLight_InsideMask)
UNITY_DEFINE_INSTANCED_PROP(half, _RimLight_FeatherOff)
UNITY_DEFINE_INSTANCED_PROP(half, _LightDirection_MaskOn)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_LightDirection_MaskLevel)
UNITY_DEFINE_INSTANCED_PROP(half, _Add_Antipodean_RimLight)
UNITY_DEFINE_INSTANCED_PROP(float4, _Ap_RimLightColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_Ap_RimLight)
UNITY_DEFINE_INSTANCED_PROP(float, _Ap_RimLight_Power)
UNITY_DEFINE_INSTANCED_PROP(half, _Ap_RimLight_FeatherOff)
UNITY_DEFINE_INSTANCED_PROP(float4, _Set_RimLightMask_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_RimLightMaskLevel)
UNITY_DEFINE_INSTANCED_PROP(half, _MatCap)
UNITY_DEFINE_INSTANCED_PROP(float4, _MatCap_Sampler_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _MatCapColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_MatCap)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_BlendAddToMatCap)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_MatCapUV)
UNITY_DEFINE_INSTANCED_PROP(float, _Rotate_MatCapUV)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_NormalMapForMatCap)
UNITY_DEFINE_INSTANCED_PROP(float4, _NormalMapForMatCap_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _Rotate_NormalMapForMatCapUV)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_UseTweakMatCapOnShadow)
UNITY_DEFINE_INSTANCED_PROP(float, _TweakMatCapOnShadow)
UNITY_DEFINE_INSTANCED_PROP(float4, _Set_MatcapMask_ST)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_MatcapMaskLevel)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_Ortho)
UNITY_DEFINE_INSTANCED_PROP(float, _CameraRolling_Stabilizer)
UNITY_DEFINE_INSTANCED_PROP(half, _BlurLevelMatcap)
UNITY_DEFINE_INSTANCED_PROP(half, _Inverse_MatcapMask)
UNITY_DEFINE_INSTANCED_PROP(float, _BumpScaleMatcap)
UNITY_DEFINE_INSTANCED_PROP(float4, _Emissive_Tex_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _Emissive_Color)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_ViewCoord_Scroll)
UNITY_DEFINE_INSTANCED_PROP(float, _Rotate_EmissiveUV)
UNITY_DEFINE_INSTANCED_PROP(float, _Base_Speed)
UNITY_DEFINE_INSTANCED_PROP(float, _Scroll_EmissiveU)
UNITY_DEFINE_INSTANCED_PROP(float, _Scroll_EmissiveV)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_PingPong_Base)
UNITY_DEFINE_INSTANCED_PROP(float4, _ColorShift)
UNITY_DEFINE_INSTANCED_PROP(float4, _ViewShift)
UNITY_DEFINE_INSTANCED_PROP(float, _ColorShift_Speed)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_ColorShift)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_ViewShift)
UNITY_DEFINE_INSTANCED_PROP(float3, emissive)
UNITY_DEFINE_INSTANCED_PROP(float, _Unlit_Intensity)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_Filter_HiCutPointLightColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_Filter_LightColor)
UNITY_DEFINE_INSTANCED_PROP(float, _StepOffset)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_BLD)
UNITY_DEFINE_INSTANCED_PROP(float, _Offset_X_Axis_BLD)
UNITY_DEFINE_INSTANCED_PROP(float, _Offset_Y_Axis_BLD)
UNITY_DEFINE_INSTANCED_PROP(half, _Inverse_Z_Axis_BLD)
UNITY_DEFINE_INSTANCED_PROP(float4, _ClippingMask_ST)
UNITY_DEFINE_INSTANCED_PROP(half, _IsBaseMapAlphaAsClippingMask)
UNITY_DEFINE_INSTANCED_PROP(float, _Clipping_Level)
UNITY_DEFINE_INSTANCED_PROP(half, _Inverse_Clipping)
UNITY_DEFINE_INSTANCED_PROP(float, _Tweak_transparency)
UNITY_DEFINE_INSTANCED_PROP(float, _GI_Intensity)
UNITY_DEFINE_INSTANCED_PROP(half , _AngelRing)
UNITY_DEFINE_INSTANCED_PROP(float4, _AngelRing_Sampler_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _AngelRing_Color)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_AR)
UNITY_DEFINE_INSTANCED_PROP(float, _AR_OffsetU)
UNITY_DEFINE_INSTANCED_PROP(float, _AR_OffsetV)
UNITY_DEFINE_INSTANCED_PROP(half, _ARSampler_AlphaOn)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_LightColor_Outline)
UNITY_DEFINE_INSTANCED_PROP(float, _Outline_Width)
UNITY_DEFINE_INSTANCED_PROP(float, _Farthest_Distance)
UNITY_DEFINE_INSTANCED_PROP(float, _Nearest_Distance)
UNITY_DEFINE_INSTANCED_PROP(float4, _Outline_Sampler_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _Outline_Color)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_BlendBaseColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Offset_Z)
UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineTex_ST)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_OutlineTex)
UNITY_DEFINE_INSTANCED_PROP(float4, _BakedNormal_ST)
UNITY_DEFINE_INSTANCED_PROP(half, _Is_BakedNormal)
UNITY_DEFINE_INSTANCED_PROP(float, _ZOverDrawMode)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _SpecColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
UNITY_DEFINE_INSTANCED_PROP(half, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(half, _BumpScale)
UNITY_DEFINE_INSTANCED_PROP(half, _OcclusionStrength)
UNITY_DEFINE_INSTANCED_PROP(half, _Surface)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_TexelSize)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_MipInfo)
UNITY_DEFINE_INSTANCED_PROP(int, _MainTexArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _1st_ShadeMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _2nd_ShadeMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _NormalMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Set_1st_ShadePositionArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Set_2nd_ShadePositionArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _ShadingGradeMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _HighColor_TexArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Set_HighColorMaskArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Set_RimLightMaskArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _MatCap_SamplerArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _NormalMapForMatCapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Set_MatcapMaskArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Emissive_TexArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _ClippingMaskArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _AngelRing_SamplerArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _Outline_SamplerArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _OutlineTexArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _BakedNormalArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _OcclusionMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _MetallicGlossMapArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _BaseMapArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _BumpMapArr_ID) 
UNITY_DEFINE_INSTANCED_PROP(int, _EmissionMapArr_ID)
UNITY_DEFINE_INSTANCED_PROP(int, _lastWearableVertCount)
UNITY_DEFINE_INSTANCED_PROP(int, _lastAvatarVertCount)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define _utsTechnique                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _utsTechnique)
#define _MainTex_ST                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST)
#define _Color                              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color)
#define _Use_BaseAs1st                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Use_BaseAs1st)
#define _Use_1stAs2nd                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Use_1stAs2nd)
#define _Is_LightColor_Base                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_Base)
#define _1st_ShadeMap_ST                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st_ShadeMap_ST)
#define _1st_ShadeColor                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st_ShadeColor)
#define _Is_LightColor_1st_Shade            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_1st_Shade)
#define _2nd_ShadeMap_ST                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _2nd_ShadeMap_ST)
#define _2nd_ShadeColor                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _2nd_ShadeColor)
#define _Is_LightColor_2nd_Shade            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_2nd_Shade)
#define _NormalMap_ST                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMap_ST)
#define _Is_NormalMapToBase                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_NormalMapToBase)
#define _Set_SystemShadowsToBase            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_SystemShadowsToBase)
#define _Tweak_SystemShadowsLevel           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_SystemShadowsLevel)
#define _BaseColor_Step                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor_Step)
#define _BaseShade_Feather                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseShade_Feather)
#define _Set_1st_ShadePosition_ST           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_1st_ShadePosition_ST)
#define _ShadeColor_Step                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadeColor_Step)
#define _1st2nd_Shades_Feather              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st2nd_Shades_Feather)
#define _Set_2nd_ShadePosition_ST           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_2nd_ShadePosition_ST)
#define _ShadingGradeMap_ST                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadingGradeMap_ST)
#define _Tweak_ShadingGradeMapLevel         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_ShadingGradeMapLevel)
#define _BlurLevelSGM                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BlurLevelSGM)
#define _1st_ShadeColor_Step                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st_ShadeColor_Step)
#define _1st_ShadeColor_Feather             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st_ShadeColor_Feather)
#define _2nd_ShadeColor_Step                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _2nd_ShadeColor_Step)
#define _2nd_ShadeColor_Feather             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _2nd_ShadeColor_Feather)
#define _HighColor                          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HighColor)
#define _HighColor_Tex_ST                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HighColor_Tex_ST)
#define _Is_LightColor_HighColor            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_HighColor)
#define _Is_NormalMapToHighColor            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_NormalMapToHighColor)
#define _HighColor_Power                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HighColor_Power)
#define _Is_SpecularToHighColor             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_SpecularToHighColor)
#define _Is_BlendAddToHiColor               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_BlendAddToHiColor)
#define _Is_UseTweakHighColorOnShadow       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_UseTweakHighColorOnShadow)
#define _TweakHighColorOnShadow             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TweakHighColorOnShadow)
#define _Set_HighColorMask_ST               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_HighColorMask_ST)
#define _Tweak_HighColorMaskLevel           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_HighColorMaskLevel)
#define _RimLight                           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RimLight)
#define _RimLightColor                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RimLightColor)
#define _Is_LightColor_RimLight             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_RimLight)
#define _Is_NormalMapToRimLight             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_NormalMapToRimLight)
#define _RimLight_Power                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RimLight_Power)
#define _RimLight_InsideMask                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RimLight_InsideMask)
#define _RimLight_FeatherOff                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RimLight_FeatherOff)
#define _LightDirection_MaskOn              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LightDirection_MaskOn)
#define _Tweak_LightDirection_MaskLevel     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_LightDirection_MaskLevel)
#define _Add_Antipodean_RimLight            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Add_Antipodean_RimLight)
#define _Ap_RimLightColor                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Ap_RimLightColor)
#define _Is_LightColor_Ap_RimLight          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_Ap_RimLight)
#define _Ap_RimLight_Power                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Ap_RimLight_Power)
#define _Ap_RimLight_FeatherOff             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Ap_RimLight_FeatherOff)
#define _Set_RimLightMask_ST                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_RimLightMask_ST)
#define _Tweak_RimLightMaskLevel            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_RimLightMaskLevel)
#define _MatCap                             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MatCap)
#define _MatCap_Sampler_ST                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MatCap_Sampler_ST)
#define _MatCapColor                        UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MatCapColor)
#define _Is_LightColor_MatCap               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_MatCap)
#define _Is_BlendAddToMatCap                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_BlendAddToMatCap)
#define _Tweak_MatCapUV                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_MatCapUV)
#define _Rotate_MatCapUV                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Rotate_MatCapUV)
#define _Is_NormalMapForMatCap              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_NormalMapForMatCap)
#define _NormalMapForMatCap_ST              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMapForMatCap_ST)
#define _Rotate_NormalMapForMatCapUV        UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Rotate_NormalMapForMatCapUV)
#define _Is_UseTweakMatCapOnShadow          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_UseTweakMatCapOnShadow)
#define _TweakMatCapOnShadow                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TweakMatCapOnShadow)
#define _Set_MatcapMask_ST                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_MatcapMask_ST)
#define _Tweak_MatcapMaskLevel              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_MatcapMaskLevel)
#define _Is_Ortho                           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_Ortho)
#define _CameraRolling_Stabilizer           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CameraRolling_Stabilizer)
#define _BlurLevelMatcap                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BlurLevelMatcap)
#define _Inverse_MatcapMask                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Inverse_MatcapMask)
#define _BumpScaleMatcap                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpScaleMatcap)
#define _Emissive_Tex_ST                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Emissive_Tex_ST)
#define _Emissive_Color                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Emissive_Color)
#define _Is_ViewCoord_Scroll                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,  _Is_ViewCoord_Scroll )
#define _Rotate_EmissiveUV                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Rotate_EmissiveUV)
#define _Base_Speed                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Base_Speed)
#define _Scroll_EmissiveU                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Scroll_EmissiveU)
#define _Scroll_EmissiveV                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Scroll_EmissiveV)
#define _Is_PingPong_Base                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_PingPong_Base)
#define _ColorShift                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorShift)
#define _ViewShift                          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ViewShift)
#define _ColorShift_Speed                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorShift_Speed)
#define _Is_ColorShift                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_ColorShift)
#define _Is_ViewShift                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_ViewShift)
#define emissive                            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, emissive)
#define _Unlit_Intensity                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Unlit_Intensity)
#define _Is_Filter_HiCutPointLightColor     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_Filter_HiCutPointLightColor)
#define _Is_Filter_LightColor               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_Filter_LightColor)
#define _StepOffset                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _StepOffset)
#define _Is_BLD                             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_BLD)
#define _Offset_X_Axis_BLD                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Offset_X_Axis_BLD)
#define _Offset_Y_Axis_BLD                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Offset_Y_Axis_BLD)
#define _Inverse_Z_Axis_BLD                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Inverse_Z_Axis_BLD)
#define _ClippingMask_ST                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ClippingMask_ST)
#define _IsBaseMapAlphaAsClippingMask       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _IsBaseMapAlphaAsClippingMask)
#define _Clipping_Level                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Clipping_Level)
#define _Inverse_Clipping                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Inverse_Clipping)
#define _Tweak_transparency                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Tweak_transparency)
#define _GI_Intensity                       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GI_Intensity)
#define  _AngelRing                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,  _AngelRing)
#define _AngelRing_Sampler_ST               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AngelRing_Sampler_ST)
#define _AngelRing_Color                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AngelRing_Color)
#define _Is_LightColor_AR                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_AR)
#define _AR_OffsetU                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AR_OffsetU)
#define _AR_OffsetV                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AR_OffsetV)
#define _ARSampler_AlphaOn                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ARSampler_AlphaOn)
#define _Is_LightColor_Outline              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_LightColor_Outline)
#define _Outline_Width                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Outline_Width)
#define _Farthest_Distance                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Farthest_Distance)
#define _Nearest_Distance                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Nearest_Distance)
#define _Outline_Sampler_ST                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Outline_Sampler_ST)
#define _Outline_Color                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Outline_Color)
#define _Is_BlendBaseColor                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_BlendBaseColor)
#define _Offset_Z                           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Offset_Z)
#define _OutlineTex_ST                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineTex_ST)
#define _Is_OutlineTex                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_OutlineTex)
#define _BakedNormal_ST                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BakedNormal_ST)
#define _Is_BakedNormal                     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Is_BakedNormal)
#define _ZOverDrawMode                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZOverDrawMode)
#define _BaseMap_ST                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST)
#define _BaseColor                          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor)
#define _SpecColor                          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecColor)
#define _EmissionColor                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor)
#define _Cutoff                             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)
#define _Smoothness                         UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness)
#define _Metallic                           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic)
#define _BumpScale                          UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpScale)
#define _OcclusionStrength                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionStrength)
#define _Surface                            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)
#define _BaseMap_TexelSize                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_TexelSize)
#define _BaseMap_MipInfo                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_MipInfo)
#define _MainTexArr_ID                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTexArr_ID)
#define _1st_ShadeMapArr_ID                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _1st_ShadeMapArr_ID)
#define _2nd_ShadeMapArr_ID                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _2nd_ShadeMapArr_ID)
#define _NormalMapArr_ID                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMapArr_ID)
#define _Set_1st_ShadePositionArr_ID        UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_1st_ShadePositionArr_ID)
#define _Set_2nd_ShadePositionArr_ID        UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_2nd_ShadePositionArr_ID)
#define _ShadingGradeMapArr_ID              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadingGradeMapArr_ID)
#define _HighColor_TexArr_ID                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HighColor_TexArr_ID)
#define _Set_HighColorMaskArr_ID            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_HighColorMaskArr_ID)
#define _Set_RimLightMaskArr_ID             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_RimLightMaskArr_ID)
#define _MatCap_SamplerArr_ID               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MatCap_SamplerArr_ID)
#define _NormalMapForMatCapArr_ID           UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalMapForMatCapArr_ID)
#define _Set_MatcapMaskArr_ID               UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Set_MatcapMaskArr_ID)
#define _Emissive_TexArr_ID                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Emissive_TexArr_ID)
#define _ClippingMaskArr_ID                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ClippingMaskArr_ID)
#define _AngelRing_SamplerArr_ID            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AngelRing_SamplerArr_ID)
#define _Outline_SamplerArr_ID              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Outline_SamplerArr_ID)
#define _OutlineTexArr_ID                   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineTexArr_ID)
#define _BakedNormalArr_ID                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BakedNormalArr_ID)
#define _OcclusionMapArr_ID                 UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OcclusionMapArr_ID)
#define _MetallicGlossMapArr_ID             UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MetallicGlossMapArr_ID)
#define _BaseMapArr_ID                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMapArr_ID)
#define _BumpMapArr_ID                      UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BumpMapArr_ID)
#define _EmissionMapArr_ID                  UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionMapArr_ID)
#define _lastWearableVertCount              UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastWearableVertCount) 
#define _lastAvatarVertCount                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _lastAvatarVertCount)

#ifdef _DCL_TEXTURE_ARRAYS
    #define DCL_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
    #define DCL_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)
    #define DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(tex) Texture2DArray tex;
    #define DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(tex,coord) tex.Sample (sampler_MainTexArr,coord)
    #define DCL_SAMPLE_TEX2DARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord,lod)

    DCL_DECLARE_TEX2DARRAY(_BaseMapArr);
    DCL_DECLARE_TEX2DARRAY(_BumpMapArr);
    DCL_DECLARE_TEX2DARRAY(_EmissionMapArr);
    DCL_DECLARE_TEX2DARRAY(_MainTexArr);

    DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_NormalMapArr);

    DCL_DECLARE_TEX2DARRAY(_MatCap_SamplerArr);
    DCL_DECLARE_TEX2DARRAY(_NormalMapForMatCapArr);
    DCL_DECLARE_TEX2DARRAY(_Set_MatcapMaskArr);
    DCL_DECLARE_TEX2DARRAY(_Emissive_TexArr);
    DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_ClippingMaskArr);

    DCL_DECLARE_TEX2DARRAY(_OcclusionMapArr);
    DCL_DECLARE_TEX2DARRAY(_MetallicGlossMapArr);

    #define _DCL_OPTIMISATION
    #ifdef _DCL_OPTIMISATION
        // DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_1st_ShadeMapArr);
        // DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_2nd_ShadeMapArr);
        // DCL_DECLARE_TEX2DARRAY(_Set_1st_ShadePositionArr); 
        // DCL_DECLARE_TEX2DARRAY(_Set_2nd_ShadePositionArr);
        // DCL_DECLARE_TEX2DARRAY(_ShadingGradeMapArr);
        // DCL_DECLARE_TEX2DARRAY(_HighColor_TexArr);
        // DCL_DECLARE_TEX2DARRAY(_Set_HighColorMaskArr);
        // DCL_DECLARE_TEX2DARRAY(_Set_RimLightMaskArr);
        // DCL_DECLARE_TEX2DARRAY(_AngelRing_SamplerArr);
        // DCL_DECLARE_TEX2DARRAY(_Outline_SamplerArr);
        // DCL_DECLARE_TEX2DARRAY(_OutlineTexArr);
        // DCL_DECLARE_TEX2DARRAY(_BakedNormalArr);

        #define SAMPLE_1ST_SHADEMAP(uv,texArrayID)                  float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_2ND_SHADEMAP(uv,texArrayID)                  float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_SET_1ST_SHADEPOSITION(uv,texArrayID)         float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_SET_2ND_SHADEPOSITION(uv,texArrayID)         float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_SHADINGGRADEMAP(uv,texArrayID,lod)           float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_HIGHCOLOR(uv,texArrayID)                     float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_HIGHCOLORMASK(uv,texArrayID)                 float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_SET_RIMLIGHTMASK(uv,texArrayID)              float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_ANGELRING(uv,texArrayID)                     float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_OUTLINE(uv,texArrayID,lod)                   float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_OUTLINETEX(uv,texArrayID)                    float4(1.0f, 1.0f, 1.0f, 1.0f)
        #define SAMPLE_BAKEDNORMAL(uv,texArrayID,lod)               float4(1.0f, 1.0f, 1.0f, 1.0f)
    #else
        DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_1st_ShadeMapArr);
        DCL_DECLARE_TEX2DARRAY_DEFAULT_SAMPLER(_2nd_ShadeMapArr);
        DCL_DECLARE_TEX2DARRAY(_Set_1st_ShadePositionArr); 
        DCL_DECLARE_TEX2DARRAY(_Set_2nd_ShadePositionArr);
        DCL_DECLARE_TEX2DARRAY(_ShadingGradeMapArr);
        DCL_DECLARE_TEX2DARRAY(_HighColor_TexArr);
        DCL_DECLARE_TEX2DARRAY(_Set_HighColorMaskArr);
        DCL_DECLARE_TEX2DARRAY(_Set_RimLightMaskArr);
        DCL_DECLARE_TEX2DARRAY(_AngelRing_SamplerArr);
        DCL_DECLARE_TEX2DARRAY(_Outline_SamplerArr);
        DCL_DECLARE_TEX2DARRAY(_OutlineTexArr);
        DCL_DECLARE_TEX2DARRAY(_BakedNormalArr);

        #define SAMPLE_1ST_SHADEMAP(uv,texArrayID)                  DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_1st_ShadeMapArr, float3(uv, texArrayID))
        #define SAMPLE_2ND_SHADEMAP(uv,texArrayID)                  DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_2nd_ShadeMapArr, float3(uv, texArrayID))
        #define SAMPLE_SET_1ST_SHADEPOSITION(uv,texArrayID)         DCL_SAMPLE_TEX2DARRAY(_Set_1st_ShadePositionArr, float3(uv, texArrayID)) 
        #define SAMPLE_SET_2ND_SHADEPOSITION(uv,texArrayID)         DCL_SAMPLE_TEX2DARRAY(_Set_2nd_ShadePositionArr, float3(uv, texArrayID))
        #define SAMPLE_SHADINGGRADEMAP(uv,texArrayID,lod)           DCL_SAMPLE_TEX2DARRAY_LOD(_ShadingGradeMapArr, float3(uv, texArrayID), lod)
        #define SAMPLE_HIGHCOLOR(uv,texArrayID)                     DCL_SAMPLE_TEX2DARRAY(_HighColor_TexArr, float3(uv, texArrayID))
        #define SAMPLE_HIGHCOLORMASK(uv,texArrayID)                 DCL_SAMPLE_TEX2DARRAY(_Set_HighColorMaskArr, float3(uv, texArrayID))
        #define SAMPLE_SET_RIMLIGHTMASK(uv,texArrayID)              DCL_SAMPLE_TEX2DARRAY(_Set_RimLightMaskArr, float3(uv, texArrayID))
        #define SAMPLE_ANGELRING(uv,texArrayID)                     DCL_SAMPLE_TEX2DARRAY(_AngelRing_SamplerArr, float3(uv, texArrayID))
        #define SAMPLE_OUTLINE(uv,texArrayID,lod)                   DCL_SAMPLE_TEX2DARRAY_LOD(_Outline_SamplerArr, float3(uv, texArrayID), lod)
        #define SAMPLE_OUTLINETEX(uv,texArrayID)                    DCL_SAMPLE_TEX2DARRAY(_OutlineTexArr, float3(uv, texArrayID))
        #define SAMPLE_BAKEDNORMAL(uv,texArrayID,lod)               DCL_SAMPLE_TEX2DARRAY_LOD(_BakedNormalArr, float3(uv, texArrayID), lod)
    #endif

    #define SAMPLE_BASEMAP(uv,texArrayID)                       DCL_SAMPLE_TEX2DARRAY(_BaseMapArr, float3(uv, texArrayID))
    #define SAMPLE_BUMPMAP(uv,texArrayID)                       DCL_SAMPLE_TEX2DARRAY(_BumpMapArr, float3(uv, texArrayID))
    #define SAMPLE_EMISSIONMAP(uv,texArrayID)                   DCL_SAMPLE_TEX2DARRAY(_EmissionMapArr, float3(uv, texArrayID))
    #define SAMPLE_MAINTEX(uv,texArrayID)                       DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_MainTexArr, float3(uv, texArrayID))
    
    //#define SAMPLE_NORMALMAP(uv,texArrayID)                     DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_NormalMapArr, float3(uv, texArrayID))
    #define SAMPLE_NORMALMAP(uv,texArrayID)                     float4(0.5f, 0.5f, 1.0f, 1.0f)
    
    // #define SAMPLE_MATCAP(uv,texArrayID,lod)                    DCL_SAMPLE_TEX2DARRAY_LOD(_MatCap_SamplerArr, float3(uv, texArrayID), lod)
    // #define SAMPLE_NORMALMAPFORMATCAP(uv,texArrayID)            DCL_SAMPLE_TEX2DARRAY(_NormalMapForMatCapArr, float3(uv, texArrayID))
    // #define SAMPLE_SET_MATCAPMASK(uv,texArrayID)                DCL_SAMPLE_TEX2DARRAY(_Set_MatcapMaskArr, float3(uv, texArrayID))
    #define SAMPLE_MATCAP(uv,texArrayID,lod)                    float4(0.0f, 0.0f, 0.0f, 0.0f)
    #define SAMPLE_NORMALMAPFORMATCAP(uv,texArrayID)            float4(0.5f, 0.5f, 1.0f, 1.0f)
    #define SAMPLE_SET_MATCAPMASK(uv,texArrayID)                float4(1.0f, 1.0f, 1.0f, 1.0f)
    #define SAMPLE_EMISSIVE(uv,texArrayID)                      DCL_SAMPLE_TEX2DARRAY(_Emissive_TexArr, float3(uv, texArrayID))
    //#define SAMPLE_EMISSIVE(uv,texArrayID)                      float4(0.0f, 0.0f, 0.0f, 0.0f)
    #define SAMPLE_CLIPPINGMASK(uv,texArrayID)                  DCL_SAMPLE_TEX2DARRAY_DEFAULT_SAMPLER(_MainTexArr, float3(uv, texArrayID))

    #define SAMPLE_OCCLUSIONMAP(uv,texArrayID)                  DCL_SAMPLE_TEX2DARRAY(_OcclusionMapArr, float3(uv, texArrayID))
    #define SAMPLE_METALLICGLOSS(uv,texArrayID)                 DCL_SAMPLE_TEX2DARRAY(_MetallicGlossMapArr, float3(uv, texArrayID))
#else
    TEXTURE2D(_BaseMap);                SAMPLER(sampler_BaseMap);
    TEXTURE2D(_BumpMap);                SAMPLER(sampler_BumpMap);
    TEXTURE2D(_EmissionMap);            SAMPLER(sampler_EmissionMap);

    TEXTURE2D(_MainTex);                SAMPLER(sampler_MainTex);
    TEXTURE2D(_1st_ShadeMap);
    TEXTURE2D(_2nd_ShadeMap);
    TEXTURE2D(_NormalMap);
    TEXTURE2D(_ClippingMask);
    TEXTURE2D(_OcclusionMap);           SAMPLER(sampler_OcclusionMap);
    TEXTURE2D(_MetallicGlossMap);       SAMPLER(sampler_MetallicGlossMap);

    sampler2D _Set_1st_ShadePosition; 
    sampler2D _Set_2nd_ShadePosition;
    sampler2D _ShadingGradeMap;
    sampler2D _HighColor_Tex;
    sampler2D _Set_HighColorMask;
    sampler2D _Set_RimLightMask;
    sampler2D _MatCap_Sampler;
    sampler2D _NormalMapForMatCap;
    sampler2D _Set_MatcapMask;
    sampler2D _Emissive_Tex;    
    sampler2D _AngelRing_Sampler;
    sampler2D _Outline_Sampler;
    sampler2D _OutlineTex;
    sampler2D _BakedNormal;

    #define SAMPLE_BASEMAP(uv,texArrayID)                   SAMPLE_TEXTURE2D(_BaseMap,                  sampler_BaseMap, uv)
    #define SAMPLE_BUMPMAP(uv,texArrayID)                   SAMPLE_TEXTURE2D(_BumpMap,                  sampler_BumpMap, uv)
    #define SAMPLE_EMISSIONMAP(uv,texArrayID)               SAMPLE_TEXTURE2D(_EmissionMap,              sampler_EmissionMap, uv)

    #define SAMPLE_MAINTEX(uv,texArrayID)                   SAMPLE_TEXTURE2D(_MainTex,                  sampler_MainTex, uv)
    #define SAMPLE_1ST_SHADEMAP(uv,texArrayID)              SAMPLE_TEXTURE2D(_1st_ShadeMap,             sampler_MainTex, uv)
    #define SAMPLE_2ND_SHADEMAP(uv,texArrayID)              SAMPLE_TEXTURE2D(_2nd_ShadeMap,             sampler_MainTex, uv)
    #define SAMPLE_NORMALMAP(uv,texArrayID)                 SAMPLE_TEXTURE2D(_NormalMap,                sampler_MainTex, uv)
    #define SAMPLE_CLIPPINGMASK(uv,texArrayID)              SAMPLE_TEXTURE2D(_ClippingMask,             sampler_MainTex, uv)
    #define SAMPLE_OCCLUSIONMAP(uv,texArrayID)              SAMPLE_TEXTURE2D(_OcclusionMap,             sampler_OcclusionMap, uv)
    #define SAMPLE_METALLICGLOSS(uv,texArrayID)             SAMPLE_TEXTURE2D(_MetallicGlossMap,         sampler_MetallicGlossMap, uv)

    #define SAMPLE_SET_1ST_SHADEPOSITION(uv,texArrayID)     tex2D(_Set_1st_ShadePosition,       uv) 
    #define SAMPLE_SET_2ND_SHADEPOSITION(uv,texArrayID)     tex2D(_Set_2nd_ShadePosition,       uv)
    #define SAMPLE_SHADINGGRADEMAP(uv,texArrayID,lod)       tex2Dlod(_ShadingGradeMap,          float4(uv, 0.0f, lod))
    #define SAMPLE_HIGHCOLOR(uv,texArrayID)                 tex2D(_HighColor_Tex,               uv)
    #define SAMPLE_HIGHCOLORMASK(uv,texArrayID)             tex2D(_Set_HighColorMask,           uv)
    #define SAMPLE_SET_RIMLIGHTMASK(uv, texArrayID)         tex2D(_Set_RimLightMask,            uv)
    #define SAMPLE_MATCAP(uv,texArrayID,lod)                tex2Dlod(_MatCap_Sampler,           float4(uv, 0.0f, lod))
    #define SAMPLE_NORMALMAPFORMATCAP(uv,texArrayID)        tex2D(_NormalMapForMatCap,          uv)
    #define SAMPLE_SET_MATCAPMASK(uv,texArrayID)            tex2D(_Set_MatcapMask,              uv)
    #define SAMPLE_EMISSIVE(uv,texArrayID)                  tex2D(_Emissive_Tex,                uv)
    #define SAMPLE_ANGELRING(uv,texArrayID)                 tex2D(_AngelRing_Sampler,           uv)
    #define SAMPLE_OUTLINE(uv,texArrayID,lod)               tex2Dlod(_Outline_Sampler,          float4(uv, 0.0f, lod))
    #define SAMPLE_OUTLINETEX(uv,texArrayID)                tex2D(_OutlineTex,                  uv)
    #define SAMPLE_BAKEDNORMAL(uv,texArrayID,lod)           tex2Dlod(_BakedNormal,              float4(uv, 0.0f, lod))
#endif

#include "DCL_Toon_SurfaceInput.hlsl"

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

    #ifdef _METALLICSPECGLOSSMAP
        int nMetallicGlossMapArrID = _MetallicGlossMapArr_ID;
        specGloss = SAMPLE_METALLICGLOSS(uv, nMetallicGlossMapArrID);
        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a *= _Smoothness;
        #endif
    #else // _METALLICSPECGLOSSMAP
        #if _SPECULAR_SETUP
            specGloss.rgb = _SpecColor.rgb;
        #else
            specGloss.rgb = _Metallic.rrr;
        #endif

        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a = _Smoothness;
        #endif
    #endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
        #if defined(SHADER_API_GLES)
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            return SAMPLE_OCCLUSIONMAP(uv, nOcclusionMapArrID).g;
        #else
            int nOcclusionMapArrID = _OcclusionMapArr_ID;
            half occ = SAMPLE_OCCLUSIONMAP(uv, nOcclusionMapArrID).g;
            return LerpWhiteTo(occ, _OcclusionStrength);
        #endif
    #else
        return 1.0;
    #endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv);
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    
    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

    #if _SPECULAR_SETUP
        outSurfaceData.metallic = 1.0h;
        outSurfaceData.specular = specGloss.rgb;
    #else
        outSurfaceData.metallic = specGloss.r;
        outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    #endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb);
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED
