using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Quality;
using DCL.SkyBox.Components;
using Decentraland.Common;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Entity = Arch.Core.Entity;
using Object = UnityEngine.Object;
using Texture = UnityEngine.Texture;

namespace DCL.SkyBox.Tests
{
    public class ApplySceneSkyboxOverridesSystemShould : UnitySystemTestBase<ApplySceneSkyboxOverridesSystem>
    {
        private const string GENESIS_NAME = "GenesisStandIn";
        private const string PANORAMIC_NAME = "PanoramicStandIn";

        // The prefab carries the shipped look and the scene look (legacy) presets Initialize and SetSceneLook read
        private const string PREFAB_PATH = "Assets/DCL/SkyBox/Prefab/SkyboxRenderController.prefab";

        // The controller writes every preset property to the material, which logs an error on a shader without them
        private const string GENESIS_MATERIAL_PATH = "Assets/DCL/SkyBox/Materials/GenesisSkybox.mat";

        private const string CLOUD_OPACITY = "_Cloud_Opacity";
        private const string CLOUDS_ROTATION_SPEED = "_CloudsRotationSpeed";
        private const string STARS_BRIGHTNESS = "_Stars_Brightness";
        private const string SUN_OPACITY = "_SunOpacity";
        private const string SUN_RADIANCE = "_Sun_Radiance";
        private const string SECOND_SUN_SIZE_FACTOR = "_Second_Sun_Size_Factor";
        private const string ZENIT_COLOR = "_ZenitColor";
        private const string HORIZON_COLOR = "_HorizonColor";
        private const string RIM_COLOR = "_RimColor";
        private const string SUN_COLOR = "_SunColor";
        private const string CLOUDS_COLOR = "_CloudsColor";
        private const string CLOUDS_CUBEMAP = "_Clouds_Cubemap";
        private const float TIME_OF_DAY = 0.5f;

        private static readonly SceneShortInfo OWNER = new (new Vector2Int(2, 2), "reflection-map");

        private Entity skyboxEntity;
        private SkyboxToCubemapRendererFeature feature = null!;
        private IRendererFeaturesCache rendererFeaturesCache = null!;
        private SkyboxRenderController skyboxRenderController = null!;
        private SkyboxLookPreset shippedPreset = null!;
        private SkyboxLookPreset legacyPreset = null!;
        private Material genesisMaterial = null!;
        private Material panoramicMaterial = null!;
        private Material genesisCopy = null!;
        private Material panoramicCopy = null!;
        private Texture2D reflectionMap = null!;
        private Texture2D skyboxTexture = null!;
        private Texture2D cloudsTexture = null!;
        private RenderTexture videoTexture = null!;

        private Material? previousSkybox;
        private Light? previousSun;
        private AmbientMode previousAmbientMode;
        private bool previousFog;
        private Color previousFogColor;
        private float previousFogDensity;
        private float previousReflectionIntensity;
        private Color previousAmbientSky;
        private Color previousAmbientEquator;
        private Color previousAmbientGround;

        private GameObject? sunLightObject;
        private SkyboxRenderController? sunController;
        private Material? sunGenesisCopy;
        private Material? sunPanoramicCopy;

        [SetUp]
        public void SetUp()
        {
            previousSkybox = RenderSettings.skybox;
            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogDensity = RenderSettings.fogDensity;
            previousReflectionIntensity = RenderSettings.reflectionIntensity;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;

            genesisMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>(GENESIS_MATERIAL_PATH)) { name = GENESIS_NAME };
            panoramicMaterial = new Material(Shader.Find("Skybox/Panoramic")) { name = PANORAMIC_NAME };
            reflectionMap = new Texture2D(2, 2);
            skyboxTexture = new Texture2D(2, 2);
            cloudsTexture = new Texture2D(2, 2);
            videoTexture = new RenderTexture(2, 2, 0);

            feature = ScriptableObject.CreateInstance<SkyboxToCubemapRendererFeature>();
            rendererFeaturesCache = Substitute.For<IRendererFeaturesCache>();
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns(feature);

