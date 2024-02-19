using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : IDCLGlobalPlugin<ChatPlugin.ChatSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private ChatController chatController;

        public ChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.chatMessagesBus = chatMessagesBus;
            this.chatEntryConfiguration = chatEntryConfiguration;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(ChatSettings settings, CancellationToken ct)
        {
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryConfiguration, ct)).Value;

            chatController = new ChatController(
                ChatController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatPanelPrefab, ct: ct)).Value.GetComponent<ChatView>(), null),
                chatEntryConfiguration,
                chatMessagesBus);

            mvcManager.RegisterController(chatController);
            mvcManager.ShowAsync(ChatController.IssueCommand()).Forget();
        }

        public class ChatSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ChatPlugin) + "." + nameof(ChatSettings))]
            [field: Space]
            [field: SerializeField]
            public ChatViewRef ChatPanelPrefab;

            [field: SerializeField]
            public AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }

            [Serializable]
            public class ChatViewRef : ComponentReference<ChatView>
            {
                public ChatViewRef(string guid) : base(guid) { }
            }
        }
    }
}

