using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
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

        private Weak<MicrophoneRtcAudioSource> callSource = Weak<MicrophoneRtcAudioSource>.Null;
        private Weak<MicrophoneRtcAudioSource> spatialSource = Weak<MicrophoneRtcAudioSource>.Null;
        private NearbyVoiceChatStateModel? nearbyStateModel;
        private bool wasNearbyMicActiveBeforeFocusLoss;

        public IReadonlyReactiveProperty<bool> IsMicrophoneEnabled => isMicrophoneEnabledProperty;

        private bool isCallActive => callSource.Resource.Has;
        private bool isSpatialActive => !isCallActive && spatialSource.Resource.Has;

        public VoiceChatMicrophoneHandler(VoiceChatConfiguration voiceChatConfiguration, ICommunityCallOrchestrator orchestrator)
        {
            this.voiceChatConfiguration = voiceChatConfiguration;
            this.orchestrator = orchestrator;

            DCLInput.Instance.VoiceChat.Talk!.performed += OnTalkHotkeyPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnTalkHotkeyReleased;
            VoiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
            Application.focusChanged += OnApplicationFocusChanged;
        }

        public void Dispose()
        {
            isMicrophoneEnabledProperty.ClearSubscriptionsList();
            DCLInput.Instance.VoiceChat.Talk!.performed -= OnTalkHotkeyPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnTalkHotkeyReleased;
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
            Application.focusChanged -= OnApplicationFocusChanged;
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

        private Option<MicrophoneRtcAudioSource> GetActiveSourceResource() =>
            callSource.Resource.Has ? callSource.Resource : spatialSource.Resource;

        private void OnTalkHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (TryGetActiveMicrophoneSource(out MicrophoneRtcAudioSource? source))
            {
                buttonPressStartTime = Time.time;
                hasIntentionToDisable = source!.IsRecording; // Disable microphone on release if it was recording
                EnableMicrophone();
            }
        }

        private void OnTalkHotkeyReleased(InputAction.CallbackContext obj)
        {
            if (TryGetActiveMicrophoneSource(out _))
            {
                float pressDuration = Time.time - buttonPressStartTime;
                // If the button was held for longer than the threshold, treat it as push-to-talk and stop communication on release
                bool shouldDisableByThreshold = pressDuration >= voiceChatConfiguration.HoldThresholdInSeconds;

                if (shouldDisableByThreshold || hasIntentionToDisable)
                    DisableMicrophone();
            }
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!isSpatialActive) return;

            if (!hasFocus)
            {
                Option<MicrophoneRtcAudioSource> resource = spatialSource.Resource;

                if (resource.Has && resource.Value.IsRecording)
                {
                    resource.Value.Stop();
                    wasNearbyMicActiveBeforeFocusLoss = true;
                    isMicrophoneEnabledProperty.Value = false;
                    nearbyStateModel?.StopSpeaking();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "Nearby mic paused — application lost focus");
                }
            }
            else if (wasNearbyMicActiveBeforeFocusLoss)
            {
                wasNearbyMicActiveBeforeFocusLoss = false;
                Option<MicrophoneRtcAudioSource> resource = spatialSource.Resource;

                if (resource.Has)
                {
                    resource.Value.Start();
                    isMicrophoneEnabledProperty.Value = true;
                    nearbyStateModel?.StartSpeaking();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "Nearby mic resumed — application regained focus");
                }
            }
        }

        public void ToggleMicrophone()
        {
            Option<MicrophoneRtcAudioSource> weakMicrophoneSource  = GetActiveSourceResource();

            if (weakMicrophoneSource.Has)
            {
                MicrophoneRtcAudioSource source = weakMicrophoneSource .Value;
                source.Toggle();
                bool newState = source.IsRecording;
                isMicrophoneEnabledProperty.Value = newState;

                if (isSpatialActive)
                {
                    if (newState) nearbyStateModel?.StartSpeaking();
                    else nearbyStateModel?.StopSpeaking();
                }

                NotifyMicrophoneStateChange(newState);
            }
        }

        public void Assign(Weak<MicrophoneRtcAudioSource> newSource, VoiceChatType voiceChat)
        {
            switch (voiceChat)
            {
                case VoiceChatType.COMMUNITY:
                case VoiceChatType.PRIVATE:
                    callSource = newSource;
                    break;
                case VoiceChatType.NEARBY:
                    spatialSource = newSource;
                    break;
            }
        }

        public void ClearSource(VoiceChatType voiceChat)
        {
            switch (voiceChat)
            {
                case VoiceChatType.COMMUNITY:
                case VoiceChatType.PRIVATE:
                    if (callSource.Resource.Has) callSource.Resource.Value.Stop();
                    callSource = Weak<MicrophoneRtcAudioSource>.Null;
                    break;
                case VoiceChatType.NEARBY:
                    if (spatialSource.Resource.Has) spatialSource.Resource.Value.Stop();
                    spatialSource = Weak<MicrophoneRtcAudioSource>.Null;
                    break;
            }
        }

        public void SetNearbyStateModel(NearbyVoiceChatStateModel? model)
        {
            nearbyStateModel = model;
        }

        internal void EnableMicrophone()
        {
            Option<MicrophoneRtcAudioSource> resource = GetActiveSourceResource();
            if (resource.Has) resource.Value.Start();
            isMicrophoneEnabledProperty.Value = true;

            if (isSpatialActive)
                nearbyStateModel?.StartSpeaking();

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
                Option<MicrophoneRtcAudioSource> weakMicrophoneSource = GetActiveSourceResource();
                if (weakMicrophoneSource .Has) weakMicrophoneSource .Value.Stop();
                isMicrophoneEnabledProperty.Value = false;

                if (isSpatialActive)
                    nearbyStateModel?.StopSpeaking();

                NotifyMicrophoneStateChange(false);
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Disabled microphone");
            }
        }

        private void OnMicrophoneChanged(MicrophoneSelection microphoneName)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change dispatching to main thread (async)");
                OnMicrophoneChangedAsync().Forget();
                return;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing on main thread (sync)");
            SwitchAllSources();
            return;

            async UniTaskVoid OnMicrophoneChangedAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing after main thread dispatch (async)");
                SwitchAllSources();
            }

            void SwitchAllSources()
            {
                TrySwitchMicrophone(callSource, microphoneName);
                TrySwitchMicrophone(spatialSource, microphoneName);
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
            if (!isCallActive) return;

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
