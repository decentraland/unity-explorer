using System;
using UnityEngine;

namespace Plugins.NativeAudioAnalysis
{
    public interface IComponentWithAudioFrameBuffer
    {
        /// <summary>
        ///     Starts capturing the component's audio output. False when there is no output to capture yet.
        /// </summary>
        bool TryAttachAudioFrameBuffer();

        /// <summary>
        ///     Copies the latest captured frame into <paramref name="destination" />. False when no new frame is pending.
        /// </summary>
        bool TryConsumeAudioFrame(Span<float> destination, out int channels, out int sampleCount);

        /// <summary>
        ///     Stops capturing and releases whatever the attach created.
        /// </summary>
        void DetachAudioFrameBuffer();
    }

    /// <summary>
    ///     Use ThreadSafeLastAudioFrameReadFilter because it has to be attached to the same GameObject.
    ///     But GameObject is owned by AudioSource MonoBehavour in practice, and gets repooled with it.
    ///     To avoid LifeCycle complications ThreadSafeLastAudioFrameReadFilter is referenced directly and owned by AudioSourceComponent.
    ///     MonoBehaviour cannot be easily pooled because the ownership issue arise.
    ///     AudioSource and ThreadSafeLastAudioFrameReadFilter share the same GameObject.
    /// </summary>
    public struct ThreadSafeLastAudioFrameReadFilterWrap
    {
        // Is ok to have a default ctor of struct, inits always NULL field
        private ThreadSafeLastAudioFrameReadFilter? lastAudioFrameReadFilter;

        public void Attach(AudioSource audioSource)
        {
            if (lastAudioFrameReadFilter != null)
                return;

            lastAudioFrameReadFilter = audioSource.gameObject.AddComponent<ThreadSafeLastAudioFrameReadFilter>();
        }

        public bool TryConsume(Span<float> destination, out int channels, out int sampleCount)
        {
            if (lastAudioFrameReadFilter != null)
                return lastAudioFrameReadFilter.Buffer.TryConsume(destination, out channels, out sampleCount);

            channels = 0;
            sampleCount = 0;
            return false;
        }

        public void Detach()
        {
            if (lastAudioFrameReadFilter != null)
            {
                // Can be pooled
                UnityEngine.Object.Destroy(lastAudioFrameReadFilter);
                lastAudioFrameReadFilter = null;
            }
        }
    }
}
