using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
using DCL.Utilities.Extensions;
using DCL.PrivateWorlds.Testing;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    /// Plugin for Private Worlds feature. Registers popup controller and spawns test trigger.
    /// The handler (PrivateWorldAccessHandler) is created in DynamicWorldContainer.
    /// Chat minimization on popup show is handled by IBlocksChat on the popup controller.
    /// </summary>
    public class PrivateWorldsPlugin : IDCLGlobalPlugin<PrivateWorldsPlugin.PrivateWorldsSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IWorldAccessGate worldAccessGate;
        private readonly IInputBlock inputBlock;

        public PrivateWorldsPlugin(
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWorldPermissionsService worldPermissionsService,
            IWorldAccessGate worldAccessGate,
            IInputBlock inputBlock)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.worldPermissionsService = worldPermissionsService;
            this.worldAccessGate = worldAccessGate;
            this.inputBlock = inputBlock;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PrivateWorldsSettings settings, CancellationToken ct)
        {
            if (settings.PrivateWorldPopup != null)
            {
                ProvidedAsset<GameObject> prefab = await assetsProvisioner.ProvideMainAssetAsync(settings.PrivateWorldPopup, ct: ct);
                PrivateWorldPopupView popupView = prefab.Value.GetComponent<PrivateWorldPopupView>()
                    .EnsureNotNull($"{nameof(PrivateWorldPopupView)} not found in the asset");

                var popupController = new PrivateWorldPopupController(PrivateWorldPopupController.CreateLazily(popupView, null), inputBlock);
                mvcManager.RegisterController(popupController);
            }

#if UNITY_EDITOR
            SpawnPrivateWorldsTestTrigger(worldPermissionsService, mvcManager, worldAccessGate);
#endif
        }

#if UNITY_EDITOR
        private static void SpawnPrivateWorldsTestTrigger(
            IWorldPermissionsService permissionsService,
            IMVCManager mvcManager,
            IWorldAccessGate worldAccessGate)
        {
            var testTriggerGo = new GameObject("[DEBUG] PrivateWorldsTestTrigger");
            var testTrigger = testTriggerGo.AddComponent<PrivateWorldsTestTrigger>();
            testTrigger.Initialize(permissionsService, mvcManager, worldAccessGate);
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
