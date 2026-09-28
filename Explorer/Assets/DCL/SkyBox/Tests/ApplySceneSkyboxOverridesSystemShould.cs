using Arch.Core;
using DCL.Quality;
using DCL.SkyBox.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DCL.SkyBox.Tests
{
    public class ApplySceneSkyboxOverridesSystemShould : UnitySystemTestBase<ApplySceneSkyboxOverridesSystem>
    {
        private const string GENESIS_NAME = "GenesisStandIn";
        private const string PANORAMIC_NAME = "PanoramicStandIn";

        // The prefab carries the serialized gradients and curves Initialize samples; an empty component would leave them null
        private const string PREFAB_PATH = "Assets/DCL/SkyBox/Prefab/SkyboxRenderController.prefab";

        private Entity skyboxEntity;
        private SkyboxToCubemapRendererFeature feature = null!;
        private IRendererFeaturesCache rendererFeaturesCache = null!;
        private SkyboxRenderController skyboxRenderController = null!;
        private Material genesisMaterial = null!;
        private Material panoramicMaterial = null!;
        private Material genesisCopy = null!;
        private Material panoramicCopy = null!;
        private Texture2D reflectionMap = null!;
        private Texture2D skyboxTexture = null!;

        private Material? previousSkybox;
        private AmbientMode previousAmbientMode;
        private bool previousFog;

        [SetUp]
        public void SetUp()
        {
            previousSkybox = RenderSettings.skybox;
            previousAmbientMode = RenderSettings.ambientMode;
            previousFog = RenderSettings.fog;

            genesisMaterial = new Material(Shader.Find("Unlit/Texture")) { name = GENESIS_NAME };
            panoramicMaterial = new Material(Shader.Find("Skybox/Panoramic")) { name = PANORAMIC_NAME };
            reflectionMap = new Texture2D(2, 2);
            skyboxTexture = new Texture2D(2, 2);

            feature = ScriptableObject.CreateInstance<SkyboxToCubemapRendererFeature>();
            rendererFeaturesCache = Substitute.For<IRendererFeaturesCache>();
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns(feature);

            skyboxRenderController = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkyboxRenderController>(PREFAB_PATH));
            skyboxRenderController.Initialize(genesisMaterial, panoramicMaterial, null!, null!, 0.5f, freezeTime: true);

            // In the Editor the controller works on copies of the materials; keep them to destroy them afterwards
            genesisCopy = RenderSettings.skybox;
            skyboxRenderController.SetSkyboxOverride(skyboxTexture);
            panoramicCopy = RenderSettings.skybox;
            skyboxRenderController.SetSkyboxOverride(null);

            skyboxEntity = world.Create(new SceneSkyboxOverrides());
            system = new ApplySceneSkyboxOverridesSystem(world, rendererFeaturesCache, skyboxRenderController, skyboxEntity);
        }

        protected override void OnTearDown()
        {
            RenderSettings.skybox = previousSkybox;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.fog = previousFog;

            Object.DestroyImmediate(skyboxRenderController.gameObject);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(genesisCopy);
            Object.DestroyImmediate(panoramicCopy);
            Object.DestroyImmediate(genesisMaterial);
            Object.DestroyImmediate(panoramicMaterial);
            Object.DestroyImmediate(reflectionMap);
            Object.DestroyImmediate(skyboxTexture);
        }

        [Test]
        public void PushReflectionMapOnChange()
        {
            // Arrange
            Overrides().ReflectionMap = reflectionMap;

            // Act
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(reflectionMap));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(reflectionMap));
        }

        [Test]
        public void ClearReflectionOnRemoval()
        {
            // Arrange
            Overrides().ReflectionMap = reflectionMap;
            system.Update(0);

            // Act
            Overrides().ReflectionMap = null;
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.Null);
            Assert.That(Overrides().AppliedReflectionSource, Is.Null);
        }

        [Test]
        public void NotTouchFeatureWhenUnchanged()
        {
            // Arrange
            Overrides().ReflectionMap = reflectionMap;
            system.Update(0);
            rendererFeaturesCache.ClearReceivedCalls();

            // Act
            system.Update(0);

            // Assert
            rendererFeaturesCache.DidNotReceive().GetRendererFeature<SkyboxToCubemapRendererFeature>();
        }

        [Test]
        public void DeferReflectionUntilFeatureIsAvailable()
        {
            // Arrange
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns((SkyboxToCubemapRendererFeature?)null);
            Overrides().ReflectionMap = reflectionMap;

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().AppliedReflectionSource, Is.Null);
            Assert.That(feature.ReflectionOverride, Is.Null);

            // Act
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns(feature);
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(reflectionMap));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(reflectionMap));
        }

        [Test]
        public void SwapSkyToPanoramicWithTexture()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.skybox.name, Is.EqualTo(PANORAMIC_NAME));
            Assert.That(RenderSettings.skybox.mainTexture, Is.SameAs(skyboxTexture));
            Assert.That(Overrides().AppliedSkyboxTexture, Is.SameAs(skyboxTexture));
        }

        [Test]
        public void RestoreGenesisSkyWhenSkyboxTextureIsCleared()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;
            system.Update(0);

            // Act
            Overrides().SkyboxTexture = null;
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.skybox.name, Is.EqualTo(GENESIS_NAME));
            Assert.That(Overrides().AppliedSkyboxTexture, Is.Null);
        }

        [Test]
        public void DeriveReflectionFromSkyboxTextureWhenReflectionMapIsNull()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(skyboxTexture));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(skyboxTexture));
        }

        [Test]
        public void PreferReflectionMapOverSkyboxTexture()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;
            Overrides().ReflectionMap = reflectionMap;

            // Act
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(reflectionMap));
            Assert.That(RenderSettings.skybox.mainTexture, Is.SameAs(skyboxTexture));
        }

        private ref SceneSkyboxOverrides Overrides() =>
            ref world.Get<SceneSkyboxOverrides>(skyboxEntity);
    }
}
