// TRUST_WEBGL_THREAD_SAFETY_FLAG

using UnityEngine;

namespace Plugins.NativeAudioAnalysis
{
    /// <summary>
    ///     Publishes the output of the AudioSource on the same GameObject into a LastAudioFrameBuffer
    ///     through Unity's filter chain.
    /// </summary>
    public class ThreadSafeLastAudioFrameReadFilter : MonoBehaviour
    {
        private bool enableRead;

        public LastAudioFrameBuffer Buffer { get; } = new ();

        private void OnEnable()
        {
            Buffer.SampleRate = AudioSettings.outputSampleRate;
            Buffer.Reset();
            enableRead = true;
        }

        private void OnDisable()
        {
            enableRead = false;
            Buffer.Reset();
        }

// TODO OnAudioFilterRead is not available in WebGL
#if !UNITY_WEBGL
        private void OnAudioFilterRead(float[] data, int readChannels)
        {
            if (!enableRead)
                return;

            Buffer.Write(data, readChannels);
        }
#endif
    }
}
