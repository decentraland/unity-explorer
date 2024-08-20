using Cinemachine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct MainCameraComponent
    {
        internal CinemachineVirtualCamera? virtualCameraInstance;
        internal int virtualCameraCRDTEntity;

        public MainCameraComponent(int virtualCamCRDTEntity = 0)
        {
            virtualCameraCRDTEntity = virtualCamCRDTEntity;
            virtualCameraInstance = null;
        }
    }
}
