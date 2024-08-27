using System;

namespace DCL.Audio
{
    public interface IUIAudioEventsBus
    {
        event Action<AudioClipConfig>? PlayUIAudioEvent;
        event Action<AudioClipConfig>? PlayContinuousUIAudioEvent;
        event Action<AudioClipConfig>? StopContinuousUIAudioEvent;

        void SendPlayAudioEvent(AudioClipConfig audioClipConfig);

        void SendPlayContinuousAudioEvent(AudioClipConfig audioClipConfig);

        void SendStopPlayingContinuousAudioEvent(AudioClipConfig audioClipConfig);

        UIAudioEventsBus.PlayAudioScope NewPlayAudioScope(AudioClipConfig config);
    }
}
