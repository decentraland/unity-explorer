using DCL.DebugUtilities;
using System;
using System.Linq;
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
                    GenerateRandomString(Random.Range(1,250)),
                    "User" + Random.Range(0, 3),
                    Random.Range(0, 2) == 0 ? "" : "#asd38",
                    Random.Range(0, 10) <= 2));
        }

        private string GenerateRandomString(int length)
        {
            const string chars = " ABCDEFGHIJ KLMNOPQRSTU VWXYZ0123456789 ";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Range(0, s.Length)]).ToArray());
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
