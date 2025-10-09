using System;

namespace DCL.Utilities
{
    /// <summary>
    ///     Глобальный простой локатор для объектов, к которым MCP должен иметь быстрый доступ.
    ///     Использует тип object и рефлексию на стороне MCP, чтобы избежать жёстких зависимостей между assembly.
    ///     Преднамеренно минималистичен как грязный прототип.
    /// </summary>
    public static class MCPGlobalLocator
    {
        public static object ChatMessagesBus;
        public static object ChatHistory;

        public static bool HasChatMessagesBus => ChatMessagesBus != null;
        public static bool HasChatHistory => ChatHistory != null;

        public static void Reset()
        {
            ChatMessagesBus = null;
            ChatHistory = null;
        }
    }
}
