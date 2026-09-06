using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using DCL.Rendering.RenderGraphs.RenderFeatures.Ocean;
using DCL.Rendering.RenderGraphs.RenderFeatures.SkyboxToCubemap;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Tests
{
    /// <summary>
    ///     The shipped renderer asset is the only thing that wires this package's
    ///     features into the pipeline; these tests pin what it references so a
    ///     player build carries every shader the features resolve at runtime.
    /// </summary>
    [TestFixture]
    public class ForwardRendererHighAssetShould
    {
        private const string RENDERER_ASSET = "Assets/Rendering/ForwardRenderer - High.asset";
        private const string AVATAR_OUTLINE_SHADER = "Hidden/DCL/RenderFeatures/AvatarOutline";
        private const string CUBE_BLUR_SHADER = "DCL/CubeBlur";
        private const string OCEAN_CAUSTICS_SHADER = "Hidden/DCL/RenderFeatures/OceanCaustics";
        private const string OCEAN_SSR_SHADER = "Hidden/DCL/RenderFeatures/OceanSSR";
        private const string OCEAN_DISPLACEMENT_SHADER = "Hidden/DCL/RenderFeatures/OceanDisplacement";
        private const string QUALITY_PRESET_MEDIUM = "Assets/DCL/Settings/Settings/QualityPresetMedium.asset";
        private const string QUALITY_PRESET_HIGH = "Assets/DCL/Settings/Settings/QualityPresetHigh.asset";

        private UniversalRendererData rendererData;
        private Object[] subAssets;

        [OneTimeSetUp]
        public void SetUp()
        {
            rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RENDERER_ASSET);
            Assert.IsNotNull(rendererData);
            subAssets = AssetDatabase.LoadAllAssetsAtPath(RENDERER_ASSET);
        }

        [Test]
        public void ContainOnlyTheFeaturesItEnqueues()
        {
            // Arrange
            List<ScriptableRendererFeature> enqueued = rendererData.rendererFeatures;

            // Assert
            Assert.AreEqual(enqueued.Count + 1, subAssets.Length, "the asset carries sub-assets its feature list never enqueues");

            foreach (Object subAsset in subAssets)
            {
                Assert.IsTrue(subAsset != null, "a sub-asset has a missing script");

                if (subAsset == rendererData)
                    continue;

                Assert.IsInstanceOf<ScriptableRendererFeature>(subAsset, $"{subAsset.name} is not a renderer feature");
                Assert.IsTrue(enqueued.Contains((ScriptableRendererFeature)subAsset), $"{subAsset.name} is not in the renderer feature list");
            }
        }

        [Test]
        public void BindTheAvatarOutlineShader()
        {
            // Arrange
            RendererFeature_AvatarOutline feature = FindFeature<RendererFeature_AvatarOutline>();

            // Act
            Shader outline = SerializedShader(feature, "outlineShader");

            // Assert
            Assert.IsNotNull(outline, "outlineShader is unassigned, so player builds strip it");
            Assert.AreEqual(AVATAR_OUTLINE_SHADER, outline.name);
        }

        [Test]
        public void BindTheSkyboxCubeBlurShader()
        {
            // Arrange
            SkyboxToCubemapRendererFeature feature = FindFeature<SkyboxToCubemapRendererFeature>();

            // Act
            Shader cubeBlur = SerializedShader(feature, "settings.cubeBlurShader");
            Shader skybox = SerializedShader(feature, "settings.skyBoxShader");

            // Assert
            Assert.IsNotNull(skybox, "skyBoxShader is unassigned");
            Assert.IsNotNull(cubeBlur, "cubeBlurShader is unassigned, so player builds strip it");
            Assert.AreEqual(CUBE_BLUR_SHADER, cubeBlur.name);
        }

        [Test]
        public void BindTheOceanSubPassShaders()
        {
            // Arrange
            RendererFeature_Ocean feature = FindFeature<RendererFeature_Ocean>();

            // Act
            Shader caustics = SerializedShader(feature, "causticsShader");
            Shader ssr = SerializedShader(feature, "ssrShader");
            Shader displacement = SerializedShader(feature, "displacementShader");

            // Assert
            Assert.IsNotNull(caustics, "causticsShader is unassigned, so player builds strip it");
            Assert.IsNotNull(ssr, "ssrShader is unassigned, so player builds strip it");
            Assert.IsNotNull(displacement, "displacementShader is unassigned, so player builds strip it");
            Assert.AreEqual(OCEAN_CAUSTICS_SHADER, caustics.name);
            Assert.AreEqual(OCEAN_SSR_SHADER, ssr.name);
            Assert.AreEqual(OCEAN_DISPLACEMENT_SHADER, displacement.name);
        }

        [Test]
        public void EnableTheAvatarOutlineFromTheMediumAndHighPresets()
        {
            // Arrange
            var medium = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(QUALITY_PRESET_MEDIUM));
            var high = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(QUALITY_PRESET_HIGH));

            // Assert
            Assert.IsTrue(medium.FindProperty("avatarOutlineEnabled").boolValue, "medium preset ships the outline off");
            Assert.IsTrue(high.FindProperty("avatarOutlineEnabled").boolValue, "high preset ships the outline off");
        }

        private T FindFeature<T>() where T: ScriptableRendererFeature
        {
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is T match)
                    return match;
            }

            Assert.Fail($"{typeof(T).Name} is not in the renderer feature list");
            return null;
        }

        private static Shader SerializedShader(Object target, string propertyPath)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyPath);
            Assert.IsNotNull(property, $"{target.name} has no serialized {propertyPath}");
            return property.objectReferenceValue as Shader;
        }
    }
}
