using DCL.Diagnostics;
using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class PartialDownloadHandler : DownloadHandlerScript
    {
        private readonly ArrayPool<byte> buffersPool;
        private int bufferPointer = 0;
        public byte[]? PartialData;
        public int DownloadedSize;

        public PartialDownloadHandler(byte[] preallocatedBuffer, ArrayPool<byte> buffersPool) : base(preallocatedBuffer)
        {
            this.buffersPool = buffersPool;
        }

        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            if (PartialData == null && contentLength > 0)
            {
                var target = (int)Math.Min(contentLength, (ulong)PartialDownloadingRange.CHUNK_SIZE);

                if (target > 0)
                    PartialData = buffersPool.Rent(target);
            }
        }

        protected override bool ReceiveData(byte[] receivedData, int dataLength)
        {
            if (dataLength == 0)
                return false; // No data received


            if (PartialData == null)
            {
                PartialData = buffersPool.Rent(dataLength);
            }
            else if(PartialData.Length < bufferPointer + dataLength)
            {
                var newSize = Math.Max(PartialData.Length * 2, bufferPointer + dataLength);
                var newBuffer = buffersPool.Rent(newSize);
                Array.Copy(PartialData, newBuffer, bufferPointer);
                buffersPool.Return(PartialData, true);
                PartialData = newBuffer;
            }

            try
            {
                Array.Copy(receivedData, 0, PartialData, bufferPointer, dataLength);
                bufferPointer += dataLength;
                DownloadedSize += dataLength;
                return true;
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Error writing data: {ex.Message}");
                return false;
            }
        }
    }
}
