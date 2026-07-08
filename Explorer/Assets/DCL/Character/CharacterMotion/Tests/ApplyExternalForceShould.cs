using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class ApplyExternalForceShould
    {
        private const float GLIDE_WIND_RESPONSE = 1.5f;
        private const float DT = 0.02f;

        private ICharacterControllerSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = Substitute.For<ICharacterControllerSettings>();
            settings.CharacterMass.Returns(1f);
            settings.GlideWindResponse.Returns(GLIDE_WIND_RESPONSE);
        }

        [Test]
        public void ComputeAccelerationFromForce()
        {
            var rigidTransform = new CharacterRigidTransform
                {
                    ExternalForce = new Vector3(10f, 20f, 0f),
                };

            var glideState = new GlideState { Value = GlideStateValue.PROP_CLOSED };

            ApplyExternalForce.Execute(settings, ref rigidTransform, glideState, DT);

            Assert.IsTrue(Mathf.Approximately(10f, rigidTransform.ExternalAcceleration.x), "Acceleration is force divided by mass");
            Assert.IsTrue(Mathf.Approximately(20f, rigidTransform.ExternalAcceleration.y), "Acceleration is force divided by mass");
        }

        [Test]
        public void MultiplyAccelerationWhileGliding()
        {
            var rigidTransform = new CharacterRigidTransform
                {
                    ExternalForce = new Vector3(10f, 20f, 0f),
                };

            var glideState = new GlideState { Value = GlideStateValue.GLIDING };

            ApplyExternalForce.Execute(settings, ref rigidTransform, glideState, DT);

            Assert.IsTrue(Mathf.Approximately(10f * GLIDE_WIND_RESPONSE, rigidTransform.ExternalAcceleration.x), "Wind response multiplier is applied while gliding");
            Assert.IsTrue(Mathf.Approximately(20f * GLIDE_WIND_RESPONSE, rigidTransform.ExternalAcceleration.y), "Wind response multiplier is applied while gliding");
        }

        [Test]
        public void IntegrateOnlyHorizontalVelocity()
        {
            var rigidTransform = new CharacterRigidTransform
                {
                    ExternalForce = new Vector3(10f, 20f, 5f),
                };

            var glideState = new GlideState { Value = GlideStateValue.GLIDING };

            ApplyExternalForce.Execute(settings, ref rigidTransform, glideState, DT);

            Assert.IsTrue(Mathf.Approximately(rigidTransform.ExternalAcceleration.x * DT, rigidTransform.ExternalVelocity.x), "Horizontal X velocity is integrated");
            Assert.IsTrue(Mathf.Approximately(rigidTransform.ExternalAcceleration.z * DT, rigidTransform.ExternalVelocity.z), "Horizontal Z velocity is integrated");
            Assert.IsTrue(Mathf.Approximately(0f, rigidTransform.ExternalVelocity.y), "Vertical velocity is handled by gravity via ExternalAcceleration.y");
        }
    }
}
