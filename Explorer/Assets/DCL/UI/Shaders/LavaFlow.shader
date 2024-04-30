Shader "Custom/LavaFlow" 
{
    Properties 
    {
        _Color1 ("Color 1", Color) = (.957, .804, .623, 1)
        _Color2 ("Color 2", Color) = (.192, .384, .933, 1)
        _Color3 ("Color 3", Color) = (.910, .510, .800, 1)
        _Color4 ("Color 4", Color) = (0.350, .71, .953, 1)
        _Speed ("Speed", float) = 1
        _Frequency ("Frequency", float) = 5
        _Amplitude ("Amplitude", float) = 30
    }
    SubShader 
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass 
        {
            CGPROGRAM
            #pragma vertex vertexShader
            #pragma fragment fragmentShader

            float4 _Color1; 
            float4 _Color2;
            float4 _Color3;
            float4 _Color4;
            float _Alpha;
            float _Speed;
            float _Frequency;
            float _Amplitude;

            struct mesh 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };

            struct interpolator 
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR; 
            };

            interpolator vertexShader (mesh m) 
            {
                interpolator o;
                o.vertex = UnityObjectToClipPos(m.vertex); 
                o.uv = m.uv;
                o.color = m.color;
                return o;
            }

            //Generates a pseudo-random value based on a 2D point
            float2 hash(float2 p) 
            {
                p = float2(dot(p,float2(127.1,311.7)), dot(p,float2(269.5,183.3)));
                return frac(sin(p)*43758.5453);
            }

            //Generates Perlin-like noise based on a 2D point
            float noise(float2 p) 
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Smooth interpolation
                float2 u = f*f*(3.0-2.0*f);

                // Mix the hashes and interpolate
                float n = lerp(
                    lerp(dot(-1.0+2.0*hash(i + float2(0.0,0.0)), f - float2(0.0,0.0)), 
                        dot(-1.0+2.0*hash(i + float2(1.0,0.0)), f - float2(1.0,0.0)), u.x),
                    lerp(dot(-1.0+2.0*hash(i + float2(0.0,1.0)), f - float2(0.0,1.0)), 
                        dot(-1.0+2.0*hash(i + float2(1.0,1.0)), f - float2(1.0,1.0)), u.x), 
                        u.y);

                // Scale and bias to [0, 1]
                return 0.5 + 0.5*n; 
            }

            // Returns a rotation matrix for a given angle
            float2x2 rotate(float a) 
            {
                float s = sin(a);
                float c = cos(a);
                return float2x2(c, -s, s, c);
            }


            float4 fragmentShader (interpolator i) : SV_Target 
            {
                float2 uv = i.uv;
                float ratio = _ScreenParams.x / _ScreenParams.y;

                // Adjust UVs and apply noise-based rotation
                float2 tuv = uv - 0.5;
                float degree = noise(float2(_Time.y*.1, tuv.x*tuv.y));

                tuv.y *= 1./ratio;
                tuv = mul(tuv, rotate(radians((degree-.5)*720.0+180.0)));
                tuv.y *= ratio;

                // Animate the UVs based on time
                float speed = _Time.y * _Speed;
                tuv.x += sin(tuv.y * _Frequency + speed) / _Amplitude;
                tuv.y += sin(tuv.x * _Frequency * 1.5 + speed) / (_Amplitude * 0.5);

                // Mix colors based on adjusted UVs
                float4 layer1 = lerp(_Color1, _Color2, smoothstep(-.3, .2, mul(tuv, rotate(radians(-5.0))).x));
                float4 layer2 = lerp(_Color3, _Color4, smoothstep(-.3, .2, mul(tuv, rotate(radians(-5.0))).x));
                float4 finalComp = lerp(layer1, layer2, smoothstep(.5, -.3, tuv.y));
                finalComp.a = i.color.a; 

                return finalComp;
            }
            ENDCG
        }
    }
}