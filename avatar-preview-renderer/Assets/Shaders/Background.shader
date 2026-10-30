Shader "Custom/GradientBackground"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    ENDHLSL

    Properties
    {
        [HideInInspector] _InnerColor ("Inner Color", Color) = (1,1,1,1)
        [HideInInspector] _OuterColor ("Outer Color", Color) = (1,1,1,1)
        [HideInInspector] _BackgroundCenter ("Background Center", Vector) = (1,1,1,1)
        [HideInInspector] _BackgroundSize ("Background Size", Float) = 1
        [HideInInspector] _HighlightColor ("HighlightColor", Color) = (1,1,1,1)
        [HideInInspector] _HighlightCenter ("Highlight Center", Vector) = (1,1,1,1)
        [HideInInspector] _HighlightSize ("Highlight Size", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "GradientPass"
            ZTest LEqual
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            float4 _InnerColor;
            float4 _OuterColor;
            float2 _BackgroundCenter;
            float _BackgroundSize;
            float4 _HighlightColor;
            float2 _HighlightCenter;
            float2 _HighlightSize;

            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_Target
            {
                // Background
                float t = saturate(distance(IN.texcoord, _BackgroundCenter) * _BackgroundSize);
                float3 base_color = lerp(_InnerColor.rgb, _OuterColor.rgb, smoothstep(0.0, 1.0, t));

                // Highlight
                float2 diff = (IN.texcoord - _HighlightCenter) / _HighlightSize;
                float t2 = saturate(length(diff));
                
                // Smooth the falloff using a curve
                float mask = 1.0 - smoothstep(0.0, 1.0, t2);
                float3 color = lerp(base_color, _HighlightColor.rgb, mask * _HighlightColor.a);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}