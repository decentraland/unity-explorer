using System;

namespace DCL.Chat.MessageBus.Deduplication
{
    public interface IMessageDeduplication<in T> where T :  IComparable<T>
    {
        bool Contains(string walletId, T timestamp);

        void Register(string walletId, T timestamp);
    }

    public static class MessageDeduplicationExtensions
    {
        public static bool TryPass<T>(this IMessageDeduplication<T> deduplication, string walletId, T timestamp) where T : IComparable<T>
        {
            if (deduplication.Contains(walletId, timestamp))
                return false;

            deduplication.Register(walletId, timestamp);
            return true;
        }
    }
}
