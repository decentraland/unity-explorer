using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace DCL.Chat.History
{
    /// <summary>
    /// Provides a centralized way to build, parse and serialize chat messages.
    /// </summary>
    internal class ChatHistorySerializer
    {
        private const int ENTRY_SENT_BY_LOCAL_USER = 0;
        private const int ENTRY_MESSAGE = 1;
        private const int ENTRY_USERNAME = 2;
        private const int ENTRY_TIMESTAMP = 3;
        private const int ENTRY_FIELD_COUNT = 4;

        private const string LOCAL_USER_TRUE_VALUE = "T";
        private const string LOCAL_USER_FALSE_VALUE = "F";
        private const char FIELD_SEPARATOR = '\u0002';
        private const char ROW_SEPARATOR = '\n';
        private readonly StringBuilder builder = new StringBuilder(256); // Enough not to be resized
        private readonly JsonSerializer jsonSerializer = new JsonSerializer();

        private readonly string[] entryValues = new string[ENTRY_FIELD_COUNT];
        private readonly ChatMessageFactory messageFactory;

        public ChatHistorySerializer(ChatMessageFactory messageFactory)
        {
            this.messageFactory = messageFactory;
        }

        /// <summary>
        /// Builds a new entry according to the data of a chat message (in a private conversation) and writes it at the end
        /// of an output.
        /// </summary>
        /// <param name="messageToAppend">The message to serialize.</param>
        /// <param name="destination">The output where the new entry will be appended.</param>
        public void AppendPrivateConversationMessage(ChatMessage messageToAppend, Stream destination)
        {
            entryValues[ENTRY_SENT_BY_LOCAL_USER] = messageToAppend.IsSentByOwnUser ? LOCAL_USER_TRUE_VALUE : LOCAL_USER_FALSE_VALUE;
            entryValues[ENTRY_MESSAGE] = messageToAppend.Message;
            entryValues[ENTRY_USERNAME] = messageToAppend.SenderValidatedName;
            entryValues[ENTRY_TIMESTAMP] = messageToAppend.SentTimestampRaw.ToString(CultureInfo.InvariantCulture);

            destination.Write(CreateHistoryEntry(entryValues));
            destination.Flush();
        }

        /// <summary>
        /// Deserializes and parses all the chat messages (of a private conversation) in an input source and returns the filled
        /// instances of all of them in the order they were stored.
        /// </summary>
        /// <remarks>
        /// Please note that the username of both the local user or the remote user may have changed, but retrieved messages
        /// will keep the old usernames.
        /// </remarks>
        /// <param name="messagesSource">The container of the serialized data to read.</param>
        /// <param name="localUserWalletAddress">The wallet address of the current account. It will be used for messages of the local user.</param>
        /// <param name="remoteUserWalletAddress">The wallet address of the remote account. It will be used for messages of the user the local user is talking with.</param>
        /// <param name="obtainedMessages">The output list where all the deserialized messages will be stored.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The task of the asynchronous operation.</returns>
        public async UniTask ReadAllPrivateConversationMessagesAsync(Stream messagesSource, string localUserWalletAddress, string remoteUserWalletAddress, List<ChatMessage> obtainedMessages, CancellationToken ct)
        {
            string fullFileContent;

            using (StreamReader reader = new StreamReader(messagesSource))
            {
                fullFileContent = await reader.ReadToEndAsync();
                // Removes the paddings of the AES algorithm
                fullFileContent = fullFileContent.Replace("\0", string.Empty); // TODO: This will change according to the chosen padding mode
            }

            using (StringReader reader2 = new StringReader(fullFileContent))
            {
                string currentLine = await reader2.ReadLineAsync();

                while(currentLine != null)
                {
                    if (ct.IsCancellationRequested) break;

                    if (string.IsNullOrWhiteSpace(currentLine))
                    {
                        currentLine = await reader2.ReadLineAsync();
                        continue;
                    }

                    try
                    {
                        ParseEntryValues(currentLine, entryValues);

                        bool sentByLocalUser = entryValues[ENTRY_SENT_BY_LOCAL_USER] == LOCAL_USER_TRUE_VALUE;
                        string walletAddress = sentByLocalUser ? localUserWalletAddress : remoteUserWalletAddress;
                        string timestampString = entryValues[ENTRY_TIMESTAMP].Trim();

                        if (!double.TryParse(timestampString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double timestamp)
                            && !double.TryParse(entryValues[ENTRY_TIMESTAMP], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out timestamp)) // fallback
                        {
                            ReportHub.LogWarning(ReportCategory.CHAT_HISTORY, $"skipping corrupted entry due to invalid timestamp: '{entryValues[ENTRY_TIMESTAMP]}'. Line: '{currentLine}'");
                            currentLine = await reader2.ReadLineAsync();
                            continue;
                        }

                        ChatMessage newMessage = messageFactory.CreateChatMessage(walletAddress, sentByLocalUser, entryValues[ENTRY_MESSAGE], entryValues[ENTRY_USERNAME], timestamp);
                        obtainedMessages.Add(newMessage);
                    }
                    catch { ReportHub.LogWarning(ReportCategory.CHAT_HISTORY, $"skipping corrupted entry. Line: '{currentLine}'"); }

                    currentLine = await reader2.ReadLineAsync();
                }
            }
        }

        /// <summary>
        /// Reads a serialized version of the user conversation settings and returns a deserialized instance.
        /// </summary>
        /// <param name="inputStream">The JSON-formatted text, with read permissions.</param>
        /// <returns>The filled instance of the user conversation settings.</returns>
        public ChatHistoryStorage.UserConversationsSettings DeserializeUserConversationSettings(Stream inputStream)
        {
            using var streamReader = new StreamReader(inputStream);
            using var jsonReader = new JsonTextReader(streamReader);

            return jsonSerializer.Deserialize<ChatHistoryStorage.UserConversationsSettings>(jsonReader);
        }

        /// <summary>
        /// Writes a serialized version of the user conversation settings into a destination output.
        /// </summary>
        /// <param name="conversationsSettingsToSerialize">The instance to be serialized.</param>
        /// <param name="outputStream">The output where to store the JSON-formatted text, with writing permission.</param>
        public void SerializeUserConversationSettings(ChatHistoryStorage.UserConversationsSettings conversationsSettingsToSerialize, Stream outputStream)
        {
            using TextWriter streamWriter = new StreamWriter(outputStream);
            jsonSerializer.Serialize(streamWriter, conversationsSettingsToSerialize);
        }

        private byte[] CreateHistoryEntry(string[] values)
        {
            builder.Clear();

            for (int i = 0; i < values.Length; ++i)
            {
                builder.Append(values[i]);

                if(i < values.Length - 1)
                    builder.Append(FIELD_SEPARATOR);
            }

            builder.Append(ROW_SEPARATOR);

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static void ParseEntryValues(string entry, string[] values)
        {
            string[] entryParts = entry.Split(FIELD_SEPARATOR);
            values[ENTRY_SENT_BY_LOCAL_USER] = (entryParts.Length > ENTRY_SENT_BY_LOCAL_USER) ? entryParts[ENTRY_SENT_BY_LOCAL_USER] : LOCAL_USER_FALSE_VALUE;
            values[ENTRY_MESSAGE] =            (entryParts.Length > ENTRY_MESSAGE)            ? entryParts[ENTRY_MESSAGE] : string.Empty;
            values[ENTRY_USERNAME] =           (entryParts.Length > ENTRY_USERNAME)           ? entryParts[ENTRY_USERNAME] : string.Empty;
            values[ENTRY_TIMESTAMP] =          (entryParts.Length > ENTRY_TIMESTAMP)          ? entryParts[ENTRY_TIMESTAMP] : "0.0";
        }

        // --- Reaction serialization ---

        private const int REACTION_MESSAGE_ID = 0;
        private const int REACTION_EMOJI_INDEX = 1;
        private const int REACTION_WALLET = 2;
        private const int REACTION_ACTION = 3;
        private const int REACTION_FIELD_COUNT = 4;

        private const string REACTION_ADD = "+";
        private const string REACTION_REMOVE = "-";

        private readonly string[] reactionValues = new string[REACTION_FIELD_COUNT];

        /// <summary>
        /// Appends a single reaction entry to an append-only reaction log.
        /// </summary>
        public void AppendReactionEntry(string messageId, int emojiIndex, string walletAddress, bool isRemoval, Stream destination)
        {
            reactionValues[REACTION_MESSAGE_ID] = messageId;
            reactionValues[REACTION_EMOJI_INDEX] = emojiIndex.ToString(CultureInfo.InvariantCulture);
            reactionValues[REACTION_WALLET] = walletAddress;
            reactionValues[REACTION_ACTION] = isRemoval ? REACTION_REMOVE : REACTION_ADD;

            destination.Write(CreateHistoryEntry(reactionValues));
            destination.Flush();
        }

        /// <summary>
        /// Reads and replays an append-only reaction log, populating the channel with the final state.
        /// Uses <see cref="ChatChannel.FillReaction"/> (silent — no events fired).
        /// </summary>
        public async UniTask ReadAllReactionsAsync(Stream source, ChatChannel channel, CancellationToken ct)
        {
            string fullContent;

            using (var reader = new StreamReader(source))
            {
                fullContent = await reader.ReadToEndAsync();
                fullContent = fullContent.Replace("\0", string.Empty);
            }

            // Replay into a temporary structure to compute final state.
            // Key: (messageId, emojiIndex) → set of wallet addresses.
            var state = new Dictionary<(string messageId, int emojiIndex), HashSet<string>>();

            using (var reader = new StringReader(fullContent))
            {
                string currentLine = await reader.ReadLineAsync();

                while (currentLine != null)
                {
                    if (ct.IsCancellationRequested) break;

                    if (!string.IsNullOrWhiteSpace(currentLine))
                    {
                        try
                        {
                            string[] parts = currentLine.Split(FIELD_SEPARATOR);

                            if (parts.Length >= REACTION_FIELD_COUNT)
                            {
                                string msgId = parts[REACTION_MESSAGE_ID];
                                string action = parts[REACTION_ACTION];

                                if (int.TryParse(parts[REACTION_EMOJI_INDEX], NumberStyles.Integer, CultureInfo.InvariantCulture, out int emoji))
                                {
                                    string wallet = parts[REACTION_WALLET];
                                    var key = (msgId, emoji);

                                    if (action == REACTION_ADD)
                                    {
                                        if (!state.TryGetValue(key, out HashSet<string>? wallets))
                                        {
                                            wallets = new HashSet<string>();
                                            state[key] = wallets;
                                        }

                                        wallets.Add(wallet);
                                    }
                                    else if (action == REACTION_REMOVE)
                                    {
                                        if (state.TryGetValue(key, out HashSet<string>? wallets))
                                        {
                                            wallets.Remove(wallet);

                                            if (wallets.Count == 0)
                                                state.Remove(key);
                                        }
                                    }
                                }
                            }
                        }
                        catch { ReportHub.LogWarning(ReportCategory.CHAT_HISTORY, $"Skipping corrupted reaction entry. Line: '{currentLine}'"); }
                    }

                    currentLine = await reader.ReadLineAsync();
                }
            }

            // Apply final state to channel silently.
            foreach (var kvp in state)
            {
                foreach (string wallet in kvp.Value)
                    channel.FillReaction(kvp.Key.messageId, kvp.Key.emojiIndex, wallet);
            }
        }
    }
}
