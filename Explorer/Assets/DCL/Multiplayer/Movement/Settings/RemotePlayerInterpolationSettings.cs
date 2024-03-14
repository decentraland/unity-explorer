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
                        + "Accelerated transition is a maximum between calculated and initial transition duration divided by this value. Thus, 1 = no acceleration (normal transition)")]
        [field: SerializeField] public float MaxSpeedUpTimeDivider { get; set; } = 1f;

        [field: Tooltip("Minimum of not processed messages in inbox, after which we start catching up process (speed-up interpolation).")]
        [field: SerializeField] public int CatchUpMessagesMin { get; set; } = 3;

        [field: Space]
        [field: Tooltip("Blending is a transition to an interpolation from terminated extrapolation point.")]
        [field: SerializeField] public bool UseBlend { get; set; } = true;
        [field: SerializeField] public InterpolationType BlendType { get; set; }

        [field: Min(5)]
        [field: Tooltip("Maximums speed for the blending.")]
        [field: SerializeField] public float MaxBlendSpeed { get; set; } = 5;
    }
}
