using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Settings.Settings;
using DCL.VoiceChat;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RustAudio;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatDebugContainer : IDisposable
    {
        private CancellationTokenSource? autoUpdateCts;

        public VoiceChatDebugContainer(IDebugContainerBuilder debugContainer, VoiceChatTrackManager? trackManager)
        {
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

            List<StreamInfo<AudioStreamInfo>> infoBuffer = new ();
            List<(string name, string value)> speakersBuffer = new ();

            debugContainer.TryAddWidget(IDebugContainerBuilder.Categories.MICROPHONE)
                         ?.AddMarker("Available Microphones", availableMicrophones, DebugLongMarkerDef.Unit.NoFormat)
#if UNITY_STANDALONE_OSX
                          .AddCustomMarker("Permission Status", permissionsStatus)
#endif
                          .AddCustomMarker("Current Microphone", currentMicrophone)
                          .AddCustomMarker("Is Recording", isRecording)
                          .AddMarker("Sample Rate", sampleRate, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Channels", channels, DebugLongMarkerDef.Unit.NoFormat)
                          .AddMarker("Remote Speakers", remoteSpeakers, DebugLongMarkerDef.Unit.NoFormat)
                          .AddList("Speakers Info", speakersInfo)
                          .AddToggleField("Auto Update", v => AutoUpdateTriggerAsync(v.newValue).Forget(), false)
                          .AddSingleButton("Update", UpdateWidget);

            return;

            async UniTaskVoid AutoUpdateTriggerAsync(bool enable)
            {
                if (enable)
                {
                    autoUpdateCts = new CancellationTokenSource();
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
                availableMicrophones.Value = (ulong)MicrophoneSelection.Devices().Length;
                currentMicrophone.Value = VoiceChatSettings.SelectedMicrophone?.name ?? string.Empty;

                var currentMicrophoneOption = trackManager.CurrentMicrophone.Resource;

                MicrophoneInfo info = currentMicrophoneOption.Has
                    ? currentMicrophoneOption.Value.MicrophoneInfo
                    : default(MicrophoneInfo);

                isRecording.Value = (currentMicrophoneOption.Has && currentMicrophoneOption.Value.IsRecording).ToString();
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
