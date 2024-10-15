using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement.Systems
{
    internal class NetworkMessageBindings
    {
        public readonly ElementBinding<string> Timestamp = new (string.Empty);
        public readonly ElementBinding<string> Position = new (string.Empty);
        public readonly ElementBinding<string> Velocity = new (string.Empty);
        public readonly ElementBinding<string> MovementKind = new (string.Empty);
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MultiplayerMovementDebugSystem : BaseUnityLoopSystem
    {
        private const float TRAIL_LIFETIME = 1.0f; // The time in seconds that the trail will fade out over
        private const float TRAIL_WIDTH = 0.07f;

        private readonly Entity playerEntity;
        private readonly IRealmData realmData;
        private readonly DebugWidgetBuilder? widget;
        private readonly RemoteEntities? remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly MultiplayerDebugSettings debugSettings;
        private readonly IMultiplayerMovementSettings mainSettings;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRemoteMetadata remoteMetadata;

        private readonly ElementBinding<string> entityId;

        private readonly ElementBinding<string> inboxCount;
        private readonly ElementBinding<string> wasTeleported;
        private readonly ElementBinding<string> wasPassedThisFrame;

        private readonly NetworkMessageBindings pastMessage = new ();
        private readonly NetworkMessageBindings nextMessage = new ();

        private readonly ElementBinding<string> isEnabled;
        private readonly ElementBinding<string> time;
        private readonly ElementBinding<string> duration;

        private readonly ElementBinding<string> metadata;

        private readonly NetworkMessageBindings intStart = new ();
        private readonly NetworkMessageBindings intEnd = new ();

        private readonly DebugWidgetVisibilityBinding widgetVisibility = new (true);

        private Entity? selfReplicaEntity;
        private bool useLinear;
        private string debugProfileId;

        internal MultiplayerMovementDebugSystem(World world, Entity playerEntity, IRealmData realmData, IDebugContainerBuilder debugBuilder, RemoteEntities remoteEntities,
            ExposedTransform playerTransform, MultiplayerDebugSettings debugSettings, IMultiplayerMovementSettings mainSettings,
            IReadOnlyEntityParticipantTable entityParticipantTable, IRemoteMetadata remoteMetadata) : base(world)
        {
            this.playerEntity = playerEntity;
            this.realmData = realmData;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.debugSettings = debugSettings;
            this.mainSettings = mainSettings;
            this.entityParticipantTable = entityParticipantTable;
            this.remoteMetadata = remoteMetadata;

            widget = debugBuilder.TryAddWidget("Multiplayer Movement")
                                ?.SetVisibilityBinding(widgetVisibility);

            widget?.AddSingleButton("Instantiate Self-Replica", () => InstantiateSelfReplica(world))
                   .AddSingleButton("Remove Self-Replica", () => RemoveSelfReplica(world))
                   .AddStringFieldWithConfirmation("SelfReplica", "Debug profile", DebugProfile)
                   .AddToggleField("Use Compression", evt => this.mainSettings.UseCompression = evt.newValue, this.mainSettings.UseCompression)
                   .AddToggleField("Use Linear", evt => SelectInterpolationType(evt.newValue), useLinear)
                   .AddToggleField("Use speed-up", evt => this.mainSettings.InterpolationSettings.UseSpeedUp = evt.newValue, this.mainSettings.InterpolationSettings.UseSpeedUp)
                   .AddCustomMarker("Entity Id", entityId = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Metadata", metadata = new ElementBinding<string>(string.Empty));

            widget?.AddCustomMarker("MOVEMENT", new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Inbox Count", inboxCount = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Was Teleported", wasTeleported = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Was Passed This Frame", wasPassedThisFrame = new ElementBinding<string>(string.Empty));

            widget?.AddCustomMarker("INTERPOLATION", new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Is Enabled", isEnabled = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Time", time = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Duration", duration = new ElementBinding<string>(string.Empty));

            AddNetworkMessageMarkers(pastMessage, "PAST MESSAGE");
            AddNetworkMessageMarkers(intStart, "INTERPOLATION START");
            AddNetworkMessageMarkers(intEnd, "INTERPOLATION END");
            AddNetworkMessageMarkers(nextMessage, "NEXT MESSAGE");

            void AddNetworkMessageMarkers(NetworkMessageBindings messageBinding, string label)
            {
                widget?.AddCustomMarker(label, new ElementBinding<string>(string.Empty))
                       .AddCustomMarker("Timestamp", messageBinding.Timestamp)
                       .AddCustomMarker("Position", messageBinding.Position)
                       .AddCustomMarker("Velocity", messageBinding.Velocity)
                       .AddCustomMarker("Movement Kind", messageBinding.MovementKind);
            }
        }

        ~MultiplayerMovementDebugSystem()
        {
            debugSettings.SelfSending = false;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured) return;
            if (!widgetVisibility.IsConnectedAndExpanded) return;
            if (!entityParticipantTable.Has(debugProfileId)) return;

            Entity entity = entityParticipantTable.Entity(debugProfileId);

            entityId.Value = entity.Id.ToString();

            metadata.Value = remoteMetadata.Metadata.TryGetValue(debugProfileId, out IRemoteMetadata.ParticipantMetadata participantMetadata) ? participantMetadata.ToString() : string.Empty;

            if (World.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent) && remotePlayerMovementComponent.Queue != null)
            {
                inboxCount.Value = remotePlayerMovementComponent.Queue.Count.ToString();
                wasTeleported.Value = remotePlayerMovementComponent.WasTeleported.ToString();
                wasPassedThisFrame.Value = remotePlayerMovementComponent.WasPassedThisFrame.ToString();

                UpdateNetworkMessageMarkers(pastMessage, remotePlayerMovementComponent.PastMessage);

                if (remotePlayerMovementComponent.Queue.Count > 0)
                    UpdateNetworkMessageMarkers(nextMessage, remotePlayerMovementComponent.Queue.First);
            }

            if (World.TryGet(entity, out InterpolationComponent interpolation))
            {
                isEnabled.Value = interpolation.Enabled.ToString();
                time.Value = interpolation.Time.ToString();
                duration.Value = interpolation.TotalDuration.ToString();

                if (interpolation.Enabled)
                {
                    UpdateNetworkMessageMarkers(intStart, interpolation.Start);
                    UpdateNetworkMessageMarkers(intEnd, interpolation.End);
                }
            }

            return;

            static void UpdateNetworkMessageMarkers(NetworkMessageBindings bindings, NetworkMovementMessage networkMessage)
            {
                bindings.Timestamp.Value = networkMessage.timestamp.ToString();
                bindings.Position.Value = networkMessage.position.ToString();
                bindings.Velocity.Value = networkMessage.velocity.ToString();
                bindings.MovementKind.Value = networkMessage.movementKind.ToString();
            }
        }

        private void DebugProfile(string profileId)
        {
            debugProfileId = profileId;
        }

        private void SelectInterpolationType(bool useLinear)
        {
            mainSettings.InterpolationSettings.InterpolationType = useLinear ? InterpolationType.Linear : InterpolationType.Hermite;
            mainSettings.InterpolationSettings.BlendType = useLinear ? InterpolationType.Linear : InterpolationType.Hermite;
        }

        private void InstantiateSelfReplica(World world)
        {
            debugSettings.SelfSending = true;

            if (selfReplicaEntity != null)
                RemoveSelfReplica(world);

            if (remoteEntities != null)
            {
                Profile playerProfiler = world.Get<Profile>(playerEntity);
                var profile = Profile.NewProfileWithAvatar(RemotePlayerMovementComponent.TEST_ID, playerProfiler.Avatar);
                var remoteProfile = new RemoteProfile(profile, RemotePlayerMovementComponent.TEST_ID);
                selfReplicaEntity = remoteEntities.TryCreateOrUpdateRemoteEntity(remoteProfile, world);

                if (world.TryGet(selfReplicaEntity.Value, out CharacterTransform transformComp))
                {
                    transformComp.Transform.position = playerTransform.Position;
                    transformComp.Transform.rotation = playerTransform.Rotation;
                    transformComp.Transform.name = RemotePlayerMovementComponent.TEST_ID;

                    TrailRenderer trail = transformComp.Transform.gameObject.TryAddComponent<TrailRenderer>();
                    trail.time = TRAIL_LIFETIME;
                    trail.startWidth = TRAIL_WIDTH;
                    trail.endWidth = TRAIL_WIDTH;

                    trail.material = new Material(Shader.Find("Unlit/Color"))
                    {
                        color = Color.yellow,
                    };
                }
            }
        }

        private void RemoveSelfReplica(World world)
        {
            debugSettings.SelfSending = false;

            if (remoteEntities == null) return;
            remoteEntities.TryRemove(RemotePlayerMovementComponent.TEST_ID, world);

            selfReplicaEntity = null;
        }
    }
}
