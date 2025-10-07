using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Settings.Settings;
using DCL.VoiceChat;
using LiveKit.Audio;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using RustAudio;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

#if UNITY_STANDALONE_OSX
using DCL.VoiceChat.Permissions;
#endif

namespace DCL.PluginSystem.Global
{
    public class VoiceChatDebugContainer : IDisposable
    {
        private CancellationTokenSource? autoUpdateCts;

        public VoiceChatDebugContainer(IDebugContainerBuilder debugContainer, VoiceChatTrackManager trackManager)
        {
            var unityOutputSampleRate = new ElementBinding<ulong>(0);

            var availableMicrophones = new ElementBinding<ulong>(0);
            var currentMicrophone = new ElementBinding<string>(string.Empty);

#if UNITY_STANDALONE_OSX
            var permissionsStatus = new ElementBinding<string>(string.Empty);
#endif

            var isRecording = new ElementBinding<string>(string.Empty);
            var sampleRate = new ElementBinding<ulong>(0);
            var channels = new ElementBinding<ulong>(0);

            var remoteSpeakers = new ElementBinding<ulong>(0);
            var speakersInfo = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());

            var wavMicrophoneStatus = new ElementBinding<string>(string.Empty);
            var wavRemotesStatusInfo = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());

            List<StreamInfo<AudioStreamInfo>> infoBuffer = new ();
            List<(string name, string value)> speakersBuffer = new ();

            List<(string name, string value)> wavRemotesBuffer = new ();

            debugContainer.TryAddWidget(IDebugContainerBuilder.Categories.VOICE_CHAT)
                         ?.AddMarker("Unity Output Sample Rate", unityOutputSampleRate, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Available Microphones", availableMicrophones, DebugLongMarkerDef.Unit.NoFormat)

#if UNITY_STANDALONE_OSX
                          .AddCustomMarker("Permission Status", permissionsStatus)
#endif

                          .AddCustomMarker("Current Microphone", currentMicrophone)
                          .AddCustomMarker("Is Recording", isRecording)
                          .AddMarker("Sample Rate", sampleRate, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Channels", channels, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Remote Speakers", remoteSpeakers, DebugLongMarkerDef.Unit.NoFormat)
                          .AddList("Speakers Info", speakersInfo)
                          .AddSingleButton("Toggle WAV Microphone", ChangeMicrophoneRecordStatus)
                          .AddCustomMarker("Status WAV Microphone", wavMicrophoneStatus)
                          .AddSingleButton("Toggle WAV Remotes", ChangeRemoteRecordStatus)
                          .AddList("Status WAV Remotes", wavRemotesStatusInfo)
                          .AddToggleField("Auto Update", v => AutoUpdateTriggerAsync(v.newValue).Forget(), false)
                          .AddSingleButton("Update", UpdateWidget);

            return;

            void ChangeMicrophoneRecordStatus()
            {
                Option<MicrophoneRtcAudioSource> currentMicrophoneOption = trackManager.CurrentMicrophone.Resource;

                if (currentMicrophoneOption.Has == false)
                {
                    wavMicrophoneStatus.Value = "Is Not Recording";
                    return;
                }

                MicrophoneRtcAudioSource source = currentMicrophoneOption.Value;
                var result = source.WavTeeControl.Toggle();
                string message;

                if (result.Success)
                {
                    var isActive = source.WavTeeControl.IsWavActive;
                    message = isActive ? "Writing" : "Sleep";
                }
                else { message = result.ErrorMessage!; }

                wavMicrophoneStatus.Value = message;
            }

            void ChangeRemoteRecordStatus()
            {
                wavRemotesBuffer.Clear();

                foreach ((StreamKey key, (Weak<AudioStream> stream, LivekitAudioSource source) value) in trackManager.RemoteStreams)
                {
                    Option<AudioStream> streamOption = value.stream.Resource;

                    if (streamOption.Has)
                    {
                        Result result = streamOption.Value.WavTeeControl.Toggle();
                        string name = $"Stream {key.identity}";
                        string message = string.Empty;

                        if (result.Success)
                        {
                            var isActive = streamOption.Value.WavTeeControl.IsWavActive;
                            message = isActive ? "Writing" : "Sleep";
                        }
                        else { message = result.ErrorMessage!; }

                        wavRemotesBuffer.Add((name, message));
                    }

                    {
                        Result toggleResult = value.source.ToggleRecordWavOutput();
                        string name = $"Stream {key.identity}";
                        string message = toggleResult.Success ? "Success" : toggleResult.ErrorMessage!;
                        wavRemotesBuffer.Add((name, message));
                    }
                }

                wavRemotesStatusInfo.SetAndUpdate(wavRemotesBuffer);
            }

            async UniTaskVoid AutoUpdateTriggerAsync(bool enable)
            {
                if (enable)
                {
                    autoUpdateCts = autoUpdateCts.SafeRestart();
                    CancellationToken current = autoUpdateCts.Token;
                    TimeSpan pollDelay = TimeSpan.FromMilliseconds(500);

                    while (current.IsCancellationRequested == false)
                    {
                        bool cancelled = await UniTask.Delay(pollDelay, cancellationToken: current).SuppressCancellationThrow();
                        if (cancelled) return;

                        UpdateWidget();
                    }
                }
                else
                {
                    autoUpdateCts?.Cancel();
                    autoUpdateCts?.Dispose();
                    autoUpdateCts = null;
                }
            }

            void UpdateWidget()
            {
                unityOutputSampleRate.Value = (ulong)UnityEngine.AudioSettings.outputSampleRate;

                availableMicrophones.Value = (ulong)MicrophoneSelection.Devices().Length;
                currentMicrophone.Value = VoiceChatSettings.SelectedMicrophone?.name ?? string.Empty;

                var currentMicrophoneOption = trackManager.CurrentMicrophone.Resource;

                MicrophoneInfo info = currentMicrophoneOption.Has
                    ? currentMicrophoneOption.Value.MicrophoneInfo
                    : default(MicrophoneInfo);

                isRecording.Value = (currentMicrophoneOption is { Has: true, Value: { IsRecording: true } }).ToString();
                sampleRate.Value = info.sampleRate;
                channels.Value = info.channels;

#if UNITY_STANDALONE_OSX
                permissionsStatus.Value = VoiceChatPermissions.CurrentState().ToString()!;
#endif

                trackManager.ActiveStreamsInfo(infoBuffer);
                remoteSpeakers.Value = (ulong)infoBuffer.Count;

                speakersBuffer.Clear();

                foreach (StreamInfo<AudioStreamInfo> streamInfo in infoBuffer)
                {
                    speakersBuffer.Add((streamInfo.key.identity, $"SampleRate - {streamInfo.info.sampleRate}"));
                    speakersBuffer.Add((streamInfo.key.identity, $"Channels - {streamInfo.info.numChannels}"));
                }

                speakersInfo.SetAndUpdate(speakersBuffer);
            }
        }

        public void Dispose()
        {
            autoUpdateCts.SafeCancelAndDispose();
        }
    }
}
