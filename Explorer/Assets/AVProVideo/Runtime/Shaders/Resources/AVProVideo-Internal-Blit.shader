Shader "AVProVideo/Internal/Blit"
{
	Properties
	{
		_SrcTex("Texture", 2D) = "white" { }
	}
	SubShader
	{
		Tags
		{
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Lighting Off
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
        {
			Name "BLIT"

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "../AVProVideo.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			uniform sampler2D _SrcTex;
			uniform float4 _SrcTex_ST;

			v2f vert(appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _SrcTex);
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				if (i.uv.x < 0.0f || i.uv.y < 0.0f || i.uv.x > 1.0f || i.uv.y > 1.0f)
					return half4(0.0f, 0.0f, 0.0f, 0.0f);
				return SampleRGBA(_SrcTex, i.uv);
			}
			ENDCG
		}
	} 

	Fallback off
}
