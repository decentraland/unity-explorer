using DCL.Diagnostics;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DCL.Chat.History
{
    /// <summary>
    /// Provides functionality to encrypt / decrypt chat data: channel ids, history file names or history file content.
    /// </summary>
    internal class ChatHistoryEncryptor
    {
        private readonly SHA256 shaEncryptor = SHA256.Create();
        private readonly AesCryptoServiceProvider cryptoProvider = new AesCryptoServiceProvider ();
        private readonly byte[] channelIdEncryptionBuffer = new byte[256]; // Enough to not need resizing

        /// <summary>
        /// Encrypts a string and converts the result to Base64 where slashes are replaced with underscores, so it does not
        /// contain any character that may be rejected by common file systems.
        /// </summary>
        /// <param name="str">Any string, normally file or folder names.</param>
        /// <returns>The encrypted file system-friendly version of the string.</returns>
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

            // When using a cyphertext with blocks the resulting length will be the next multiple of the block size after the length of the original string
            int length = str.Length + cryptoProvider.BlockSize / 8 - (str.Length % (cryptoProvider.BlockSize / 8));

            // Converted to Base64 to avoid characters forbidden by the file system
            result = Convert.ToBase64String(channelIdEncryptionBuffer, 0, length);
            result = result.Replace('/', '_');

            return result;
        }

        /// <summary>
        /// Decrypts a string that was encrypted with <see cref="StringToFileName"/> and creates a channel Id with it.
        /// </summary>
        /// <param name="fileName">An encrypted string (normally a file or folder name).</param>
        /// <returns>A valid channel Id.</returns>
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
        /// Provides a new stream ready to write encrypted bytes into another stream.
        /// </summary>
        /// <param name="outputStream">The stream that will receive the encrypted data.</param>
        /// <returns>The new encrypting stream.</returns>
        public Stream CreateEncryptionStreamWriter(Stream outputStream) =>
            new CryptoStream(outputStream, cryptoProvider.CreateEncryptor(), CryptoStreamMode.Write);

        /// <summary>
        /// Provides a new stream ready to read and decrypt bytes from another stream.
        /// </summary>
        /// <param name="inputStream">The stream whose content is to be decrypted.</param>
        /// <returns>The new decrypting stream.</returns>
        public Stream CreateDecryptionStreamReader(Stream inputStream) =>
            new CryptoStream(inputStream, cryptoProvider.CreateDecryptor(), CryptoStreamMode.Read);

        /// <summary>
        /// Changes how the data will be encrypted / decrypted. If there are encryption / decryption streams in use,
        /// they should be closed and re-created in order to use the new encryption key.
        /// </summary>
        /// <param name="newEncryptionKey">The encryption key to use in new streams.</param>
        public void SetNewEncryptionKey(string newEncryptionKey)
        {
            byte[] hashedEncryptionKey = shaEncryptor.ComputeHash(Encoding.UTF8.GetBytes(newEncryptionKey));

            cryptoProvider.Key = hashedEncryptionKey;
            cryptoProvider.IV = hashedEncryptionKey.AsSpan(0, 16).ToArray();
            cryptoProvider.Mode = CipherMode.ECB; // TODO: USE CBC
            cryptoProvider.Padding = PaddingMode.Zeros; // TODO: USE PKCS7
        }
    }
}
