Shader "DCL/ChatReactions/EmojiInstancedUnlit"
{
    Properties
    {
        _AtlasTex ("Emoji Atlas", 2D) = "white" {}
        _AtlasCols ("Atlas Columns", Float) = 64
        _AtlasRows ("Atlas Rows", Float) = 64
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1
        _FlipY ("Flip Atlas Y", Float) = 1

        // Fade tuning (optional)
        _FadeIn ("Fade In (t01)", Range(0.01,0.5)) = 0.10
        _FadeOut ("Fade Out Start (t01)", Range(0.5,0.99)) = 0.80
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _AtlasTex;
            float _AtlasCols;
            float _AtlasRows;
            float _GlobalAlpha;
            float _FlipY;
            float _FadeIn;
            float _FadeOut;

            // Per-instance packed data:
            // _PosSize: xyz = world pos, w = startSize
            // _Extra:   x = endSize
            // _Emoji:   x = emojiIndex
            // _LifeT:   x = t01 (age/lifetime 0..1)
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _PosSize)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Extra)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Emoji)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LifeT)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float  a   : TEXCOORD1;
            };

            float2 EmojiUV(float2 baseUV, float emojiIndex)
            {
                float cols = max(1.0, _AtlasCols);
                float rows = max(1.0, _AtlasRows);

                float tileW = 1.0 / cols;
                float tileH = 1.0 / rows;

                float x = fmod(emojiIndex, cols);
                float y = floor(emojiIndex / cols);

                if (_FlipY > 0.5)
                    y = (rows - 1.0) - y;

                float2 tileMin = float2(x * tileW, y * tileH);
                return tileMin + baseUV * float2(tileW, tileH);
            }

            // nice cheap easing for size growth
            float EaseOut(float t)
            {
                // cubic ease out: 1 - (1-t)^3
                float u = 1.0 - t;
                return 1.0 - u*u*u;
            }

            float FadeInOut(float t)
            {
                // fade in 0.._FadeIn
                float fin = smoothstep(0.0, _FadeIn, t);
                // fade out _FadeOut..1
                float fout = 1.0 - smoothstep(_FadeOut, 1.0, t);
                return saturate(fin * fout);
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                float4 posSize = UNITY_ACCESS_INSTANCED_PROP(Props, _PosSize);
                float4 extra   = UNITY_ACCESS_INSTANCED_PROP(Props, _Extra);
                float4 emoji   = UNITY_ACCESS_INSTANCED_PROP(Props, _Emoji);
                float  t01     = UNITY_ACCESS_INSTANCED_PROP(Props, _LifeT).x;

                float3 centerWS = posSize.xyz;
                float  startS   = posSize.w;
                float  endS     = extra.x;

                float size = lerp(startS, endS, EaseOut(saturate(t01)));

                // Billboard basis from camera
                float3 right = unity_CameraToWorld._m00_m10_m20;
                float3 up    = unity_CameraToWorld._m01_m11_m21;

                float3 ws = centerWS + right * (v.vertex.x * size) + up * (v.vertex.y * size);

                v2f o;
                o.pos = UnityWorldToClipPos(ws);
                o.uv  = EmojiUV(v.uv, emoji.x);
                o.a   = _GlobalAlpha * FadeInOut(saturate(t01));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_AtlasTex, i.uv);
                c.a *= i.a;
                return c;
            }
            ENDHLSL
        }
    }
}