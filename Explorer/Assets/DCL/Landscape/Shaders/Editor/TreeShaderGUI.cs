using UnityEngine;
using UnityEditor;
using System;

public class TreeShaderGUI : ShaderGUI
{
    private MaterialEditor m_MaterialEditor;
    private MaterialProperty[] m_Properties;

    // Properties
    private MaterialProperty baseMapProp;
    private MaterialProperty baseColorProp;
    private MaterialProperty cutoffProp;
    private MaterialProperty smoothnessProp;
    private MaterialProperty specColorProp;
    private MaterialProperty specGlossMapProp;
    private MaterialProperty smoothnessSourceProp;
    private MaterialProperty specularHighlightsProp;
    private MaterialProperty bumpMapProp;
    private MaterialProperty bumpScaleProp;
    private MaterialProperty emissionColorProp;
    private MaterialProperty emissionMapProp;
    private MaterialProperty receiveShadowsProp;

    // Wind properties
    private MaterialProperty requiresWindProp;
    private MaterialProperty windSpeedProp;
    private MaterialProperty windStrengthProp;
    private MaterialProperty windFrequencyProp;
    private MaterialProperty windTurbulenceProp;
    private MaterialProperty windDirectionProp;

    // Flutter properties
    private MaterialProperty flutterSpeedProp;
    private MaterialProperty flutterStrengthProp;
    private MaterialProperty flutterFrequencyProp;

    // Surface properties
    private MaterialProperty surfaceProp;
    private MaterialProperty blendProp;
    private MaterialProperty cullProp;
    private MaterialProperty alphaClipProp;
    private MaterialProperty srcBlendProp;
    private MaterialProperty dstBlendProp;
    private MaterialProperty srcBlendAlphaProp;
    private MaterialProperty dstBlendAlphaProp;
    private MaterialProperty zWriteProp;

    private bool m_FirstTimeApply = true;
    private bool showWindSettings = true;
    private bool showFlutterSettings = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        m_MaterialEditor = materialEditor;
        m_Properties = properties;

        Material material = materialEditor.target as Material;

        if (m_FirstTimeApply)
        {
            OnOpenGUI(material, materialEditor);
            m_FirstTimeApply = false;
        }

