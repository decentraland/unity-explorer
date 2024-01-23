//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA
//#define DEFAULT_GUI

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if URP
using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using UI = StylizedGrass.StylizedGrassGUI;

namespace StylizedGrass
{
    [HelpURL(("http://staggart.xyz/unity/stylized-grass-shader/sgs-docs/?section=grass-shader"))]
    public class MaterialUI : ShaderGUI
    {
#if URP
        public MaterialEditor materialEditor;
        
        private MaterialProperty _BaseMap;
        private MaterialProperty _BumpMap;
        private MaterialProperty _BumpScale;
        private MaterialProperty _Cutoff;
        private MaterialProperty _AlphaToCoverage;
        private MaterialProperty _BaseColor;
        private MaterialProperty _EmissionColor;

        private MaterialProperty _HueVariation;
        private MaterialProperty _HueVariationHeight;
        private MaterialProperty _ColorMapStrength;
        private MaterialProperty _ColorMapHeight;
        private MaterialProperty _ScalemapInfluence;
        private MaterialProperty _OcclusionStrength;
        private MaterialProperty _VertexDarkening;
        private MaterialProperty _Smoothness;
        private MaterialProperty _TranslucencyDirect;
        private MaterialProperty _TranslucencyIndirect;
        private MaterialProperty _TranslucencyOffset;
        private MaterialProperty _TranslucencyFalloff;
        private MaterialProperty _NormalFlattening;
        private MaterialProperty _NormalSpherify;
        private MaterialProperty _NormalSpherifyMask;
        private MaterialProperty _NormalFlattenDepthNormals;

        private MaterialProperty _BendMode;
        private MaterialProperty _BendPushStrength;
        private MaterialProperty _BendFlattenStrength;
        private MaterialProperty _PerspectiveCorrection;
        private MaterialProperty _BendTint;

        private MaterialProperty _WindAmbientStrength;
        private MaterialProperty _WindSpeed;
        private MaterialProperty _WindDirection;
        private MaterialProperty _WindVertexRand;
        private MaterialProperty _WindObjectRand;
        private MaterialProperty _WindRandStrength;
        private MaterialProperty _WindSwinging;
        private MaterialProperty _WindMap;
        private MaterialProperty _WindGustStrength;
        private MaterialProperty _WindGustFreq;
        private MaterialProperty _WindGustSpeed;
        private MaterialProperty _WindGustTint;
        
        private MaterialProperty _FadingOn;
        private MaterialProperty _FadeNear;
        private MaterialProperty _FadeFar;
        private MaterialProperty _FadeAngleThreshold;
        private MaterialProperty _Cull;

        private MaterialProperty _ReceiveShadows;
        private MaterialProperty _LightingMode;
        private MaterialProperty _EnvironmentReflections;
        private MaterialProperty _SpecularHighlights;
        private MaterialProperty _Scalemap;
        private MaterialProperty _Billboard;

        private MaterialProperty _CurvedWorldBendSettings;

        private MaterialProperty _VertexColorShadingChannel;
        private MaterialProperty _VertexColorWindChannel;
        private MaterialProperty _VertexColorBendingChannel;
        
        private UI.Material.Section renderingSection;
        private UI.Material.Section mapsSection;
        private UI.Material.Section colorSection;
        private UI.Material.Section shadingSection;
        private UI.Material.Section verticesSection;
        private UI.Material.Section windSection;

        private bool initialized;

