using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;

namespace DCL.McpServer.Components
{
    /// <summary>
    ///     Present on the player entity while an agent-requested global input action awaits delivery.
    ///     McpInputActionSystem publishes the requested edge(s) into the same per-frame GlobalInputEvents buffer
    ///     the real key bindings feed, so the current scene receives an entity-less PBPointerEventsResult on its
    ///     root entity — the form an SDK7 scene reads through inputSystem.isTriggered / isPressed without any
    ///     entity being involved. A request the simulation never picks up is removed by the tool-side timeout.
    /// </summary>
    public struct McpInputActionIntent : IMcpEcsRequest<McpInputActionResult>
    {
        public readonly InputAction Action;

        /// <summary>
        ///     Pins delivery to one scene, matched by the definition id get_scene_state reports: the edge fails
        ///     instead of landing in whatever scene is current if the player moved after the request was made.
        ///     Null accepts the current scene as is.
        /// </summary>
        public readonly string? SceneId;

        /// <summary>The single edge to publish, or PetDown when <see cref="HoldSeconds" /> asks for a release too.</summary>
        public readonly PointerEventType EventType;

        /// <summary>
        ///     Seconds the button stays down before the matching release is published; null for a lone edge.
        ///     The release is owned by the system rather than by the caller, so an agent that disconnects mid-hold
        ///     cannot leave the scene believing the button is still held.
        /// </summary>
        public readonly float? HoldSeconds;

        public UniTaskCompletionSource<McpInputActionResult>? Completion { get; set; }

        /// <summary>UnityEngine.Time.time at which the press was published; null until it has been, which is also what tells the system a release is still owed.</summary>
        public float? PressTime;

        /// <summary>Scene tick the press is taken to have been stamped with; the release waits for the scene to pass it.</summary>
        public uint? PressTick;

        public McpInputActionIntent(InputAction action, string? sceneId, PointerEventType eventType, float? holdSeconds = null)
        {
            Action = action;
            SceneId = sceneId;
            EventType = eventType;
            HoldSeconds = holdSeconds;
            Completion = null;
            PressTime = null;
            PressTick = null;
        }
    }

    /// <summary>Wire-facing outcome of a global input action, serialized by the press_input_action tool.</summary>
    public struct McpInputActionResult
    {
        /// <summary>
        ///     The edge was published to a running current scene. That is as far as the client can attest: what
        ///     the scene's JavaScript does with it, or whether it polls that action at all, is not observable here.
        /// </summary>
        public bool Delivered;

        public string? FailureReason;

        /// <summary>Definition id of the scene the edge was published to, when one was resolved.</summary>
        public string? SceneId;

        /// <summary>How long the button was actually held, for a press that completed both legs.</summary>
        public float HeldSeconds;

        /// <summary>
        ///     The press was published but its release was not (the scene stopped being current or stopped
        ///     running mid-hold, or a newer request preempted this one): the scene still sees the button as
        ///     held, exactly as it would for a real key whose scene was torn down mid-press.
        /// </summary>
        public bool ReleaseMissed;
    }
}
