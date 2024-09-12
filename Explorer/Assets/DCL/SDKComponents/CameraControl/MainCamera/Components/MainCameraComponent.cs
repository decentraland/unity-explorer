using Cinemachine;
using CRDT;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct MainCameraComponent
    {
        internal CinemachineFreeLook? virtualCameraInstance;
        internal CRDTEntity? virtualCameraCRDTEntity;
    }
}
