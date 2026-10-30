using DCL.ECSComponents;
using NUnit.Framework;
using Vector3 = UnityEngine.Vector3;

namespace DCL.SkyBox.Tests
{
    public class SceneSunOverrideShould
    {
        [Test]
        public void BeNeutralForAMissingComponent()
        {
            // Act
            SceneSunOverride sun = SceneSunOverride.FromProto(null);

            // Assert
            Assert.That(sun, Is.EqualTo(SceneSunOverride.NONE));
            Assert.That(sun.Intensity, Is.Null);
            Assert.That(sun.Direction, Is.Null);
        }

        [Test]
        public void BeNeutralWhenNothingIsSet()
        {
            // Act & Assert
            Assert.That(SceneSunOverride.FromProto(new PBSun { Visible = false }), Is.EqualTo(SceneSunOverride.NONE));
        }

        [Test]
        public void BuildFromIntensityAlone()
        {
            // Act
            SceneSunOverride sun = SceneSunOverride.FromProto(new PBSun { Intensity = 2.5f });

            // Assert
            Assert.That(sun.Intensity, Is.EqualTo(2.5f));
            Assert.That(sun.Direction, Is.Null);
        }

        [Test]
        public void ClampNegativeIntensityToZero()
        {
            // Act & Assert
            Assert.That(SceneSunOverride.FromProto(new PBSun { Intensity = -3f }).Intensity, Is.EqualTo(0f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TreatNonFiniteIntensityAsUnset(float intensity)
        {
            // Act & Assert
            Assert.That(SceneSunOverride.FromProto(new PBSun { Intensity = intensity }).Intensity, Is.Null);
        }

        [Test]
        public void NormalizeDirection()
        {
            // Act
            SceneSunOverride sun = SceneSunOverride.FromProto(new PBSun { Direction = new Decentraland.Common.Vector3 { X = 0f, Y = -4f, Z = 3f } });

            // Assert
            Assert.That(sun.Direction, Is.Not.Null);
            Vector3 direction = sun.Direction!.Value;
            Assert.That(direction.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(direction.y, Is.EqualTo(-0.8f).Within(1e-5f));
            Assert.That(direction.z, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(1e-5f, 0f, 0f)]
        [TestCase(float.NaN, 0f, 1f)]
        [TestCase(0f, float.PositiveInfinity, 0f)]
        public void TreatZeroOrNonFiniteDirectionAsUnset(float x, float y, float z)
        {
            // Act
            SceneSunOverride sun = SceneSunOverride.FromProto(new PBSun { Direction = new Decentraland.Common.Vector3 { X = x, Y = y, Z = z } });

            // Assert
            Assert.That(sun.Direction, Is.Null);
            Assert.That(sun, Is.EqualTo(SceneSunOverride.NONE));
        }

        [Test]
        public void CompareByValue()
        {
            // Arrange
            var pbSun = new PBSun { Intensity = 2f, Direction = new Decentraland.Common.Vector3 { X = 1f } };

            // Act & Assert
            Assert.That(SceneSunOverride.FromProto(pbSun), Is.EqualTo(SceneSunOverride.FromProto(pbSun)));
            Assert.That(SceneSunOverride.FromProto(pbSun), Is.Not.EqualTo(SceneSunOverride.FromProto(new PBSun { Intensity = 2f })));
            Assert.That(SceneSunOverride.FromProto(pbSun), Is.Not.EqualTo(SceneSunOverride.FromProto(new PBSun { Intensity = 3f, Direction = pbSun.Direction })));
        }
    }
}
