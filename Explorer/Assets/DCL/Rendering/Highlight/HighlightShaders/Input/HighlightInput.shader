Shader "DCL/HighlightInput_Override"
{
    Properties
    {
        _HighlightObjectOffset ("Highlight Object Offset", Vector) = (0.0, 100.0, 0.0, 0.0)
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
                // o.pos = UnityObjectToClipPos(v.vertex);
                // return o;
                

                float4 objPos = mul ( unity_ObjectToWorld, float4(0,0,0,1) );
                float Set_Outline_Width = (_Outline_Width*0.001*smoothstep( _Farthest_Distance, _Nearest_Distance, distance(objPos.rgb,_WorldSpaceCameraPos) )).r;
                Set_Outline_Width *= (1.0f - _ZOverDrawMode);

                float4 _ClipCameraPos = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos.xyz, 1));
                
                #if defined(UNITY_REVERSED_Z)
                    _Offset_Z = _Offset_Z * -0.01;
                #else
                    _Offset_Z = _Offset_Z * 0.01;
                #endif
                
                Set_Outline_Width = Set_Outline_Width*50;
                float signVar = dot(normalize(v.vertex.xyz),normalize(v.normal))<0 ? -1 : 1;
                float4 vertOffset = _HighlightObjectOffset;
                //vertOffset = float4(0.0f, 0.0f, 0.0f, 0.0f);
                #ifdef _DCL_COMPUTE_SKINNING
                    float4 vVert = float4(_GlobalAvatarBuffer[_lastAvatarVertCount + _lastWearableVertCount + v.index].position.xyz, 1.0f);
                    o.pos = UnityObjectToClipPos(float4(vVert.xyz + signVar*normalize(vVert - vertOffset)*Set_Outline_Width, 1));
                #else
                    o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + signVar*normalize(v.vertex)*Set_Outline_Width, 1));
                #endif

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