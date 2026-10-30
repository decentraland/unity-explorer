Shader "DCL/HighlightInput_Override"
{
    Properties
    {
        _HighlightObjectOffset ("Highlight Object Offset", Vector) = (0.0, 0.0, 0.0, 0.0)
        _HighlightColour ("Highlight Colour", Color) = (0,1,0,1)
        _HighlightWidth ("Highlight Width", Float) = 1.0
        _Outline_Width ("Outline_Width", Float ) = 2
        _Farthest_Distance ("Farthest_Distance", Float ) = 100
        _Nearest_Distance ("Nearest_Distance", Float ) = 0.5
        [Enum(OFF, 0, ON, 1)]	_ZOverDrawMode("ZOver Draw Mode", Float) = 0  //OFF/ON
        _Offset_Z ("Offset_Camera_Z", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #include "UnityCG.cginc"

            float4 _HighlightObjectOffset;
            float4 _HighlightColour;
            float _HighlightWidth;
            float _Outline_Width;
            float _Farthest_Distance;
            float _Nearest_Distance;
            float _ZOverDrawMode;
            float _Offset_Z;

            #define IDENTITY_MATRIX float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)

            float4x4 m_scale(float4x4 m, float3 v)
            {
                float x = v.x, y = v.y, z = v.z;

                m[0][0] *= x; m[1][0] *= y; m[2][0] *= z;
                m[0][1] *= x; m[1][1] *= y; m[2][1] *= z;
                m[0][2] *= x; m[1][2] *= y; m[2][2] *= z;
                m[0][3] *= x; m[1][3] *= y; m[2][3] *= z;

                return m;
            }
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert( appdata_base v )
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                //float4 clipPosition = UnityObjectToClipPos(v.vertex);
                float4x4 scaledModel = IDENTITY_MATRIX;
                //scaledModel = m_scale(scaledModel, float3(1.1f, 1.1f, 1.1f));
                float4 clipPosition = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, mul(scaledModel, float4(v.vertex.xyz + _HighlightObjectOffset, 1.0))));
                float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, mul((float3x3) UNITY_MATRIX_M, v.normal));

                float _OutlineWidth = _Outline_Width;
                //_OutlineWidth = 6.0f;
                //float2 offset = normalize(clipNormal.xy) * _OutlineWidth * clipPosition.w;
                float2 offset = normalize(clipNormal.xy) / _ScreenParams.xy * _OutlineWidth * clipPosition.w * 2.0f;
                clipPosition.xy += offset;

                o.pos = clipPosition;
                return o;
                
                // o.pos = UnityObjectToClipPos(v.vertex);
                // return o;
                
                float4x4 scaleMat = unity_ObjectToWorld;
                float4 objPos = mul ( scaleMat, float4(0,0,0,1) );
                float Set_Outline_Width = (_Outline_Width*0.001*smoothstep( _Farthest_Distance, _Nearest_Distance, distance(objPos.rgb,_WorldSpaceCameraPos) )).r;
                Set_Outline_Width *= (1.0f - _ZOverDrawMode);

                float4 _ClipCameraPos = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos.xyz, 1));
                
                #if defined(UNITY_REVERSED_Z)
                    _Offset_Z = _Offset_Z * -0.01;
                #else
                    _Offset_Z = _Offset_Z * 0.01;
                #endif
                
                Set_Outline_Width = _Outline_Width * 0.1f;//Set_Outline_Width*50;
                float signVar = dot(normalize(v.vertex.xyz),normalize(v.normal))<0 ? -1 : 1;
                float4 vertOffset = _HighlightObjectOffset;
                //vertOffset = float4(0.0f, 0.0f, 0.0f, 0.0f);
                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + signVar*normalize(v.vertex + vertOffset)*Set_Outline_Width, 1));
                o.pos.z = o.pos.z + _Offset_Z * _ClipCameraPos.z;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                return float4(_HighlightColour);
            }
            ENDCG
        }
    }

    Fallback Off
}