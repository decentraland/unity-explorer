using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Plugins.NativeAudioAnalysis
{
    public interface IComponentWithAudioFrameBuffer
    {
        bool TryGetOrAttachAudioFrameBuffer([NotNullWhen(true)] out LastAudioFrameBuffer? output);

        void EnsureAudioFrameBufferIsRemoved();
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

        public bool TryAttachLastAudioFrameReadFilterOrUseExisting(
            AudioSource audioSource,
            [NotNullWhen(true)] out LastAudioFrameBuffer? output
        )
        {
            if (lastAudioFrameReadFilter != null)
            {
                output = lastAudioFrameReadFilter.Buffer;
                return true;
            }

            if (audioSource != null)
            {
                lastAudioFrameReadFilter = audioSource.gameObject.AddComponent<ThreadSafeLastAudioFrameReadFilter>();
                output = lastAudioFrameReadFilter.Buffer;
                return true;
            }

            output = null;
            return false;
        }

        public void EnsureLastAudioFrameReadFilterIsRemoved()
        {
            if (lastAudioFrameReadFilter != null)
            {
                // Can be pooled
                Object.Destroy(lastAudioFrameReadFilter);
                lastAudioFrameReadFilter = null;
            }
        }
    }
}
