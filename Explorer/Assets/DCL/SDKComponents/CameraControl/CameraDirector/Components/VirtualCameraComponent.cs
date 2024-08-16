using Cinemachine;

namespace DCL.SDKComponents.CameraControl.CameraDirector.Components
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
