using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Serialization;
using System;
using System.Threading;

namespace DCL.PluginSystem
{
    public static class PluginContainerExtensions
    {
        public static async UniTask<(TPlugin plugin, bool success)> InitializePluginAsync<TPlugin>(
            this IPluginSettingsContainer pluginSettingsContainer,
            TPlugin plugin,
            CancellationToken ct
        ) where TPlugin: class, IDCLPlugin
        {
            try { await plugin.Initialize(pluginSettingsContainer, ct); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Error initializing plugin {plugin.GetType().Name}: {e}");
                return (plugin, false);
            }

            return (plugin, true);
        }

        public static async UniTask<(TPlugin plugin, bool success)> InitializePluginWithAnalyticsAsync<TPlugin>(
            this IPluginSettingsContainer pluginSettingsContainer,
            TPlugin plugin,
            IAnalyticsController analytics,
            CancellationToken ct
        ) where TPlugin: class, IDCLPlugin
        {
            string pluginName = plugin.GetType().Name;

            Track(analytics, pluginName, "started", string.Empty);
            try
            {
                await plugin.Initialize(pluginSettingsContainer, ct);
                Track(analytics, pluginName, "ended", string.Empty);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Error initializing plugin {pluginName}: {e}");
                Track(analytics, pluginName, "failed", e.ToString());
                return (plugin, false);
            }

            return (plugin, true);
        }

        private static void Track(IAnalyticsController analytics, string pluginName, string status, string exception) => analytics.Track(AnalyticsEvents.General.PLUGINS_INIT, new JsonObject { { "plugin", pluginName }, { "status", status }, { "exception", exception } });

        public static UniTask<TPlugin> ThrowOnFail<TPlugin>(this UniTask<(TPlugin? plugin, bool success)> parentTask) where TPlugin: class, IDCLPlugin
        {
            return parentTask.ContinueWith(t =>
            {
                if (!t.success)
                    throw new PluginNotInitializedException(typeof(TPlugin));

                return t.plugin!;
            });
        }

        public static async UniTask<(TContainer? container, bool success)> InitializeContainerAsync<TContainer, TSettings>(
            this TContainer container,
            IPluginSettingsContainer pluginSettingsContainer,
            CancellationToken ct,
            Func<TContainer, UniTask>? createDependencies = null)
            where TContainer: DCLContainer<TSettings>
            where TSettings: IDCLPluginSettings, new()
        {
            (_, bool result) = await pluginSettingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            if (createDependencies != null)
                await createDependencies(container);

            return (container, true);
        }
    }
}
