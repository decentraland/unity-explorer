using UnityEngine;
using UnityEngine.Serialization;
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
        [field: Header("POOL")]
        [field: Note("INIT-ONLY — read once to allocate the particle array. " +
                     "Keep at or below 1023 to stay within a single RenderMeshInstanced call.")]
        [field: Range(64, 1023)]
        [field: SerializeField] public int MaxParticles { get; internal set; } = 1023;

        [field: Header("SPAWN")]
        [field: Note("World-space offset added to the avatar head position before spawning reactions. " +
                     "Y lifts bursts above the nametag point; XZ rarely useful. " +
                     "Keeps spawn origin and anchor-follow target aligned.")]
        [field: SerializeField] public Vector3 AnchorOffset { get; private set; } = new(0f, 0.15f, 0f);

        [field: Header("PHYSICS (WORLD SPACE — UNITS, UNITS/SEC, UNITS/SEC²)")]
        [field: Note("How long each particle lives (seconds). Randomised per particle between min and max.")]
        [field: MinMaxRange(0f, 5f)]
        [field: SerializeField] public Vector2 LifetimeRange { get; internal set; } = new(0.6f, 1.0f);

        [field: Note("Upward launch speed in world units/sec. Randomised per particle between min and max.")]
        [field: MinMaxRange(0f, 5f)]
        [field: SerializeField] public Vector2 SpeedRange { get; internal set; } = new(0.8f, 2.0f);

        [field: Note("Final particle size in world units. Each particle spawns at 20–50% of this and grows to full size.")]
        [field: MinMaxRange(0f, 1f)]
        [field: SerializeField] public Vector2 SizeRange { get; internal set; } = new(0.08f, 0.12f);

        [field: Note("Linear drag — 0 = no slowdown, higher = particles decelerate faster.")]
        [field: Range(0f, 5f)]
        [field: SerializeField] public float Drag { get; private set; } = 0.4f;

        [field: Note("Constant acceleration (units/sec²). Negative Y = gentle downward pull.")]
        [field: SerializeField] public Vector3 Gravity { get; private set; } = new(0f, -0.2f, 0f);

        [field: Header("ANCHOR FOLLOW — AVATAR-FOLLOWING FORCE")]
        [field: Note("Exponential follow rate for anchored particles on XZ. " +
                     "Higher = tighter follow. Zero disables following (particles float freely). " +
                     "Half-life ≈ ln(2) / FollowRate seconds.")]
        [field: Range(0f, 20f)]
        [field: FormerlySerializedAs("<TetherStrength>k__BackingField")]
        [field: FormerlySerializedAs("<SpringStrength>k__BackingField")]
        [field: SerializeField] public float FollowRate { get; internal set; } = 5f;

        [field: Note("Multiplier on follow rate over normalised lifetime [0,1]. " +
                     "1 at birth → 0 at death makes young particles follow tightly " +
                     "while old ones drift free. Leave empty for constant rate.")]
        [field: FormerlySerializedAs("<TetherOverLifetime>k__BackingField")]
        [field: FormerlySerializedAs("<SpringOverLifetime>k__BackingField")]
        [field: SerializeField] public AnimationCurve FollowOverLifetime { get; private set; }

        [field: Header("BURST")]
        [field: Note("How many particles to spawn per burst trigger (tap or stream tick).")]
        [field: Range(1, 20)]
        [field: SerializeField] public int BurstCount { get; internal set; } = 5;

        [field: Note("Maximum concurrent alive particles per avatar anchor. " +
                     "New bursts are dropped when an avatar already has this many active particles. " +
                     "0 = disabled (unlimited).")]
        [field: Range(0, 100)]
        [field: SerializeField] public int MaxParticlesPerAvatar { get; internal set; } = 15;

        [field: Header("STREAMING (HOLD-TO-EMIT)")]
        [field: Note("Emission ticks per second while world stream is active. Each tick spawns BurstCount particles.")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float StreamRatePerSecond { get; private set; } = 6f;

        [field: Header("DEBUG")]
        [field: Note("Emission ticks per second per nearby avatar during debug mode.")]
        [field: Range(0f, 30f)]
        [field: SerializeField] public float DebugRatePerSecond { get; private set; } = 3f;

        [field: Header("ZIG-ZAG — LATERAL OSCILLATION WHILE FLOATING")]
        [field: Note("Peak lateral displacement (world units) for the sinusoidal zig-zag. " +
                     "Each particle oscillates in a random horizontal direction. 0 = straight up. " +
                     "Applied as a visual position offset — independent of drag and spring.")]
        [field: Range(0f, 0.5f)]
        [field: SerializeField] public float ZigZagAmplitude { get; private set; } = 0.06f;

        [field: Note("Oscillation frequency (Hz). Lower = slow gentle sway, higher = rapid wobble.")]
        [field: Range(0.1f, 5f)]
        [field: SerializeField] public float ZigZagFrequency { get; private set; } = 0.5f;

        [field: Header("RENDERING")]
        [field: Note("Unity rendering layer for world particles.")]
        [field: SerializeField] public int RenderLayer { get; private set; } = 0;

        [field: Note("INIT-ONLY — size multiplier curve over lifetime [0,1]. " +
                     "Enables pop/shrink effects. Leave empty for raw start→end interpolation.")]
        [field: SerializeField] public AnimationCurve SizeOverLifetime { get; private set; }

        [field: Header("VISIBILITY CULLING")]
        [field: Note("Max distance (world units) at which world-space reactions are rendered. " +
                     "Avatars beyond this or outside the camera view are skipped during rendering. " +
                     "Matches nametag range by default.")]
        [field: Range(10f, 100f)]
        [field: SerializeField] public float MaxSpawnDistance { get; private set; } = 40;

    }
}
