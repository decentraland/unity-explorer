using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Emoji;
using DCL.Nametags;
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
        private readonly NametagsData nametagsData;
        private ChatController chatController;

        public ChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            NametagsData nametagsData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.chatMessagesBus = chatMessagesBus;
            this.nametagsData = nametagsData;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            chatController.InjectToWorld(ref builder);

        public async UniTask InitializeAsync(ChatSettings settings, CancellationToken ct)
        {
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryConfiguration, ct)).Value;
            EmojiPanelConfigurationSO emojiPanelConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiPanelConfiguration, ct)).Value;
            EmojiSectionView emojiSectionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSectionPrefab, ct)).Value;
            EmojiButton emojiButtonPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiButtonPrefab, ct)).Value;
            EmojiSuggestionView emojiSuggestionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSuggestionPrefab, ct)).Value;

            chatController = new ChatController(
                ChatController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatPanelPrefab, ct: ct)).Value.GetComponent<ChatView>(), null),
                chatEntryConfiguration,
                chatMessagesBus,
                nametagsData,
                emojiPanelConfig,
                settings.EmojiMappingJson,
                emojiSectionPrefab,
                emojiButtonPrefab,
                emojiSuggestionPrefab);

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
            public EmojiButtonRef EmojiButtonPrefab;

            [field: SerializeField]
            public EmojiSectionRef EmojiSectionPrefab;

            [field: SerializeField]
            public EmojiSuggestionRef EmojiSuggestionPrefab;

            [field: SerializeField]
            public AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<EmojiPanelConfigurationSO> EmojiPanelConfiguration { get; private set; }

            [field: SerializeField]
            public TextAsset EmojiMappingJson { get; private set; }

            [Serializable]
            public class EmojiSuggestionPanelRef : ComponentReference<EmojiSuggestionPanelView>
            {
                public EmojiSuggestionPanelRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmojiSuggestionRef : ComponentReference<EmojiSuggestionView>
            {
                public EmojiSuggestionRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmojiSectionRef : ComponentReference<EmojiSectionView>
            {
                public EmojiSectionRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmojiButtonRef : ComponentReference<EmojiButton>
            {
                public EmojiButtonRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmojiPanelRef : ComponentReference<EmojiPanelView>
            {
                public EmojiPanelRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class ChatViewRef : ComponentReference<ChatView>
            {
                public ChatViewRef(string guid) : base(guid) { }
            }
        }
    }
}

