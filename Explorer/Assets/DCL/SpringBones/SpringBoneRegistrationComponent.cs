using System.Collections.Generic;
using UnityEngine;

namespace DCL.SpringBones
{
    public struct SpringBoneSlot
    {
        public int SlotIndex;
        public Transform WearableParent;
        public Transform AvatarParent;
    }

    public struct SpringBoneRegistrationComponent
    {
        public List<SpringBoneSlot> Slots;
        public int AvatarVersion;
        public int StructuralHash;
    }
}
