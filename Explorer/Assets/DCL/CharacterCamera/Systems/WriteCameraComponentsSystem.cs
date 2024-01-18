using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Runs in the Scene World and propagates camera data to the JavaScript scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class WriteCameraComponentsSystem : BaseUnityLoopSystem
    {
        private static readonly PBCameraMode SHARED_CAMERA_MODE = new ();
        private static readonly PBPointerLock SHARED_POINTER_LOCK = new ();

        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IExposedCameraData exposedCameraData;
        private readonly ISceneData sceneData;

        internal WriteCameraComponentsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, IExposedCameraData exposedCameraData, ISceneData sceneData) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedCameraData = exposedCameraData;
            this.sceneData = sceneData;
        }

        public override void Initialize()
        {
            // set camera position for a newly created scene
            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedCameraData, SpecialEntitiesID.CAMERA_ENTITY, sceneData.Geometry.BaseParcelPosition, false);
            Update(0);
        }

        protected override void Update(float t)
        {
            //SHARED_CAMERA_MODE.Mode = exposedCameraData.CameraType;
            //SHARED_POINTER_LOCK.IsPointerLocked = exposedCameraData.PointerIsLocked;

            //SHARED_TRANSFORM.Position = ParcelMathHelper.GetSceneRelativePosition(exposedCameraData.WorldPosition, scenePosition);
            //SHARED_TRANSFORM.Rotation = exposedCameraData.WorldRotation;

            // TODO Access to CRDT LWW components is not synchronized, bi-directional access causes concurrency exceptions
            // TODO Poor performance, should jobified
            // TODO Uncommenting causes random APPEND/PUT [silent] failures
            // ecsToCrdtWriter.PutMessage(SpecialEntitiesID.CAMERA_ENTITY, SHARED_TRANSFORM);
            //ecsToCrdtWriter.PutMessage(SpecialEntitiesID.CAMERA_ENTITY, SHARED_CAMERA_MODE);
            //ecsToCrdtWriter.PutMessage(SpecialEntitiesID.CAMERA_ENTITY, SHARED_POINTER_LOCK);

            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedCameraData, SpecialEntitiesID.CAMERA_ENTITY, sceneData.Geometry.BaseParcelPosition, true);
        }
    }
}
