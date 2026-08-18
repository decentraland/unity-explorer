using Cysharp.Threading.Tasks;
using DCL.UI.PortableExperiences;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PortableExperiences.Tests
{
    public class PortableExperienceAuthorizationPopupControllerShould
    {
        private static readonly List<string> PERMISSIONS = new () { "USE_WEB3_API" };

        private IMVCManager mvcManager;

        [SetUp]
        public void Setup()
        {
            mvcManager = Substitute.For<IMVCManager>();
        }

        [Test]
        public async Task ReturnTrueWhenAuthorized()
        {
            // Arrange
            SetupUserChoice(true);

            // Act
            bool authorized = await PortableExperienceAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, "px", PERMISSIONS, CancellationToken.None);

            // Assert
            Assert.IsTrue(authorized);
        }

        [Test]
        public async Task ReturnFalseWhenDenied()
        {
            // Arrange
            SetupUserChoice(false);

            // Act
            bool authorized = await PortableExperienceAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, "px", PERMISSIONS, CancellationToken.None);

            // Assert
            Assert.IsFalse(authorized);
        }

        [Test]
        public async Task ReturnFalseWhenCancelledBeforeShowing()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            bool authorized = await PortableExperienceAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, "px", PERMISSIONS, cts.Token);

            // Assert
            Assert.IsFalse(authorized);
        }

        private void SetupUserChoice(bool authorize)
        {
            mvcManager.ShowAsync(
                          Arg.Do<ShowCommand<PortableExperienceAuthorizationPopupView, PortableExperienceAuthorizationPopupController.Params>>(
                              command => command.InputData.CompletionSource.TrySetResult(authorize)),
                          Arg.Any<CancellationToken>())
                      .Returns(UniTask.CompletedTask);
        }
    }
}
