#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using DCL.Utilities;

namespace DCL.Chat.Commands.SpamChat
{
    public class SpamChatCommand : IChatCommand
    {
        private const int DEFAULT_MESSAGE_COUNT = 1;
        private const int DEFAULT_DELAY_MSG_MS = 0;
        private const int DEFAULT_DELAY_BATCH_MS = 50;
        private const int DEFAULT_BATCH_SIZE = 10;
        private const int MAX_HISTORY_MSG_FOR_DISPLAY = 5;

        public string Command => "spam";

        public string Description =>
            "<b>/spam check | <i><count> [delayMsgMs (0)] [delayBatchMs (50)] [batchSize (10)]</i></b>\n" +
            "  Sends test messages to the 'nearby' channel.\n" +
            "  - <i>count</i>: Total messages to send (e.g., 50). If <b>0</b>, checks current channel history for duplicate messages.\n" +
            "  - <i>delayMsgMs</i>: (Optional) Milliseconds between messages in a batch.\n" +
            "  - <i>delayBatchMs</i>: (Optional) Milliseconds between batches.\n" +
            "  - <i>batchSize</i>: (Optional) Messages per quick burst.\n" +
            "  Example: /spam 100 10 200 20";

        private readonly ObjectProxy<IChatMessagesBus> chatMessagesBusProxy;
        private ChatMessageSpammer? messageSpammer;

        public SpamChatCommand(ObjectProxy<IChatMessagesBus> chatMessagesBusProxy)
        {
            this.chatMessagesBusProxy = chatMessagesBusProxy;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length < 1) return false;
            if (!int.TryParse(parameters[0], out int count) || count < 0) return false;
            if (parameters.Length > 1 && (!int.TryParse(parameters[1], out int delayMsg) || delayMsg < 0)) return false;
            if (parameters.Length > 2 && (!int.TryParse(parameters[2], out int delayBatch) || delayBatch < 0)) return false;
            if (parameters.Length > 3 && (!int.TryParse(parameters[3], out int batchSize) || batchSize <= 0)) return false;
            return true;
        }

