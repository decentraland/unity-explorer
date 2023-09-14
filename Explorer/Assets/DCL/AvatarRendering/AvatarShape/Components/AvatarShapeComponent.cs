using DCL.AvatarRendering.Wearables.Helpers;
using Stella3D;
using System.Collections.Generic;
using Unity.Burst;
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
        public List<SimpleGPUSkinning> gpuSkinningComponent;

        public TransformAccessArray Bones;
        public SharedArray<Matrix4x4, float4x4> boneSharedArray;
        public BoneMatrixCalculationJob BoneBoneMatrixCalculation;
        public Matrix4x4 matrixCache;


        public AvatarShapeComponent(string id, WearablesLiterals.BodyShape bodyShape, Promise wearablePromise)
        {
            ID = id;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            Base = null;
            InstantiatedWearables = new List<GameObject>();
            gpuSkinningComponent = new List<SimpleGPUSkinning>();
            Bones = new TransformAccessArray(0);
            boneSharedArray = null;
            BoneBoneMatrixCalculation = new BoneMatrixCalculationJob();
            matrixCache = new Matrix4x4();
        }

        public void SetMatrixValues()
        {
            Bones = new TransformAccessArray(Base.AvatarSkinnedMeshRenderer.bones);
            boneSharedArray = new SharedArray<Matrix4x4, float4x4>(Base.AvatarSkinnedMeshRenderer.bones.Length);
            BoneBoneMatrixCalculation.BonesMatricesResult = boneSharedArray;
            matrixCache = Base.transform.worldToLocalMatrix;
        }

        public void DoBoneMatrixCalculation()
        {
            JobHandle handle = BoneBoneMatrixCalculation.Schedule(Bones);
            handle.Complete();
        }
    }

    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelForTransform
    {
        public NativeArray<float4x4> BonesMatricesResult;

        public void Execute(int index, TransformAccess transform)
        {
            BonesMatricesResult[index] = transform.localToWorldMatrix;
        }
    }

    }

