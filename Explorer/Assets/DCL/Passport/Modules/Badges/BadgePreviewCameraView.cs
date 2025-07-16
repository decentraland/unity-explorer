using UnityEngine;

public class BadgePreviewCameraView : MonoBehaviour
{
    [field: SerializeField]
    public Animator badge3DAnimator { get; private set; }

    [field: SerializeField]
    public Renderer badge3DRenderer { get; private set; }
}
