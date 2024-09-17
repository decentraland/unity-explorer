
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using Global.Dynamic;
using UnityEngine.Playables;
using UnityEngine;

namespace DCL.AvatarAnimation
{
    /// <summary>
    /// A playable / clip for the Unity timeline that makes an avatar move and / or rotate. When the clip is not playing, the avatar does not move.
    /// Movements do not depend on the player's camera.
    /// Note: It can't work with player's avatar, these transformations are  overriden by real inputs.
    /// </summary>
    public class MoveAvatarPlayableBehaviour : PlayableBehaviour
    {
        public float Forward = 0.0f;
        public MovementKind MovementAnimation = MovementKind.IDLE;
        public float Rotation = 0.0f;

        private Entity cachedEntity = Entity.Null;
        private AvatarBase cachedAvatar;
        private Transform cachedCharacterControllerTransform;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Gets the asset associated to the track
            AvatarBase avatar = (AvatarBase)playerData;

            // If the asset was changed in the editor, update the cached data
            if (avatar != cachedAvatar)
            {
                cachedAvatar = avatar;
                cachedEntity = FindEntityFromAvatarBase(cachedAvatar);
                cachedCharacterControllerTransform = cachedAvatar.GetComponentInParent<CharacterController>().transform;
            }

            if (cachedEntity != Entity.Null && GlobalWorld.ECSWorldInstance.Has<MovementInputComponent>(cachedEntity))
            {
                ref MovementInputComponent movement = ref GlobalWorld.ECSWorldInstance.TryGetRef<MovementInputComponent>(cachedEntity, out bool exists);
                movement.Kind = MovementAnimation;
                movement.Axes = new Vector2(0.0f, Forward);

                cachedCharacterControllerTransform.rotation *= Quaternion.AngleAxis(-Rotation * Time.deltaTime, Vector3.up);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if(cachedAvatar == null || cachedEntity == Entity.Null)
                return;

            ref MovementInputComponent movement = ref GlobalWorld.ECSWorldInstance.TryGetRef<MovementInputComponent>(cachedEntity, out bool hasInputcomponent);

            if (hasInputcomponent)
            {
                movement.Kind = MovementKind.IDLE;
                movement.Axes = Vector2.zero;
            }
        }

        private static Entity FindEntityFromAvatarBase(AvatarBase avatar)
        {
            Entity foundEntity = Entity.Null;
            Query allAvatars = GlobalWorld.ECSWorldInstance.Query(new QueryDescription().WithAll<AvatarBase>());

            foreach (ref var chunk in allAvatars)
            {
                AvatarBase[] avatars = chunk.GetArray<AvatarBase>();

                foreach (int entityIndex in chunk)
                    if (entityIndex > -1 && avatars[entityIndex] == avatar)
                    {
                        foundEntity = chunk.Entity(entityIndex);
                        break;
                    }

                if (foundEntity != Entity.Null)
                    break;
            }

            return foundEntity;
        }
    }
}
