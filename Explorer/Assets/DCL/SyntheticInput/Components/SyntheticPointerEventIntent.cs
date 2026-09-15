using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.SyntheticInput.Core;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>At most one pending agent-requested pointer gesture, held on the player entity until it is delivered.</summary>
    public struct SyntheticPointerEventIntent : IEcsRequest<SyntheticPointerOutcome>
    {
        /// <summary>Arch entity id in the scene world; -1 when aiming at an explicit point or not aiming at all.</summary>
        public readonly int TargetEntityId;

        /// <summary>Pins the gesture to one scene, matched by the scene definition id; null accepts the current scene.</summary>
        public readonly string? SceneId;

        /// <summary>Explicit world-space aim point; when null the aim is the target's collider center.</summary>
        public readonly Vector3? AimPoint;

        /// <summary>Screen-space aim, used only when <see cref="AimPoint" /> is null.</summary>
        public readonly Vector2? ScreenPoint;

        public readonly InputAction Button;

        public readonly PointerEventType EventType;

        /// <summary>Set on the release leg of a click: the press this release must stay ordered after.</summary>
        public readonly SyntheticPressHandoff? Press;

        /// <summary>Absolute Time.time at which a hover hold ends.</summary>
        public readonly float HoldEndTime;

        /// <summary>Aim through UI covering the <see cref="ScreenPoint" />. Off by default, because a real click at a covered pixel lands on the UI.</summary>
        public readonly bool Force;

        public UniTaskCompletionSource<SyntheticPointerOutcome>? Completion { get; set; }

        /// <summary>Set once the synthetic input was posted; the outcome is observed one frame later.</summary>
        public bool Injected;

        /// <summary>Scene tick the synthetic input was posted on, carried by the press handoff for release ordering.</summary>
        public uint InjectedTick;

        /// <summary>The world point the posted aim targeted, to recognize the pipeline's answer on the observe frame.</summary>
        public Vector3 InjectedAimPoint;

        /// <summary>A hover-only aim hold; <see cref="Button" /> is ignored.</summary>
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
        {
            TargetEntityId = targetEntityId;
            SceneId = sceneId;
            AimPoint = aimPoint;
            ScreenPoint = screenPoint;
            Button = InputAction.IaAny;
            EventType = PointerEventType.PetHoverEnter;
            Press = null;
            HoldEndTime = holdEndTime;
            Force = force;
            Completion = null;
            Injected = false;
            InjectedTick = 0;
            InjectedAimPoint = Vector3.zero;
        }

        public static SyntheticPointerEventIntent Hover(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint, float holdEndTime, bool force = false) =>
            new (targetEntityId, sceneId, aimPoint, screenPoint, holdEndTime, force);
    }

    /// <summary>Where a delivered press landed. An aimless press hands off <see cref="Entity" /> Entity.Null: only the tick ordering and the world guard apply to its release.</summary>
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

        /// <summary>What UI covered a screen-addressed aim, when that is why nothing was clicked.</summary>
        public string? BlockedByUi;

        /// <summary>The release did not reach the press target, so the scene received only the PetDown.</summary>
        public bool UpRayMissed;

        /// <summary>The gesture did not complete within the driver-side timeout; the scene may have observed only part of it.</summary>
        public bool TimedOut;

        /// <summary>No entity consumed the edge, so the pipeline fanned it out to the scene root.</summary>
        public bool RootBroadcast;

        /// <summary>The wire shape both driver front-ends (MCP tools, AltTester probes) hand back, so a field means the same thing in either.</summary>
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

    /// <summary>
    ///     What a held-and-turn sweep achieved. A sweep whose press never landed reports
    ///     <see cref="FailureReason" /> and leaves the other two legs at their defaults.
    /// </summary>
    public struct SyntheticSweepResult
    {
        public SyntheticPointerResult Press;
        public SyntheticInputDelivery CameraSweep;
        public SyntheticPointerResult Release;

        /// <summary>Why the sweep was abandoned before the camera turned; null when the whole gesture ran.</summary>
        public string? FailureReason;
    }

    public struct SyntheticPointerOutcome
    {
        public SyntheticPointerResult Result;
        public SyntheticPressHandoff? Press;
    }
}
