using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Multiplayer.Connections.Typing;
using DCL.Multiplayer.Profiles.Tables;
using System.Diagnostics;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.SDKComponents.Utils
{
    public static class FindAvatarUtils
    {
        private static readonly QueryDescription AVATAR_BASE_AND_SHAPE_QUERY = new QueryDescription().WithAll<AvatarBase, AvatarShapeComponent>();
        private static readonly QueryDescription AVATAR_BASE_QUERY = new QueryDescription().WithAll<AvatarBase>();

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEBUG")]
        private static void AssertMainThread() =>
            MultithreadingUtility.AssertMainThread(nameof(FindAvatarUtils), true);

        public static LightResult<AvatarBase> AvatarWithID(World globalWorld, string id, IReadOnlyEntityParticipantTable entityParticipantTable)
        {
            AssertMainThread();

            // Try to find the avatar using the EntityParticipantTable
            if (entityParticipantTable.TryGet(id, out IReadOnlyEntityParticipantTable.Entry entry)
                && globalWorld.TryGet(entry.Entity, out AvatarBase? avatarBase))
                return new LightResult<AvatarBase>(avatarBase!);

            // Fall back to the less performant ECS query approach if the entity participant table lookup failed
            AvatarBase? foundEntity = null;

            globalWorld.Query(in AVATAR_BASE_AND_SHAPE_QUERY, entity =>
            {
                if (foundEntity) return;

                AvatarShapeComponent avatarShape = globalWorld.Get<AvatarShapeComponent>(entity);
                if (avatarShape.ID != id) return;

                foundEntity = globalWorld.Get<AvatarBase>(entity);
            });

            return !foundEntity
                ? LightResult<AvatarBase>.FAILURE
                : new LightResult<AvatarBase>(foundEntity!);
        }

        public static LightResult<Entity> AvatarWithTransform(World globalWorld, Transform avatarTransform)
        {
            AssertMainThread();

            Entity foundEntity = Entity.Null;

            globalWorld.Query(in AVATAR_BASE_QUERY, entity =>
            {
                if (foundEntity != Entity.Null) return;

                Transform t = globalWorld.Get<AvatarBase>(entity).transform;
                // Support our own avatar, based on its hierarchy:
                // - CharacterObject: CharacterController which affects the trigger event
                // -    Avatar {userId}: AvatarBase
                if (t.parent != avatarTransform
                    // Support peer avatars, based on its hierarchy:
                    // - REMOTE_ENTITY_{userId}
                    // -    Collider {userId} which affects the trigger event
                    // -    Avatar {userId}: AvatarBase
                    && t.parent != avatarTransform.parent) return;

                foundEntity = entity;
            });

            return foundEntity == Entity.Null
                ? LightResult<Entity>.FAILURE
                : new LightResult<Entity>(foundEntity);
        }
    }
}
