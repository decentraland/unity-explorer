using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Chat.History;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.VoiceChat;

namespace DCL.PluginSystem.Global
{
    public class MouthAnimationPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IChatHistory chatHistory;
        private readonly IRoomHub roomHub;
        private readonly IVoiceChatOrchestratorState voiceChatOrchestratorState;

        public MouthAnimationPlugin(IChatHistory chatHistory, IRoomHub roomHub, IVoiceChatOrchestratorState voiceChatOrchestratorState)
        {
            this.chatHistory = chatHistory;
            this.roomHub = roomHub;
            this.voiceChatOrchestratorState = voiceChatOrchestratorState;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            MouthAnimationSystem.InjectToWorld(ref builder, chatHistory, roomHub.VoiceChatRoom().Room(), voiceChatOrchestratorState);
        }
    }
}