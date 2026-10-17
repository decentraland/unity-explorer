using NUnit.Framework;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyStageShould
    {
        [TestCase(1.8f, 2.17f, 1.03f, 1f)]
        [TestCase(2.17f, 2.17f, 1.03f, 1f)]
        [TestCase(2.4f, 2f, 1.03f, 1.236f)]
        [TestCase(2.4f, 0f, 1.03f, 1f)]
        [TestCase(2.4f, -0.5f, 1.03f, 1f)]
        public void ComputePullBackFactor(float avatarHeight, float fittingHeight, float headroom, float expected)
        {
            // Act
            float factor = LobbyStage.PullBackFactor(avatarHeight, fittingHeight, headroom);

            // Assert
            Assert.AreEqual(expected, factor, 1e-4f);
        }
    }
}
