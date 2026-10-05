using DCL.ECSComponents;
using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Texture = Decentraland.Common.Texture;

namespace DCL.SkyBox.Tests
{
    public class SceneEnvironmentProfileShould
    {
        [Test]
        public void ReturnNullWhenNothingIsSet()
        {
            // Arrange
            var pbSkybox = new PBSkybox { SkyboxTexture = new TextureUnion { Texture = new Texture { Src = "images/sky.png" } } };

            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(pbSkybox), Is.Null);
        }

        [Test]
        public void ReturnNullWhenGroupsAreSetButEmpty()
        {
            // Arrange
            var pbSkybox = new PBSkybox
            {
                Sun = new PBSkybox.Types.Sun { Color = new ColorGradient() },
                SkyColors = new PBSkybox.Types.SkyColors(),
                Fog = new PBSkybox.Types.Fog(),
                Clouds = new PBSkybox.Types.Clouds(),
                Stars = new PBSkybox.Types.Stars(),
            };

            // Act & Assert
            Assert.That(SceneEnvironmentProfile.FromProto(pbSkybox), Is.Null);
        }

        [Test]
        public void KeepUnsetCloudFieldsNull()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Speed = 0.2f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.CloudsOpacity, Is.Null);
            Assert.That(profile.CloudsSpeed, Is.EqualTo(0.2f));
            Assert.That(profile.StarsBrightness, Is.Null);
            Assert.That(profile.SunColor, Is.Null);
            Assert.That(profile.FogColor, Is.Null);
            Assert.That(profile.FogDensity, Is.Null);
            Assert.That(profile.SunVisible, Is.Null);
        }

        [Test]
        public void BuildProfileFromFogDensityAlone()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Fog = new PBSkybox.Types.Fog { Density = 0.02f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogDensity, Is.EqualTo(0.02f));
            Assert.That(profile.FogColor, Is.Null);
        }

        [Test]
        public void KeepUnsetFogDensityNull()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Fog = new PBSkybox.Types.Fog { Color = ColorGradientConverterShould.Gradient((0f, Color.gray)) } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogColor, Is.Not.Null);
            Assert.That(profile.FogDensity, Is.Null);
        }

        [Test]
        public void ClampNegativeFogDensityToZero()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Fog = new PBSkybox.Types.Fog { Density = -0.5f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FogDensity, Is.EqualTo(0f));
        }

        [Test]
        public void BuildProfileFromSunVisibilityAlone()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Sun = new PBSkybox.Types.Sun { Visible = false } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.SunVisible, Is.False);
            Assert.That(profile.SunColor, Is.Null);
        }

        [Test]
        public void ClampCloudsOpacityToUnitRange()
        {
            // Arrange
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Opacity = 3f } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

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
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

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
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

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
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

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
                Sun = new PBSkybox.Types.Sun { Color = ColorGradientConverterShould.Gradient((0f, Color.yellow)) },
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Zenith = ColorGradientConverterShould.Gradient((0f, Color.blue)),
                    Nadir = ColorGradientConverterShould.Gradient((0f, Color.black)),
                },
                Fog = new PBSkybox.Types.Fog { Color = ColorGradientConverterShould.Gradient((0f, Color.gray)) },
            };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

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
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Fog = new PBSkybox.Types.Fog { Density = 0.02f } });

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
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Color = ColorGradientConverterShould.Gradient((0f, Color.red)) } });

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
            SceneEnvironmentProfile profile = Profile(new PBSkybox { Sun = new PBSkybox.Types.Sun { Visible = false } });

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

        [Test]
        public void LeaveCloudsCubemapAloneOnApply()
        {
            // Arrange
            using var presets = new Presets();
            var cubemap = new RenderTexture(2, 2, 0);
            presets.Target.CloudsCubemap = cubemap;

            try
            {
                // Act
                SceneEnvironmentProfile.EMPTY.ApplyTo(presets.Target, presets.Defaults);

                // Assert
                Assert.That(presets.Target.CloudsCubemap, Is.SameAs(cubemap));
            }
            finally { Object.DestroyImmediate(cubemap); }
        }

        private static SceneEnvironmentProfile Profile(PBSkybox pbSkybox)
        {
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);
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
