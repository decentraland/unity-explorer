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
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    /// <summary>
    ///     Covers how <see cref="ExplorerUiActions" /> chooses between opening the explore panel and answering
    ///     the scene that it was already open, and the life cycle events it reports back for the requests it
    ///     did accept. One instance exists per scene, so it cannot have witnessed the panel opening before it
    ///     was built, and MVC skips OnViewClosed when a view's lifecycle is cancelled — a panel the scene did
    ///     not open therefore has to be read from MVC at the moment of the decision, while a panel this scene
    ///     is itself putting up is one MVC does not report yet.
    /// </summary>
    [TestFixture]
    public class ExplorerUiActionsShould
    {
        private const uint TICK = 21;

        private IMVCManager mvcManager = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private Queue<ExplorerUiEvent> events = null!;
        private ExplorerUiActions explorerUiActions = null!;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(TICK);
            events = new Queue<ExplorerUiEvent>();

            // Built before any panel state is arranged, the way a scene load builds it long after the user
            // could have opened a panel.
            explorerUiActions = new ExplorerUiActions(mvcManager, sceneStateProvider, events);
        }

        [Test]
        public async Task AnswerWasAlreadyOpenForAPanelThisSceneDidNotOpen()
        {
            // Covers both a panel the user opened before the scene loaded and one they opened while the
            // request was travelling: the answer is decided at a single point, after the thread hop.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);

            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            Assert.That(events, Is.Empty);
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OpenTheSectionWhileThePanelIsHidden()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseASecondRequestWhileTheFirstOneIsStillPuttingThePanelUp()
        {
            // MVC reports the panel as hidden throughout: it only flips once the view's life cycle starts,
            // which is exactly the window a scene firing two calls back to back lands in.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(new UniTaskCompletionSource().Task);

            Assert.That(await OpenAsync(ExplorerUi.EuEvents, ExploreSections.Events), Is.EqualTo(OpenExplorerUiResult.Opened));
            Assert.That(await OpenAsync(ExplorerUi.EuEvents, ExploreSections.Events), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            // One accepted request means one panel and one life cycle: a refused request reports nothing.
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());

            Assert.That(events, Is.EqualTo(new[] { new ExplorerUiEvent(ExplorerUi.EuEvents, ExplorerUiEventKind.Opened, 0, TICK) }));
        }

        [Test]
        public async Task FollowThePanelStateAcrossCalls()
        {
            // Re-arranged before every call rather than queued up as a sequence: the substituted show
            // resolves at once, so each accepted request has already let its panel go by the next call.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);
            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));

            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);
            Assert.That(await OpenAsync(ExplorerUi.EuPlaces, ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            // A cached flag would stay stuck on the middle answer; the panel closing has to be picked up.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);
            Assert.That(await OpenAsync(ExplorerUi.EuPlaces, ExploreSections.Places), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public async Task ReportTheOpenedAndClosedPairOfItsOwnRequest()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            await OpenAsync(ExplorerUi.EuBackpack, ExploreSections.Backpack);

            // The events carry the protocol value the scene asked with, not the section MVC was driven by:
            // the two enums are unrelated and only this direction of the mapping exists.
            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Opened, 0, TICK),
                new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Closed, 0, TICK),
            }));
        }

        [Test]
        public async Task ReportClosedWhenTheShowDoesNotEndNormally()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromCanceled());

            await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap);

            // Every way the show can end leaves the panel down, so the pair has to close on all of them: a
            // reported open with no reported close is a scene waiting forever.
            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened, 0, TICK),
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Closed, 0, TICK),
            }));
        }

        [Test]
        public async Task EchoTheRequestIdOnBothEventsOfTheCallThatCarriedIt()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            await OpenAsync(ExplorerUi.EuPlaces, ExploreSections.Places, 88);

            // Without the echo on both ends a scene waiting on the close cannot tell which of its calls
            // the panel it just lost belonged to.
            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuPlaces, ExplorerUiEventKind.Opened, 88, TICK),
                new ExplorerUiEvent(ExplorerUi.EuPlaces, ExplorerUiEventKind.Closed, 88, TICK),
            }));
        }

        [Test]
        public async Task StampEachEventWithTheTickItHappenedOn()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            var show = new UniTaskCompletionSource();

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(show.Task);

            sceneStateProvider.TickNumber.Returns((uint)5);
            await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap, 3);

            // The panel stays up across several ticks, so the close cannot share the open's stamp.
            sceneStateProvider.TickNumber.Returns((uint)9);
            show.TrySetResult();

            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened, 3, 5),
                new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Closed, 3, 9),
            }));
        }

        [Test]
        public async Task AcceptAgainAfterAFailedShowReleasedThePanel()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromCanceled());

            await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap);

            // A show that never reached the screen must not leave the scene locked out of the panel.
            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        private async Task<OpenExplorerUiResult> OpenAsync(ExplorerUi ui, ExploreSections section, uint requestId = 0) =>
            await explorerUiActions.OpenSectionAsync(ui, section, requestId, CancellationToken.None);
    }
}
