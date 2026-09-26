using System;

namespace DCL.Chat.MessageBus
{
    public enum ChatMessageOrigin
    {
        Chat,
        DebugPanel,
        RestrictedActionApi,
        Minimap,
        JumpIn,
        TeleportPrompt,
    }

    public static class ChatMessageOriginExtensions
    {
        public static string ToStringValue(this ChatMessageOrigin origin)
        {
            return origin switch
                   {
                       ChatMessageOrigin.Chat => "chat",
                       ChatMessageOrigin.DebugPanel => "debug panel",
                       ChatMessageOrigin.RestrictedActionApi => "RestrictedActionAPI",
                       ChatMessageOrigin.Minimap => "minimap",
                       ChatMessageOrigin.JumpIn => "jump in",
                       ChatMessageOrigin.TeleportPrompt => "teleport prompt",
                       _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
                   };
        }
    }
}
