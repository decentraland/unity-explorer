using DCL.AvatarRendering.AvatarShape.ComputeShader;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent 
    {
        public int IndexInGlobalJobArray;
        public Transform[] bones;

        public static AvatarTransformMatrixComponent Create(Transform[] bones)
        {
            return new AvatarTransformMatrixComponent
            {
                IndexInGlobalJobArray = -1,
                bones = bones
            };
        }
    }
}
