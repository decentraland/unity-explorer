Shader "Voxelization/TwoSided"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            Name "TwoSided"
            
            // Render both front and back faces
            Cull Off
            ZWrite On
            ZTest LEqual
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag(v2f i, bool facing : SV_IsFrontFace) : SV_Target
            {
                // Blue for front-facing (outside view)
                // Red for back-facing (inside view)
                return facing ? fixed4(0, 0, 1, 1) : fixed4(1, 0, 0, 1);
            }
            ENDCG
        }
    }
}