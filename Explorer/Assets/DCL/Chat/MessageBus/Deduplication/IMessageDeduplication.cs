namespace DCL.Chat.MessageBus.Deduplication
{
    public interface IMessageDeduplication
    {
        bool Contains(string walletId, double timestamp);

        void Register(string walletId, double timestamp);
    }

    public static class MessageDeduplicationExtensions
    {
        public static bool TryPass(this IMessageDeduplication deduplication, string walletId, double timestamp)
        {
            if (deduplication.Contains(walletId, timestamp))
                return false;

            deduplication.Register(walletId, timestamp);
            return true;
        }
    }
}
