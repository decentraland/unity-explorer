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
        private PrivateWorldAccessHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            permissionsService = Substitute.For<IWorldPermissionsService>();
            mvcManager = Substitute.For<IMVCManager>();
            handler = new PrivateWorldAccessHandler(permissionsService, mvcManager);
        }

        private static CheckWorldAccessEvent CreateEvent(string worldName, CancellationToken ct, out UniTaskCompletionSource<WorldAccessResult> resultSource)
        {
            resultSource = new UniTaskCompletionSource<WorldAccessResult>();
            return new CheckWorldAccessEvent(worldName, null, resultSource, ct);
        }

        [Test]
        public async Task Allowed_WhenWorldIsUnrestricted()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(new WorldAccessCheckContext { Result = WorldAccessCheckResult.Allowed }));
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            permissionsService.Received(1).CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CheckFailed_WhenPermissionServiceThrows()
        {
            // Arrange
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromException<WorldAccessCheckContext>(new Exception("API error")));
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);
            LogAssert.Expect(LogType.Exception, "Exception: API error");

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.CheckFailed, result);
        }

        [Test]
        public async Task ShowsPasswordPopup_WhenSharedSecret()
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            await mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(c =>
                c.InputData.Mode == PrivateWorldPopupMode.PasswordRequired && c.InputData.WorldName == WORLD_NAME), Arg.Any<CancellationToken>());
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ReturnsAllowed_WhenPasswordCorrect()
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.Allowed, result);
            permissionsService.Received(1).ValidatePasswordAsync(WORLD_NAME, "correct", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReturnsCancelled_WhenUserCancelsPassword()
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ReturnsCancelled_AfterMaxPasswordAttempts()
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(5));

            // Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
        }

        [Test]
        public async Task ShowsAccessDeniedPopup_WhenNotOnAllowList()
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            await mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<PrivateWorldPopupView, PrivateWorldPopupParams>>(c =>
                c.InputData.Mode == PrivateWorldPopupMode.AccessDenied && c.InputData.WorldName == WORLD_NAME), Arg.Any<CancellationToken>());
            Assert.AreEqual(WorldAccessResult.Denied, result);
        }

        [Test]
        public async Task ReturnsCancelled_WhenCancellationTokenFires()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            permissionsService.CheckWorldAccessAsync(WORLD_NAME, Arg.Any<CancellationToken>())
                .Returns(callInfo => UniTask.Create(async () =>
                {
                    await UniTask.Delay(5000, cancellationToken: callInfo.ArgAt<CancellationToken>(1));
                    return new WorldAccessCheckContext { Result = WorldAccessCheckResult.Allowed };
                }));
            var evt = CreateEvent(WORLD_NAME, cts.Token, out var resultSource);

            // Act
            handler.OnCheckWorldAccess(evt);
            cts.Cancel();
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.PasswordCancelled, result);
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
            var evt = CreateEvent(WORLD_NAME, CancellationToken.None, out var resultSource);
            LogAssert.Expect(LogType.Exception, "Exception: Network error");

            // Act
            handler.OnCheckWorldAccess(evt);
            var result = await resultSource.Task.AttachExternalCancellation(CancellationToken.None).Timeout(TimeSpan.FromSeconds(2));

            // Assert
            Assert.AreEqual(WorldAccessResult.CheckFailed, result);
        }
    }
}
