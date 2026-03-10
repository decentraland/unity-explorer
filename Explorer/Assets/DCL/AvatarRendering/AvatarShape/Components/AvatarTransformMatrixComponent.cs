using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.Diagnostics;
using DCL.Utility.Types;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent
    {
        public GlobalJobArrayIndex IndexInGlobalJobArray;
        public BoneArray bones;

        public static AvatarTransformMatrixComponent Create(BoneArray bones) =>
            new ()
            {
                IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized(),
                bones = bones,
            };

#if UNITY_INCLUDE_TESTS
        public static AvatarTransformMatrixComponent NewDefault() =>
            Create(BoneArray.NewDefault());
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GlobalJobArrayIndex
    {
        private const int UNINITIALIZED = -100;
        private const int NOT_ASSIGNED = -1;

        private readonly int index;

        private GlobalJobArrayIndex(int index)
        {
            this.index = index;
        }

        public static GlobalJobArrayIndex Uninitialized()
        {
            return new (UNINITIALIZED);
        }

        public static GlobalJobArrayIndex Unassign()
        {
            return new (NOT_ASSIGNED);
        }

        /// <summary>
        /// Caller guarantees the int value is equal or greater than 0.
        /// Otherwise use Uninitialized or Unassign consts
        ///
        ///    if (value < 0)
        ///        throw new Exception($"Value is invalid: {value}");
        /// </summary>
        public static GlobalJobArrayIndex ValidUnsafe(int value)
        {
            return new GlobalJobArrayIndex(value);
        }

        public bool IsValid()
        {
            return index >= 0;
        }

        public bool TryGetValue(out int value)
        {
            value = index;
            return index >= 0;
        }
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
