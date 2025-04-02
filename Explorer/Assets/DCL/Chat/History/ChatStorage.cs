using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat.History
{
    /// <summary>
    ///
    /// </summary>
    public class ChatStorage : IDisposable
    {
        /// <summary>
        ///
        /// </summary>
        [Serializable]
        public class UserConversationsSettings
        {
            /// <summary>
            ///
            /// </summary>
            [SerializeField]
            public List<string> ConversationFilePaths; // Already sorted, a conversation is closed if it does not appear here
        }

        private class ChannelFile
        {
            public string Path;
            public Stream Content;
            public bool IsInitialized;
            public float LastMessageTime;
        }

        private struct MessageToProcess
        {
            public ChatChannel.ChannelId DestinationChannelId;
            public ChatMessage Message;
        }

        private readonly IChatHistory chatHistory;

        private readonly string localUserWalletAddress;

        private readonly Dictionary<ChatChannel.ChannelId, ChannelFile> channelFiles = new Dictionary<ChatChannel.ChannelId, ChannelFile>();
        private readonly Queue<MessageToProcess> messagesToProcess = new();

        private readonly CancellationTokenSource cts = new();
        private readonly object queueLocker = new object();
        private readonly object channelsLocker = new object();

        private readonly List<ChatMessage> messagesBuffer = new List<ChatMessage>();

        private readonly string channelFilesFolder;
        private readonly string userFilesFolder;
        private readonly string userConversationSettingsFile;
        private UserConversationsSettings conversationSettings;

        private bool areAllChannelsLoaded;

        private readonly ChatHistoryEncryptor chatEncryptor;
        private readonly ChatHistorySerializer chatSerializer;

        private readonly ReportData reportData = new ReportData(ReportCategory.CHAT_HISTORY);

        public ChatStorage(IChatHistory chatHistory, ChatMessageFactory messageFactory, string localUserWalletAddress)
        {
            channelFilesFolder = Application.persistentDataPath + "/c/";

            chatEncryptor = new ChatHistoryEncryptor(localUserWalletAddress);
            chatSerializer = new ChatHistorySerializer(messageFactory);

            this.chatHistory = chatHistory;

            this.localUserWalletAddress = localUserWalletAddress;
            chatHistory.MessageAdded += OnChatHistoryMessageAddedAsync;
            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ChannelCleared += OnChatHistoryChannelCleared;

            userFilesFolder = channelFilesFolder + chatEncryptor.StringToFileName(localUserWalletAddress) + "/";
            userConversationSettingsFile = userFilesFolder + chatEncryptor.StringToFileName("Settings");

            UniTask.RunOnThreadPool(() => ProcessQueueAsync(cts.Token)).Forget();
            UniTask.RunOnThreadPool(() => CheckChannelFileTimeoutsAsync(cts.Token)).Forget();
        }

        /// <summary>
        ///
        /// </summary>
        public void LoadAllChannelsWithoutMessages()
        {
            ReportHub.Log(reportData, $"Loading all conversations (not their messages).");

            areAllChannelsLoaded = false;

            if (Directory.Exists(userFilesFolder))
            {
                LoadConversationSettings();

                bool isConversationSettingsPresent = conversationSettings != null;

                List<string> filePaths = conversationSettings?.ConversationFilePaths;

                // If there is no settings file, all stored conversations will be visible
                if (!isConversationSettingsPresent)
                {
                    filePaths = new List<string>(Directory.GetFiles(userFilesFolder));
                    conversationSettings = new UserConversationsSettings(){ ConversationFilePaths = new List<string>(filePaths.Count) };
                }

                for (int i = 0; i < filePaths.Count; ++i)
                {
                    if (filePaths[i] == userConversationSettingsFile)
                        continue;

                    ChannelFile newFile = new ChannelFile();

                    string currentFileName = Path.GetFileName(filePaths[i]);
                    ChatChannel.ChannelId fileChannelId = chatEncryptor.FileNameToChannelId(currentFileName);
                    newFile.Path = filePaths[i];

                    ReportHub.Log(reportData, $"Creating channel for file " + filePaths[i] + " for channel with Id: " + fileChannelId.Id);

                    lock (channelsLocker)
                    {
                        channelFiles.Add(fileChannelId, newFile);
                    }

                    chatHistory.AddOrGetChannel(fileChannelId, ChatChannel.ChatChannelType.User);

                    // All stored conversations will be added to the settings file, when it is not present
                    if(!isConversationSettingsPresent)
                        conversationSettings.ConversationFilePaths.Add(filePaths[i]);

                    ReportHub.Log(reportData, $"Channel with Id " + fileChannelId.Id + " created.");
                }

                if (!isConversationSettingsPresent)
                    StoreConversationSettings();

                areAllChannelsLoaded = true;

                ReportHub.Log(reportData, $"All conversations loaded.");
            }
            else
                ReportHub.Log(reportData, $"Nothing to load.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="channelId"></param>
        public async UniTask InitializeChannelWithMessagesAsync(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(reportData, $"Initializing conversation with messages for channel: " + channelId.Id);

            // If already reading or if the file did not exist, ignore it
            if(!channelFiles.ContainsKey(channelId) ||
               (channelFiles[channelId].Content != null && channelFiles[channelId].Content.CanRead))
                return;

            messagesBuffer.Clear();
            await ReadMessagesFromFileAsync(channelId, messagesBuffer);

            if (messagesBuffer.Count > 0)
                chatHistory.Channels[channelId].FillChannel(messagesBuffer);

            channelFiles[channelId].IsInitialized = true;

            ReportHub.Log(reportData, $"Conversation initialized.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public bool IsChannelInitialized(ChatChannel.ChannelId channelId) =>
            channelFiles.TryGetValue(channelId, out ChannelFile channelFile) && channelFile.IsInitialized;

        public void Dispose()
        {
            chatHistory.MessageAdded -= OnChatHistoryMessageAddedAsync;
            chatHistory.ChannelAdded -= OnChatHistoryChannelAdded;
            cts.SafeCancelAndDispose();

            // Closes any open file
            foreach (KeyValuePair<ChatChannel.ChannelId, ChannelFile> channelFilePair in channelFiles)
            {
                if(channelFilePair.Value.Content != null)
                    CloseChannelFile(channelFilePair.Key);
            }
        }

        private void LoadConversationSettings()
        {
            ReportHub.Log(reportData, $"Reading conversation settings from " + userConversationSettingsFile);

            try
            {
                if (File.Exists(userConversationSettingsFile))
                {
                    using (FileStream encryptedStream = new FileStream(userConversationSettingsFile, FileMode.Open, FileAccess.Read))
                    {
                        conversationSettings = chatEncryptor.DecryptUserConversationSettings(encryptedStream);
                    }

                    ReportHub.Log(reportData, $"Conversation settings file read from " + userConversationSettingsFile);
                }
                else
                {
                    ReportHub.LogWarning(reportData, "The conversation settings file was not present. Default values will be used.");
                }
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, "An error occurred while loading the conversation history settings. " + e.Message + e.StackTrace);
            }
        }

        private void StoreConversationSettings()
        {
            ReportHub.Log(reportData, $"Storing conversation settings at " + userConversationSettingsFile);

            try
            {
                using (FileStream fileStream = new FileStream(userConversationSettingsFile, FileMode.Create, FileAccess.Write))
                {
                    chatEncryptor.EncryptUserConversationSettings(conversationSettings, fileStream);
                }

                ReportHub.Log(reportData, $"Conversation settings file stored at " + userConversationSettingsFile);
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, "An error occurred while storing the conversation history settings file. " + e.Message + e.StackTrace);
            }
        }

        private void OnChatHistoryChannelAdded(ChatChannel addedChannel)
        {
            if(!areAllChannelsLoaded) // Avoids reacting to itself
                return;

            if (addedChannel.ChannelType == ChatChannel.ChatChannelType.User)
            {
                conversationSettings.ConversationFilePaths.Add(userFilesFolder + chatEncryptor.StringToFileName(addedChannel.Id.Id));
                StoreConversationSettings();
                GetOrCreateChannelFile(addedChannel.Id);
            }
        }

        private async void OnChatHistoryMessageAddedAsync(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            if (destinationChannel.ChannelType == ChatChannel.ChatChannelType.User)
            {
                if (!IsChannelInitialized(destinationChannel.Id))
                {
                    GetOrCreateChannelFile(destinationChannel.Id);
                    await InitializeChannelWithMessagesAsync(destinationChannel.Id);
                }

                lock (queueLocker)
                {
                    if(destinationChannel.ChannelType == ChatChannel.ChatChannelType.User)
                        messagesToProcess.Enqueue(new MessageToProcess(){ DestinationChannelId = destinationChannel.Id, Message = addedMessage});
                }
            }
        }

        private async UniTaskVoid CheckChannelFileTimeoutsAsync(CancellationToken ct)
        {
            const float TIMEOUT = 5.0f;

            while (!ct.IsCancellationRequested)
            {
                lock (channelsLocker)
                {
                    foreach (KeyValuePair<ChatChannel.ChannelId, ChannelFile> channelFilePair in channelFiles)
                    {
                        if(channelFilePair.Value.Content != null && Time.realtimeSinceStartup - channelFilePair.Value.LastMessageTime >= TIMEOUT)
                            CloseChannelFile(channelFilePair.Key);
                    }
                }

                await UniTask.Delay(200, DelayType.Realtime, PlayerLoopTiming.Update, ct); // To avoid traversing the collection too often
            }
        }

        private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                while (messagesToProcess.Count > 0 && !ct.IsCancellationRequested)
                {
                    MessageToProcess messageToProcess;

                    lock (queueLocker)
                    {
                        messageToProcess = messagesToProcess.Dequeue();
                    }

                    AppendMessageToFile(messageToProcess.DestinationChannelId, messageToProcess.Message);

                    await UniTask.Yield();
                }

                await UniTask.Yield();
            }
        }

        private ChannelFile OpenChannelFileForWriting(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(reportData, $"Opening channel file (writing) for " + channelId.Id);

            ChannelFile channelFile = GetOrCreateChannelFile(channelId);

            if(channelFile == null)
                return null;

            try
            {
                if (channelFile.Content == null)
                    channelFile.Content = chatEncryptor.CreateEncryptionStreamWriter(new FileStream(channelFile.Path, FileMode.Append));

                ReportHub.Log(reportData, $"Channel file opened (writing) for " + channelId.Id);
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, $"An error occurred while opening the channel file for writing: {channelFile.Path} for Id: {channelId.Id}. {e.Message} {e.StackTrace}");

                channelFile.Content?.Dispose();
            }

            return channelFile;
        }

        private ChannelFile OpenChannelFileForReading(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(reportData, $"Opening channel file (reading) for " + channelId.Id);

            ChannelFile channelFile = GetOrCreateChannelFile(channelId);

            if(channelFile == null)
                return null;

            long fileSize = new FileInfo(userFilesFolder + chatEncryptor.StringToFileName(channelId.Id)).Length;

            if (fileSize > 0)
            {
                try
                {
                    if(channelFile.Content == null)
                        channelFile.Content = chatEncryptor.CreateDecryptionStreamReader(new FileStream(channelFile.Path, FileMode.Open, FileAccess.Read));

                    ReportHub.Log(reportData, $"Channel file opened (reading) for " + channelId.Id);
                }
                catch (Exception e)
                {
                    ReportHub.LogError(reportData, $"An error occurred while opening the channel file for reading: {channelFile.Path} for Id: {channelId.Id}. {e.Message} {e.StackTrace}");

                    channelFile.Content?.Dispose();
                }
            }

            return channelFile;
        }

        private void CloseChannelFile(ChatChannel.ChannelId channelId)
        {
            ChannelFile channelFile;

            if (!channelFiles.TryGetValue(channelId, out channelFile) || channelFile.Content == null)
            {
                ReportHub.LogWarning(reportData, $"Trying to close a channel file that was not open for Id: {channelId.Id}. The call will be ignored.");
                return;
            }

            try
            {
                ReportHub.Log(reportData, $"Closing channel file {channelFile.Path} for Id {channelId.Id}");

                channelFile.Content.Dispose();
                channelFile.Content = null;

                ReportHub.Log(reportData, $"Channel file closed for " + channelId.Id);
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, $"An error occurred while closing the channel file: {channelFile.Path} for Id: {channelId.Id}. {e.Message} {e.StackTrace}");

                channelFile.Content = null;
            }
        }

        private void AppendMessageToFile(ChatChannel.ChannelId channelId, ChatMessage messageToAppend)
        {
            ReportHub.Log(reportData, $"Appending message to file. Message: " + messageToAppend.Message);

            ChannelFile channelFile = OpenChannelFileForWriting(channelId);
            channelFile.LastMessageTime = Time.realtimeSinceStartup;

            if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.User)
                chatSerializer.AppendPrivateConversationMessage(messageToAppend, channelFile.Content);

            channelFile.IsInitialized = true; // It could be the first message to be written to the file, so it makes sure the channel is marked as initialized

            ReportHub.Log(reportData, $"Message appended to file. Message: " + messageToAppend.Message);
        }

        private async UniTask ReadMessagesFromFileAsync(ChatChannel.ChannelId channelId, List<ChatMessage> messages)
        {
            ReportHub.Log(reportData, $"Reading messages from file for " + channelId.Id);

            ChannelFile channelFile = OpenChannelFileForReading(channelId);
            long fileSize = new FileInfo(userFilesFolder + chatEncryptor.StringToFileName(channelId.Id)).Length;

            if (fileSize > 0)
            {
                if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.User)
                    await chatSerializer.ReadAllPrivateConversationMessagesAsync(channelFile.Content, localUserWalletAddress, channelId.Id, messages, cts.Token);

                channelFile.IsInitialized = true;

                CloseChannelFile(channelId);
            }
            else
            {
                ReportHub.Log(reportData, $"The file was empty.");
            }

            ReportHub.Log(reportData, $"Messages read from file for " + channelId.Id);
        }

        private ChannelFile GetOrCreateChannelFile(ChatChannel.ChannelId channelId)
        {
            ChannelFile channelFile = null;

            try
            {
                // Makes sure the channels folder exists
                if (!Directory.Exists(channelFilesFolder))
                {
                    ReportHub.Log(reportData, "Creating channels directory at " + channelFilesFolder);
                    Directory.CreateDirectory(channelFilesFolder);
                    ReportHub.Log(reportData, "Channels directory created at " + channelFilesFolder);
                }

                // Makes sure the user folder and the conversation settings file exist
                if (!Directory.Exists(userFilesFolder))
                {
                    ReportHub.Log(reportData, "Creating user directory at " + userFilesFolder);
                    Directory.CreateDirectory(userFilesFolder);
                    ReportHub.Log(reportData, "User directory created at " + userFilesFolder);

                    StoreConversationSettings();
                }

                if (!channelFiles.TryGetValue(channelId, out channelFile))
                {
                    // Registers the channel if it does not exist
                    channelFile = new ChannelFile()
                    {
                        LastMessageTime = float.MaxValue,
                        Path = userFilesFolder + chatEncryptor.StringToFileName(channelId.Id)
                    };

                    lock (channelsLocker)
                    {
                        channelFiles.Add(channelId, channelFile);
                    }
                }

                if (!File.Exists(channelFile.Path))
                {
                    ReportHub.Log(reportData, "Creating channel file at " + channelFile.Path);
                    File.Create(channelFile.Path).Dispose();
                    ReportHub.Log(reportData, "Channel file created at " + channelFile.Path);
                }
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, "Error while trying to get or create the channel file. " + e.Message + e.StackTrace);
            }

            return channelFile;
        }

        private void OnChatHistoryChannelCleared(ChatChannel clearedChannel)
        {
            ChannelFile channelFile;
            channelFiles.TryGetValue(clearedChannel.Id, out channelFile);

            if (!File.Exists(channelFile.Path))
            {
                ReportHub.Log(reportData, $"Clearing channel file at " + channelFile.Path);
                File.Create(channelFile.Path).Dispose();
                ReportHub.Log(reportData, $"Channel file cleared at " + channelFile.Path);
            }
        }

        private void OnChatHistoryChannelRemoved(ChatChannel.ChannelId removedChannel)
        {
            ChannelFile channelFile = channelFiles[removedChannel];

            lock (channelsLocker)
            {
                channelFiles.Remove(removedChannel);
            }

            conversationSettings.ConversationFilePaths.Remove(channelFile.Path);
            StoreConversationSettings();

            channelFile.LastMessageTime = float.MaxValue;
            channelFile.IsInitialized = false;
            channelFile.Path = null;
            channelFile.Content?.Dispose();
            channelFile.Content = null;
        }
    }
}
