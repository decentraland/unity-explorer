using DCL.Diagnostics;
using UnityEngine;

namespace DCL.Character
{
    public interface ICharacterObject
    {
        CharacterController Controller { get; }

        Transform CameraFocus { get; }

        Transform Transform { get; }

        Vector3 Position { get; }

        class Fake : ICharacterObject
        {
            public Fake(Vector3 position) : this(null!, null!, null!, position)
            {
                ReportHub.LogWarning(
                    ReportCategory.UNSPECIFIED,
                    "Using Fake ICharacterObject, use only the position property for this case"
                );
            }

            public Fake(CharacterController controller, Transform cameraFocus, Transform transform, Vector3 position)
            {
                Controller = controller;
                CameraFocus = cameraFocus;
                Transform = transform;
                Position = position;
            }

            public CharacterController Controller { get; }
            public Transform CameraFocus { get; }
            public Transform Transform { get; }
            public Vector3 Position { get; }
        }
    }
}