        FindProperties(properties);
        ShaderPropertiesGUI(material);
    }

    public virtual void OnOpenGUI(Material material, MaterialEditor materialEditor)
    {
        // Foldout states
        showWindSettings = EditorPrefs.GetBool("TreeShaderGUI_WindSettings", true);
        showFlutterSettings = EditorPrefs.GetBool("TreeShaderGUI_FlutterSettings", true);
    }

    public void FindProperties(MaterialProperty[] properties)
    {
        // Main properties
        baseMapProp = FindProperty("_BaseMap", properties);
        baseColorProp = FindProperty("_BaseColor", properties);
        cutoffProp = FindProperty("_Cutoff", properties, false);
        smoothnessProp = FindProperty("_Smoothness", properties, false);
        specColorProp = FindProperty("_SpecColor", properties, false);
        specGlossMapProp = FindProperty("_SpecGlossMap", properties, false);
        smoothnessSourceProp = FindProperty("_SmoothnessSource", properties, false);
        specularHighlightsProp = FindProperty("_SpecularHighlights", properties, false);

        // Normal map
        bumpMapProp = FindProperty("_BumpMap", properties, false);
        bumpScaleProp = FindProperty("_BumpScale", properties, false);

        // Emission
        emissionColorProp = FindProperty("_EmissionColor", properties, false);
        emissionMapProp = FindProperty("_EmissionMap", properties, false);

        // Shadows
        receiveShadowsProp = FindProperty("_ReceiveShadows", properties, false);

        // Wind properties
        requiresWindProp = FindProperty("_RequiresWind", properties, false);
        windSpeedProp = FindProperty("_WindSpeed", properties, false);
        windStrengthProp = FindProperty("_WindStrength", properties, false);
        windFrequencyProp = FindProperty("_WindFrequency", properties, false);
        windTurbulenceProp = FindProperty("_WindTurbulence", properties, false);
        windDirectionProp = FindProperty("_WindDirection", properties, false);

        // Flutter properties
        flutterSpeedProp = FindProperty("_FlutterSpeed", properties, false);
        flutterStrengthProp = FindProperty("_FlutterStrength", properties, false);
        flutterFrequencyProp = FindProperty("_FlutterFrequency", properties, false);

        // Surface properties
        surfaceProp = FindProperty("_Surface", properties, false);
        blendProp = FindProperty("_Blend", properties, false);
        cullProp = FindProperty("_Cull", properties, false);
        alphaClipProp = FindProperty("_AlphaClip", properties, false);
        srcBlendProp = FindProperty("_SrcBlend", properties, false);
        dstBlendProp = FindProperty("_DstBlend", properties, false);
        srcBlendAlphaProp = FindProperty("_SrcBlendAlpha", properties, false);
        dstBlendAlphaProp = FindProperty("_DstBlendAlpha", properties, false);
        zWriteProp = FindProperty("_ZWrite", properties, false);
    }

    public void ShaderPropertiesGUI(Material material)
    {
        EditorGUIUtility.labelWidth = 0f;

        EditorGUI.BeginChangeCheck();

        // Surface Options
        DrawSurfaceOptions(material);
        EditorGUILayout.Space();

        // Surface Inputs
        DrawSurfaceInputs(material);
        EditorGUILayout.Space();

        // Wind Settings
        DrawWindSettings(material);
        EditorGUILayout.Space();

        // Flutter Settings
        DrawFlutterSettings(material);
        EditorGUILayout.Space();

        // Advanced Options
        DrawAdvancedOptions(material);

        if (EditorGUI.EndChangeCheck())
        {
            foreach (var obj in m_MaterialEditor.targets)
            {
                MaterialChanged((Material)obj);
            }
        }
    }

    private void DrawSurfaceOptions(Material material)
    {
        EditorGUILayout.LabelField("Surface Options", EditorStyles.boldLabel);

        if (surfaceProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = surfaceProp.hasMixedValue;
            var surfaceType = (SurfaceType)surfaceProp.floatValue;
            surfaceType = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", surfaceType);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Surface Type");
                surfaceProp.floatValue = (float)surfaceType;
            }
            EditorGUI.showMixedValue = false;
        }

        if (alphaClipProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = alphaClipProp.hasMixedValue;
            var alphaClip = EditorGUILayout.Toggle("Alpha Clipping", alphaClipProp.floatValue == 1);
            if (EditorGUI.EndChangeCheck())
            {
                alphaClipProp.floatValue = alphaClip ? 1 : 0;
                material.SetFloat("_AlphaClip", alphaClipProp.floatValue);
            }
            EditorGUI.showMixedValue = false;

            if (alphaClipProp.floatValue == 1 && cutoffProp != null)
            {
                m_MaterialEditor.ShaderProperty(cutoffProp, "Threshold", 1);
            }
        }

        if (cullProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = cullProp.hasMixedValue;
            var culling = (RenderFace)cullProp.floatValue;
            culling = (RenderFace)EditorGUILayout.EnumPopup("Render Face", culling);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Render Face");
                cullProp.floatValue = (float)culling;
                material.doubleSidedGI = (RenderFace)cullProp.floatValue != RenderFace.Front;
            }
            EditorGUI.showMixedValue = false;
        }
    }

    private void DrawSurfaceInputs(Material material)
    {
        EditorGUILayout.LabelField("Surface Inputs", EditorStyles.boldLabel);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMapProp, baseColorProp);

        if (specColorProp != null && specGlossMapProp != null)
        {
            m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Specular Map"), specGlossMapProp, specColorProp);
        }

        if (smoothnessProp != null)
        {
            EditorGUI.indentLevel += 2;
            m_MaterialEditor.ShaderProperty(smoothnessProp, "Smoothness");
            EditorGUI.indentLevel -= 2;
        }

        if (bumpMapProp != null)
        {
            m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), bumpMapProp,
                bumpMapProp.textureValue != null ? bumpScaleProp : null);
        }

        if (emissionMapProp != null && emissionColorProp != null)
        {
            m_MaterialEditor.TexturePropertyWithHDRColor(new GUIContent("Emission"), emissionMapProp, emissionColorProp, false);
        }
    }

    private void DrawWindSettings(Material material)
    {
        showWindSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showWindSettings, "Wind Settings");
        EditorPrefs.SetBool("TreeShaderGUI_WindSettings", showWindSettings);

        if (showWindSettings)
        {
            EditorGUI.indentLevel++;

            if (requiresWindProp != null)
            {
                EditorGUI.BeginChangeCheck();
                var requiresWind = EditorGUILayout.Toggle("Enable Wind", requiresWindProp.floatValue == 1);
                if (EditorGUI.EndChangeCheck())
                {
                    requiresWindProp.floatValue = requiresWind ? 1 : 0;
                }
            }

            EditorGUI.BeginDisabledGroup(requiresWindProp != null && requiresWindProp.floatValue == 0);

            if (windSpeedProp != null)
                m_MaterialEditor.ShaderProperty(windSpeedProp, "Speed");
            if (windStrengthProp != null)
                m_MaterialEditor.ShaderProperty(windStrengthProp, "Strength");
            if (windFrequencyProp != null)
                m_MaterialEditor.ShaderProperty(windFrequencyProp, "Frequency");
            if (windTurbulenceProp != null)
                m_MaterialEditor.ShaderProperty(windTurbulenceProp, "Turbulence");
            if (windDirectionProp != null)
                m_MaterialEditor.ShaderProperty(windDirectionProp, "Direction");

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawFlutterSettings(Material material)
    {
        showFlutterSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showFlutterSettings, "Flutter Settings");
        EditorPrefs.SetBool("TreeShaderGUI_FlutterSettings", showFlutterSettings);

        if (showFlutterSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUI.BeginDisabledGroup(requiresWindProp != null && requiresWindProp.floatValue == 0);

            if (flutterSpeedProp != null)
                m_MaterialEditor.ShaderProperty(flutterSpeedProp, "Speed");
            if (flutterStrengthProp != null)
                m_MaterialEditor.ShaderProperty(flutterStrengthProp, "Strength");
            if (flutterFrequencyProp != null)
                m_MaterialEditor.ShaderProperty(flutterFrequencyProp, "Frequency");

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawAdvancedOptions(Material material)
    {
        EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);

        if (receiveShadowsProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = receiveShadowsProp.hasMixedValue;
            var receiveShadows = EditorGUILayout.Toggle("Receive Shadows", receiveShadowsProp.floatValue == 1);
            if (EditorGUI.EndChangeCheck())
            {
                receiveShadowsProp.floatValue = receiveShadows ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;
        }

        if (specularHighlightsProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = specularHighlightsProp.hasMixedValue;
            var specHighlights = EditorGUILayout.Toggle("Specular Highlights", specularHighlightsProp.floatValue == 1);
            if (EditorGUI.EndChangeCheck())
            {
                specularHighlightsProp.floatValue = specHighlights ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;
        }

        m_MaterialEditor.EnableInstancingField();
        m_MaterialEditor.DoubleSidedGIField();
        m_MaterialEditor.RenderQueueField();
    }

    public static void SetMaterialKeywords(Material material)
    {
        // Alpha clip
        if (material.HasProperty("_AlphaClip"))
        {
            bool alphaClipEnabled = material.GetFloat("_AlphaClip") == 1;
            if (alphaClipEnabled)
                material.EnableKeyword("_ALPHATEST_ON");
            else
                material.DisableKeyword("_ALPHATEST_ON");
        }

        // Normal map
        if (material.HasProperty("_BumpMap"))
        {
            bool normalMapEnabled = material.GetTexture("_BumpMap") != null;
            if (normalMapEnabled)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
        }

        // Emission
        if (material.HasProperty("_EmissionMap") && material.HasProperty("_EmissionColor"))
        {
            var emissionEnabled = material.GetTexture("_EmissionMap") != null ||
                                 material.GetColor("_EmissionColor") != Color.black;
            if (emissionEnabled)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }

        // Specular setup
        if (material.HasProperty("_SpecColor"))
            material.EnableKeyword("_SPECULAR_COLOR");
    }

    public static void MaterialChanged(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        SetMaterialKeywords(material);
    }

    private enum SurfaceType
    {
        Opaque,
        Transparent
    }

    private enum RenderFace
    {
        Front = 2,
        Back = 1,
        Both = 0
    }
}
