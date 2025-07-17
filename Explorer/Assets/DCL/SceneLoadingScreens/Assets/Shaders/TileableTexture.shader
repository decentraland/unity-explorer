Shader "Custom/AnimatedBackgroundMovingTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // just placeholder to remove exception in the build
        _InnerColor ("Inner Color", Color) = (0.8,0.2,1,1)
        _OuterColor ("Outer Color", Color) = (0.3,0,0.5,1)
        _Radius ("Radius", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0.01,0.5)) = 0.25
        _OverlayTex ("Overlay Texture", 2D) = "white" {}
        _OverlayColor ("Overlay Color", Color) = (1,1,1,1)
        _OverlayTiling ("Overlay Tiling", Float) = 1.0
        _OverlayDirection ("Overlay Direction", Vector) = (-1,1,0,0)
        _OverlaySpeed ("Overlay Speed", Float) = 0.1
        _OverlayAlpha ("Overlay Alpha", Range(0,1)) = 1
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowStrength ("Glow Strength", Float) = 1.0
        _GlowRadius ("Glow Radius", Vector) = (0.2, 0.2, 0, 0)
        _GlowSmoothness ("Glow Smoothness", Float) = 0.1
        _GlowCenter ("Glow Center", Vector) = (0.5,0.5,0,0)
        _LuminosityStrength ("Luminosity Blend Strength", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _OverlayTex;
            float4 _OverlayTex_ST;
            float4 _OverlayColor;
            float _OverlayTiling;
            float4 _OverlayDirection;
            float _OverlaySpeed;
            float _OverlayAlpha;

            float4 _InnerColor;
            float4 _OuterColor;
            float _Radius;
            float _Smoothness;
            float4 _GlowColor;
            float _GlowStrength;
            float4 _GlowRadius;
            float _GlowSmoothness;
            float4 _GlowCenter;
            float _LuminosityStrength;

            float2 AspectCorrectUV(float2 uv)
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                uv = (uv - 0.5) * float2(aspect, 1) + 0.5;
                return uv;
            }

            // RGB to HSV
            float3 rgb2hsv(float3 c) {
                float4 K = float4(0., -1./3., 2./3., -1.);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6. * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB
            float3 hsv2rgb(float3 c) {
                float4 K = float4(1., 2./3., 1./3., 3.);
                float3 p = abs(frac(c.xxx + K.xyz) * 6. - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Vignette background
                float2 uv = AspectCorrectUV(i.uv);
                float2 centerUV = uv - 0.5;
                centerUV.x *= _ScreenParams.y / _ScreenParams.x;
                float radius = length(centerUV);
                float mask = smoothstep(_Radius + _Smoothness, _Radius, radius);
                float4 vignette = lerp(_OuterColor, _InnerColor, mask);

                // Moving overlay texture
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 overlayUV = i.uv;
                overlayUV.x *= _OverlayTiling * aspect;
                overlayUV.y *= _OverlayTiling;
                overlayUV += _Time.y * _OverlayDirection.xy * _OverlaySpeed;
                float4 overlay = tex2D(_OverlayTex, overlayUV) * _OverlayColor;
                overlay.a *= _OverlayAlpha * mask;

                // Luminosity blend mode for overlay
                float3 vignetteHSV = rgb2hsv(vignette.rgb);
                float3 overlayHSV = rgb2hsv(overlay.rgb);
                float v = lerp(0.5, 1.0, overlayHSV.z); // Remap overlay value to [0.5, 1.0]
                float3 luminosityBlend = hsv2rgb(float3(vignetteHSV.x, vignetteHSV.y, v));
                float luminosityBlendAmount = overlay.a * _LuminosityStrength;
                float4 result = float4(lerp(vignette.rgb, luminosityBlend, luminosityBlendAmount), 1.0);
                // Radial glow
                float2 glowCenter = _GlowCenter.xy;
                float2 glowDelta = (i.uv - glowCenter) / _GlowRadius.xy;
                float glowDist = length(glowDelta);
                float glowMask = 1.0 - smoothstep(1.0, 1.0 + _GlowSmoothness, glowDist);
                float4 glow = _GlowColor * glowMask * _GlowStrength;
                result.rgb += glow.rgb * glow.a;
                return result;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
