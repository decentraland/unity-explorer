using DCL.Optimization.Hashing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace DCL.Chat.History
{
    /// <summary>
    ///
    /// </summary>
    internal class ChatHistoryEncryptor
    {
        private AesCryptoServiceProvider cryptoProvider = new AesCryptoServiceProvider ();
        private readonly byte[] channelIdEncryptionBuffer = new byte[256]; // Enough to not need resizing

        /// <summary>
        ///
        /// </summary>
        /// <param name="encryptionKey"></param>
        public ChatHistoryEncryptor(string encryptionKey)
        {
            SetNewEncryptionKey(encryptionKey);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="encryptedStream"></param>
        /// <returns></returns>
        public ChatStorage.UserConversationsSettings DecryptUserConversationSettings(Stream encryptedStream)
        {
            ChatStorage.UserConversationsSettings result;

            using (CryptoStream fileStream = new CryptoStream(encryptedStream, cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read))
            {
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                    {
                        JObject jsonObject = (JObject)JToken.ReadFrom(jsonReader);
                        result = JsonConvert.DeserializeObject<ChatStorage.UserConversationsSettings>(jsonObject.ToString());
                    }
                }
            }

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="conversationsSettingsToEncrypt"></param>
        /// <param name="outputStream"></param>
        public void EncryptUserConversationSettings(ChatStorage.UserConversationsSettings conversationsSettingsToEncrypt, Stream outputStream)
        {
            using (CryptoStream fileStream = new CryptoStream(outputStream, cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write))
            {
                using (StreamWriter streamWriter = new StreamWriter(fileStream))
                {
                    string serializedSettings = JsonConvert.SerializeObject(conversationsSettingsToEncrypt);
                    streamWriter.Write(serializedSettings);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string StringToFileName(string str)
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

            // Note: This is the only way I know to get the actual length of the written bytes
            int length = 0;

            for (int i = 0; i < channelIdEncryptionBuffer.Length; ++i)
            {
                if(channelIdEncryptionBuffer[i] == 0)
                    break;

                length++;
            }

            // Converted to Base64 to avoid characters forbidden by the file system
            result = Convert.ToBase64String(channelIdEncryptionBuffer, 0, length);
            result = result.Replace('/', '_');

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public ChatChannel.ChannelId FileNameToChannelId(string fileName)
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
                        // Removes the padding added by the AES algorithm
                        id = id.Replace("\0", string.Empty); // TODO: This will change according to the chosen padding mode
                        result = new ChatChannel.ChannelId(id);
                    }
                }
            }

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="outputStream"></param>
        /// <returns></returns>
        public Stream CreateEncryptionStreamWriter(Stream outputStream) =>
            new CryptoStream(outputStream, cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write);

        /// <summary>
        ///
        /// </summary>
        /// <param name="inputStream"></param>
        /// <returns></returns>
        public Stream CreateDecryptionStreamReader(Stream inputStream) =>
            new CryptoStream(inputStream, cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read);

        /// <summary>
        ///
        /// </summary>
        /// <param name="newEncryptionKey"></param>
        public void SetNewEncryptionKey(string newEncryptionKey)
        {
            byte[] hashedEncryptionKey = HashKey.FromString(newEncryptionKey).Hash.Memory;

            cryptoProvider.Key = hashedEncryptionKey;
            cryptoProvider.IV = hashedEncryptionKey.AsSpan(0, 16).ToArray();
            cryptoProvider.Mode = CipherMode.ECB; // TODO: USE CBC
            cryptoProvider.Padding = PaddingMode.Zeros; // TODO: USE PKCS7
        }
    }
}
