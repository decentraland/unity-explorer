using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using System;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Chat
{
    public class ChatMessagesBus : IChatMessagesBus
    {
        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string>? MessageSent;

        public ChatMessagesBus(IDebugContainerBuilder debugBuilder)
        {
            debugBuilder.AddWidget("Chat").AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null);
        }

        private void CreateTestChatEntry()
        {
            string sender = "User" + Random.Range(0, 10);

            OnMessageAdded?.Invoke(
                new ChatMessage(
                    StringUtils.GenerateRandomString(Random.Range(1,250)),
                    sender,
                    Random.Range(0, 2) == 0 ? "" : sender,
                    Random.Range(0, 10) <= 2,
                    true));
        }

        //Add message will be called from the message handling system of the livekit integration

        public void Send(string message)
        {
            OnMessageAdded?.Invoke(new ChatMessage(message, "Self", Random.Range(0, 2) == 0 ? "" : "#asd38", true, true));
        }

        public void Dispose()
        {
        }
    }
}
