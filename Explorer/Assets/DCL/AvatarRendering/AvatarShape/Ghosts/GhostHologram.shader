// Hand-written URP replacement for HologramShader.shadergraph.
// Eliminates texture samples for scanlines/flicker (procedural math instead),
// uses half precision throughout, and has zero shadow/depth passes.
Shader "DCL/GhostHologram"
{
    Properties
    {
        [HDR] _FresnelColor     ("Fresnel Color",       Color)   = (0, 4.82, 7.13, 0.97)
        _FresnelPower           ("Fresnel Power",       Float)   = 1.0
        _FresnelBandDensity     ("Fresnel Band Density",Float)   = 4.0
        _FresnelBandSpeed       ("Fresnel Band Speed",  Float)   = 0.7
        _Emission_Intensity     ("Emission Intensity",  Float)   = 5.0

        // Scanlines (procedural — no texture needed)
        _ScanLineTilling        ("Scanline Tiling",     Vector)  = (1, 5, 0, 0)
        _ScanLineSpeed          ("Scanline Speed",      Vector)  = (0, -1, 0, 0)
        _Scanlines_Alpha        ("Scanlines Alpha",     Float)   = 0.5
        _ScanlineNoiseMin       ("Scanline Noise Min",  Range(0,1)) = 0.4

        // Flicker (procedural — no texture needed)
        _Flicker_Speed          ("Flicker Speed",       Float)   = 0.8

        // Horizontal glitch jitter
        _GlitchIntensity        ("Glitch Intensity",    Float)   = 0.03
        _GlitchDensity          ("Glitch Density",      Float)   = 12.0
        _GlitchSpeed            ("Glitch Speed",        Float)   = 6.0
        _GlitchThreshold        ("Glitch Threshold",    Range(0,1)) = 0.85

        // Reveal animation — driven from AvatarGhostSystem.cs
        _RevealPosition         ("Reveal Position",     Vector)  = (0, -0.05, 0, 0)
        _RevealNormal           ("Reveal Normal",       Vector)  = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        // Match blend state from the original HologramShader.mat
        Blend     SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite    Off
        ZTest     LEqual
        Cull      Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vert
            #pragma fragment frag

            // GPU instancing support
            #pragma multi_compile_instancing

            #include "GhostHologram_ForwardPass.hlsl"
            ENDHLSL
        }

        // No DepthOnly, ShadowCaster, or MotionVectors passes —
        // transparent ghost doesn't cast shadows or write depth.
    }
}
