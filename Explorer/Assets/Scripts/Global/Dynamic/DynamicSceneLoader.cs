using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3Authentication;
using DCL.Profiles;
using Decentraland.Common;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Utility;
using Avatar = DCL.Profiles.Avatar;
using Entity = Arch.Core.Entity;
using Vector3 = UnityEngine.Vector3;

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
        [SerializeField] private TMP_InputField addressInput;

        private StaticContainer staticContainer;
        private DynamicWorldContainer dynamicWorldContainer;
        private GlobalWorld globalWorld;

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
            }

            realmLauncher.OnRealmSelected = null;
            DisposeAsync().Forget();
        }

        private async UniTask InitializationFlowAsync(CancellationToken ct)
        {
            try
            {
                IWeb3Authenticator web3Authenticator = await CreateWeb3AuthenticatorAsync(ct);

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, web3Authenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                IWeb3Identity web3Identity = await web3Authenticator.LoginAsync(ct);

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
                    dynamicWorldContainer.EmptyScenesWorldFactory, staticContainer.CharacterObject, web3Identity);

                dynamicWorldContainer.DebugContainer.Builder.Build(debugUiRoot);

                string selectedRealm = await WaitUntilRealmIsSelected(ct);
                await ChangeRealmAsync(staticContainer, selectedRealm, ct);

                UpdateOwnAvatarShape(await EnsureProfileAsync(web3Identity.EphemeralAccount.Address, ct));
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

        private void UpdateOwnAvatarShape(Profile profile)
        {
            globalWorld.EcsWorld.Query(in new QueryDescription().WithAll<PBAvatarShape>().WithNone<Profile>(),
                (in Entity entity, ref PBAvatarShape avatarShape) =>
                {
                    // the catalyst converts the address to lower case
                    if (!string.Equals(avatarShape.Id, profile.UserId, StringComparison.CurrentCultureIgnoreCase)) return;
                    globalWorld.EcsWorld.Add(entity, profile);
                });
        }

        private async UniTask<string> WaitUntilRealmIsSelected(CancellationToken ct)
        {
            string selectedRealm = null;

            void SetRealm(string str) =>
                selectedRealm = str;

            realmLauncher.OnRealmSelected += SetRealm;

            await UniTask.WaitUntil(() => !string.IsNullOrEmpty(selectedRealm), cancellationToken: ct);

            return selectedRealm;
        }

        private async UniTask ChangeRealmAsync(StaticContainer globalContainer, string selectedRealm, CancellationToken ct)
        {
            if (globalWorld != null)
                await dynamicWorldContainer.RealmController.UnloadCurrentRealmAsync(globalWorld);

            await UniTask.SwitchToMainThread();

            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(StartPosition);
            characterPos.y = 1f;

            globalContainer.CharacterObject.Controller.transform.position = characterPos;

            await dynamicWorldContainer.RealmController.SetRealmAsync(globalWorld, URLDomain.FromString(selectedRealm), ct);
        }

        private async UniTask<IWeb3Authenticator> CreateWeb3AuthenticatorAsync(CancellationToken ct)
        {
            // TODO: create the real web3 authenticator and remove addressInputField. Missing auth dapp
            var isWeb3PublicAddressSet = false;

            addressInput.onSubmit.AddListener(publicAddress =>
            {
                if (string.IsNullOrEmpty(publicAddress)) return;
                isWeb3PublicAddressSet = true;

                // cannot reassign address in the same session
                addressInput.gameObject.SetActive(false);
            });

            await UniTask.WaitUntil(() => isWeb3PublicAddressSet, cancellationToken: ct);

            return new FakeWeb3Authenticator(addressInput.text);
        }

        private async UniTask<Profile> EnsureProfileAsync(string profileId, CancellationToken ct) =>
            await dynamicWorldContainer.ProfileRepository.Get(profileId, 0, ct) ?? CreateRandomProfile(profileId);

        private Profile CreateRandomProfile(string profileId)
        {
            var name = $"Player#{profileId.Substring(profileId.Length - 4, 4)}";

            var avatar = new Avatar(BodyShape.MALE,
                new HashSet<string>(WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE)),
                new HashSet<string>(WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE)),
                new HashSet<string>(),
                new Dictionary<string, Emote>(),
                URLAddress.EMPTY, URLAddress.EMPTY,
                Color.white, WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());

            return new Profile(profileId, name, name, false, "",
                0, "", 0,
                avatar,
                new HashSet<string>(),
                new List<string>());
        }
    }
}
