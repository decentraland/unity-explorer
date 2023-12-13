Shader "Hidden/BlitAdd" 
{
	Properties{ _MainTex("Texture", any) = "" {} }
	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			Blend One Zero

			CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag

	#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _Source;
			uniform float4 _MainTex_ST;
			float4 _Source_TexelSize;

			struct appdata_t 
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f 
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float4 main = tex2D(_MainTex, i.texcoord);

#if UNITY_UV_STARTS_AT_TOP
				if (_Source_TexelSize.y < 0)
					i.texcoord.y = 1 - i.texcoord.y;
#endif
				float4 source = tex2D(_Source, i.texcoord);

				source *= main.w;
				source.xyz += main.xyz;
				return source;
			}
			ENDCG
		}
	}
	Fallback Off
}
