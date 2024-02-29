using DCL.DebugUtilities;
using System;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Chat
{
    public class ChatMessagesBus : IChatMessagesBus
    {
        public event Action<ChatMessage> OnMessageAdded;

        public ChatMessagesBus(IDebugContainerBuilder debugBuilder)
        {
            debugBuilder.AddWidget("Chat").AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null);
        }

        private void CreateTestChatEntry()
        {
            OnMessageAdded?.Invoke(
                new ChatMessage(
                    StringUtils.GenerateRandomString(Random.Range(1,250)),
                    "User" + Random.Range(0, 3),
                    Random.Range(0, 2) == 0 ? "" : "#asd38",
                    Random.Range(0, 10) <= 2));
        }

        //Add message will be called from the message handling system of the livekit integration

        public void Send(string message)
        {
            OnMessageAdded?.Invoke(new ChatMessage(message, "Self", Random.Range(0, 2) == 0 ? "" : "#asd38", true));
        }
    }
}
