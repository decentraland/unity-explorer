using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Tuning for the screen-space UI particle lane — the emoji bursts that float upward
    /// above the chat panel when a user triggers a situational reaction.
    /// All physics values are in screen space (pixels, pixels/sec, pixels/sec²).
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsUILaneConfig",
                     menuName = "DCL/Chat/Reactions/UI Lane Config")]
    public class ChatReactionsUILaneConfig : ScriptableObject
    {
        [field: Header("Pool")]
        [field: Min(64)]
        [field: Tooltip("Ring-buffer capacity — the maximum number of simultaneously live particles. " +
                        "Keep at or below 1023 to stay within a single DrawMeshInstanced call.")]
        [field: SerializeField] public int MaxParticles { get; private set; } = 1023;

        [field: Header("Placement")]
        [field: Min(0.1f)]
        [field: Tooltip("Distance in front of the camera (world units) at which particles are rendered.")]
        [field: SerializeField] public float DepthFromCamera { get; private set; } = 1.0f;

        [field: Header("Physics (screen space — pixels, px/sec, px/sec²)")]
        [field: Tooltip("Particle lifetime in seconds (min, max).")]
        [field: SerializeField] public Vector2 LifetimeRange { get; private set; } = new(1.0f, 1.8f);

        [field: Tooltip("Initial upward speed in pixels/sec (min, max).")]
        [field: SerializeField] public Vector2 SpeedRange { get; private set; } = new(80f, 200f);

        [field: Tooltip("Particle size at death in pixels (min, max). Spawn size is 20–50% of this.")]
        [field: SerializeField] public Vector2 SizeRange { get; private set; } = new(40f, 80f);

        [field: Min(0f)]
        [field: Tooltip("Linear drag coefficient. Higher values slow particles faster.")]
        [field: SerializeField] public float Drag { get; private set; } = 1.2f;

        [field: Tooltip("Acceleration in screen space (pixels/sec²). Keep at zero for floating-upward look.")]
        [field: SerializeField] public Vector2 Gravity { get; private set; } = Vector2.zero;

        [field: Header("Streaming (hold-to-emit)")]
        [field: Min(0f)]
        [field: Tooltip("Particles emitted per second while a stream is active (button held down).")]
        [field: SerializeField] public float StreamRatePerSecond { get; private set; } = 8f;

        [field: Min(1)]
        [field: Tooltip("How many particles to spawn per stream tick.")]
        [field: SerializeField] public int StreamBurst { get; private set; } = 1;

        [field: Header("Defaults")]
        [field: Min(0)]
        [field: Tooltip("Atlas tile index used when no specific emoji is requested.")]
        [field: SerializeField] public int DefaultEmojiIndex { get; private set; } = 500;

        [field: Tooltip("Pick a random atlas tile on each emission instead of DefaultEmojiIndex.")]
        [field: SerializeField] public bool RandomEmoji { get; private set; } = true;

        [field: Header("Rendering")]
        [field: Tooltip("Unity rendering layer for UI particles. Must be included in the main camera's culling mask.")]
        [field: SerializeField] public int RenderLayer { get; private set; } = 0;

        [field: Header("Flight Path")]
        [field: Tooltip("Enable balloon-style flight path with horizontal kick and zig-zag oscillation. " +
                        "When disabled, particles float straight upward using SpeedRange.")]
        [field: SerializeField] public bool UseFlightPath { get; private set; }

        [field: Tooltip("Horizontal kick speed range at spawn (px/sec). Only used when UseFlightPath is true.")]
        [field: SerializeField] public Vector2 KickSpeedRange { get; private set; } = new(120f, 220f);

        [field: Tooltip("Initial upward speed range at spawn (px/sec). Overrides SpeedRange when UseFlightPath is true.")]
        [field: SerializeField] public Vector2 InitialUpRange { get; private set; } = new(10f, 40f);

        [field: Tooltip("Upward acceleration in px/sec² applied every frame for sustained buoyancy.")]
        [field: SerializeField] public float FloatUpAcceleration { get; private set; } = 60f;

        [field: Tooltip("Peak lateral acceleration in px/sec² for sinusoidal zig-zag oscillation.")]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 50f;

        [field: Min(0.1f)]
        [field: Tooltip("Zig-zag oscillation frequency in Hz.")]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 1.2f;

        [field: Tooltip("Size multiplier curve over normalised lifetime [0,1]. " +
                        "Used when UseFlightPath is true for pop/shrink effects.")]
        [field: SerializeField] public AnimationCurve SizeOverLifetime { get; private set; } = DefaultFlightSizeCurve();

        private static AnimationCurve DefaultFlightSizeCurve() =>
            new AnimationCurve(
                new Keyframe(0.00f, 0.20f),
                new Keyframe(0.12f, 1.00f),
                new Keyframe(0.72f, 1.00f),
                new Keyframe(0.88f, 1.35f),
                new Keyframe(1.00f, 0.00f)
            );
    }
}
