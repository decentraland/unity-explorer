using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Components
{
    public struct AvatarAttachComponent
    {
        public Transform anchorPointTransform;
        public Vector3 lastAnchorPointPosition;
        public Quaternion lastAnchorPointRotation;
    }
}
