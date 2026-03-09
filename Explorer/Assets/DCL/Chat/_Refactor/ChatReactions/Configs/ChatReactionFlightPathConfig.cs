using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Balloon-style flight config for screen-space emoji particles.
    /// Particles get a horizontal kick at spawn, then float upward with zig-zag oscillation.
    /// All values are in screen space: pixels/sec for velocity, pixels/sec² for acceleration.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionFlightPathConfig",
                     menuName = "DCL/Chat/Reactions/Flight Path Config")]
    public class ChatReactionFlightPathConfig : ScriptableObject
    {
        [field: Header("Kick — applied once at spawn (px/sec)")]

        [field: Tooltip("Horizontal kick speed range. Pushes particles away from the chat panel.")]
        [field: SerializeField] public Vector2 KickSpeedRange { get; private set; } = new(120f, 220f);

        [field: Tooltip("Initial upward speed range at spawn. Keep low for balloon-like gentle release. " +
                        "Overrides UILaneConfig.SpeedRange when flight path is assigned.")]
        [field: SerializeField] public Vector2 InitialUpRange { get; private set; } = new(10f, 40f);

        [field: Header("Float — sustained upward buoyancy")]

        [field: Tooltip("Upward acceleration in px/sec² applied every frame. Counteracts drag to keep particles rising.")]
        [field: SerializeField] public float FloatUpAcceleration { get; private set; } = 60f;

        [field: Header("Zig-Zag — lateral oscillation while floating")]

        [field: Tooltip("Peak lateral acceleration in px/sec² for the sinusoidal zig-zag. Higher = wider sway.")]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 50f;

        [field: Min(0.1f)]
        [field: Tooltip("Oscillation frequency in Hz. Lower = slow gentle sway, higher = rapid wobble.")]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 1.2f;

        [field: Header("Size animation over lifetime")]

        [field: Tooltip("Size multiplier curve over normalised lifetime [0,1]. " +
                        "Default: small spawn, full size mid-life, brief enlargement, then pop to zero.")]
        [field: SerializeField] public AnimationCurve SizeOverLifetime { get; private set; } = DefaultSizeCurve();

        private static AnimationCurve DefaultSizeCurve() =>
            new AnimationCurve(
                new Keyframe(0.00f, 0.20f),
                new Keyframe(0.12f, 1.00f),
                new Keyframe(0.72f, 1.00f),
                new Keyframe(0.88f, 1.35f),
                new Keyframe(1.00f, 0.00f)
            );
    }
}
