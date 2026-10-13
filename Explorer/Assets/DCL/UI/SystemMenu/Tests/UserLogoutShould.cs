using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.UI.SystemMenu.Tests
{
    [TestFixture]
    public class UserLogoutShould
    {
        private IWeb3IdentityCache identityCache = null!;
        private ICompositeWeb3Provider web3Authenticator = null!;
        private IProfileCache profileCache = null!;
        private IUserInAppInitializationFlow initializationFlow = null!;
        private World world = null!;
        private UserLogout logout = null!;

        [SetUp]
        public void SetUp()
        {
            identityCache = Substitute.For<IWeb3IdentityCache>();
            web3Authenticator = Substitute.For<ICompositeWeb3Provider>();
            web3Authenticator.LogoutAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            profileCache = Substitute.For<IProfileCache>();
            initializationFlow = Substitute.For<IUserInAppInitializationFlow>();
            initializationFlow.ExecuteAsync(Arg.Any<UserInAppInitializationFlowParameters>(), Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            world = World.Create();

            logout = new UserLogout(identityCache, web3Authenticator, profileCache, initializationFlow, world, Entity.Null);
        }

        [TearDown]
        public void TearDown() =>
            world.Dispose();

        [Test]
        public async Task SignOutForgetTheProfileAndRestartAtTheAuthentication()
        {
            //Arrange
            var identity = new IWeb3Identity.Random();
            Web3Address address = identity.Address;
            identityCache.Identity.Returns(identity);

            //Act
            await logout.LogoutAsync(CancellationToken.None);

            //Assert
            Received.InOrder(() =>
            {
                web3Authenticator.LogoutAsync(Arg.Any<CancellationToken>());
                profileCache.Remove(address);

                initializationFlow.ExecuteAsync(Arg.Is<UserInAppInitializationFlowParameters>(p =>
                    p.LoadSource == IUserInAppInitializationFlow.LoadSource.Logout && p.ShowAuthentication && p.ShowLoading && p.World == world), Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public async Task DoNothingWithoutAnIdentity()
        {
            //Arrange
            identityCache.Identity.Returns((IWeb3Identity?)null);
            LogAssert.Expect(LogType.Error, new Regex("Cannot logout"));

            //Act
            await logout.LogoutAsync(CancellationToken.None);

            //Assert
            await web3Authenticator.DidNotReceive().LogoutAsync(Arg.Any<CancellationToken>());
            await initializationFlow.DidNotReceive().ExecuteAsync(Arg.Any<UserInAppInitializationFlowParameters>(), Arg.Any<CancellationToken>());
        }
    }
}
