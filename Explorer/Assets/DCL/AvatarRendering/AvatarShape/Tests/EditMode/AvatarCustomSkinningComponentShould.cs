using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarCustomSkinningComponentShould
    {
        private const string COMPUTE_SHADER_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/ComputeShaderSkinning_TestAsset.compute";
        private const int VERT_COUNT = 64;

        private IAvatarMaterialPoolHandler materialPoolHandler = null!;
        private IObjectPool<UnityEngine.ComputeShader> computeShaderPool = null!;
        private UnityEngine.ComputeShader computeShader = null!;
        private NativeArray<float4x4> bonesResult;

        [SetUp]
        public void SetUp()
        {
            materialPoolHandler = Substitute.For<IAvatarMaterialPoolHandler>();
            computeShaderPool = Substitute.For<IObjectPool<UnityEngine.ComputeShader>>();

            computeShader = AssetDatabase.LoadAssetAtPath<UnityEngine.ComputeShader>(COMPUTE_SHADER_TEST_ASSET_PATH);
            Assert.IsNotNull(computeShader, $"Could not load the compute shader test asset from {COMPUTE_SHADER_TEST_ASSET_PATH}");

            bonesResult = new NativeArray<float4x4>(ComputeShaderConstants.MAX_BONE_COUNT, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            bonesResult.Dispose();
        }

        /// <summary>
        ///     A re-instantiation releases the skinning component before building its replacement. When the replacement
        ///     fails, the released component is what the skinning dispatch finds, and writing the bones into its released
        ///     buffer throws a NullReferenceException from the native side.
        /// </summary>
        [Test]
        public void ReportAnErrorInsteadOfThrowingWhenSkinningADisposedComponent()
        {
            // Arrange
            AvatarCustomSkinningComponent component = NewComponent();
            component.Dispose(materialPoolHandler, computeShaderPool);

            // Act
            Result result = component.ComputeSkinning(bonesResult, GlobalJobArrayIndex.ValidUnsafe(0));

            // Assert
            Assert.IsFalse(result.Success, "a disposed component must report the dispatch as failed instead of touching its released buffers");
        }

        private AvatarCustomSkinningComponent NewComponent()
        {
            var bones = new ComputeBuffer(ComputeShaderConstants.BASE_BONE_COUNT, Unsafe.SizeOf<float4x4>());
            var buffers = new AvatarCustomSkinningComponent.Buffers(bones, kernel: 0);

            return new AvatarCustomSkinningComponent(VERT_COUNT, ComputeShaderConstants.BASE_BONE_COUNT, buffers,
                AvatarCustomSkinningComponent.USED_SLOTS_POOL.Get(), computeShader, new Bounds());
        }
    }
}
