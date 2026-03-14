Shader "DCL/Sprites/CircleAlwaysOnTop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackgroundColor ("Background Color", Color) = (0.2, 0.2, 0.8, 1)
        _CircleRadius ("Circle Radius", Range(0, 0.5)) = 0.5
        _CircleSoftness ("Edge Softness", Range(0, 0.05)) = 0.01
        _BorderThickness ("Border Thickness", Range(0, 0.15)) = 0.04
        _BorderBrightness ("Border Brightness Factor", Range(1, 3)) = 1.6
        _UVRect ("UV Rect (xy=min, zw=size)", Vector) = (0, 0, 1, 1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Back
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CircleFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "DCL_UnitySprites.cginc"

            fixed4 _BackgroundColor;
            float _CircleRadius;
            float _CircleSoftness;
            float _BorderThickness;
            float _BorderBrightness;
            float4 _UVRect;

            fixed4 CircleFrag(v2f IN) : SV_Target
            {
                // Normalize UVs to 0-1 based on the sprite's actual UV rect
                float2 normalizedUV = (IN.texcoord - _UVRect.xy) / _UVRect.zw;
                float2 centered = normalizedUV - 0.5;
                float dist = length(centered);

                float outerRadius = _CircleRadius;
                float innerRadius = outerRadius - _BorderThickness;

                float outerMask = 1.0 - smoothstep(outerRadius - _CircleSoftness, outerRadius, dist);
                float innerMask = 1.0 - smoothstep(innerRadius - _CircleSoftness, innerRadius, dist);

                fixed4 texColor = SampleSpriteTexture(IN.texcoord) * IN.color;

                fixed4 bg = _BackgroundColor;
                fixed3 borderColor = saturate(bg.rgb * _BorderBrightness);

                // Inside inner circle: sprite composited over background
                fixed3 contentColor = lerp(bg.rgb, texColor.rgb, texColor.a);
                // Blend between border ring and content area
                fixed3 finalColor = lerp(borderColor, contentColor, innerMask);

                fixed4 result;
                result.rgb = finalColor;
                result.a = outerMask;

                return result;
            }
        ENDCG
        }
    }
}
