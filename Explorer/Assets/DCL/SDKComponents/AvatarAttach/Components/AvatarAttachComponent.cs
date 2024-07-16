using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Components
{
    public struct AvatarAttachComponent
    {
        public Transform AnchorPointTransform { get; private set; }
        public AvatarAnchorPointType PointType { get; private set; }

        public AvatarAttachComponent(Transform anchorPointTransform, AvatarAnchorPointType pointType)
        {
            AnchorPointTransform = anchorPointTransform;
            PointType = pointType;
        }

        public void Update(Transform newTransform, AvatarAnchorPointType pointType)
        {
            AnchorPointTransform = newTransform;
            PointType = pointType;
        }

        public override string ToString() =>
            $"({nameof(AvatarAttachComponent)} {nameof(AnchorPointTransform)}: {AnchorPointTransform}; {nameof(PointType)}: {PointType})";
    }
}
