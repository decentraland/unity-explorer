using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Profiles.Tables;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    /// Applies <see cref="VoiceChatConfiguration"/> LiveKit spatial settings to every <see cref="NearbyAudioSourceComponent"/> when changed,
    /// and surfaces ECS↔LiveKit consistency metrics through the Nearby Voice Chat debug widget.
    /// Metric polling is throttled to <see cref="POLL_INTERVAL_SECONDS"/> and only runs while the widget is expanded,
    /// so stress-test sessions can attribute frozen nametags / unresponsive sound waves to either stale ECS components or LiveKit room state.
    /// Runs before <see cref="NearbyAudioPositionSystem"/> so config changes are visible the same frame the position system reads them.
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateBefore(typeof(NearbyAudioPositionSystem))]
    public partial class NearbyVoiceChatDebugSystem : BaseUnityLoopSystem
    {
        private const string EM_DASH = "—";
        private const float POLL_INTERVAL_SECONDS = 0.25f;
        private static readonly QueryDescription COMPONENT_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private readonly VoiceChatConfiguration configuration;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly IRoom islandRoom;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly INearbyAudioStreamRegistry streamRegistry;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;

        private readonly ElementBinding<bool> spatializeBinding;
        private readonly ElementBinding<bool> smoothPanningBinding;
        private readonly ElementBinding<float> ildBinding;

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

        private bool prevSpatialize;
        private bool prevSmoothPanning;
        private float prevIldStrength;

        private float pollAccumulator;

        // Mismatch edge-detection: log when ECS↔LiveKit divergence appears and when it heals.
        // Avoids per-poll spam while still correlating to a precise frame for log-vs-screenshot triage.
        private bool wasMismatched;
        private int mismatchStartFrame;
        private float mismatchStartTime;

        internal NearbyVoiceChatDebugSystem(
            World world,
            VoiceChatConfiguration configuration,
            IDebugContainerBuilder debugBuilder,
            IRoom islandRoom,
            NearbyVoiceChatStateModel stateModel,
            INearbyAudioStreamRegistry streamRegistry,
            IReadOnlyEntityParticipantTable entityParticipantTable) : base(world)
        {
            this.configuration = configuration;
            this.debugBuilder = debugBuilder;
            this.islandRoom = islandRoom;
            this.stateModel = stateModel;
            this.streamRegistry = streamRegistry;
            this.entityParticipantTable = entityParticipantTable;

            spatializeBinding = new ElementBinding<bool>(configuration.nearbySpatialize,
                evt => { configuration.nearbySpatialize = evt.newValue; });

            smoothPanningBinding = new ElementBinding<bool>(configuration.nearbySmoothPanning,
                evt => { configuration.nearbySmoothPanning = evt.newValue; });

            ildBinding = new ElementBinding<float>(configuration.nearbyIldStrength,
                evt => { configuration.nearbyIldStrength = evt.newValue; });

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NEARBY_VOICE_CHAT)
                       ?.SetVisibilityBinding(visibility)
                        .AddControl(new DebugConstLabelDef("Spatialize"), new DebugToggleDef(spatializeBinding))
                        .AddControl(new DebugConstLabelDef("Smooth Panning"), new DebugToggleDef(smoothPanningBinding))
                        .AddFloatSliderField("ILD Strength", ildBinding, 0f, 1f)
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

            prevSpatialize = configuration.nearbySpatialize;
            prevSmoothPanning = configuration.nearbySmoothPanning;
            prevIldStrength = configuration.nearbyIldStrength;
        }

        protected override void Update(float t)
        {
            ApplyConfigChanges();
            PollMetrics(t);
        }

        private void ApplyConfigChanges()
        {
            bool changed = prevSpatialize != configuration.nearbySpatialize
                        || prevSmoothPanning != configuration.nearbySmoothPanning
                        || !Mathf.Approximately(prevIldStrength, configuration.nearbyIldStrength);

            if (!changed)
                return;

            prevSpatialize = configuration.nearbySpatialize;
            prevSmoothPanning = configuration.nearbySmoothPanning;
            prevIldStrength = configuration.nearbyIldStrength;

            spatializeBinding.Value = configuration.nearbySpatialize;
            smoothPanningBinding.Value = configuration.nearbySmoothPanning;
            ildBinding.Value = configuration.nearbyIldStrength;

            ApplySettingsQuery(World);
        }

        private void PollMetrics(float t)
        {
            if (!debugBuilder.IsVisible || !visibility.IsConnectedAndExpanded)
            {
                pollAccumulator = 0f;
                return;
            }

            pollAccumulator += t;
            if (pollAccumulator < POLL_INTERVAL_SECONDS)
                return;

            pollAccumulator = 0f;

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

            ulong audioSourceCount = 0;
            if (isConnected)
                foreach (KeyValuePair<string, LKParticipant> entry in islandRoom.Participants.RemoteParticipantIdentities())
                    audioSourceCount += (ulong)(streamRegistry.GetAudioSidsArray(entry.Key)?.Length ?? 0);
            activeAudioSourcesBinding.Value = audioSourceCount;

            ulong componentCount = (ulong)World.CountEntities(in COMPONENT_QUERY);
            nearbyComponentsBinding.Value = componentCount;

            // Registry mirrors LiveKit's native audio publications; ECS components are bound by NearbyAudioBindingSystem.
            // A persistent mismatch points at a binding-pipeline regression (avatar entity gone, wallet drift, throttle backlog).
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

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref NearbyAudioSourceComponent nearbyAudio)
        {
            nearbyAudio.LivekitAudioSource.ApplySpatialSettings(configuration);
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
