using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.ExplorePanel;
using DCL.UI;
using Decentraland.Kernel.Apis;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions
{
    /// <summary>
    ///     Implementation of <see cref="IExplorerUiActions" />. The sibling asmref compiles it into
    ///     DCL.Social (not into SceneRuntime like the rest of this folder) because it references
    ///     <see cref="ExplorePanelController" />, and DCL.Social already depends on SceneRuntime.
    /// </summary>
    public class ExplorerUiActions : IExplorerUiActions
    {
        private readonly IMVCManager mvcManager;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly Queue<ExplorerUiEvent> events;

        /// <summary>
        ///     True from the moment a request of this scene hands the panel to MVC until that panel closes.
        ///     MVC reports the panel as showing only once the view's life cycle has begun, which is later
        ///     than the call to <c>ShowAsync</c>, so it cannot answer for a request that is still landing.
        /// </summary>
        private bool showPending;

        public ExplorerUiActions(IMVCManager mvcManager, ISceneStateProvider sceneStateProvider, Queue<ExplorerUiEvent> events)
        {
            this.mvcManager = mvcManager;
            this.sceneStateProvider = sceneStateProvider;
            this.events = events;
        }

        public async UniTask<OpenExplorerUiResult> OpenSectionAsync(ExplorerUi ui, ExploreSections section, uint requestId, CancellationToken ct)
        {
            // Communities availability depends on the user identity (feature flag + wallets allowlist),
            // so it cannot be gated through FeaturesRegistry like the other sections.
            if (section == ExploreSections.Communities && !CommunitiesFeatureAccess.Instance.IsUserAllowedCached())
            {
                ReportHub.Log(ReportCategory.RESTRICTED_ACTIONS, "OpenSection: the Communities feature is not available for this user");
                return OpenExplorerUiResult.RejectedFeatureDisabled;
            }

            await UniTask.SwitchToMainThread(ct);

            // Deciding the answer here, rather than on the scene's thread, is the whole point of the hop:
            // the slot can be taken by the user or by this scene's previous request while the call travels.
            if (showPending || mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>())
                return OpenExplorerUiResult.WasAlreadyOpen;

            showPending = true;

            // Queued before the answer leaves, so an Opened verdict always has its life cycle behind it.
            Enqueue(ui, ExplorerUiEventKind.Opened, requestId);

            ShowUntilClosedAsync(ui, section, requestId).Forget();
            return OpenExplorerUiResult.Opened;
        }

        /// <summary>
        ///     <c>ShowAsync</c> resolves when the panel closes, so it is left running rather than awaited by
        ///     the request: the answer owes the scene nothing beyond the open.
        /// </summary>
        private async UniTask ShowUntilClosedAsync(ExplorerUi ui, ExploreSections section, uint requestId)
        {
            try
            {
                try { await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section))); }
                finally
                {
                    showPending = false;
                    Enqueue(ui, ExplorerUiEventKind.Closed, requestId);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.RESTRICTED_ACTIONS); }
        }

        // The tick is read here rather than where the queue is drained: a scene is told when the event
        // happened, and draining can be a tick or more later.
        private void Enqueue(ExplorerUi ui, ExplorerUiEventKind kind, uint requestId) =>
            events.Enqueue(new ExplorerUiEvent(ui, kind, requestId, sceneStateProvider.TickNumber));
    }
}
