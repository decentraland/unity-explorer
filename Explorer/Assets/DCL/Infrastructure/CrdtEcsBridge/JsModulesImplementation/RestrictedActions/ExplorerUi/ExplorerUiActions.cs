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

            // The answer is decided after the hop, because the slot can be taken while the call travels.
            // MVC reports a panel as showing only once the view's life cycle starts, which is later than the
            // call to ShowAsync, so showPending covers the request that is still landing.
            if (showPending || mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>())
                return OpenExplorerUiResult.WasAlreadyOpen;

            showPending = true;

            Enqueue(ui, ExplorerUiEventKind.Opened, requestId);

            ShowUntilClosedAsync(ui, section, requestId).Forget();
            return OpenExplorerUiResult.Opened;
        }

        // ShowAsync resolves when the panel closes, so this runs detached instead of being awaited.
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

        private void Enqueue(ExplorerUi ui, ExplorerUiEventKind kind, uint requestId) =>
            events.Enqueue(new ExplorerUiEvent(ui, kind, requestId, sceneStateProvider.TickNumber));
    }
}
