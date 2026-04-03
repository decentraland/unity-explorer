using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.VoiceChat.Proximity;
using LiveKit.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Result = RichTypes.Result;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private readonly VoiceChatConfiguration voiceChatConfiguration;
        private readonly ICommunityCallOrchestrator? orchestrator;

        private readonly ReactiveProperty<bool> isMicrophoneEnabledProperty = new (false);
        private float buttonPressStartTime;
        private bool hasIntentionToDisable;

        private Weak<MicrophoneRtcAudioSource> communitySource = Weak<MicrophoneRtcAudioSource>.Null;
        private Weak<MicrophoneRtcAudioSource> proximitySource = Weak<MicrophoneRtcAudioSource>.Null;
        private ProximityVoiceChatStateModel? proximityStateModel;

        public IReadonlyReactiveProperty<bool> IsMicrophoneEnabled => isMicrophoneEnabledProperty;

        private bool IsCommunityActive => communitySource.Resource.Has;
        private bool IsProximityActive => !IsCommunityActive && proximitySource.Resource.Has;

        private Option<MicrophoneRtcAudioSource> GetActiveSourceResource()
        {
            if (communitySource.Resource.Has) return communitySource.Resource;
            return proximitySource.Resource;
        }

        public VoiceChatMicrophoneHandler(VoiceChatConfiguration voiceChatConfiguration, ICommunityCallOrchestrator orchestrator)
        {
            this.voiceChatConfiguration = voiceChatConfiguration;
            this.orchestrator = orchestrator;

            DCLInput.Instance.VoiceChat.Talk!.performed += OnTalkHotkeyPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnTalkHotkeyReleased;
            VoiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
        }

        public void Dispose()
        {
            isMicrophoneEnabledProperty.ClearSubscriptionsList();
            DCLInput.Instance.VoiceChat.Talk!.performed -= OnTalkHotkeyPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnTalkHotkeyReleased;
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
        }

        /// <summary>
        ///     Don't keep reference.
        ///     Must be consumed in place without async operation.
        ///     Source is not guaranteed to be alive after the scope.
        /// </summary>
        private bool TryGetActiveMicrophoneSource(out MicrophoneRtcAudioSource? source)
        {
            Option<MicrophoneRtcAudioSource> resource = GetActiveSourceResource();
            source = resource.Has ? resource.Value : null;
            return resource.Has;
        }

        private void OnTalkHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (TryGetActiveMicrophoneSource(out MicrophoneRtcAudioSource? source))
            {
                buttonPressStartTime = Time.time;
                hasIntentionToDisable = source!.IsRecording;
                EnableMicrophone();
            }
        }

        private void OnTalkHotkeyReleased(InputAction.CallbackContext obj)
        {
            if (TryGetActiveMicrophoneSource(out _))
            {
                float pressDuration = Time.time - buttonPressStartTime;
                bool shouldDisableByThreshold = pressDuration >= voiceChatConfiguration.HoldThresholdInSeconds;

                if (shouldDisableByThreshold || hasIntentionToDisable)
                    DisableMicrophone();
            }
        }

        public void ToggleMicrophone()
        {
            Option<MicrophoneRtcAudioSource> resource = GetActiveSourceResource();

            if (resource.Has)
            {
                MicrophoneRtcAudioSource source = resource.Value;
                source.Toggle();
                bool newState = source.IsRecording;
                isMicrophoneEnabledProperty.Value = newState;

                if (IsProximityActive)
                {
                    if (newState) proximityStateModel?.StartSpeaking();
                    else proximityStateModel?.StopSpeaking();
                }

                NotifyMicrophoneStateChange(newState);
            }
        }

        public void Assign(Weak<MicrophoneRtcAudioSource> newSource)
        {
            communitySource = newSource;
        }

        public void AssignProximity(Weak<MicrophoneRtcAudioSource> source)
        {
            proximitySource = source;
        }

        public void ClearProximity()
        {
            proximitySource = Weak<MicrophoneRtcAudioSource>.Null;
        }

        public void SetProximityStateModel(ProximityVoiceChatStateModel? model)
        {
            proximityStateModel = model;
        }

        internal void EnableMicrophone()
        {
            Option<MicrophoneRtcAudioSource> resource = GetActiveSourceResource();
            if (resource.Has) resource.Value.Start();
            isMicrophoneEnabledProperty.Value = true;

            if (IsProximityActive)
                proximityStateModel?.StartSpeaking();

            NotifyMicrophoneStateChange(true);
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Enabled microphone");
        }

        private void DisableMicrophone()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                DisableMicrophoneAsync().Forget();
                return;
            }

            DisableMicrophoneInternal();
            return;

            async UniTaskVoid DisableMicrophoneAsync()
            {
                await UniTask.SwitchToMainThread();
                DisableMicrophoneInternal();
            }

            void DisableMicrophoneInternal()
            {
                Option<MicrophoneRtcAudioSource> resource = GetActiveSourceResource();
                if (resource.Has) resource.Value.Stop();
                isMicrophoneEnabledProperty.Value = false;

                if (IsProximityActive)
                    proximityStateModel?.StopSpeaking();

                NotifyMicrophoneStateChange(false);
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Disabled microphone");
            }
        }

        private void OnMicrophoneChanged(MicrophoneSelection microphoneName)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnMicrophoneChangedAsync().Forget();
                return;
            }

            SwitchAllSources();
            return;

            async UniTaskVoid OnMicrophoneChangedAsync()
            {
                await UniTask.SwitchToMainThread();
                SwitchAllSources();
            }

            void SwitchAllSources()
            {
                TrySwitchMicrophone(communitySource, microphoneName);
                TrySwitchMicrophone(proximitySource, microphoneName);
            }
        }

        private static void TrySwitchMicrophone(Weak<MicrophoneRtcAudioSource> source, MicrophoneSelection selection)
        {
            if (!source.Resource.Has) return;

            Result result = source.Resource.Value.SwitchMicrophone(selection);

            if (!result.Success)
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot switch microphone: {result.ErrorMessage}");
        }

        public void EnableMicrophoneForCall()
        {
            EnableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone enabled for call (room connected)");
        }

        public void DisableMicrophoneForCall()
        {
            DisableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone disabled for call");
        }

        private void NotifyMicrophoneStateChange(bool isEnabled)
        {
            if (!IsCommunityActive) return;

            if (orchestrator != null &&
                orchestrator.CommunityCallStatus.Value == VoiceChatStatus.VOICE_CHAT_IN_CALL &&
                orchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Value)
            {
                string localParticipantId = orchestrator.ParticipantsStateService.LocalParticipantId;

                if (!string.IsNullOrEmpty(localParticipantId))
                {
                    // isEnabled = true means microphone is unmuted, so we want to unmute the speaker
                    // isEnabled = false means microphone is muted, so we want to mute the speaker
                    orchestrator.NotifyMuteSpeakerInCurrentCall(localParticipantId, !isEnabled);
                }
            }
        }
    }
}
