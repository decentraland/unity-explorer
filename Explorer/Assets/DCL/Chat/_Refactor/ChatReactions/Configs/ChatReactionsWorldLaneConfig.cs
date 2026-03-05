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

        [field: Header("Rendering")]
        [field: Tooltip("Unity rendering layer for world particles.")]
        [field: SerializeField] public int RenderLayer { get; private set; } = 0;
    }
}