        private void OnEnable(MaterialEditor materialEditorIn)
        {
            renderingSection = new StylizedGrassGUI.Material.Section(materialEditorIn, "RENDERING", "Rendering");
            mapsSection = new StylizedGrassGUI.Material.Section(materialEditorIn,"MAPS", "Main maps");
            colorSection = new StylizedGrassGUI.Material.Section(materialEditorIn,"COLOR", "Color");
            shadingSection = new StylizedGrassGUI.Material.Section(materialEditorIn,"SHADING", "Shading");
            verticesSection = new StylizedGrassGUI.Material.Section(materialEditorIn,"VERTICES", "Vertices");
            windSection = new StylizedGrassGUI.Material.Section(materialEditorIn,"WIND", "Wind");

            ShaderConfigurator.GetIntegration((materialEditor.target as Material).shader);

            foreach (var obj in materialEditorIn.targets)
            {
                MaterialChanged((Material)obj);
            }
        }
        public void FindProperties(MaterialProperty[] props, Material material)
        {
            _Cull = FindProperty("_Cull", props);
            
            _BaseMap = FindProperty("_BaseMap", props);
            _Cutoff = FindProperty("_Cutoff", props);
            _AlphaToCoverage = FindProperty("_AlphaToCoverage", props);
            _BumpMap = FindProperty("_BumpMap", props);
            _BumpScale =  FindProperty("_BumpScale", props);
            _BaseColor = FindProperty("_BaseColor", props);
            _HueVariation = FindProperty("_HueVariation", props);
            _HueVariationHeight = FindProperty("_HueVariationHeight", props);
            _EmissionColor = FindProperty("_EmissionColor", props);
            
            _FadingOn = FindProperty("_FadingOn", props);
            _FadeNear = FindProperty("_FadeNear", props);
            _FadeFar = FindProperty("_FadeFar", props);
            
            _ColorMapStrength = FindProperty("_ColorMapStrength", props);
            _ColorMapHeight = FindProperty("_ColorMapHeight", props);
            _ScalemapInfluence = FindProperty("_ScalemapInfluence", props);

            _OcclusionStrength = FindProperty("_OcclusionStrength", props);
            _VertexDarkening = FindProperty("_VertexDarkening", props);
            _Smoothness = FindProperty("_Smoothness", props);
            _TranslucencyDirect = FindProperty("_TranslucencyDirect", props);
            _TranslucencyIndirect = FindProperty("_TranslucencyIndirect", props);
            _TranslucencyOffset = FindProperty("_TranslucencyOffset", props);
            _TranslucencyFalloff = FindProperty("_TranslucencyFalloff", props);
            
            _NormalFlattening = FindProperty("_NormalFlattening", props);
            _NormalSpherify = FindProperty("_NormalSpherify", props);
            _NormalSpherifyMask = FindProperty("_NormalSpherifyMask", props);
            _NormalFlattenDepthNormals = FindProperty("_NormalFlattenDepthNormals", props);

            _WindAmbientStrength = FindProperty("_WindAmbientStrength", props);
            _WindSpeed = FindProperty("_WindSpeed", props);
            _WindDirection = FindProperty("_WindDirection", props);
            _WindVertexRand = FindProperty("_WindVertexRand", props);
            _WindObjectRand = FindProperty("_WindObjectRand", props);
            _WindRandStrength = FindProperty("_WindRandStrength", props);
            _WindSwinging = FindProperty("_WindSwinging", props);

            _BendMode = FindProperty("_BendMode", props);
            _BendPushStrength = FindProperty("_BendPushStrength", props);
            _BendFlattenStrength = FindProperty("_BendFlattenStrength", props);
            _PerspectiveCorrection = FindProperty("_PerspectiveCorrection", props);
            _BendTint = FindProperty("_BendTint", props);

            _WindMap = FindProperty("_WindMap", props);
            _WindGustStrength = FindProperty("_WindGustStrength", props);
            _WindGustFreq = FindProperty("_WindGustFreq", props);
            _WindGustSpeed = FindProperty("_WindGustSpeed", props);
            _WindGustTint = FindProperty("_WindGustTint", props);

            _LightingMode = FindProperty("_LightingMode", props);
            _ReceiveShadows = FindProperty("_ReceiveShadows", props);
            _EnvironmentReflections = FindProperty("_EnvironmentReflections", props);
            _SpecularHighlights = FindProperty("_SpecularHighlights", props);
            _Scalemap = FindProperty("_Scalemap", props);
            _Billboard = FindProperty("_Billboard", props);
            _FadeAngleThreshold = FindProperty("_FadeAngleThreshold", props);
            
            _VertexColorShadingChannel = FindProperty("_VertexColorShadingChannel", props);
            _VertexColorWindChannel = FindProperty("_VertexColorWindChannel", props);
            _VertexColorBendingChannel = FindProperty("_VertexColorBendingChannel", props);
            
            if(material.HasProperty("_CurvedWorldBendSettings")) _CurvedWorldBendSettings = FindProperty("_CurvedWorldBendSettings", props);
        }

