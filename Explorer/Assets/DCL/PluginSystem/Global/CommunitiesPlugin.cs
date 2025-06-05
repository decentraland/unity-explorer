using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Communities.CommunityCreation;
using DCL.Input;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class CommunitiesPlugin : IDCLGlobalPlugin<CommunitiesPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;

        private CommunityCreationEditionController? communityCreationEditionController;

        public CommunitiesPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWebBrowser webBrowser,
            IInputBlock inputBlock)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
        }

        public void Dispose()
        {
            communityCreationEditionController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(CommunitiesPluginSettings settings, CancellationToken ct)
        {
            CommunityCreationEditionView communityCreationEditionViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCreationEditionPrefab, ct: ct)).GetComponent<CommunityCreationEditionView>();
            ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>.ViewFactoryMethod communityCreationEditionViewFactoryMethod = CommunityCreationEditionController.Preallocate(communityCreationEditionViewAsset, null, out CommunityCreationEditionView communityCreationEditionView);
            communityCreationEditionController = new CommunityCreationEditionController(communityCreationEditionViewFactoryMethod, webBrowser, inputBlock);
            mvcManager.RegisterController(communityCreationEditionController);
        }
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Creation Edition Wizard")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCreationEditionPrefab { get; private set; }
    }
}
