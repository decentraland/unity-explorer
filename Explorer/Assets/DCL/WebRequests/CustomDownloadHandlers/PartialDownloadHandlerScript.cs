using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class PartialDownloadHandlerScript : DownloadHandlerScript
    {
        private readonly UnityWebRequest request;
        private readonly Action<UnityWebRequest, long> onComplete;
        private const int PARTIAL_CHUNK_SIZE = 1024 * 1024;
        private const string RANGE_HEADER = "Range";

        private readonly FileStream fileStream;
        private long downloadedSize;

        public PartialDownloadHandlerScript(UnityWebRequest request, string filePath, Action<UnityWebRequest, long> onComplete, byte[] preallocatedBuffer) : base(preallocatedBuffer)
        {
            this.request = request;
            this.onComplete = onComplete;
            fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            downloadedSize = fileStream.Length;
            fileStream.Seek(downloadedSize, SeekOrigin.Begin);

            request.SetRequestHeader(RANGE_HEADER, DownloadHandlersUtils.GetContentRangeHeaderValue(downloadedSize, downloadedSize + PARTIAL_CHUNK_SIZE - 1));
        }

        public void StartDownload()
        {
            request.SendWebRequest().completed += _ => onComplete?.Invoke(request, downloadedSize);
        }

        protected override void CompleteContent()
        {
            fileStream.Close();
        }

        protected override bool ReceiveData(byte[] receivedData, int dataLength)
        {
            if (receivedData == null || dataLength == 0)
                return false; // No data received

            try
            {
                fileStream.Write(receivedData, 0, dataLength);
                downloadedSize += dataLength;
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
