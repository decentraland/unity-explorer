using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Time;
using DCL.UI.ErrorPopup;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class EnsureClockSyncPlugin : IDCLGlobalPlugin<EnsureClockSyncPlugin.Settings>
    {
        private readonly IRealmNavigator realmNavigator;
        private readonly IMVCManager mvcManager;
        private readonly EnsureClockSync ensureClockSync;
        private CancellationTokenSource? lifeCycleCts;

        public EnsureClockSyncPlugin(IRealmNavigator realmNavigator,
            IMVCManager mvcManager,
            RealmClock realmClock,
            IWebRequestController webRequestController)
        {
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;
            ensureClockSync = new EnsureClockSync(realmClock, webRequestController, ShowClockDesyncPopupAsync);
        }

        public void Dispose()
        {
            realmNavigator.NavigationExecuted -= CheckForClockSync;
        }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            realmNavigator.NavigationExecuted += CheckForClockSync;
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        private void CheckForClockSync(Vector2Int obj)
        {
            lifeCycleCts = lifeCycleCts.SafeRestart();
            ensureClockSync.ExecuteAsync(lifeCycleCts.Token).Forget();
        }

        private async UniTask<EnsureClockSync.Result> ShowClockDesyncPopupAsync(CancellationToken ct)
        {
            var input = new ErrorPopupWithRetryController.Input(
                title: "Time sync needed",
                description: "Your clock may be out of sync. Turn on “Set time automatically” in Date & Time settings and try again.",
                retryText: "Retry",
                iconType: ErrorPopupWithRetryController.IconType.CLOCK);

            await mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(input), ct);

            switch (input.SelectedOption)
            {
                case ErrorPopupWithRetryController.Result.EXIT:
                    // The error popup will automatically request application exit
                    return EnsureClockSync.Result.CONTINUE;
                case ErrorPopupWithRetryController.Result.RESTART:
                    return EnsureClockSync.Result.RESTART;
            }

            return EnsureClockSync.Result.CONTINUE;
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
        }
    }
}
