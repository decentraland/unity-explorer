using System.Collections.Generic;
using UnityEngine;

namespace DCL.SpringBones
{
    public struct SpringBoneSlot
    {
        public int SlotIndex;
        public Transform WearableParent;
        public Transform AvatarParent;
        public Vector3 RestAvatarScale;
    }

    public struct SpringBoneRegistrationComponent
    {
        public List<SpringBoneSlot> Slots;
        public int AvatarVersion;
    }
}
