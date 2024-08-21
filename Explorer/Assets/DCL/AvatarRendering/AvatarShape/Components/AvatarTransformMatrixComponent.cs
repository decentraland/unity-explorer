using DCL.AvatarRendering.AvatarShape.ComputeShader;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent : IDisposable
    {
        public int IndexInGlobalJobArray;
        public Transform[] bones;

        public void Dispose()
        {
        }

        public static AvatarTransformMatrixComponent Create(Transform avatarBaseTransform, Transform[] bones) =>
            new ()
            {
                bones = bones
            };
    }
}
