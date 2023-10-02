using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.Wearables;
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
        public string Name;
        public Color SkinColor;
        public Color HairColor;

        public BodyShape BodyShape;
        public bool IsDirty;

        public Promise WearablePromise;

        public AvatarBase Base;
        public List<GameObject> InstantiatedWearables;
        public CustomSkinning skinningMethod;

        private TransformAccessArray Bones;
        private BoneMatrixCalculationJob job;
        private JobHandle handle;

        public AvatarShapeComponent(string name, string id, BodyShape bodyShape, Promise wearablePromise, Color skinColor,
            Color hairColor)
        {
            ID = id;
            Name = name;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            Base = null;
            InstantiatedWearables = new List<GameObject>();
            Bones = default(TransformAccessArray);
            job = default(BoneMatrixCalculationJob);
            handle = default(JobHandle);
            SkinColor = skinColor;
            HairColor = hairColor;

            //TODO: Debug feature, remove when done
            if (ID == "0")
                skinningMethod = new ComputeShaderSkinning();
            else
                skinningMethod = new UnityCustomSkinning();
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

        public void Clear()
        {
            skinningMethod.Dispose();
            //TODO: Clear BurstJob and everything else that could be dirty
        }

    }


    }

