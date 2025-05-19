using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Settings.Settings;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        private readonly ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy;
        private readonly IAssetsProvisioner assetsProvisioner;


        public VoiceChatPlugin(ObjectProxy<VoiceChatSettingsAsset> voiceChatSettingsProxy, IAssetsProvisioner assetsProvisioner)
        {
            this.voiceChatSettingsProxy = voiceChatSettingsProxy;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            var voiceChatSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatSettings, ct: ct);
            voiceChatSettingsProxy.SetObject(voiceChatSettings.Value);
        }

        public void Dispose()
        {
        }


        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatSettingsReference VoiceChatSettings { get; private set; }

            [Serializable]
            public class VoiceChatSettingsReference : AssetReferenceT<VoiceChatSettingsAsset>
            {
                public VoiceChatSettingsReference(string guid) : base(guid) { }
            }



        }
    }
}
