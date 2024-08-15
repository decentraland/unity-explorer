using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using System;

namespace DCL.SDKComponents.CameraControl.CameraDirector.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.CAMERA_DIRECTOR)]
    public partial class CameraDirectorSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        public CameraDirectorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            // UpdateCameraDirectorQuery(World);
            // SetupVirtualCameraQuery(World);
            //
            // HandleEntityDestructionQuery(World);
            // HandleComponentRemovalQuery(World);
        }



        public void FinalizeComponents(in Query query)
        {
            // throw new NotImplementedException();
        }
    }
}
