using UnityEngine;

namespace Plugins.NativeAudioAnalysis
{
    public interface IComponentWithAudioFrameBuffer
    {
        bool TryAttachLastAudioFrameReadFilterOrUseExisting(out ThreadSafeLastAudioFrameReadFilter? output);

        void EnsureLastAudioFrameReadFilterIsRemoved();
    }

    public struct ThreadSafeLastAudioFrameReadFilterWrap
    {
        // Is ok to have a default ctor of struct, inits always NULL field
        private ThreadSafeLastAudioFrameReadFilter? lastAudioFrameReadFilter;

        public bool TryAttachLastAudioFrameReadFilterOrUseExisting(
            AudioSource audioSource,
            out ThreadSafeLastAudioFrameReadFilter? output
        )
        {
            if (lastAudioFrameReadFilter != null)
            {
                output = lastAudioFrameReadFilter;
                return true;
            }


            if (audioSource != null) 
            {
                output = lastAudioFrameReadFilter = audioSource.gameObject.AddComponent<ThreadSafeLastAudioFrameReadFilter>();
                return lastAudioFrameReadFilter != null;
            }

            output = null;
            return false;
        }

        public void EnsureLastAudioFrameReadFilterIsRemoved()
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
