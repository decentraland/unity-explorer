using Unity.Collections;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class AdditiveBreathDataBridge : MonoBehaviour
    {
        private const int BONE_COUNT = 3;

        private NativeArray<Quaternion> cachedRotations;
        private NativeArray<Quaternion> bindPoseRotations;

        public NativeArray<Quaternion> CachedRotations => cachedRotations;
        public NativeArray<Quaternion> BindPoseRotations => bindPoseRotations;
        public bool IsInitialized { get; private set; }

        public void Initialize(Transform upperArm, Transform forearm, Transform hand)
        {
            if (IsInitialized)
                return;

            cachedRotations = new NativeArray<Quaternion>(BONE_COUNT, Allocator.Persistent);
            bindPoseRotations = new NativeArray<Quaternion>(BONE_COUNT, Allocator.Persistent);

            bindPoseRotations[0] = upperArm.localRotation;
            bindPoseRotations[1] = forearm.localRotation;
            bindPoseRotations[2] = hand.localRotation;

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (cachedRotations.IsCreated)
                cachedRotations.Dispose();

            if (bindPoseRotations.IsCreated)
                bindPoseRotations.Dispose();

            IsInitialized = false;
        }
    }
}
