// Decentraland / Stylized Grass Shader (URP)
//
// A URP-lit stylized grass/foliage shader. It renders the meshes the project's grass, flower,
// and hedge materials are authored against, using a wide superset of the property names those
// .mat files bind so their authored values are preserved on rebind (Unity keys m_SavedProperties
// by name).
//
// What it does:
//   * Samples _BaseMap modulated by _BaseColor / _Color.
//   * Animates vertices with a directional sine-wave wind that bends each blade: the tip sways
//     further than the root and arcs downward, rather than the whole blade sliding sideways.
//     The authored _WindMap scrolls over the ground as a gust envelope, and the per-object /
//     per-vertex randomness parameters give every blade its own phase, strength and flutter.
//   * Tints blades by the baked world-space ground colour map (_ColorMap / _ColorMapBounds,
//     published as shader globals), gated by the map's coverage alpha and concentrated toward the
//     root via _ColorMapHeight.
//   * Applies a height gradient that darkens the blade base by _Darkening, brightens blades
//     caught in a gust by _WindGustTint, and under _HUE_VARIATION tints each blade between the
//     two authored hue colours by a per-instance random.
//   * Remaps the lighting normal: _NormalSpherify rounds a clump outward from its origin,
//     _NormalFlattening pulls blades toward world up so they shade like the ground.
//   * Adds back-light translucency (_Translucency / _TranslucencyFalloff / _SubsurfaceColor)
//     masked per texel by _TranslucencyMap, so blades glow when viewed against the light.
//   * Full URP forward lighting: main light + shadows, additional lights through the Forward+
//     cluster loop, SH ambient, optional specular / environment reflection, fog.
//   * Alpha-to-coverage cut-outs (_AlphaToCoverage) so blade cards keep MSAA-resolved soft edges.
//   * Distance fade (_FadeNear / _FadeFar) and view-angle fade (_AngleFading) under the _FADING
//     keyword, dissolved through coverage.
//   * Under _BILLBOARD, turns each card toward the camera (yaw only unless
//     _BillboardingVerticalRotation) and thins its shadow as it turns edge-on to the light.
//   * Casts and receives shadows; participates in DepthOnly / DepthNormals.

