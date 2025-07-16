using UnityEngine;

public class BadgePreviewCameraView : MonoBehaviour
{
    [field: SerializeField]
    public Animator badge3DAnimator { get; private set; }

    [field: SerializeField]
    public Renderer badge3DRenderer { get; private set; }

    [field: SerializeField]
    public Camera badge3DCamera { get; private set; }
}
