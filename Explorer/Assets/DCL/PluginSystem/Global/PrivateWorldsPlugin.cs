using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Input;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
using DCL.Utilities.Extensions;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    /// Plugin for Private Worlds feature. Registers popup controller.
    /// The handler (PrivateWorldAccessHandler) is created in DynamicWorldContainer.
    /// The popup controller receives a callback that blurs the chat through the chat event bus.
    /// When in a world, a permission guard checks access on comms disconnect signals and teleports to Genesis Plaza if denied.
    /// </summary>
    public class PrivateWorldsPlugin : IDCLGlobalPlugin<PrivateWorldsPlugin.PrivateWorldsSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRoomHub roomHub;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IInputBlock inputBlock;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IChatHistory chatHistory;
        private readonly ChatEventBus chatEventBus;

        private PrivateWorldPermissionGuard? permissionGuard;

        public PrivateWorldsPlugin(
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IRoomHub roomHub,
            IWorldPermissionsService worldPermissionsService,
            IInputBlock inputBlock,
            IRealmData realmData,
            IRealmNavigator realmNavigator,
            IChatHistory chatHistory,
            ChatEventBus chatEventBus)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.roomHub = roomHub;
            this.worldPermissionsService = worldPermissionsService;
            this.inputBlock = inputBlock;
            this.realmData = realmData;
            this.realmNavigator = realmNavigator;
            this.chatHistory = chatHistory;
            this.chatEventBus = chatEventBus;
        }

        public void Dispose() =>
            permissionGuard?.Dispose();

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PrivateWorldsSettings settings, CancellationToken ct)
        {
            permissionGuard = new PrivateWorldPermissionGuard(roomHub, realmData, worldPermissionsService, realmNavigator, chatHistory);

            if (settings.PrivateWorldPopup != null)
            {
                ProvidedAsset<GameObject> prefab = await assetsProvisioner.ProvideMainAssetAsync(settings.PrivateWorldPopup, ct: ct);
                PrivateWorldPopupView popupView = prefab.Value.GetComponent<PrivateWorldPopupView>()
                    .EnsureNotNull($"{nameof(PrivateWorldPopupView)} not found in the asset");

                var popupController = new PrivateWorldPopupController(
                    PrivateWorldPopupController.CreateLazily(popupView, null),
                    inputBlock,
                    worldPermissionsService,
                    requestChatBlur: () => chatEventBus.Publish(new ChatEvents.BlurRequestedEvent()));
                mvcManager.RegisterController(popupController);
            }
        }

        [Serializable]
        public class PrivateWorldsSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PrivateWorldsPlugin) + "." + nameof(PrivateWorldsSettings))]
            [field: SerializeField]
            public AssetReferenceGameObject? PrivateWorldPopup { get; set; }
        }
    }
}
