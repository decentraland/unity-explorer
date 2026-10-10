using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.UI;
using MVC;
using System;
using System.Threading;

namespace DCL.ExplorePanel
{
    public static class BackpackDeepLinkOpener
    {
        public static async UniTaskVoid OpenBackpackWhenLandedAsync(IMVCManager mvcManager, ILoadingStatus loadingStatus, CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);
                await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack)), ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }
    }
}
