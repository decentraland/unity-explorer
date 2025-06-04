using System;
using System.Collections.Generic;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;

namespace DCL.Chat.MessageBus
{
    /// <summary>
    ///     Detects potential head-of-line blocking in message processing by analyzing
    ///     timestamp patterns of received messages.
    /// </summary>
    public class HeadOfLineBlockingDetection
    {
        private readonly List<MessageInfo> recentMessages;
        private readonly TimeSpan windowDuration;
        private readonly string roomName;
        private readonly int burstThreshold;
        private readonly double timespanDispersionThreshold;

        /// <summary>
        ///     Information about a message for head-of-line blocking detection
        /// </summary>
        private struct MessageInfo
        {
            public double ReceivedTimestamp;
            public double MessageTimestamp;
            public string FromWalletId;
        }

        /// <summary>
        ///     Initializes a new instance of the HeadOfLineBlockingDetection struct.
        /// </summary>
        /// <param name="roomName">The name of the observed room</param>
        /// <param name="windowDurationSeconds">The time window to consider for burst detection in seconds</param>
        /// <param name="burstThreshold">Minimum number of messages to consider as a burst</param>
        /// <param name="timespanDispersionThreshold">Maximum dispersion of message timestamps to trigger detection</param>
        public HeadOfLineBlockingDetection(
            string roomName,
            double windowDurationSeconds = 5.0,
            int burstThreshold = 8,
            double timespanDispersionThreshold = 1.5)
        {
            recentMessages = new List<MessageInfo>(burstThreshold * 2); // Pre-allocate capacity
            windowDuration = TimeSpan.FromSeconds(windowDurationSeconds);
            this.roomName = roomName;
            this.burstThreshold = burstThreshold;
            this.timespanDispersionThreshold = timespanDispersionThreshold;
        }

        /// <summary>
        ///     Records a new message and checks for potential head-of-line blocking
        /// </summary>
        /// <param name="receivedMessage">The message that was received</param>
        /// <returns>True if head-of-line blocking was detected</returns>
        public bool RecordAndDetect(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            double currentTime = DateTime.UtcNow.TimeOfDay.TotalSeconds;

            // Add the current message to our tracking list
            recentMessages.Add(new MessageInfo
            {
                ReceivedTimestamp = currentTime,
                MessageTimestamp = receivedMessage.Payload.Timestamp,
                FromWalletId = receivedMessage.FromWalletId,
            });

            // Remove messages older than our window
            double cutoffTime = currentTime - windowDuration.TotalSeconds;
            var removeCount = 0;

            // Count how many messages to remove from the beginning of the list
            for (var i = 0; i < recentMessages.Count; i++)
            {
                if (recentMessages[i].ReceivedTimestamp < cutoffTime)
                    removeCount++;
                else
                    break;
            }

            // Remove expired messages if any
            if (removeCount > 0)
                recentMessages.RemoveRange(0, removeCount);

            // Check if we have enough messages to potentially detect a burst
            if (recentMessages.Count >= burstThreshold) { return DetectHeadOfLineBlocking(); }

            return false;
        }

        private bool DetectHeadOfLineBlocking()
        {
            if (recentMessages.Count < burstThreshold)
                return false;

            // Find the min and max of message timestamps
            double minMessageTimestamp = double.MaxValue;
            double maxMessageTimestamp = double.MinValue;

            // Calculate the min and max of received timestamps
            double minReceivedTimestamp = double.MaxValue;
            double maxReceivedTimestamp = double.MinValue;

            // Process all messages in our window
            for (var i = 0; i < recentMessages.Count; i++)
            {
                MessageInfo msg = recentMessages[i];

                minMessageTimestamp = Math.Min(minMessageTimestamp, msg.MessageTimestamp);
                maxMessageTimestamp = Math.Max(maxMessageTimestamp, msg.MessageTimestamp);

                minReceivedTimestamp = Math.Min(minReceivedTimestamp, msg.ReceivedTimestamp);
                maxReceivedTimestamp = Math.Max(maxReceivedTimestamp, msg.ReceivedTimestamp);
            }

            // Calculate dispersions
            double messageTimestampDispersion = maxMessageTimestamp - minMessageTimestamp;
            double receivedTimestampDispersion = maxReceivedTimestamp - minReceivedTimestamp;

            // If messages were created over a significant timespan but received in a short burst
            if (messageTimestampDispersion > timespanDispersionThreshold &&
                receivedTimestampDispersion < windowDuration.TotalSeconds / 2)
            {
                LogHeadOfLineBlockingDetected(messageTimestampDispersion, receivedTimestampDispersion);
                return true;
            }

            return false;
        }

        private void LogHeadOfLineBlockingDetected(
            double messageTimestampDispersion,
            double receivedTimestampDispersion)
        {
            ReportHub.Log(
                ReportCategory.CHAT_MESSAGES,
                $"HEAD-OF-LINE BLOCKING DETECTED in {roomName}: {recentMessages.Count} messages received within {receivedTimestampDispersion:F3}s " +
                $"but message timestamps span {messageTimestampDispersion:F3}s"
            );

            // Log details of the first few messages for debugging
            int messagesToLog = Math.Min(recentMessages.Count, 5);

            for (var i = 0; i < messagesToLog; i++)
            {
                MessageInfo msg = recentMessages[i];

                ReportHub.Log(
                    ReportCategory.CHAT_MESSAGES,
                    $"  Message {i + 1}/{messagesToLog}: From={msg.FromWalletId}, " +
                    $"MessageTS={msg.MessageTimestamp:F3}, ReceivedTS={msg.ReceivedTimestamp:F3}"
                );
            }
        }
    }
}
