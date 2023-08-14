using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using Diagnostics.ReportsHandling;
using SceneRunner.Scene;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Global.Static
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class StaticSceneLauncher : MonoBehaviour
    {
        [SerializeField] private SceneLauncher sceneLauncher;
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;

        private ISceneFacade sceneFacade;

        private StaticContainer staticContainer;

        private void Awake()
        {
            InitializationFlow(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            staticContainer?.Dispose();
        }

        public async UniTask InitializationFlow(CancellationToken ct)
        {
            try
            {
                SceneSharedContainer sceneSharedContainer;
                (staticContainer, sceneSharedContainer) = await Install(globalPluginSettingsContainer, scenePluginSettingsContainer, ct);
                sceneLauncher.Initialize(sceneSharedContainer, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // unhandled exception
                PrintGameIsDead();
                throw;
            }
        }

        public static async UniTask<(StaticContainer staticContainer, SceneSharedContainer sceneSharedContainer)> Install(
            IPluginSettingsContainer globalSettingsContainer,
            IPluginSettingsContainer sceneSettingsContainer,
            CancellationToken ct)
        {
            // First load the common global plugin
            (StaticContainer staticContainer, bool isLoaded) = await StaticContainer.Create(globalSettingsContainer, ct);

            if (!isLoaded)
                PrintGameIsDead();

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => sceneSettingsContainer.InitializePlugin(gp, ct)));

            var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

            return (staticContainer, sceneSharedContainer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrintGameIsDead()
        {
            ReportHub.LogError(ReportCategory.ENGINE, "Initialization Failed! Game is irrecoverably dead!");
        }
    }
}
