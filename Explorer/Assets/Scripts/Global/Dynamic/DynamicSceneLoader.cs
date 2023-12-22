using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3Authentication;
using System;
using System.Collections.Generic;
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
        [Header("Settings")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer;
        [Space]
        [SerializeField] private UIDocument uiToolkitRoot;
        [SerializeField] private UIDocument debugUiRoot;
        [SerializeField] private Vector2Int StartPosition;
        [SerializeField] [Obsolete] private int SceneLoadRadius = 4;

        // If it's 0, it will load every parcel in the range
        [SerializeField] private List<int2> StaticLoadPositions;
        [SerializeField] private RealmLauncher realmLauncher;
        [SerializeField] private string[] realms;
        [SerializeField] private DynamicSettings dynamicSettings;

        private StaticContainer staticContainer;
        private DynamicWorldContainer dynamicWorldContainer;
        private GlobalWorld globalWorld;
        private IWeb3Authenticator web3Authenticator;

        private void Awake()
        {
            realmLauncher.Initialize(realms);

            InitializationFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            async UniTaskVoid DisposeAsync()
            {
                if (dynamicWorldContainer != null)
                {
                    dynamicWorldContainer.Dispose();
                    foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                        plugin.Dispose();
                }

                if (globalWorld != null)
                    await dynamicWorldContainer.RealmController.DisposeGlobalWorldAsync(globalWorld).SuppressCancellationThrow();

                await UniTask.SwitchToMainThread();

                staticContainer?.Dispose();
                web3Authenticator?.Dispose();
            }

            realmLauncher.OnRealmSelected = null;
            DisposeAsync().Forget();
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                web3Authenticator = new DappWeb3Authenticator(new UnityAppWebBrowser());

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, web3Authenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                var sceneSharedContainer = SceneSharedContainer.Create(in staticContainer);

                (dynamicWorldContainer, isLoaded) = await DynamicWorldContainer.CreateAsync(
                    staticContainer,
                    scenePluginSettingsContainer,
                    ct,
                    uiToolkitRoot,
                    StaticLoadPositions,
                    SceneLoadRadius,
                    dynamicSettings,
                    web3Authenticator);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                // Initialize global plugins
                var anyFailure = false;

                void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
                {
                    if (!result.success)
                        anyFailure = true;
                }

                await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));
                await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));

                if (anyFailure)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory,
                    dynamicWorldContainer.EmptyScenesWorldFactory, staticContainer.CharacterObject);

                dynamicWorldContainer.DebugContainer.Builder.Build(debugUiRoot);

                // TODO: required for the authentication ui for fetching the profile.. keep it until we have a real initialization flow
                ChangeRealm("https://peer.decentraland.org");

                realmLauncher.OnRealmSelected += ChangeRealm;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        private void ChangeRealm(string selectedRealm)
        {
            async UniTask ChangeRealmAsync(StaticContainer globalContainer, string selectedRealm, CancellationToken ct)
            {
                if (globalWorld != null)
                    await dynamicWorldContainer.RealmController.UnloadCurrentRealmAsync(globalWorld);

                await UniTask.SwitchToMainThread();

                Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
                characterPos.y = 1f;

                globalContainer.CharacterObject.Controller.transform.position = characterPos;

                await dynamicWorldContainer.RealmController.SetRealmAsync(globalWorld, URLDomain.FromString(selectedRealm), ct);
            }

            ChangeRealmAsync(staticContainer, selectedRealm, CancellationToken.None).Forget();
        }
    }
}
