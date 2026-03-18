using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Tuning for the world-space particle lane — the emoji bursts that float upward
    /// above an avatar's head when a situational reaction is triggered in world space.
    /// All physics values are in world space (units, units/sec, units/sec²).
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsWorldLaneConfig",
                     menuName = "DCL/Chat/Reactions/World Lane Config")]
    public class ChatReactionsWorldLaneConfig : ScriptableObject
    {
        [field: Header("Pool")]
        [field: Note("INIT-ONLY — read once to allocate the particle ring-buffer. " +
                     "Keep at or below 1023 to stay within a single RenderMeshInstanced call.")]
        [field: Range(64, 1023)]
        [field: SerializeField] public int MaxParticles { get; private set; } = 1023;

        [field: Header("Physics (world space — units, units/sec, units/sec²)")]
        [field: Note("How long each particle lives (seconds). Randomised per particle between min and max.")]
        [field: MinMaxRange(0f, 5f)]
        [field: SerializeField] public Vector2 LifetimeRange { get; private set; } = new(0.6f, 1.0f);

        [field: Note("Upward launch speed in world units/sec. Randomised per particle between min and max.")]
        [field: MinMaxRange(0f, 5f)]
        [field: SerializeField] public Vector2 SpeedRange { get; private set; } = new(0.8f, 2.0f);

        [field: Note("Final particle size in world units. Each particle spawns at 20–50% of this and grows to full size.")]
        [field: MinMaxRange(0f, 1f)]
        [field: SerializeField] public Vector2 SizeRange { get; private set; } = new(0.08f, 0.12f);

        [field: Note("Linear drag — 0 = no slowdown, higher = particles decelerate faster.")]
        [field: Range(0f, 5f)]
        [field: SerializeField] public float Drag { get; private set; } = 0.4f;

        [field: Note("Constant acceleration (units/sec²). Negative Y = gentle downward pull.")]
        [field: SerializeField] public Vector3 Gravity { get; private set; } = new(0f, -0.2f, 0f);

        [field: Header("Tether — avatar-following spring")]
        [field: Note("Spring stiffness pulling anchored particles toward their avatar on XZ. " +
                     "Higher = tighter follow. Zero disables tethering (particles behave as before).")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float TetherStrength { get; private set; } = 15f;

        [field: Note("Damping on the tether spring (XZ only). Counteracts overshoot — " +
                     "higher = smoother and more mellow. Critical damping ≈ 2 × √TetherStrength.")]
        [field: Range(0f, 20f)]
        [field: SerializeField] public float TetherDamping { get; private set; } = 8f;

        [field: Note("Multiplier on tether strength over normalised lifetime [0,1]. " +
                     "1 at birth → 0 at death makes young particles follow tightly " +
                     "while old ones drift free. Leave empty for constant strength.")]
        [field: SerializeField] public AnimationCurve TetherOverLifetime { get; private set; }

        [field: Header("Burst")]
        [field: Note("How many particles to spawn per burst trigger (tap or stream tick).")]
        [field: Range(1, 20)]
        [field: SerializeField] public int BurstCount { get; private set; } = 5;

        [field: Header("Streaming (hold-to-emit)")]
        [field: Note("Emission ticks per second while world stream is active. Each tick spawns BurstCount particles.")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float StreamRatePerSecond { get; private set; } = 6f;

        [field: Header("Debug")]
        [field: Note("Emission ticks per second per nearby avatar during debug mode.")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float DebugRatePerSecond { get; private set; } = 3f;

        [field: Header("Zig-Zag — lateral oscillation while floating")]
        [field: Note("Peak lateral acceleration (units/sec²) for the sinusoidal zig-zag. " +
                     "Each particle oscillates in a random horizontal direction. 0 = straight up.")]
        [field: Range(0f, 2f)]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 0.15f;

        [field: Note("Oscillation frequency (Hz). Lower = slow gentle sway, higher = rapid wobble.")]
        [field: Range(0.1f, 5f)]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 0.5f;

        [field: Header("Rendering")]
        [field: Note("Unity rendering layer for world particles.")]
        [field: SerializeField] public int RenderLayer { get; private set; } = 0;

        [field: Note("INIT-ONLY — size multiplier curve over lifetime [0,1]. " +
                     "Enables pop/shrink effects. Leave empty for raw start→end interpolation.")]
        [field: SerializeField] public AnimationCurve SizeOverLifetime { get; private set; }

        [field: Header("Mock Simulation")]
        [field: Note("Minimum seconds between simulated incoming reactions.")]
        [field: Range(0.5f, 10f)]
        [field: SerializeField] public float MockIntervalMin { get; private set; } = 1f;

        [field: Note("Maximum seconds between simulated incoming reactions.")]
        [field: Range(0.5f, 10f)]
        [field: SerializeField] public float MockIntervalMax { get; private set; } = 4f;

        [field: Note("Minimum emoji particles per simulated reaction burst.")]
        [field: Range(1, 10)]
        [field: SerializeField] public int MockMinEmojisPerBurst { get; private set; } = 1;

        [field: Note("Maximum emoji particles per simulated reaction burst.")]
        [field: Range(1, 10)]
        [field: SerializeField] public int MockMaxEmojisPerBurst { get; private set; } = 3;
    }
}
