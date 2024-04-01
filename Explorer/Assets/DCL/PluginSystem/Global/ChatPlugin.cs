using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Emoji;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : DCLGlobalPluginBase<ChatPlugin.ChatSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly NametagsData nametagsData;
        private ChatController chatController;
        private DCLInput dclInput;
        private readonly IRealmNavigator realmNavigator;

        public ChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            DCLInput dclInput,
            IRealmNavigator realmNavigator)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.chatMessagesBus = chatMessagesBus;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.dclInput = dclInput;
            this.realmNavigator = realmNavigator;
        }

        public void Dispose() { }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(ChatSettings settings, CancellationToken ct)
        {
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryConfiguration, ct)).Value;
            EmojiPanelConfigurationSO emojiPanelConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiPanelConfiguration, ct)).Value;
            EmojiSectionView emojiSectionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSectionPrefab, ct)).Value;
            EmojiButton emojiButtonPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiButtonPrefab, ct)).Value;
            EmojiSuggestionView emojiSuggestionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSuggestionPrefab, ct)).Value;
            ChatView chatView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatPanelPrefab, ct: ct)).Value.GetComponent<ChatView>();

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                chatController = new ChatController(
                    ChatController.CreateLazily(chatView, null),
                    chatEntryConfiguration,
                    chatMessagesBus,
                    entityParticipantTable,
                    nametagsData,
                    emojiPanelConfig,
                    settings.EmojiMappingJson,
                    emojiSectionPrefab,
                    emojiButtonPrefab,
                    emojiSuggestionPrefab,
                    builder.World,
                    arguments.PlayerEntity,
                    dclInput,
                    realmNavigator
                );

                mvcManager.RegisterController(chatController);
            };
        }

        public class ChatSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ChatPlugin) + "." + nameof(ChatSettings))]
            [field: Space]
            [field: SerializeField]
            public ChatViewRef ChatPanelPrefab { get; private set; }

            [field: SerializeField]
            public EmojiButtonRef EmojiButtonPrefab { get; private set; }

            [field: SerializeField]
            public EmojiSectionRef EmojiSectionPrefab { get; private set; }

            [field: SerializeField]
            public EmojiSuggestionRef EmojiSuggestionPrefab { get; private set; }

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
