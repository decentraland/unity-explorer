using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public string ID;
        public WearablesLiterals.BodyShape BodyShape;
        public bool IsDirty;

        public Promise WearablePromise;

        public AvatarBase Base;
        public List<GameObject> InstantiatedWearables;
        public SimpleComputeShaderSkinning CombinedMeshGpuSkinningComponent;


        public TransformAccessArray Bones;
        public BoneMatrixCalculationJob job;
        public JobHandle handle;

        public AvatarShapeComponent(string id, WearablesLiterals.BodyShape bodyShape, Promise wearablePromise)
        {
            ID = id;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            Base = null;
            InstantiatedWearables = new List<GameObject>();
            Bones = default(TransformAccessArray);
            job = default(BoneMatrixCalculationJob);
            handle = default(JobHandle);
            CombinedMeshGpuSkinningComponent = null;
        }

        public void SetupBurstJob(Transform avatarBaseTransform, Transform[] bones)
        {
            Bones = new TransformAccessArray(bones);

            job = new BoneMatrixCalculationJob
            {
                BonesMatricesResult = new NativeArray<float4x4>(bones.Length, Allocator.Persistent),
                AvatarTransform = avatarBaseTransform.worldToLocalMatrix,
            };
        }

        public void ScheduleBoneMatrixCalculation()
        {
            job.AvatarTransform = Base.transform.worldToLocalMatrix;
            handle = job.Schedule(Bones);
        }

        public NativeArray<float4x4> CompleteBoneMatrixCalculations()
        {
            handle.Complete();
            return job.BonesMatricesResult;
        }

    }


    }

