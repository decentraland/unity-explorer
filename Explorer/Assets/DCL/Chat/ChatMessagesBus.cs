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
            string sender = "User" + Random.Range(0, 10);

            OnMessageAdded?.Invoke(
                new ChatMessage(
                    StringUtils.GenerateRandomString(Random.Range(1,250)),
                    sender,
                    Random.Range(0, 2) == 0 ? "" : sender,
                    Random.Range(0, 10) <= 2));
        }

        //Add message will be called from the message handling system of the livekit integration
        public void AddMessage()
        {
            OnMessageAdded?.Invoke(ProcessChatMessage());
        }

        //This function will get the message from the livekit integration and convert it to a ChatMessage
        private ChatMessage ProcessChatMessage()
        {
            return new ChatMessage();
        }
    }
}
