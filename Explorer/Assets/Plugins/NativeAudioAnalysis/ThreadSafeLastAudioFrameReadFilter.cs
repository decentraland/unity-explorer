using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;

namespace Plugins.NativeAudioAnalysis
{
    public class ThreadSafeLastAudioFrameReadFilter : MonoBehaviour
    {
        // Thread-safe flags
        // 0 = nothing written OR already consumed
        // 1 = new frame available
        private int readyFlag = 0;
        private float[] buffer = Array.Empty<float>();
        private int channels;

        private bool enableRead;
            private int sampleRate ;

        /// <summary>
        /// Main thread tries to consume the latest audio frame. Output is not owned and must be used at place
        /// </summary>
        public bool TryConsume(out float[]? output, out int outChannels, out int outSampleRate) 
        {
            outChannels = channels;
            outSampleRate = sampleRate;
            // Atomically check and clear readyFlag
            bool result = Interlocked.Exchange(ref readyFlag, 0) == 1;
            output = result ? buffer : null;
            return result;
        }

        private void OnEnable() 
        {
            sampleRate = AudioSettings.outputSampleRate;
            enableRead = true;
            Interlocked.Exchange(ref readyFlag, 0);
        }

        private void OnDisable()
        {
            enableRead = false;
            Interlocked.Exchange(ref readyFlag, 0);
        }

        private void OnAudioFilterRead(float[] data, int readChannels) 
        {
            if (!enableRead)
                return;

            // If previous frame wasn't consumed yet then drop this one
            if (Interlocked.CompareExchange(ref readyFlag, 1, 0) != 0)
                return; // Skip; main thread hasn't consumed yet

            // Ensure buffer is preallocated or data.Length is equal.
            if (buffer.Length != data.Length)
                buffer = new float[data.Length]; // One-time resize only

            data.CopyTo(buffer, 0);
            channels = readChannels;
        }
    }
}

