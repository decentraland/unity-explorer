using DCL.ECSComponents;
using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.SkyBox.Tests
{
    public class SceneEnvironmentProfileShould
    {
        [Test]
        public void ReturnNullWhenNothingIsSet()
        {
            // Arrange
            var pbSkybox = new PBSkybox { SkyboxTexture = "images/sky.png" };

            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(pbSkybox, null, null), Is.Null);
        }

        [Test]
        public void ReturnNullWhenNoComponentIsPresent()
        {
            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(null, null, null), Is.Null);
        }

        [Test]
        public void ReturnNullWhenGroupsAreSetButEmpty()
        {
            // Arrange
            var pbSkybox = new PBSkybox
            {
                SkyColors = new PBSkybox.Types.SkyColors(),
                Clouds = new PBSkybox.Types.Clouds(),
                Stars = new PBSkybox.Types.Stars(),
            };

            var pbSun = new PBSun { Color = new ColorGradient(), Direction = new Decentraland.Common.Vector3() };
            var pbFog = new PBEnvironmentFog { Color = new ColorGradient() };

            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(pbSkybox, pbSun, pbFog), Is.Null);
        }

        [Test]
        public void KeepUnsetCloudFieldsNull()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Speed = 0.2f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, new PBSun(), new PBEnvironmentFog());

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.CloudsOpacity, Is.Null);
            Assert.That(profile.CloudsSpeed, Is.EqualTo(0.2f));
            Assert.That(profile.StarsBrightness, Is.Null);
            Assert.That(profile.SunColor, Is.Null);
            Assert.That(profile.FogColor, Is.Null);
            Assert.That(profile.FogDensity, Is.Null);
            Assert.That(profile.SunVisible, Is.Null);
            Assert.That(profile.SunIntensity, Is.Null);
            Assert.That(profile.SunDirection, Is.Null);
        }

        [Test]
        public void BuildProfileFromFogDensityAlone()
        {
            // Arrange
            var pbFog = new PBEnvironmentFog { Density = 0.02f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, null, pbFog);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogDensity, Is.EqualTo(0.02f));
            Assert.That(profile.FogColor, Is.Null);
        }

        [Test]
        public void KeepUnsetFogDensityNull()
        {
            // Arrange
            var pbFog = new PBEnvironmentFog { Color = ColorGradientConverterShould.Gradient((0f, Color.gray)) };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, null, pbFog);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogColor, Is.Not.Null);
            Assert.That(profile.FogDensity, Is.Null);
        }

        [Test]
        public void ClampNegativeFogDensityToZero()
        {
            // Arrange
            var pbFog = new PBEnvironmentFog { Density = -0.5f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, null, pbFog);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogDensity, Is.EqualTo(0f));
        }

        [Test]
        public void BuildProfileFromSunVisibilityAlone()
        {
            // Arrange
            var pbSun = new PBSun { Visible = false };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, pbSun, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunVisible, Is.False);
            Assert.That(profile.SunColor, Is.Null);
        }

        [Test]
        public void BuildProfileFromSunIntensityAlone()
        {
            // Arrange
            var pbSun = new PBSun { Intensity = 2.5f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, pbSun, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunIntensity, Is.EqualTo(2.5f));
            Assert.That(profile.SunDirection, Is.Null);
            Assert.That(profile.SunVisible, Is.Null);
        }

        [Test]
        public void ClampNegativeSunIntensityToZero()
        {
            // Arrange
            var pbSun = new PBSun { Intensity = -3f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, pbSun, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void NormalizeSunDirection()
        {
            // Arrange
            var pbSun = new PBSun { Direction = new Decentraland.Common.Vector3 { X = 0f, Y = -4f, Z = 3f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, pbSun, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunDirection, Is.Not.Null);
            Vector3 direction = profile.SunDirection!.Value;
            Assert.That(direction.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(direction.y, Is.EqualTo(-0.8f).Within(1e-5f));
            Assert.That(direction.z, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void IgnoreZeroSunDirection()
        {
            // Arrange
            var pbSun = new PBSun { Direction = new Decentraland.Common.Vector3 { X = 0f, Y = 0f, Z = 0f }, Intensity = 1f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(null, pbSun, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunDirection, Is.Null);
        }

        [Test]
        public void ReturnNullWhenOnlyANearZeroSunDirectionIsSet()
        {
            // Arrange
            var pbSun = new PBSun { Direction = new Decentraland.Common.Vector3 { X = 1e-5f, Y = 0f, Z = 0f } };

            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(null, pbSun, null), Is.Null);
        }

        [Test]
        public void CombineValuesFromAllThreeComponents()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Stars = new PBSkybox.Types.Stars { Brightness = 2f } };
            var pbSun = new PBSun { Color = ColorGradientConverterShould.Gradient((0f, Color.red)), Intensity = 0.5f };
            var pbFog = new PBEnvironmentFog { Density = 0.01f };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, pbSun, pbFog);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.StarsBrightness, Is.EqualTo(2f));
            ColorGradientConverterShould.AssertColor(profile.SunColor!.Evaluate(0.5f), Color.red);
            Assert.That(profile.SunIntensity, Is.EqualTo(0.5f));
            Assert.That(profile.FogDensity, Is.EqualTo(0.01f));
        }

        [Test]
        public void ClampCloudsOpacityToUnitRange()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Opacity = 3f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, null, null);

            // Assert
            Assert.That(profile!.CloudsOpacity, Is.EqualTo(1f));
        }

        [Test]
        public void ClampNegativeSpeedAndBrightnessToZero()
        {
            // Arrange
            var pbSkybox = new PBSkybox
            {
                Clouds = new PBSkybox.Types.Clouds { Speed = -1f },
                Stars = new PBSkybox.Types.Stars { Brightness = -5f },
            };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, null, null);

            // Assert
            Assert.That(profile!.CloudsSpeed, Is.EqualTo(0f));
            Assert.That(profile.StarsBrightness, Is.EqualTo(0f));
        }

        [Test]
        public void BuildProfileFromCloudsColorAlone()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Color = ColorGradientConverterShould.Gradient((0f, Color.green)) } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, null, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.CloudsColor, Is.Not.Null);
            ColorGradientConverterShould.AssertColor(profile.CloudsColor!.Evaluate(0.5f), Color.green);
            Assert.That(profile.CloudsOpacity, Is.Null);
            Assert.That(profile.CloudsSpeed, Is.Null);
        }

        [Test]
        public void BuildProfileFromRimAlone()
        {
            // Arrange
            var pbSkybox = new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Rim = ColorGradientConverterShould.Gradient((0f, Color.magenta)) } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, null, null);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Rim, Is.Not.Null);
            ColorGradientConverterShould.AssertColor(profile.Rim!.Evaluate(0.5f), Color.magenta);
            Assert.That(profile.Horizon, Is.Null);
        }

        [Test]
        public void BuildRampsPerSkyChannel()
        {
            // Arrange
            var pbSkybox = new PBSkybox
            {
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Zenith = ColorGradientConverterShould.Gradient((0f, Color.blue)),
                    Nadir = ColorGradientConverterShould.Gradient((0f, Color.black)),
                },
            };

            var pbSun = new PBSun { Color = ColorGradientConverterShould.Gradient((0f, Color.yellow)) };
            var pbFog = new PBEnvironmentFog { Color = ColorGradientConverterShould.Gradient((0f, Color.gray)) };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox, pbSun, pbFog);

            // Assert
            Assert.That(profile, Is.Not.Null);
            ColorGradientConverterShould.AssertColor(profile!.SunColor!.Evaluate(0.5f), Color.yellow);
            ColorGradientConverterShould.AssertColor(profile.Zenith!.Evaluate(0.5f), Color.blue);
            Assert.That(profile.Horizon, Is.Null);
            ColorGradientConverterShould.AssertColor(profile.Nadir!.Evaluate(0.5f), Color.black);
            ColorGradientConverterShould.AssertColor(profile.FogColor!.Evaluate(0.5f), Color.gray);
        }

        [Test]
        public void WriteDefaultsForEveryUnsetValueOnApply()
        {
            // Arrange
            using var presets = new Presets();
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Speed = 0.3f } });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.CloudsRotationSpeed, Is.EqualTo(0.3f));
            Assert.That(presets.Target.DirectionalColorRamp, Is.SameAs(presets.Defaults.DirectionalColorRamp));
            Assert.That(presets.Target.SunColorRamp, Is.SameAs(presets.Defaults.SunColorRamp));
            Assert.That(presets.Target.SkyZenitColorRamp, Is.SameAs(presets.Defaults.SkyZenitColorRamp));
            Assert.That(presets.Target.SkyHorizonColorRamp, Is.SameAs(presets.Defaults.SkyHorizonColorRamp));
            Assert.That(presets.Target.SkyNadirColorRamp, Is.SameAs(presets.Defaults.SkyNadirColorRamp));
            Assert.That(presets.Target.RimColorRamp, Is.SameAs(presets.Defaults.RimColorRamp));
            Assert.That(presets.Target.IndirectSkyRamp, Is.SameAs(presets.Defaults.IndirectSkyRamp));
            Assert.That(presets.Target.IndirectEquatorRamp, Is.SameAs(presets.Defaults.IndirectEquatorRamp));
            Assert.That(presets.Target.GroundEquatorRamp, Is.SameAs(presets.Defaults.GroundEquatorRamp));
            Assert.That(presets.Target.FogColorRamp, Is.SameAs(presets.Defaults.FogColorRamp));
            Assert.That(presets.Target.FogDensityByPhase, Is.EqualTo(presets.Defaults.FogDensityByPhase));
            Assert.That(presets.Target.CloudsColorRamp, Is.SameAs(presets.Defaults.CloudsColorRamp));
            Assert.That(presets.Target.CloudOpacity, Is.EqualTo(presets.Defaults.CloudOpacity));
            Assert.That(presets.Target.StarsBrightness, Is.EqualTo(presets.Defaults.StarsBrightness));
            Assert.That(presets.Target.SunOpacity, Is.SameAs(presets.Defaults.SunOpacity));
            Assert.That(presets.Target.SunRadiance, Is.SameAs(presets.Defaults.SunRadiance));
            Assert.That(presets.Target.SunRadianceIntensity, Is.SameAs(presets.Defaults.SunRadianceIntensity));
            Assert.That(presets.Target.LensFlareIntensity, Is.SameAs(presets.Defaults.LensFlareIntensity));
            Assert.That(presets.Target.SecondSunSizeFactor, Is.EqualTo(presets.Defaults.SecondSunSizeFactor));
        }

        [Test]
        public void WriteConstantFogDensityToEveryPhaseOnApply()
        {
            // Arrange
            using var presets = new Presets();
            SceneEnvironmentProfile profile = Profile(fog: new PBEnvironmentFog { Density = 0.02f });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.FogDensityByPhase, Is.EqualTo(new Vector4(0.02f, 0.02f, 0.02f, 0.02f)));
            Assert.That(presets.Target.FogColorRamp, Is.SameAs(presets.Defaults.FogColorRamp));
        }

        [Test]
        public void PinPhaseToTimeOfDayOnApply()
        {
            // Arrange
            using var presets = new Presets();

            // Act
            SceneEnvironmentProfile.EMPTY.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.EvaluatePhase(0.3f), Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(presets.Target.EvaluatePhase(0.8f), Is.EqualTo(0.8f).Within(1e-4f));
        }

        [Test]
        public void DeriveAmbientAndRimFromSkyColorsOnApply()
        {
            // Arrange
            using var presets = new Presets();

            SceneEnvironmentProfile profile = Profile(new PBSkybox
            {
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Zenith = ColorGradientConverterShould.Gradient((0f, Color.blue)),
                    Horizon = ColorGradientConverterShould.Gradient((0f, Color.red)),
                },
            });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.IndirectSkyRamp, Is.SameAs(profile.Zenith));
            Assert.That(presets.Target.IndirectEquatorRamp, Is.SameAs(profile.Horizon));
            Assert.That(presets.Target.GroundEquatorRamp, Is.SameAs(presets.Defaults.GroundEquatorRamp));
            Assert.That(presets.Target.RimColorRamp, Is.SameAs(profile.Horizon));
            Assert.That(presets.Target.SkyNadirColorRamp, Is.SameAs(presets.Defaults.SkyNadirColorRamp));
        }

        [Test]
        public void PreferExplicitRimOnApply()
        {
            // Arrange
            using var presets = new Presets();

            SceneEnvironmentProfile profile = Profile(new PBSkybox
            {
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Horizon = ColorGradientConverterShould.Gradient((0f, Color.red)),
                    Rim = ColorGradientConverterShould.Gradient((0f, Color.yellow)),
                },
            });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.RimColorRamp, Is.SameAs(profile.Rim));
        }

        [Test]
        public void TintLightAndDiscFromSunColorOnApply()
        {
            // Arrange
            using var presets = new Presets();
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Color = ColorGradientConverterShould.Gradient((0f, Color.red)) });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.DirectionalColorRamp, Is.SameAs(profile.SunColor));
            Assert.That(presets.Target.SunColorRamp, Is.SameAs(profile.SunColor));
        }

        [Test]
        public void ZeroDiscHaloMoonAndFlareWhenSunHiddenOnApply()
        {
            // Arrange
            using var presets = new Presets();
            SceneEnvironmentProfile profile = Profile(sun: new PBSun { Visible = false });

            // Act
            profile.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.SunOpacity.Evaluate(0.5f), Is.EqualTo(0f));
            Assert.That(presets.Target.SunRadiance.Evaluate(0.5f), Is.EqualTo(0f));
            Assert.That(presets.Target.SunRadianceIntensity.Evaluate(0.5f), Is.EqualTo(0f));
            Assert.That(presets.Target.LensFlareIntensity.Evaluate(0.5f), Is.EqualTo(0f));
            Assert.That(presets.Target.SecondSunSizeFactor, Is.EqualTo(0f));

            // Act: the next profile without the flag brings the defaults back
            SceneEnvironmentProfile.EMPTY.ApplyTo(presets.Target, presets.Defaults);

            // Assert
            Assert.That(presets.Target.SunOpacity, Is.SameAs(presets.Defaults.SunOpacity));
            Assert.That(presets.Target.SecondSunSizeFactor, Is.EqualTo(presets.Defaults.SecondSunSizeFactor));
        }

        private static SceneEnvironmentProfile Profile(PBSkybox? skybox = null, PBSun? sun = null, PBEnvironmentFog? fog = null)
        {
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(skybox, sun, fog);
            Assert.That(profile, Is.Not.Null);
            return profile!;
        }

        /// <summary>
        ///     A defaults preset with distinct values and an empty target to write into, both destroyed at the end of the test.
        /// </summary>
        private sealed class Presets : System.IDisposable
        {
            public readonly SkyboxLookPreset Defaults = ScriptableObject.CreateInstance<SkyboxLookPreset>();
            public readonly SkyboxLookPreset Target = ScriptableObject.CreateInstance<SkyboxLookPreset>();

            public Presets()
            {
                Defaults.CloudOpacity = 0.7f;
                Defaults.CloudsRotationSpeed = 0.02f;
                Defaults.StarsBrightness = 3f;
                Defaults.SecondSunSizeFactor = 0.15f;
                Defaults.FogDensityByPhase = new Vector4(0.001f, 0.002f, 0.003f, 0.004f);
                Defaults.SunOpacity = AnimationCurve.Constant(0f, 1f, 0.9f);
                Defaults.TimeToPhase = AnimationCurve.Linear(0f, 0f, 1f, 0.5f);
                Target.TimeToPhase = AnimationCurve.Linear(0f, 0f, 1f, 0.5f);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Defaults);
                Object.DestroyImmediate(Target);
            }
        }
    }
}