        public override void OnMaterialPreviewGUI(MaterialEditor materialEditor, Rect r, GUIStyle background)
        {
            GUI.DrawTexture(r, UI.AssetIcon, ScaleMode.ScaleToFit);
        }

        public override void OnClosed(Material material)
        {
            initialized = false;
        }

        //https://github.com/Unity-Technologies/ScriptableRenderPipeline/blob/648184ec8405115e2fcf4ad3023d8b16a191c4c7/com.unity.render-pipelines.universal/Editor/ShaderGUI/BaseShaderGUI.cs
        public override void OnGUI(MaterialEditor materialEditorIn, MaterialProperty[] props)
        {
            
            this.materialEditor = materialEditorIn;

            materialEditor.SetDefaultGUIWidths();
            materialEditor.UseDefaultMargins();
            EditorGUIUtility.labelWidth = 0f;

            Material material = materialEditor.target as Material;
            FindProperties(props, material);
            
#if DEFAULT_GUI
            base.OnGUI(materialEditor, props);
            return;
#endif
            
            if (!initialized)
            {
                OnEnable(materialEditor);
                initialized = true;
            }

            ShaderPropertiesGUI(material);
            
            EditorGUILayout.Space();
            
            StylizedGrassGUI.DrawFooter();
        }

        public void ShaderPropertiesGUI(Material material)
        {
            EditorGUI.BeginChangeCheck();
            
            #if UNITY_2022_1_OR_NEWER
            //As of this version extra padding seems to be added, to make room for property override indicators
            EditorGUI.indentLevel--;
            #endif
            
            DrawHeader();
            
            EditorGUILayout.Space();
            
            DrawRendering();
            DrawMaps();
            DrawColor();
            DrawShading();
            DrawVertices(material);
            DrawWind();
            
            EditorGUILayout.Separator();
            
            DrawStandardFields(material);
            
            EditorGUILayout.Space();

            DrawIntegrations();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var obj in  materialEditor.targets)
                    MaterialChanged((Material)obj);
            }
        }

        public void MaterialChanged(Material material)
        {
            if (material == null) throw new ArgumentNullException("material");

            SetMaterialKeywords(material);
        }

        private void SetMaterialKeywords(Material material)
        {
            // Clear all keywords for fresh start
            material.shaderKeywords = null;
            
#if URP
            //Keywords
            int lightingMode = (int)material.GetFloat("_LightingMode");
            CoreUtils.SetKeyword(material, "_NORMALMAP", _BumpMap.textureValue && lightingMode > 0);
            CoreUtils.SetKeyword(material, "_SIMPLE_LIGHTING", lightingMode == 1);
            CoreUtils.SetKeyword(material, "_ADVANCED_LIGHTING", lightingMode == 2);
            
            CoreUtils.SetKeyword(material, "_RECEIVE_SHADOWS_OFF", material.GetFloat("_ReceiveShadows") == 0.0f);
            CoreUtils.SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", material.GetFloat("_EnvironmentReflections") == 0.0f);
            CoreUtils.SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", material.GetFloat("_SpecularHighlights") == 0.0f);

            CoreUtils.SetKeyword(material, "_SCALEMAP", material.GetFloat("_Scalemap") == 1.0f);
            CoreUtils.SetKeyword(material, "_BILLBOARD", material.GetFloat("_Billboard") == 1.0f);
            CoreUtils.SetKeyword(material, "_FADING", material.GetFloat("_FadingOn") == 1.0f);
#endif
        }

        private void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect();
            
            GUIContent c = new GUIContent($"{AssetInfo.ASSET_NAME} v{AssetInfo.INSTALLED_VERSION}");
            rect.width = EditorStyles.label.CalcSize(c).x;
            //rect.x += (rect.width * 2f);
            rect.y -= 3f;
            GUI.Label(rect, c, EditorStyles.label);

            rect.x += rect.width + 3f;
            rect.y += 2f;
            rect.width = 16f;
            rect.height = 16f;
            
            GUI.DrawTexture(rect, EditorGUIUtility.IconContent("preAudioLoopOff").image);
            if (Event.current.type == EventType.MouseDown)
            {
                if (rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
                {
                    AssetInfo.VersionChecking.GetLatestVersionPopup();
                    Event.current.Use();
                }
            }

            if (rect.Contains(Event.current.mousePosition))
            {
                Rect tooltipRect = rect;
                tooltipRect.y -= 20f;
                tooltipRect.width = 120f;
                GUI.Label(tooltipRect, "Check for update", GUI.skin.button);
            }

        }
        
        private void DrawRendering()
        {
            renderingSection.Expanded = UI.Material.DrawHeader(renderingSection.title, renderingSection.Expanded, () => SwitchSection(renderingSection));
            renderingSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(renderingSection.anim.faded))
            {
                EditorGUILayout.Space();
                
                materialEditor.ShaderProperty(_LightingMode, new GUIContent("Lighting mode", "None: Unlit, no lighting is applied, material purely displays its base color\n\n" +
                                                                                             "Simple: Similar to the Simple Lit URP shader\n\n" +
                                                                                             "Advanced: Similar to the Lit URP shader"));
                
                materialEditor.ShaderProperty(_Cull, _Cull.displayName);
                
                using(new EditorGUI.DisabledGroupScope(_LightingMode.floatValue == 0))
                {
                    materialEditor.ShaderProperty(_ReceiveShadows, new GUIContent("Receive shadows", "Apply shadows cast by other objects onto this material.\n\nShadow casting behaviour can be set on a per Mesh Renderer basis."));
                }

                materialEditor.ShaderProperty(_AlphaToCoverage, new GUIContent("Alpha to coverage", "Reduces aliasing when using MSAA"));
                if (_AlphaToCoverage.floatValue > 0 && UniversalRenderPipeline.asset.msaaSampleCount == 1) EditorGUILayout.HelpBox("MSAA is disabled, alpha to coverage will have no effect", MessageType.None);
                
                materialEditor.ShaderProperty(_Billboard, new GUIContent(_Billboard.displayName, "Force the Z-axis of the mesh to face the camera (Requires the GrassBillboardQuad mesh!)"));

                EditorGUILayout.Space();
                

                materialEditor.ShaderProperty(_FadingOn, new GUIContent("Distance/Angle fading", "Reduces the alpha clipping based on camera distance and viewing angle (relative to the geometry)." +
                                                                                                   "\n\nNote that this does not improve performance, only pixels are being hidden, meshes are still being rendered, " +
                                                                                                   "best to match these settings to your maximum grass draw distance"));

                if (_FadingOn.floatValue > 0f || _FadingOn.hasMixedValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Surface angle based", EditorStyles.boldLabel);

                    materialEditor.ShaderProperty(_FadeAngleThreshold, new GUIContent("Angle threshold", "Fadeout the mesh's facing that aren't facing the camera using dithering. The angle relates to the camera's viewing direction, along the geometry surface."));
                    
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Distance based", EditorStyles.boldLabel);

                    materialEditor.ShaderProperty(_FadeNear, _FadeNear.displayName);
                    materialEditor.ShaderProperty(_FadeFar, _FadeFar.displayName);
                    EditorGUI.indentLevel--;

                }
                
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFadeGroup();
            
        }

        private void DrawMaps()
        {
            mapsSection.Expanded = UI.Material.DrawHeader(mapsSection.title, mapsSection.Expanded, () => SwitchSection(mapsSection));
            mapsSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(mapsSection.anim.faded))
            {
                EditorGUILayout.Space();
                
                materialEditor.TextureProperty(_BaseMap, "Texture (A=Alpha)");
                materialEditor.ShaderProperty(_Cutoff, "Alpha clipping");
                materialEditor.TextureProperty(_BumpMap, "Normal map");
                materialEditor.ShaderProperty(_BumpScale, "Strength");
                
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFadeGroup();
        }

        private void DrawColor()
        {
            colorSection.Expanded = UI.Material.DrawHeader(colorSection.title, colorSection.Expanded, () => SwitchSection(colorSection));
            colorSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(colorSection.anim.faded))
            {
                EditorGUILayout.Space();

                materialEditor.ShaderProperty(_BaseColor, new GUIContent(_BaseColor.displayName, "This color is multiplied with the texture. Use a white texture to color the grass by this value entirely."));
                materialEditor.ShaderProperty(_HueVariation, new GUIContent(_HueVariation.displayName, "Every object will receive a random color between this color, and the main color. The alpha channel controls the intensity"));
                materialEditor.ShaderProperty(_HueVariationHeight, "Height", 1);
                materialEditor.ShaderProperty(_EmissionColor, new GUIContent(_EmissionColor.displayName, "Color is added on top of everything, and contributes to HDR"));
                
                materialEditor.ShaderProperty(_VertexColorShadingChannel, new GUIContent("Vertex Color Shading Channel", "Vertex color used for masking Ambient Occlusion, translucency, color variation and color map height"));
                EditorGUILayout.Space();
                
                materialEditor.ShaderProperty(_OcclusionStrength, new GUIContent(_OcclusionStrength.displayName, "Darkens the mesh based on the red vertex color painted into the mesh"));
                materialEditor.ShaderProperty(_VertexDarkening, new GUIContent(_VertexDarkening.displayName, "Gives each vertex a random darker tint. Use in moderation to slightly break up visual repetition"));

                EditorGUILayout.Space();
                
                EditorGUILayout.LabelField("Color map", EditorStyles.boldLabel);
                if (!GrassColorMap.Active) EditorGUILayout.HelpBox("No color map is currently active", MessageType.None);
                materialEditor.ShaderProperty(_ColorMapStrength, new GUIContent("Strength", "Controls the much the color map influences the material. Overrides any other colors"));
                materialEditor.ShaderProperty(_ColorMapHeight, new GUIContent("Height", "Controls which part of the mesh is affected, from bottom to top (based on the mesh's red vertex colors"));

                EditorGUILayout.Space();

                materialEditor.ShaderProperty(_BendTint, new GUIContent(_BendTint.displayName, "Multiplies the base color by this color, where ever something is bending the grass." +
                                                                                               "\n\nThis only applies to the top of the grass, so the difference in color doesn't clash with the underlying terrain color"));
                
                EditorGUILayout.Space();

            }
            EditorGUILayout.EndFadeGroup();
        }

        private void DrawShading()
        {
            shadingSection.Expanded = UI.Material.DrawHeader(shadingSection.title, shadingSection.Expanded, () => SwitchSection(shadingSection));
            shadingSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(shadingSection.anim.faded))
            {
                EditorGUILayout.Space();

                if (_LightingMode.floatValue == 2f || _LightingMode.hasMixedValue)
                {
                    materialEditor.ShaderProperty(_EnvironmentReflections, new GUIContent(_EnvironmentReflections.displayName, "Enables reflections from skybox and reflection probes"));
                    #if UNITY_2022_1_OR_NEWER
                    var customReflectionUsed = RenderSettings.customReflectionTexture;
                    #else
                    var customReflectionUsed = RenderSettings.customReflection;
                    #endif
                    if (_EnvironmentReflections.floatValue == 1f && RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom && !customReflectionUsed)
                    {
                        EditorGUILayout.HelpBox("Environment reflection source is set to \"Custom\" but no cubemap is assigned. ", MessageType.Warning);

                    }
                    materialEditor.ShaderProperty(_SpecularHighlights, new GUIContent(_SpecularHighlights.displayName, "Enables specular reflections from lights"));

                    materialEditor.ShaderProperty(_Smoothness, new GUIContent(_Smoothness.displayName, "Controls how strongly the skybox and reflection probes affect the material (similar to PBR smoothness)"));
                }
                
                EditorGUILayout.Space();

                EditorGUILayout.LabelField(new GUIContent("Translucency"), EditorStyles.boldLabel);

                materialEditor.ShaderProperty(_TranslucencyDirect, new GUIContent("Direct (back)", "Simulates sun light passing through the grass. Most noticeable at glancing or low sun angles\n\nControls the strength of light hitting the BACK"));
                materialEditor.ShaderProperty(_TranslucencyIndirect, new GUIContent("Indirect (front)", "Simulates sun light passing through the grass. Most noticeable at glancing or low sun angles\n\nControls the strength of light hitting the FRONT"));
                
                materialEditor.ShaderProperty(_TranslucencyFalloff, new GUIContent("Exponent", "Controls the size of the effect"));
                materialEditor.ShaderProperty(_TranslucencyOffset, new GUIContent("Offset", "Controls how much the effect wraps around the mesh. This at least requires spherical normals to take effect"));

                EditorGUILayout.Space();

                EditorGUILayout.LabelField(new GUIContent("Normals", "Normals control the orientation of the vertices for lighting effect"), EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_NormalFlattening, new GUIContent("Flatten normals (lighting)", "Gradually has the normals point straight up, this will help match the shading to the surface the grass is placed on."));
                materialEditor.ShaderProperty(_NormalSpherify, new GUIContent("Spherify normals", "Gradually has the normals point away from the object's pivot point. For grass this results in fluffy-like shading"));
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_NormalSpherifyMask, new GUIContent("Tip mask", "Only apply spherifying to the top of the mesh (based on the red vertex color channel of the mesh"));
                EditorGUI.indentLevel--;
                
                #if !UNITY_2020_2_OR_NEWER
                using (new EditorGUI.DisabledScope(true))
                #endif
                {
                    materialEditor.ShaderProperty(_NormalFlattenDepthNormals, new GUIContent("Flatten normals (SSAO)", "(When rendering the object during the Depth Normals prepass). Gradually has the normals point straight up, this determines how SSAO will affect the grass"));
                    #if !UNITY_2020_2_OR_NEWER
                    EditorGUILayout.HelpBox("Only has effect in Unity 2020.2+", MessageType.None);
                    #endif
                }
                EditorGUILayout.Space();

            }
            EditorGUILayout.EndFadeGroup();
        }

        private void DrawVertices(Material material)
        {
            verticesSection.Expanded = UI.Material.DrawHeader(verticesSection.title, verticesSection.Expanded, () => SwitchSection(verticesSection));
            verticesSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(verticesSection.anim.faded))
            {
                EditorGUILayout.Space();
                
                 if (material.HasProperty("_CurvedWorldBendSettings"))
                {
                    EditorGUILayout.LabelField("Curved World 2020", EditorStyles.boldLabel);
                    materialEditor.ShaderProperty(_CurvedWorldBendSettings, _CurvedWorldBendSettings.displayName);
                    EditorGUILayout.Space();
                }
                 
                materialEditor.ShaderProperty(_VertexColorBendingChannel, new GUIContent("Vertex Color Mask", "Vertex color used for denote parts of the mesh that will be influenced by bending"));

                materialEditor.ShaderProperty(_BendMode, new GUIContent(_BendMode.displayName, "Per-vertex: Bending is applied on a per-vertex basis\n\nUniform: Applied to all vertices at once, use this for plants/flowers to avoid distorting the mesh"));

                materialEditor.ShaderProperty(_BendPushStrength, new GUIContent(_BendPushStrength.displayName, "The amount of pushing the material should receive by Grass Benders"));
                materialEditor.ShaderProperty(_BendFlattenStrength, new GUIContent(_BendFlattenStrength.displayName, "A multiplier for how much the material is flattened by Grass Benders"));

                EditorGUILayout.Space();

                materialEditor.ShaderProperty(_PerspectiveCorrection, new GUIContent(_PerspectiveCorrection.displayName, "The amount by which the grass is gradually bent away from the camera as it looks down. Useful for better coverage in top-down perspectives"));
                
                EditorGUILayout.Space();

                materialEditor.ShaderProperty(_Scalemap, new GUIContent("Apply scale map", "Enable scaling through terrain-layer heightmap"));
                if (_Scalemap.floatValue == 1 || _Scalemap.hasMixedValue)
                {
                    if (!GrassColorMap.Active) EditorGUILayout.HelpBox("No color map is currently active", MessageType.None);
                    else
                    {
                        if (GrassColorMap.Active.hasScalemap == false) EditorGUILayout.HelpBox("Active color map has no scale information", MessageType.None);
                    }
                    
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_ScalemapInfluence, new GUIContent("Scale influence (XYZ)", "Controls the scale strength of the heightmap per axis"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

            }
            EditorGUILayout.EndFadeGroup();
        }

        private void DrawWind()
        {
            windSection.Expanded = UI.Material.DrawHeader(windSection.title, windSection.Expanded, () => SwitchSection(windSection));
            windSection.SetTarget();

            if (EditorGUILayout.BeginFadeGroup(windSection.anim.faded))
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.LabelField("Wind", EditorStyles.boldLabel);

                if (StylizedGrassRenderer.Instance && StylizedGrassRenderer.Instance.windZone && StylizedGrassRenderer.Instance.listenToWindZone)
                {
                    StylizedGrassGUI.DrawActionBox("  Wind strength and speed are influenced by a Wind Zone component", "Select Wind Zone", MessageType.Info, () => Selection.activeObject = StylizedGrassRenderer.Instance.windZone);
                }
                materialEditor.ShaderProperty(_VertexColorWindChannel, new GUIContent("Vertex Color Mask", "Vertex color used for denote parts of the mesh that will be influenced by wind"));

                materialEditor.ShaderProperty(_WindAmbientStrength, new GUIContent(_WindAmbientStrength.displayName, "The amount of wind that is applied without gusting"));
                materialEditor.ShaderProperty(_WindSpeed, new GUIContent(_WindSpeed.displayName, "The speed the wind and gusting moves at"));
                materialEditor.ShaderProperty(_WindDirection, new GUIContent(_WindDirection.displayName, "The Y and W components are unused"));
                materialEditor.ShaderProperty(_WindSwinging, new GUIContent(_WindSwinging.displayName, "Controls the amount the grass is able to spring back against the wind direction"));

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_WindObjectRand, new GUIContent("Per-object", "Adds a per-object offset, making each object move randomly rather than in unison"));
                materialEditor.ShaderProperty(_WindVertexRand, new GUIContent("Per-vertex", "Adds a per-vertex offset"));
                materialEditor.ShaderProperty(_WindRandStrength, new GUIContent(_WindRandStrength.displayName, "Gives each object a random wind strength. This is useful for breaking up repetition and gives the impression of turbulence"));

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Gusting", EditorStyles.boldLabel);
                materialEditor.TexturePropertySingleLine(new GUIContent("Gust texture (Grayscale)"), _WindMap);
                materialEditor.ShaderProperty(_WindGustStrength, new GUIContent("Strength", "Gusting add wind strength based on the gust texture, which moves over the grass"));
                materialEditor.ShaderProperty(_WindGustSpeed, "Speed");
                materialEditor.ShaderProperty(_WindGustFreq, new GUIContent("Frequency", "Controls the tiling of the gusting texture, essentially setting the size of the gusting waves"));
                materialEditor.ShaderProperty(_WindGustTint, new GUIContent("Max. Color tint", "Uses the gusting texture to add a brighter tint based on the gusting strength"));

                EditorGUILayout.Space();

            }
            EditorGUILayout.EndFadeGroup();
        }

        private void DrawStandardFields(Material material)
        {
            EditorGUILayout.LabelField("Native settings", EditorStyles.boldLabel);

            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
            materialEditor.LightmapEmissionProperty();
        }
        private void DrawIntegrations()
        {
            EditorGUILayout.LabelField("Third-party renderer integration:", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(ShaderConfigurator.CurrentIntegration.ToString());
                if (GUILayout.Button("Change", GUILayout.MaxWidth(100f)))
                {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(new GUIContent("None"), ShaderConfigurator.CurrentIntegration == ShaderConfigurator.Integration.None, () => ShaderConfigurator.SetIntegration(ShaderConfigurator.Integration.None));
                    menu.AddItem(new GUIContent("Vegetation Studio (Pro)"), ShaderConfigurator.CurrentIntegration == ShaderConfigurator.Integration.VegetationStudio, () => ShaderConfigurator.SetIntegration(ShaderConfigurator.Integration.VegetationStudio));
                    menu.AddItem(new GUIContent("GPU Instancer"), ShaderConfigurator.CurrentIntegration == ShaderConfigurator.Integration.GPUInstancer, () => ShaderConfigurator.SetIntegration(ShaderConfigurator.Integration.GPUInstancer));
                    menu.AddItem(new GUIContent("Nature Renderer"), ShaderConfigurator.CurrentIntegration == ShaderConfigurator.Integration.NatureRenderer, () => ShaderConfigurator.SetIntegration(ShaderConfigurator.Integration.NatureRenderer));
                    menu.AddItem(new GUIContent("Nature Renderer 2021"), ShaderConfigurator.CurrentIntegration == ShaderConfigurator.Integration.NatureRenderer2021, () => ShaderConfigurator.SetIntegration(ShaderConfigurator.Integration.NatureRenderer2021));

                    menu.ShowAsContext();
                }
            }
        }
        
        private void SwitchSection(UI.Material.Section s)
        {
            /*
            renderingSection.Expanded = (s == renderingSection) ? !renderingSection.Expanded : false;
            mapsSection.Expanded = (s == mapsSection) ? !mapsSection.Expanded : false;
            colorSection.Expanded = (s == colorSection) ? !colorSection.Expanded : false;
            shadingSection.Expanded = (s == shadingSection) ? !shadingSection.Expanded : false;
            verticesSection.Expanded = (s == verticesSection) ? !verticesSection.Expanded : false;
            windSection.Expanded = (s == windSection) ? !windSection.Expanded : false;
            */
        }
