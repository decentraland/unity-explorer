using DCL.ECSComponents;
using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;
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
            Assert.That(profile.SunVisible, Is.Null);
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
            var pbSkybox = new PBSkybox { Clouds = new PBSkybox.Types.Clouds { Color = ColorRampShould.Gradient((0f, Color.green)) } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.CloudsColor, Is.Not.Null);
            ColorRampShould.AssertColor(profile.CloudsColor!.Evaluate(0.5f), Color.green);
            Assert.That(profile.CloudsOpacity, Is.Null);
            Assert.That(profile.CloudsSpeed, Is.Null);
        }

        [Test]
        public void BuildProfileFromRimAlone()
        {
            // Arrange
            var pbSkybox = new PBSkybox { SkyColors = new PBSkybox.Types.SkyColors { Rim = ColorRampShould.Gradient((0f, Color.magenta)) } };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Rim, Is.Not.Null);
            ColorRampShould.AssertColor(profile.Rim!.Evaluate(0.5f), Color.magenta);
            Assert.That(profile.Horizon, Is.Null);
        }

        [Test]
        public void BuildRampsPerSkyChannel()
        {
            // Arrange
            var pbSkybox = new PBSkybox
            {
                Sun = new PBSkybox.Types.Sun { Color = ColorRampShould.Gradient((0f, Color.yellow)) },
                SkyColors = new PBSkybox.Types.SkyColors
                {
                    Zenith = ColorRampShould.Gradient((0f, Color.blue)),
                    Nadir = ColorRampShould.Gradient((0f, Color.black)),
                },
                Fog = new PBSkybox.Types.Fog { Color = ColorRampShould.Gradient((0f, Color.gray)) },
            };

            // Act
            SceneEnvironmentProfile? profile = SceneEnvironmentProfile.FromProto(pbSkybox);

            // Assert
            Assert.That(profile, Is.Not.Null);
            ColorRampShould.AssertColor(profile!.SunColor!.Evaluate(0.5f), Color.yellow);
            ColorRampShould.AssertColor(profile.Zenith!.Evaluate(0.5f), Color.blue);
            Assert.That(profile.Horizon, Is.Null);
            ColorRampShould.AssertColor(profile.Nadir!.Evaluate(0.5f), Color.black);
            ColorRampShould.AssertColor(profile.FogColor!.Evaluate(0.5f), Color.gray);
        }
    }
}
