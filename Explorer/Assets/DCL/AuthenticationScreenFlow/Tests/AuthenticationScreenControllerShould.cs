using DCL.Profiles;
using ECS.TestSuite;
using NUnit.Framework;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class AuthenticationScreenControllerShould
    {
        private const string FAKE_WALLET = "0x0000000000000000000000000000000000000001";

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [Test]
        public void NotThrowOnDisposeWhenViewWasNeverShown()
        {
            AuthenticationScreenController controller = NewNeverShownController();

            Assert.DoesNotThrow(controller.Dispose);
        }

        [TestCase(true, AuthStatus.LoggedInCached)]
        [TestCase(false, AuthStatus.LoggedIn)]
        public void ReportLoggedInWhenCompletingExistingAccountLoginWithoutWelcomeStep(bool isRestoredSession, AuthStatus expectedStatus)
        {
            // Arrange
            AuthenticationScreenController controller = NewNeverShownController();
            controller.IsCurrentlyNewAccount = true;

            // Act
            controller.CompleteExistingAccountLogin(Profile.NewRandomProfile(FAKE_WALLET), isRestoredSession);

            // Assert
            Assert.That(controller.CurrentState.Value, Is.EqualTo(expectedStatus));
            Assert.That(controller.IsCurrentlyNewAccount, Is.False);
        }

        // Mirrors --skip-auth-screen with a cached identity: the view is never instantiated, so the constructor only stores the null! dependencies
        internal static AuthenticationScreenController NewNeverShownController() =>
            new (
                () => null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                string.Empty,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);
    }
}
