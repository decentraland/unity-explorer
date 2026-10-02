// Draws an equirectangular (2:1 latitude-longitude) texture as the skybox.
// The direction-to-uv mapping is the same one DCL/EquirectToCube uses for the reflection cubemap,
// so a scene skybox and the reflections derived from it always line up.
Shader "DCL/PanoramicSkybox"
{
    Properties
    {
        _MainTex ("Equirectangular", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "DCL_PanoramicSkybox"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The skybox mesh is centred on the camera, so the object-space position is the view direction
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                // u wraps around +Z (image centre), v runs from -Y (bottom row) to +Y (top row)
                float2 uv = float2(atan2(d.x, d.z) / (2.0 * PI) + 0.5, asin(d.y) / PI + 0.5);
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
