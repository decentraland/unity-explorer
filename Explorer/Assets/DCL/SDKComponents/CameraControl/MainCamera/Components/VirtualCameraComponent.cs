using Cinemachine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct VirtualCameraComponent
    {
        internal readonly CinemachineVirtualCamera virtualCameraInstance;

        public VirtualCameraComponent(CinemachineVirtualCamera virtualCameraInstance)
        {
            this.virtualCameraInstance = virtualCameraInstance;
        }
    }
}
