Shader "DCL/UI/LobbyAmbient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AmbientColor ("Ambient particles", Color) = (1,0.87,0.65,0.22)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color, _AmbientColor;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                float2 drift = i.uv * float2(12,7) + float2(_Time.y * .009, -_Time.y * .016);
                float2 cell = floor(drift);
                float seed = frac(sin(dot(cell, float2(127.1,311.7))) * 43758.5453);
                float2 position = frac(drift) - float2(seed, frac(seed * 31.3));
                float mote = (1 - smoothstep(.008, .026, length(position))) * smoothstep(.4,.8,seed);
                color.rgb += _AmbientColor.rgb * mote * _AmbientColor.a;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
