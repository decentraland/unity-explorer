using Cinemachine;
using CRDT;

namespace DCL.SDKComponents.CameraControl.MainCamera.Components
{
    public struct VirtualCameraComponent
    {
        internal readonly CinemachineFreeLook virtualCameraInstance;
        internal CRDTEntity? lookAtCRDTEntity;

        public VirtualCameraComponent(CinemachineFreeLook virtualCameraInstance, CRDTEntity? lookAtCRDTEntity)
        {
            this.virtualCameraInstance = virtualCameraInstance;
            this.lookAtCRDTEntity = lookAtCRDTEntity;
        }
    }
}
