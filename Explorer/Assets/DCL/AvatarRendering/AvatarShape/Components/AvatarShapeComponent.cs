using DCL.AvatarRendering.Wearables.Helpers;
using System;
using System.Collections.Generic;
using UnityEngine;
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

        public Matrix4x4[] BoneMatrices;
        public Transform[] Bones;


        public AvatarShapeComponent(string id, WearablesLiterals.BodyShape bodyShape, Promise wearablePromise)
        {
            ID = id;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
            Base = null;
            InstantiatedWearables = new List<GameObject>();
            gpuSkinningComponent = new List<SimpleGPUSkinning>();
            Bones = Array.Empty<Transform>();
            BoneMatrices = Array.Empty<Matrix4x4>();
        }
    }
}
