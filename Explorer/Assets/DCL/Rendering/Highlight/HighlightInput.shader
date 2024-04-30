Shader "Highlight/HighlightInput"
{
    Properties
    {

    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 norm : NORMAL;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert( appdata_base v )
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.norm = COMPUTE_VIEW_NORMAL;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                return float4( (i.norm.xyz + 1.0f) * 0.5f, 1.0f);
            }
            ENDCG
        }
    }

    Fallback Off
}