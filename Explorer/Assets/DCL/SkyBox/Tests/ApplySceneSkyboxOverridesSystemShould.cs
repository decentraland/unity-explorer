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

namespace DCL.SkyBox.Tests
{
    public class ApplySceneSkyboxOverridesSystemShould : UnitySystemTestBase<ApplySceneSkyboxOverridesSystem>
    {
        private const string GENESIS_NAME = "GenesisStandIn";
        private const string PANORAMIC_NAME = "PanoramicStandIn";

        // The prefab carries the serialized gradients and curves Initialize samples; an empty component would leave them null
        private const string PREFAB_PATH = "Assets/DCL/SkyBox/Prefab/SkyboxRenderController.prefab";

        // Initialize reads the cloud and star floats back from the material, which logs an error on a shader without them
        private const string GENESIS_MATERIAL_PATH = "Assets/DCL/SkyBox/Materials/GenesisSkybox.mat";

        private const string CLOUD_OPACITY = "_Cloud_Opacity";
        private const string CLOUDS_ROTATION_SPEED = "_CloudsRotationSpeed";
        private const string STARS_BRIGHTNESS = "_Stars_Brightness";
        private const string SUN_OPACITY = "_SunOpacity";
        private const string SUN_RADIANCE = "_Sun_Radiance";
        private const string SECOND_SUN_SIZE_FACTOR = "_Second_Sun_Size_Factor";
        private const string HORIZON_COLOR = "_HorizonColor";
        private const string RIM_COLOR = "_RimColor";
        private const string CLOUDS_COLOR = "_CloudsColor";
        private const float DEFAULT_CLOUD_OPACITY = 1f;
        private const float DEFAULT_CLOUDS_ROTATION_SPEED = 0.01f;
        private const float DEFAULT_STARS_BRIGHTNESS = 4.62f;
        private const float DEFAULT_SECOND_SUN_SIZE_FACTOR = 0.1f;
        private const float TIME_OF_DAY = 0.5f;

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
        private Light? previousSun;
        private AmbientMode previousAmbientMode;
        private bool previousFog;
        private Color previousFogColor;
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
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;

            genesisMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>(GENESIS_MATERIAL_PATH)) { name = GENESIS_NAME };

            // Genesis defaults the controller captures on Initialize, pinned so the asset can change without affecting the assertions
            genesisMaterial.SetFloat(CLOUD_OPACITY, DEFAULT_CLOUD_OPACITY);
            genesisMaterial.SetFloat(CLOUDS_ROTATION_SPEED, DEFAULT_CLOUDS_ROTATION_SPEED);
            genesisMaterial.SetFloat(STARS_BRIGHTNESS, DEFAULT_STARS_BRIGHTNESS);
            genesisMaterial.SetFloat(SECOND_SUN_SIZE_FACTOR, DEFAULT_SECOND_SUN_SIZE_FACTOR);
            panoramicMaterial = new Material(Shader.Find("Skybox/Panoramic")) { name = PANORAMIC_NAME };
            reflectionMap = new Texture2D(2, 2);
            skyboxTexture = new Texture2D(2, 2);

            feature = ScriptableObject.CreateInstance<SkyboxToCubemapRendererFeature>();
            rendererFeaturesCache = Substitute.For<IRendererFeaturesCache>();
            rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>().Returns(feature);

