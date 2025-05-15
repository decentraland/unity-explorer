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
    /// Listens to the activity of the chat history and stores its data in a local disk. It also provides functions to retrieve that data from disk.
    /// Data includes open channels, channel order, channel Id and channel messages.
    /// </summary>
    /// <remarks>
    /// Some technical details:
    ///
    /// - The chat data is stored in C:\Users\your user\AppData\LocalLow\Decentraland\Explorer\c (WINDOWS)
    ///   or ~/Library/Application Support/Decentraland/Explorer/c (MAC).
    /// - The content of all the files is encrypted using AES algorithm.
    /// - The name of each file and folder are encrypted and transformed to Base64.
    /// - There is one folder per account, so the user should be able to log in with different accounts in the same machine.
    /// - There is an encrypted JSON file that stores the open conversations, in order. It is created the first time a conversation is open. In case it is corrupted
    ///   or missing, a new one will be created that includes all the stored chats, even if they were closed in the past.
    /// - There is one encrypted CSV file per conversation, created the first time that conversation receives a message.
    /// - When a message is sent or received, the file of that conversation is open for writing for a period of time, so it is not opening / closing for every message.
    /// - If a conversation is closed, its history remains stored in a file. When it is re-opened, that file is read.
    /// - The only way to erase the history of a conversation is by clearing it.
    /// - Usernames of the chat messages are stored, so when retrieved they will not show the current username, if it was changed.
    /// </remarks>
    public class ChatHistoryStorage : IDisposable
    {
        /// <summary>
        /// A deserialized representation of the file that stores metadata about channels, which where open and in which order they are listed.
        /// </summary>
        [Serializable]
        public class UserConversationsSettings
        {
            /// <summary>
            /// The open conversations, in order of appearance. Any conversation that is no here is considered closed.
            /// </summary>
            [SerializeField]
            public List<string> ConversationFilePaths;
        }

        private class ChannelFile
        {
            public string Path;
            public Stream Content;
            public bool IsInitialized;
            public float LastMessageTime = float.MaxValue;
        }

        private struct MessageToProcess
        {
            public ChatChannel.ChannelId DestinationChannelId;
            public ChatMessage Message;
        }

        private readonly IChatHistory chatHistory;

        private string localUserWalletAddress;

        private readonly Dictionary<ChatChannel.ChannelId, ChannelFile> channelFiles = new Dictionary<ChatChannel.ChannelId, ChannelFile>();
        private readonly Queue<MessageToProcess> messagesToProcess = new();

        private readonly CancellationTokenSource cts = new();
        private readonly object queueLocker = new object();
        private readonly object channelsLocker = new object();

        private readonly List<ChatMessage> messagesBuffer = new List<ChatMessage>();

        private readonly string channelFilesFolder;
        private string userFilesFolder;
        private string userConversationSettingsFile;
        private UserConversationsSettings conversationSettings;

        private bool areAllChannelsLoaded;

        private readonly ChatHistoryEncryptor chatEncryptor;
        private readonly ChatHistorySerializer chatSerializer;

        private readonly ReportData reportData = new ReportData(ReportCategory.CHAT_HISTORY);

        public ChatHistoryStorage(IChatHistory chatHistory, ChatMessageFactory messageFactory, string localUserWalletAddress)
        {
            const string CHAT_HISTORY_FOLDER = "/c/";
            channelFilesFolder = Application.persistentDataPath + CHAT_HISTORY_FOLDER;

            this.chatHistory = chatHistory;
            chatEncryptor = new ChatHistoryEncryptor();
            chatSerializer = new ChatHistorySerializer(messageFactory);

            SetNewLocalUserWalletAddress(localUserWalletAddress);

            chatHistory.MessageAdded += OnChatHistoryMessageAddedAsync;
            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;
            chatHistory.ChannelRemoved += OnChatHistoryChannelRemoved;
            chatHistory.ChannelCleared += OnChatHistoryChannelCleared;

            UniTask.RunOnThreadPool(() => ProcessQueueAsync(cts.Token)).Forget();
            UniTask.RunOnThreadPool(() => CheckChannelFileTimeoutsAsync(cts.Token)).Forget();
        }

        /// <summary>
        /// Reads the user conversation settings file and asks the ChatHistory to create all the (open) channels that appear in
        /// it, in order.
        /// </summary>
        /// <remarks>
        /// If the file is not present or is corrupt, all stored conversations will be considered as open and a new file will
        /// be stored.
        /// </remarks>
        public void LoadAllChannelsWithoutMessages()
        {
            ReportHub.Log(reportData, $"Loading all open conversations (not their messages).");

            areAllChannelsLoaded = false;

            try
            {
                CreateUserFolderIfDoesNotExist();

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
                        // Ignores the user conversation settings file
                        if (filePaths[i] == userConversationSettingsFile)
                            continue;

                        string currentFileName = Path.GetFileName(filePaths[i]);
                        ChatChannel.ChannelId fileChannelId = chatEncryptor.FileNameToChannelId(currentFileName);

                        ChannelFile newFile = new ChannelFile
                            {
                                Path = filePaths[i],
                            };

                        ReportHub.Log(reportData, $"Creating channel for file " + filePaths[i] + " for channel with Id: " + fileChannelId.Id);

                        lock (channelsLocker)
                        {
                            channelFiles.Add(fileChannelId, newFile);
                        }

                        chatHistory.AddOrGetChannel(fileChannelId, ChatChannel.ChatChannelType.USER);

                        // All stored conversations will be added to the settings file, when it is not present
                        if(!isConversationSettingsPresent)
                            conversationSettings.ConversationFilePaths.Add(filePaths[i]);

                        ReportHub.Log(reportData, $"Channel with Id " + fileChannelId.Id + " created.");
                    }

                    if (!isConversationSettingsPresent)
                        StoreConversationSettings();

                    ReportHub.Log(reportData, $"All conversations loaded.");
                }
                else
                    ReportHub.Log(reportData, $"Nothing to load.");

                areAllChannelsLoaded = true;
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, "An error occurred while loading all open conversations. " + e.Message + e.StackTrace);
            }
        }

        /// <summary>
        /// Reads all the messages stored in a local file for a given channel and fills the existing channel in the ChatHistory.
        /// </summary>
        /// <remarks>
        /// If there is no file for the channel or if the channel was already initialized, nothing will be done.
        /// </remarks>
        /// <param name="channelId">The id of the channel that is to be filled.</param>
        public async UniTask InitializeChannelWithMessagesAsync(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(reportData, $"Initializing conversation with messages for channel: " + channelId.Id);

            ChannelFile channelFile;

            // If already reading or if the file did not exist, ignore it
            if (!channelFiles.TryGetValue(channelId, out channelFile) ||
                channelFile.IsInitialized ||
                (channelFile.Content != null && channelFile.Content.CanRead))
            {
                ReportHub.LogWarning(reportData, $"Initialization canceled. The file does not exist or the channel was already initialized: " + channelId.Id);
                return;
            }

            messagesBuffer.Clear();
            await ReadMessagesFromFileAsync(channelId, messagesBuffer);

            if (messagesBuffer.Count > 0)
                chatHistory.Channels[channelId].FillChannel(messagesBuffer);

            channelFile.IsInitialized = true;

            ReportHub.Log(reportData, $"Conversation initialized.");
        }

        /// <summary>
        /// Checks whether a channel has been already initialized or not (<see cref="InitializeChannelWithMessagesAsync"/>).
        /// </summary>
        /// <param name="channelId">The id of the channel.</param>
        /// <returns></returns>
        public bool IsChannelInitialized(ChatChannel.ChannelId channelId) =>
            channelFiles.TryGetValue(channelId, out ChannelFile channelFile) && channelFile.IsInitialized;

        /// <summary>
        /// Replaces the wallet address of the current user, which affects the place and the way files are encrypted.
        /// </summary>
        /// <remarks>
        /// If a user is going to use the chat with a different account, this method must be called.
        /// </remarks>
        /// <param name="newLocalUserWalletAddress">The new wallet address. If it is null or empty, it will be ignored.</param>
        public void SetNewLocalUserWalletAddress(string newLocalUserWalletAddress)
        {
            ReportHub.Log(reportData, "Setting new local user wallet address: " + (newLocalUserWalletAddress ?? string.Empty));

            if (string.IsNullOrEmpty(newLocalUserWalletAddress))
            {
                ReportHub.LogWarning(reportData, "No new wallet address was provided, operation aborted.");
                return;
            }

            lock (channelsLocker)
            {
                if(channelFiles.Count > 0)
                    UnloadAllFiles();
            }

            localUserWalletAddress = newLocalUserWalletAddress;

            chatEncryptor.SetNewEncryptionKey(localUserWalletAddress);

            userFilesFolder = channelFilesFolder + chatEncryptor.StringToFileName(localUserWalletAddress) + "/";
            userConversationSettingsFile = userFilesFolder + chatEncryptor.StringToFileName("UserConversationSettings");

            ReportHub.Log(reportData, "Local user wallet address set: " + newLocalUserWalletAddress);
        }

        /// <summary>
        /// Closes all files and removes their metadata from memory.
        /// </summary>
        public void UnloadAllFiles()
        {
            lock (queueLocker)
            {
                messagesToProcess.Clear();
            }

            lock (channelsLocker)
            {
                foreach (KeyValuePair<ChatChannel.ChannelId, ChannelFile> channelFilePair in channelFiles)
                {
                    ChannelFile channelFile = channelFiles[channelFilePair.Key];

                    if (channelFile.Content != null)
                        CloseChannelFile(channelFilePair.Key);
                }

                channelFiles.Clear();
            }

            conversationSettings = null;
        }

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
                        Stream decryptingStream = chatEncryptor.CreateDecryptionStreamReader(encryptedStream);
                        conversationSettings = chatSerializer.DeserializeUserConversationSettings(decryptingStream);
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
                    Stream encryptingStream = chatEncryptor.CreateEncryptionStreamWriter(fileStream);
                    chatSerializer.SerializeUserConversationSettings(conversationSettings, encryptingStream);
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

            if (addedChannel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                conversationSettings.ConversationFilePaths.Add(userFilesFolder + chatEncryptor.StringToFileName(addedChannel.Id.Id));
                StoreConversationSettings();
                GetOrCreateChannelFile(addedChannel.Id);
            }
        }

        private async void OnChatHistoryMessageAddedAsync(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            if (destinationChannel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                if (!IsChannelInitialized(destinationChannel.Id))
                {
                    GetOrCreateChannelFile(destinationChannel.Id);
                    await InitializeChannelWithMessagesAsync(destinationChannel.Id);
                }

                lock (queueLocker)
                {
                    if(destinationChannel.ChannelType == ChatChannel.ChatChannelType.USER)
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

            lock (channelsLocker)
            {
                if (!channelFiles.TryGetValue(channelId, out channelFile) || channelFile.Content == null)
                {
                    ReportHub.LogWarning(reportData, $"Trying to close a channel file that was not open for Id: {channelId.Id}. The call will be ignored.");
                    return;
                }
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

            if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.USER)
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
                if (chatHistory.Channels[channelId].ChannelType == ChatChannel.ChatChannelType.USER)
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
                CreateUserFolderIfDoesNotExist();

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

        private void CreateUserFolderIfDoesNotExist()
        {
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
                }
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportData, $"An error occurred while creating the user directories. " + e.Message + e.StackTrace);
            }
        }

        private void OnChatHistoryChannelCleared(ChatChannel clearedChannel)
        {
            if (channelFiles.TryGetValue(clearedChannel.Id, out ChannelFile channelFile) &&
                File.Exists(channelFile.Path))
            {
                ReportHub.Log(reportData, $"Clearing channel file at " + channelFile.Path);

                if(channelFile.Content != null)
                    CloseChannelFile(clearedChannel.Id);

                File.Create(channelFile.Path).Dispose();
                ReportHub.Log(reportData, $"Channel file cleared at " + channelFile.Path);
            }
        }

        private void OnChatHistoryChannelRemoved(ChatChannel.ChannelId removedChannel)
        {
            if (!channelFiles.TryGetValue(removedChannel, out var channelFile)) return;

            lock (channelsLocker)
            {
                if (channelFile.Content != null)
                {
                    CloseChannelFile(removedChannel);
                }

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
