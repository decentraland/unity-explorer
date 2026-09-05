Shader "DCL/UI/Waves"
{
    Properties
    {
        _ColorMask ("Color Mask", Float) = 15
        _Color ("Tint", Color) = (1,1,1,1)
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
        ColorMask [_ColorMask]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
            };

            static const float overallSpeed = 0.2;
            static const float gridSmoothWidth = 0.015;
            static const float axisWidth = 0.05;
            static const float majorLineWidth = 0.025;
            static const float minorLineWidth = 0.0125;
            static const float majorLineFrequency = 5.0;
            static const float minorLineFrequency = 1.0;
            static const float scale = 5.0;
            static const float4 lineColor = float4(0.25, 0.5, 1.0, 1.0);
            static const float minLineWidth = 0.02;
            static const float maxLineWidth = 0.5;
            static const float lineSpeed = 1.0 * overallSpeed; // overall speed
            static const float lineAmplitude = 1.0;
            static const float lineFrequency = 0.2;
            static const float warpSpeed = 0.2 * overallSpeed; // overall speed
            static const float warpFrequency = 0.5;
            static const float warpAmplitude = 1.0;
            static const float offsetFrequency = 0.5;
            static const float offsetSpeed = 1.33 * overallSpeed; // overall speed
            static const float minOffsetSpread = 0.6;
            static const float maxOffsetSpread = 2.0;
            static const int linesPerGroup = 16;

            static const float4 bgColors[2] = {
                lineColor * 0.5,
                lineColor - float4(0.2, 0.2, 0.7, 1)
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;

            // Vertex Shader
            v2f vert(appdata_t v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            #define drawCircle(pos, radius, coord) smoothstep(radius + gridSmoothWidth, radius, length(coord - (pos)))

            #define drawSmoothLine(pos, halfWidth, t) smoothstep(halfWidth, 0.0, abs(pos - (t)))

            #define drawCrispLine(pos, halfWidth, t) smoothstep(halfWidth + gridSmoothWidth, halfWidth, abs(pos - (t)))

            #define drawPeriodicLine(freq, width, t) drawCrispLine(freq / 2.0, width, abs(fmod(t, freq) - (freq) / 2.0))

            float drawGridLines(float axis)
            {
                return drawCrispLine(0.0, axisWidth, axis)
                    + drawPeriodicLine(majorLineFrequency, majorLineWidth, axis)
                    + drawPeriodicLine(minorLineFrequency, minorLineWidth, axis);
            }

            float drawGrid(float2 space)
            {
                return min(1., drawGridLines(space.x) + drawGridLines(space.y));
            }

            // probably can optimize w/ noise, but currently using fourier transform
            float random(float t)
            {
                return (cos(t) + cos(t * 1.3 + 1.3) + cos(t * 1.4 + 1.4)) / 3.0;
            }

            float getPlasmaY(float x, float horizontalFade, float offset)
            {
                return random(x * lineFrequency + _Time.y * lineSpeed) * horizontalFade * lineAmplitude + offset;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.position.xy / _ScreenParams.xy;
                float2 space = (i.position.xy - _ScreenParams.xy / 2.0) / _ScreenParams.x * 2.0 * scale;

                float horizontalFade = 1.0 - (cos(uv.x * 6.28) * 0.5 + 0.5);
                float verticalFade = 1.0 - (cos(uv.y * 6.28) * 0.5 + 0.5);

                // fun with nonlinear transformations! (wind / turbulence)
                space.y += random(space.x * warpFrequency + _Time.y * warpSpeed) * warpAmplitude * (0.5 +
                    horizontalFade);
                space.x += random(space.y * warpFrequency + _Time.y * warpSpeed + 2.0) * warpAmplitude * horizontalFade;

                float4 lines = float4(0, 0, 0, 0);

                for (int l = 0; l < linesPerGroup; l++)
                {
                    float normalizedLineIndex = float(l) / float(linesPerGroup);
                    float offsetTime = _Time.y * offsetSpeed;
                    float offsetPosition = float(l) + space.x * offsetFrequency;
                    float rand = random(offsetPosition + offsetTime) * 0.5 + 0.5;
                    float halfWidth = lerp(minLineWidth, maxLineWidth, rand * horizontalFade) / 2.0;
                    float offset = random(offsetPosition + offsetTime * (1.0 + normalizedLineIndex)) * lerp(
                        minOffsetSpread, maxOffsetSpread, horizontalFade);
                    float linePosition = getPlasmaY(space.x, horizontalFade, offset);
                    float linez = drawSmoothLine(linePosition, halfWidth, space.y) / 2.0 + drawCrispLine(
                        linePosition, halfWidth * 0.15, space.y);

                    float circleX = fmod(float(l) + _Time.y * lineSpeed, 25.0) - 12.0;
                    float2 circlePosition = float2(circleX, getPlasmaY(circleX, horizontalFade, offset));
                    float circle = drawCircle(circlePosition, 0.01, space) * 4.0;


                    linez = linez + circle;
                    lines += linez * lineColor * rand;
                }

                float4 fragColor = lerp(bgColors[0], bgColors[1], uv.x);
                fragColor *= verticalFade;
                fragColor.a = 1.0;
                fragColor += lines;
                fragColor = fragColor * i.color + _TextureSampleAdd;
                fragColor.rgb *= i.color.a;

                return fragColor;
            }
            ENDCG
        }
    }
}