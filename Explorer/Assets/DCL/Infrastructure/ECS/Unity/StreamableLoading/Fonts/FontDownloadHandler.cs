using System;
using System.Text;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Fonts
{
    public class FontDownloadHandler : DownloadHandlerScript
    {
        private const int CHUNK_BYTES = 64 * 1024;

        private readonly int maxBytes;
        private byte[] buffer = Array.Empty<byte>();
        private int length;

        public bool LimitExceeded { get; private set; }

        public FontDownloadHandler(int maxBytes) : base(new byte[CHUNK_BYTES])
        {
            this.maxBytes = maxBytes;
        }

        protected override void ReceiveContentLengthHeader(ulong contentLength) =>
            LimitExceeded |= contentLength > (ulong)maxBytes;

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (LimitExceeded || dataLength > maxBytes - length)
            {
                LimitExceeded = true;
                return false;
            }

            int required = length + dataLength;

            if (required > buffer.Length)
                Array.Resize(ref buffer, Math.Min(maxBytes, Math.Max(required, Math.Max(CHUNK_BYTES, buffer.Length * 2))));

            Buffer.BlockCopy(data, 0, buffer, length, dataLength);
            length = required;
            return true;
        }

        protected override byte[] GetData()
        {
            var result = new byte[length];
            Buffer.BlockCopy(buffer, 0, result, 0, length);
            return result;
        }

        protected override string GetText() =>
            Encoding.UTF8.GetString(buffer, 0, length);
    }
}
