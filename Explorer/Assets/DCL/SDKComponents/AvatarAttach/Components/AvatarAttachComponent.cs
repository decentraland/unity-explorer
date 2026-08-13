using System;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Components
{
    public struct AvatarAttachComponent
    {
        public readonly Transform AnchorPointTransform;

        [Obsolete("It's a cheat to eliminate 0.75 offset from the old client")]
        public readonly float PivotCorrection;

        public AvatarAttachComponent(Transform anchorPointTransform, float pivotCorrection = 0)
        {
            AnchorPointTransform = anchorPointTransform;
#pragma warning disable CS0618 // PivotCorrection is an intentional cheat to offset the old-client 0.75
            PivotCorrection = pivotCorrection;
#pragma warning restore CS0618
        }

        public static implicit operator AvatarAttachComponent(Transform transform) =>
            new (transform);
    }
}
