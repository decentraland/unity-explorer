using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Settings.Settings;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        public VoiceChatPlugin()
        {

        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct) =>
            throw new NotImplementedException();

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
