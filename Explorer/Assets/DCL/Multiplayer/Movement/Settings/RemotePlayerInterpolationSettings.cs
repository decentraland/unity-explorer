using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class RemotePlayerInterpolationSettings : ScriptableObject
    {
        [field: SerializeField] public InterpolationType InterpolationType { get; set; }
        [field: SerializeField] public float LookAtTimeDelta { get; set; }

        [field: Space]
        [field: SerializeField] public bool UseSpeedUp { get; set; }
        [field: SerializeField] public float MaxSpeedUpTimeDivider { get; set; } = 0;

        [field: Space]
        [field: SerializeField] public bool UseBlend { get; set; } = true;
        [field: SerializeField] public InterpolationType BlendType { get; set; }
        [field: SerializeField] public float MaxBlendSpeed { get; set; } = 5;
    }
}
