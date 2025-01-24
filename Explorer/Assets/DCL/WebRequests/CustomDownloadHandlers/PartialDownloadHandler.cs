using DCL.WebRequests.PartialDownload;
using System;
using System.Buffers;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class PartialDownloadHandler : DownloadHandlerScript
    {
        private readonly PartialDownloadingData partialData;
        private readonly ArrayPool<byte> buffersPool;
        private int bufferPointer = 0;

        public PartialDownloadHandler(ref PartialDownloadingData partialData, byte[] preallocatedBuffer, ArrayPool<byte> buffersPool) : base(preallocatedBuffer)
        {
            this.partialData = partialData;
            this.buffersPool = buffersPool;
        }

        protected override void CompleteContent()
        {

        }

        protected override bool ReceiveData(byte[] receivedData, int dataLength)
        {
            if (dataLength == 0)
                return false; // No data received

            if (partialData.DataBuffer == null)
            {
                partialData.DataBuffer = buffersPool.Rent(dataLength);
            }
            else if(partialData.DataBuffer.Length < bufferPointer + dataLength)
            {
                var newBuffer = buffersPool.Rent(partialData.DataBuffer.Length + dataLength);
                Array.Copy(partialData.DataBuffer, newBuffer, partialData.DataBuffer.Length);
                buffersPool.Return(partialData.DataBuffer, true);
                partialData.DataBuffer = newBuffer;
            }

            try
            {
                Array.Copy(receivedData, 0, partialData.DataBuffer, bufferPointer, dataLength);
                bufferPointer += dataLength;
                partialData.downloadedSize += dataLength;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error writing data: {ex.Message}");
                return false;
            }
        }
    }
}
