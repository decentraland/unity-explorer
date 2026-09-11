using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.ExplorePanel;
using DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions;
using DCL.UI;
using Decentraland.Kernel.Apis;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    /// <summary>
    ///     Covers how <see cref="ExplorerUiActions" /> chooses between opening the explore panel and answering
    ///     the scene that it was already open, and the life cycle events it reports back for the requests it
    ///     did accept. One instance exists per scene, so it cannot have witnessed the panel opening before it
    ///     was built, and MVC skips OnViewClosed when a view's lifecycle is cancelled — the panel state
    ///     therefore has to be read from MVC at the moment of the decision.
    /// </summary>
    [TestFixture]
    public class ExplorerUiActionsShould
    {
        private IMVCManager mvcManager = null!;
        private Queue<ExplorerUiEvent> events = null!;
        private ExplorerUiActions explorerUiActions = null!;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            events = new Queue<ExplorerUiEvent>();

            // Built before any panel state is arranged, the way a scene load builds it long after the user
            // could have opened a panel.
            explorerUiActions = new ExplorerUiActions(mvcManager, events);
        }

        [Test]
        public void AnswerWasAlreadyOpenForAPanelOpenedBeforeTheSceneLoaded()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);

            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void OpenTheSectionWhileThePanelIsHidden()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void FollowThePanelStateAcrossCalls()
        {
            // Re-arranged before every call rather than queued up as a sequence: each accepted request asks
            // MVC twice, once per thread it runs on, so the number of calls is not the test's business.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);
            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));

            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);
            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuPlaces, ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            // A cached flag would stay stuck on the middle answer; the panel closing has to be picked up.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);
            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuPlaces, ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public void ReportTheOpenedAndClosedPairOfItsOwnRequest()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            explorerUiActions.OpenSection(ExplorerUi.EuBackpack, ExploreSections.Backpack);

            // The events carry the protocol value the scene asked with, not the section MVC was driven by:
            // the two enums are unrelated and only this direction of the mapping exists.
            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Opened),
                new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Closed),
            }));
        }

        [Test]
        public void ReportNothingWhenTheUserOpensThePanelFirst()
        {
            // False on the scene's JS thread, true once the request reaches the main thread. ShowAsync would
            // silently do nothing from here on, so a reported pair would describe a panel this scene never
            // opened and would never see closed either.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false, true);

            Assert.That(explorerUiActions.OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));

            Assert.That(events, Is.Empty);
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ReportClosedWhenTheShowDoesNotEndNormally()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromCanceled());

            explorerUiActions.OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap);

            // Every way the show can end leaves the panel down, so the pair has to close on all of them: a
            // reported open with no reported close is a scene waiting forever.
            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened),
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Closed),
            }));
        }
    }
}
