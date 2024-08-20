using Cinemachine;

namespace DCL.SDKComponents.CameraControl.CameraDirector.Components
{
    public struct CameraDirectorComponent
    {
        internal CinemachineVirtualCamera? virtualCameraInstance;
        internal int virtualCameraCRDTEntity;

        public CameraDirectorComponent(int virtualCamCRDTEntity = 0)
        {
            virtualCameraCRDTEntity = virtualCamCRDTEntity;
            virtualCameraInstance = null;
        }
    }
}
