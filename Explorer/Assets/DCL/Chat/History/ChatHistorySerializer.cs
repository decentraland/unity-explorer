using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace DCL.Chat.History
{
    /// <summary>
    ///
    /// </summary>
    internal class ChatHistorySerializer
    {
        private const int ENTRY_SENT_BY_LOCAL_USER = 0;
        private const int ENTRY_MESSAGE = 1;
        private const int ENTRY_USERNAME = 2;

        private const string LOCAL_USER_TRUE_VALUE = "T";
        private const string LOCAL_USER_FALSE_VALUE = "F";
        private readonly StringBuilder builder = new StringBuilder(256); // Enough not to be resized

        private readonly string[] entryValues = new string[3];
        private readonly ChatMessageFactory messageFactory;

        public ChatHistorySerializer(ChatMessageFactory messageFactory)
        {
            this.messageFactory = messageFactory;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="messageToAppend"></param>
        /// <param name="destination"></param>
        public void AppendPrivateConversationMessage(ChatMessage messageToAppend, Stream destination)
        {
            entryValues[ENTRY_SENT_BY_LOCAL_USER] = messageToAppend.IsSentByOwnUser ? LOCAL_USER_TRUE_VALUE : LOCAL_USER_FALSE_VALUE;
            entryValues[ENTRY_MESSAGE] = messageToAppend.Message;
            entryValues[ENTRY_USERNAME] = messageToAppend.SenderValidatedName;

            destination.Write(CreateHistoryEntry(entryValues));
            destination.Flush();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="messagesSource"></param>
        /// <param name="localUserWalletAddress"></param>
        /// <param name="remoteUserWalletAddress"></param>
        /// <param name="obtainedMessages"></param>
        /// <param name="ct"></param>
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
                    ParseEntryValues(currentLine, entryValues);
                    bool sentByLocalUser = entryValues[ENTRY_SENT_BY_LOCAL_USER] == LOCAL_USER_TRUE_VALUE;
                    string walletAddress = sentByLocalUser ? localUserWalletAddress : remoteUserWalletAddress;
                    ChatMessage newMessage = await messageFactory.CreateChatMessageAsync(walletAddress, sentByLocalUser, entryValues[ENTRY_MESSAGE], entryValues[ENTRY_USERNAME], ct);

                    obtainedMessages.Add(newMessage);
                    currentLine = await reader2.ReadLineAsync();
                }
            }
        }

        private byte[] CreateHistoryEntry(string[] values)
        {
            builder.Clear();

            for (int i = 0; i < values.Length; ++i)
            {
                builder.Append(entryValues[i]);

                if(i < values.Length - 1)
                    builder.Append(",");
            }

            builder.Append("\n");

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static void ParseEntryValues(string entry, string[] values)
        {
            string[] entryParts = entry.Split(',');
            values[ENTRY_SENT_BY_LOCAL_USER] = (entryParts.Length >= ENTRY_SENT_BY_LOCAL_USER + 1) ? entryParts[ENTRY_SENT_BY_LOCAL_USER] : LOCAL_USER_FALSE_VALUE;
            values[ENTRY_MESSAGE] =            (entryParts.Length >= ENTRY_MESSAGE + 1)            ? entryParts[ENTRY_MESSAGE] : string.Empty;
            values[ENTRY_USERNAME] =           (entryParts.Length >= ENTRY_USERNAME + 1)           ? entryParts[ENTRY_USERNAME] : string.Empty;
        }
    }
}
