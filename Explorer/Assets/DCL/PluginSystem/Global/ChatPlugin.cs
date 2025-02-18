using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Emoji;
using DCL.Friends.Chat;
using DCL.Input;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : IDCLGlobalPlugin<ChatPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly NametagsData nametagsData;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly MainUIView mainUIView;
        private readonly IChatLifecycleBusController chatLifecycleBusController;
        private readonly ViewDependencies viewDependencies;
        private readonly IChatCommandsBus chatCommandsBus;
        private readonly IRoomHub roomHub;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ITextFormatter hyperlinkTextFormatter;

        private ChatController chatController;

        public ChatPlugin(
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            MainUIView mainUIView,
            IInputBlock inputBlock,
            IChatLifecycleBusController chatLifecycleBusController,
            Arch.Core.World world,
            Entity playerEntity,
            ViewDependencies viewDependencies,
            IChatCommandsBus chatCommandsBus,
            IRoomHub roomHub,
            IAssetsProvisioner assetsProvisioner, ITextFormatter hyperlinkTextFormatter)
        {
            this.mvcManager = mvcManager;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.inputBlock = inputBlock;
            this.world = world;
            this.playerEntity = playerEntity;
            this.viewDependencies = viewDependencies;
            this.chatCommandsBus = chatCommandsBus;
            this.assetsProvisioner = assetsProvisioner;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
            this.chatLifecycleBusController = chatLifecycleBusController;
            this.roomHub = roomHub;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ChatPluginSettings settings, CancellationToken ct)
        {
            ProvidedAsset<ChatAudioSettingsAsset> chatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatSettingsAsset, ct);

            chatController = new ChatController(
                () =>
                {
                    ChatView? view = mainUIView.ChatView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                chatMessagesBus,
                chatHistory,
                entityParticipantTable,
                nametagsData,
                world,
                playerEntity,
                chatLifecycleBusController,
                inputBlock,
                viewDependencies,
                chatCommandsBus,
                roomHub,
                chatSettingsAsset.Value,
                hyperlinkTextFormatter
            );

            mvcManager.RegisterController(chatController);
        }
    }

    public class ChatPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceT<ChatAudioSettingsAsset> ChatSettingsAsset { get; private set; }
    }
}
