using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    internal static class AvatarAttachUtils
    {
        internal const float OLD_CLIENT_PIVOT_CORRECTION = -0.75f;

        internal static AvatarAttachComponent GetAnchorPointTransform(AvatarAnchorPointType anchorPointType, AvatarBase avatarBase)
        {
            switch (anchorPointType)
            {
                case AvatarAnchorPointType.AaptPosition:
                    return new AvatarAttachComponent(avatarBase.transform, OLD_CLIENT_PIVOT_CORRECTION);
                case AvatarAnchorPointType.AaptNameTag:
                    return avatarBase.NameTagAnchorPoint;
                case AvatarAnchorPointType.AaptHead:
                    return avatarBase.HeadAnchorPoint;
                case AvatarAnchorPointType.AaptNeck:
                    return avatarBase.NeckAnchorPoint;
                case AvatarAnchorPointType.AaptSpine:
                    return avatarBase.SpineAnchorPoint;
                case AvatarAnchorPointType.AaptSpine1:
                    return avatarBase.Spine1AnchorPoint;
                case AvatarAnchorPointType.AaptSpine2:
                    return avatarBase.Spine2AnchorPoint;
                case AvatarAnchorPointType.AaptHip:
                    return avatarBase.HipAnchorPoint;
                case AvatarAnchorPointType.AaptLeftShoulder:
                    return avatarBase.LeftShoulderAnchorPoint;
                case AvatarAnchorPointType.AaptLeftArm:
                    return avatarBase.LeftArmAnchorPoint;
                case AvatarAnchorPointType.AaptLeftForearm:
                    return avatarBase.LeftForearmAnchorPoint;
                case AvatarAnchorPointType.AaptLeftHand:
                    return avatarBase.LeftHandAnchorPoint;
                case AvatarAnchorPointType.AaptLeftHandIndex:
                    return avatarBase.LeftHandIndexAnchorPoint;
                case AvatarAnchorPointType.AaptRightShoulder:
                    return avatarBase.RightShoulderAnchorPoint;
                case AvatarAnchorPointType.AaptRightArm:
                    return avatarBase.RightArmAnchorPoint;
                case AvatarAnchorPointType.AaptRightForearm:
                    return avatarBase.RightForearmAnchorPoint;
                case AvatarAnchorPointType.AaptRightHand:
                    return avatarBase.RightHandAnchorPoint;
                case AvatarAnchorPointType.AaptRightHandIndex:
                    return avatarBase.RightHandIndexAnchorPoint;
                case AvatarAnchorPointType.AaptLeftUpLeg:
                    return avatarBase.LeftUpLegAnchorPoint;
                case AvatarAnchorPointType.AaptLeftLeg:
                    return avatarBase.LeftLegAnchorPoint;
                case AvatarAnchorPointType.AaptLeftFoot:
                    return avatarBase.LeftFootAnchorPoint;
                case AvatarAnchorPointType.AaptLeftToeBase:
                    return avatarBase.LeftToeBaseAnchorPoint;
                case AvatarAnchorPointType.AaptRightUpLeg:
                    return avatarBase.RightUpLegAnchorPoint;
                case AvatarAnchorPointType.AaptRightLeg:
                    return avatarBase.RightLegAnchorPoint;
                case AvatarAnchorPointType.AaptRightFoot:
                    return avatarBase.RightFootAnchorPoint;
                case AvatarAnchorPointType.AaptRightToeBase:
                    return avatarBase.RightToeBaseAnchorPoint;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchorPointType), anchorPointType, "Unknown anchor point type");
            }
        }

        internal static bool ApplyAnchorPointTransformValues(TransformComponent targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.AnchorPointTransform.position + (avatarAttachComponent.PivotCorrection * Vector3.up);
            Quaternion anchorPointRotation = avatarAttachComponent.AnchorPointTransform.rotation;
            var modifiedComponent = false;

            if (anchorPointPosition != targetTransform.Cached.WorldPosition)
            {
                targetTransform.Transform.position = anchorPointPosition;
                modifiedComponent = true;
            }

            if (anchorPointRotation != targetTransform.Cached.WorldRotation)
            {
                targetTransform.Transform.rotation = anchorPointRotation;
                modifiedComponent = true;
            }

            return modifiedComponent;
        }
    }
}
