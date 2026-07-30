using DCL.ExplorePanel;
using DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions;
using DCL.UI;
using Decentraland.Kernel.Apis;
using MVC;
using NSubstitute;
using NUnit.Framework;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    /// <summary>
    ///     Covers how <see cref="ExplorerUiActions" /> chooses between opening the explore panel and answering
    ///     the scene that it was already open. One instance exists per scene, so it cannot have witnessed the
    ///     panel opening before it was built, and MVC skips OnViewClosed when a view's lifecycle is cancelled —
    ///     the panel state therefore has to be read from MVC at the moment of the decision.
    /// </summary>
    [TestFixture]
    public class ExplorerUiActionsShould
    {
        private IMVCManager mvcManager;
        private ExplorerUiActions explorerUiActions;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();

            // Built before any panel state is arranged, the way a scene load builds it long after the user
            // could have opened a panel.
            explorerUiActions = new ExplorerUiActions(mvcManager);
        }

        [Test]
        public void AnswerWasAlreadyOpenForAPanelOpenedBeforeTheSceneLoaded()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);

            Assert.That(explorerUiActions.OpenSection(ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));
        }

        [Test]
        public void OpenTheSectionWhileThePanelIsHidden()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            Assert.That(explorerUiActions.OpenSection(ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public void FollowThePanelStateAcrossCalls()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false, true, false);

            Assert.That(explorerUiActions.OpenSection(ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
            Assert.That(explorerUiActions.OpenSection(ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            // A cached flag would stay stuck on the middle answer; the panel closing has to be picked up.
            Assert.That(explorerUiActions.OpenSection(ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.Opened));
        }
    }
}
