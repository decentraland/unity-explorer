#if VEGETATION_STUDIO_PRO
using AwesomeTechnologies.VegetationSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace AwesomeTechnologies.Shaders
{
    public class StylizedGrassShaderController : IShaderController
    {
        public bool MatchShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;

            return (shaderName == "Universal Render Pipeline/Nature/Stylized Grass") ? true : false;
        }

        public bool MatchBillboardShader(Material[] materials)
        {
            return false;
        }

        public ShaderControllerSettings Settings { get; set; }
        public void CreateDefaultSettings(Material[] materials)
        {
            Settings = new ShaderControllerSettings
            {
                Heading = "Stylized Grass",
                Description = "Description text",
                LODFadePercentage = false,
                LODFadeCrossfade = true,
                UpdateWind = true,
                SampleWind = true,
                DynamicHUE = true,
                SupportsInstantIndirect = true
            };

            
            Settings.AddBooleanProperty("_FadingOn", "Distance/Angle fading", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_FadingOn") == 1f ? true : false);
            Settings.AddFloatProperty("_FadeNearStart", "Fade Near start", "", ShaderControllerSettings.GetVector4FromMaterials(materials,"_FadeNear").x, 0, 100);
            Settings.AddFloatProperty("_FadeNearEnd", "Fade Near end", "", ShaderControllerSettings.GetVector4FromMaterials(materials,"_FadeNear").y, 0, 100);
            Settings.AddFloatProperty("_FadeFarStart", "Fade Near start", "", ShaderControllerSettings.GetVector4FromMaterials(materials,"_FadeFar").x, 0, 500);
            Settings.AddFloatProperty("_FadeFarEnd", "Fade Near end", "", ShaderControllerSettings.GetVector4FromMaterials(materials,"_FadeFar").y, 0, 500);
            Settings.AddFloatProperty("_FadeAngleThreshold", "Angle threshold", "", ShaderControllerSettings.GetFloatFromMaterials(materials,"_FadeAngleThreshold"), 0, 90);

            Settings.AddLabelProperty(" ");

            Settings.AddLabelProperty("Color");
            Settings.AddColorProperty("_BaseColor", "Base color", "", ShaderControllerSettings.GetColorFromMaterials(materials, "_BaseColor"));
            Settings.AddColorProperty("_HueVariation", "Hue variation", "", ShaderControllerSettings.GetColorFromMaterials(materials, "_HueVariation"));
            Settings.AddColorProperty("_EmissionColor", "Emission", "", ShaderControllerSettings.GetColorFromMaterials(materials, "_EmissionColor"));
            Settings.AddFloatProperty("_Smoothness", "Smoothness", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_Smoothness"), 0, 1);

            Settings.AddFloatProperty("_ColorMapStrength", "Colormap strength", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_ColorMapStrength"), 0, 1);
            if (Shader.GetGlobalVector("_ColorMapParams").x == 0f) Settings.AddLabelProperty("No color map is currently active");
            Settings.AddFloatProperty("_ColorMapHeight", "Colormap height", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_ColorMapHeight"), 0, 1);
            
            Settings.AddLabelProperty(" ");

            //No support for vectors!
            //Settings.add("_HeightmapScaleInfluence", "Heightmap scale influence", "", ShaderControllerSettings.GetVector4FromMaterials(materials, "_HeightmapScaleInfluence"), 0, 1);
            Settings.AddLabelProperty("Shading");

            Settings.AddFloatProperty("_OcclusionStrength", "Ambient Occlusion", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_OcclusionStrength"), 0, 1);
            Settings.AddFloatProperty("_VertexDarkening", "Random darkening", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_VertexDarkening"), 0, 1);
            Settings.AddFloatProperty("_TranslucencyDirect", "Translucency (Direct)", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_TranslucencyDirect"), 0, 1);
            Settings.AddFloatProperty("_TranslucencyIndirect", "Translucency (Indirect)", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_TranslucencyIndirect"), 0, 1);
            Settings.AddFloatProperty("_TranslucencyFalloff", "Translucency Falloff", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_TranslucencyFalloff"), 1, 8);
            Settings.AddFloatProperty("_TranslucencyOffset", "Translucency Offset", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_TranslucencyOffset"), 0, 1);
            
            Settings.AddLabelProperty(" ");

            Settings.AddLabelProperty("Normals");
            Settings.AddFloatProperty("_BumpScale", "Normal map strength", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_BumpScale"), 0, 1);
            Settings.AddFloatProperty("_NormalFlattening", "Flatten normals (lighting)", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_NormalFlattening"), 0, 1);
            Settings.AddFloatProperty("_NormalSpherify", "Spherify normals", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_NormalSpherify"), 0, 1);
            Settings.AddFloatProperty("_NormalSpherifyMask", "Tip mask", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_NormalSpherifyMask"), 0, 1);
            Settings.AddFloatProperty("_NormalFlattenDepthNormals", "Flatten normals (geometry)", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_NormalFlattenDepthNormals"), 0, 1);

            Settings.AddLabelProperty(" ");

            Settings.AddLabelProperty("Bending");
            Settings.AddBooleanProperty("_BendMode", "Per-vertex", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_BendMode") == 0 ? true : false);
            Settings.AddFloatProperty("_BendPushStrength", "Pushing", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_BendPushStrength"), 0, 1);
            Settings.AddFloatProperty("_BendFlattenStrength", "Flattening", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_BendFlattenStrength"), 0, 1);
            Settings.AddFloatProperty("_PerspectiveCorrection", "Perspective Correction", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_PerspectiveCorrection"), 0, 1);

            Settings.AddLabelProperty(" ");

            Settings.AddLabelProperty("Wind");
            Settings.AddFloatProperty("_WindAmbientStrength", "Ambient Strength", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindAmbientStrength"), 0, 1);
            Settings.AddFloatProperty("_WindSpeed", "Speed", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindSpeed"), 0, 10);
            Settings.AddFloatProperty("_WindVertexRand", "Vertex randomization", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindVertexRand"), 0, 1);
            Settings.AddFloatProperty("_WindObjectRand", "Object randomization", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindObjectRand"), 0, 1);
            Settings.AddFloatProperty("_WindRandStrength", "Random strength", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindRandStrength"), 0, 1);
            Settings.AddFloatProperty("_WindSwinging", "Swinging", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindSwinging"), 0, 1);
            Settings.AddFloatProperty("_WindGustStrength", "Gust Strength", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindGustStrength"), 0, 1);
            Settings.AddFloatProperty("_WindGustSpeed", "Gust Speed", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindGustSpeed"), 0, 10);
            Settings.AddFloatProperty("_WindGustFreq", "Gust Frequency", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindGustFreq"), 0, 10);
            Settings.AddFloatProperty("_WindGustTint", "Gust Tint", "", ShaderControllerSettings.GetFloatFromMaterials(materials, "_WindGustTint"), 0, 1);
            


        }

        public void UpdateMaterial(Material material, EnvironmentSettings environmentSettings)
        {
            if (Settings == null) return;
            
            #if URP
            CoreUtils.SetKeyword(material, "_FADING", Settings.GetBooleanPropertyValue("_FadingOn"));
            #endif

            material.SetVector("_FadeNear", new Vector4(Settings.GetFloatPropertyValue("_FadeNearStart"), Settings.GetFloatPropertyValue("_FadeNearEnd")));
            material.SetVector("_FadeFar", new Vector4(Settings.GetFloatPropertyValue("_FadeFarStart"), Settings.GetFloatPropertyValue("_FadeFarEnd")));
            material.SetFloat("_FadeAngleThreshold", Settings.GetFloatPropertyValue("_FadeAngleThreshold"));

            material.SetColor("_BaseColor", Settings.GetColorPropertyValue("_BaseColor"));
            material.SetColor("_HueVariation", Settings.GetColorPropertyValue("_HueVariation"));
            material.SetFloat("_Smoothness", Settings.GetFloatPropertyValue("_Smoothness"));
            material.SetColor("_EmissionColor", Settings.GetColorPropertyValue("_EmissionColor"));

            material.SetFloat("_ColorMapStrength", Settings.GetFloatPropertyValue("_ColorMapStrength"));
            material.SetFloat("_ColorMapHeight", Settings.GetFloatPropertyValue("_ColorMapHeight"));
            material.SetFloat("_OcclusionStrength", Settings.GetFloatPropertyValue("_OcclusionStrength"));
            material.SetFloat("_VertexDarkening", Settings.GetFloatPropertyValue("_VertexDarkening"));
            material.SetFloat("_TranslucencyDirect", Settings.GetFloatPropertyValue("_TranslucencyDirect"));
            material.SetFloat("_TranslucencyIndirect", Settings.GetFloatPropertyValue("_TranslucencyIndirect"));
            material.SetFloat("_TranslucencyFalloff", Settings.GetFloatPropertyValue("_TranslucencyFalloff"));
            material.SetFloat("_TranslucencyOffset", Settings.GetFloatPropertyValue("_TranslucencyOffset"));
            
            material.SetFloat("_BumpScale", Settings.GetFloatPropertyValue("_BumpScale"));
            material.SetFloat("_NormalFlattening", Settings.GetFloatPropertyValue("_NormalFlattening"));
            material.SetFloat("_NormalSpherify", Settings.GetFloatPropertyValue("_NormalSpherify"));
            material.SetFloat("_NormalSpherifyMask", Settings.GetFloatPropertyValue("_NormalSpherifyMask"));
            material.SetFloat("_NormalFlattenDepthNormals", Settings.GetFloatPropertyValue("_NormalFlattenDepthNormals"));

            material.SetFloat("_BendMode", Settings.GetBooleanPropertyValue("_BendMode") ? 0f : 1f);
            material.SetFloat("_BendPushStrength", Settings.GetFloatPropertyValue("_BendPushStrength"));
            material.SetFloat("_BendFlattenStrength", Settings.GetFloatPropertyValue("_BendFlattenStrength"));
            material.SetFloat("_PerspectiveCorrection", Settings.GetFloatPropertyValue("_PerspectiveCorrection"));

            material.SetFloat("_WindAmbientStrength", Settings.GetFloatPropertyValue("_WindAmbientStrength"));
            material.SetFloat("_WindSpeed", Settings.GetFloatPropertyValue("_WindSpeed"));
            material.SetFloat("_WindVertexRand", Settings.GetFloatPropertyValue("_WindVertexRand"));
            material.SetFloat("_WindObjectRand", Settings.GetFloatPropertyValue("_WindObjectRand"));
            material.SetFloat("_WindRandStrength", Settings.GetFloatPropertyValue("_WindRandStrength"));
            material.SetFloat("_WindSwinging", Settings.GetFloatPropertyValue("_WindSwinging"));
            material.SetFloat("_WindGustStrength", Settings.GetFloatPropertyValue("_WindGustStrength"));
            material.SetFloat("_WindGustSpeed", Settings.GetFloatPropertyValue("_WindGustSpeed"));
            material.SetFloat("_WindGustFreq", Settings.GetFloatPropertyValue("_WindGustFreq"));
            material.SetFloat("_WindGustTint", Settings.GetFloatPropertyValue("_WindGustTint"));
        }

        public void UpdateWind(Material material, WindSettings windSettings)
        {
        }
    }
}
#endif