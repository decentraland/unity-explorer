using System.Threading;
using DCL.Chat.History;

namespace DCL.Chat.Commands.SpamChat
{ 
    public readonly struct SpamExecutionOptions
    {
        public int TotalMessages { get; }
        public string SpamSessionId { get; }
        public CancellationToken CancellationToken { get; }
        public int DelayBetweenMessagesMs { get; }
        public int BatchSize { get; }
        public int DelayBetweenBatchesMs { get; }
        public ChatChannel TargetChannel { get; }

        public SpamExecutionOptions(
            int totalMessages,
            string spamSessionId,
            CancellationToken cancellationToken,
            ChatChannel targetChannel,
            int delayBetweenMessagesMs = 0,
            int batchSize = 10,
            int delayBetweenBatchesMs = 50)
        {
            TotalMessages = totalMessages;
            SpamSessionId = spamSessionId;
            CancellationToken = cancellationToken;
            TargetChannel = targetChannel;
            DelayBetweenMessagesMs = delayBetweenMessagesMs;
            BatchSize = batchSize > 0 ? batchSize : 10;
            DelayBetweenBatchesMs = delayBetweenBatchesMs;
        }
    }
}