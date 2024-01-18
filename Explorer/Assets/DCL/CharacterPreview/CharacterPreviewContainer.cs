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

        public void Initialize(RenderTexture targetTexture)
        {
            camera.targetTexture = targetTexture;
            cameraTarget.position = defaultPositionTransform.position;
            rotationTarget.rotation = Quaternion.identity;
            this.transform.position = new Vector3(0, 5000);

            //Set correct height for the object, reset position of targets and rotation
            //Magic values should be serialized fields
        }

    }

}
