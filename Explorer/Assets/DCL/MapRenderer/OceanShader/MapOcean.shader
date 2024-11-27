Shader "Unlit/MapOcean"
{
    Properties
    {
        _OceanShapes ("OceanShapes", 2D) = "white" {}
        _OceanGround ("OceanGround", 2D) = "white" {}
        _OceanNoise ("OceanNoise", 2D) = "white" {}
        _WaterLevel ("WaterLevel", Range (0.0, 2.0)) = 0.94
        _WaterLevelMultiplier ("Water Level Multiplier", Range (0.0, 1.0)) = 0.003
        _DeepWaterFadeDepth ("Deep Water Fade Depth", Range (0.0, 2.0)) = 0.6
        _DeepWaterFadeDepthMultiplier ("Deep Water Fade Depth Multiplier", Range (0.0, 2.0)) = 0.003
        _HeightUVMultiplier ("Height UV Multiplier", Range (0.0, 30.0)) = 15.0
        _coast2water_fadedepth ("coast2water_fadedepth", Range (0.0, 1.0)) = 0.10
        _large_waveheight("large_waveheight", Range (0.0, 1.0)) = 0.50 // change to adjust the "heavy" waves
        _large_wavesize("large_wavesize", Range (0.0, 10.0)) = 4.0 // factor to adjust the large wave size
        _small_waveheight("small_waveheight", Range (0.0, 1.0)) = 0.6  // change to adjust the small random waves
        _small_wavesize("small_wavesize", Range (0.0, 1.0)) = 0.5   // factor to ajust the small wave size
        _water_softlight_fact("water_softlight_fact", Range (0.0, 30.0)) = 15.0  // range [1..200] (should be << smaller than glossy-fact)
        _water_glossylight_fact("water_glossylight_fact", Range (0.0, 150.0)) = 120.0 // range [1..200]
        _particle_amount("particle_amount", Range (0.0, 100.0)) = 70.0
        _watercolor ("watercolor", Vector) = (0.43, 0.60, 0.66) // 'transparent' low-water color (RGB)
        _watercolor2 ("watercolor2", Vector) = (0.06, 0.07, 0.11) // deep-water color (RGB, should be darker than the low-water color)
        _water_specularcolor ("water_specularcolor", Vector) = (1.3, 1.3, 0.9)    // specular Color (RGB) of the water-highlights
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _OceanShapes;
            sampler2D _OceanGround;
            sampler2D _OceanNoise;
            float4 _OceanShapes_ST;
            float4 _OceanGround_ST;
            float4 _OceanNoise_ST;

            float _WaterLevelMultiplier;
            float _WaterLevel;
            float _DeepWaterFadeDepth;
            float _DeepWaterFadeDepthMultiplier;
            float _HeightUVMultiplier;
            float _coast2water_fadedepth;
            float _large_waveheight;
            float _large_wavesize;
            float _small_waveheight;
            float _small_wavesize;
            float _water_softlight_fact;
            float _water_glossylight_fact;
            float _particle_amount;
            float3 _watercolor;
            float3 _watercolor2;
            float3 _water_specularcolor;

            // 'hash' and 'noise' function by iq
            // License: Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
            //
            // V1.1  - added 'real' water height (hits terrain) and waterheight is visible on shadow
            //
            // CLICK and MOVE the MOUSE to:
            // X -> Change water height  /  Y -> Change water clarity.
            
            float3 light;
            
            // calculate random value
            float hash( float n )
            {
                return frac(sin(n)*43758.5453123f);
            }

            // 2d noise function
            float noise1( in float2 x )
            {
                float2 p  = floor(x);
                float2 f  = smoothstep(0.0f, 1.0f, frac(x));
                float n = p.x + p.y*57.0f;
                return lerp(lerp( hash(n+  0.0f), hash(n+  1.0f),f.x),
                lerp( hash(n+ 57.0f), hash(n+ 58.0f),f.x),f.y);
            }

            float noise(float2 p)
            {
                return tex2Dlod(_OceanNoise, float4(p.x * 1.0f/256.0f, p.y * 1.0f/256.0f, 0.0f, 0.0f)).x;
            }

            float height_map( float2 p )
            {
                float2x2 m = float2x2( 0.9563f*1.4f,  -0.2924f*1.4f,  0.2924f*1.4f,  0.9563f*1.4f );
                p = p*6.0f;
                float f = 0.6000f*noise1( p );p = mul(m,p)*1.1f;
                f += 0.2500f*noise1( p ); p = mul(m,p)*1.32f;
                f += 0.1666f*noise1( p ); p = mul(m,p)*1.11f;
                f += 0.0834f*noise( p ); p = mul(m,p)*1.12f;
                f += 0.0634f*noise( p ); p = mul(m,p)*1.13f;
                f += 0.0444f*noise( p ); p = mul(m,p)*1.14f;
                f += 0.0274f*noise( p ); p = mul(m,p)*1.15f;
                f += 0.0134f*noise( p ); p = mul(m,p)*1.16f;
                f += 0.0104f*noise( p ); p = mul(m,p)*1.17f;
                f += 0.0084f*noise( p );
            
                const float FLAT_LEVEL = 0.525f;
                if (f<FLAT_LEVEL)
                    f = f;
                else
                    f = pow((f-FLAT_LEVEL)/(1.0f-FLAT_LEVEL), 2.0f)*(1.0f-FLAT_LEVEL)*2.0f+FLAT_LEVEL; // makes a smooth coast-increase
                
                return clamp(f, 0.0f, 10.0f);
            }

            float3 terrain_map( float2 p )
            {
                return float3(0.7f, 0.55f, 0.4f)+tex2D(_OceanShapes, p * 2.0f).rgb*0.5f; // test-terrain is simply 'sandstone'
            }

            float water_map( float2 p, float height )
            {
                float2x2 m = float2x2( 0.72f, -1.60f,  1.60f,  0.72f );
                float2 p2 = p*_large_wavesize;
                float2 shift1 = 0.001f*float2( _Time.y * 160.0f * 2.0f, _Time.y * 120.0f * 2.0f );
                float2 shift2 = 0.001f*float2( _Time.y * 190.0f * 2.0f, -_Time.y * 130.0f * 2.0f );

                // coarse crossing 'ocean' waves...
                float f = 0.6000f*noise( p );
                f += 0.2500f*noise( mul(p,m) );
                f += 0.1666f*noise( mul(mul(p,m),m) );
                // f += 0.2500f*noise( mul(m,p) );
                // f += 0.1666f*noise( mul(m, mul(m,p)) );
                float wave = sin(p2.x*0.622f+p2.y*0.622f+shift2.x*4.269f)*_large_waveheight*f*height*height ;

                p *= _small_wavesize;
                f = 0.0f;
                float amp = 1.0f, s = 0.5f;
                for (int i=0; i<9; i++)
                {
                    p = mul(m,p)*0.947f;
                    f -= amp*abs(sin((noise( p+shift1*s )-0.5f)*2.0f));
                    amp = amp*0.59f;
                    s*=-1.329f;
                }

                return (wave+f)*_small_waveheight;
            }

            float nautic(float2 p)
            {
                float2x2 m = float2x2( 0.72f, -1.60f,  1.60f,  0.72f );
                p *= 18.0f;
                float f = 0.0f;
                float amp = 1.0f, s = 0.5f;
                for (int i=0; i<3; i++)
                {
                    p = mul(m,p)*1.2f;
                    f += amp*abs(smoothstep(0.0f, 1.0f, noise( p+_Time.y*s ))-0.5f);
                    amp = amp*0.5f;
                    s*=-1.227f;
                }
                return pow(1.0f-f, 5.0f);
            }

            float particles(float2 p)
            {
                float2x2 m = float2x2( 0.72f, -1.60f,  1.60f,  0.72f );
                p *= 200.0f;
                float f = 0.0f;
                float amp = 1.0f, s = 1.5f;
                for (int i=0; i<3; i++)
                {
                    p = mul(m,p)*1.2f;
                    f += amp*noise( p+_Time.y*s );
                    amp = amp*0.5f;
                    s*=-1.227f;
                }
                return pow(f*0.35f, 7.0f)*_particle_amount;
            }
            
            float test_shadow( float2 xy, float height)
            {
                float3 r0 = float3(xy, height);
                float3 rd = normalize( light - r0 );
                
                float hit = 1.0f;
                float t   = 0.001f;
                for (int j=1; j<25; j++)
                {
                    float3 p = r0 + t*rd;
                    float h = height_map( p.xy );
                    float height_diff = p.z - h;
                    if (height_diff<0.0f)
                    {
                        return 0.0f;
                    }
                    t += 0.01f+height_diff*0.02f;
                    hit = min(hit, 2.0f*height_diff/t); // soft shaddow   
                }
                return hit;
            }

            float3 CalcTerrain(float2 uv, float height)
            {
                float3 col = terrain_map( uv );
                float h1 = height_map(uv-float2(0.0f, 0.01f));
                float h2 = height_map(uv+float2(0.0f, 0.01f));
                float h3 = height_map(uv-float2(0.01f, 0.0f));
                float h4 = height_map(uv+float2(0.01f, 0.0f));
                float3 norm = normalize(float3(h3-h4, h1-h2, 1.0f));
                float3 r0 = float3(uv, height);
                float3 rd = normalize( light - r0 );
                float grad = dot(norm, rd);
                col *= grad+pow(grad, 8.0f);
                float terrainshade = 1.0f;//test_shadow( uv, height );
                col = lerp(col*0.25f, col, terrainshade);
                return col;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _OceanShapes);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                light = float3(-0.0f, sin(_Time.y*0.5f)*0.5f + 0.35f, 2.8f); // position of the sun
	            float2 uv = i.uv;//(i.uv.xy / _ScreenParams.xy - float2(-0.12f, 0.25f));

		        float WATER_LEVEL = _WaterLevel * _WaterLevelMultiplier;
                float deepwater_fadedepth = (_DeepWaterFadeDepth * _DeepWaterFadeDepthMultiplier) + _coast2water_fadedepth;
                
                float height = height_map( uv );
                float3 col;
                
                float waveheight = clamp(WATER_LEVEL*3.0f-1.5f, 0.0f, 1.0f);
                float level = WATER_LEVEL + 0.2f*water_map(uv*15.0f + float2(_Time.y*0.1f, _Time.y*0.1f), waveheight);
                if (height > level || false)
                {
                    col = CalcTerrain(uv, height);
                }
                
                if (height <= level)
                {
                    float2 dif = float2(0.0f, 0.01f);
                    float2 pos = uv * _HeightUVMultiplier + float2(_Time.y * 0.01f, _Time.y * 0.01f);
                    float h1 = water_map(pos-dif,waveheight);
                    float h2 = water_map(pos+dif,waveheight);
                    float h3 = water_map(pos-dif.yx,waveheight);
                    float h4 = water_map(pos+dif.yx,waveheight);
                    float3 normwater = normalize(float3(h3-h4, h1-h2, 0.125f)); // norm-vector of the 'bumpy' water-plane
                    uv += normwater.xy*0.002f*(level-height); 
                    
                    col = CalcTerrain(uv, height);

                    float coastfade = clamp((level-height)/_coast2water_fadedepth, 0.0f, 1.0f);
                    float coastfade2= clamp((level-height)/deepwater_fadedepth, 0.0f, 1.0f);
                    float intensity = col.r*0.2126f+col.g*0.7152f+col.b*0.0722f;
                    _watercolor = lerp(_watercolor*intensity, _watercolor2, smoothstep(0.0f, 1.0f, coastfade2));

                    float3 r0 = float3(uv, WATER_LEVEL);
                    float3 rd = normalize( light - r0 ); // ray-direction to the light from water-position
                    float grad     = dot(normwater, rd); // dot-product of norm-vector and light-direction
                    float specular = pow(grad, _water_softlight_fact);  // used for soft highlights                          
                    float specular2= pow(grad, _water_glossylight_fact); // used for glossy highlights
                    float gradpos  = dot(float3(0.0f, 0.0f, 1.0f), rd);
                    float specular1= smoothstep(0.0f, 1.0f, pow(gradpos, 5.0f));  // used for diffusity (some darker corona around light's specular reflections...)                          
                    float watershade  = 1.0f;//test_shadow( uv, level );
                    _watercolor *= 2.2f+watershade;
   		            _watercolor += (0.2f+0.8f*watershade) * ((grad-1.0f)*0.5f+specular) * 0.25f;
   		            _watercolor /= (1.0f+specular1*1.25f);
   		            _watercolor += watershade*specular2*_water_specularcolor;
                    _watercolor += watershade*coastfade*(1.0f-coastfade2)*(float3(0.5f, 0.6f, 0.7f)*nautic(uv)+float3(1.0f, 1.0f, 1.0f)*particles(uv));
                    
                    col = lerp(col, _watercolor, coastfade);
                }
                
	            return float4(col.xyz , 1.0f);
            }
            ENDCG
        }
    }
}
