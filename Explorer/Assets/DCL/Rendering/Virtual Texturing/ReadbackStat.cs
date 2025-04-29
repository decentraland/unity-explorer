using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualTexture
{
    /// <summary>
    /// Statistics collector for GPU readback operations.
    /// Tracks performance metrics like latency and error counts for asynchronous GPU texture readbacks.
    /// </summary>
    public class ReadbackStat
    {
        // Queue to track frame count of each pending request
        private Queue<int> m_Requests;

        // Total accumulated latency for all processed requests (in frames)
        private int m_LatencyTotal;

        // Total number of requests processed
        private int m_RequestCount;

        /// <summary>
        /// The latency of the most recently completed request (in frames)
        /// </summary>
        public int CurrentLatency { get; private set; }

        /// <summary>
        /// The average latency across all processed requests (in frames)
        /// </summary>
        public float AverageLatency { get; private set; }

        /// <summary>
        /// The highest latency observed for any request (in frames)
        /// </summary>
        public int MaxLatency { get; private set; }

        /// <summary>
        /// The total number of requests that have failed
        /// </summary>
        public int FailedCount { get; private set; }

        /// <summary>
        /// Records the start of a readback request.
        /// Stores the current frame count to calculate latency later.
        /// </summary>
        /// <param name="request">The AsyncGPUReadbackRequest being started</param>
        public void BeginRequest(AsyncGPUReadbackRequest request)
        {
            // Skip statistics collection during first 5 seconds to avoid startup anomalies
            if(Time.realtimeSinceStartup < 5)
                return;

            if (m_Requests == null)
                m_Requests = new Queue<int>();

            m_Requests.Enqueue(Time.frameCount);
        }

        /// <summary>
        /// Records the completion of a readback request.
        /// Calculates latency and updates all statistics.
        /// </summary>
        /// <param name="request">The AsyncGPUReadbackRequest that completed</param>
        /// <param name="success">Whether the request completed successfully</param>
        public void EndRequest(AsyncGPUReadbackRequest request, bool success)
        {
            if(m_Requests == null || m_Requests.Count == 0)
                return;
            
            if (!success)
                FailedCount++;

            // Calculate latency as frame difference between request start and completion
            CurrentLatency = Time.frameCount - m_Requests.Dequeue();
            MaxLatency = Mathf.Max(MaxLatency, CurrentLatency);

            m_LatencyTotal += CurrentLatency;
            m_RequestCount++;

            // Update running average
            AverageLatency = (float)m_LatencyTotal / m_RequestCount;
        }
    }
}