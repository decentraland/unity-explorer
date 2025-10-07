using System;

namespace DCL.Chat.MessageBus
{
    public enum ChatMessageOrigin
    {
        CHAT,
        DEBUG_PANEL,
        RESTRICTED_ACTION_API,
        MINIMAP,
        JUMP_IN,
        TELEPORT_PROMPT,
    }

    public static class ChatMessageOriginExtensions
    {
        public static string ToStringValue(this ChatMessageOrigin origin)
        {
            return origin switch
                   {
                       ChatMessageOrigin.CHAT => "chat",
                       ChatMessageOrigin.DEBUG_PANEL => "debug panel",
                       ChatMessageOrigin.RESTRICTED_ACTION_API => "RestrictedActionAPI",
                       ChatMessageOrigin.MINIMAP => "minimap",
                       ChatMessageOrigin.JUMP_IN => "jump in",
                       ChatMessageOrigin.TELEPORT_PROMPT => "teleport prompt",
                       _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
                   };
        }
    }
}
