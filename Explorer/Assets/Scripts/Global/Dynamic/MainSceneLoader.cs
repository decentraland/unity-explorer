using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.Profiles;
using DCL.SceneLoadingScreens;
using DCL.SkyBox;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Avatar = DCL.Profiles.Avatar;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour
    {
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private UIDocument uiToolkitRoot = null!;
        [SerializeField] private UIDocument debugUiRoot = null!;
        [SerializeField] private SkyBoxSceneData skyBoxSceneData = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private string realmUrl = "https://peer.decentraland.org";
        [SerializeField] private GameObject splashRoot = null!;
        [SerializeField] private VideoPlayer splashAnimation = null!;
        [SerializeField] private bool showSplash;
        [SerializeField] private bool showAuthentication;
        [SerializeField] private bool showLoading;
        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;
        private IWeb3IdentityCache? identityCache;

        private AsyncLoadProcessReport? loadReport;
        private SceneSharedContainer? sceneSharedContainer;
        private StaticContainer? staticContainer;
        private IWeb3VerifiedAuthenticator? web3Authenticator;
        private DappWeb3Authenticator? web3VerifiedAuthenticator;

        private void Awake()
        {
            InitializeFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            web3Authenticator.SafeDispose(ReportCategory.AUTHENTICATION);

            if (dynamicWorldContainer != null)
            {
                foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                    plugin.SafeDispose(ReportCategory.ENGINE);

                if (globalWorld != null)
                    dynamicWorldContainer.RealmController.DisposeGlobalWorld();

                dynamicWorldContainer.SafeDispose(ReportCategory.ENGINE);
            }

            if (staticContainer != null)
            {
                // Exclude SharedPlugins as they were disposed as they were already disposed of as `GlobalPlugins`
                foreach (IDCLPlugin worldPlugin in staticContainer.ECSWorldPlugins.Except<IDCLPlugin>(staticContainer.SharedPlugins))
                    worldPlugin.SafeDispose(ReportCategory.ENGINE);

                staticContainer.SafeDispose(ReportCategory.ENGINE);
            }
        }

        private async UniTask InitializeFlowAsync(CancellationToken ct)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // To avoid configuration issues, force full flow on build
            showSplash = true;
            showAuthentication = true;
            showLoading = true;
#endif

            try
            {
                splashRoot.SetActive(showSplash);

                loadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                identityCache = new ProxyIdentityCache(new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()));

                web3VerifiedAuthenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                    settings.AuthWebSocketUrl,
                    settings.AuthSignatureUrl,
                    identityCache,
                    new HashSet<string>(settings.Web3WhitelistMethods));

                web3Authenticator = new ProxyVerifiedWeb3Authenticator(
                    web3VerifiedAuthenticator,
                    identityCache);

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, identityCache, web3VerifiedAuthenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                (dynamicWorldContainer, isLoaded) = await DynamicWorldContainer.CreateAsync(
                    staticContainer,
                    scenePluginSettingsContainer,
                    ct,
                    uiToolkitRoot,
                    skyBoxSceneData,
                    settings.StaticLoadPositions,
                    settings.SceneLoadRadius,
                    dynamicSettings,
                    web3Authenticator,
                    identityCache);

                sceneSharedContainer = SceneSharedContainer.Create(in staticContainer, dynamicWorldContainer!.MvcManager);

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

                Entity playerEntity;

                (globalWorld, playerEntity) = dynamicWorldContainer!.GlobalWorldFactory.Create(sceneSharedContainer!.SceneFactory,
                    dynamicWorldContainer.EmptyScenesWorldFactory);

                dynamicWorldContainer.DebugContainer.Builder.Build(debugUiRoot);
                dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

                await ChangeRealmAsync(ct);

                if (showSplash)
                    await WaitUntilSplashAnimationEndsAsync(ct);

                splashRoot.SetActive(false);

                if (showAuthentication)
                    await ShowAuthenticationScreenAsync(ct);

                if (showLoading)
                    await UniTask.WhenAll(ShowLoadingScreenAsync(ct), LoadCharacterAndWorldAsync(playerEntity, ct));
                else
                    await LoadCharacterAndWorldAsync(playerEntity, ct);
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

        private async UniTask WaitUntilSplashAnimationEndsAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => splashAnimation.frame >= (long)(splashAnimation.frameCount - 1),
                cancellationToken: ct);
        }

        private async UniTask LoadCharacterAndWorldAsync(Entity playerEntity, CancellationToken ct)
        {
            Profile ownProfile = await GetOwnProfileAsync(ct);

            loadReport!.ProgressCounter.Value = 0.2f;

            // Add the profile into the player entity so it will create the avatar in world
            globalWorld!.EcsWorld.Add(playerEntity, ownProfile);

            await TeleportToSpawnPointAsync(ct);

            loadReport.ProgressCounter.Value = 1f;
            loadReport.CompletionSource.TrySetResult();
        }

        private async UniTask<Profile> GetOwnProfileAsync(CancellationToken ct)
        {
            if (identityCache!.Identity == null) return CreateRandomProfile();

            return await dynamicWorldContainer!.ProfileRepository.GetAsync(identityCache!.Identity.Address, 0, ct)
                   ?? CreateRandomProfile();
        }

        private Profile CreateRandomProfile() =>
            new (identityCache!.Identity?.Address ?? "fakeUserId", "Player",
                new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor()));

        private async UniTask TeleportToSpawnPointAsync(CancellationToken ct)
        {
            var teleportLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            await UniTask.WhenAny(teleportLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, 0.8f),
                dynamicWorldContainer!.ParcelServiceContainer.TeleportController.TeleportToSceneSpawnPointAsync(
                    settings.StartPosition, teleportLoadReport, ct));
        }

        private async UniTask ShowLoadingScreenAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromMinutes(2);

            await dynamicWorldContainer!.MvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport!, timeout)))
                                        .AttachExternalCancellation(ct);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await dynamicWorldContainer!.MvcManager.ShowAsync(AuthenticationScreenController.IssueCommand())
                                        .AttachExternalCancellation(ct);
        }

        private async UniTask ChangeRealmAsync(CancellationToken ct)
        {
            string realm = realmUrl;

            IRealmController realmController = dynamicWorldContainer!.RealmController;
            await realmController.SetRealmAsync(URLDomain.FromString(realm), ct);
        }
    }
}
