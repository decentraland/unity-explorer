using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms.Systems
{
    /// <summary>
    ///     This system syncs the Camera and Player transforms to specially created entities in each SDK scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    public partial class SyncGlobalTransformSystem : BaseUnityLoopSystem
    {
        // A proxy write is skipped only when the desired world pose already matches the proxy's
        // ACTUAL live world transform. The gate is keyed on the proxy Transform itself, never on a
        // cached snapshot of the camera/player source: the proxies are parented under the per-scene
        // container, which GatherGltfAssetsSystem.Conclude() repositions once from Mordor to the
        // scene's BaseParcel. That parent move changes the proxy's world pose WITHOUT changing the
        // source, so a source-only gate would leave the proxy stranded at the Mordor offset. Reading
        // the live Transform catches both source motion and parent motion, and covers first-write for
        // free (a freshly constructed proxy sits at the container origin, far from the source pose).
        // The residual error when the gate skips is bounded (never accumulates) because every compare
        // is against ground truth, so a tight epsilon only skips true no-ops.
        private const float POS_EPSILON_SQR = 1e-6f; // ~1 mm
        private const float ROT_EPSILON_DEG = 0.01f;

        private readonly Entity cameraEntityProxy;
        private readonly Entity playerEntityProxy;
        private readonly ExposedTransform playerTransform;
        private readonly IExposedCameraData cameraData;

        private SyncGlobalTransformSystem(World world,
            in Entity cameraEntityProxy,
            in Entity playerEntityProxy,
            ExposedTransform playerTransform,
            IExposedCameraData cameraData) : base(world)
        {
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntityProxy = playerEntityProxy;
            this.playerTransform = playerTransform;
            this.cameraData = cameraData;
        }

        protected override void Update(float t)
        {
            SyncProxy(cameraEntityProxy, cameraData.WorldPosition.Value, cameraData.WorldRotation.Value);
            SyncProxy(playerEntityProxy, playerTransform.Position.Value, playerTransform.Rotation.Value);
        }

        private void SyncProxy(in Entity proxy, in Vector3 worldPosition, in Quaternion worldRotation)
        {
            ref TransformComponent transformComponent = ref World.Get<TransformComponent>(proxy);
            Transform transform = transformComponent.Transform;

            // Single native read of the proxy's live world pose (parent hierarchy included).
            transform.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation);

            bool positionDirty = (worldPosition - currentPosition).sqrMagnitude > POS_EPSILON_SQR;
            bool rotationDirty = Quaternion.Angle(worldRotation, currentRotation) > ROT_EPSILON_DEG;

            if (positionDirty || rotationDirty)
                transformComponent.SetWorldTransform(worldPosition, worldRotation, Vector3.one);
        }
    }
}
