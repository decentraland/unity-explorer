using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatAudioProcessor
    {
        void Reset();

        void ProcessAudio(Span<float> audioData, int sampleRate);
    }
}