Shader "Decentraland/StylizedGrass"
{
    Properties
    {
        // --- Core surface ---------------------------------------------------
        [MainTexture] _BaseMap          ("Base Map (Albedo Alpha)", 2D)        = "white" {}
        [MainColor]   _BaseColor        ("Base Color", Color)                  = (0.45, 0.7, 0.3, 1)
                      _Color            ("Tint (legacy)", Color)               = (1,1,1,1)

        _Cutoff         ("Alpha Cutoff", Range(0, 1))                          = 0.5

        // --- Color map (world-space tint) ----------------------------------
        // _ColorMap / _ColorMapBounds / _ColorMap_TexelSize are shader GLOBALS published by
        // GrassColorMap.SetActive, so they are deliberately absent from this block: a property
        // declared here would shadow the global with a per-material value and the bake would never
        // reach the shader.
                       _ColorMapStrength("Color Map Strength", Range(0, 1))    = 1
                       _ColorMapHeight  ("Color Map Height Falloff", Range(0, 8)) = 0.5
        // x scales the per-instance ground tint in the _GPU_GRASS_BATCHING path, y blends toward
        // replacing the authored albedo with that tint (GrassIndirectRenderer sets (2,1,0,0) for
        // grass; flowers keep the neutral default).
        [HideInInspector] _ColorMapParams ("Color Map Params", Vector)         = (1, 0, 0, 0)

        // --- Hue variation (per-instance, _HUE_VARIATION) ------------------
        [Toggle(_HUE_VARIATION)] _HueVariationKwToggle ("Hue Variation", Float) = 0
        _HueVariation        ("Hue Variation Color A (a = strength)", Color)   = (1,1,1,1)
        _HueVariationColor   ("Hue Variation Color B (a = amount)", Color)     = (1,0.5,0,0.1)
        _HueVariationHeight  ("Hue Variation Toward Tip", Range(0, 1))         = 0

        // --- Height gradient / translucency --------------------------------
        _Darkening           ("Root Darkening", Range(0, 1))                   = 0.5
        _Translucency        ("Translucency", Range(0, 4))                     = 1
        _TranslucencyFalloff ("Translucency Falloff", Range(1, 8))             = 2
        _TranslucencyDirect  ("Translucency Direct Light", Range(0, 2))        = 0.5
        _TranslucencyIndirect("Translucency Ambient Light", Range(0, 2))       = 0
        _TranslucencyOffset  ("Translucency Normal Offset", Range(0, 2))       = 1
        [NoScaleOffset] _TranslucencyMap ("Translucency Map (R)", 2D)          = "white" {}
        _SubsurfaceColor     ("Subsurface Color", Color)                      = (1,1,1,1)

        // --- Wind / bend ---------------------------------------------------
        _WindDirection      ("Wind Direction (xz)", Vector)                    = (1, 0, 0, 0)
        _WindStrength       ("Wind Strength",     Range(0, 2))                 = 0.15
        _WindSpeed          ("Wind Speed",        Range(0, 10))                = 2
        _WindFrequency      ("Wind Frequency",    Range(0, 4))                 = 1
        _WindWavelength     ("Wind Wavelength",   Range(0.01, 4))              = 0.4
        _WindAmbientStrength("Wind Ambient",      Range(0, 2))                 = 0.2
        _WindSwinging       ("Wind Swing Back",   Range(0, 1))                 = 0.2
        _WindFlutter        ("Wind Flutter",      Range(0, 2))                 = 0.5
        _WindObjectRand     ("Wind Phase Per Blade", Range(0, 1))              = 1
        _WindVertexRand     ("Wind Phase Per Vertex", Range(0, 1))             = 1
        _WindRandStrength   ("Wind Strength Per Blade", Range(0, 1))           = 1
        _WindRandSpeed      ("Wind Speed Per Blade", Range(0, 1))              = 1
        [NoScaleOffset] _WindMap ("Wind Map (gust noise)", 2D)                 = "black" {}
        _WindGustFreq       ("Gust Repeats Per 10 m", Range(0, 4))             = 0.33
        _WindGustSpeed      ("Gust Speed (m/s)",  Range(0, 20))                = 5
        _WindGustStrength   ("Gust Strength",     Range(0, 1))                 = 0.05
        _WindGustTint       ("Gust Brightening",  Range(0, 1))                 = 0.08
        _BendPushStrength   ("Bend Push",         Range(0, 4))                 = 1
        _BendFlattenStrength("Bend Flatten",      Range(0, 4))                 = 1

        // --- Billboarding (_BILLBOARD) -------------------------------------
        [Toggle(_BILLBOARD)] _BillboardKwToggle ("Billboard", Float)           = 0
        _Billboard                    ("Billboard Amount", Range(0, 1))        = 0
        _BillboardingVerticalRotation ("Billboard Tilt Toward Camera", Range(0, 1)) = 0
        _BillboardShadowFade          ("Billboard Shadow Fade", Range(0, 1))   = 0.5

        // --- Normal remap --------------------------------------------------
        _SpherifyNormals     ("Spherify Normals", Float)                       = 1
        _NormalSpherify      ("Spherify Amount", Range(0, 1))                  = 0
        _NormalSpherifyMask  ("Spherify Toward Tip", Range(0, 1))              = 0
        _NormalFlattening    ("Flatten Toward Up", Range(0, 1))                = 1
        _NormalFlattenDepthNormals ("Flatten In Depth Normals", Float)         = 0

        // --- Distance / angle fade -----------------------------------------
        _FadeNear       ("Fade Near (start, end)", Vector)                     = (50, 100, 0, 0)
        _FadeFar        ("Fade Far  (start, end)", Vector)                     = (50, 100, 0, 0)
        _AngleFading    ("Angle Fading", Range(0, 1))                          = 0
        _FadeAngleThreshold ("Angle Fade Threshold", Range(0, 1))              = 0

        // --- Scatter placement ---------------------------------------------
        // Metres per parcel, matching GrassIndirectRenderer's parcel grid. Nothing binds it today
        // (RenderGroundSystem only sets _ParcelSize on the ground material), so the default is the
        // authoritative value and a future binder can override it without a shader change.
        [HideInInspector] _ParcelSize   ("Parcel Size (m)", Float)              = 16

        // --- Render state --------------------------------------------------
        [HideInInspector] _Surface  ("__surface", Float)                       = 0
        [HideInInspector] _Blend    ("__blend",   Float)                       = 0
        [HideInInspector] _BlendOp  ("__blendop", Float)                       = 0
        [HideInInspector] _Cull     ("__cull",    Float)                       = 0
        [HideInInspector] _SrcBlend ("__src",     Float)                       = 1
        [HideInInspector] _DstBlend ("__dst",     Float)                       = 0
        [HideInInspector] _ZWrite   ("__zw",      Float)                       = 1
        [HideInInspector] _AlphaToCoverage("__a2c", Float)                     = 0
        [HideInInspector] _AlphaClip("__clip",    Float)                       = 0
        [HideInInspector] _QueueOffset("__qo",    Float)                       = 0
        [HideInInspector] _Mode     ("__mode",    Float)                       = 0

        // --- Legacy / passthrough (kept so existing .mat files retain values
        // even though this shader does not consume them). ------------------
        [HideInInspector] _BumpMap          ("Normal Map", 2D)                  = "bump" {}
        [HideInInspector] _BumpScale        ("Normal Scale", Float)             = 1
        [HideInInspector] _EmissionMap      ("Emission Map", 2D)                = "white" {}
        [HideInInspector] _EmissionColor    ("Emission Color", Color)           = (0,0,0,1)
        [HideInInspector] _EmissionEnabled  ("Emission Enabled", Float)         = 0
        [HideInInspector] _ExtraTex         ("Extra Map", 2D)                   = "white" {}
        [HideInInspector] _MetallicGlossMap ("Metallic", 2D)                    = "white" {}
        [HideInInspector] _GlossMap         ("Gloss Map", 2D)                   = "white" {}
        [HideInInspector] _OcclusionMap     ("Occlusion", 2D)                   = "white" {}
        [HideInInspector] _SpecGlossMap     ("Spec Gloss", 2D)                  = "white" {}
        [HideInInspector] _SubsurfaceTex    ("Subsurface", 2D)                  = "white" {}
        [HideInInspector] _DetailAlbedoMap  ("Detail Albedo", 2D)               = "white" {}
        [HideInInspector] _DetailMask       ("Detail Mask", 2D)                 = "white" {}
        [HideInInspector] _DetailNormalMap  ("Detail Normal", 2D)               = "bump" {}
        [HideInInspector] _DetailNormalMapScale ("Detail Normal Scale", Float)  = 1
        [HideInInspector] _DetailTex        ("Detail Tex", 2D)                  = "white" {}
        [HideInInspector] _ParallaxMap      ("Parallax Map", 2D)                = "black" {}
        [HideInInspector] _Parallax         ("Parallax", Float)                 = 0.02
        [HideInInspector] _UVSec            ("UV Set for secondary", Float)     = 0

        [HideInInspector] _AdvancedLighting          ("AdvancedLighting", Float)          = 1
        // Reciprocal of the authored blade height in object space; see GrassTipness.
                          _BendInfluence             ("Bend Influence", Float)            = 1
        [HideInInspector] _BendMode                  ("BendMode", Float)                  = 0
        [HideInInspector] _BendTint                  ("BendTint", Color)                  = (1,1,1,1)
        [HideInInspector] _CameraFadingEnabled       ("CameraFadingEnabled", Float)       = 0
        [HideInInspector] _CameraNearFadeDistance    ("CameraNearFadeDistance", Float)    = 1
        [HideInInspector] _CameraFarFadeDistance     ("CameraFarFadeDistance", Float)     = 2
        [HideInInspector] _CameraFadeParams          ("CameraFadeParams", Vector)         = (0,0,0,0)
        [HideInInspector] _DistortionEnabled         ("DistortionEnabled", Float)         = 0
        [HideInInspector] _DistortionBlend           ("DistortionBlend", Float)           = 0.5
        [HideInInspector] _DistortionStrength        ("DistortionStrength", Float)        = 1
        [HideInInspector] _DistortionStrengthScaled  ("DistortionStrengthScaled", Float)  = 0.1
        [HideInInspector] _EnvironmentReflections    ("EnvironmentReflections", Float)    = 1
        [HideInInspector] _FadeParams                ("FadeParams", Vector)               = (50,100,0,0)
        [HideInInspector] _FadingOn                  ("FadingOn", Float)                  = 1
        [HideInInspector] _FlipbookMode              ("FlipbookMode", Float)              = 0
        [HideInInspector] _GlossMapScale             ("GlossMapScale", Float)             = 0
        [HideInInspector] _Glossiness                ("Glossiness", Float)                = 0
        [HideInInspector] _GlossyReflections         ("GlossyReflections", Float)         = 0
        [HideInInspector] _LightingEnabled           ("LightingEnabled", Float)           = 1
        [HideInInspector] _LightingMode              ("LightingMode", Float)              = 2
        [HideInInspector] _LODDebugColor             ("LODDebugColor", Color)             = (1,1,1,1)
        [HideInInspector] _Metallic                  ("Metallic", Float)                  = 0
        [HideInInspector] _NormalMapKwToggle         ("NormalMapKwToggle", Float)         = 0
        [HideInInspector] _NormalParams              ("NormalParams", Vector)             = (0,1,0,0)
        [HideInInspector] _OcclusionStrength         ("OcclusionStrength", Float)         = 0
        [HideInInspector] _PerspectiveCorrection     ("PerspectiveCorrection", Float)     = 1
        [HideInInspector] _ReceiveShadows            ("ReceiveShadows", Float)            = 1
        [HideInInspector] _Scalemap                  ("Scalemap", Float)                  = 0
        [HideInInspector] _ScalemapInfluence         ("ScalemapInfluence", Vector)        = (0,1,0,0)
        [HideInInspector] _ShadowBiasCorrection      ("ShadowBiasCorrection", Float)      = 0
        [HideInInspector] _ShadowOffset              ("ShadowOffset", Float)              = 0
        [HideInInspector] _Shininess                 ("Shininess", Float)                 = 0.5
                          _Smoothness                ("Smoothness", Range(0, 1))          = 0
        [HideInInspector] _SmoothnessTextureChannel  ("SmoothnessTextureChannel", Float)  = 0
        [HideInInspector] _SoftParticlesEnabled      ("SoftParticlesEnabled", Float)      = 0
        [HideInInspector] _SoftParticlesNearFadeDistance ("SoftParticlesNearFadeDistance", Float) = 0
        [HideInInspector] _SoftParticlesFarFadeDistance  ("SoftParticlesFarFadeDistance", Float)  = 1
        [HideInInspector] _SoftParticleFadeParams    ("SoftParticleFadeParams", Vector)   = (0,0,0,0)
                          _SpecColor                 ("Specular Color", Color)            = (0.2,0.2,0.2,1)
        [HideInInspector] _SpecularAmount            ("SpecularAmount", Float)            = 0
        [HideInInspector] _SpecularHighlights        ("SpecularHighlights", Float)        = 0
        [HideInInspector] _SquashAmount              ("SquashAmount", Float)              = 0
        [HideInInspector] _SubsurfaceIndirect        ("SubsurfaceIndirect", Float)        = 0.25
        [HideInInspector] _SubsurfaceKwToggle        ("SubsurfaceKwToggle", Float)        = 0
        [HideInInspector] _TreeInstanceColor         ("TreeInstanceColor", Color)         = (1,1,1,1)
        [HideInInspector] _TreeInstanceScale         ("TreeInstanceScale", Vector)        = (1,1,1,1)
        [HideInInspector] _TwoSided                  ("TwoSided", Float)                  = 0
        [HideInInspector] _VertexColorBendingChannel ("VertexColorBendingChannel", Float) = 0
        [HideInInspector] _VertexColorShadingChannel ("VertexColorShadingChannel", Float) = 0
        [HideInInspector] _VertexColorWindChannel    ("VertexColorWindChannel", Float)    = 0
        [HideInInspector] _VertexDarkening           ("VertexDarkening", Float)           = 0
        [HideInInspector] _WindQuality               ("WindQuality", Float)               = 0
        [HideInInspector] _WindStr                   ("WindStr", Float)                   = 0.067
        [HideInInspector] _WindStrengths             ("WindStrengths", Float)             = 1
        [HideInInspector] _WorkflowMode              ("WorkflowMode", Float)              = 1
        [HideInInspector] _batchingBlockSize         ("batchingBlockSize", Float)         = 256
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "Queue"           = "Geometry"
        }
        LOD 200

        // Shared HLSL for all passes ----------------------------------------
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _Color;
            float  _Cutoff;

            float  _ColorMapStrength;
            float  _ColorMapHeight;

            float4 _HueVariation;
            float4 _HueVariationColor;
            float  _HueVariationHeight;

            float  _Darkening;
            float  _Translucency;
            float  _TranslucencyFalloff;
            float  _TranslucencyDirect;
            float  _TranslucencyIndirect;
            float  _TranslucencyOffset;
            float4 _SubsurfaceColor;

            float4 _WindDirection;
            float  _WindStrength;
            float  _WindSpeed;
            float  _WindFrequency;
            float  _WindWavelength;
            float  _WindAmbientStrength;
            float  _WindSwinging;
            float  _WindFlutter;
            float  _WindObjectRand;
            float  _WindVertexRand;
            float  _WindRandStrength;
            float  _WindRandSpeed;
            float  _WindGustFreq;
            float  _WindGustSpeed;
            float  _WindGustStrength;
            float  _WindGustTint;
            float  _BendPushStrength;
            float  _BendFlattenStrength;
            float  _BendInfluence;

            float  _Billboard;
            float  _BillboardingVerticalRotation;
            float  _BillboardShadowFade;

            float  _SpherifyNormals;
            float  _NormalSpherify;
            float  _NormalSpherifyMask;
            float  _NormalFlattening;
            float  _NormalFlattenDepthNormals;

            float4 _FadeNear;
            float4 _FadeFar;
            float  _AngleFading;
            float  _FadeAngleThreshold;

            float4 _ColorMapParams;
            float  _batchingBlockSize;
            float  _ParcelSize;

            float4 _SpecColor;
            float  _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);         SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WindMap);         SAMPLER(sampler_WindMap);
        TEXTURE2D(_TranslucencyMap); SAMPLER(sampler_TranslucencyMap);

        // Shader globals, not material properties: GrassColorMap.SetActive publishes all three so a
        // single bake tints every grass, flower and hedge material at once. They must live outside
        // UnityPerMaterial or SetGlobalVector could never reach them.
        TEXTURE2D(_ColorMap);   SAMPLER(sampler_ColorMap);
        float4 _ColorMapBounds;
        float4 _ColorMap_TexelSize;

        // --- GPU-scattered indirect path (_GPU_GRASS_BATCHING) -------------
        // GrassIndirectRenderer draws this material with Graphics.RenderMeshIndirect;
        // placement comes from the scatter computes (GrassScatter / FlowerScatter /
        // CatTailScatter), which write one PerInst per potential instance:
        //   position.xz  = offset inside the parcel (metres)
        //   position.y   = world-space terrain height
        //   position.w   = uniform scale (grass always scatters a live blade; only the flower
        //                  scatter parks unused slots at scale 0 and y = -5000)
        //   quatRotation = terrain-normal alignment + random yaw
        //   colour       = ground albedo at the root (grass) or white (flowers)
        // The owning parcel is _PerParcelBuffer[instanceID / _batchingBlockSize] in _ParcelSize
        // metre parcel coordinates.
        #if defined(_GPU_GRASS_BATCHING)
        struct GrassPerInst
        {
            float4 position;
            float4 quatRotation;
            float4 colour;
        };
        StructuredBuffer<GrassPerInst> _PerInstanceBuffer;
        StructuredBuffer<int2>         _PerParcelBuffer;

        float3 RotateByQuaternion(float4 q, float3 v)
        {
            return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
        }

        void FetchScatterInstance(uint instanceID,
            out float3 originWS, out float4 rotation, out float scale, out float4 tint)
        {
            GrassPerInst inst = _PerInstanceBuffer[instanceID];
            uint block = (uint)max(_batchingBlockSize, 1.0);
            int2 parcel = _PerParcelBuffer[instanceID / block];
            originWS = float3(parcel.x * _ParcelSize + inst.position.x, inst.position.y,
                              parcel.y * _ParcelSize + inst.position.z);
            rotation = inst.quatRotation;
            scale    = inst.position.w;
            tint     = inst.colour;
        }
        #else
        // Object-draw path: the blade origin and uniform scale come from the object matrix.
        void FetchObjectInstance(out float3 originWS, out float scale)
        {
            float4x4 m = GetObjectToWorldMatrix();
            originWS = m._m03_m13_m23;
            scale    = length(m._m00_m10_m20);
        }
        #endif // _GPU_GRASS_BATCHING

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            #if defined(_GPU_GRASS_BATCHING) && !UNITY_ANY_INSTANCING_ENABLED
            // UNITY_VERTEX_INPUT_INSTANCE_ID already declares an SV_InstanceID field whenever
            // instancing is on (UNITY_ANY_INSTANCING_ENABLED is always defined, as 1 or 0),
            // and a struct may only carry that semantic once.
            uint svInstanceID : SV_InstanceID;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        #if defined(_GPU_GRASS_BATCHING)
            #if UNITY_ANY_INSTANCING_ENABLED
                // UNITY_VERTEX_INPUT_INSTANCE_ID declares the field as `instanceID`.
                #define GRASS_INSTANCE_ID(IN) (IN).instanceID
            #else
                #define GRASS_INSTANCE_ID(IN) (IN).svInstanceID
            #endif
        #endif

        struct Varyings
        {
            float4 positionCS  : SV_POSITION;
            float2 uv          : TEXCOORD0;
            float3 positionWS  : TEXCOORD1;
            float3 normalWS    : TEXCOORD2;
            // x = tipness (0 root .. 1 tip), y = gust envelope, z = per-instance random.
            float4 misc        : TEXCOORD3;
            float  fogFactor   : TEXCOORD4;
            #if defined(_GPU_GRASS_BATCHING)
            float4 instTint    : TEXCOORD5;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct GrassDepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float  shadowFade : TEXCOORD1;
        };

        // Root -> tip gradient, driven by object-space height. The source meshes (grass.fbx,
        // Flower0*.fbx) carry no vertex-colour stream, so a colour-keyed weight would read an
        // undefined, platform-dependent COLOR register. Blades are authored with the root at y = 0
        // and _BendInfluence is the reciprocal of their authored height (1 for the one-unit cards),
        // so this is 0 at the root and 1 at the tip.
        float GrassTipness(float3 positionOS)
        {
            return saturate(positionOS.y * _BendInfluence);
        }

        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Gust envelope from the authored wind map: world XZ tiled at _WindGustFreq repeats per
        // ten metres and scrolled along the wind at _WindGustSpeed metres per second.
        float SampleGust(float2 xz, float2 dir)
        {
            float2 uv = (xz - dir * (_Time.y * _WindGustSpeed)) * (_WindGustFreq * 0.1);
            return SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, uv, 0).r;
        }

        // Directional sine-wave wind that bends the blade. windWeight (0 at the root, 1 at the tip)
        // makes the tip travel further than the root; the tip is also pulled down in proportion to
        // its horizontal sway so the blade arcs over instead of stretching. objectRand offsets the
        // phase, strength and speed per blade, vertexRand per vertex, and the gust envelope adds a
        // directional push plus a sideways flutter on top of the wave.
        float3 ApplyWindAndBend(float3 positionWS, float3 originWS, float windWeight,
            float objectRand, float vertexRand, out float gust)
        {
            float2 dir   = normalize(_WindDirection.xz + float2(1e-5, 0));
            float2 side  = float2(-dir.y, dir.x);
            float  speed = _WindSpeed * lerp(1.0, 0.75 + 0.5 * objectRand, _WindRandSpeed);
            float  t     = _Time.y * speed;
            float  proj  = dot(positionWS.xz, dir);
            float  phase = proj * _WindWavelength + t * _WindFrequency
                         + objectRand * _WindObjectRand * TWO_PI
                         + vertexRand * _WindVertexRand;
            float  wave  = sin(phase) + 0.5 * sin(phase * 2.07 + 1.3);
            // _WindSwinging opens the windward half of the wave; at 0 the blade only leans leeward.
            wave = lerp(max(wave, 0.0), wave, _WindSwinging);
            float  ambient  = _WindAmbientStrength * sin(phase * 0.5 + 1.1);
            float  strength = _WindStrength * lerp(1.0, 0.5 + objectRand, _WindRandStrength);

            gust = SampleGust(originWS.xz, dir);

            // _WindGustStrength is authored at a quarter of the sway scale: a full gust pushes as
            // far as four units of it.
            float  sway    = ((wave + ambient) * strength + gust * _WindGustStrength * 4.0)
                           * windWeight * _BendPushStrength;
            float  flutter = sin(t * 6.0 + vertexRand * TWO_PI + phase * 3.0)
                           * _WindFlutter * 0.02 * windWeight * (0.5 + gust);
            float3 offset  = float3(dir.x, 0, dir.y) * sway + float3(side.x, 0, side.y) * flutter;
            offset.y -= (sway * sway) * _BendFlattenStrength * 0.5;

            return positionWS + offset;
        }

        // Lighting normal: _NormalSpherify bends card normals outward from the blade origin so a
        // clump reads as a rounded tuft, _NormalFlattening pulls them toward world up so blades
        // shade like the ground they stand on whatever their yaw.
        float3 GrassShadingNormal(float3 normalWS, float3 positionWS, float3 originWS, float tipness)
        {
            float3 radial   = normalize(positionWS - originWS + float3(0, 1e-3, 0));
            float  spherify = _NormalSpherify * _SpherifyNormals * lerp(1.0, tipness, _NormalSpherifyMask);
            normalWS = normalize(lerp(normalWS, radial, saturate(spherify)));
            return normalize(lerp(normalWS, float3(0, 1, 0), saturate(_NormalFlattening)));
        }

        // Instance placement, billboarding and wind for every pass. rand.x is per instance, rand.y
        // per vertex; gust is the wind-map envelope at the blade.
        void GrassTransform(GrassAttributes IN,
            out float3 positionWS, out float3 normalWS, out float3 originWS,
            out float4 instTint, out float2 rand, out float gust)
        {
            float3 positionOS = IN.positionOS.xyz;
            float  scale;

            #if defined(_GPU_GRASS_BATCHING)
            float4 rotation;
            FetchScatterInstance(GRASS_INSTANCE_ID(IN), originWS, rotation, scale, instTint);
            positionWS = originWS + RotateByQuaternion(rotation, positionOS * scale);
            normalWS   = RotateByQuaternion(rotation, IN.normalOS);
            #else
            FetchObjectInstance(originWS, scale);
            positionWS = TransformObjectToWorld(positionOS);
            normalWS   = TransformObjectToWorldNormal(IN.normalOS);
            instTint   = float4(1, 1, 1, 1);
            #endif

            rand.x = Hash21(originWS.xz);
            rand.y = Hash21(positionOS.xy + rand.x);

            #if defined(_BILLBOARD)
            // Card meshes are authored in the XY plane facing +Z: rebuild the vertex in a basis
            // that faces the camera, yaw-only unless _BillboardingVerticalRotation tilts it too.
            float3 toCam = GetCameraPositionWS() - originWS;
            toCam.y *= _BillboardingVerticalRotation;
            toCam = normalize(toCam + float3(1e-4, 0, 1e-4));
            float3 right = normalize(cross(float3(0, 1, 0), toCam));
            float3 upB   = cross(toCam, right);
            float3 billboardWS = originWS + (right * positionOS.x + upB * positionOS.y + toCam * positionOS.z) * scale;
            positionWS = lerp(positionWS, billboardWS, _Billboard);
            normalWS   = normalize(lerp(normalWS, toCam, _Billboard));
            #endif

            positionWS = ApplyWindAndBend(positionWS, originWS, GrassTipness(positionOS), rand.x, rand.y, gust);
        }

        // 0..1 coverage from the two authored distance ranges: _FadeNear (start, end) ramps grass in
        // close to the camera, _FadeFar (start, end) ramps it out at range. Both endpoints are
        // separated by an epsilon because smoothstep is undefined when they coincide.
        float ComputeDistanceFade(float3 positionWS)
        {
            float d = distance(GetCameraPositionWS(), positionWS);
            float fadeIn  = smoothstep(_FadeNear.x, max(_FadeNear.y, _FadeNear.x + 1e-3), d);
            float fadeOut = 1.0 - smoothstep(_FadeFar.x, max(_FadeFar.y, _FadeFar.x + 1e-3), d);
            return saturate(fadeIn * fadeOut);
        }

        // Blades viewed steeply from above thin to their edge: _AngleFading dissolves them past
        // _FadeAngleThreshold of the straight-down view. The threshold stays below the upper
        // endpoint because smoothstep is undefined when they coincide.
        float ComputeAngleFade(float3 viewDirWS)
        {
            float steepness = saturate(viewDirWS.y);
            return 1.0 - _AngleFading * smoothstep(min(_FadeAngleThreshold, 0.999), 1.0, steepness);
        }

        // World-space ground-colour-map tint. Gated by the bounds box and the map's coverage alpha,
        // and concentrated toward the root through _ColorMapHeight so tips keep their base colour.
        float3 ApplyColorMap(float3 albedo, float3 positionWS, float tipness)
        {
            float2 uv = (positionWS.xz - _ColorMapBounds.xy)
                      / max(_ColorMapBounds.zw, float2(1e-3, 1e-3));
            float2 mask2 = step(0, uv) * step(uv, 1);
            float  inside = mask2.x * mask2.y;

            // Half-texel inset: the map is clamp-sampled bilinearly, so without it a blade standing
            // on the border blends in a texel from beyond the baked region. Falls back to no inset
            // when the texel size global has not been published.
            float2 halfTexel = 0.5 * _ColorMap_TexelSize.xy;
            float2 sampleUV = clamp(uv, halfTexel, 1.0 - halfTexel);

            float4 cm = SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, sampleUV, 0);
            float  cmHeight = pow(saturate(1.0 - tipness), _ColorMapHeight);
            float  amount = _ColorMapStrength * inside * cm.a * cmHeight;

            return lerp(albedo, albedo * cm.rgb, amount);
        }

        // Blinn-Phong highlight whose amplitude IS _Smoothness, so a material that leaves the
        // default 0 gets no highlight at all and _SPECULARHIGHLIGHTS_OFF only has to skip the term
        // rather than cancel a baked-in one.
        float3 GrassSpecular(float3 N, float3 V, float3 L, float3 radiance)
        {
            float smoothness = saturate(_Smoothness);
            float3 H = normalize(L + V);
            float exponent = max(1.0, smoothness * smoothness * 256.0);
            return radiance * (pow(saturate(dot(N, H)), exponent) * smoothness) * _SpecColor.rgb;
        }

        Varyings GrassVertex(GrassAttributes IN)
        {
            Varyings OUT = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionWS;
            float3 normalWS;
            float3 originWS;
            float4 instTint;
            float2 rand;
            float  gust;
            GrassTransform(IN, positionWS, normalWS, originWS, instTint, rand, gust);

            float tipness = GrassTipness(IN.positionOS.xyz);

            OUT.positionWS  = positionWS;
            OUT.positionCS  = TransformWorldToHClip(positionWS);
            OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
            OUT.normalWS    = GrassShadingNormal(normalWS, positionWS, originWS, tipness);
            OUT.misc        = float4(tipness, gust, rand.x, 0);
            OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
            #if defined(_GPU_GRASS_BATCHING)
            OUT.instTint    = instTint;
            #endif
            return OUT;
        }
        ENDHLSL

        // -----------------------------------------------------------------
        // Forward Lit
        // -----------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            Cull   [_Cull]
            AlphaToMask [_AlphaToCoverage]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   GrassVertex
            #pragma fragment GrassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _GPU_GRASS_BATCHING
            #pragma target 4.5 _GPU_GRASS_BATCHING
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHATOCOVERAGE_ON
            #pragma shader_feature_local _ _FADING
            #pragma shader_feature_local _ _BILLBOARD
            #pragma shader_feature_local_fragment _ _HUE_VARIATION
            #pragma shader_feature_local_fragment _ _ADVANCED_LIGHTING
            #pragma shader_feature_local_fragment _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ _ENVIRONMENTREFLECTIONS_OFF

            half4 GrassFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = baseSample * _BaseColor * _Color;

                float tipness  = IN.misc.x;
                float gust     = IN.misc.y;
                float instRand = IN.misc.z;

                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                #ifdef _FADING
                // Fed through coverage rather than a boolean cut so alpha-to-coverage dissolves the
                // blade across the authored range instead of popping it out at the far endpoint.
                albedo.a *= ComputeDistanceFade(IN.positionWS) * ComputeAngleFade(V);
                #endif

                #if defined(_ALPHATEST_ON) || defined(_ALPHATOCOVERAGE_ON)
                clip(albedo.a - _Cutoff);
                #endif

                #if defined(_GPU_GRASS_BATCHING)
                // The scatter compute already sampled the ground albedo per instance.
                // _ColorMapParams.x rescales that tint; .y blends between modulating the
                // authored albedo (0 — flowers keep their texture colours) and replacing
                // it with the ground tint scaled by the texture's luminance (1 — grass
                // blades take the terrain colour so they sit in the ground they grow from).
                half3 groundTint = IN.instTint.rgb * _ColorMapParams.x;
                half  baseLuma   = dot(baseSample.rgb, half3(0.2126, 0.7152, 0.0722));
                albedo.rgb = lerp(albedo.rgb * groundTint,
                                  groundTint * baseLuma,
                                  saturate(_ColorMapParams.y));
                #else
                // Ground colour-map tint.
                albedo.rgb = ApplyColorMap(albedo.rgb, IN.positionWS, tipness);
                #endif

                #ifdef _HUE_VARIATION
                // Each blade sits somewhere between the two authored hue colours by its instance
                // random; _HueVariationHeight moves the tint toward the tip.
                float3 hueTint   = lerp(_HueVariation.rgb, _HueVariationColor.rgb, instRand);
                float  hueWeight = _HueVariation.a * _HueVariationColor.a * lerp(1.0, tipness, _HueVariationHeight);
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * hueTint, hueWeight);
                #endif

                // Blades caught in a gust catch more light.
                albedo.rgb *= 1.0 + gust * _WindGustTint;

                // Height gradient last: at _ColorMapParams.y == 1 the blend above rebuilds the
                // albedo from the ground tint, which would otherwise discard the root darkening.
                // Both blends are linear in albedo, so ordering costs nothing elsewhere.
                albedo.rgb *= lerp(1.0 - _Darkening, 1.0, tipness);

                float3 N = normalize(IN.normalWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);
                float3 mainRadiance = mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                float3 diffuse  = mainRadiance * saturate(dot(N, mainLight.direction));
                float3 specular = 0;
                #ifndef _SPECULARHIGHLIGHTS_OFF
                specular = GrassSpecular(N, V, mainLight.direction, mainRadiance);
                #endif

                #ifdef _ADDITIONAL_LIGHTS
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                uint pixelLightCount = GetAdditionalLightsCount();
                // No lightmap shadow mask on grass, so every baked channel is fully lit and the
                // three-argument overload resolves to pure realtime additional-light shadows.
                half4 shadowMask = half4(1, 1, 1, 1);

                #if USE_CLUSTER_LIGHT_LOOP
                // The cluster iterator only walks punctual lights; additional directional lights sit
                // in the first URP_FP_DIRECTIONAL_LIGHTS_COUNT slots and need their own pass. The
                // loop variable is named lightIndex because the subtractive check expands to a test
                // against it.
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

                    Light dirLight = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                    float3 dirRadiance = dirLight.color * (dirLight.shadowAttenuation * dirLight.distanceAttenuation);
                    diffuse += dirRadiance * saturate(dot(N, dirLight.direction));
                    #ifndef _SPECULARHIGHLIGHTS_OFF
                    specular += GrassSpecular(N, V, dirLight.direction, dirRadiance);
                    #endif
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                    float3 radiance = light.color * (light.shadowAttenuation * light.distanceAttenuation);
                    diffuse += radiance * saturate(dot(N, light.direction));
                    #ifndef _SPECULARHIGHLIGHTS_OFF
                    specular += GrassSpecular(N, V, light.direction, radiance);
                    #endif
                LIGHT_LOOP_END
                #endif

                float3 ambient = SampleSH(N);

                // URP's GlossyEnvironmentReflection is what honours _ENVIRONMENTREFLECTIONS_OFF: it
                // returns the flat _GlossyEnvironmentColor instead of sampling the probe cubemap.
                // Weighted by _Smoothness for the same opt-in reason as GrassSpecular.
                float smoothness = saturate(_Smoothness);
                float fresnel = pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 envSpecular = GlossyEnvironmentReflection(reflect(-V, N), IN.positionWS,
                                         1.0 - smoothness, 1.0, screenUV)
                                   * lerp(_SpecColor.rgb, 1.0, fresnel) * smoothness;

                // Back-light translucency: strongest when looking toward the light through the
                // blade. The light direction is offset toward the normal by _TranslucencyOffset so
                // the glow wraps around the blade, the authored map masks it per texel, and direct
                // and ambient light contribute by their own weights.
                #ifdef _ADVANCED_LIGHTING
                half   transMask = SAMPLE_TEXTURE2D(_TranslucencyMap, sampler_TranslucencyMap, IN.uv).r;
                float3 transDir  = normalize(mainLight.direction + N * _TranslucencyOffset);
                float  back  = saturate(dot(V, -transDir));
                float  trans = pow(back, _TranslucencyFalloff) * _Translucency * transMask;
                float3 translucency = (mainRadiance * _TranslucencyDirect + ambient * _TranslucencyIndirect)
                                    * trans * _SubsurfaceColor.rgb;
                #else
                float3 translucency = 0;
                #endif

                half3 color = albedo.rgb * (diffuse + ambient + translucency) + specular + envSpecular;
                color = MixFog(color, IN.fogFactor);

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Shadow caster
        // -----------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            AlphaToMask [_AlphaToCoverage]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVertex
            #pragma fragment ShadowFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ _GPU_GRASS_BATCHING
            #pragma target 4.5 _GPU_GRASS_BATCHING
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ _BILLBOARD
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHATOCOVERAGE_ON

            float3 _LightDirection;
            float3 _LightPosition;

            float3 ShadowLightDirection(float3 positionWS)
            {
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    return normalize(_LightPosition - positionWS);
                #else
                    return _LightDirection;
                #endif
            }

            float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
            {
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            GrassDepthVaryings ShadowVertex(GrassAttributes IN)
            {
                GrassDepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS;
                float3 normalWS;
                float3 originWS;
                float4 instTint;
                float2 rand;
                float  gust;
                GrassTransform(IN, positionWS, normalWS, originWS, instTint, rand, gust);

                float3 lightDirectionWS = ShadowLightDirection(positionWS);

                OUT.positionCS = GetShadowPositionHClip(positionWS, normalWS, lightDirectionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowFade = 1.0;

                #if defined(_BILLBOARD)
                // A camera-facing card turned edge-on to the light casts a sliver; the fade thins
                // its shadow in proportion instead of leaving a hard line on the ground.
                OUT.shadowFade = lerp(1.0, saturate(abs(dot(normalWS, lightDirectionWS))),
                                      _BillboardShadowFade * _Billboard);
                #endif

                return OUT;
            }

            half4 ShadowFragment(GrassDepthVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON) || defined(_ALPHATOCOVERAGE_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a * _Color.a;
                    clip(a * IN.shadowFade - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Depth only
        // -----------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]
            AlphaToMask [_AlphaToCoverage]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _GPU_GRASS_BATCHING
            #pragma target 4.5 _GPU_GRASS_BATCHING
            #pragma shader_feature_local _ _BILLBOARD
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHATOCOVERAGE_ON

            GrassDepthVaryings DepthVertex(GrassAttributes IN)
            {
                GrassDepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS;
                float3 normalWS;
                float3 originWS;
                float4 instTint;
                float2 rand;
                float  gust;
                GrassTransform(IN, positionWS, normalWS, originWS, instTint, rand, gust);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowFade = 1.0;
                return OUT;
            }

            half4 DepthFragment(GrassDepthVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON) || defined(_ALPHATOCOVERAGE_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a * _Color.a;
                    clip(a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Depth normals
        // -----------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]
            AlphaToMask [_AlphaToCoverage]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _GPU_GRASS_BATCHING
            #pragma target 4.5 _GPU_GRASS_BATCHING
            #pragma shader_feature_local _ _BILLBOARD
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHATOCOVERAGE_ON

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            DNVaryings DepthNormalsVertex(GrassAttributes IN)
            {
                DNVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS;
                float3 normalWS;
                float3 originWS;
                float4 instTint;
                float2 rand;
                float  gust;
                GrassTransform(IN, positionWS, normalWS, originWS, instTint, rand, gust);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                // The normals target keeps the geometric normal unless the material asks for the
                // same remap the lighting uses.
                OUT.normalWS   = lerp(normalWS,
                                      GrassShadingNormal(normalWS, positionWS, originWS, GrassTipness(IN.positionOS.xyz)),
                                      saturate(_NormalFlattenDepthNormals));
                return OUT;
            }

            half4 DepthNormalsFragment(DNVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON) || defined(_ALPHATOCOVERAGE_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a * _Color.a;
                    clip(a - _Cutoff);
                #endif
                // URP's _CameraNormalsTexture holds raw signed world normals; a 0..1 remap here
                // would halve every normal the consumers of that target read back.
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
