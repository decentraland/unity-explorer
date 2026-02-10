using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat.EventBus;
using DCL.Input;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
using DCL.Utilities.Extensions;
using DCL.PrivateWorlds.Testing;
using MVC;
using System;
using System.Threading;
using DCL.Chat;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    /// Plugin for Private Worlds feature. Wires handler (subscribes to CheckWorldAccessEvent), popup controller, and test trigger.
    /// </summary>
    public class PrivateWorldsPlugin : IDCLGlobalPlugin<PrivateWorldsPlugin.PrivateWorldsSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IEventBus eventBus;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IInputBlock inputBlock;

        private IDisposable? checkWorldAccessSubscription;

        public IWorldPermissionsService WorldPermissionsService => worldPermissionsService;

        public PrivateWorldsPlugin(
            IMVCManager mvcManager,
            IEventBus eventBus,
            IAssetsProvisioner assetsProvisioner,
            IWorldPermissionsService worldPermissionsService,
            IInputBlock inputBlock)
        {
            this.mvcManager = mvcManager;
            this.eventBus = eventBus;
            this.assetsProvisioner = assetsProvisioner;
            this.worldPermissionsService = worldPermissionsService;
            this.inputBlock = inputBlock;
        }

        public void Dispose()
        {
            checkWorldAccessSubscription?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PrivateWorldsSettings settings, CancellationToken ct)
        {
            var handler = new PrivateWorldAccessHandler(
                worldPermissionsService,
                mvcManager,
                () => eventBus.Publish(new ChatEvents.CloseChatEvent()));
            checkWorldAccessSubscription = eventBus.Subscribe<CheckWorldAccessEvent>(handler.OnCheckWorldAccess);

            if (settings.PrivateWorldPopup != null)
            {
                ProvidedAsset<GameObject> prefab = await assetsProvisioner.ProvideMainAssetAsync(settings.PrivateWorldPopup, ct: ct);
                PrivateWorldPopupView popupView = prefab.Value.GetComponent<PrivateWorldPopupView>()
                    .EnsureNotNull($"{nameof(PrivateWorldPopupView)} not found in the asset");

                var popupController = new PrivateWorldPopupController(PrivateWorldPopupController.CreateLazily(popupView, null), inputBlock);
                mvcManager.RegisterController(popupController);
            }

#if UNITY_EDITOR
            SpawnPrivateWorldsTestTrigger(worldPermissionsService, mvcManager, eventBus);
#endif
        }

#if UNITY_EDITOR
        private static void SpawnPrivateWorldsTestTrigger(
            IWorldPermissionsService permissionsService,
            IMVCManager mvcManager,
            IEventBus eventBus)
        {
            var testTriggerGo = new GameObject("[DEBUG] PrivateWorldsTestTrigger");
            var testTrigger = testTriggerGo.AddComponent<PrivateWorldsTestTrigger>();
            testTrigger.Initialize(permissionsService, mvcManager, eventBus);
        }
#endif

        public class PrivateWorldsSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PrivateWorldsPlugin) + "." + nameof(PrivateWorldsSettings))]
            [field: SerializeField]
            public AssetReferenceGameObject? PrivateWorldPopup { get; set; }
        }
    }
}
