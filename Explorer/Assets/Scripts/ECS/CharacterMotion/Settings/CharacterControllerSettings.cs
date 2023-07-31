using UnityEngine;

namespace ECS.CharacterMotion.Settings
{
    [CreateAssetMenu(menuName = "Create Character Controller Settings", fileName = "CharacterControllerSettings", order = 0)]
    public class CharacterControllerSettings : ScriptableObject, ICharacterControllerSettings
    {
        [field: SerializeField] [field: Tooltip("Maximum walk speed")]
        public float WalkSpeed { get; private set; } = 1.8f;

        [field: SerializeField] [field: Tooltip("Maximum run speed")]
        public float RunSpeed { get; private set; } = 3.5f;

        [field: SerializeField] [field: Tooltip("Acceleration on the ground")]
        public float GroundAcceleration { get; private set; } = 40;

        [field: SerializeField] [field: Tooltip("Acceleration in the air")]
        public float AirAcceleration { get; private set; } = 10;

        [field: SerializeField] [field: Tooltip("Speed of rotation to the desired forward vector")]
        public float RotationAngularSpeed { get; private set; } = 10;

        [field: SerializeField]
        public float Gravity { get; private set; } = -9.8f;

        [field: SerializeField] [field: Tooltip("Min and Max jump height")]
        public Vector2 JumpHeight { get; private set; } = new (1.5f, 2.0f);

        [field: SerializeField]
        public float AirDrag { get; private set; }
    }
}