            skyboxRenderController = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkyboxRenderController>(PREFAB_PATH));
            skyboxRenderController.Initialize(genesisMaterial, panoramicMaterial, null!, null!, TIME_OF_DAY);
            shippedPreset = skyboxRenderController.Preset;
            legacyPreset = skyboxRenderController.SceneLookPreset;

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
            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogDensity = previousFogDensity;
            RenderSettings.reflectionIntensity = previousReflectionIntensity;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;

            if (sunController != null)
                Object.DestroyImmediate(sunController.gameObject);

            if (sunLightObject != null)
                Object.DestroyImmediate(sunLightObject);

            if (sunGenesisCopy != null)
                Object.DestroyImmediate(sunGenesisCopy);

            if (sunPanoramicCopy != null)
                Object.DestroyImmediate(sunPanoramicCopy);

            Object.DestroyImmediate(skyboxRenderController.gameObject);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(genesisCopy);
            Object.DestroyImmediate(panoramicCopy);
            Object.DestroyImmediate(genesisMaterial);
            Object.DestroyImmediate(panoramicMaterial);
            Object.DestroyImmediate(reflectionMap);
            Object.DestroyImmediate(skyboxTexture);
            Object.DestroyImmediate(cloudsTexture);
            Object.DestroyImmediate(videoTexture);
        }

        [Test]
        public void KeepShippedLookWhileNoSceneOwnsTheSkybox()
        {
            // Act
            system.Update(0);

            // Assert
            Assert.That(skyboxRenderController.Preset, Is.SameAs(shippedPreset));
            Assert.That(Overrides().AppliedSceneControlled, Is.False);
        }

        [Test]
        public void SwitchToACopyOfTheLegacyLookWhenASceneOwnsTheSkybox()
        {
            // Arrange
            Control();

            // Act
            system.Update(0);

            // Assert
            SkyboxLookPreset sceneLook = skyboxRenderController.Preset;
            Assert.That(sceneLook, Is.Not.SameAs(shippedPreset));
            Assert.That(sceneLook, Is.Not.SameAs(legacyPreset), "the asset itself must never be written to");
            Assert.That(sceneLook.name, Does.StartWith(legacyPreset.name));
            Assert.That(sceneLook.UseSkyLut, Is.EqualTo(legacyPreset.UseSkyLut));
            Assert.That(genesisCopy.GetTexture(CLOUDS_CUBEMAP), Is.SameAs(legacyPreset.CloudsCubemap));
            Assert.That(Overrides().AppliedSceneControlled, Is.True);
        }

        [Test]
        public void RestoreTheShippedLookWhenTheSceneReleasesTheSkybox()
        {
            // Arrange
            Control();
            system.Update(0);

            // Act
            Release();
            system.Update(0);

            // Assert
            Assert.That(skyboxRenderController.Preset, Is.SameAs(shippedPreset));
            Assert.That(Overrides().AppliedSceneControlled, Is.False);
        }

        [Test]
        public void ReuseTheSameSceneCopyAcrossOwnershipChanges()
        {
            // Arrange
            Control();
            system.Update(0);
            SkyboxLookPreset firstSceneLook = skyboxRenderController.Preset;
            Release();
            system.Update(0);

            // Act
            Control();
            system.Update(0);

            // Assert
            Assert.That(skyboxRenderController.Preset, Is.SameAs(firstSceneLook));
        }

        [Test]
        public void ReturnToTheLookPickedFromTheDebugPanelNotTheShippedOne()
        {
            // Arrange: the debug dropdown applies presets straight to the controller
            skyboxRenderController.ApplyPreset(legacyPreset);
            Control();
            system.Update(0);
            Assert.That(skyboxRenderController.Preset, Is.Not.SameAs(legacyPreset));

            // Act
            Release();
            system.Update(0);

            // Assert
            Assert.That(skyboxRenderController.Preset, Is.SameAs(legacyPreset));
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
        public void KeepWritingTimeOfDayToGenesisWhilePanoramicIsShown()
        {
            // Arrange
            Control();
            Overrides().SkyboxTexture = skyboxTexture;
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.skybox.name, Is.EqualTo(PANORAMIC_NAME));
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.green);
            Assert.That(panoramicCopy.HasProperty(HORIZON_COLOR), Is.False, "the panoramic material has no Genesis properties and must not receive them");
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

        [Test]
        public void PushVideoRenderTextureToSkyAndReflections()
        {
            // Arrange
            Overrides().SkyboxTexture = videoTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.skybox.name, Is.EqualTo(PANORAMIC_NAME));
            Assert.That(RenderSettings.skybox.mainTexture, Is.SameAs(videoTexture));
            Assert.That(feature.ReflectionOverride, Is.SameAs(videoTexture));
            Assert.That(Overrides().AppliedSkyboxTexture, Is.SameAs(videoTexture));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(videoTexture));
        }

        [Test]
        public void TrackAppliedCloudsTextureAndRestoreLegacyCubemapOnClear()
        {
            // Arrange
            Texture? legacyCubemap = legacyPreset.CloudsCubemap;
            Assert.That(legacyCubemap, Is.Not.Null);
            Control();
            Overrides().CloudsTexture = cloudsTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().AppliedCloudsTexture, Is.SameAs(cloudsTexture));

            // Act
            Overrides().CloudsTexture = null;
            system.Update(0);

            // Assert
            Assert.That(Overrides().AppliedCloudsTexture, Is.Null);
            Assert.That(genesisCopy.GetTexture(CLOUDS_CUBEMAP), Is.SameAs(legacyCubemap));
            Assert.That(skyboxRenderController.Preset.CloudsCubemap, Is.SameAs(legacyCubemap));
        }

        [Test]
        public void BindProjectedCubemapToGenesisMaterialAndSceneLook()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("The equirect-to-cube projection needs a graphics device");

            // Arrange
            Control();
            Overrides().CloudsTexture = cloudsTexture;

            // Act
            system.Update(0);

            // Assert
            Texture bound = genesisCopy.GetTexture(CLOUDS_CUBEMAP);
            Assert.That(bound, Is.InstanceOf<RenderTexture>());
            Assert.That(((RenderTexture)bound).dimension, Is.EqualTo(TextureDimension.Cube));
            Assert.That(bound.width, Is.EqualTo(256));
            Assert.That(skyboxRenderController.Preset.CloudsCubemap, Is.SameAs(bound), "a look refresh must keep the projected clouds");
        }

        [Test]
        public void KeepProjectedCloudsThroughAnEnvironmentChange()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("The equirect-to-cube projection needs a graphics device");

            // Arrange
            Control();
            Overrides().CloudsTexture = cloudsTexture;
            system.Update(0);
            Texture projected = genesisCopy.GetTexture(CLOUDS_CUBEMAP);

            // Act: a new profile rewrites the scene look
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetTexture(CLOUDS_CUBEMAP), Is.SameAs(projected));
        }

        [Test]
        public void NotReprojectCloudsWhenReferenceUnchanged()
        {
            // Arrange
            Control();
            Overrides().CloudsTexture = cloudsTexture;
            system.Update(0);
            Texture applied = genesisCopy.GetTexture(CLOUDS_CUBEMAP);

            // Act
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetTexture(CLOUDS_CUBEMAP), Is.SameAs(applied));
            Assert.That(Overrides().AppliedCloudsTexture, Is.SameAs(cloudsTexture));
        }

        [Test]
        public void ApplyConstantFogColorAtFrozenTime()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.fogColor, Color.red);
            Assert.That(Overrides().AppliedEnvironment, Is.SameAs(Overrides().Environment));
        }

        [Test]
        public void RestoreLegacyFogColorWhenEnvironmentCleared()
        {
            // Arrange
            Control();
            system.Update(0);
            Color legacyFog = RenderSettings.fogColor;
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });
            system.Update(0);

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.fogColor, legacyFog);
            Assert.That(Overrides().AppliedEnvironment, Is.Null);
        }

        [Test]
        public void EvaluateGradientAtCurrentTimeOfDay()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.black), (1f, Color.white)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.fogColor, new Color(TIME_OF_DAY, TIME_OF_DAY, TIME_OF_DAY, 1f));
        }

        [Test]
        public void DeriveAmbientEquatorFromHorizonOnly()
        {
            // Arrange
            Control();
            system.Update(0);
            Color legacySky = RenderSettings.ambientSkyColor;
            Color legacyGround = RenderSettings.ambientGroundColor;
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.ambientEquatorColor, Color.green);
            ColorGradientConverterShould.AssertColor(RenderSettings.ambientSkyColor, legacySky);
            ColorGradientConverterShould.AssertColor(RenderSettings.ambientGroundColor, legacyGround);
        }

        [Test]
        public void WriteSkyColorsToGenesisMaterial()
        {
            // Arrange
            Control();
            system.Update(0);
            Color legacyZenith = genesisCopy.GetColor(ZENIT_COLOR);
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.green);
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(ZENIT_COLOR), legacyZenith);
        }

        [Test]
        public void DeriveRimFromHorizonWhenRimUnset()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), Color.red);
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.red);
        }

        [Test]
        public void PreferExplicitRimOverHorizon()
        {
            // Arrange
            Control();

            Overrides().Environment = Profile(new PBSkybox
            {
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Horizon = Gradient((0f, Color.red)),
                    Rim = Gradient((0f, Color.blue)),
                },
            });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), Color.blue);
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.red);
        }

        [Test]
        public void TintCloudsColor()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Color = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(CLOUDS_COLOR), Color.green);
        }

        [Test]
        public void KeepLegacyRimAndCloudsColorWhenOnlyFogIsSet()
        {
            // Arrange
            Control();
            system.Update(0);
            Color legacyRim = genesisCopy.GetColor(RIM_COLOR);
            Color legacyClouds = genesisCopy.GetColor(CLOUDS_COLOR);
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), legacyRim);
            ColorGradientConverterShould.AssertColor(genesisCopy.GetColor(CLOUDS_COLOR), legacyClouds);
        }

        [Test]
        public void ApplyAndRestoreCloudOpacityAndStarsBrightness()
        {
            // Arrange
            Control();

            Overrides().Environment = Profile(new PBSkybox
            {
                Clouds = new PBSkybox.Types.Clouds { Opacity = 0.25f },
                Stars = new PBSkybox.Types.Stars { Brightness = 2f },
            });

            // Act
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUD_OPACITY), Is.EqualTo(0.25f));
            Assert.That(genesisCopy.GetFloat(STARS_BRIGHTNESS), Is.EqualTo(2f));
            Assert.That(genesisCopy.GetFloat(CLOUDS_ROTATION_SPEED), Is.EqualTo(legacyPreset.CloudsRotationSpeed));

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUD_OPACITY), Is.EqualTo(legacyPreset.CloudOpacity));
            Assert.That(genesisCopy.GetFloat(STARS_BRIGHTNESS), Is.EqualTo(legacyPreset.StarsBrightness));
        }

        [Test]
        public void ApplyCloudsSpeed()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Speed = 0.5f } });

            // Act
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUDS_ROTATION_SPEED), Is.EqualTo(0.5f));
        }

        [Test]
        public void KeepCloudsSpeedZeroWhenTimeDisabled()
        {
            // Arrange
            skyboxRenderController.DisableSkyboxTime();
            Control();
            Overrides().Environment = Profile(new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Speed = 0.5f } });

            // Act
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUDS_ROTATION_SPEED), Is.EqualTo(0f));

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUDS_ROTATION_SPEED), Is.EqualTo(0f));
        }

        [Test]
        public void NotReapplyWhenReferenceUnchanged()
        {
            // Arrange
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });
            Control();
            Overrides().Environment = profile;
            Overrides().AppliedEnvironment = profile;
            Overrides().AppliedSceneControlled = true;
            RenderSettings.fogColor = Color.blue;

            // Act
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.fogColor, Color.blue);
        }

        [Test]
        public void TintDirectionalLightFromSunColor()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            sunController!.SetSceneLook(true, null);
            Color legacyLightColor = sunLight.color;
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Color = Gradient((0f, Color.red)) } });

            // Act
            sunController.SetSceneLook(true, profile);

            // Assert
            ColorGradientConverterShould.AssertColor(sunLight.color, Color.red);
            ColorGradientConverterShould.AssertColor(sunGenesisCopy!.GetColor(SUN_COLOR), Color.red);

            // Act
            sunController.SetSceneLook(true, null);

            // Assert
            ColorGradientConverterShould.AssertColor(sunLight.color, legacyLightColor);
        }

        [Test]
        public void HideSunMoonAndFlareWhenSunNotVisible()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: true);
            var lensFlare = sunLight.GetComponent<LensFlareComponentSRP>();
            sunController!.SetSceneLook(true, null);
            float legacySunOpacity = sunGenesisCopy!.GetFloat(SUN_OPACITY);
            Assert.That(legacySunOpacity, Is.GreaterThan(0f), "the legacy sun is visible at noon");
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Visible = false } });

            // Act
            sunController.SetSceneLook(true, profile);

            // Assert
            Assert.That(sunGenesisCopy.GetFloat(SUN_OPACITY), Is.EqualTo(0f));
            Assert.That(sunGenesisCopy.GetFloat(SUN_RADIANCE), Is.EqualTo(0f));
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(0f));
            Assert.That(lensFlare.intensity, Is.EqualTo(0f));

            // Act
            sunController.SetSceneLook(true, null);

            // Assert
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(legacyPreset.SecondSunSizeFactor));
            Assert.That(sunGenesisCopy.GetFloat(SUN_OPACITY), Is.EqualTo(legacySunOpacity));
        }

        [Test]
        public void KeepSunVisibleByDefault()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: true);
            var lensFlare = sunLight.GetComponent<LensFlareComponentSRP>();
            sunController!.SetSceneLook(true, null);
            float legacySunOpacity = sunGenesisCopy!.GetFloat(SUN_OPACITY);
            float legacyFlareIntensity = lensFlare.intensity;
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            sunController.SetSceneLook(true, profile);

            // Assert
            Assert.That(profile.SunVisible, Is.Null);
            Assert.That(sunGenesisCopy.GetFloat(SUN_OPACITY), Is.EqualTo(legacySunOpacity));
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(legacyPreset.SecondSunSizeFactor));
            Assert.That(lensFlare.intensity, Is.EqualTo(legacyFlareIntensity));
        }

        [Test]
        public void RestoreShippedSunWhenSceneReleasesTheSkybox()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: true);
            var lensFlare = sunLight.GetComponent<LensFlareComponentSRP>();
            Color shippedLightColor = sunLight.color;
            float shippedFlareIntensity = lensFlare.intensity;
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Color = Gradient((0f, Color.red)), Visible = false } });
            sunController!.SetSceneLook(true, profile);

            // Act
            sunController.SetSceneLook(false, null);

            // Assert
            Assert.That(sunController.Preset, Is.SameAs(shippedPreset));
            ColorGradientConverterShould.AssertColor(sunLight.color, shippedLightColor);
            Assert.That(lensFlare.intensity, Is.EqualTo(shippedFlareIntensity));
            Assert.That(sunGenesisCopy!.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(shippedPreset.SecondSunSizeFactor));
        }

        /// <summary>
        ///     Builds a second controller around a real directional light so the sun, moon and lens flare paths run;
        ///     the shared controller of the fixture has no light.
        /// </summary>
        private Light CreateSunController(bool lensFlareEnabled)
        {
            sunLightObject = new GameObject("SunLight");
            Light sunLight = sunLightObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            sunController = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkyboxRenderController>(PREFAB_PATH));
            sunController.Initialize(genesisMaterial, panoramicMaterial, sunLight, null!, TIME_OF_DAY, lensFlareEnabled);
            sunGenesisCopy = RenderSettings.skybox;
            sunController.SetSkyboxOverride(skyboxTexture);
            sunPanoramicCopy = RenderSettings.skybox;
            sunController.SetSkyboxOverride(null);

            return sunLight;
        }

        private void Control() =>
            Overrides().Owner = OWNER;

        private void Release() =>
            Overrides().Owner = null;

        private static ColorGradient Gradient(params (float time, Color color)[] keys) =>
            ColorGradientConverterShould.Gradient(keys);

        private static SceneEnvironmentProfile Profile(PBSkybox pbSkybox)
        {
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);
            Assert.That(profile, Is.Not.Null);
            return profile!;
        }

        private ref SceneSkyboxOverrides Overrides() =>
            ref world.Get<SceneSkyboxOverrides>(skyboxEntity);
    }
}
