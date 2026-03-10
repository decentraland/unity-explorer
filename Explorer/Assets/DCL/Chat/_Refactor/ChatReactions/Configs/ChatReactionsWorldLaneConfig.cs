using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Tuning for the world-space particle lane — the emoji bursts that float upward
    /// above an avatar's head when a situational reaction is triggered in world space.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsWorldLaneConfig",
                     menuName = "DCL/Chat/Reactions/World Lane Config")]
    public class ChatReactionsWorldLaneConfig : ScriptableObject
    {
        [field: Header("Pool")]
        [field: Min(64)]
        [field: Tooltip("Ring-buffer capacity — the maximum number of simultaneously live particles. " +
                        "Keep at or below 1023 to stay within a single DrawMeshInstanced call.")]
        [field: SerializeField] public int MaxParticles { get; private set; } = 1023;

        [field: Header("Physics")]
        [field: Tooltip("Particle lifetime in seconds (min, max).")]
        [field: SerializeField] public Vector2 LifetimeRange { get; private set; } = new(0.6f, 1.0f);

        [field: Tooltip("Initial upward speed in world units/sec (min, max).")]
        [field: SerializeField] public Vector2 SpeedRange { get; private set; } = new(0.8f, 2.0f);

        [field: Tooltip("World-space particle size at death (min, max). Spawn size is 20–50% of this.")]
        [field: SerializeField] public Vector2 SizeRange { get; private set; } = new(0.08f, 0.12f);

        [field: Min(0f)]
        [field: Tooltip("Linear drag coefficient. Higher values slow particles faster.")]
        [field: SerializeField] public float Drag { get; private set; } = 0.4f;

        [field: Tooltip("Gravity vector in world space (world units/sec²).")]
        [field: SerializeField] public Vector3 Gravity { get; private set; } = new(0f, -0.2f, 0f);

        [field: Header("Burst")]
        [field: Min(1)]
        [field: Tooltip("How many particles to spawn per burst trigger (tap or stream tick).")]
        [field: SerializeField] public int BurstCount { get; private set; } = 5;

        [field: Header("Streaming")]
        [field: Min(0f)]
        [field: Tooltip("Particles emitted per second per avatar while world stream is active.")]
        [field: SerializeField] public float StreamRatePerSecond { get; private set; } = 6f;

        [field: Header("Debug")]
        [field: Min(0f)]
        [field: Tooltip("Particles emitted per second per nearby avatar during debug mode.")]
        [field: SerializeField] public float DebugRatePerSecond { get; private set; } = 3f;

        [field: Header("Zig-Zag — lateral oscillation while floating")]

        [field: Tooltip("Peak lateral acceleration in world units/sec² for the sinusoidal zig-zag. " +
                        "Each particle oscillates in a random horizontal direction.")]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 0.15f;

        [field: Min(0.1f)]
        [field: Tooltip("Oscillation frequency in Hz. Lower = slow gentle sway, higher = rapid wobble.")]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 0.5f;

        [field: Header("Rendering")]
        [field: Tooltip("Unity rendering layer for world particles.")]
        [field: SerializeField] public int RenderLayer { get; private set; } = 0;

        [field: Tooltip("Optional size multiplier over normalised lifetime [0,1]. " +
                        "Enables pop/shrink effects on world particles. Leave null to use raw start→end interpolation.")]
        [field: SerializeField] public AnimationCurve SizeOverLifetime { get; private set; }

        [field: Header("Mock Simulation")]
        [field: Tooltip("Enable the mock reaction simulation. Nearby avatars will appear to send random reactions.")]
        [field: SerializeField] public bool MockEnabled { get; private set; } = false;

        [field: Min(0.5f)]
        [field: Tooltip("Minimum seconds between simulated incoming reactions.")]
        [field: SerializeField] public float MockIntervalMin { get; private set; } = 1f;

        [field: Min(0.5f)]
        [field: Tooltip("Maximum seconds between simulated incoming reactions.")]
        [field: SerializeField] public float MockIntervalMax { get; private set; } = 4f;

        [field: Min(1)]
        [field: Tooltip("Minimum number of emoji particles per simulated reaction burst.")]
        [field: SerializeField] public int MockMinEmojisPerBurst { get; private set; } = 1;

        [field: Min(1)]
        [field: Tooltip("Maximum number of emoji particles per simulated reaction burst.")]
        [field: SerializeField] public int MockMaxEmojisPerBurst { get; private set; } = 3;
    }
}
