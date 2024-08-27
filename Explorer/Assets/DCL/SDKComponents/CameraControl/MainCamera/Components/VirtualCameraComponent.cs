using Cinemachine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct VirtualCameraComponent
    {
        internal readonly CinemachineFreeLook virtualCameraInstance;
        internal int lookAtCRDTEntity;

        public VirtualCameraComponent(CinemachineFreeLook virtualCameraInstance, int lookAtCRDTEntity)
        {
            this.virtualCameraInstance = virtualCameraInstance;
            this.lookAtCRDTEntity = lookAtCRDTEntity;
        }
    }
}
