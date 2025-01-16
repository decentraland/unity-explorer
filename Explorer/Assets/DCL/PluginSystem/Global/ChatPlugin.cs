using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.Emoji;
using DCL.Input;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.UI;
using DCL.UI.MainUI;
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
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly NametagsData nametagsData;
        private readonly DCLInput dclInput;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly IEventSystem eventSystem;
        private readonly MainUIView mainUIView;
        private readonly IClipboardManager clipboardManager;

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
            IInputBlock inputBlock,
            Arch.Core.World world,
            Entity playerEntity, IClipboardManager clipboardManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.dclInput = dclInput;
            this.inputBlock = inputBlock;
            this.world = world;
            this.playerEntity = playerEntity;
            this.clipboardManager = clipboardManager;
            this.eventSystem = eventSystem;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ChatSettings settings, CancellationToken ct)
        {
            ChatEntryConfigurationSO chatEntryConfiguration = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryConfiguration, ct)).Value;

            chatController = new ChatController(
                () =>
                {
                    ChatView? view = mainUIView.ChatView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                chatEntryConfiguration,
                chatMessagesBus,
                chatHistory,
                entityParticipantTable,
                nametagsData,
                world,
                playerEntity,
                dclInput,
                eventSystem,
                inputBlock,
                mvcManager,
                clipboardManager
            );

            mvcManager.RegisterController(chatController);
        }

        public class ChatSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ChatPlugin) + "." + nameof(ChatSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }

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
