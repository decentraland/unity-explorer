using Arch.Core;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Profiles;
using System;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementDebug : IDisposable
    {
        private const float TRAIL_LIFETIME = 1.0f; // The time in seconds that the trail will fade out over
        private const float TRAIL_WIDTH = 0.07f;

        private readonly Entity playerEntity;
        private readonly RemoteEntities? remoteEntities;
        private readonly IExposedTransform playerTransform;
        private readonly MultiplayerDebugSettings settings;

        private Entity? selfReplicaEntity;

        public MultiplayerMovementDebug(World world, Entity playerEntity, IDebugContainerBuilder debugBuilder, RemoteEntities remoteEntities, IExposedTransform playerTransform,
            MultiplayerDebugSettings settings)
        {
            this.playerEntity = playerEntity;
            this.remoteEntities = remoteEntities;
            this.playerTransform = playerTransform;
            this.settings = settings;

            debugBuilder.TryAddWidget("Multiplayer Movement")
                       ?.AddSingleButton("Instantiate Self-Replica", () => InstantiateSelfReplica(world))
                        .AddSingleButton("Remove Self-Replica", () => RemoveSelfReplica(world));
        }

        public void Dispose()
        {
            settings.SelfSending = false;
        }

        private void InstantiateSelfReplica(World world)
        {
            settings.SelfSending = true;

            if (selfReplicaEntity != null)
                RemoveSelfReplica(world);

            if (remoteEntities != null)
            {
                var playerProfiler = world.Get<Profile>(playerEntity);
                Profile profile = Profile.NewProfileWithAvatar(RemotePlayerMovementComponent.TEST_ID, playerProfiler.Avatar);
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
            settings.SelfSending = false;

            if (remoteEntities == null) return;
            remoteEntities.TryRemove(RemotePlayerMovementComponent.TEST_ID, world);

            selfReplicaEntity = null;
        }
    }
}
