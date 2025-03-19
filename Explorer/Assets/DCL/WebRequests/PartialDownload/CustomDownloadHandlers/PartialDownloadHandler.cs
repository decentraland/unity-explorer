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
                var newBuffer = buffersPool.Rent(PartialData.Length + dataLength);
                Array.Copy(PartialData, newBuffer, PartialData.Length);
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
