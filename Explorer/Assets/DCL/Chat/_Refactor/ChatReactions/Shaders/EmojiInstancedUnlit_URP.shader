Shader "DCL/ChatReactions/EmojiInstancedUnlit_URP"
{
    Properties
    {
        _AtlasTex   ("Emoji Atlas",   2D)            = "white" {}
        _AtlasCols  ("Atlas Columns", Float)         = 64
        _AtlasRows  ("Atlas Rows",    Float)         = 64
        _FlipY      ("Flip Atlas Y",  Float)         = 1
        _GlobalAlpha ("Global Alpha", Range(0,1))   = 1
        _FadeIn    ("Fade In  (t01)", Range(0.01, 0.5))  = 0.10
        _FadeOut   ("Fade Out (t01)", Range(0.5,  0.99)) = 0.80
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
        }

        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"

            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Globals (set once on init, not per-instance) ───────────────────
            TEXTURE2D(_AtlasTex);
            SAMPLER(sampler_AtlasTex);

            float _AtlasCols;
            float _AtlasRows;
            float _FlipY;
            float _GlobalAlpha;
            float _FadeIn;
            float _FadeOut;

            // ── Per-instance data (packed into 4 float4s per draw call) ────────
            //   _PosSize : xyz = world pos,  w = startSize
            //   _Extra   : x  = endSize
            //   _Emoji   : x  = emojiIndex (float, cast to int in shader)
            //   _LifeT   : x  = t01 (age / lifetime, 0..1)
            //   _GlobalAlpha pushed per-batch via MaterialPropertyBlock
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _PosSize)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Extra)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Emoji)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LifeT)
            UNITY_INSTANCING_BUFFER_END(Props)

            // ── Structs ────────────────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  alpha      : TEXCOORD1;
            };

            // ── Helpers ────────────────────────────────────────────────────────

            float2 EmojiUV(float2 baseUV, float emojiIndex)
            {
                float cols  = max(1.0, _AtlasCols);
                float rows  = max(1.0, _AtlasRows);
                float tileW = 1.0 / cols;
                float tileH = 1.0 / rows;

                float x = fmod(emojiIndex, cols);
                float y = floor(emojiIndex / cols);

                if (_FlipY > 0.5)
                    y = (rows - 1.0) - y;

                return float2(x * tileW, y * tileH) + baseUV * float2(tileW, tileH);
            }

            // Cubic ease-out for smooth size growth
            float EaseOut(float t)
            {
                float u = 1.0 - t;
                return 1.0 - u * u * u;
            }

            float FadeInOut(float t)
            {
                float fin  = smoothstep(0.0, _FadeIn,  t);
                float fout = 1.0 - smoothstep(_FadeOut, 1.0, t);
                return saturate(fin * fout);
            }
            
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float4 posSize = UNITY_ACCESS_INSTANCED_PROP(Props, _PosSize);
                float4 extra   = UNITY_ACCESS_INSTANCED_PROP(Props, _Extra);
                float4 emojiV  = UNITY_ACCESS_INSTANCED_PROP(Props, _Emoji);
                float  t01     = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _LifeT).x);
                float  gAlpha  = _GlobalAlpha;

                float3 centerWS = posSize.xyz;
                float  size     = lerp(posSize.w, extra.x, EaseOut(t01));

                // Camera-aligned billboard axes from the view matrix
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;   // camera right in world space
                float3 up    = UNITY_MATRIX_I_V._m01_m11_m21;   // camera up    in world space

                float3 posWS = centerWS
                             + right * (IN.positionOS.x * size)
                             + up    * (IN.positionOS.y * size);

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = EmojiUV(IN.uv, emojiV.x);
                OUT.alpha      = gAlpha * FadeInOut(t01);
                return OUT;
            }

            // ── Fragment ───────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_AtlasTex, sampler_AtlasTex, IN.uv);
                c.a    *= IN.alpha;
                return c;
            }

            ENDHLSL
        }
    }
}
