using DCL.ExternalUrlPrompt;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    public class RestrictedActionsAPIImplementationShould
    {
        private RestrictedActionsAPIImplementation restrictedActionsAPIImplementation;
        private IMVCManager mvcManager;
        private ISceneStateProvider sceneStateProvider;
        private IGlobalWorldActions globalWorldActions;
        private ISceneData sceneData;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            globalWorldActions = Substitute.For<IGlobalWorldActions>();
            sceneData = Substitute.For<ISceneData>();
            restrictedActionsAPIImplementation = new RestrictedActionsAPIImplementation(
                mvcManager,
                sceneStateProvider,
                globalWorldActions,
                sceneData);
        }

        [Test]
        public void OpenExternalUrl()
        {
            // Arrange
            var testUrl = "www.test.com";

            // Act
            restrictedActionsAPIImplementation.OpenExternalUrl(testUrl);

            // Assert
            mvcManager.Received(1).ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(testUrl)));
        }
    }
}
