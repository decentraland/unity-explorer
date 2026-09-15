using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Threading;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class AuthenticationScreenControllerShould
    {
        [Test]
        public void NotThrowOnDisposeWhenViewWasNeverShown()
        {
            var controller = new TestController();

            Assert.DoesNotThrow(controller.Dispose);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompleteLoginBeforeOrAfterTheViewAppears(bool beforeShow)
        {
            using var controller = new TestController();
            if (beforeShow) controller.TrySetLifeCycle();
            UniTask closing = controller.WaitAsync();
            if (!beforeShow) controller.TrySetLifeCycle();
            Assert.AreEqual(UniTaskStatus.Succeeded, closing.Status);
        }

        private sealed class TestController : AuthenticationScreenController
        {
            public TestController() : base(
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
                null!,
                null!) { }

            public UniTask WaitAsync() => WaitForCloseIntentAsync(CancellationToken.None);
        }
    }
}
