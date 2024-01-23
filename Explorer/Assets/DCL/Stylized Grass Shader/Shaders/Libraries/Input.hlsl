//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float4 _BaseMap_ST;
float4 _BumpMap_ST;
float4 _HueVariation;
float4 _BendTint;
float4 _EmissionColor;
half4 _WindDirection;
half4 _ScalemapInfluence;
half _HueVariationHeight;
half _ColorMapStrength;
half _ColorMapHeight;
half _Cutoff;
half _Smoothness;
half _TranslucencyDirect;
half _TranslucencyIndirect;
half _TranslucencyOffset;
half _TranslucencyFalloff;
half _OcclusionStrength;
half _VertexDarkening;
half _BumpScale;

half _NormalFlattening;
half _NormalSpherify;
half _NormalSpherifyMask;
half _NormalFlattenDepthNormals;
//X: Spherify
//Y: Spherify tip mask
//Z: Flatten bottom
//W:

bool _FadingOn;
half4 _FadeFar;
half4 _FadeNear;
half _FadeAngleThreshold;

//Bending
half _BendPushStrength;
half _BendMode;
half _BendFlattenStrength;
half _PerspectiveCorrection;

//Wind
half _WindAmbientStrength;
float _WindSpeed;
half _WindVertexRand;
half _WindObjectRand;
half _WindRandStrength;
half _WindSwinging;
half _WindGustStrength;
half _WindGustFreq;
float _WindGustSpeed;
half _WindGustTint;

//Vegetation Studio Pro
half4 _LODDebugColor;

int _VertexColorShadingChannel;
int _VertexColorWindChannel;
int _VertexColorBendingChannel;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

#define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
#endif