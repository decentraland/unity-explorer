using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace DCL.RuntimeDeepLink.Tests
{
    public class DeepLinkHandleShould
    {
        private IDeeplinkSigninDispatcher dispatcher;
        private DeepLinkHandle handle;

        [SetUp]
        public void SetUp()
        {
            dispatcher = Substitute.For<IDeeplinkSigninDispatcher>();

            // StartParcel, ChatTeleporter and CommunityDataService are concrete deps that the signin path never
            // reaches, so null! is safe here. The no-signin test below deliberately uses a deep link with no
            // routable params to keep them untouched.
            handle = new DeepLinkHandle(null!, null!, CancellationToken.None, null!, dispatcher);
        }

        [Test]
        public void RouteSigninToTheDispatcher()
        {
            DeepLink deeplink = DeepLink.FromRaw("decentraland://open?signin=identity-1").Value;

            DCL.Utility.Types.Result result = handle.HandleDeepLink(deeplink);

            Assert.That(result.Success, Is.True);
            dispatcher.Received(1).Dispatch("identity-1");
        }

        [Test]
        public void PreferSigninOverRealm()
        {
            DeepLink deeplink = DeepLink.FromRaw("decentraland://open?signin=identity-1&realm=https://my.realm").Value;

            DCL.Utility.Types.Result result = handle.HandleDeepLink(deeplink);

            Assert.That(result.Success, Is.True);
            dispatcher.Received(1).Dispatch("identity-1");
        }

        [Test]
        public void NotDispatchWhenNoSignin()
        {
            // A deep link with no routable params hits the "no matches" branch without touching any concrete dep,
            // so the null! deps stay safe while we assert the dispatcher was never invoked.
            DeepLink deeplink = DeepLink.FromRaw("decentraland://open?foo=bar").Value;

            DCL.Utility.Types.Result result = handle.HandleDeepLink(deeplink);

            Assert.That(result.Success, Is.False);
            dispatcher.DidNotReceive().Dispatch(Arg.Any<string>());
        }
    }
}
