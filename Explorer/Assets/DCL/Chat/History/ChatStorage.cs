using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        [Serializable]
        public class UserConversationsSettings
        {
            [SerializeField]
            public string[] ConversationFileNames; // Already sorted, a conversation is closed if it does not appear here
        }

        private class ChannelFile
        {
            public string EncryptedChannelId;
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
        private readonly byte[] encryptionKey;

        private readonly Dictionary<ChatChannel.ChannelId, ChannelFile> channelFiles = new Dictionary<ChatChannel.ChannelId, ChannelFile>();
        private readonly Queue<MessageToProcess> messagesToProcess = new();

        private readonly CancellationTokenSource cts = new();
        private readonly object queueLocker = new object();

//        private readonly CryptoStream channelIdEncryptorStream;
//        private readonly CryptoStream channelIdDecryptorStream;
//        private readonly MemoryStream auxiliarChannelIdStream = new MemoryStream(2048); // Enough to not need resizing
        private readonly AesCryptoServiceProvider cryptoProvider = new AesCryptoServiceProvider ();
        private byte[] channelIdEncryptionBuffer = new byte[2048]; // Enough to not need resizing
        private byte[] channelIdEncryptionBuffer2 = new byte[8048]; // Enough to not need resizing

//        private readonly StreamWriter auxiliarChannelIdWriter;
        private readonly List<ChatMessage> messagesBuffer = new List<ChatMessage>();

        private readonly string channelFilesFolder;
        private readonly string userFilesFolder;
        private readonly string userConversationSettingsFile;

        /// <summary>
        ///
        /// </summary>
        public UserConversationsSettings ConversationsSettings { get; private set; }

        public ChatStorage(IChatHistory chatHistory, string encryptionKey)
        {
            channelFilesFolder = Application.persistentDataPath + "/channels/";

            this.encryptionKey = HashKey.FromString(encryptionKey).Hash.Memory;
            this.chatHistory = chatHistory;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;
            chatHistory.ChannelAdded += OnChatHistoryChannelAdded;

            // Encryption initialization
            cryptoProvider.Key = this.encryptionKey;
            cryptoProvider.IV = this.encryptionKey.AsSpan(0, 16).ToArray();
//            channelIdEncryptorStream = new CryptoStream(auxiliarChannelIdStream, cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write);
//            channelIdDecryptorStream = new CryptoStream(auxiliarChannelIdStream, cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read);

 //           auxiliarChannelIdWriter = new StreamWriter(auxiliarChannelIdStream);

            userFilesFolder = channelFilesFolder + StringToFileName(encryptionKey) + "/";
            userConversationSettingsFile = userFilesFolder + StringToFileName("SettingsSettingsSettingsSettingsSettingsSettingsSettings");

            UniTask.RunOnThreadPool(() => ProcessQueueAsync(cts.Token)).Forget();
            UniTask.RunOnThreadPool(() => CheckChannelFileTimeoutsAsync(cts.Token)).Forget();

            string yeah = StringToFileName(encryptionKey);
            ChatChannel.ChannelId id = FileNameToChannelId(yeah);
            string yeah2 = StringToFileName(encryptionKey);
        }

        /// <summary>
        ///
        /// </summary>
        public void LoadAllChannelsWithoutMessages()
        {
            if (Directory.Exists(userFilesFolder))
            {
                LoadConversationSettings();

                ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.User;

                string[] fileNames = ConversationsSettings.ConversationFileNames;

                if (fileNames == null || fileNames.Length == 0)
                    fileNames = Directory.GetFiles(userFilesFolder);

                for (int i = 0; i < fileNames.Length; ++i)
                {
                    if (fileNames[i] == userConversationSettingsFile)
                        continue;

                    ChannelFile newFile = new ChannelFile();

                    string currentFileName = Path.GetFileName(fileNames[i]);
                    ChatChannel.ChannelId fileChannelId = FileNameToChannelId(currentFileName);
                    newFile.EncryptedChannelId = currentFileName;

                    ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Creating channel for file " + fileNames[i] + " for channel with Id: " + fileChannelId.Id);

                    channelFiles.Add(fileChannelId, newFile);
                    //chatHistory.AddChannel(channelType, fileChannelId.Id);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="channelId"></param>
        public void InitializeChannelWithMessages(ChatChannel.ChannelId channelId)
        {
            messagesBuffer.Clear();
            ReadMessagesFromFile(channelId, messagesBuffer);

            chatHistory.Channels[channelId].FillChannel(messagesBuffer);
            channelFiles[channelId].IsInitialized = true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public bool IsChannelInitialized(ChatChannel.ChannelId channelId) =>
            channelFiles[channelId].IsInitialized;

        private void LoadConversationSettings()
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Reading conversation settings from " + userConversationSettingsFile);

            try
            {
                if (File.Exists(userConversationSettingsFile))
                {
                    using (CryptoStream fileStream = new CryptoStream(new FileStream(userConversationSettingsFile, FileMode.Open, FileAccess.Read), cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                            {
                                JObject jsonObject = (JObject)JToken.ReadFrom(jsonReader);
                                ConversationsSettings = JsonConvert.DeserializeObject<UserConversationsSettings>(jsonObject.ToString());
                            }
                        }
                    }

                    ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Conversation settings file read from " + userConversationSettingsFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {

            }
        }

        private void StoreConversationSettings()
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Storing conversation settings at " + userConversationSettingsFile);

            try
            {
                using (CryptoStream fileStream = new CryptoStream(new FileStream(userConversationSettingsFile, FileMode.CreateNew, FileAccess.Write), cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fileStream))
                    {
                        if (ConversationsSettings == null)
                        {
                            ConversationsSettings = new UserConversationsSettings
                                {
                                    ConversationFileNames = Array.Empty<string>(),
                                };
                        }

                        string serializedSettings = JsonConvert.SerializeObject(ConversationsSettings);
                        streamWriter.Write(serializedSettings);
                    }
                }

                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Conversation settings file stored at " + userConversationSettingsFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {

            }
        }

        private void OnChatHistoryChannelAdded(ChatChannel addedChannel)
        {
            OpenChannelFileForReading(addedChannel.Id);
        }

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            lock (queueLocker)
            {
                if(destinationChannel.ChannelType == ChatChannel.ChatChannelType.User)
                    messagesToProcess.Enqueue(new MessageToProcess(){ DestinationChannelId = destinationChannel.Id, Message = addedMessage});
            }
        }

        private async UniTaskVoid CheckChannelFileTimeoutsAsync(CancellationToken ct)
        {
            const float TIMEOUT = 5.0f;

            while (!ct.IsCancellationRequested)
            {
                foreach (KeyValuePair<ChatChannel.ChannelId, ChannelFile> channelFilePair in channelFiles)
                {
                    if(channelFilePair.Value.Content != null && Time.realtimeSinceStartup - channelFilePair.Value.LastMessageTime >= TIMEOUT)
                        CloseChannelFile(channelFilePair.Key);
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

        private string StringToFileName(string str)
        {
            string result = null;

            Array.Clear(channelIdEncryptionBuffer, 0, channelIdEncryptionBuffer.Length);

            using (MemoryStream auxiliarChannelIdStream = new MemoryStream(channelIdEncryptionBuffer))
            {
                using (CryptoStream channelIdEncryptorStream = new CryptoStream(auxiliarChannelIdStream, cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (StreamWriter writer = new StreamWriter(channelIdEncryptorStream))
                    {
                        writer.Write(str);
                        writer.Flush();
                    }
                }
            }

            int length = 0;

            for (int i = 0; i < channelIdEncryptionBuffer.Length; ++i)
            {
                if(channelIdEncryptionBuffer[i] == 0)
                    break;

                length++;
            }

            result = Convert.ToBase64String(channelIdEncryptionBuffer, 0, length);
            result = result.Replace('/', '_');

            return result;
        }

        private ChatChannel.ChannelId FileNameToChannelId(string fileName)
        {
            ChatChannel.ChannelId result;
            fileName = fileName.Replace('_', '/');
            byte[] fileNameAes = Convert.FromBase64String(fileName);

            using (MemoryStream auxiliarChannelIdStream = new MemoryStream(fileNameAes))
            {
                using (CryptoStream channelIdDecryptorStream = new CryptoStream(auxiliarChannelIdStream, cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new(channelIdDecryptorStream))
                    {
                        string id = srDecrypt.ReadToEnd();
                        result = new ChatChannel.ChannelId(id);
                    }
                }
            }

            return result;
        }

        private ChannelFile OpenChannelFileForWriting(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Opening channel file (writing) for " + channelId.Id);

            ChannelFile channelFile = GetOrCreateChannelFile(channelId);

            try
            {
                if(channelFile.Content == null)
                    channelFile.Content = new CryptoStream(new FileStream(userFilesFolder + channelFile.EncryptedChannelId, FileMode.Append),
                                                        cryptoProvider.CreateEncryptor(),
                                                                CryptoStreamMode.Write);
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Channel file opened (writing) for " + channelId.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {

            }

            return channelFile;
        }

        private ChannelFile OpenChannelFileForReading(ChatChannel.ChannelId channelId)
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Opening channel file (reading) for " + channelId.Id);

            ChannelFile channelFile = GetOrCreateChannelFile(channelId);

            try
            {
                if(channelFile.Content == null)
                    channelFile.Content = new CryptoStream(new FileStream(userFilesFolder + channelFile.EncryptedChannelId, FileMode.Open, FileAccess.Read),
                                                        cryptoProvider.CreateDecryptor(),
                                                                CryptoStreamMode.Read);

                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Channel file opened (reading) for " + channelId.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {

            }

            return channelFile;
        }

        private void CloseChannelFile(ChatChannel.ChannelId channelId)
        {
            ChannelFile channelFile;

            if (!channelFiles.TryGetValue(channelId, out channelFile) || channelFile.Content == null)
                return;

            try
            {
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Closing channel file for " + channelId.Id);

                channelFile.Content.Close();
                channelFile.Content.Dispose();
                channelFile.Content = null;

                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Channel file closed for " + channelId.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {

            }
        }

        private void AppendMessageToFile(ChatChannel.ChannelId channelId, ChatMessage messageToAppend)
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Appending message to file. Message: " + messageToAppend.Message);

            ChannelFile channelFile = OpenChannelFileForWriting(channelId);
            channelFile.LastMessageTime = Time.realtimeSinceStartup;

            channelFile.Content.Write(Encoding.UTF8.GetBytes($"{(messageToAppend.SentByOwnUser ? "T": "F")},{messageToAppend.Message}\n")); // TODO: Reuse a string builder?

            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Message appended to file. Message: " + messageToAppend.Message);
        }

        private void ReadMessagesFromFile(ChatChannel.ChannelId channelId, List<ChatMessage> messages)
        {
            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Reading messages from file for " + channelId.Id);

            ChannelFile channelFile = OpenChannelFileForReading(channelId);
            string fullFileContent;

            using (StreamReader reader = new StreamReader(channelFile.Content))
            {
                fullFileContent = reader.ReadToEnd();
            }

            using (StreamReader reader = new StreamReader(fullFileContent))
            {
                while(!reader.EndOfStream)
                {
                    string currentLine = reader.ReadLine();

                    string[] values = currentLine.Split(',');

                    ChatMessage newMessage;

                    // TODO: Detect channel type
                    // If private conversation:
                    newMessage = new ChatMessage(values[1], values[0] == "T");
                    messages.Add(newMessage);
                }
            }

            CloseChannelFile(channelId);

            ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Messages read from file for " + channelId.Id);
        }

        public void Dispose()
        {
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;
            chatHistory.ChannelAdded -= OnChatHistoryChannelAdded;
            cts.SafeCancelAndDispose();

            // Closes any open file
            foreach (KeyValuePair<ChatChannel.ChannelId, ChannelFile> channelFilePair in channelFiles)
            {
                if(channelFilePair.Value.Content != null)
                    CloseChannelFile(channelFilePair.Key);
            }
        }

        private ChannelFile GetOrCreateChannelFile(ChatChannel.ChannelId channelId)
        {
            // Makes sure the channels folder exists
            if (!Directory.Exists(channelFilesFolder))
            {
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), "Creating channels directory at " + channelFilesFolder);
                Directory.CreateDirectory(channelFilesFolder);
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), "Channels directory created at " + channelFilesFolder);
            }

            // Makes sure the user folder and the conversation settings file exist
            if (!Directory.Exists(userFilesFolder))
            {
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), "Creating user directory at " + userFilesFolder);
                Directory.CreateDirectory(userFilesFolder);
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), "User directory created at " + userFilesFolder);

                StoreConversationSettings();
            }

            ChannelFile channelFile;

            if (!channelFiles.TryGetValue(channelId, out channelFile))
            {
                // Registers the channel if it does not exist
                channelFile = new ChannelFile()
                {
                    LastMessageTime = Time.realtimeSinceStartup,
                    EncryptedChannelId = StringToFileName(channelId.Id)
                };
            }

            string filePath = userFilesFolder + channelFile.EncryptedChannelId;

            if (!File.Exists(filePath))
            {
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Creating channel file at " + filePath);
                File.Create(filePath).Close();
                ReportHub.Log(new ReportData(ReportCategory.CHAT_HISTORY), $"Channel file created at " + filePath);
            }

            return channelFile;
        }
    }
}
