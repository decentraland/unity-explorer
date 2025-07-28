using DCL.Chat.History;

namespace DCL.Chat.ChatUseCases
{
    public class MarkMessagesAsReadCommand
    {
        public void Execute(ChatChannel channel, int? messagesCount = null)
        {
            if (channel.ReadMessages == channel.Messages.Count) return;

            if (messagesCount == null)
                channel.MarkAllMessagesAsRead();
            else
                channel.ReadMessages = messagesCount.Value;
        }
    }
}
