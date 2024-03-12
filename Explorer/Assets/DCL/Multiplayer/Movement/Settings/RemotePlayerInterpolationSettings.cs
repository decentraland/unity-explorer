using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class RemotePlayerInterpolationSettings : ScriptableObject
    {
        [field: SerializeField] public InterpolationType InterpolationType { get; set; }

        [field: Min(0)]
        [field: Tooltip("Player looks forward to its movement. This value defines how far in future he will look. "
                        + "It defines time delta in seconds for the next step (to look at).")]
        [field: SerializeField] public float LookAtTimeDelta { get; set; } = 0.003f;

        [field: Space]
        [field: Tooltip("Speed Up increases speed of interpolation if player has a lot of inbox messages to process. "
                        + "Thus, he catching to latest state earlier and reduces latency lag.")]
        [field: SerializeField] public bool UseSpeedUp { get; set; } = true;

        [field: Min(1)]
        [field: Tooltip("When using speed up, this value controls its maximum speed. Higher values results in the faster transition speed. "
                        + "Maximum speeded transition duration is clamped by initial transition duration divided by this value.")]
       public float MaxSpeedUpTimeDivider { get; } = 1f; // Temporarily disabled (set to 1) as not stable solution

        [field: Space]
        [field: Tooltip("Blending is a transition to an interpolation from terminated extrapolation point.")]
        [field: SerializeField] public bool UseBlend { get; set; } = true;
        [field: SerializeField] public InterpolationType BlendType { get; set; }

        [field: Min(5)]
        [field: Tooltip("Maximums speed for the blending.")]
        [field: SerializeField] public float MaxBlendSpeed { get; set; } = 5;
    }
}
