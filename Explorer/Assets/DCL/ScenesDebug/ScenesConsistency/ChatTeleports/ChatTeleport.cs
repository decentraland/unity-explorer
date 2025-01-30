using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.ScenesDebug.ScenesConsistency.DelayedResources;
using DCL.Utilities.Extensions;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.ChatTeleports
{
    public class ChatTeleport : IChatTeleport
    {
        private readonly IDelayedResource<ChatView> chatViewResource;

        public ChatTeleport(IDelayedResource<ChatView> chatViewResource)
        {
            this.chatViewResource = chatViewResource;
        }

        public UniTask WaitReadyAsync() =>
            chatViewResource.ResourceAsync();

        public void GoTo(Vector2Int coordinate)
        {
            ChatView view = chatViewResource.DangerousResource().EnsureNotNull();
            view.InputBoxText = $"/{ChatCommandsUtils.COMMAND_GOTO} {coordinate.x},{coordinate.y}";
            view.SubmitInput();
        }
    }
}
