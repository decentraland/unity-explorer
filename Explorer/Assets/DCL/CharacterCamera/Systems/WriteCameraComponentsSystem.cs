using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
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
        private static readonly SDKTransform SHARED_TRANSFORM = new ();
        private static readonly PBCameraMode SHARED_CAMERA_MODE = new ();
        private static readonly PBPointerLock SHARED_POINTER_LOCK = new ();

        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IExposedCameraData exposedCameraData;
        private readonly Entity sceneRootEntity;

        internal WriteCameraComponentsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, IExposedCameraData exposedCameraData, Entity sceneRootEntity) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedCameraData = exposedCameraData;
            this.sceneRootEntity = sceneRootEntity;
        }

        public override void Initialize()
        {
            // set camera position for a newly created scene
            Update(0);
        }

        protected override void Update(float t)
        {
            Vector3 scenePosition = World.Get<TransformComponent>(sceneRootEntity).Cached.WorldPosition;

            SHARED_CAMERA_MODE.Mode = exposedCameraData.CameraType;
            SHARED_POINTER_LOCK.IsPointerLocked = exposedCameraData.PointerIsLocked;
            SHARED_TRANSFORM.Position = ParcelMathHelper.GetSceneRelativePosition(exposedCameraData.WorldPosition, scenePosition);
            SHARED_TRANSFORM.Rotation = exposedCameraData.WorldRotation;

            // TODO Access to CRDT LWW components is not synchronized, bi-directional access causes concurrency exceptions
            // ecsToCrdtWriter.PutMessage(SpecialEntititiesID.CAMERA_ENTITY, SHARED_TRANSFORM);
            ecsToCrdtWriter.PutMessage(SpecialEntitiesID.CAMERA_ENTITY, SHARED_CAMERA_MODE);
            ecsToCrdtWriter.PutMessage(SpecialEntitiesID.CAMERA_ENTITY, SHARED_POINTER_LOCK);
        }
    }
}
