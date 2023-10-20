using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using Diagnostics.ReportsHandling;
using ECS.Unity.GLTFContainer.Asset.Cache;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     An entry point to install and resolve all dependencies
    /// </summary>
    public class DynamicSceneLoader : MonoBehaviour
    {
        public bool clearCache;
        [Header("Settings")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;
        [Space]
        [SerializeField] private UIDocument uiToolkitRoot;
        [SerializeField] private Vector2Int StartPosition;
        [SerializeField] private int SceneLoadRadius = 4;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<int2> StaticLoadPositions;
        [SerializeField] private RealmLauncher realmLauncher;
        [SerializeField] private string[] realms;

        private StaticContainer staticContainer;
        private DynamicWorldContainer dynamicWorldContainer;

        private GlobalWorld globalWorld;

        private void Awake()
        {
            realmLauncher.Initialize(realms);
            InitializationFlow(destroyCancellationToken).Forget();
        }

        private void Update()
        {
            if (clearCache) { GltfContainerAssetsCache.clearCache = clearCache; }
        }

        private void OnDestroy()
        {
            async UniTaskVoid DisposeAsync()
            {
                if (dynamicWorldContainer != null)
                {
                    foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                        plugin.Dispose();
                }

                if (globalWorld != null)
                    await dynamicWorldContainer.RealmController.DisposeGlobalWorld(globalWorld).SuppressCancellationThrow();

                await UniTask.SwitchToMainThread();

                staticContainer?.Dispose();
            }

            realmLauncher.OnRealmSelected = null;
            DisposeAsync().Forget();
        }

        private async UniTask InitializationFlow(CancellationToken ct)
        {
            try
            {
                // First load the common global plugin
                bool isLoaded;
                (staticContainer, isLoaded) = await StaticContainer.Create(globalPluginSettingsContainer, ct);

                if (!isLoaded)
                {
                    PrintGameIsDead();
                    return;
                }

                var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

                dynamicWorldContainer = DynamicWorldContainer.Create(
                    in staticContainer,
                    uiToolkitRoot,
                    StaticLoadPositions,
                    SceneLoadRadius);

                // Initialize global plugins
                var anyFailure = false;

                void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
                {
                    if (!result.success)
                        anyFailure = true;
                }

                await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePlugin(gp, ct).ContinueWith(OnPluginInitialized)));
                await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePlugin(gp, ct).ContinueWith(OnPluginInitialized)));

                if (anyFailure)
                {
                    PrintGameIsDead();
                    return;
                }

                globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory, dynamicWorldContainer.EmptyScenesWorldFactory, staticContainer.CharacterObject);

                void SetRealm(string selectedRealm)
                {
                    ChangeRealm(staticContainer, destroyCancellationToken, selectedRealm).Forget();
                }

                realmLauncher.OnRealmSelected += SetRealm;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                PrintGameIsDead();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrintGameIsDead()
        {
            ReportHub.LogError(ReportCategory.ENGINE, "Initialization Failed! Game is irrecoverably dead!");
        }

        private async UniTask ChangeRealm(StaticContainer globalContainer, CancellationToken ct, string selectedRealm)
        {
            if (globalWorld != null)
                await dynamicWorldContainer.RealmController.UnloadCurrentRealm(globalWorld);

            await UniTask.SwitchToMainThread();

            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            characterPos.y = 1f;

            globalContainer.CharacterObject.Controller.Move(characterPos - globalContainer.CharacterObject.Transform.position);

            await dynamicWorldContainer.RealmController.SetRealm(globalWorld, URLDomain.FromString(selectedRealm), ct);
        }
    }
}
