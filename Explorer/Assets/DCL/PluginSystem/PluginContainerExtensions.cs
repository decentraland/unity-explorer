using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

namespace DCL.PluginSystem
{
    public static class PluginContainerExtensions
    {
        public static async UniTask<(TPlugin plugin, bool success)> InitializePluginAsync<TPlugin>(this IPluginSettingsContainer pluginSettingsContainer, TPlugin plugin, CancellationToken ct) where TPlugin: class, IDCLPlugin
        {
            try { await plugin.Initialize(pluginSettingsContainer, ct); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Error initializing plugin {plugin.GetType().Name}: {e}");
                return (plugin, false);
            }

            return (plugin, true);
        }

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
            Func<TContainer, UniTask> createDependencies)
            where TContainer: DCLContainer<TSettings>
            where TSettings: IDCLPluginSettings, new()
        {
            (_, bool result) = await pluginSettingsContainer.InitializePluginAsync(container, ct);

            if (!result)
                return (null, false);

            await createDependencies(container);

            return (container, true);
        }
    }
}
