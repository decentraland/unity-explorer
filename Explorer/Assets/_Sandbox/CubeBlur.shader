Shader "DCL/CubeBlur"
{
    HLSLINCLUDE
        #include "UnityCG.cginc"
    ENDHLSL

    Properties 
    {
        _MainTex ("Main", CUBE) = "" {}
        _TexelSize ("Texel", Float) = 0.0156250//0.0078125
        _MipLevel ("Level", Float) = 0.0
        _BlurScale ("Scale", Float) = 1.0
        _Current_CubeFace ("Current_CubeFace", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            // "Queue"="Background"
            // "RenderType"="Background"
            // "PreviewType"="Skybox"
        }
        
        Pass
        {
            Name "DCL_CubeBlur"

            ZTest Off
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma enable_d3d11_debug_symbols
                #pragma target 5.0
                
                float _Current_CubeFace;
                
                float3 ComputeCubeDirection(float2 globalTexcoord)
                {
                    float2 xy = (globalTexcoord * 2.0) - 1.0;
                    
                    float3 direction;

                    if(_Current_CubeFace == 0.0f)
                    {
                        direction = (float3(1.0, -xy.y, -xy.x));
                    }
                    else if(_Current_CubeFace == 1.0f)
                    {
                        direction = (float3(-1.0, -xy.y, xy.x));
                    }
                    else if(_Current_CubeFace == 2.0f)
                    {
                        direction = (float3(xy.x, 1.0, xy.y));
                    }
                    else if(_Current_CubeFace == 3.0f)
                    {
                        direction = (float3(xy.x, -1.0, -xy.y));
                    }
                    else if(_Current_CubeFace == 4.0f)
                    {
                        direction = (float3(xy.x, -xy.y, 1.0));
                    }
                    else if(_Current_CubeFace == 5.0f)
                    {
                        direction = (float3(-xy.x, -xy.y, -1.0));
                    }
                    else
                    {
                        direction = float3(0, 0, 0);
                    }
                    return direction;
                }
                
                struct appdata_blur
                {
                    uint vertexID : SV_VertexID;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
                
                struct v2f {
                    // half4 pos : SV_POSITION;
                    // half4 uvw : TEXCOORD0;
                    float4 vertex           : SV_POSITION;
                    float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
                    float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
                    uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
                    float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap

                };

                v2f vert(appdata_blur IN)
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
                    // o.pos = UnityObjectToClipPos(v.vertex);
                    // o.uvw = v.texcoord;
                    // return o;
                }

                UNITY_DECLARE_TEXCUBE(_MainTex);
                //SAMPLER(_MainTex_point_clamp_sampler);
                SamplerState _MainTex_point_clamp_sampler;

                #define UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(tex, dir,  lod) max(half4(0.0, 0.0, 0.0, 0.0), tex.SampleLevel(_MainTex_point_clamp_sampler, dir, lod))
                //#define UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(tex, dir,  lod) max(half4(0.0, 0.0, 0.0, 0.0), UNITY_SAMPLE_TEXCUBE_LOD(tex, dir, lod))
                
                half _MipLevel; // Workaround for Metal driver bug: please keep this uniform aligned to 4 bytes (case 899153)
                half _TexelSize;
                half _BlurScale;

                #define zero    half3(0., 0., 0.)
                #define one     half3(1., 1., 1.)
                #define two     half3(2., 2., 2.)

                half3 fold(half3 st, half3 face)
                {
                    half3 c = min(max(st, -one), one);
                    half3 f = abs(st - c);
                    half m = max(max(f.x, f.y), f.z);
                    return c - m*face;
                }

                half3 gauss(half d)
                {
                    // compute coefficients for positions .5*d/.5, 1.5*d/.5 and 2.5*d/.5
                    // this assumes a sigma of .5 for a density of 1.
                    half3 v = half3(d, 3.*d, 5.*d)*_BlurScale;
                    return exp(-v*v);
                }

                half4 frag_default(v2f IN)
                {
                    half3 st;

                    //half4 pos = IN.;
                    half3 uvw = normalize(IN.direction);
                    
                    half3 face = lerp(zero, uvw.xyz, abs(uvw.xyz)==one);
                    half3 u = face.zxy*_TexelSize;
                    half3 v = face.yzx*_TexelSize;
                    half4 s = half4(uvw.xyz*(one - abs(face)), 0.);

                    // modulate coefficients based on position (texel density on projected sphere)
                    half w = 1. / sqrt(1. + dot(s.xyz, s.xyz));
                    half3 C = gauss(w*w*w);

                    half4 s1, s2, s3;
                    half3 c;

                    half3 up1 = fold(uvw.xyz + 1.5*u, face);
                    half3 um1 = fold(uvw.xyz - 1.5*u, face);
                    half3 up2 = fold(uvw.xyz + 2.5*u, face);
                    half3 um2 = fold(uvw.xyz - 2.5*u, face);

                    half3 vp1 = fold(uvw.xyz + 1.5*v, face);
                    half3 vm1 = fold(uvw.xyz - 1.5*v, face);
                    half3 vp2 = fold(uvw.xyz + 2.5*v, face);
                    half3 vm2 = fold(uvw.xyz - 2.5*v, face);

                    s = 0.;
                    w = 0.;

                    // first row

                    c = C.xyz*C.zzz;

                    st = uvw.xyz - 2.5*u - 2.5*v;
                    st = fold(st, face);
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 1.5*u - 2.5*v;
                    st = fold(st, face);
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vm2 - .5*u;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vm2 + .5*u;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 1.5*u - 2.5*v;
                    st = fold(st, face);
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 2.5*u - 2.5*v;
                    st = fold(st, face);
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    // second row

                    c = C.xyz*C.yyy;

                    st = uvw.xyz + 2.5*u - 1.5*v;
                    st = fold(st, face);
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 1.5*u - 1.5*v;
                    st = fold(st, face);
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vm1 + .5*u;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vm1 - .5*u;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 1.5*u - 1.5*v;
                    st = fold(st, face);
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 2.5*u - 1.5*v;
                    st = fold(st, face);
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    // third row

                    c = C.xyz*C.xxx;

                    st = um2 - .5*v;
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = um1 - .5*v;
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - .5*u - .5*v;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + .5*u - .5*v;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = up1 - .5*v;
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = up2 - .5*v;
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    // fourth row

                    c = C.xyz*C.xxx;

                    st = up2 + .5*v;
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = up1 + .5*v;
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + .5*u + .5*v;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - .5*u + .5*v;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = um1 + .5*v;
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = um2 + .5*v;
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    // fifth row

                    c = C.xyz*C.yyy;

                    st = uvw.xyz - 2.5*u + 1.5*v;
                    st = fold(st, face);
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 1.5*u + 1.5*v;
                    st = fold(st, face);
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vp1 - .5*u;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vp1 + .5*u;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 1.5*u + 1.5*v;
                    st = fold(st, face);
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 2.5*u + 1.5*v;
                    st = fold(st, face);
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    // sixth row

                    c = C.xyz*C.zzz;

                    st = uvw.xyz + 2.5*u + 2.5*v;
                    st = fold(st, face);
                    s3 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz + 1.5*u + 2.5*v;
                    st = fold(st, face);
                    s2 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vp2 + .5*u;
                    s1 = UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = vp2 - .5*u;
                    s1 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 1.5*u + 2.5*v;
                    st = fold(st, face);
                    s2 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    st = uvw.xyz - 2.5*u + 2.5*v;
                    st = fold(st, face);
                    s3 += UNITY_SAMPLE_TEXCUBE_LOD_CLAMPED(_MainTex, st, _MipLevel);

                    w += dot(c, two);
                    s1 = c.x*s1 + c.y*s2;
                    s += c.z*s3;
                    s += s1;

                    return s/w;
                }

                half4 frag_loop(v2f IN)
                {
                    half3 uvw = IN.direction;
                    half3 face = lerp(zero, uvw.xyz, abs(uvw.xyz) == one);
                    half3 u = face.zxy*_TexelSize;
                    half3 v = face.yzx*_TexelSize;
                    half3 s = uvw.xyz*(one - abs(face));

                    // modulate coefficients based on position (texel density on projected sphere)
                    half w = 1. / sqrt(1. + dot(s, s));
                    half3 C = gauss(w*w*w);

                    half3 accuColor = half3(0.0, 0.0, 0.0);
                    half accuWeight = 0.0;

                    for (int y = 2; y >= 0; --y)
                    {
                        for (int iy = 0; iy < 2; iy++)
                        {
                            half ySign = iy * 2 - 1;
                            half fy = ySign * (half(y)+0.5h);

                            for (int x = 2; x >= 0; --x)
                            {
                                half3 rgb = half3(0.0, 0.0, 0.0);
                                UNITY_UNROLL for (int ix = 0; ix < 2; ix++)
                                {
                                    half xSign = ix * 2 - 1;
                                    half fx = xSign * (half(x) + 0.5h);

                                    half3 uvwTemp = uvw.xyz + fx * u + fy * v;
                                    uvwTemp = fold(uvwTemp, face);
                                    rgb += UNITY_SAMPLE_TEXCUBE_LOD(_MainTex, uvwTemp, _MipLevel).rgb;
                                }

                                half weight = C[x] * C[y];
                                accuColor += rgb * weight;
                                accuWeight += weight * 2;
                            }
                        }
                    }

                    half4 result = half4(accuColor / accuWeight, 1.0);

                    return result;
                }

                half4 frag_noblur(v2f IN)
                {
                    half3 uvw = IN.direction;
                    return UNITY_SAMPLE_TEXCUBE_LOD(_MainTex, uvw.xyz, _MipLevel);
                }

                half4 frag(v2f  i) : SV_Target
                {
                    //return half4(normalize(i.direction) * 0.5 + 0.5, 1.0);
                    //return frag_default(i);
                    //return half4(i.direction, 1.0);
                    //return frag_default(i);
                    return frag_loop(i);
                    //return half4(1.0, 0.0, 0.0, 1.0);
                    #if (SHADER_TARGET < 30 || defined(SHADER_API_GLES))
                        return frag_noblur(i);
                    #elif defined(SHADER_API_GLES3) || (defined(SHADER_API_VULKAN) && defined(SHADER_API_MOBILE))
                        // frag_default uses too many registers for Mali, register spilling makes it load/store bound
                        // This path also does not blur the alpha channel and it does not clamp the sampled color to values >= 0
                        return frag_loop(i);
                    #else
                        // TODO: check if frag_loop performs well on all platforms so it could replace frag_default
                        return frag_default(i);
                    #endif
                }
            ENDHLSL
        }
    }
}
