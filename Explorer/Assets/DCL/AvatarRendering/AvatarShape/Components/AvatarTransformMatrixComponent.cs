﻿using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.Diagnostics;
using DCL.Utility.Types;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent
    {
        public int IndexInGlobalJobArray;
        public BoneArray bones;

        public static AvatarTransformMatrixComponent Create(BoneArray bones) =>
            new ()
            {
                IndexInGlobalJobArray = -1,
                bones = bones,
            };

#if UNITY_INCLUDE_TESTS
        public static AvatarTransformMatrixComponent NewDefault() =>
            Create(BoneArray.NewDefault());
#endif
    }

    /// <summary>
    /// Guarantees amount of bones in the array
    /// </summary>
    public readonly struct BoneArray
    {
        public const int COUNT = ComputeShaderConstants.BONE_COUNT;

        public readonly Transform[] Inner;

        public Transform this[int i] => Inner[i];

        private BoneArray(Transform[] inner)
        {
            this.Inner = inner;
        }

        public static Result<BoneArray> From(Transform[] bones) =>
            bones.Length != COUNT
                ? Result<BoneArray>.ErrorResult($"Cannot map bone array, mismatch count: real {bones.Length}, expected: {COUNT}")
                : Result<BoneArray>.SuccessResult(new BoneArray(bones));

        public static BoneArray FromOrDefault(Transform[] bones, ReportData reportData)
        {
            var boneArrayResult = From(bones);

            if (boneArrayResult.Success == false)
            {
                ReportHub.LogError(reportData, $"Cannot instantiate avatar, fallback to default bone array: {boneArrayResult.ErrorMessage}");
                return NewDefault();
            }

            return boneArrayResult.Value;
        }

        public static BoneArray NewDefault()
        {
            var inner = new Transform[COUNT];
            for (int i = 0; i < COUNT; i++) inner[i] = new GameObject("BoneDefault").transform;
            return new BoneArray(inner);
        }
    }
}
