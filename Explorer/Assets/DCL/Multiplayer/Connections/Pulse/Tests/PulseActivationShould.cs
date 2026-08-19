using NUnit.Framework;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class PulseActivationShould
    {
        [Test]
        public void StartActiveWhenSeededActive()
        {
            // Arrange
            var activation = new PulseActivation(true);

            // Assert
            Assert.IsTrue(activation.IsActive);
        }

        [Test]
        public void StartInactiveWhenSeededInactive()
        {
            // Arrange
            var activation = new PulseActivation(false);

            // Assert
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void BecomeInactiveAfterDeactivate()
        {
            // Arrange
            var activation = new PulseActivation(true);

            // Act
            activation.Deactivate();

            // Assert
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void StayInactiveWhenDeactivatedRepeatedly()
        {
            // Arrange
            var activation = new PulseActivation(true);

            // Act
            activation.Deactivate();
            activation.Deactivate();

            // Assert
            Assert.IsFalse(activation.IsActive);
        }
    }
}
