Shader "Hidden/UUAV/NV12ToRGB"
{
    Properties
    {
        _YTex ("Y plane", 2D) = "black" {}
        _UVTex ("UV plane", 2D) = "gray" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _YTex;
            sampler2D _UVTex;

            // rows of the two transforms uuav_player_get_frame_info publishes:
            // _Yuv* the stream's matrix coefficients, range and bit depth, _Uv*
            // the vertical flip composed with rotation and the visible box
            float4 _YuvR, _YuvG, _YuvB;
            float4 _UvX, _UvY;

            float4 frag(v2f_img i) : SV_Target
            {
                float3 quad = float3(i.uv, 1.0);
                float2 uv = float2(dot(_UvX.xyz, quad), dot(_UvY.xyz, quad));
                float4 yuv = float4(tex2D(_YTex, uv).r, tex2D(_UVTex, uv).rg, 1.0);
                float3 rgb = saturate(float3(dot(_YuvR, yuv), dot(_YuvG, yuv), dot(_YuvB, yuv)));
                // video RGB is display-referred (BT.709 OETF ~ sRGB); the
                // linear pipeline expects scene-linear samples, the sRGB
                // render target re-encodes on store
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif
                return float4(rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
