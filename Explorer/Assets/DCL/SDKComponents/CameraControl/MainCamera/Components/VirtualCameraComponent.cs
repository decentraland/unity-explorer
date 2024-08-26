using Cinemachine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct VirtualCameraComponent
    {
        internal readonly CinemachineFreeLook virtualCameraInstance;

        public VirtualCameraComponent(CinemachineFreeLook virtualCameraInstance)
        {
            this.virtualCameraInstance = virtualCameraInstance;
        }
    }
}