#else
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            EditorGUILayout.HelpBox("The Universal Render Pipeline v" + AssetInfo.MIN_URP_VERSION + " is not installed (Package Manager).", MessageType.Error);
        }
#endif
    }
    
    public class MinMaxSlider : MaterialPropertyDrawer
    {
        private const float FIELD_WIDTH = 70f;

        private readonly float minLimit;
        private readonly float maxLimit = 500f;

        public MinMaxSlider(float min, float max)
        {
            this.minLimit = min;
            this.maxLimit = max;
        }
        
        public override void OnGUI (Rect position, MaterialProperty prop, String label, MaterialEditor editor)
        {
            #if UNITY_2022_1_OR_NEWER
            MaterialEditor.BeginProperty(prop);
            #endif
            
            float minVal = prop.vectorValue.x;
            float maxVal = prop.vectorValue.y;
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;

            Rect labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelRect, label);
            
            Rect minPos = position;
            minPos.x = EditorGUIUtility.labelWidth;
            minPos.width = FIELD_WIDTH;
            minVal = EditorGUI.FloatField(minPos, minVal);

            Rect sliderPos = position;
            sliderPos.x = minPos.x + minPos.width;
            sliderPos.width -= EditorGUIUtility.labelWidth + (FIELD_WIDTH * 2f);
            EditorGUI.MinMaxSlider(sliderPos, ref minVal, ref maxVal, minLimit, maxLimit);
            
            Rect maxPos = position;
            maxPos.x = sliderPos.x + sliderPos.width;
            maxPos.width = FIELD_WIDTH;
            maxVal = EditorGUI.FloatField(maxPos, maxVal);

            EditorGUI.showMixedValue = false;
            
            if (EditorGUI.EndChangeCheck())
            {
                prop.vectorValue = new Vector4(minVal, maxVal, 0f, 0f);
            }
            
            #if UNITY_2022_1_OR_NEWER
            MaterialEditor.EndProperty();
            #endif
        }
    }
}