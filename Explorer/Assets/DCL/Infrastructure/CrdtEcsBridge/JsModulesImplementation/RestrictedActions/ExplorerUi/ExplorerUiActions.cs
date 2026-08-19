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
using System;
using System.Collections.Generic;

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
        private readonly Queue<ExplorerUiEvent> events;

        public ExplorerUiActions(IMVCManager mvcManager, Queue<ExplorerUiEvent> events)
        {
            this.mvcManager = mvcManager;
            this.events = events;
        }

        public OpenExplorerUiResult OpenSection(ExplorerUi ui, ExploreSections section)
        {
            // Communities availability depends on the user identity (feature flag + wallets allowlist),
            // so it cannot be gated through FeaturesRegistry like the other sections.
            if (section == ExploreSections.Communities && !CommunitiesFeatureAccess.Instance.IsUserAllowedCached())
            {
                ReportHub.Log(ReportCategory.RESTRICTED_ACTIONS, "OpenSection: the Communities feature is not available for this user");
                return OpenExplorerUiResult.RejectedFeatureDisabled;
            }

            if (mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>())
                return OpenExplorerUiResult.WasAlreadyOpen;

            OpenSectionAsync(ui, section).Forget();
            return OpenExplorerUiResult.Opened;
        }

        private async UniTask OpenSectionAsync(ExplorerUi ui, ExploreSections section)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // The answer given to the scene was decided on its JS thread; by now the user may have opened
                // the panel themselves, and ShowAsync does nothing for a controller that is not hidden.
                if (mvcManager.IsShowing<ExplorePanelView, ExplorePanelParameter>())
                    return;

                // ShowAsync resolves when the panel closes, so the pair brackets its whole life cycle. The
                // opened event goes out before the await because there is no later moment that still means
                // "shown".
                events.Enqueue(new ExplorerUiEvent(ui, ExplorerUiEventKind.Opened));

                try { await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section))); }
                finally { events.Enqueue(new ExplorerUiEvent(ui, ExplorerUiEventKind.Closed)); }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.RESTRICTED_ACTIONS); }
        }
    }
}
