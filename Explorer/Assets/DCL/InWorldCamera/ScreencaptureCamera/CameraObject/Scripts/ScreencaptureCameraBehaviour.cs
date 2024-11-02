using DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Scripts;
using UnityEngine;

public class ScreencaptureCameraBehaviour : MonoBehaviour
{
    private bool isInstantiated;

    [SerializeField] private Camera cameraPrefab;
    [SerializeField] private LayerMask layer;

    public GameObject character;
    private static Transform characterCameraTransform => Camera.main.transform;

    [ContextMenu("Create Camera Objects")]
    private void CreateCameraObjects()
    {
        Camera screenshotCamera = Instantiate(cameraPrefab, characterCameraTransform.position, characterCameraTransform.rotation, transform);
        screenshotCamera.gameObject.layer = character.layer;

        ScreencaptureCameraMovement cameraMovement = screenshotCamera.GetComponent<ScreencaptureCameraMovement>();
        // cameraMovement.Initialize(cameraTarget, virtualCamera, characterCameraTransform);
        // cameraMovement.enabled = true;
    }
}
