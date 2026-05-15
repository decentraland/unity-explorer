using System;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public class AvatarTransformMatrixJobWrapper : IDisposable
    {
        // Each task processes one full avatar (62 bone multiplies). Small batch count keeps
        // worker utilisation high without excessive scheduling overhead.
        private const int BONE_MATRIX_BATCH_COUNT = 4;

        internal const int AVATAR_ARRAY_SIZE = 100;
        private const int BONES_ARRAY_LENGTH = ComputeShaderConstants.MAX_BONE_COUNT;
        private const int BONES_PER_AVATAR_LENGTH = AVATAR_ARRAY_SIZE * BONES_ARRAY_LENGTH;

        private bool disposed;

        // Placeholder transform for released or unassigned slots in the TAAs.
        private readonly Transform dummyTransform;

        private readonly MainPlayerPipeline mainPlayerAvatar;
        private readonly RemoteAvatarPipeline remoteAvatars;

        public NativeArray<float4x4> MainPlayerBonesResult => mainPlayerAvatar.Job.BonesMatricesResult;

        public NativeArray<float4x4> RemoteAvatarsBonesResult  => remoteAvatars.Job.BonesMatricesResult;

#if UNITY_INCLUDE_TESTS
        public int MatrixFromAllAvatarsLength => remoteAvatars.MatrixFromAllAvatarsLength;
        public int UpdateAvatarLength => remoteAvatars.UpdateAvatarLength;
        public int CurrentAvatarAmountSupported => remoteAvatars.CurrentAvatarAmountSupported;
#endif

        public AvatarTransformMatrixJobWrapper()
        {
            var dummyGO = new GameObject("AvatarTransformMatrixDummy") { hideFlags = HideFlags.HideAndDontSave };
            dummyTransform = dummyGO.transform;

            remoteAvatars = new RemoteAvatarPipeline(AVATAR_ARRAY_SIZE, BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH, dummyTransform);
            mainPlayerAvatar = new MainPlayerPipeline(BONES_ARRAY_LENGTH);
        }

        /// <summary>
        ///     Schedules bone gather + matrix calculation for all avatars.
        ///     The main player pipeline is completed immediately so its transforms are unlocked
        ///     before InterpolateCharacterSystem runs.
        /// </summary>
        public void ScheduleBoneMatrixCalculation()
        {
            mainPlayerAvatar.ScheduleAndComplete();
            remoteAvatars.Schedule(BONE_MATRIX_BATCH_COUNT);
        }

        public void CompleteBoneMatrixCalculations()
        {
            remoteAvatars.Complete();
        }

        /// <summary>
        ///     Registers the main player avatar into a dedicated pipeline whose transforms
        ///     are gathered and released before the remote batch, preventing TransformAccessArray
        ///     locks from blocking InterpolateCharacterSystem.
        /// </summary>
        /// <summary>
        ///     Registers from a local (pre-Add) component. Sets index and flag on the component
        ///     so the caller can pass it into World.Add already registered.
        /// </summary>
        public void RegisterMainPlayerAvatar(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.ValidUnsafe(0);
            transformMatrixComponent.IsMainPlayer = true;

            mainPlayerAvatar.Register(avatarBase.transform, transformMatrixComponent.bones, dummyTransform);
        }

        /// <summary>
        ///     Registers a remote avatar for bone matrix calculation.
        ///     Subsequent calls for already-registered avatars are no-ops; per-frame work is handled by the gather jobs.
        /// </summary>
        public void RegisterAvatar(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            remoteAvatars.Register(avatarBase, ref transformMatrixComponent);
        }

        public void Dispose()
        {
            remoteAvatars.Complete();

            remoteAvatars.Dispose();
            mainPlayerAvatar.Dispose();

            if (dummyTransform != null)
                UnityEngine.Object.Destroy(dummyTransform.gameObject);

            disposed = true;
        }

        public void ReleaseAvatar(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (disposed) return;

            //Main player avatar never gets released
            if (avatarTransformMatrixComponent.IsMainPlayer)
                return;

            remoteAvatars.Release(ref avatarTransformMatrixComponent);
        }
    }
}
