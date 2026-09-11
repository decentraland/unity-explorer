using NUnit.Framework;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class AuthenticationScreenControllerShould
    {
        [Test]
        public void NotThrowOnDisposeWhenViewWasNeverShown()
        {
            // Never-shown lifecycle (--skip-auth-screen with a cached identity): OnViewInstantiated never runs,
            // so lazily-created members stay null; the constructor only stores dependencies, so null! args are safe.
            var controller = new AuthenticationScreenController(
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

            Assert.DoesNotThrow(controller.Dispose);
        }
    }
}
