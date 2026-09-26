Shader "Flipping/FlipperLinearToGammaShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FlipVertical ("Flip Vertical", Float) = 1
        _FlipHorizontal ("Flip Horizontal", Float) = 0
    }
    
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float _FlipVertical;
            float _FlipHorizontal;
            
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                if (_FlipVertical > 0.5) uv.y = 1.0 - uv.y;
                if (_FlipHorizontal > 0.5) uv.x = 1.0 - uv.x;
                
                fixed4 color = tex2D(_MainTex, uv);
                
                // Convert linear to gamma (sRGB)
                color.rgb = GammaToLinearSpace(color.rgb);
                
                return color;
            }
            ENDCG
        }
    }
}