
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
    public class TeleportAvatarPlayableBehaviour : BaseAvatarPlayableBehaviour
    {
        public Transform ReferenceTransform;

        private CharacterController cachedCharacterController;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            base.ProcessFrame(playable, info, playerData);

            if (ReferenceTransform == null)
            {
                string avatarName = playerData != null ? ((AvatarBase)playerData).name : "<Not assigned>";
                Debug.LogError("'Teleport avatar' clip requires a reference to a Transform (track with avatar: '" + avatarName + "'). Please select the clip and set the transform.");
                return;
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

        protected override void OnAvatarChanged(Entity newEntity, AvatarBase newAvatar)
        {
            cachedCharacterController = newAvatar?.GetComponentInParent<CharacterController>();
        }
    }
}
