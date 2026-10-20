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
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

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
        private const string SUN_DIRECTION = "_DclSunDirection";
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
        private Texture2D skyboxTexture = null!;

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
        private SkyboxLookPreset? customSceneLook;

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
            skyboxTexture = new Texture2D(2, 2);

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

            if (customSceneLook != null)
                Object.DestroyImmediate(customSceneLook);

            Object.DestroyImmediate(skyboxRenderController.gameObject);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(genesisCopy);
            Object.DestroyImmediate(panoramicCopy);
            Object.DestroyImmediate(genesisMaterial);
            Object.DestroyImmediate(panoramicMaterial);
            Object.DestroyImmediate(skyboxTexture);
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

        // Next iteration: the explicit reflection map (PBSkybox.reflection_map) was removed from the protocol and
        // SceneSkyboxOverrides; reflections are always derived from the skybox texture. Restore with that field.
        // [Test]
        // public void PushReflectionMapOnChange()
        // {
        //     // Arrange
        //     Overrides().ReflectionMap = reflectionMap;
        //
        //     // Act
        //     system.Update(0);
        //
        //     // Assert
        //     Assert.That(feature.ReflectionOverride, Is.SameAs(reflectionMap));
        //     Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(reflectionMap));
        // }

        [Test]
        public void ClearDerivedReflectionWhenSkyboxTextureIsCleared()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;
            system.Update(0);

            // Act
            Overrides().SkyboxTexture = null;
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.Null);
            Assert.That(Overrides().AppliedReflectionSource, Is.Null);
        }

        [Test]
        public void NotTouchFeatureWhenUnchanged()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;
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
            Overrides().SkyboxTexture = skyboxTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().AppliedReflectionSource, Is.Null);
            Assert.That(feature.ReflectionOverride, Is.Null);

            // Act
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns(feature);
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(skyboxTexture));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(skyboxTexture));
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
        public void DeriveReflectionFromSkyboxTexture()
        {
            // Arrange
            Overrides().SkyboxTexture = skyboxTexture;

            // Act
            system.Update(0);

            // Assert
            Assert.That(feature.ReflectionOverride, Is.SameAs(skyboxTexture));
            Assert.That(Overrides().AppliedReflectionSource, Is.SameAs(skyboxTexture));
        }

        // Next iteration: the explicit reflection map (PBSkybox.reflection_map) was removed from the protocol and
        // SceneSkyboxOverrides; reflections are always derived from the skybox texture. Restore with that field.
        // [Test]
        // public void PreferReflectionMapOverSkyboxTexture()
        // {
        //     // Arrange
        //     Overrides().SkyboxTexture = skyboxTexture;
        //     Overrides().ReflectionMap = reflectionMap;
        //
        //     // Act
        //     system.Update(0);
        //
        //     // Assert
        //     Assert.That(feature.ReflectionOverride, Is.SameAs(reflectionMap));
        //     Assert.That(RenderSettings.skybox.mainTexture, Is.SameAs(skyboxTexture));
        // }

        [Test]
        public void ApplyConstantFogColorAtFrozenTime()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.red)) });

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
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.red)) });
            system.Update(0);

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            ColorGradientConverterShould.AssertColor(RenderSettings.fogColor, legacyFog);
            Assert.That(Overrides().AppliedEnvironment, Is.Null);
        }

        [Test]
        public void ApplyFogDensity()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Density = 0.02f });

            // Act
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.02f).Within(1e-5f));
            Assert.That(Overrides().AppliedEnvironment, Is.SameAs(Overrides().Environment));
        }

        [Test]
        public void RestoreLegacyFogDensityWhenEnvironmentCleared()
        {
            // Arrange
            Control();
            system.Update(0);
            float legacyFogDensity = RenderSettings.fogDensity;
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Density = legacyFogDensity + 0.5f });
            system.Update(0);

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(legacyFogDensity).Within(1e-5f));
            Assert.That(Overrides().AppliedEnvironment, Is.Null);
        }

        [Test]
        public void EvaluateGradientAtCurrentTimeOfDay()
        {
            // Arrange
            Control();
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.black), (1f, Color.white)) });

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
            Overrides().Environment = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.red)) });

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
            SceneEnvironmentProfile profile = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.red)) });
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
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Color = Gradient((0f, Color.red)) });

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
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Visible = false });

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
            SceneEnvironmentProfile profile = Profile(fog: new PBEnvironmentFog { Color = Gradient((0f, Color.red)) });

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
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Color = Gradient((0f, Color.red)), Visible = false });
            sunController!.SetSceneLook(true, profile);

            // Act
            sunController.SetSceneLook(false, null);

            // Assert
            Assert.That(sunController.Preset, Is.SameAs(shippedPreset));
            ColorGradientConverterShould.AssertColor(sunLight.color, shippedLightColor);
            Assert.That(lensFlare.intensity, Is.EqualTo(shippedFlareIntensity));
            Assert.That(sunGenesisCopy!.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(shippedPreset.SecondSunSizeFactor));
        }

        [Test]
        public void MultiplySunIntensityAndRestoreItWhenCleared()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            sunController!.SetSceneLook(true, null);
            float timeOfDayIntensity = sunLight.intensity;
            Assert.That(timeOfDayIntensity, Is.GreaterThan(0f), "the legacy light is lit at noon");
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Intensity = 0.5f });

            // Act
            sunController.SetSceneLook(true, profile);
            sunController.SetSceneLook(true, profile);
            sunController.UpdateSkybox(TIME_OF_DAY + 0.1f);
            sunController.UpdateSkybox(TIME_OF_DAY);

            // Assert
            Assert.That(sunLight.intensity, Is.EqualTo(timeOfDayIntensity * 0.5f).Within(1e-4f));

            // Act
            sunController.SetSceneLook(true, null);

            // Assert
            Assert.That(sunLight.intensity, Is.EqualTo(timeOfDayIntensity).Within(1e-4f));
        }

        [Test]
        public void NotCompoundSunIntensityWhenTheLightIntensityIsItsOwnFallback()
        {
            // Arrange: no clip and no intensity curve, so every evaluation falls back to the intensity already on the light
            Light sunLight = CreateSunController(lensFlareEnabled: false, withoutLightClip: true, sceneLookWithoutIntensityCurve: true);
            sunController!.SetSceneLook(true, null);
            float fallbackIntensity = sunLight.intensity;
            Assert.That(fallbackIntensity, Is.GreaterThan(0f));
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Intensity = 0.5f });

            // Act
            sunController.SetSceneLook(true, profile);
            sunController.SetSceneLook(true, profile);
            sunController.UpdateSkybox(TIME_OF_DAY + 0.1f);
            sunController.UpdateSkybox(TIME_OF_DAY);

            // Assert
            Assert.That(sunLight.intensity, Is.EqualTo(fallbackIntensity * 0.5f).Within(1e-5f));
        }

        [Test]
        public void ZeroTheLightWithAZeroSunIntensity()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);

            // Act
            sunController!.SetSceneLook(true, Profile(sun: new PBSun { Intensity = 0f }));

            // Assert
            Assert.That(sunLight.intensity, Is.EqualTo(0f));
        }

        [Test]
        public void KeepTheSunAtAFixedDirectionAtEveryTimeOfDay()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            Vector3 travelDirection = new Vector3(1f, -1f, 0.5f).normalized;
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Direction = new Decentraland.Common.Vector3 { X = 1f, Y = -1f, Z = 0.5f } });

            // Act
            sunController!.SetSceneLook(true, profile);

            // Assert
            AssertDirection(sunLight.transform.forward, travelDirection);
            AssertDirection(Shader.GetGlobalVector(SUN_DIRECTION), -travelDirection);

            // Act
            sunController.UpdateSkybox(0.9f);

            // Assert
            AssertDirection(sunLight.transform.forward, travelDirection);
            AssertDirection(Shader.GetGlobalVector(SUN_DIRECTION), -travelDirection);
        }

        [TestCase(0f, -1f, 0f)]
        [TestCase(0f, 1f, 0f)]
        public void PointTheLightAlongAPoleDirection(float x, float y, float z)
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Direction = new Decentraland.Common.Vector3 { X = x, Y = y, Z = z } });

            // Act
            sunController!.SetSceneLook(true, profile);

            // Assert
            AssertDirection(sunLight.transform.forward, new Vector3(x, y, z));
        }

        [Test]
        public void FollowTheTimeOfDayAgainWhenTheSunOverridesAreCleared()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            Color shippedLightColor = sunLight.color;
            float shippedIntensity = sunLight.intensity;
            Quaternion shippedRotation = sunLight.transform.rotation;
            sunController!.SetSceneLook(true, null);
            Vector3 timeOfDayForward = sunLight.transform.forward;
            Vector3 travelDirection = Vector3.right;
            Assume.That(Vector3.Angle(timeOfDayForward, travelDirection), Is.GreaterThan(1f));
            sunController.SetSceneLook(true, Profile(sun: new PBSun { Intensity = 2f, Direction = new Decentraland.Common.Vector3 { X = 1f } }));
            AssertDirection(sunLight.transform.forward, travelDirection);

            // Act
            sunController.SetSceneLook(true, null);

            // Assert
            AssertDirection(sunLight.transform.forward, timeOfDayForward);

            // Act
            sunController.SetSceneLook(true, Profile(sun: new PBSun { Intensity = 2f, Direction = new Decentraland.Common.Vector3 { X = 1f } }));
            sunController.SetSceneLook(false, null);

            // Assert
            Assert.That(sunController.Preset, Is.SameAs(shippedPreset));
            Assert.That(sunLight.intensity, Is.EqualTo(shippedIntensity).Within(1e-4f));
            Assert.That(Quaternion.Angle(sunLight.transform.rotation, shippedRotation), Is.LessThan(0.01f));
            ColorGradientConverterShould.AssertColor(sunLight.color, shippedLightColor);
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Angle(actual, expected), Is.LessThan(0.01f), $"expected {expected}, got {actual}");

        /// <summary>
        ///     Builds a second controller around a real directional light so the sun, moon and lens flare paths run;
        ///     the shared controller of the fixture has no light. Optionally drops the light clip and the scene look's
        ///     intensity curve, so the light's own intensity is the fallback of every evaluation.
        /// </summary>
        private Light CreateSunController(bool lensFlareEnabled, bool withoutLightClip = false, bool sceneLookWithoutIntensityCurve = false)
        {
            sunLightObject = new GameObject("SunLight");
            Light sunLight = sunLightObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;

            sunController = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkyboxRenderController>(PREFAB_PATH));
            var serializedController = new SerializedObject(sunController);

            if (withoutLightClip)
                serializedController.FindProperty("lightAnimation").objectReferenceValue = null;

            if (sceneLookWithoutIntensityCurve)
            {
                customSceneLook = Object.Instantiate(legacyPreset);
                var serializedLook = new SerializedObject(customSceneLook);
                serializedLook.FindProperty("lightIntensity").animationCurveValue = new AnimationCurve();
                serializedLook.ApplyModifiedPropertiesWithoutUndo();
                serializedController.FindProperty("sceneLookPreset").objectReferenceValue = customSceneLook;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
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

        private static SceneEnvironmentProfile Profile(PBSkybox? skybox = null, PBSun? sun = null, PBEnvironmentFog? fog = null)
        {
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(skybox, sun, fog);
            Assert.That(profile, Is.Not.Null);
            return profile!;
        }

        private ref SceneSkyboxOverrides Overrides() =>
            ref world.Get<SceneSkyboxOverrides>(skyboxEntity);
    }
}
