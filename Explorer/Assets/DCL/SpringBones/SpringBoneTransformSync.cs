using UnityEngine;

namespace DCL.SpringBones
{
    internal static class SpringBoneTransformSync
    {
        /// <summary>
        /// Aligns the wearable's parent pivot to the corresponding avatar bone, dividing
        /// localScale by parent-of-wearable lossy scale so the wearable inherits the avatar's
        /// world scale exactly. Returns the avatar's lossy scale for callers needing it.
        /// </summary>
        public static Vector3 SyncWearableParentToAvatar(Transform wearableParent, Transform avatarParent)
        {
            wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

            Vector3 parentLossy = wearableParent.parent != null ? wearableParent.parent.lossyScale : Vector3.one;
            Vector3 avatarLossy = avatarParent.lossyScale;

            wearableParent.localScale = new Vector3(
                avatarLossy.x / parentLossy.x,
                avatarLossy.y / parentLossy.y,
                avatarLossy.z / parentLossy.z);

            return avatarLossy;
        }
    }
}