using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
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

        private static void Track(IAnalyticsController analytics, string pluginName, string status, string exception) =>
            analytics.Track(AnalyticsEvents.General.PLUGINS_INIT, new JObject { { "plugin", pluginName }, { "status", status }, { "exception", exception } });

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
            UnityEngine.Debug.Log($"[JM-DEBUG] InitializeContainerAsync: loading settings for {typeof(TContainer).Name}");
            (_, bool result) = await pluginSettingsContainer.InitializePluginAsync(container, ct);
            UnityEngine.Debug.Log($"[JM-DEBUG] InitializeContainerAsync: settings loaded for {typeof(TContainer).Name}, result={result}");

            if (!result)
                return (null, false);

            if (createDependencies != null)
            {
                UnityEngine.Debug.Log($"[JM-DEBUG] InitializeContainerAsync: running createDependencies for {typeof(TContainer).Name}");
                await createDependencies(container);
                UnityEngine.Debug.Log($"[JM-DEBUG] InitializeContainerAsync: createDependencies done for {typeof(TContainer).Name}");
            }

            return (container, true);
        }
    }
}