        public UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct)
        {
            int messageCount = DEFAULT_MESSAGE_COUNT;
            int delayMsgMs = DEFAULT_DELAY_MSG_MS;
            int delayBatchMs = DEFAULT_DELAY_BATCH_MS;
            int batchSize = DEFAULT_BATCH_SIZE;

            if (parameters.Length > 0 && int.TryParse(parameters[0], out int parsedMessageCount)) messageCount = parsedMessageCount;
            if (parameters.Length > 1 && int.TryParse(parameters[1], out int parsedDelayMsg)) delayMsgMs = parsedDelayMsg;
            if (parameters.Length > 2 && int.TryParse(parameters[2], out int parsedDelayBatch)) delayBatchMs = parsedDelayBatch;
            if (parameters.Length > 3 && int.TryParse(parameters[3], out int parsedBatchSize) && parsedBatchSize > 0) batchSize = parsedBatchSize;

            if (!chatMessagesBusProxy.Configured ||
                chatMessagesBusProxy.Object == null)
            {
                ReportHub.LogError(ReportCategory.CHAT_MESSAGES, "SpamChatCommand: ChatMessagesBus proxy not configured or bus is null!");
                return UniTask.FromResult("🔴 Error: Chat system (bus) not ready.");
            }

            // NOTE: if count is 0 then check for duplicates
            if (messageCount == 0)
            {
                return ExecuteCheckDuplicatesInHistoryAsync(channel);
            }

            messageSpammer ??= new ChatMessageSpammer(chatMessagesBusProxy.Object);

            var spamSessionId = Guid
                .NewGuid()
                .ToString("N")
                .Substring(0, 8);

            var options = new SpamExecutionOptions(
                messageCount,
                spamSessionId,
                ct,
                channel,
                delayMsgMs,
                batchSize,
                delayBatchMs
            );

            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine($"🟢 Initiated spamming of {messageCount} messages to '{channel.Id.Id}' (Session ID: {spamSessionId}).");
            responseBuilder.AppendLine($"Params: MsgDelay={delayMsgMs}ms, BatchDelay={delayBatchMs}ms, BatchSize={batchSize}.");

            AppendRecentHistoryToResponse(responseBuilder, channel);

            messageSpammer
                .SpamMessagesAsync(options)
                .Forget();

            return UniTask.FromResult(responseBuilder.ToString());
        }

        private void AppendRecentHistoryToResponse(StringBuilder builder, ChatChannel? channel)
        {
            if (channel != null && channel.Messages.Count > 1)
            {
                builder.AppendLine($"--- Recent History for '{channel.Id.Id}' (newest first, max {MAX_HISTORY_MSG_FOR_DISPLAY}) ---");
                var messagesShown = 0;
                for (int i = 1; i < channel.Messages.Count - 1 && messagesShown < MAX_HISTORY_MSG_FOR_DISPLAY; i++)
                {
                    var msg = channel.Messages[i];
                    if (msg.IsPaddingElement) continue;

                    var sender = string.IsNullOrEmpty(msg.SenderValidatedName)
                        ? string.IsNullOrEmpty(msg.SenderWalletAddress) ? "System" : msg.SenderWalletAddress.Substring(0, Math.Min(6, msg.SenderWalletAddress.Length)) + "..."
                        : msg.SenderValidatedName;

                    var preview = msg.Message.Length > 35 ? msg.Message.Substring(0, 32) + "..." : msg.Message;

                    builder.AppendLine($"- {sender}: {preview}");

                    messagesShown++;
                }

                if (messagesShown == 0) builder.AppendLine("(No actual messages found in recent history).");
            }
            else
            {
                builder.AppendLine($"(No history or channel '{channel?.Id.Id ?? "unknown"}' not found/empty).");
            }
        }

        private UniTask<string> ExecuteCheckDuplicatesInHistoryAsync(ChatChannel? channel)
        {
            if (channel == null)  return UniTask.FromResult("Error: Current channel is not available to check for duplicates.");

            var messages = new List<ChatMessage>();
            foreach (var msg in channel.Messages)
            {
                if (msg.IsPaddingElement || string.IsNullOrEmpty(msg.Message)) continue;
                messages.Add(msg);
            }

            if (messages.Count < 2)
            {
                string reason = messages.Count == 0 ?
                    "no relevant messages" :
                    "only one relevant message";
                
                return UniTask.FromResult($"No duplicates possible: " +
                                          $"Channel '{channel.Id.Id}' " +
                                          $"(Type: {channel.ChannelType}) has {reason} " +
                                          $"(Total raw messages: {channel.Messages.Count}).");
            }

            var response = new StringBuilder();
            response
                .AppendLine($"Checking {messages.Count} " +
                            $"relevant messages for duplicates in channel '{channel.Id.Id}' " +
                            $"(Type: {channel.ChannelType}, " +
                            $"Total raw: {channel.Messages.Count})...");

            var messageDetails = new Dictionary<string, List<ChatMessage>>();

            foreach (var msg in messages)
            {
                if (!messageDetails.TryGetValue(msg.Message, out var messageList))
                {
                    messageList = new List<ChatMessage>();
                    messageDetails[msg.Message] = messageList;
                }

                messageList.Add(msg);
            }

            var distinctDuplicatesFound = 0;
            var totalDuplicateInstances = 0;

            foreach (var kvp in messageDetails
                         .OrderByDescending(kvp => kvp.Value.Count)
                         .ThenBy(kvp => kvp.Key))
            {
                if (kvp.Value.Count > 1)
                {
                    distinctDuplicatesFound++;
                    totalDuplicateInstances += kvp.Value.Count;
                    response.AppendLine($"  - Found {kvp.Value.Count} instances of: \"{kvp.Key}\"");
                }
            }

            if (distinctDuplicatesFound == 0)  response.AppendLine("No duplicate messages found in the channel history among relevant messages.");
            else
            {
                string summary = $"Found {distinctDuplicatesFound} distinct message string(s) duplicated, " +
                                 $"comprising a total of {totalDuplicateInstances} message instances (including originals).\n";
                
                int idxToInsert = response.ToString().IndexOf('\n');
                if (idxToInsert >= 0)
                    response.Insert(idxToInsert + 1, summary);
                else
                    response.Append(summary);
            }

            return UniTask.FromResult(response.ToString());
        }
    }
}