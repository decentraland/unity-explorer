using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.SyntheticInput.Core;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>One pending pointer gesture, held on the player entity until it is delivered.</summary>
    public struct SyntheticPointerEventIntent : IEcsRequest<SyntheticPointerOutcome>
    {
        /// <summary>Arch entity id in the scene world. -1 when the aim names no entity.</summary>
        public readonly int TargetEntityId;

        public readonly string? SceneId;

        public readonly Vector3? AimPoint;

        /// <summary>Used only when <see cref="AimPoint" /> is null.</summary>
        public readonly Vector2? ScreenPoint;

        public readonly InputAction Button;

        public readonly PointerEventType EventType;

        /// <summary>On a release: the press that this release must stay ordered after.</summary>
        public readonly SyntheticPressHandoff? Press;

        /// <summary>Value of Time.time at which a hover hold ends.</summary>
        public readonly float HoldEndTime;

        /// <summary>Aim through UI that covers the <see cref="ScreenPoint" />.</summary>
        public readonly bool Force;

        /// <summary>Written by the delivering system once the input is posted.</summary>
        public bool Injected;

        /// <summary>Scene tick on which the input was posted.</summary>
        public uint InjectedTick;

        /// <summary>World point that the posted aim targeted.</summary>
        public Vector3 InjectedAimPoint;

        public UniTaskCompletionSource<SyntheticPointerOutcome>? Completion { get; set; }

        /// <summary>True for a hover hold. <see cref="Button" /> is ignored then.</summary>
        public bool IsHover => EventType == PointerEventType.PetHoverEnter;

        public bool HasAimTarget => TargetEntityId >= 0 || AimPoint.HasValue || ScreenPoint.HasValue;

        public SyntheticPointerEventIntent(int targetEntityId, string? sceneId, Vector3? aimPoint, InputAction button, PointerEventType eventType,
            SyntheticPressHandoff? press = null, Vector2? screenPoint = null, bool force = false)
        {
            TargetEntityId = targetEntityId;
            SceneId = sceneId;
            AimPoint = aimPoint;
            ScreenPoint = screenPoint;
            Button = button;
            EventType = eventType;
            Press = press;
            HoldEndTime = 0f;
            Force = force;
            Completion = null;
            Injected = false;
            InjectedTick = 0;
            InjectedAimPoint = Vector3.zero;
        }

        private SyntheticPointerEventIntent(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint, float holdEndTime, bool force)
            : this(targetEntityId, sceneId, aimPoint, InputAction.IaAny, PointerEventType.PetHoverEnter, null, screenPoint, force)
        {
            HoldEndTime = holdEndTime;
        }

        public static SyntheticPointerEventIntent Hover(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint, float holdEndTime, bool force = false) =>
            new (targetEntityId, sceneId, aimPoint, screenPoint, holdEndTime, force);
    }

    /// <summary>Where a delivered press landed. <see cref="Entity" /> is Entity.Null for an aimless press.</summary>
    public struct SyntheticPressHandoff
    {
        public Arch.Core.World World;
        public Arch.Core.Entity Entity;
        public uint Tick;
    }

    public struct SyntheticPointerResult
    {
        public bool Hit;
        public string? FailureReason;
        public int SceneEntityId;
        public int CrdtEntityId;
        public string? HoverText;
        public Vector3 HitPoint;
        public float Distance;
        public int? BlockedByEntityId;
        public int? BlockedByCrdtId;
        public string? BlockedByColliderName;

        /// <summary>The UI that covered a screen-space aim, when that is why nothing was hit.</summary>
        public string? BlockedByUi;

        /// <summary>The release did not reach the press target, so the scene received only the press.</summary>
        public bool UpRayMissed;

        /// <summary>The gesture did not complete within the driver-side timeout.</summary>
        public bool TimedOut;

        /// <summary>The edge reached the scene root because no entity consumed it.</summary>
        public bool RootBroadcast;

        public readonly JObject ToJson()
        {
            var json = new JObject
            {
                ["hit"] = Hit,
                ["entityId"] = SceneEntityId,
                ["crdtEntityId"] = CrdtEntityId,
            };

            if (FailureReason != null)
                json["reason"] = FailureReason;

            if (BlockedByUi != null)
                json["blockedByUi"] = BlockedByUi;

            if (Hit)
            {
                json["hitPoint"] = new JObject
                {
                    ["x"] = Math.Round(HitPoint.x, 2),
                    ["y"] = Math.Round(HitPoint.y, 2),
                    ["z"] = Math.Round(HitPoint.z, 2),
                };

                json["distance"] = Math.Round(Distance, 2);
            }

            if (HoverText != null)
                json["hoverText"] = HoverText;

            if (BlockedByEntityId != null)
            {
                json["blockedByEntityId"] = BlockedByEntityId;
                json["blockedByCrdtId"] = BlockedByCrdtId;
                json["blockedByCollider"] = BlockedByColliderName;
            }

            if (UpRayMissed)
                json["upRayMissed"] = true;

            if (TimedOut)
                json["timedOut"] = true;

            if (RootBroadcast)
                json["rootBroadcast"] = true;

            return json;
        }
    }

    /// <summary>Result of a press, camera-turn and release gesture.</summary>
    public struct SyntheticSweepResult
    {
        public SyntheticPointerResult Press;
        public SyntheticInputDelivery CameraSweep;
        public SyntheticPointerResult Release;

        /// <summary>Set when the press failed and the gesture stopped. The later legs are then at their defaults.</summary>
        public string? FailureReason;
    }

    public struct SyntheticPointerOutcome
    {
        public SyntheticPointerResult Result;
        public SyntheticPressHandoff? Press;
    }
}
