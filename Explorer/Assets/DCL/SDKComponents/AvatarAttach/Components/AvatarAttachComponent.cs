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
            PivotCorrection = pivotCorrection;
        }

        public static implicit operator AvatarAttachComponent(Transform transform) =>
            new (transform);
    }
}
