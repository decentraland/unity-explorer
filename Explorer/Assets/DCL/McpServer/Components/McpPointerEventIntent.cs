using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using UnityEngine;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.McpServer.Components
{
    /// <summary>
    ///     Present on the player entity while a single agent-requested pointer event awaits delivery.
    ///     McpPointerEventSystem validates the aim with a physics raycast, appends the event to the target's
    ///     <see cref="PBPointerEvents.AppendPointerEventResultsIntent" /> and removes the component. The request
    ///     is immutable; a full click is composed by the click_entity tool from two intents — a press, then a
    ///     release carrying the press <see cref="Press" /> handoff. A request the simulation never picks up is
    ///     removed by the tool-side timeout.
    /// </summary>
    public struct McpEcsPointerEventIntent : IMcpEcsRequest<McpPointerClickResult>
    {
        /// <summary>Arch entity id in the current scene world; -1 when aiming at an explicit world point.</summary>
        public readonly int TargetEntityId;

        /// <summary>Explicit world-space aim point; when null the aim is the target's collider center.</summary>
        public readonly Vector3? AimPoint;

        public readonly InputAction Button;

        /// <summary>PetDown or PetUp.</summary>
        public readonly PointerEventType EventType;

        /// <summary>
        ///     Set on the release leg of a click: the press this release must stay ordered after. Delivery waits
        ///     until the scene has advanced past the press tick, is bound to the world that received the press,
        ///     and falls back to the press-frame hit when the fresh ray no longer reaches the target.
        /// </summary>
        public readonly McpPressHandoff? Press;

        public UniTaskCompletionSource<McpPointerClickResult>? Completion { get; set; }

        public McpEcsPointerEventIntent(int targetEntityId, Vector3? aimPoint, InputAction button, PointerEventType eventType, McpPressHandoff? press = null)
        {
            TargetEntityId = targetEntityId;
            AimPoint = aimPoint;
            Button = button;
            EventType = eventType;
            Press = press;
            Completion = null;
        }
    }

    /// <summary>
    ///     Where a delivered pointer event landed. Handed back inside <see cref="McpPointerClickResult" /> and
    ///     passed verbatim on the release intent of a click. In-process only, never serialized.
    /// </summary>
    public struct McpPressHandoff
    {
        public Arch.Core.World World;
        public Arch.Core.Entity Entity;
        public uint Tick;
        public RaycastHit Hit;
        public Ray Ray;
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
        ///     The release ray no longer hit the target (it moved after the press): PetUp was delivered with the
        ///     press-frame hit, or not at all when <see cref="Hit" /> is also false.
        /// </summary>
        public bool UpRayMissed;

        /// <summary>Where the event landed; the click_entity tool passes it back on the release leg of a click. In-process only.</summary>
        public McpPressHandoff? Press;
    }
}
