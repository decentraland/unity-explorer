using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.SyntheticInput.Core;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>
    ///     <para>
    ///         Present on the player entity while a single agent-requested pointer gesture awaits delivery.
    ///         SyntheticPointerEventSystem drives the real reticle pipeline: it posts a synthetic aim and/or button
    ///         edge for one frame and reads the outcome from the pipeline's own raycast and hover state one frame
    ///         later. The request fields are immutable; a full click is composed by SyntheticInputAgent from two
    ///         intents — a press, then a release carrying the press <see cref="Press" /> handoff. A request the
    ///         simulation never picks up is removed by the driver-side timeout.
    ///     </para>
    ///     <para>
    ///         Three gesture shapes exist: a button edge aimed at a target (<see cref="EventType" /> PetDown/PetUp
    ///         with an entity, world-point or screen-point aim), a hover-only aim hold (<see cref="Hover" />, no
    ///         button, re-posted until <see cref="HoldEndTime" />), and an aimless button edge
    ///         (<see cref="HasAimTarget" /> false) that keeps the cursor ray and fans out to the scene exactly like
    ///         a real key press.
    ///     </para>
    /// </summary>
    public struct SyntheticPointerEventIntent : IEcsRequest<SyntheticPointerOutcome>
    {
        /// <summary>Arch entity id in the current scene world; -1 when aiming at an explicit point or not aiming at all.</summary>
        public readonly int TargetEntityId;

        /// <summary>
        ///     Pins delivery to one scene, matched by the definition id get_scene_state reports: the event fails
        ///     instead of landing in whatever scene is current if the player moved after the request was made.
        ///     Null accepts the current scene as is.
        /// </summary>
        public readonly string? SceneId;

        /// <summary>Explicit world-space aim point; when null the aim is the target's collider center.</summary>
        public readonly Vector3? AimPoint;

        /// <summary>Explicit screen-space aim: the ray is built through this screen point instead of a world point.</summary>
        public readonly Vector2? ScreenPoint;

        public readonly InputAction Button;

        /// <summary>PetDown or PetUp for button gestures; PetHoverEnter marks a hover-only aim hold.</summary>
        public readonly PointerEventType EventType;

        /// <summary>
        ///     Set on the release leg of a click: the press this release must stay ordered after. The synthetic
        ///     release is posted only once the scene has advanced past the press tick, and only while the world
        ///     that received the press is still the current one.
        /// </summary>
        public readonly SyntheticPressHandoff? Press;

        /// <summary>Hover-only gestures keep re-posting the aim until this Time.time, then observe the outcome.</summary>
        public readonly float HoldEndTime;

        /// <summary>
        ///     Aim through UI that covers a <see cref="ScreenPoint" />. Off by default, because a real click at a
        ///     covered pixel lands on the UI; set only to reach the world deliberately past an overlay.
        /// </summary>
        public readonly bool Force;

        public UniTaskCompletionSource<SyntheticPointerOutcome>? Completion { get; set; }

        /// <summary>Set once the synthetic input was posted to the pipeline; the outcome is observed one frame later.</summary>
        public bool Injected;

        /// <summary>Scene tick at the moment the synthetic input was posted; the press handoff carries it for release ordering.</summary>
        public uint InjectedTick;

        /// <summary>The world point the posted aim targeted, to recognize the pipeline's answer on the observe frame.</summary>
        public Vector3 InjectedAimPoint;

        /// <summary>A hover-only aim hold; <see cref="Button" /> is ignored.</summary>
        public bool IsHover => EventType == PointerEventType.PetHoverEnter;

        /// <summary>False when the gesture aims at nothing: the pipeline keeps the cursor ray and only the button edge is posted.</summary>
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

        /// <summary>A hover-only aim hold: no button edge, the aim is re-posted every frame until <paramref name="holdEndTime" />.</summary>
        public static SyntheticPointerEventIntent Hover(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint, float holdEndTime, bool force = false) =>
            new (targetEntityId, sceneId, aimPoint, screenPoint, holdEndTime, force);
    }

    /// <summary>
    ///     Where a delivered press landed. Handed back inside <see cref="SyntheticPointerOutcome" /> and passed
    ///     verbatim on the release intent of a click. An aimless (global) press hands off with
    ///     <see cref="Entity" /> set to Entity.Null: only the tick ordering and the world guard apply to its release.
    /// </summary>
    public struct SyntheticPressHandoff
    {
        public Arch.Core.World World;
        public Arch.Core.Entity Entity;
        public uint Tick;
    }

    /// <summary>Wire-facing outcome of a synthetic pointer event or click, handed back to the requesting driver.</summary>
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

        /// <summary>
        ///     The release did not reach the press target (it moved, died or got occluded after the press, or a
        ///     scene guard rejected the release): the scene received only the PetDown, exactly as it would for a
        ///     real cursor that lost its target mid-click.
        /// </summary>
        public bool UpRayMissed;

        /// <summary>
        ///     The simulation never completed the gesture within the driver-side timeout; the pending intent was
        ///     abandoned and the scene may have observed a partial gesture.
        /// </summary>
        public bool TimedOut;

        /// <summary>
        ///     The wire shape both driver front-ends (MCP tools, AltTester probes) hand back for a pointer gesture:
        ///     the same field means the same thing whichever tool or probe reports it.
        /// </summary>
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

            return json;
        }
    }

    /// <summary>
    ///     What a held-and-turn sweep achieved: the press that armed it, how the camera rotation ended, and the
    ///     release that closed it. A sweep whose press never landed reports <see cref="FailureReason" /> and never
    ///     ran the other two legs — the press outcome is the diagnostic that matters, because a scene watching for
    ///     a pointer-down arms on it or not at all.
    /// </summary>
    public struct SyntheticSweepResult
    {
        public SyntheticPointerResult Press;
        public SyntheticInputDelivery CameraSweep;
        public SyntheticPointerResult Release;

        /// <summary>Why the sweep was abandoned before the camera turned; null when the whole gesture ran.</summary>
        public string? FailureReason;
    }

    /// <summary>
    ///     What the fulfilling system hands back for one intent: the wire-facing <see cref="Result" /> plus, on a
    ///     delivered press, the <see cref="Press" /> handoff the release leg of a click carries.
    /// </summary>
    public struct SyntheticPointerOutcome
    {
        public SyntheticPointerResult Result;
        public SyntheticPressHandoff? Press;
    }
}
