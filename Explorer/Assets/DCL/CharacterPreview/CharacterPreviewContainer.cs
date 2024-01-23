using System;
using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Contains serialized data only needed for the character preview
    ///     See CharacterPreviewController in the old renderer
    /// </summary>
    public class CharacterPreviewContainer : MonoBehaviour
    {
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal new Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }

        [field: SerializeField] internal Transform defaultPositionTransform { get; private set; }
        [field: SerializeField] internal Transform topPositionTransform { get; private set; }
        [field: SerializeField] internal Transform bottomPositionTransform { get; private set; }
        [field: SerializeField] internal Transform shoesPositionTransform { get; private set; }
        [field: SerializeField] internal Transform headPositionTransform { get; private set; }

        [field: SerializeField] internal LayerMask layer { get; private set; }

        [field: SerializeField] internal float dragMovementModifier { get; private set; }
        [field: SerializeField] internal float scrollModifier { get; private set; }
        [field: SerializeField] internal float rotationModifier { get; private set; }


        [field: SerializeField] internal float maxHorizontalOffset { get; private set; }

        [field: SerializeField] internal float minVerticalOffset { get; private set; }
        [field: SerializeField] internal float maxVerticalOffset { get; private set; }

        [field: SerializeField] internal Vector2 depthLimits { get; private set; }

        public void Initialize(RenderTexture targetTexture)
        {
            this.transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            cameraTarget.position = defaultPositionTransform.position;
            rotationTarget.rotation = Quaternion.identity;
        }
    }

}
