using NUnit.Framework;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class AuthenticationScreenControllerShould
    {
        [Test]
        public void NotThrowOnDisposeWhenViewWasNeverShown()
        {
            // Registered-but-never-shown lifecycle: the view factory is never invoked, so
            // OnViewInstantiated never runs and the lazily-created members stay null
            // (sessions with --skip-auth-screen + a valid cached identity).
            // The constructor only stores its dependencies; none are dereferenced before the view exists.
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
