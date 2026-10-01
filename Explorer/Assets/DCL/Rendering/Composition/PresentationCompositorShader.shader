Shader "DCL/PresentationCompositor"
{
    Properties
    {
        _MainTex ("Slide", 2D) = "black" {}
        _VideoTex ("Video", 2D) = "black" {}
        _CameraTex ("Camera", 2D) = "black" {}
        _VideoRect ("Video rect (x, y, w, h; top-left origin, normalized)", Vector) = (0, 0, 0, 0)
        _CameraRect ("Camera rect (left, top, w, h; top-left origin, normalized)", Vector) = (0, 0, 0, 0)
        _VideoEnabled ("Video rect enabled (filled black)", Float) = 0
        _VideoTexEnabled ("Video frame available", Float) = 0
        _CameraEnabled ("Camera enabled", Float) = 0
        _CameraEdge ("Circle edge softness in circle-local units", Float) = 0.01
        _SlideSize ("Slide size in px (x, y)", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _VideoTex;
            sampler2D _CameraTex;
            float4 _VideoTex_TexelSize;
            float4 _CameraTex_TexelSize;
            float4 _VideoRect;
            float4 _CameraRect;
            float _VideoEnabled;
            float _VideoTexEnabled;
            float _CameraEnabled;
            float _CameraEdge;
            float4 _SlideSize;

            bool Inside(float2 p, float2 origin, float2 size)
            {
                float2 local = p - origin;
                return all(local >= 0) && all(local <= size);
            }

            float4 ContainRect(float4 r)
            {
                float2 slideSize = _SlideSize.xy;
                float rectAspect = (r.z * slideSize.x) / (r.w * slideSize.y);
                float videoAspect = _VideoTex_TexelSize.z / _VideoTex_TexelSize.w;

                float2 size = videoAspect > rectAspect
                    ? float2(r.z, r.z * slideSize.x / videoAspect / slideSize.y)
                    : float2(r.w * slideSize.y * videoAspect / slideSize.x, r.w);

                return float4(r.xy + (r.zw - size) * 0.5, size);
            }

            float2 CoverSquareUv(float2 q)
            {
                float cw = _CameraTex_TexelSize.z;
                float ch = _CameraTex_TexelSize.w;

                return cw > ch
                    ? float2(0.5 + (q.x - 0.5) * ch / cw, q.y)
                    : float2(q.x, 0.5 + (q.y - 0.5) * cw / ch);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = float2(i.uv.x, 1.0 - i.uv.y);
                fixed4 color = tex2D(_MainTex, i.uv);

                if (_VideoEnabled > 0.5 && Inside(p, _VideoRect.xy, _VideoRect.zw))
                {
                    color = fixed4(0, 0, 0, 1);

                    if (_VideoTexEnabled > 0.5)
                    {
                        float4 content = ContainRect(_VideoRect);

                        if (Inside(p, content.xy, content.zw))
                            color = tex2Dlod(_VideoTex, float4((p - content.xy) / content.zw, 0, 0));
                    }
                }

                if (_CameraEnabled > 0.5)
                {
                    float2 q = (p - _CameraRect.xy) / _CameraRect.zw;
                    float dist = length(q - 0.5);
                    float mask = 1.0 - smoothstep(0.5 - _CameraEdge, 0.5, dist);

                    if (mask > 0)
                        color.rgb = lerp(color.rgb, tex2Dlod(_CameraTex, float4(CoverSquareUv(q), 0, 0)).rgb, mask);
                }

                return fixed4(color.rgb, 1);
            }
            ENDCG
        }
    }
}
