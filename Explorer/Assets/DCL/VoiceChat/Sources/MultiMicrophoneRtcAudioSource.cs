using LiveKit;
using LiveKit.Audio;
using LiveKit.Internal;
using RichTypes;
using System;

namespace DCL.VoiceChat.Sources
{
    public class MultiMicrophoneRtcAudioSource : IRtcAudioSource, IDisposable
    {
        private MicrophoneRtcAudioSource currentSource;
        private bool isPlaying;

        private MultiMicrophoneRtcAudioSource(MicrophoneRtcAudioSource source)
        {
            currentSource = source;
        }

        public static Result<MultiMicrophoneRtcAudioSource> New(string? microphoneName = null)
        {
            Result<MicrophoneRtcAudioSource> source = MicrophoneRtcAudioSource.New(microphoneName);

            return source.Success
                ? Result<MultiMicrophoneRtcAudioSource>.SuccessResult(new MultiMicrophoneRtcAudioSource(source.Value))
                : Result<MultiMicrophoneRtcAudioSource>.ErrorResult($"Cannot create {nameof(MultiMicrophoneRtcAudioSource)}: {source.ErrorMessage}");
        }

        public void Dispose()
        {
            currentSource.Dispose();
        }

        public Result SwitchMicrophone(string microphoneName)
        {
            currentSource.Dispose();
            Result<MicrophoneRtcAudioSource> newSourceResult = MicrophoneRtcAudioSource.New(microphoneName);

            if (newSourceResult.Success)
            {
                currentSource = newSourceResult.Value;

                if (isPlaying)
                    currentSource.Start();

                return Result.SuccessResult();
            }

            return Result.ErrorResult(newSourceResult.ErrorMessage!);
        }

        FfiHandle IRtcAudioSource.BorrowHandle() =>
            throw new InvalidOperationException();

        public void Start()
        {
            if (isPlaying)
                return;

            currentSource.Start();
            isPlaying = true;
        }

        public void Stop()
        {
            if (isPlaying == false)
                return;

            currentSource.Stop();
            isPlaying = false;
        }

        public void Toggle()
        {
            if (isPlaying)
                Stop();
            else
                Start();
        }
    }
}
