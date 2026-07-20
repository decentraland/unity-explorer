using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using UnityEngine;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.McpServer.Components
{
    /// <summary>
    ///     Present on the player entity while an agent-requested pointer click is in flight.
    ///     <see cref="DCL.McpServer.Systems.McpPointerClickSystem" /> validates the aim with a physics raycast each frame,
    ///     delivers the press through the target's <see cref="PBPointerEvents.AppendPointerEventResultsIntent" />
    ///     and removes the component once the click completes or fails.
    /// </summary>
    public struct McpPointerClickIntent
    {
        public enum ClickKind : byte
        {
            /// <summary>Pointer down, then pointer up on the next scene tick.</summary>
            CLICK,
            DOWN,
            UP,
        }

        public enum ClickPhase : byte
        {
            DOWN,
            WAIT_TICK,
            UP,
        }

        /// <summary>Arch entity id in the current scene world; -1 when aiming at an explicit world point.</summary>
        public int TargetEntityId;
        public Vector3 AimPoint;
        public bool HasExplicitAimPoint;
        public InputAction Button;
        public ClickKind Kind;
        public ClickPhase Phase;

        /// <summary>Time.time after which the click is abandoned.</summary>
        public float Deadline;
        public UniTaskCompletionSource<McpPointerClickResult>? Completion;

        // In-flight state owned by McpPointerClickSystem.

        /// <summary>
        ///     The scene world that received the press. Set only when a CLICK stays in flight across frames
        ///     awaiting the release; a different current world afterwards means the scene reloaded mid-click
        ///     and the rest of the in-flight state below is stale.
        /// </summary>
        public Arch.Core.World? DownWorld;
        public Arch.Core.Entity ResolvedEntity;
        public uint DownTick;
        public RaycastHit DownHit;
        public Ray DownRay;
        public McpPointerClickResult? DownResult;
    }

    /// <summary>Outcome of a synthetic pointer click, serialized by the click_entity tool.</summary>
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

        /// <summary>The release ray no longer hit the target (it moved after the press); PetUp was delivered with the press-frame hit.</summary>
        public bool UpRayMissed;
    }
}
