Shader "DCL/EquirectToCube"
{
    HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "HLSLSupport.cginc"
    ENDHLSL

    Properties
    {
        _MainTex ("Equirectangular", 2D) = "black" {}
        _CubemapFace ("Cubemap Face", Float) = 0.0
    }
    SubShader
    {
        Pass
        {
            Name "DCL_EquirectToCube"

            ZTest Off
            ZWrite Off
            Cull Off
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                float _CubemapFace;

                float3 ComputeCubeDirection(float2 globalTexcoord)
                {
                    float2 xy = (globalTexcoord * 2.0) - 1.0;

                    float3 direction;

                    if(_CubemapFace == 0.0f)
                    {
                        direction = (float3(1.0, -xy.y, -xy.x));
                    }
                    else if(_CubemapFace == 1.0f)
                    {
                        direction = (float3(-1.0, -xy.y, xy.x));
                    }
                    else if(_CubemapFace == 2.0f)
                    {
                        direction = (float3(xy.x, 1.0, xy.y));
                    }
                    else if(_CubemapFace == 3.0f)
                    {
                        direction = (float3(xy.x, -1.0, -xy.y));
                    }
                    else if(_CubemapFace == 4.0f)
                    {
                        direction = (float3(xy.x, -xy.y, 1.0));
                    }
                    else if(_CubemapFace == 5.0f)
                    {
                        direction = (float3(-xy.x, -xy.y, -1.0));
                    }
                    else
                    {
                        direction = float3(0, 0, 0);
                    }
                    return direction;
                }

                struct appdata_equirect
                {
                    uint vertexID : SV_VertexID;
                };

                struct v2f {
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                    float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap
                };

                v2f vert(appdata_equirect IN)
                {
                    v2f OUT;
                    uint primitiveID = IN.vertexID / 3;
                    uint vertexID = IN.vertexID % 3;

                    #if UNITY_UV_STARTS_AT_TOP
                        const float2 vertexPositions[3] =
                        {
                            { -1.0f,  3.0f },
                            { -1.0f, -1.0f },
                            {  3.0f, -1.0f }
                        };

                        const float2 texCoords[3] =
                        {
                            { 0.0f, -1.0f },
                            { 0.0f, 1.0f },
                            { 2.0f, 1.0f }
                        };
                    #else
                        const float2 vertexPositions[3] =
                        {
                            {  3.0f,  3.0f },
                            { -1.0f, -1.0f },
                            { -1.0f,  3.0f }
                        };

                        const float2 texCoords[3] =
                        {
                            { 2.0f, 1.0f },
                            { 0.0f, -1.0f },
                            { 0.0f, 1.0f }
                        };
                    #endif

                    float2 pos = vertexPositions[vertexID];
                    OUT.vertex = float4(pos, 0.0, 1.0);
                    OUT.primitiveID = primitiveID;
                    OUT.localTexcoord = float3(texCoords[vertexID], 0.0f);
                    OUT.globalTexcoord = float3(pos.xy * 0.5 + 0.5, 1.0);
                    #if UNITY_UV_STARTS_AT_TOP
                        OUT.globalTexcoord.y = 1.0 - OUT.globalTexcoord.y;
                    #endif
                    OUT.direction = ComputeCubeDirection(OUT.globalTexcoord.xy);
                    return OUT;
                }

                sampler2D _MainTex;

                // Latitude-longitude lookup: u wraps around +Z (image centre), v runs from -Y (bottom row) to +Y (top row).
                float4 frag(v2f IN) : SV_Target
                {
                    float3 d = normalize(IN.direction);
                    float2 uv = float2(atan2(d.x, d.z) / (2.0 * UNITY_PI) + 0.5, asin(d.y) / UNITY_PI + 0.5);
                    return float4(tex2D(_MainTex, uv).rgb, 1.0);
                }
            ENDHLSL
        }
    }
}
