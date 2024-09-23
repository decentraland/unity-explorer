using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using ECS.Abstract;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MultiplayerMovementDebugSystem : BaseUnityLoopSystem
    {
        private const float TRAIL_LIFETIME = 1.0f; // The time in seconds that the trail will fade out over
        private const float TRAIL_WIDTH = 0.07f;

        private readonly Entity playerEntity;
        private readonly DebugWidgetBuilder? widget;
        private readonly RemoteEntities? remoteEntities;
        private readonly ExposedTransform playerTransform;
        private readonly MultiplayerDebugSettings debugSettings;
        private readonly IMultiplayerMovementSettings mainSettings;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;

        private Entity? selfReplicaEntity;
        private bool useLinear;

        private readonly ElementBinding<string> entityId;

        private readonly ElementBinding<string> inboxCount;
        private readonly ElementBinding<string> wasTeleported;
        private readonly ElementBinding<string> wasPassedThisFrame;
        // NetworkMovementMessage pastMessage;
        // NetworkMovementMessage nextMessage;

        private readonly ElementBinding<string> isEnabled;
        private readonly ElementBinding<string> time;
        private readonly ElementBinding<string> duration;
        // NetworkMovementMessage start;
        // NetworkMovementMessage end;

        internal MultiplayerMovementDebugSystem(World world, Entity playerEntity, IDebugContainerBuilder debugBuilder, RemoteEntities remoteEntities,
            ExposedTransform playerTransform, MultiplayerDebugSettings debugSettings, IMultiplayerMovementSettings mainSettings,
            IReadOnlyEntityParticipantTable entityParticipantTable) : base(world)
        {
            this.playerEntity = playerEntity;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.debugSettings = debugSettings;
            this.mainSettings = mainSettings;
            this.entityParticipantTable = entityParticipantTable;

            widget = debugBuilder.TryAddWidget("Multiplayer Movement");

            widget?.AddSingleButton("Instantiate Self-Replica", () => InstantiateSelfReplica(world))
                   .AddSingleButton("Remove Self-Replica", () => RemoveSelfReplica(world))
                   .AddSingleButton("Debug Profile", () => DebugProfile(world))
                   .AddToggleField("Use Compression", evt => this.mainSettings.UseCompression = evt.newValue, this.mainSettings.UseCompression)
                   .AddToggleField("Use Linear", evt => SelectInterpolationType(evt.newValue), useLinear)
                   .AddToggleField("Use speed-up", evt => this.mainSettings.InterpolationSettings.UseSpeedUp = evt.newValue, this.mainSettings.InterpolationSettings.UseSpeedUp)
                   .AddCustomMarker("Entity Id", entityId = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("MOVEMENT", new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Inbox Count", inboxCount = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Was Teleported", wasTeleported = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Was Passed This Frame", wasPassedThisFrame = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("INTERPOLATION", new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Is Enabled", isEnabled = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Time", time = new ElementBinding<string>(string.Empty))
                   .AddCustomMarker("Duration", duration = new ElementBinding<string>(string.Empty));
        }

        public void Dispose()
        {
            debugSettings.SelfSending = false;
        }

        protected override void Update(float t)
        {
            if (entityParticipantTable.Has(RemotePlayerMovementComponent.TEST_ID))
            {
                Entity entity = entityParticipantTable.Entity(RemotePlayerMovementComponent.TEST_ID);

                entityId.Value = entity.Id.ToString();

                if (World.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
                {
                    inboxCount.Value = remotePlayerMovementComponent.Queue.Count.ToString();
                    wasTeleported.Value = remotePlayerMovementComponent.WasTeleported.ToString();
                    wasPassedThisFrame.Value = remotePlayerMovementComponent.WasPassedThisFrame.ToString();
                }

                if (World.TryGet(entity, out InterpolationComponent interpolation))
                {
                    isEnabled.Value = interpolation.Enabled.ToString();
                    time.Value = interpolation.Time.ToString();
                    duration.Value = interpolation.TotalDuration.ToString();
                }
            }
        }

        private void DebugProfile(World world)
        {
            // Entity entity = entityParticipantTable.Entity(RemotePlayerMovementComponent.TEST_ID);
            // world.TryGet(entity, out remotePlayerMovementComponent);

            // if (world.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
            // {
            //     // pastMessage = remotePlayerMovementComponent.PastMessage;
            //     // nextMessage = remotePlayerMovementComponent.Queue.First;
            // }

            //
            // if (world.TryGet(entity, out InterpolationComponent interpolationComponent))
            // {
            //     interpolationIsEnabled = interpolationComponent.Enabled;
            //     start = interpolationComponent.Start;
            //     end = interpolationComponent.End;
            //     time = interpolationComponent.Time;
            //     duration = interpolationComponent.TotalDuration;
            //
            //     pastMessage = remotePlayerMovementComponent.PastMessage;
            //     nextMessage = remotePlayerMovementComponent.Queue.First;
            // }
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
