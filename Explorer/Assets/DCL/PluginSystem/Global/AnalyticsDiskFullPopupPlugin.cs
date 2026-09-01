using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.UI.ErrorPopup;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Shows a warning popup when the analytics persistent queue reports the disk is full
    ///     (raised as <see cref="AnalyticsDiskFullDetected" /> by RustSegmentAnalyticsService),
    ///     so the user learns their machine ran out of space instead of failing silently.
    /// </summary>
    public class AnalyticsDiskFullPopupPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IEventBus analyticsEventBus;
        private readonly IMVCManager mvcManager;
        private readonly CancellationTokenSource cts = new ();
        private IDisposable? subscription;

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
            var data = new ErrorPopupData(
                UIProperty<Sprite>.UseDefault,
                UIProperty<string>.From("Storage Full"),
                UIProperty<string>.From("Your device is running out of disk space. Free up space to keep Decentraland working correctly."));

            mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(data), cts.Token).Forget();
        }

        public void Dispose()
        {
            subscription?.Dispose();
            cts.Cancel();
            cts.Dispose();
        }
    }
}
