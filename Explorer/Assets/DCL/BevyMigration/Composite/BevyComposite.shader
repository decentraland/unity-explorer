// Composites Bevy's frame over what the Unity camera already drew (the skybox): the frame is premultiplied by
// coverage, cleared to alpha zero where Bevy drew nothing (BevyMigration/docs/README.md § 8 "Topology").
Shader "Hidden/DCL/BevyComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "BevyComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0);
            }
            ENDHLSL
        }
    }
}
