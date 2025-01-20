using DCL.WebRequests.PartialDownload;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class PartialDownloadHandler : DownloadHandlerScript
    {
        private readonly PartialDownloadingData partialData;
        private int bufferPointer = 0;

        public PartialDownloadHandler(ref PartialDownloadingData partialData, byte[] preallocatedBuffer) : base(preallocatedBuffer)
        {
            this.partialData = partialData;
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
                partialData.DataBuffer = new byte[dataLength];
            }
            else if(partialData.DataBuffer.Length < bufferPointer + dataLength)
            {
                var newBuffer = new byte[partialData.DataBuffer.Length + dataLength];
                Array.Copy(partialData.DataBuffer, newBuffer, partialData.DataBuffer.Length);
                partialData.DataBuffer = newBuffer;
            }

            try
            {
                for(var i = 0; i < dataLength; i++)
                {
                    partialData.DataBuffer[bufferPointer] = receivedData[i];
                    bufferPointer++;
                }
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
