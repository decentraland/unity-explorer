using Cysharp.Threading.Tasks;
using DCL.PrivateWorlds.UI;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.PrivateWorlds.Tests.EditMode
{
    [TestFixture]
    public class PrivateWorldAccessHandlerShould
    {
        private const string WORLD_NAME = "yourname.dcl.eth";
        private IWorldPermissionsService permissionsService = null!;
        private IMVCManager mvcManager = null!;
        private IWorldCommsSecret worldCommsSecret = null!;
        private PrivateWorldAccessHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            permissionsService = Substitute.For<IWorldPermissionsService>();
            mvcManager = Substitute.For<IMVCManager>();
            worldCommsSecret = new WorldCommsSecret();
            handler = new PrivateWorldAccessHandler(permissionsService, mvcManager, worldCommsSecret);
        }

        [Test]
        public async Task AllowedWhenWorldIsUnrestricted()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext { Result = WorldAccessCheckResult.Allowed }));

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            permissionsService.Received(1).CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CheckFailedWhenPermissionServiceThrows()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromException<WorldAccessCheckContext>(new Exception("API error")));

            // ReportHub.LogException emits two LogType.Exception entries (category prefix + exception);
            // suppress them so the test runner does not treat them as failures.
            LogAssert.ignoreFailingMessages = true;

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(WorldAccessResult.CheckFailed, result);
        }

        [Test]
        public async Task ShowsPasswordPopupWhenSharedSecret()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.PasswordRequired,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    cmd.InputData.Result = PrivateWorldPopupResult.Cancelled;
                    return UniTask.CompletedTask;
                });

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(c =>
                c.InputData.Mode == PrivateWorldPopupMode.PasswordRequired && c.InputData.WorldName == WORLD_NAME), Arg.Any<CancellationToken>());
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ReturnsAllowedWhenPasswordCorrect()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.PasswordRequired,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            permissionsService.ValidatePasswordAsync(WORLD_NAME, "correct", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(true));
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    cmd.InputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                    cmd.InputData.EnteredPassword = "correct";
                    return UniTask.CompletedTask;
                });

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            permissionsService.Received(1).ValidatePasswordAsync(WORLD_NAME, "correct", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReturnsCancelledWhenUserCancelsPassword()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.PasswordRequired,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    cmd.InputData.Result = PrivateWorldPopupResult.Cancelled;
                    return UniTask.CompletedTask;
                });

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ReturnsCancelledAfterMaxPasswordAttempts()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.PasswordRequired,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            permissionsService.ValidatePasswordAsync(WORLD_NAME, "wrong", Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(false));
            var callCount = 0;
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    callCount++;
                    if (callCount >= 3)
                        cmd.InputData.Result = PrivateWorldPopupResult.Cancelled;
                    else
                    {
                        cmd.InputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                        cmd.InputData.EnteredPassword = "wrong";
                    }
                    return UniTask.CompletedTask;
                });

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ShowsAccessDeniedPopupWhenNotOnAllowList()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.AccessDenied,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    cmd.InputData.Result = PrivateWorldPopupResult.Cancelled;
                    return UniTask.CompletedTask;
                });

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(c =>
                c.InputData.Mode == PrivateWorldPopupMode.AccessDenied && c.InputData.WorldName == WORLD_NAME), Arg.Any<CancellationToken>());
            Assert.AreEqual(WorldAccessResult.Denied, result);
        }

        [Test]
        public async Task ThrowsOperationCanceledWhenCancellationTokenFires()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(callInfo => UniTask.Create(async () =>
                {
                    await UniTask.Delay(5000, cancellationToken: callInfo.ArgAt<CancellationToken>(1));
                    return new WorldAccessCheckContext { Result = WorldAccessCheckResult.Allowed };
                }));

            // Act
            cts.Cancel();

            // Assert — cancellation propagates to the caller (TaskCanceledException extends OperationCanceledException)
            try
            {
                await handler.CheckAccessAsync(WORLD_NAME, null, cts.Token);
                Assert.Fail("Expected OperationCanceledException was not thrown");
            }
            catch (OperationCanceledException) { /* expected */ }
        }

        [Test]
        public async Task HandlesExceptionDuringPasswordValidation()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext
                {
                    Result = WorldAccessCheckResult.PasswordRequired,
                    AccessInfo = new WorldAccessInfo { OwnerAddress = "0xOwner" }
                }));
            permissionsService.ValidatePasswordAsync(WORLD_NAME, "test", Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromException<bool>(new Exception("Network error")));
            mvcManager.ShowAsync(Arg.Any<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var cmd = callInfo.Arg<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>();
                    cmd.InputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                    cmd.InputData.EnteredPassword = "test";
                    return UniTask.CompletedTask;
                });

            // ReportHub.LogException emits two LogType.Exception entries (category prefix + exception);
            // suppress them so the test runner does not treat them as failures.
            LogAssert.ignoreFailingMessages = true;

            // Act
            var result = await handler.CheckAccessAsync(WORLD_NAME, null, CancellationToken.None);

            // Assert
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(WorldAccessResult.CheckFailed, result);
        }
    }
}
