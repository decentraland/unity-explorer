using DCL.AvatarRendering.AvatarShape.ComputeShader;
using UnityEngine;
using Utility.Types;

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

        public static AvatarTransformMatrixComponent NewDefault() =>
            Create(BoneArray.NewDefault());
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

        public static BoneArray NewDefault()
        {
            var inner = new Transform[COUNT];
            for (int i = 0; i < COUNT; i++) inner[i] = new GameObject().transform;
            return new BoneArray(inner);
        }
    }
}
