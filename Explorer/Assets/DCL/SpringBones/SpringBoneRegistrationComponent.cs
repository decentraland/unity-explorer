using System.Collections.Generic;
using UnityEngine;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Holds spring bones per-avatar data.
    /// </summary>
    public struct SpringBoneRegistrationComponent
    {
        /// <summary>
        ///     The avatar version the currently simulated springs were built against.
        ///     Every time the avatar is marked dirty, its version number is also bumped.
        /// </summary>
        public int AvatarVersion;

        /// <summary>
        ///     Which slots in the <see cref="SpringBoneService"/> are reserved for this avatar.
        ///     Slots represent all the data that is needed for the springs to be simulated (a slice into multiple arrays).
        /// </summary>
        public List<int> Slots;

        /// <summary>
        ///     A list of (wearable bone, avatar bone) pairs that need to be kept in sync.
        ///     Wearables with springs have extra bones that are not part of the avatar skeleton. Since the avatar skeleton is animated we need to keep
        ///     the parent bone of each spring in sync with the animated avatar bone.
        /// </summary>
        public List<(Transform wearableParent, Transform skeletonBone)> SyncedBones;
    }
}