            skyboxRenderController = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkyboxRenderController>(PREFAB_PATH));
            skyboxRenderController.Initialize(genesisMaterial, panoramicMaterial, null!, null!, TIME_OF_DAY, freezeTime: true);

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

        [Test]
        public void ApplyConstantFogColorAtFrozenTime()
        {
            // Arrange
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(RenderSettings.fogColor, Color.red);
            Assert.That(Overrides().AppliedEnvironment, Is.SameAs(Overrides().Environment));
        }

        [Test]
        public void RestoreFogColorWhenEnvironmentCleared()
        {
            // Arrange
            Color defaultFog = RenderSettings.fogColor;
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });
            system.Update(0);

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(RenderSettings.fogColor, defaultFog);
            Assert.That(Overrides().AppliedEnvironment, Is.Null);
        }

        [Test]
        public void EvaluateGradientAtCurrentTimeOfDay()
        {
            // Arrange
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.black), (1f, Color.white)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(RenderSettings.fogColor, new Color(TIME_OF_DAY, TIME_OF_DAY, TIME_OF_DAY, 1f));
        }

        [Test]
        public void DeriveAmbientEquatorFromHorizonOnly()
        {
            // Arrange
            Color defaultSky = RenderSettings.ambientSkyColor;
            Color defaultGround = RenderSettings.ambientGroundColor;
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(RenderSettings.ambientEquatorColor, Color.green);
            ColorRampShould.AssertColor(RenderSettings.ambientSkyColor, defaultSky);
            ColorRampShould.AssertColor(RenderSettings.ambientGroundColor, defaultGround);
        }

        [Test]
        public void WriteSkyColorsToGenesisMaterial()
        {
            // Arrange
            Color defaultZenith = genesisCopy.GetColor("_ZenitColor");
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.green);
            ColorRampShould.AssertColor(genesisCopy.GetColor("_ZenitColor"), defaultZenith);
        }

        [Test]
        public void DeriveRimFromHorizonWhenRimUnset()
        {
            // Arrange
            Overrides().Environment = Profile(new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Horizon = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), Color.red);
            ColorRampShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.red);
        }

        [Test]
        public void PreferExplicitRimOverHorizon()
        {
            // Arrange
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
            ColorRampShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), Color.blue);
            ColorRampShould.AssertColor(genesisCopy.GetColor(HORIZON_COLOR), Color.red);
        }

        [Test]
        public void TintCloudsColor()
        {
            // Arrange
            Overrides().Environment = Profile(new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Color = Gradient((0f, Color.green)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(genesisCopy.GetColor(CLOUDS_COLOR), Color.green);
        }

        [Test]
        public void KeepDefaultRimAndCloudsColorWhenOnlyFogIsSet()
        {
            // Arrange
            Color defaultRim = genesisCopy.GetColor(RIM_COLOR);
            Color defaultClouds = genesisCopy.GetColor(CLOUDS_COLOR);
            Overrides().Environment = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(genesisCopy.GetColor(RIM_COLOR), defaultRim);
            ColorRampShould.AssertColor(genesisCopy.GetColor(CLOUDS_COLOR), defaultClouds);
        }

        [Test]
        public void ApplyAndRestoreCloudOpacityAndStarsBrightness()
        {
            // Arrange
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
            Assert.That(genesisCopy.GetFloat(CLOUDS_ROTATION_SPEED), Is.EqualTo(DEFAULT_CLOUDS_ROTATION_SPEED));

            // Act
            Overrides().Environment = null;
            system.Update(0);

            // Assert
            Assert.That(genesisCopy.GetFloat(CLOUD_OPACITY), Is.EqualTo(DEFAULT_CLOUD_OPACITY));
            Assert.That(genesisCopy.GetFloat(STARS_BRIGHTNESS), Is.EqualTo(DEFAULT_STARS_BRIGHTNESS));
        }

        [Test]
        public void ApplyCloudsSpeed()
        {
            // Arrange
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
            Overrides().Environment = profile;
            Overrides().AppliedEnvironment = profile;
            RenderSettings.fogColor = Color.blue;

            // Act
            system.Update(0);

            // Assert
            ColorRampShould.AssertColor(RenderSettings.fogColor, Color.blue);
        }

        [Test]
        public void TintDirectionalLightFromSunColor()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: false);
            Color defaultLightColor = sunLight.color;
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Color = Gradient((0f, Color.red)) } });

            // Act
            sunController!.SetEnvironmentOverride(profile);

            // Assert
            ColorRampShould.AssertColor(sunLight.color, Color.red);
            ColorRampShould.AssertColor(sunGenesisCopy!.GetColor("_SunColor"), Color.red);

            // Act
            sunController.SetEnvironmentOverride(null);

            // Assert
            ColorRampShould.AssertColor(sunLight.color, defaultLightColor);
        }

        [Test]
        public void HideSunMoonAndFlareWhenSunNotVisible()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: true);
            var lensFlare = sunLight.GetComponent<LensFlareComponentSRP>();
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Visible = false } });

            // Act
            sunController!.SetEnvironmentOverride(profile);

            // Assert
            Assert.That(sunGenesisCopy!.GetFloat(SUN_OPACITY), Is.EqualTo(0f));
            Assert.That(sunGenesisCopy.GetFloat(SUN_RADIANCE), Is.EqualTo(0f));
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(0f));
            Assert.That(lensFlare.intensity, Is.EqualTo(0f));

            // Act
            sunController.SetEnvironmentOverride(null);

            // Assert
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(DEFAULT_SECOND_SUN_SIZE_FACTOR));
            Assert.That(sunGenesisCopy.GetFloat(SUN_OPACITY), Is.EqualTo(sunLight.transform.localScale.y));
        }

        [Test]
        public void KeepSunVisibleByDefault()
        {
            // Arrange
            Light sunLight = CreateSunController(lensFlareEnabled: true);
            var lensFlare = sunLight.GetComponent<LensFlareComponentSRP>();
            float defaultSunOpacity = sunGenesisCopy!.GetFloat(SUN_OPACITY);
            float defaultFlareIntensity = lensFlare.intensity;
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = Gradient((0f, Color.red)) } });

            // Act
            sunController!.SetEnvironmentOverride(profile);

            // Assert
            Assert.That(profile.SunVisible, Is.Null);
            Assert.That(sunGenesisCopy.GetFloat(SUN_OPACITY), Is.EqualTo(defaultSunOpacity));
            Assert.That(sunGenesisCopy.GetFloat(SECOND_SUN_SIZE_FACTOR), Is.EqualTo(DEFAULT_SECOND_SUN_SIZE_FACTOR));
            Assert.That(lensFlare.intensity, Is.EqualTo(defaultFlareIntensity));
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
            sunController.Initialize(genesisMaterial, panoramicMaterial, sunLight, null!, TIME_OF_DAY, lensFlareEnabled, freezeTime: true);
            sunGenesisCopy = RenderSettings.skybox;
            sunController.SetSkyboxOverride(skyboxTexture);
            sunPanoramicCopy = RenderSettings.skybox;
            sunController.SetSkyboxOverride(null);

            return sunLight;
        }

        private static ColorGradient Gradient(params (float time, Color color)[] keys) =>
            ColorRampShould.Gradient(keys);

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
