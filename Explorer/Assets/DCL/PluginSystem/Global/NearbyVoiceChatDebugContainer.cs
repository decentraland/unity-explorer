using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Profiles.Tables;
using DCL.VoiceChat;
using DCL.VoiceChat.Nearby;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Time = UnityEngine.Time;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Live runtime metrics widget for Nearby Voice Chat. Polls every 250 ms while the widget is expanded.
    ///     Surfaces ECS↔LiveKit consistency so stress-test sessions can attribute frozen nametags / unresponsive
    ///     sound waves to either stale ECS components or LiveKit room state.
    /// </summary>
    public class NearbyVoiceChatDebugContainer : IDisposable
    {
        private const string EM_DASH = "—";
        private static readonly TimeSpan POLL_DELAY = TimeSpan.FromMilliseconds(250);
        private static readonly QueryDescription COMPONENT_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private readonly IRoom islandRoom;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;

        private readonly DebugWidgetVisibilityBinding visibility = new (true);
        private readonly ElementBinding<string> stateBinding = new (string.Empty);
        private readonly ElementBinding<string> suppressionBinding = new (EM_DASH);
        private readonly ElementBinding<string> localSpeakingBinding = new (string.Empty);
        private readonly ElementBinding<string> connectionBinding = new (string.Empty);
        private readonly ElementBinding<ulong> remoteParticipantsBinding = new (0);
        private readonly ElementBinding<ulong> activeSpeakersBinding = new (0);
        private readonly ElementBinding<ulong> activeAudioSourcesBinding = new (0);
        private readonly ElementBinding<ulong> nearbyComponentsBinding = new (0);
        private readonly ElementBinding<string> mismatchBinding = new (string.Empty);
        private readonly ElementBinding<ulong> entityParticipantsBinding = new (0);
        private readonly ElementBinding<IReadOnlyList<(string name, string value)>> speakersListBinding =
            new (Array.Empty<(string, string)>());

        private readonly List<(string name, string value)> speakersBuffer = new ();

        private CancellationTokenSource? pollCts;

        // Mismatch edge-detection: log when ECS↔LiveKit divergence appears and when it heals.
        // Avoids per-poll spam while still correlating to a precise frame for log-vs-screenshot triage.
        private bool wasMismatched;
        private int mismatchStartFrame;
        private float mismatchStartTime;

        public NearbyVoiceChatDebugContainer(
            IDebugContainerBuilder debugBuilder,
            IRoom islandRoom,
            NearbyVoiceChatStateModel stateModel,
            ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world)
        {
            this.islandRoom = islandRoom;
            this.stateModel = stateModel;
            this.activeAudioSources = activeAudioSources;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NEARBY_VOICE_CHAT)
                       ?.SetVisibilityBinding(visibility)
                        .AddCustomMarker("State", stateBinding)
                        .AddCustomMarker("Suppression", suppressionBinding)
                        .AddCustomMarker("Local Speaking", localSpeakingBinding)
                        .AddCustomMarker("Island Room", connectionBinding)
                        .AddMarker("Remote Participants (LK)", remoteParticipantsBinding, DebugLongMarkerDef.Unit.NoFormat)
                        .AddMarker("Active Speakers", activeSpeakersBinding, DebugLongMarkerDef.Unit.NoFormat)
                        .AddMarker("Active Audio Sources", activeAudioSourcesBinding, DebugLongMarkerDef.Unit.NoFormat)
                        .AddMarker("ECS NearbyAudio Components", nearbyComponentsBinding, DebugLongMarkerDef.Unit.NoFormat)
                        .AddCustomMarker("Sources / Components", mismatchBinding)
                        .AddMarker("Entity↔Participant entries", entityParticipantsBinding, DebugLongMarkerDef.Unit.NoFormat)
                        .AddList("Speakers", speakersListBinding);

            pollCts = new CancellationTokenSource();
            PollLoopAsync(pollCts.Token).Forget();
        }

        public void Dispose()
        {
            pollCts.SafeCancelAndDispose();
            pollCts = null;
        }

        private async UniTaskVoid PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool cancelled = await UniTask.Delay(POLL_DELAY, cancellationToken: ct).SuppressCancellationThrow();
                if (cancelled) return;

                if (!visibility.IsConnectedAndExpanded)
                    continue;

                UpdateMetrics();
            }
        }

        private void UpdateMetrics()
        {
            NearbyVoiceChatState state = stateModel.State.Value;
            stateBinding.Value = $"<color={ColorFor(state)}>{state}</color>";
            suppressionBinding.Value = stateModel.ActiveSuppression.Value?.ToString() ?? EM_DASH;

            localSpeakingBinding.Value = stateModel.IsLocalSpeaking
                ? "<color=green>SPEAKING</color>"
                : "<color=#888888>silent</color>";

            LKConnectionState conn = islandRoom.Info.ConnectionState;
            connectionBinding.Value = $"<color={ColorFor(conn)}>{conn}</color>";

            bool isConnected = conn == LKConnectionState.ConnConnected;

            remoteParticipantsBinding.Value = isConnected
                ? (ulong)islandRoom.Participants.RemoteParticipantIdentities().Count
                : 0;

            speakersBuffer.Clear();
            ulong speakersCount = 0;

            if (isConnected)
            {
                LKParticipant? local = islandRoom.Participants.LocalParticipant();
                string? localIdentity = local?.Identity;

                foreach (string identity in islandRoom.ActiveSpeakers)
                {
                    speakersCount++;
                    bool isLocal = identity == localIdentity;
                    speakersBuffer.Add((isLocal ? $"{identity} (you)" : identity, isLocal ? "local" : "remote"));
                }
            }

            activeSpeakersBinding.Value = speakersCount;
            speakersListBinding.SetAndUpdate(speakersBuffer);

            ulong audioSourceCount = (ulong)activeAudioSources.Count;
            activeAudioSourcesBinding.Value = audioSourceCount;

            ulong componentCount = (ulong)world.CountEntities(in COMPONENT_QUERY);
            nearbyComponentsBinding.Value = componentCount;

            // activeAudioSources is the source of truth (LiveKit native side); ECS components mirror it.
            // A persistent mismatch points at the stale-reference bug after Island Room renewal.
            bool isMismatched = audioSourceCount != componentCount;
            mismatchBinding.Value = isMismatched
                ? $"<color=red>{audioSourceCount} sources vs {componentCount} components</color>"
                : $"<color=green>OK ({audioSourceCount} = {componentCount})</color>";

            LogMismatchEdges(isMismatched, audioSourceCount, componentCount);

            entityParticipantsBinding.Value = (ulong)entityParticipantTable.Count;
        }

        private void LogMismatchEdges(bool isMismatched, ulong audioSourceCount, ulong componentCount)
        {
            if (isMismatched && !wasMismatched)
            {
                mismatchStartFrame = UnityEngine.Time.frameCount;
                mismatchStartTime = UnityEngine.Time.unscaledTime;
                ReportHub.LogWarning(ReportCategory.NEARBY_VOICE_CHAT,
                    $"Mismatch START frame={mismatchStartFrame} sources={audioSourceCount} components={componentCount}");
            }
            else if (!isMismatched && wasMismatched)
            {
                int durationFrames = UnityEngine.Time.frameCount - mismatchStartFrame;
                float durationMs = (UnityEngine.Time.unscaledTime - mismatchStartTime) * 1000f;
                ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT,
                    $"Mismatch RESOLVED frame={UnityEngine.Time.frameCount} duration={durationFrames}f / {durationMs:F0}ms");
            }

            wasMismatched = isMismatched;
        }

        private static string ColorFor(NearbyVoiceChatState state) =>
            state switch
            {
                NearbyVoiceChatState.OPEN_MIC => "green",
                NearbyVoiceChatState.IDLE => "white",
                NearbyVoiceChatState.SUPPRESSED => "yellow",
                NearbyVoiceChatState.DISABLED => "#888888",
                _ => "white",
            };

        private static string ColorFor(LKConnectionState conn) =>
            conn switch
            {
                LKConnectionState.ConnConnected => "green",
                LKConnectionState.ConnReconnecting => "yellow",
                _ => "red",
            };
    }
}
