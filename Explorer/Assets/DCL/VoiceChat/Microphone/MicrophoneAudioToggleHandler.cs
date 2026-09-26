using DCL.Audio;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Plays an audio cue on every transition of a boolean reactive source —
    ///     <paramref name="onAudio"/> when it flips to <c>true</c>, <paramref name="offAudio"/> when it flips to <c>false</c>.
    ///     Source is generic so the handler can be reused for Community (mic enabled/disabled) and Nearby (OPEN_MIC enter/exit).
    /// </summary>
    public class MicrophoneAudioToggleHandler : IDisposable
    {
        private readonly AudioClipConfig offAudio;
        private readonly AudioClipConfig onAudio;
        private readonly IDisposable stateSubscription;

        public MicrophoneAudioToggleHandler(IReadonlyReactiveProperty<bool> source, AudioClipConfig offAudio, AudioClipConfig onAudio)
        {
            this.offAudio = offAudio;
            this.onAudio = onAudio;

            stateSubscription = source.Subscribe(OnStateChanged);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
        }

        private void OnStateChanged(bool isOn)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn ? onAudio : offAudio);
        }
    }
}
