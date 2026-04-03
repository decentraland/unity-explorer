using UnityEngine;
using Utility;

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
        [field: Header("POOL")]
        [field: Note("INIT-ONLY — read once to allocate the particle ring-buffer. " +
                     "Changing at runtime has no effect.")]
        [field: Range(64, 1023)]
        [field: SerializeField] public int MaxParticles { get; private set; } = 1023;

        [field: Note("Soft cap on visible UI particles. New particles are dropped when alive count exceeds this. " +
                     "Must be ≤ MaxParticles. 0 = disabled (uses MaxParticles pool cap only).")]
        [field: Range(0, 1023)]
        [field: SerializeField] public int MaxVisibleParticles { get; private set; } = 50;

        [field: Header("PLACEMENT")]
        [field: Note("How far in front of the camera (world units) the particle quads are rendered. " +
                     "Too close clips through UI; too far gets occluded by geometry.")]
        [field: Range(0.1f, 10f)]
        [field: SerializeField] public float DepthFromCamera { get; private set; } = 1.0f;

        [field: Header("PHYSICS (SCREEN SPACE — PIXELS, PX/SEC, PX/SEC²)")]
        [field: Note("How long each particle lives (seconds). Randomised per particle between min and max.")]
        [field: MinMaxRange(0f, 5f)]
        [field: SerializeField] public Vector2 LifetimeRange { get; private set; } = new(1.0f, 1.8f);

        [field: Note("Upward launch speed when UseFlightPath is OFF (simple straight-up mode). " +
                     "Ignored when flight path is ON — InitialUpRange is used instead.")]
        [field: MinMaxRange(0f, 500f)]
        [field: SerializeField] public Vector2 SpeedRange { get; private set; } = new(80f, 200f);

        [field: Note("Final particle size in pixels. Each particle spawns at 20-50% of this and grows to full size.")]
        [field: MinMaxRange(0f, 200f)]
        [field: SerializeField] public Vector2 SizeRange { get; private set; } = new(40f, 80f);

        [field: Note("Linear drag — 0 = no slowdown, higher = particles decelerate faster.")]
        [field: Range(0f, 5f)]
        [field: SerializeField] public float Drag { get; private set; } = 1.2f;

        [field: Note("Constant acceleration (px/sec²). X = horizontal drift, Y = vertical. " +
                     "Zero = floating-upward feel; negative Y = confetti falling down.")]
        [field: SerializeField] public Vector2 Gravity { get; private set; } = Vector2.zero;

        [field: Header("STREAMING (HOLD-TO-EMIT)")]
        [field: Note("Emission ticks per second while holding the reaction button. Each tick spawns StreamBurst particles.")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float StreamRatePerSecond { get; private set; } = 8f;

        [field: Note("Particles per emission tick. Also the default burst count for single-tap reactions.")]
        [field: Range(1, 10)]
        [field: SerializeField] public int StreamBurst { get; private set; } = 1;

        [field: Header("FLIGHT PATH")]
        [field: Note("INIT-ONLY — switches motion modes. " +
                     "ON = balloon-style (kick + float + zig-zag). OFF = simple straight-up (uses SpeedRange). " +
                     "Toggling at runtime has no effect.")]
        [field: SerializeField] public bool UseFlightPath { get; private set; }

        [field: Note("Horizontal kick at spawn (px/sec) — creates the initial sideways arc. Flight path only.")]
        [field: MinMaxRange(0f, 500f)]
        [field: SerializeField] public Vector2 KickSpeedRange { get; private set; } = new(120f, 220f);

        [field: Note("Gentle upward speed at spawn (px/sec). Much lower than kick so particles arc out first. Flight path only.")]
        [field: MinMaxRange(0f, 200f)]
        [field: SerializeField] public Vector2 InitialUpRange { get; private set; } = new(10f, 40f);

        [field: Note("Sustained upward push every frame (px/sec²). Makes particles keep rising after the kick fades. Flight path only.")]
        [field: Range(0f, 200f)]
        [field: SerializeField] public float FloatUpAcceleration { get; private set; } = 60f;

        [field: Note("Peak sideways acceleration of the zig-zag (px/sec²). Higher = wider lateral swings. 0 = straight. Flight path only.")]
        [field: Range(0f, 200f)]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 50f;

        [field: Note("Zig-zag speed (Hz). 1 = one full left-right cycle per second. Higher = tighter weaving. Flight path only.")]
        [field: Range(0.1f, 5f)]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 1.2f;

        [field: Note("INIT-ONLY — size multiplier curve over lifetime [0,1]. " +
                     "Default: pop in, hold, slight overshoot, shrink to zero. Flight path only.")]
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
