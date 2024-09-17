
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using Global.Dynamic;
using UnityEngine.Playables;
using UnityEngine;

namespace DCL.AvatarAnimation
{
    // Note: It can't work with player avatar, input is overriden by real input

    /// <summary>
    /// A playable / clip for the Unity timeline that sets the position and rotation of an avatar.
    /// </summary>
    public class TeleportAvatarPlayableBehaviour : PlayableBehaviour
    {
        public Transform ReferenceTransform;

        private Entity cachedEntity = Entity.Null;
        private AvatarBase cachedAvatar;
        private CharacterController cachedCharacterController;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Gets the asset associated to the track
            AvatarBase avatar = (AvatarBase)playerData;

            if (ReferenceTransform == null)
            {
                string avatarName = avatar != null ? avatar.name : "<Not assigned>";
                Debug.LogError("'Teleport avatar' clip requires a reference to a Transform (track with avatar: '" + avatarName + "'). Please select the clip and set the transform.");
                return;
            }

            // If the asset was changed in the editor, update the cached data
            if (avatar != cachedAvatar)
            {
                cachedAvatar = avatar;
                cachedEntity = FindEntityFromAvatarBase(cachedAvatar);
                cachedCharacterController = cachedAvatar.GetComponentInParent<CharacterController>();
            }

            if (cachedEntity != Entity.Null && cachedCharacterController != null)
            {
                // This must be changed too so the RotateCharacterSystem does not override the rotation
                CharacterRigidTransform rigidTransform = GlobalWorld.ECSWorldInstance.Get<CharacterRigidTransform>(cachedEntity);
                rigidTransform.LookDirection = ReferenceTransform.rotation * Vector3.forward;

                // It has to be disabled, otherwise position will be overriden
                cachedCharacterController.enabled = false;
                cachedCharacterController.transform.position = ReferenceTransform.position;
                cachedCharacterController.transform.rotation = ReferenceTransform.rotation;
                cachedCharacterController.enabled = true;
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
