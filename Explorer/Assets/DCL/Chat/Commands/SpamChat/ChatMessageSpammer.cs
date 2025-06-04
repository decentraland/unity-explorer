using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;

namespace DCL.Chat.Commands.SpamChat
{
    public class ChatMessageSpammer
    {
        private readonly IChatMessagesBus chatMessagesBus;
        
        public ChatMessageSpammer(IChatMessagesBus chatMessagesBus)
        {
            this.chatMessagesBus = chatMessagesBus;
        }

        public async UniTaskVoid SpamMessagesAsync(SpamExecutionOptions options)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES,
                $"[Spammer-{options.SpamSessionId}] Starting Spam on Channel '{options.TargetChannel.Id.Id}' (Type: {options.TargetChannel.ChannelType}): " +
                $"Total={options.TotalMessages}, BatchSize={options.BatchSize}, " +
                $"MsgDelay={options.DelayBetweenMessagesMs}, BatchDelay={options.DelayBetweenBatchesMs}");

            var messagesSent = 0;

            // NOTE: delay to allow the command to be processed before starting the spam
            await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: options.CancellationToken);
            
            try
            {
                while (messagesSent < options.TotalMessages && !options.CancellationToken.IsCancellationRequested)
                {
                    for (var i = 0; i < options.BatchSize && 
                                    messagesSent < options.TotalMessages &&
                                    !options.CancellationToken.IsCancellationRequested; i++)
                    {
                        var clientTimestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                        var channelIdentifier = options.TargetChannel.ChannelType == ChatChannel.ChatChannelType.NEARBY
                            ? "Nearby"
                            : $"Private({options.TargetChannel.Id.Id})";

                        string messageContent =
                            $"[SPAM_SESSION:{options.SpamSessionId}][MSG_IDX:{messagesSent:D4}] " +
                            $"TS:{clientTimestamp}" +
                            $"Channel:{channelIdentifier} " +
                            $"Payload: Test message {messagesSent + 1}/{options.TotalMessages}.";

                        chatMessagesBus.Send(options.TargetChannel,
                            messageContent,
                            $"spam_cmd_{options.TargetChannel.ChannelType.ToString().ToLower()}_{options.SpamSessionId}");
                        
                        messagesSent++;

                        if (options.DelayBetweenMessagesMs > 0)
                        {
                            await UniTask.Delay(options.DelayBetweenMessagesMs,
                                ignoreTimeScale: true,
                                cancellationToken: options.CancellationToken);
                        }
                    }

                    if (messagesSent < options.TotalMessages &&
                        options.DelayBetweenBatchesMs > 0 &&
                        !options.CancellationToken.IsCancellationRequested)
                    {
                        await UniTask.Delay(options.DelayBetweenBatchesMs,
                            ignoreTimeScale: true,
                            cancellationToken: options.CancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.CHAT_MESSAGES, 
                    $"[Spammer-{options.SpamSessionId}] Spam Task Canceled: {messagesSent}/{options.TotalMessages} messages sent.");
                return;
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES);
                return;
            }

            ReportHub.Log(ReportCategory.CHAT_MESSAGES,
                $"[Spammer-{options.SpamSessionId}] Spam Task Finished: {messagesSent}/{options.TotalMessages} messages sent.");
        }
    }
}