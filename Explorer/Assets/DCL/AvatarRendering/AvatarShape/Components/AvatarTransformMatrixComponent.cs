using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.Diagnostics;
using DCL.Utility.Types;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent
    {
        public GlobalJobArrayIndex IndexInGlobalJobArray;
        public BoneArray bones;
        public bool IsMainPlayer;

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
    ///     Bone transform array with separate count and capacity. The internal array never shrinks —
    ///     <see cref="Append"/> reuses existing capacity when possible, avoiding heap allocation.
    /// </summary>
    public struct BoneArray
    {
        public Transform[] Inner;
        public int Count;

        public Transform this[int i] => Inner[i];

        private BoneArray(Transform[] inner, int count)
        {
            Inner = inner;
            Count = count;
        }

        public static Result<BoneArray> From(Transform[] bones)
        {
            if (bones.Length < ComputeShaderConstants.BASE_BONE_COUNT || bones.Length > ComputeShaderConstants.MAX_BONE_COUNT)
                return Result<BoneArray>.ErrorResult($"Cannot map bone array, count {bones.Length} outside valid range [{ComputeShaderConstants.BASE_BONE_COUNT}, {ComputeShaderConstants.MAX_BONE_COUNT}]");

            return Result<BoneArray>.SuccessResult(new BoneArray(bones, bones.Length));
        }

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
            var inner = new Transform[ComputeShaderConstants.BASE_BONE_COUNT];
            for (int i = 0; i < ComputeShaderConstants.BASE_BONE_COUNT; i++) inner[i] = new GameObject("BoneDefault").transform;
            return new BoneArray(inner, ComputeShaderConstants.BASE_BONE_COUNT);
        }

        public void Append(List<Transform> bones)
        {
            if (bones.Count == 0) return;

            int newCount = Count + bones.Count;

            if (newCount > ComputeShaderConstants.MAX_BONE_COUNT)
            {
                ReportHub.LogWarning(ReportCategory.AVATAR, $"Spring bone count would exceed MAX_BONE_COUNT ({ComputeShaderConstants.MAX_BONE_COUNT}), capping from {newCount} to {ComputeShaderConstants.MAX_BONE_COUNT}");
                newCount = ComputeShaderConstants.MAX_BONE_COUNT;
            }

            if (Inner.Length < newCount)
                Array.Resize(ref Inner, newCount);

            int extraCount = newCount - Count;

            for (int i = 0; i < extraCount; i++)
                Inner[Count + i] = bones[i];

            Count = newCount;
        }
    }
}
