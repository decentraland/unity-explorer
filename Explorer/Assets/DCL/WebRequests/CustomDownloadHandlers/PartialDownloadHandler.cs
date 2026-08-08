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
            // A fresh handler serves a single 1MB range request, so the (partial-response)
            // body never legitimately exceeds CHUNK_SIZE. Pre-size once from Content-Length
            // so the common case is a single rent with zero regrow copies. Clamp guards a
            // lying/over-reported header; the ReceiveData fallback handles an absent/under-reported one.
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
                // Geometric growth (Content-Length absent or under-reported): caps regrows at
                // ~log2(callbacks) instead of the old arithmetic per-callback rent+full-copy (O(N^2)).
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
