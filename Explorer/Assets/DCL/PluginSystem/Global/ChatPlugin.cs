using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Input;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : IDCLGlobalPluginWithoutSettings
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
        private readonly ViewDependencies viewDependencies;
        private readonly IChatCommandsBus chatCommandsBus;
        private readonly IProfileNameColorHelper profileNameColorHelper;

        private ChatController chatController;

        public ChatPlugin(
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            MainUIView mainUIView,
            IInputBlock inputBlock,
            Arch.Core.World world,
            Entity playerEntity,
            ViewDependencies viewDependencies,
            IChatCommandsBus chatCommandsBus,
            IProfileNameColorHelper profileNameColorHelper)
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
            this.profileNameColorHelper = profileNameColorHelper;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct)
        {
            chatController = new ChatController(
                () =>
                {
                    ChatView? view = mainUIView.ChatView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                profileNameColorHelper,
                chatMessagesBus,
                chatHistory,
                entityParticipantTable,
                nametagsData,
                world,
                playerEntity,
                inputBlock,
                viewDependencies,
                chatCommandsBus
            );

            mvcManager.RegisterController(chatController);
        }
    }
}
