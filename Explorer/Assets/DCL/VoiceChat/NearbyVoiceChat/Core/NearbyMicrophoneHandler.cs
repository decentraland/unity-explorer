using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Prefs;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Runtime.Scripts.Audio;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Owns the local microphone-track lifecycle for Nearby Voice Chat:
    ///     publish/unpublish, retry, device-switch republish, app-focus pause/resume,
    ///     reconnect on island-room connection updates, local-speaking detection (VAD),
    ///     and start/stop driven by <see cref="NearbyVoiceChatStateModel"/>.
    ///     Mirrors <see cref="VoiceChatMicrophoneHandler"/> in role.
    /// </summary>
    public class NearbyMicrophoneHandler : IDisposable
    {
        private const string TAG = nameof(NearbyMicrophoneHandler);

        private readonly VoiceChatConfiguration configuration;
        private readonly IRoom islandRoom;
        private readonly NearbyVoiceChatStateModel stateModel;

        private readonly MicrophoneTrackPublisher micPublisher;
        private readonly IDisposable nearbyStateSubscription;

        private CancellationTokenSource? activationCts;
        private bool wasNearbyMicActiveBeforeFocusLoss;

        private bool disposed;

        public NearbyMicrophoneHandler(NearbyVoiceChatStateModel stateModel, IRoom islandRoom, VoiceChatConfiguration configuration)
        {
            this.stateModel = stateModel;
            this.islandRoom = islandRoom;
            this.configuration = configuration;

            micPublisher = new MicrophoneTrackPublisher(islandRoom, configuration, VoiceChatType.NEARBY);

            islandRoom.ConnectionUpdated += OnConnectionUpdated;
            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            Application.focusChanged += OnApplicationFocusChanged;
            VoiceChatSettings.MicrophoneChanged += OnMicrophoneDeviceChanged;

            nearbyStateSubscription = stateModel.State.Subscribe(OnNearbyStateChanged);

            Connect();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            activationCts.SafeCancelAndDispose();
            nearbyStateSubscription.Dispose();

            islandRoom.ConnectionUpdated -= OnConnectionUpdated;
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;

            Application.focusChanged -= OnApplicationFocusChanged;
            VoiceChatSettings.MicrophoneChanged -= OnMicrophoneDeviceChanged;

            Disconnect();
            micPublisher.Dispose();

            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"{TAG} Disposed");
        }

        private void Connect()
        {
            if (islandRoom.Info.ConnectionState != LKConnectionState.ConnConnected) return;
            if (stateModel.State.Value is not (NearbyVoiceChatState.IDLE or NearbyVoiceChatState.OPEN_MIC)) return;

            PublishMicWithRetryAsync(startMic: stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC).Forget();
        }

        private void Disconnect()
        {
            stateModel.IsLocalSpeaking = false;
            micPublisher.Unpublish();
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Deactivated");
        }

        private void OnNearbyStateChanged(NearbyVoiceChatState newState)
        {
            // Cancel in-flight mic publish immediately so PublishAsync observes it via its CancellationToken
            if (newState is NearbyVoiceChatState.SUPPRESSED or NearbyVoiceChatState.DISABLED)
                activationCts.SafeCancelAndDispose();

            OnNearbyStateChangedInternalAsync(newState).Forget();
            return;

            async UniTaskVoid OnNearbyStateChangedInternalAsync(NearbyVoiceChatState state)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                if (disposed) return;

                switch (state)
                {
                    case NearbyVoiceChatState.DISABLED:
                    case NearbyVoiceChatState.SUPPRESSED:
                        Disconnect();
                        break;

                    case NearbyVoiceChatState.IDLE:
                        if (micPublisher.isRecording)
                            micPublisher.StopMicrophone();

                        Connect(); // ensures listener + track published; no-ops if already done
                        break;

                    case NearbyVoiceChatState.OPEN_MIC:
                        if (micPublisher.isPublished)
                            micPublisher.StartMicrophone();
                        else
                            Connect(); // publishes + starts mic
                        break;
                }
            }
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate update, LKDisconnectReason? reason)
        {
            // Cancel in-flight mic publish synchronously — the event may arrive off the main thread, and any deferral
            // until the main-thread hop below lets PublishMicWithRetryAsync start another attempt before observing cancellation.
            if (update == ConnectionUpdate.Disconnected)
                activationCts.SafeCancelAndDispose();

            OnConnectionUpdatedInternalAsync(update, reason).Forget();
            return;

            async UniTaskVoid OnConnectionUpdatedInternalAsync(ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason)
            {
                if (!PlayerLoopHelper.IsMainThread)
                    await UniTask.SwitchToMainThread();

                if (disposed) return;

                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT,
                    $"Island Room connection: {connectionUpdate}{(disconnectReason.HasValue ? $" (reason: {disconnectReason.Value})" : "")}");

                if (connectionUpdate == ConnectionUpdate.Disconnected)
                {
                    if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
                        ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"Valid disconnect ({disconnectReason}) — no reconnection needed");

                    Disconnect();
                }
                else
                {
                    // A fresh Connected means a new room session (including the simulated post-swap Connected):
                    // any previously published track is bound to the old room and must be dropped before republishing.
                    if (connectionUpdate == ConnectionUpdate.Connected)
                        micPublisher.Unpublish();

                    Connect();
                }
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            LKParticipant local = islandRoom.Participants.LocalParticipant();
            if (local != null)
                stateModel.IsLocalSpeaking = islandRoom.ActiveSpeakers.Contains(local.Identity);
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUTE_MIC_IN_BACKGROUND, true))
                return;

            if (!hasFocus && stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC && micPublisher.isRecording)
            {
                micPublisher.StopMicrophone();
                wasNearbyMicActiveBeforeFocusLoss = true;
                stateModel.StopSpeaking();
                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic paused — application lost focus");
            }
            else if (hasFocus && wasNearbyMicActiveBeforeFocusLoss)
            {
                wasNearbyMicActiveBeforeFocusLoss = false;

                if (stateModel.State.Value == NearbyVoiceChatState.SUPPRESSED)
                {
                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic NOT resumed — state is SUPPRESSED");
                    return;
                }

                stateModel.StartSpeaking(NearbyVoiceActivation.FOCUS_RESUMED);
                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, "Nearby mic resumed — application regained focus");
            }
        }

        private void OnMicrophoneDeviceChanged(MicrophoneSelection selection)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                SwitchAsync().Forget();
                return;
            }

            RepublishIfNeeded();
            return;

            async UniTaskVoid SwitchAsync()
            {
                await UniTask.SwitchToMainThread();
                RepublishIfNeeded();
            }

            void RepublishIfNeeded()
            {
                if (!micPublisher.isPublished) return;

                bool wasSpeaking = stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC;
                micPublisher.Unpublish();
                PublishMicWithRetryAsync(startMic: wasSpeaking).Forget();
            }
        }

        private async UniTask PublishMicWithRetryAsync(bool startMic = false)
        {
            activationCts = activationCts.SafeRestart();
            var ct = activationCts.Token;

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread(ct);

            for (var attempt = 1; attempt <= configuration.MaxReconnectionAttempts; attempt++)
            {
                if (ct.IsCancellationRequested || disposed) return;

                try
                {
                    await micPublisher.PublishAsync(false, ct);

                    if (ct.IsCancellationRequested || disposed) return;

                    if (startMic && stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC)
                        micPublisher.StartMicrophone();

                    ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, startMic ? "Mic track published and started" : "Mic track published (standby)");
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT,
                        $"Mic publish attempt {attempt}/{configuration.MaxReconnectionAttempts} failed: {ex.Message}");

                    micPublisher.Unpublish();

                    if (attempt >= configuration.MaxReconnectionAttempts)
                    {
                        ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT, "All mic publish attempts exhausted — listening-only mode active");
                        return;
                    }

                    try { await UniTask.Delay(configuration.ReconnectionDelayMs, cancellationToken: ct); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }
    }
}
