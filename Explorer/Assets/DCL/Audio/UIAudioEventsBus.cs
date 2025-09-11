using CodeLess.Attributes;
using System;

namespace DCL.Audio
{
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class UIAudioEventsBus : IDisposable
    {
        public event Action<AudioClipConfig>? PlayUIAudioEvent;
        public event Action<AudioClipConfig>? PlayContinuousUIAudioEvent;
        public event Action<AudioClipConfig>? StopContinuousUIAudioEvent;
        public event Action<AudioClipConfig, bool>? MuteContinuousUIAudioEvent;

        public void Dispose() { }

        public void SendPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                PlayUIAudioEvent?.Invoke(audioClipConfig);
        }

        public void SendPlayContinuousAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                PlayContinuousUIAudioEvent?.Invoke(audioClipConfig);
        }

        public void SendStopPlayingContinuousAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                StopContinuousUIAudioEvent?.Invoke(audioClipConfig);
        }

        public void SendMuteContinuousAudioEvent(AudioClipConfig audioClipConfig, bool isMuted)
        {
            if(audioClipConfig != null)
                MuteContinuousUIAudioEvent?.Invoke(audioClipConfig, isMuted);
        }

        public PlayAudioScope NewPlayAudioScope(AudioClipConfig config) =>
            new (this, config);

        public readonly struct PlayAudioScope : IDisposable
        {
            private readonly UIAudioEventsBus bus;
            private readonly AudioClipConfig audioClipConfig;

            public PlayAudioScope(UIAudioEventsBus bus, AudioClipConfig audioClipConfig)
            {
                this.bus = bus;
                this.audioClipConfig = audioClipConfig;
                this.bus.SendPlayContinuousAudioEvent(audioClipConfig);
            }

            public void Dispose()
            {
                bus.SendStopPlayingContinuousAudioEvent(audioClipConfig);
            }
        }
    }
}
