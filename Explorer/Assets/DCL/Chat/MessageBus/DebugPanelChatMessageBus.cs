using DCL.DebugUtilities;
using System;
using Utility;

namespace DCL.Chat.MessageBus
{
    public class DebugPanelChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;

        public event Action<ChatMessage>? OnMessageAdded;

        public DebugPanelChatMessageBus(IChatMessagesBus origin, IDebugContainerBuilder debugBuilder)
        {
            this.origin = origin;
            this.origin.OnMessageAdded += OriginOnOnMessageAdded;
            debugBuilder.AddWidget("Chat")!.AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null!);
        }

        ~DebugPanelChatMessageBus()
        {
            this.origin.OnMessageAdded -= OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public void Send(string message)
        {
            origin.Send(message);
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            OnMessageAdded?.Invoke(obj);
        }

        private void CreateTestChatEntry()
        {
            Send(StringUtils.GenerateRandomString(UnityEngine.Random.Range(1, 250)));
        }
    }
}
