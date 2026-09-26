using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.PrivateWorlds.UI;
using ECS;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace DCL.PrivateWorlds.Tests
{
    public class PrivateWorldAccessHandlerShould
    {
        private static readonly URLDomain WORLD_REALM = URLDomain.FromString("https://worlds.example.com/world/private-world.dcl.eth");
        private const string WORLD_NAME = "private-world.dcl.eth";
        private const string PASSWORD = "example-password-123";

        private IWorldPermissionsService worldPermissionsService = null!;
        private IMVCManager mvcManager = null!;
        private IRealmData realmData = null!;
        private PrivateWorldAccessHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            worldPermissionsService = Substitute.For<IWorldPermissionsService>();
            mvcManager = Substitute.For<IMVCManager>();
            realmData = Substitute.For<IRealmData>();
            handler = new PrivateWorldAccessHandler(worldPermissionsService, mvcManager, realmData);
        }

        [Test]
        public void ScopePendingSecretToValidatedRealmAndZeroPopupPasswordWhenSubmitted()
        {
            //Arrange
            SetAccessCheckResult(WorldAccessCheckResult.PasswordRequired);
            PrivateWorldPopupParams? shownParams = null;

            mvcManager.ShowAsync(Arg.Do<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(command =>
            {
                shownParams = command.InputData;
                command.InputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                command.InputData.EnteredPassword = PASSWORD;
            }), Arg.Any<CancellationToken>());

            //Act
            WorldAccessResult result = handler.CheckAccessAsync(WORLD_NAME, null, WORLD_REALM, CancellationToken.None).GetAwaiter().GetResult();

            //Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            realmData.Received(1).SetPendingWorldCommsSecret(WORLD_REALM, PASSWORD);
            Assert.IsNotNull(shownParams);
            Assert.IsNull(shownParams!.EnteredPassword);
        }

        [Test]
        public void ClearPendingSecretWhenAccessIsAllowedWithoutPassword()
        {
            //Arrange
            SetAccessCheckResult(WorldAccessCheckResult.Allowed);

            //Act
            WorldAccessResult result = handler.CheckAccessAsync(WORLD_NAME, null, WORLD_REALM, CancellationToken.None).GetAwaiter().GetResult();

            //Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            realmData.Received(1).ClearPendingWorldCommsSecret();
            realmData.DidNotReceive().SetPendingWorldCommsSecret(Arg.Any<URLDomain>(), Arg.Any<string>());
        }

        [Test]
        public void NotStorePendingSecretWhenPopupIsCancelled()
        {
            //Arrange: the popup's default result is Cancelled, so no mvcManager setup is needed.
            SetAccessCheckResult(WorldAccessCheckResult.PasswordRequired);

            //Act
            WorldAccessResult result = handler.CheckAccessAsync(WORLD_NAME, null, WORLD_REALM, CancellationToken.None).GetAwaiter().GetResult();

            //Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
            realmData.DidNotReceive().SetPendingWorldCommsSecret(Arg.Any<URLDomain>(), Arg.Any<string>());
        }

        private void SetAccessCheckResult(WorldAccessCheckResult result)
        {
            worldPermissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                                   .Returns(UniTask.FromResult(new WorldAccessCheckContext { Result = result }));
        }
    }
}
