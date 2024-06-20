using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class PassportPlugin : IDCLGlobalPlugin<PassportPlugin.PassportSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private PassportController passportController;
        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;

        public PassportPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
        }

        public async UniTask InitializeAsync(PassportSettings promptSettings, CancellationToken ct)
        {
            passportController = new PassportController(
                PassportController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.PassportPrefab, ct: ct)).Value.GetComponent<PassportView>(), null),
                cursor,
                profileRepository,
                characterPreviewFactory);

            mvcManager.RegisterController(passportController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            passportController.SetWorld(builder.World);
        }

        public void Dispose() =>
            passportController.Dispose();

        public class PassportSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PassportPlugin) + "." + nameof(PassportSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PassportPrefab;
        }
    }
}
