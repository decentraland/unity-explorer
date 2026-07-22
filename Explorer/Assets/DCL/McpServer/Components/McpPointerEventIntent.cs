using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using UnityEngine;

namespace DCL.McpServer.Components
{
    /// <summary>
    ///     Present on the player entity while a single agent-requested pointer event awaits delivery.
    ///     McpPointerEventSystem drives the real reticle pipeline: it posts a synthetic aim and button edge for
    ///     one frame and reads the outcome from the pipeline's own raycast and hover state one frame later. The
    ///     request fields are immutable; a full click is composed by the click_entity tool from two intents — a
    ///     press, then a release carrying the press <see cref="Press" /> handoff. A request the simulation never
    ///     picks up is removed by the tool-side timeout.
    /// </summary>
    public struct McpEcsPointerEventIntent : IMcpEcsRequest<McpPointerClickResult>
    {
        /// <summary>Arch entity id in the current scene world; -1 when aiming at an explicit world point.</summary>
        public readonly int TargetEntityId;

        /// <summary>
        ///     Pins delivery to one scene, matched by the definition id get_scene_state reports: the event fails
        ///     instead of landing in whatever scene is current if the player moved after the request was made.
        ///     Null accepts the current scene as is.
        /// </summary>
        public readonly string? SceneId;

        /// <summary>Explicit world-space aim point; when null the aim is the target's collider center.</summary>
        public readonly Vector3? AimPoint;

        public readonly InputAction Button;

        /// <summary>PetDown or PetUp.</summary>
        public readonly PointerEventType EventType;

        /// <summary>
        ///     Set on the release leg of a click: the press this release must stay ordered after. The synthetic
        ///     release is posted only once the scene has advanced past the press tick, and only while the world
        ///     that received the press is still the current one.
        /// </summary>
        public readonly McpPressHandoff? Press;

        public UniTaskCompletionSource<McpPointerClickResult>? Completion { get; set; }

        /// <summary>Set once the synthetic input was posted to the pipeline; the outcome is observed one frame later.</summary>
        public bool Injected;

        /// <summary>Scene tick at the moment the synthetic input was posted; the press handoff carries it for release ordering.</summary>
        public uint InjectedTick;

        /// <summary>The world point the posted aim targeted, to recognize the pipeline's answer on the observe frame.</summary>
        public Vector3 InjectedAimPoint;

        public McpEcsPointerEventIntent(int targetEntityId, string? sceneId, Vector3? aimPoint, InputAction button, PointerEventType eventType, McpPressHandoff? press = null)
        {
            TargetEntityId = targetEntityId;
            SceneId = sceneId;
            AimPoint = aimPoint;
            Button = button;
            EventType = eventType;
            Press = press;
            Completion = null;
            Injected = false;
            InjectedTick = 0;
            InjectedAimPoint = Vector3.zero;
        }
    }

    /// <summary>
    ///     Where a delivered press landed. Handed back inside <see cref="McpPointerClickResult" /> and passed
    ///     verbatim on the release intent of a click. In-process only, never serialized.
    /// </summary>
    public struct McpPressHandoff
    {
        public Arch.Core.World World;
        public Arch.Core.Entity Entity;
        public uint Tick;
    }

    /// <summary>Outcome of a synthetic pointer event or click, serialized by the click_entity tool.</summary>
    public struct McpPointerClickResult
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

        /// <summary>
        ///     The release did not reach the press target (it moved, died or got occluded after the press):
        ///     the scene received only the PetDown, exactly as it would for a real cursor that lost its target
        ///     mid-click.
        /// </summary>
        public bool UpRayMissed;

        /// <summary>Where the press landed; the click_entity tool passes it back on the release leg of a click. In-process only.</summary>
        public McpPressHandoff? Press;
    }
}
