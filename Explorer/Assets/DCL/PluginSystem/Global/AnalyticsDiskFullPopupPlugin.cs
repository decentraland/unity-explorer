using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.ErrorPopup;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsDiskFullPopupPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IEventBus analyticsEventBus;
        private readonly IMVCManager mvcManager;
        private readonly CancellationTokenSource cts = new ();
        private IDisposable? subscription;

        // The event repeats with every failing flush; the popup must show once per session
        private bool popupShown;

        public AnalyticsDiskFullPopupPlugin(IEventBus analyticsEventBus, IMVCManager mvcManager)
        {
            this.analyticsEventBus = analyticsEventBus;
            this.mvcManager = mvcManager;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct)
        {
            subscription = analyticsEventBus.Subscribe<AnalyticsDiskFullDetected>(OnDiskFull);
            return UniTask.CompletedTask;
        }

        private void OnDiskFull(AnalyticsDiskFullDetected evt)
        {
            if (popupShown)
                return;

            popupShown = true;
            ShowPopupAsync(cts.Token).Forget();
        }

        private async UniTaskVoid ShowPopupAsync(CancellationToken ct)
        {
            var data = new ErrorPopupData(
                UIProperty<Sprite>.UseDefault,
                UIProperty<string>.From("Storage Full"),
                UIProperty<string>.From("Your device is running out of disk space. Free up space to keep Decentraland working correctly."));

            EnumResult<TaskError> result = await mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(data), ct)
                                                           .SuppressToResultAsync(ReportCategory.ANALYTICS);

            // A show that failed never reached the user, so the next disk-full event may retry
            if (result.Error is { State: not TaskError.Cancelled })
                popupShown = false;
        }

        public void Dispose()
        {
            subscription?.Dispose();
            cts.SafeCancelAndDispose();
        }
    }
}
