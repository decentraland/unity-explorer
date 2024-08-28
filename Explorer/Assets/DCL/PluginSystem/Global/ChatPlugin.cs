using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Emoji;
using DCL.Input;
using DCL.Input.UnityInputSystem.Blocks;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.UI.MainUI;
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
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly NametagsData nametagsData;
        private readonly DCLInput dclInput;
        private readonly IInputBlock inputBlock;
        private readonly IEventSystem eventSystem;
        private readonly MainUIView mainUIView;

        private ChatController chatController;

        public ChatPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            DCLInput dclInput,
            IEventSystem eventSystem,
            MainUIView mainUIView,
            IInputBlock inputBlock
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.dclInput = dclInput;
            this.eventSystem = eventSystem;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(ChatSettings settings, CancellationToken ct)
        {
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryConfiguration, ct)).Value;
            EmojiPanelConfigurationSO emojiPanelConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiPanelConfiguration, ct)).Value;
            EmojiSectionView emojiSectionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSectionPrefab, ct)).Value;
            EmojiButton emojiButtonPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiButtonPrefab, ct)).Value;
            EmojiSuggestionView emojiSuggestionPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmojiSuggestionPrefab, ct)).Value;

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                chatController = new ChatController(
                    () =>
                    {
                        var view = mainUIView.ChatView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    chatEntryConfiguration,
                    chatMessagesBus,
                    chatHistory,
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
                    eventSystem,
                    inputBlock
                );

                mvcManager.RegisterController(chatController);
            };
        }

        public class ChatSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ChatPlugin) + "." + nameof(ChatSettings))]
            [field: Space]

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
            public class MainUIRef : ComponentReference<MainUIView>
            {
                public MainUIRef(string guid) : base(guid) { }
            }

        }
    }
}
