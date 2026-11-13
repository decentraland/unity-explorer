using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.ExplorePanel;
using DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.UI;
using Decentraland.Kernel.Apis;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

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
        private const uint TICK = 21;
        private const string ITEM_URN = "urn:decentraland:matic:collections-v2:0x2222222222222222222222222222222222222222:3";

        private IMVCManager mvcManager = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private Queue<ExplorerUiEvent> events = null!;
        private Queue<ExplorerItemPurchaseEvent> itemPurchaseEvents = null!;
        private SceneItemPurchaseFlow itemPurchaseFlow = null!;
        private ExplorerUiActions explorerUiActions = null!;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(TICK);
            events = new Queue<ExplorerUiEvent>();
            itemPurchaseEvents = new Queue<ExplorerItemPurchaseEvent>();

            // Only the virtual members are substituted: the slot (TryReserve and Release) is the real one,
            // which is why the flow is given the MVC manager.
            itemPurchaseFlow = Substitute.For<SceneItemPurchaseFlow>(mvcManager, null, null, null);
            itemPurchaseFlow.IsAvailable.Returns(true);
            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(ExplorerItemPurchaseStatus.Purchased));

            // Built before any panel state is arranged, the way a scene load builds it long after the user
            // could have opened a panel.
            explorerUiActions = new ExplorerUiActions(mvcManager, sceneStateProvider, events, itemPurchaseEvents, itemPurchaseFlow);
        }

        [Test]
        public async Task AnswerWasAlreadyOpenForAPanelThisSceneDidNotOpen()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);

            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            Assert.That(events, Is.Empty);
            _ = mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OpenTheSectionWhileThePanelIsHidden()
        {
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
            _ = mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseASecondRequestWhileTheFirstOneIsStillPuttingThePanelUp()
        {
            // MVC reports the panel as hidden throughout: that is the window two calls back to back land in.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(false);

            mvcManager.ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>())
                      .Returns(new UniTaskCompletionSource().Task);

            Assert.That(await OpenAsync(ExplorerUi.EuEvents, ExploreSections.Events), Is.EqualTo(OpenExplorerUiResult.Opened));
            Assert.That(await OpenAsync(ExplorerUi.EuEvents, ExploreSections.Events), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            _ = mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(), Arg.Any<CancellationToken>());

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

            Assert.That(await OpenAsync(ExplorerUi.EuMap, ExploreSections.Navmap), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public async Task RefuseAnItemPurchaseWhileBuyingWithCreditsIsUnavailable()
        {
            itemPurchaseFlow.IsAvailable.Returns(false);

            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.RejectedFeatureDisabled));

            Assert.That(events, Is.Empty);
            Assert.That(itemPurchaseEvents, Is.Empty);
            _ = itemPurchaseFlow.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task AnswerWasAlreadyOpenForAPurchaseModalThisSceneDidNotOpen()
        {
            mvcManager.IsShowing<CreditPurchaseModalView, CreditPurchaseModalControllerParams>().Returns(true);

            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            Assert.That(events, Is.Empty);
            _ = itemPurchaseFlow.DidNotReceive().RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseASecondItemPurchaseWhileTheFirstOneIsStillRunning()
        {
            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(new UniTaskCompletionSource<ExplorerItemPurchaseStatus>().Task);

            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.Opened));
            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));

            _ = itemPurchaseFlow.Received(1).RunAsync(ITEM_URN, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OpenAnItemPurchaseWhileTheExplorePanelIsOpen()
        {
            // The purchase modal does not share the explore panel's slot.
            mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>().Returns(true);

            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public async Task ReportThePurchaseOutcomeBetweenTheOpenedAndClosedEvents()
        {
            var purchase = new UniTaskCompletionSource<ExplorerItemPurchaseStatus>();
            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(purchase.Task);

            sceneStateProvider.TickNumber.Returns((uint)5);
            await OpenItemPurchaseAsync(31);

            // Only Opened is out while the purchase is still running.
            Assert.That(events, Is.EqualTo(new[] { new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Opened, 31, 5) }));
            Assert.That(itemPurchaseEvents, Is.Empty);

            sceneStateProvider.TickNumber.Returns((uint)9);
            purchase.TrySetResult(ExplorerItemPurchaseStatus.Failed);

            Assert.That(itemPurchaseEvents, Is.EqualTo(new[] { new ExplorerItemPurchaseEvent(ITEM_URN, ExplorerItemPurchaseStatus.Failed, 31, 9) }));

            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Opened, 31, 5),
                new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Closed, 31, 9),
            }));
        }

        [Test]
        public async Task AcceptAnItemPurchaseAgainOnceTheFirstOneEnded()
        {
            await OpenItemPurchaseAsync();

            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public async Task RefuseAnItemPurchaseWhileAnotherSceneIsStillRunningOne()
        {
            // Arrange: the flow is client wide, so both scenes are handed the same one.
            var otherSceneEvents = new Queue<ExplorerUiEvent>();
            var otherSceneActions = new ExplorerUiActions(mvcManager, sceneStateProvider, otherSceneEvents, new Queue<ExplorerItemPurchaseEvent>(), itemPurchaseFlow);

            var purchase = new UniTaskCompletionSource<ExplorerItemPurchaseStatus>();
            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(purchase.Task);

            // Act
            OpenExplorerUiResult first = await OpenItemPurchaseAsync();
            OpenExplorerUiResult second = await otherSceneActions.OpenItemPurchaseAsync(ITEM_URN, 0, CancellationToken.None);

            // Assert
            Assert.That(first, Is.EqualTo(OpenExplorerUiResult.Opened));
            Assert.That(second, Is.EqualTo(OpenExplorerUiResult.WasAlreadyOpen));
            Assert.That(otherSceneEvents, Is.Empty);
            _ = itemPurchaseFlow.Received(1).RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

            // The other scene is accepted once the first purchase is over.
            purchase.TrySetResult(ExplorerItemPurchaseStatus.Dismissed);

            Assert.That(await otherSceneActions.OpenItemPurchaseAsync(ITEM_URN, 0, CancellationToken.None), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        [Test]
        public async Task ReportFailedAndClosedAndFreeTheSlotWhenThePurchaseFlowThrows()
        {
            // Arrange
            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromException<ExplorerItemPurchaseStatus>(new InvalidOperationException("flow broke")));

            LogAssert.Expect(LogType.Exception, new Regex("flow broke"));

            // Act
            await OpenItemPurchaseAsync(7);

            // Assert
            Assert.That(itemPurchaseEvents, Is.EqualTo(new[] { new ExplorerItemPurchaseEvent(ITEM_URN, ExplorerItemPurchaseStatus.Failed, 7, TICK) }));

            Assert.That(events, Is.EqualTo(new[]
            {
                new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Opened, 7, TICK),
                new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Closed, 7, TICK),
            }));

            itemPurchaseFlow.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(ExplorerItemPurchaseStatus.Purchased));
            Assert.That(await OpenItemPurchaseAsync(), Is.EqualTo(OpenExplorerUiResult.Opened));
        }

        private async Task<OpenExplorerUiResult> OpenItemPurchaseAsync(uint requestId = 0) =>
            await explorerUiActions.OpenItemPurchaseAsync(ITEM_URN, requestId, CancellationToken.None);

        private async Task<OpenExplorerUiResult> OpenAsync(ExplorerUi ui, ExploreSections section, uint requestId = 0) =>
            await explorerUiActions.OpenSectionAsync(ui, section, requestId, CancellationToken.None);
    }
}
