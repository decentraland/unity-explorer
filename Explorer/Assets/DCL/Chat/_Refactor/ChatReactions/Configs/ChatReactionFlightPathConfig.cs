using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Tweakable config for how emoji particles travel after being spawned.
    /// All velocity values are in camera-local 2D space (X = camera right, Y = camera up).
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionFlightPathConfig",
                     menuName = "DCL/Chat/Reactions/Flight Path Config")]
    public class ChatReactionFlightPathConfig : ScriptableObject
    {
        [field: Header("Exit Kick — applied once at spawn")]

        [field: Tooltip("Lateral exit speed range (camera-right direction) in world units/sec. " +
                        "Kick pushes particles out of the chat panel toward the open world.")]
        [field: SerializeField] public Vector2 ExitKickRange { get; private set; } = new(0.6f, 1.4f);

        [field: Min(0f)]
        [field: Tooltip("Randomises the exit angle ± this many degrees around pure camera-right. " +
                        "Zero = all particles exit straight right.")]
        [field: SerializeField] public float ExitAngleVarianceDeg { get; private set; } = 20f;

        [field: Tooltip("Adds a random upward component to the spawn velocity (world units/sec, ±range). " +
                        "Positive values spread the stream vertically.")]
        [field: SerializeField] public float FloatDriftRange { get; private set; } = 0.15f;

        [field: Header("Float — sustained per-frame upward force")]

        [field: Tooltip("Upward acceleration in world units/sec² applied every frame. " +
                        "Counteracts drag to keep particles rising after the exit kick decays. " +
                        "Set to 0 to let drag alone determine how long they travel.")]
        [field: SerializeField] public float FloatUpAcceleration { get; private set; } = 0.5f;

        [field: Header("Pop — size animation over lifetime")]

        [field: Tooltip("Multiplier applied to the particle's interpolated size at each normalised lifetime [0,1]. " +
                        "Default creates a small spawn, full size through mid-life, a brief enlargement, " +
                        "then a snap to zero (the 'pop'). Set all keys to 1 to disable.")]
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
